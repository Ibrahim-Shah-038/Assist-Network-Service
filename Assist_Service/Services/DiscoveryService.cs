using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;

namespace Assist_Service.Services
{
    public class DiscoveryService
    {
        private readonly UdpClient _udpClient;
        private readonly NodeConfig _nodeConfig;
        private readonly List<Peer> _peers;
        private readonly object _peersLock;
        private bool _isRunning;

        public const int BroadcastPort = 12345;
        public const string DiscoveryMessage = "DISCOVER";
        public const string AcknowledgeMessage = "ACK";
        public const string LeaveMessage = "LEAVE";
        public static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.250");

        private readonly Logging Logger = new Logging();

        string macAddress = NetworkHelper.GetMacAddress();
        string ipv4 = NetworkHelper.GetLocalIPv4(); // helper method you’ll add

        public DiscoveryService(UdpClient udpClient, NodeConfig nodeConfig, List<Peer> peers, object peersLock)
        {
            _udpClient = udpClient;
            _nodeConfig = nodeConfig;
            _peers = peers;
            _peersLock = peersLock;
        }

        public void Start()
        {
            _isRunning = true;

            Thread discoveryThread = new Thread(DiscoverPeers) { IsBackground = true };
            discoveryThread.Start();

            Thread listenerThread = new Thread(ListenForMessages) { IsBackground = true };
            listenerThread.Start();

            Thread cleanupThread = new Thread(CleanupPeers) { IsBackground = true };
            cleanupThread.Start();
        }

        public void Stop()
        {
            try
            {
                string token = SecurityHelper.GenerateToken(LeaveMessage);
                string message = $"{LeaveMessage}:{_nodeConfig.NodeName}:{ipv4}:{token}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(bytes, bytes.Length, new IPEndPoint(MulticastAddress, BroadcastPort));
                Logger.Log($"Sent LEAVE message to peers: {_nodeConfig.NodeName}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[Stop] Failed to send LEAVE message: {ex.Message}");
            }

            _isRunning = false;
        }

        private void DiscoverPeers()
        {
           
            while (_isRunning)
            {
                try
                {
                    string token = SecurityHelper.GenerateToken(DiscoveryMessage);
                    
                    string message = $"{DiscoveryMessage}:{_nodeConfig.NodeName}:{ipv4}:{macAddress}:{token}";
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    _udpClient.Send(bytes, bytes.Length, new IPEndPoint(MulticastAddress, BroadcastPort));

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[DiscoverPeers] Error: {ex.Message}");
                }
            }
        }

        private void ListenForMessages()
        {
            while (_isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(bytes);
                    Logger.Log($"Received message from {remoteEP}: {message}");

                    string[] parts = message.Split(':');
                    if (parts.Length < 4) continue;

                    string messageType = parts[0];
                    string peerName = parts[1];
                    string ipAddress = parts[2];
                    string macAddress = parts[3];
                    string token = parts[4];

                    switch (messageType)
                    {
                        case DiscoveryMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(DiscoveryMessage, token))
                            {
                                UpdatePeer(peerName, remoteEP, ipAddress, macAddress);
                                UpdatePeerAndPersist(peerName, remoteEP, ipAddress, macAddress);
                                SendAcknowledgment(peerName, remoteEP);
                            }
                            break;

                        case AcknowledgeMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(AcknowledgeMessage, token))
                            {
                                UpdatePeer(peerName, remoteEP, ipAddress, macAddress);
                                UpdatePeerAndPersist(peerName, remoteEP, ipAddress, macAddress);
                            }
                            break;

                        case LeaveMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(LeaveMessage, token))
                            {
                                HandleGracefulLeave(peerName);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ListenForMessages] Exception: {ex.Message}");
                }
            }
        }

        private Peer UpdatePeer(string peerName, IPEndPoint endpoint, string ipAddress, string macAddress)
        {
            lock (_peersLock)
            {
                var existingPeer = _peers.FirstOrDefault(p =>
                    p.EndPoint.Address.Equals(endpoint.Address));

                if (existingPeer != null)
                {
                    existingPeer.NodeName = peerName;
                    existingPeer.IPAddress = ipAddress; // NEW
                    existingPeer.MacAddress = macAddress;
                    existingPeer.LastSeen = DateTime.UtcNow;
                    existingPeer.MissedHeartbeats = 0;
                    existingPeer.LeftGracefully = false;
                    existingPeer.Status = "Online";

                    return existingPeer;
                }
                else
                {
                    var newPeer = new Peer
                    {
                        NodeName = peerName,
                        EndPoint = endpoint,
                        IPAddress = ipAddress, // NEW
                        MacAddress = macAddress,
                        Status = "Online",
                        LastSeen = DateTime.UtcNow
                    };

                    _peers.Add(newPeer);

                    Logger.Log($"[UpdatePeer] New peer discovered: {peerName} @ {ipAddress}:{endpoint.Port} (MAC: {macAddress})");

                    return newPeer;
                }
            }
        }

        // Extended version: updates in-memory + persists
        private void UpdatePeerAndPersist(string peerName, IPEndPoint endpoint, string ipAddress, string macAddress)
        {
            var peer = UpdatePeer(peerName, endpoint, ipAddress, macAddress);
            PeerFileStorage.UpdateAndSavePeer(peer);
        }

        private void SendAcknowledgment(string peerName, IPEndPoint remoteEP)
        {
            string token = SecurityHelper.GenerateToken(AcknowledgeMessage);
            string message = $"{AcknowledgeMessage}:{_nodeConfig.NodeName}:{ipv4}:{macAddress}:{token}";
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            _udpClient.Send(bytes, bytes.Length, remoteEP);
        }

        private void HandleGracefulLeave(string peerName)
        {
            lock (_peersLock)
            {
                var peer = _peers.FirstOrDefault(p => p.NodeName == peerName);
                if (peer != null)
                {
                    peer.LeftGracefully = true;
                    Logger.Log($"Peer {peerName} has gracefully left the network.");
                }
            }
        }

        private void CleanupPeers()
        {
            while (_isRunning)
            {
                lock (_peersLock)
                {
                    foreach (var peer in _peers.ToList())
                    {
                        if (peer.NodeName == _nodeConfig.NodeName || peer.LeftGracefully)
                            continue;

                        double secondsSinceLastSeen = (DateTime.UtcNow - peer.LastSeen).TotalSeconds;

                        if (secondsSinceLastSeen > 10)
                        {
                            peer.MissedHeartbeats++;

                            Logger.Log($"Peer {peer.NodeName} missed heartbeat #{peer.MissedHeartbeats}.");

                            if (peer.MissedHeartbeats >= 5)
                            {
                                Logger.Log($"Removing peer {peer.NodeName} after {peer.MissedHeartbeats} missed heartbeats (likely disconnected).");
                                _peers.Remove(peer);
                            }
                        }
                    }
                }

                Thread.Sleep(10000); // Run cleanup every 10 seconds
            }
        }

        
    }
}
