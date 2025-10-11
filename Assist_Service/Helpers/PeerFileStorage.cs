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

        // Save full peer list (replace file but keep old peers intact)
        public static void SavePeersToJson(List<Peer> peers)
        {
            string json = JsonSerializer.Serialize(peers, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }

        // Update or add a peer and save immediately
        public static void UpdateAndSavePeer(Peer peer)
        {
            // Always refresh IPAddress from EndPoint before saving
            if (peer.EndPoint != null)
            {
                peer.IPAddress = peer.EndPoint.Address.ToString();
            }

            var peers = LoadPeersFromJson();

            // ✅ First, try to find an existing peer by MAC address (unique device identity)
            Peer existing = null;
            if (!string.IsNullOrEmpty(peer.MacAddress))
            {
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.MacAddress) &&
                    p.MacAddress.Equals(peer.MacAddress, StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                // ✅ If NodeName changed, update it — don't add a new peer
                if (!string.Equals(existing.NodeName, peer.NodeName, StringComparison.OrdinalIgnoreCase))
                {
                    existing.NodeName = peer.NodeName;
                }

                // ✅ Update latest network and status info
                existing.IPAddress = peer.IPAddress;
                existing.LastSeen = peer.LastSeen;
                existing.Status = peer.Status;
                existing.LeftGracefully = peer.LeftGracefully;
                existing.MissedHeartbeats = peer.MissedHeartbeats;

                if (peer.EndPoint != null)
                    existing.EndPoint = peer.EndPoint;
            }
            else
            {
                // ✅ Fallback: check by NodeName (if MAC missing)
                existing = peers.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.NodeName) &&
                    p.NodeName.Equals(peer.NodeName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update Node info if found by name
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
                    // ✅ If completely new MAC and NodeName, add as new peer
                    peers.Add(peer);
                }
            }

            SavePeersToJson(peers);
        }
    }
}
