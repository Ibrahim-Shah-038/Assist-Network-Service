using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Assist_Service.Models;
using System.Reflection;
using System.Net;

namespace Assist_Service.Helpers
{
    public static class PeerFileStorage
    {
        private static readonly string FilePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "peers.json"
        );

        // Load existing peers
        public static List<Peer> LoadPeersFromJson()
        {
            if (!File.Exists(FilePath)) return new List<Peer>();

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<Peer>>(json) ?? new List<Peer>();
            }
            catch
            {
                return new List<Peer>();
            }
        }

        // Save full peer list (replace file)
        public static void SavePeersToJson(List<Peer> peers)
        {
            string json = JsonSerializer.Serialize(peers, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }

        // Update or add a peer and save immediately
        public static void UpdateAndSavePeer(Peer peer, string originalMacAddress = null, string originalNodeName = null)
        {
            if (peer.EndPoint != null)
                peer.IPAddress = peer.EndPoint.Address.ToString();

            var peers = LoadPeersFromJson();
            Peer existing = null;

            // Method 1: Use original MAC if provided
            if (!string.IsNullOrEmpty(originalMacAddress))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.MacAddress) &&
                    p.MacAddress.Equals(originalMacAddress, StringComparison.OrdinalIgnoreCase));
            }

            // Method 2: Use original NodeName if provided
            if (existing == null && !string.IsNullOrEmpty(originalNodeName))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.NodeName) &&
                    p.NodeName.Equals(originalNodeName, StringComparison.OrdinalIgnoreCase));
            }

            // Method 3: Fallback to current MAC
            if (existing == null && !string.IsNullOrEmpty(peer.MacAddress))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.MacAddress) &&
                    p.MacAddress.Equals(peer.MacAddress, StringComparison.OrdinalIgnoreCase));
            }

            // Method 4: Fallback to current NodeName
            if (existing == null && !string.IsNullOrEmpty(peer.NodeName))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.NodeName) &&
                    p.NodeName.Equals(peer.NodeName, StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                // Update all fields
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

        // ✅ New: Mark a peer offline by IP or MAC
        public static void MarkPeerOffline(string ipAddress, string macAddress = null)
        {
            var peers = LoadPeersFromJson();

            var peer = peers.FirstOrDefault(p =>
                (p.EndPoint?.Address.ToString() == ipAddress) ||
                (!string.IsNullOrEmpty(macAddress) &&
                 !string.IsNullOrEmpty(p.MacAddress) &&
                 p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase)));

            if (peer != null)
            {
                peer.Status = "Offline";
                peer.LastSeen = DateTime.Now;
                SavePeersToJson(peers);
            }
        }
    }
}
