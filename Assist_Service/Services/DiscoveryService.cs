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
        public static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.250");

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
            Thread discoveryThread = new Thread(DiscoverPeers);
            discoveryThread.IsBackground = true;
            discoveryThread.Start();

            Thread listenerThread = new Thread(ListenForMessages);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        public void Stop()
        {
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
                    // Log error
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

                    string[] parts = message.Split(':');
                    if (parts.Length < 2) continue;

                    string messageType = parts[0];
                    string token = parts[parts.Length - 1];

                    switch (messageType)
                    {
                        case DiscoveryMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(DiscoveryMessage, token))
                            {
                                string peerName = parts[1];
                                UpdatePeer(peerName, remoteEP);
                                SendAcknowledgment(peerName, remoteEP);
                            }
                            break;

                        case AcknowledgeMessage:
                            if (parts.Length >= 3 && SecurityHelper.ValidateToken(AcknowledgeMessage, token))
                            {
                                string peerName = parts[1];
                                UpdatePeer(peerName, remoteEP);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Log error
                }
            }
        }

        private void UpdatePeer(string peerName, IPEndPoint endpoint)
        {
            lock (_peersLock)
            {
                try
                {
                    var existingPeer = _peers.FirstOrDefault(p =>
                        p.EndPoint.Address.Equals(endpoint.Address) &&
                        p.EndPoint.Port == endpoint.Port);

                    if (existingPeer != null)
                    {
                        existingPeer.NodeName = peerName;
                        existingPeer.LastSeen = DateTime.UtcNow;
                    }
                    else
                    {
                        _peers.Add(new Peer
                        {
                            NodeName = peerName,
                            EndPoint = endpoint,
                            LastSeen = DateTime.UtcNow
                        });
                    }

                    _peers.RemoveAll(p => (DateTime.UtcNow - p.LastSeen).TotalSeconds > 30);
                }
                catch (Exception ex)
                {
                    // Log error
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
    }
}