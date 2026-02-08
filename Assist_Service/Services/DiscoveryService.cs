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
using System.Net.NetworkInformation;

namespace Assist_Service.Services
{
    public class DiscoveryService
    {
        public UdpClient _udpClient;
        public readonly NodeConfig _nodeConfig;
        public List<Peer> _peers;
        public readonly object _peersLock;
        private bool _isRunning;

        public const int BroadcastPort = 12345;
        public const string DiscoveryMessage = "DISCOVER";
        public const string AcknowledgeMessage = "ACK";
        public const string LeaveMessage = "LEAVE";
        public static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.250");
        private static readonly TimeSpan RejoinIgnoreWindow = TimeSpan.FromSeconds(60);

        private readonly Logging Logger = new Logging();
        private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(10);

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

            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            

            Thread discoveryThread = new Thread(DiscoverPeers) { IsBackground = true };
            discoveryThread.Start();

            Thread listenerThread = new Thread(ListenForMessages) { IsBackground = true };
            listenerThread.Start();

            Thread cleanupThread = new Thread(CleanupPeers)
            {
                IsBackground = true
            };
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
                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
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
                    if (_peers == null)
                        _peers = new List<Peer>();

                    // -------------------------------
                    // 1️⃣ Merge peers.json into memory (MAC ONLY)
                    // -------------------------------
                    if (File.Exists(_peersFile))
                    {
                        var json = File.ReadAllText(_peersFile);
                        var filePeers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();

                        foreach (var filePeer in filePeers)
                        {
                            var existing = _peers.FirstOrDefault(p =>
                                !string.IsNullOrEmpty(p.MacAddress) &&
                                p.MacAddress.Equals(filePeer.MacAddress, StringComparison.OrdinalIgnoreCase));

                            if (existing == null)
                            {
                                _peers.Add(filePeer);
                            }
                            else
                            {
                                // Sync persisted state (DO NOT overwrite identity)
                                existing.IPAddress = filePeer.IPAddress;
                                existing.Status = filePeer.Status;
                                existing.LastSeen = filePeer.LastSeen;
                                existing.LeftGracefully = filePeer.LeftGracefully;
                                existing.LeftGracefullyAt = filePeer.LeftGracefullyAt;
                                existing.MissedHeartbeats = filePeer.MissedHeartbeats;
                            }
                        }
                    }

