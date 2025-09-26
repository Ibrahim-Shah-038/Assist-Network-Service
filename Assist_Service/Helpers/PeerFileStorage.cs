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

            // Match by NodeName or MacAddress (persistent identifiers)
            var existing = peers.FirstOrDefault(p =>
                (!string.IsNullOrEmpty(p.NodeName) && p.NodeName == peer.NodeName)
                || (!string.IsNullOrEmpty(p.MacAddress) && p.MacAddress == peer.MacAddress)
            );

            if (existing != null)
            {
                existing.NodeName = peer.NodeName;
                existing.IPAddress = peer.IPAddress;   // ✅ Always update to latest IP
                existing.MacAddress = peer.MacAddress;
                existing.LastSeen = peer.LastSeen;
                existing.Status = peer.Status;
                existing.LeftGracefully = peer.LeftGracefully;
                existing.MissedHeartbeats = peer.MissedHeartbeats;

                // Always update EndPoint as well
                if (peer.EndPoint != null)
                    existing.EndPoint = peer.EndPoint;
            }
            else
            {
                peers.Add(peer);
            }

            SavePeersToJson(peers);
        }
    }
}
