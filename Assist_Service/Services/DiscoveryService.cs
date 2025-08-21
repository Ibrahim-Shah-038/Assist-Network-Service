using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
                string message = $"{LeaveMessage}:{_nodeConfig.NodeName}:{token}";
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
                    string message = $"{DiscoveryMessage}:{_nodeConfig.NodeName}:{token}";
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
                    if (parts.Length < 2) continue;

                    string messageType = parts[0];
                    string token = parts[parts.Length - 1];
                    string peerName = parts[1];

                    switch (messageType)
                    {
                        case DiscoveryMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(DiscoveryMessage, token))
                            {
                                UpdatePeer(peerName, remoteEP);
                                SendAcknowledgment(peerName, remoteEP);
                            }
                            break;

                        case AcknowledgeMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(AcknowledgeMessage, token))
                            {
                                UpdatePeer(peerName, remoteEP);
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

        private void UpdatePeer(string peerName, IPEndPoint endpoint)
        {
            lock (_peersLock)
            {
                var existingPeer = _peers.FirstOrDefault(p =>
                    p.EndPoint.Address.Equals(endpoint.Address) &&
                    p.EndPoint.Port == endpoint.Port);

                if (existingPeer != null)
                {
                    existingPeer.NodeName = peerName;
                    existingPeer.LastSeen = DateTime.UtcNow;
                    existingPeer.MissedHeartbeats = 0;
                    existingPeer.LeftGracefully = false;
                }
                else
                {
                    _peers.Add(new Peer
                    {
                        NodeName = peerName,
                        EndPoint = endpoint,
                        LastSeen = DateTime.UtcNow
                    });

                    Logger.Log($"[UpdatePeer] New peer discovered: {peerName} @ {endpoint}");
                }
            }
        }

        private void SendAcknowledgment(string peerName, IPEndPoint remoteEP)
        {
            string token = SecurityHelper.GenerateToken(AcknowledgeMessage);
            string message = $"{AcknowledgeMessage}:{_nodeConfig.NodeName}:{token}";
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
