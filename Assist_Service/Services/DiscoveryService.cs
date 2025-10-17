using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Newtonsoft.Json;

namespace Assist_Service.Services
{
    public class DiscoveryService
    {
        private readonly UdpClient _udpClient;
        private readonly NodeConfig _nodeConfig;
        private  List<Peer> _peers;
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
        private readonly string _peersFile = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "peers.json"
        );

        private List<Peer> __peers = new List<Peer>();

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

        public Peer UpdatePeer(string peerName, IPEndPoint endpoint, string ipAddress, string macAddress, string originalNodeName = null)
        {
            lock (_peersLock)
            {
                try
                {
                    // Ensure _peers is always initialized
                    if (_peers == null)
                    {
                        _peers = new List<Peer>();
                    }

                    // Load from file to sync with latest data
                    if (File.Exists(_peersFile))
                    {
                        var json = File.ReadAllText(_peersFile);
                        var filePeers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();

                        // Merge file data with current _peers to ensure consistency
                        foreach (var filePeer in filePeers)
                        {
                            var existingInMemory = _peers.FirstOrDefault(p =>
                                (!string.IsNullOrEmpty(p.MacAddress) && !string.IsNullOrEmpty(filePeer.MacAddress) &&
                                 p.MacAddress.Equals(filePeer.MacAddress, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(p.NodeName) && !string.IsNullOrEmpty(filePeer.NodeName) &&
                                 p.NodeName.Equals(filePeer.NodeName, StringComparison.OrdinalIgnoreCase)));

                            if (existingInMemory == null)
                            {
                                _peers.Add(filePeer);
                            }
                            else
                            {
                                // Update existing in-memory peer with file data
                                existingInMemory.NodeName = filePeer.NodeName;
                                existingInMemory.IPAddress = filePeer.IPAddress;
                                existingInMemory.MacAddress = filePeer.MacAddress;
                                existingInMemory.Status = filePeer.Status;
                                existingInMemory.LastSeen = filePeer.LastSeen;
                                existingInMemory.LeftGracefully = filePeer.LeftGracefully;
                                existingInMemory.MissedHeartbeats = filePeer.MissedHeartbeats;
                                existingInMemory.EndPoint = filePeer.EndPoint;
                            }
                        }
                    }

                    Peer existingPeer = null;

                    // STRATEGY 1: Find by MAC address first (most reliable for renames)
                    if (!string.IsNullOrEmpty(macAddress))
                    {
                        existingPeer = _peers.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.MacAddress) &&
                            p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase));
                    }

                    // STRATEGY 2: Find by original node name (if provided for renames)
                    if (existingPeer == null && !string.IsNullOrEmpty(originalNodeName))
                    {
                        existingPeer = _peers.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.NodeName) &&
                            p.NodeName.Equals(originalNodeName, StringComparison.OrdinalIgnoreCase));
                    }

                    // STRATEGY 3: Find by current node name (fallback)
                    if (existingPeer == null)
                    {
                        existingPeer = _peers.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.NodeName) &&
                            p.NodeName.Equals(peerName, StringComparison.OrdinalIgnoreCase));
                    }

                    // Use peer-reported IP (not endpoint.Address, which may be 127.0.0.1)
                    string newIp = ipAddress;

                    if (existingPeer != null)
                    {
                        string oldName = existingPeer.NodeName;
                        bool wasRenamed = !string.Equals(oldName, peerName, StringComparison.OrdinalIgnoreCase);

                        if (existingPeer.IPAddress != newIp)
                        {
                            Console.WriteLine($"[UpdatePeer] Peer {oldName} updated IP: {existingPeer.IPAddress} -> {newIp}");
                            existingPeer.IPAddress = newIp;
                        }

                        if (wasRenamed)
                        {
                            Console.WriteLine($"[UpdatePeer] Peer renamed: {oldName} -> {peerName}");
                        }

                        existingPeer.NodeName = peerName;
                        existingPeer.MacAddress = macAddress;
                        existingPeer.LastSeen = DateTime.UtcNow;
                        existingPeer.Status = "Online";
                        existingPeer.EndPoint = endpoint;
                        existingPeer.MissedHeartbeats = 0;
                        existingPeer.LeftGracefully = false;
                    }
                    else
                    {
                        var newPeer = new Peer
                        {
                            NodeName = peerName,
                            EndPoint = endpoint,
                            IPAddress = newIp,
                            MacAddress = macAddress,
                            Status = "Online",
                            LastSeen = DateTime.UtcNow,
                            MissedHeartbeats = 0,
                            LeftGracefully = false
                        };
                        _peers.Add(newPeer);

                        Console.WriteLine($"[UpdatePeer] New peer discovered: {peerName} @ {newIp}:{endpoint.Port} (MAC: {macAddress})");
                        existingPeer = newPeer;
                    }

                    // Save to file and ensure _peers is consistent
                    var updatedJson = JsonConvert.SerializeObject(_peers, Formatting.Indented);
                    File.WriteAllText(_peersFile, updatedJson);

                    Console.WriteLine($"[UpdatePeer] Successfully updated _peers list. Total peers: {_peers.Count}");
                    return existingPeer;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UpdatePeer] Error: {ex.Message}");
                    return null;
                }
            }
        }



        // Extended version: updates in-memory + persists
        // Extended version: updates in-memory + persists with original identifiers
        private void UpdatePeerAndPersist(string peerName, IPEndPoint endpoint, string ipAddress, string macAddress)
        {
            try
            {
                // First, try to find the original node name before updating
                string originalNodeName = FindOriginalNodeName(macAddress, peerName);

                Console.WriteLine($"[UpdatePeerAndPersist] Processing: {peerName}, Original: {originalNodeName}, MAC: {macAddress}");

                var peer = UpdatePeer(peerName, endpoint, ipAddress, macAddress, originalNodeName);

                if (peer != null)
                {
                    // Pass original identifiers to prevent duplicates during renames
                    PeerFileStorage.UpdateAndSavePeer(peer, originalMacAddress: macAddress, originalNodeName: originalNodeName);

                    Console.WriteLine($"[UpdatePeerAndPersist] Successfully processed peer: {peer.NodeName}");
                }
                else
                {
                    Console.WriteLine($"[UpdatePeerAndPersist] Failed to update peer: {peerName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdatePeerAndPersist] Error: {ex.Message}");
            }
        }

        private string FindOriginalNodeName(string macAddress, string currentPeerName)
        {
            try
            {
                if (File.Exists(_peersFile))
                {
                    var json = File.ReadAllText(_peersFile);
                    var existingPeers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();

                    // Try to find by MAC address first
                    if (!string.IsNullOrEmpty(macAddress))
                    {
                        var peerByMac = existingPeers.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.MacAddress) &&
                            p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase));

                        if (peerByMac != null && !string.Equals(peerByMac.NodeName, currentPeerName, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[FindOriginalNodeName] Found original name by MAC: {peerByMac.NodeName} -> {currentPeerName}");
                            return peerByMac.NodeName; // Return original name if different
                        }
                    }

                    // If MAC not found or same name, try to find by current name
                    var peerByCurrentName = existingPeers.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.NodeName) &&
                        p.NodeName.Equals(currentPeerName, StringComparison.OrdinalIgnoreCase));

                    if (peerByCurrentName != null)
                    {
                        Console.WriteLine($"[FindOriginalNodeName] Name unchanged: {currentPeerName}");
                        return peerByCurrentName.NodeName; // Name hasn't changed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FindOriginalNodeName] Error: {ex.Message}");
            }

            Console.WriteLine($"[FindOriginalNodeName] Using current name as original: {currentPeerName}");
            return currentPeerName; // Fallback to current name
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

        public List<Peer> GetCurrentPeers()
        {
            lock (_peersLock)
            {
                // Return a copy to avoid modification issues
                return _peers?.Select(p => new Peer
                {
                    NodeName = p.NodeName,
                    EndPoint = p.EndPoint,
                    IPAddress = p.IPAddress,
                    MacAddress = p.MacAddress,
                    Status = p.Status,
                    LastSeen = p.LastSeen,
                    LeftGracefully = p.LeftGracefully,
                    MissedHeartbeats = p.MissedHeartbeats
                }).ToList() ?? new List<Peer>();
            }
        }

        // Or if you want to return the actual reference (be careful with thread safety):
        public List<Peer> GetCurrentPeersReference()
        {
            lock (_peersLock)
            {
                return _peers ?? new List<Peer>();
            }
        }

    }
}
