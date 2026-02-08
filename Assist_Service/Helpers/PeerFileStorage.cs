using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using Assist_Service.Models;
using Newtonsoft.Json; // ✅ Using only Newtonsoft.Json

namespace Assist_Service.Helpers
{

    public class PeerFileStorage
    {
        private static readonly string FilePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "peers.json"
        );

        //private static readonly object _peersLock;
        private static List<Peer> _peers = new List<Peer>();

        private readonly static Remote_Power_Log PWR_Log = new Remote_Power_Log();

        // ✅ Load existing peers
        public static List<Peer> LoadPeersFromJson()
        {
            if (!File.Exists(FilePath))
            {
                return new List<Peer>();
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                var peers = JsonConvert.DeserializeObject<List<Peer>>(json);
                return peers ?? new List<Peer>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoadPeersFromJson] Error reading file: {ex.Message}");
                return new List<Peer>();
            }
        }

        // ✅ Clear all nodes (empty file)
        public static void ClearAllNodes()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Console.WriteLine($"[ClearAllNodes] File not found: {FilePath}");
                    return;
                }

                File.WriteAllText(FilePath, "[]");
                Console.WriteLine("[ClearAllNodes] All node entries cleared successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClearAllNodes] Error while clearing nodes: {ex.Message}");
            }
        }

        // ✅ Save all peers back to file
        public static void SavePeersToJson(List<Peer> peers)
        {
            try
            {
                string json = JsonConvert.SerializeObject(peers, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SavePeersToJson] Error saving peers: {ex.Message}");
            }
        }

        // ✅ Add or update a peer
        public static void UpdateAndSavePeer(Peer peer, string originalMacAddress = null, string originalNodeName = null)
        {
            if (peer == null) return;

            if (peer.EndPoint != null)
                peer.IPAddress = peer.EndPoint.Address.ToString();

            var peers = LoadPeersFromJson();
            Peer existing = null;

            // Search by original MAC
            if (!string.IsNullOrEmpty(originalMacAddress))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.MacAddress) &&
                    p.MacAddress.Equals(originalMacAddress, StringComparison.OrdinalIgnoreCase));
            }

            // Search by original node name
            if (existing == null && !string.IsNullOrEmpty(originalNodeName))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.NodeName) &&
                    p.NodeName.Equals(originalNodeName, StringComparison.OrdinalIgnoreCase));
            }

            // Search by current MAC
            if (existing == null && !string.IsNullOrEmpty(peer.MacAddress))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.MacAddress) &&
                    p.MacAddress.Equals(peer.MacAddress, StringComparison.OrdinalIgnoreCase));
            }

            // Search by current node name
            if (existing == null && !string.IsNullOrEmpty(peer.NodeName))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.NodeName) &&
                    p.NodeName.Equals(peer.NodeName, StringComparison.OrdinalIgnoreCase));
            }

            // Update existing or add new peer
            if (existing != null)
            {
                existing.NodeName = peer.NodeName;
                existing.IPAddress = peer.IPAddress;
                existing.MacAddress = peer.MacAddress;
                existing.LastSeen = peer.LastSeen;
                existing.Status = peer.Status;
                existing.LeftGracefully = peer.LeftGracefully;
                existing.MissedHeartbeats = peer.MissedHeartbeats;

                if (peer.EndPoint != null)
                    existing.EndPoint = peer.EndPoint;
            }
            else
            {
                peers.Add(peer);
            }

            SavePeersToJson(peers);
        }

        // ✅ Mark a peer offline upon receiving a "goodbye" message
        public static void MarkPeerOffline(string macAddress, string nodeName)
        {
            PWR_Log.PWR_Log($"[MarkPeerOffline] ENTRY. macAddress='{macAddress ?? "null"}', nodeName='{nodeName ?? "null"}'");

            lock (Service1._peersLock)
            {
                try
                {
                    PWR_Log.PWR_Log("[MarkPeerOffline] Acquired lock");

                    // Ensure in-memory list exists
                    if (_peers == null)
                    {
                        PWR_Log.PWR_Log("[MarkPeerOffline] _peers is null -> initializing new list");
                        _peers = new List<Peer>();
                    }

                    // 🔒 LOAD FROM FILE ONLY ONCE (initial load)
                    if (_peers.Count == 0 && File.Exists(FilePath))
                    {
                        PWR_Log.PWR_Log("[MarkPeerOffline] Initial load from peers.json");

                        var json = File.ReadAllText(FilePath);
                        _peers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();

                        PWR_Log.PWR_Log($"[MarkPeerOffline] Loaded {_peers.Count} peers from file");
                    }

                    // Validate inputs
                    if (string.IsNullOrEmpty(macAddress) && string.IsNullOrEmpty(nodeName))
                    {
                        PWR_Log.PWR_Log("[MarkPeerOffline] Both macAddress and nodeName are null/empty -> EXIT");
                        return;
                    }

                    PWR_Log.PWR_Log($"[MarkPeerOffline] Searching peer in-memory. Count: {_peers.Count}");

                    Peer peer = null;

                    // ✅ PRIMARY MATCH: MAC ADDRESS (authoritative)
                    if (!string.IsNullOrEmpty(macAddress))
                    {
                        peer = _peers.FirstOrDefault(p =>
                            p != null &&
                            !string.IsNullOrEmpty(p.MacAddress) &&
                            string.Equals(p.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase));
                    }

                    // ✅ FALLBACK MATCH: NODE NAME (only if MAC not found)
                    if (peer == null && !string.IsNullOrEmpty(nodeName))
                    {
                        peer = _peers.FirstOrDefault(p =>
                            p != null &&
                            !string.IsNullOrEmpty(p.NodeName) &&
                            string.Equals(p.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (peer != null)
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Found peer. Node='{peer.NodeName}', MAC='{peer.MacAddress}', Status='{peer.Status}'");

                        // --- EXISTING BEHAVIOR (UNCHANGED) ---
                        peer.Status = "Offline";
                        peer.LeftGracefully = true;
                        peer.LastSeen = DateTime.UtcNow;
                        peer.LeftGracefullyAt = DateTime.UtcNow;

                        PWR_Log.PWR_Log($"[MarkPeerOffline] Marked Offline at {peer.LeftGracefullyAt:O}");

                        // Persist updated list
                        var updatedJson = JsonConvert.SerializeObject(_peers, Formatting.Indented);

                        var tempFile = FilePath + ".tmp";
                        File.WriteAllText(tempFile, updatedJson);
                        File.Copy(tempFile, FilePath, true);
                        File.Delete(tempFile);

                        PWR_Log.PWR_Log($"[MarkPeerOffline] Persisted peers.json. Total peers: {_peers.Count}");
                    }
                    else
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Peer NOT FOUND. Node='{nodeName ?? "null"}', MAC='{macAddress ?? "null"}'");
                    }
                }
                catch (Exception ex)
                {
                    PWR_Log.PWR_Log($"[MarkPeerOffline] ERROR: {ex}");
                }
                finally
                {
                    PWR_Log.PWR_Log("[MarkPeerOffline] Releasing lock and EXIT");
                }
            }
        }



    }
}