                    // -------------------------------
                    // 2️⃣ SELF-PROTECTION (MANDATORY)
                    // -------------------------------
                    if (!string.IsNullOrEmpty(macAddress) &&
                        macAddress.Equals(this.macAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        // Never update self from network
                        return null;
                    }

                    // -------------------------------
                    // 3️⃣ Find peer by MAC ONLY (CRITICAL FIX)
                    // -------------------------------
                    var peer = _peers.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.MacAddress) &&
                        p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase));

                    if (peer != null)
                    {
                        bool wasOffline = peer.Status != "Online";

                        // -------------------------------
                        // 4️⃣ Graceful leave handling (UNCHANGED)
                        // -------------------------------
                        if (peer.LeftGracefully)
                        {
                            if (peer.LeftGracefullyAt.HasValue)
                            {
                                var elapsed = DateTime.UtcNow - peer.LeftGracefullyAt.Value;
                                if (elapsed < RejoinIgnoreWindow)
                                {
                                    Console.WriteLine(
                                        $"[UpdatePeer] Ignoring update for {peer.NodeName} — LeftGracefully within ignore window ({elapsed.TotalSeconds:N1}s).");
                                    return peer;
                                }

                                Console.WriteLine(
                                    $"[UpdatePeer] LeftGracefully window expired for {peer.NodeName}. Allowing rejoin.");
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"[UpdatePeer] Ignoring update for {peer.NodeName} — LeftGracefully with no timestamp.");
                                return peer;
                            }

                            peer.LeftGracefully = false;
                            peer.LeftGracefullyAt = null;
                        }

                        // -------------------------------
                        // 5️⃣ SAFE RENAME SUPPORT
                        // -------------------------------
                        if (!string.Equals(peer.NodeName, peerName, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(
                                $"[UpdatePeer] Rename detected: {peer.NodeName} → {peerName}");
                            peer.NodeName = peerName;
                        }

                        // -------------------------------
                        // 6️⃣ Update runtime state
                        // -------------------------------
                        peer.IPAddress = ipAddress;
                        peer.LastSeen = DateTime.UtcNow;
                        peer.Status = "Online";
                        peer.MissedHeartbeats = 0;
                        peer.EndPoint = endpoint;

                        if (wasOffline)
                            Console.WriteLine($"[UpdatePeer] Peer {peer.NodeName} is back online.");
                    }
                    else
                    {
                        // -------------------------------
                        // 7️⃣ New peer (MAC not seen before)
                        // -------------------------------
                        peer = new Peer
                        {
                            NodeName = peerName,
                            EndPoint = endpoint,
                            IPAddress = ipAddress,
                            MacAddress = macAddress,
                            Status = "Online",
                            LastSeen = DateTime.UtcNow,
                            MissedHeartbeats = 0,
                            LeftGracefully = false,
                            LeftGracefullyAt = null
                        };

                        _peers.Add(peer);
                        Console.WriteLine($"[UpdatePeer] New peer discovered: {peerName} @ {ipAddress}");
                    }

                    // -------------------------------
                    // 8️⃣ Persist state
                    // -------------------------------
                    File.WriteAllText(_peersFile, JsonConvert.SerializeObject(_peers, Formatting.Indented));

                    return peer;
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
                string originalNodeName = FindOriginalNodeName(macAddress, peerName);
                Console.WriteLine($"[UpdatePeerAndPersist] Processing: {peerName}, Original: {originalNodeName}, MAC: {macAddress}");

                List<Peer> filePeers = new List<Peer>();
                if (File.Exists(_peersFile))
                {
                    var json = File.ReadAllText(_peersFile);
                    filePeers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();
                }

                var existingPeer = filePeers.FirstOrDefault(p =>
                    (!string.IsNullOrEmpty(p.MacAddress) &&
                     p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(p.NodeName) &&
                     p.NodeName.Equals(peerName, StringComparison.OrdinalIgnoreCase)));

                if (existingPeer != null)
                {
                    bool wasOffline = existingPeer.Status != "Online";

                    if (existingPeer.LeftGracefully)
                    {
                        if (existingPeer.LeftGracefullyAt.HasValue)
                        {
                            var elapsed = DateTime.UtcNow - existingPeer.LeftGracefullyAt.Value;
                            if (elapsed < RejoinIgnoreWindow)
                            {
                                Console.WriteLine($"[UpdatePeerAndPersist] Skipping update for '{peerName}' — LeftGracefully and within ignore window ({elapsed.TotalSeconds:N1}s).");
                                return;
                            }

                            Console.WriteLine($"[UpdatePeerAndPersist] LeftGracefully window expired for '{peerName}' (elapsed {elapsed.TotalSeconds:N1}s). Allowing rejoin.");
                        }
                        else
                        {
                            Console.WriteLine($"[UpdatePeerAndPersist] Skipping update for '{peerName}' — LeftGracefully=true but no timestamp.");
                            return;
                        }

                        // Allow rejoin: reset
                        existingPeer.LeftGracefully = false;
                        existingPeer.LeftGracefullyAt = null;
                    }

                    existingPeer.NodeName = peerName;
                    existingPeer.MacAddress = macAddress;
                    existingPeer.IPAddress = ipAddress;
                    existingPeer.EndPoint = endpoint;
                    existingPeer.LastSeen = DateTime.UtcNow;
                    existingPeer.Status = "Online";
                    existingPeer.MissedHeartbeats = 0;

                    Console.WriteLine($"[UpdatePeerAndPersist] Updated existing peer '{peerName}' -> Status=Online.");
                }
                else
                {
                    var newPeer = new Peer
                    {
                        NodeName = peerName,
                        EndPoint = endpoint,
                        IPAddress = ipAddress,
                        MacAddress = macAddress,
                        Status = "Online",
                        LastSeen = DateTime.UtcNow,
                        LeftGracefully = false,
                        LeftGracefullyAt = null,
                        MissedHeartbeats = 0
                    };

                    filePeers.Add(newPeer);
                    Console.WriteLine($"[UpdatePeerAndPersist] Added new peer '{peerName}' to list.");
                }

                File.WriteAllText(_peersFile, JsonConvert.SerializeObject(filePeers, Formatting.Indented));

                Console.WriteLine($"[UpdatePeerAndPersist] Successfully persisted peers.json. Total: {filePeers.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdatePeerAndPersist] ERROR: {ex}");
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

        //NETWORK CHANGE LOGIC
        private void OnNetworkChanged(object sender, EventArgs e)
        {
            try
            {
                Logger.Log("[Network] Network change detected. Rebinding UDP socket.");

                lock (_peersLock)
                {
                    _udpClient?.Close();

                    _udpClient = new UdpClient();
                    _udpClient.ExclusiveAddressUse = false;
                    _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));

                    _udpClient.JoinMulticastGroup(MulticastAddress);
                }

                // Immediately announce presence
                SendImmediateDiscovery();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Network] Rebind failed: {ex.Message}");
            }
        }

        private void SendImmediateDiscovery()
        {
            try
            {
                string token = SecurityHelper.GenerateToken(DiscoveryMessage);
                string message = $"{DiscoveryMessage}:{_nodeConfig.NodeName}:{ipv4}:{macAddress}:{token}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);

                _udpClient.Send(bytes, bytes.Length, new IPEndPoint(MulticastAddress, BroadcastPort));

                Logger.Log("[Discovery] Immediate DISCOVER sent after network change.");
            }
            catch (Exception ex)
            {
                Logger.Log($"[Discovery] Immediate send failed: {ex.Message}");
            }
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

        /*private void CleanupPeers()
        {
            while (_isRunning)
            {
                lock (_peersLock)
                {
                    foreach (var peer in _peers)
                    {
                        // Skip self
                        if (peer.NodeName == _nodeConfig.NodeName)
                            continue;

                        // Skip peers that left gracefully (already handled elsewhere)
                        if (peer.LeftGracefully)
                            continue;

                        double secondsSinceLastSeen =
                            (DateTime.UtcNow - peer.LastSeen).TotalSeconds;

                        if (secondsSinceLastSeen > 10)
                        {
                            peer.MissedHeartbeats++;

                            Logger.Log(
                                $"Peer {peer.NodeName} missed heartbeat #{peer.MissedHeartbeats}."
                            );

                            // Instead of removing, mark as Offline
                            if (peer.MissedHeartbeats >= 5)
                            {
                                if (peer.Status != "Offline")
                                {
                                    peer.Status = "Offline";
                                    Logger.Log(
                                        $"Peer {peer.NodeName} marked as OFFLINE (no auto-removal)."
                                    );
                                }
                            }
                        }
                        else
                        {
                            // Peer is healthy again
                            peer.MissedHeartbeats = 0;
                            peer.Status = "Online";
                        }
                    }
                }

                Thread.Sleep(10000);
            }
        }*/


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

        private void CleanupPeers()
        {
            while (_isRunning)
            {
                try
                {
                    bool changed = false;

                    lock (_peersLock)
                    {
                        foreach (var peer in _peers)
                        {
                            // Skip self
                            if (peer.NodeName == _nodeConfig.NodeName)
                                continue;

                            // Graceful leave already handled
                            if (peer.LeftGracefully)
                                continue;

                            var timeSinceLastSeen = DateTime.UtcNow - peer.LastSeen;

                            if (timeSinceLastSeen > OfflineThreshold)
                            {
                                if (peer.Status != "Offline")
                                {
                                    peer.Status = "Offline";
                                    peer.MissedHeartbeats++;
                                    changed = true;

                                    Logger.Log(
                                        $"[Cleanup] Peer {peer.NodeName} marked OFFLINE (last seen {timeSinceLastSeen.TotalSeconds:N0}s ago)."
                                    );
                                }
                            }
                            else
                            {
                                // Peer revived
                                if (peer.Status != "Online")
                                {
                                    peer.Status = "Online";
                                    peer.MissedHeartbeats = 0;
                                    changed = true;

                                    Logger.Log(
                                        $"[Cleanup] Peer {peer.NodeName} is back ONLINE."
                                    );
                                }
                            }
                        }

                        // Persist only if something changed
                        if (changed)
                        {
                            File.WriteAllText(
                                _peersFile,
                                JsonConvert.SerializeObject(_peers, Formatting.Indented)
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Cleanup] Error: {ex.Message}");
                }

                Thread.Sleep(CleanupInterval);
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