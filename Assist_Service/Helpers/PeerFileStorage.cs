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
        public static void UpdatePeerStatusOnGoodbye(string macAddress, string nodeName)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    PWR_Log.PWR_Log($"[Service] peers.json file not found.");
                    return;
                }

                string jsonContent = File.ReadAllText(FilePath);
                var peers = JsonConvert.DeserializeObject<List<Peer>>(jsonContent);

                if (peers == null || peers.Count == 0)
                {
                    PWR_Log.PWR_Log($"[Service] No peers found in peers.json");
                    return;
                }

                // Find the peer by MAC address (case-insensitive)
                var peer = peers.FirstOrDefault(p =>
                    p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase));

                if (peer != null)
                {
                    peer.Status = "Offline";
                    peer.LeftGracefully = true;
                    peer.LastSeen = DateTime.UtcNow;

                    string updatedJson = JsonConvert.SerializeObject(peers, Formatting.Indented);
                    File.WriteAllText(FilePath, updatedJson);
                    UpdateAndSavePeer(peer, macAddress, nodeName);

                    PWR_Log.PWR_Log($"[Service] Updated peer status to Offline - Node: {nodeName}, MAC: {macAddress}");
                }
                else
                {
                    PWR_Log.PWR_Log($"[Service] Peer with MAC address {macAddress} not found in peers.json");
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[Service] Error updating peer status: {ex.Message}");
            }
        }
    }
}
