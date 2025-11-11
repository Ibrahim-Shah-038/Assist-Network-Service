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

                    if (_peers == null)
                    {
                        PWR_Log.PWR_Log("[MarkPeerOffline] _peers is null -> initializing new list");
                        _peers = new List<Peer>();
                    }
                    else
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] _peers already initialized. Current count: {_peers.Count}");
                    }

                    // Load peers from file (merge)
                    if (File.Exists(FilePath))
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] peers file exists at: {FilePath}");

                        var json = File.ReadAllText(FilePath);
                        if (json == null)
                        {
                            PWR_Log.PWR_Log("[MarkPeerOffline] ReadFile returned null json");
                        }
                        else
                        {
                            PWR_Log.PWR_Log($"[MarkPeerOffline] ReadFile length: {json.Length} chars");
                            var previewLen = Math.Min(500, json.Length);
                            PWR_Log.PWR_Log($"[MarkPeerOffline] peers.json preview: {json.Substring(0, previewLen)}{(json.Length > previewLen ? "..." : "")}");
                        }

                        var filePeers = JsonConvert.DeserializeObject<List<Peer>>(json) ?? new List<Peer>();
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Deserialized filePeers count: {filePeers.Count}");

                        int idx = 0;
                        foreach (var filePeer in filePeers)
                        {
                            PWR_Log.PWR_Log($"[MarkPeerOffline] Inspecting filePeer index={idx}");

                            if (filePeer == null)
                            {
                                PWR_Log.PWR_Log($"[MarkPeerOffline] filePeer[{idx}] is null -> skipping");
                                idx++;
                                continue;
                            }

                            if (string.IsNullOrEmpty(filePeer.MacAddress))
                            {
                                PWR_Log.PWR_Log($"[MarkPeerOffline] filePeer[{idx}].MacAddress is null/empty -> skipping");
                                idx++;
                                continue;
                            }

                            bool alreadyExists = _peers.Any(p =>
                                p != null &&
                                !string.IsNullOrEmpty(p.MacAddress) &&
                                string.Equals(p.MacAddress, filePeer.MacAddress, StringComparison.OrdinalIgnoreCase));

                            if (!alreadyExists)
                            {
                                _peers.Add(filePeer);
                                PWR_Log.PWR_Log($"[MarkPeerOffline] Added filePeer[{idx}] to in-memory list. New _peers count: {_peers.Count}");
                            }

                            idx++;
                        }
                    }
                    else
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] peers file does not exist at: {FilePath}");
                    }

                    // Validate inputs before proceeding
                    if (string.IsNullOrEmpty(macAddress) && string.IsNullOrEmpty(nodeName))
                    {
                        PWR_Log.PWR_Log("[MarkPeerOffline] Both macAddress and nodeName are null/empty -> nothing to do. EXIT");
                        return;
                    }

                    PWR_Log.PWR_Log($"[MarkPeerOffline] Searching for peer. Current in-memory count: {_peers.Count}");

                    // Find target peer safely
                    var peer = _peers.FirstOrDefault(p =>
                        p != null &&
                        (
                            (!string.IsNullOrEmpty(macAddress) &&
                             !string.IsNullOrEmpty(p.MacAddress) &&
                             string.Equals(p.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase))
                            ||
                            (!string.IsNullOrEmpty(nodeName) &&
                             !string.IsNullOrEmpty(p.NodeName) &&
                             string.Equals(p.NodeName, nodeName, StringComparison.OrdinalIgnoreCase))
                        ));

                    if (peer != null)
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Found peer. NodeName='{peer.NodeName ?? "null"}', MacAddress='{peer.MacAddress ?? "null"}', Status='{peer.Status ?? "null"}'");

                        // --- KEEP EXISTING BEHAVIOR ---
                        peer.Status = "Offline";
                        peer.LeftGracefully = true;
                        peer.LastSeen = DateTime.UtcNow;

                        // --- NEW LOGIC: mark when it left gracefully ---
                        peer.LeftGracefullyAt = DateTime.UtcNow;

                        PWR_Log.PWR_Log($"[MarkPeerOffline] Marking LeftGracefullyAt={peer.LeftGracefullyAt:O}");

                        // Persist the updated list to disk
                        PWR_Log.PWR_Log("[MarkPeerOffline] Serializing updated _peers to JSON");
                        var updatedJson = JsonConvert.SerializeObject(_peers, Formatting.Indented);

                        // Safer atomic write
                        var tempFile = FilePath + ".tmp";
                        File.WriteAllText(tempFile, updatedJson);
                        File.Copy(tempFile, FilePath, true);
                        File.Delete(tempFile);

                        PWR_Log.PWR_Log($"[MarkPeerOffline] Persisted updated peers.json. Total peers saved: {_peers.Count}");
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Marked peer Offline - Node: {peer.NodeName}, MAC: {peer.MacAddress}");
                    }
                    else
                    {
                        PWR_Log.PWR_Log($"[MarkPeerOffline] Peer not found to mark Offline - Node: {nodeName ?? "null"}, MAC: {macAddress ?? "null"}");
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
