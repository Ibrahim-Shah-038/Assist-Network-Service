using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Assist_Service.Models;
using System.Reflection;

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
            // Always ensure we save the complete list, not just one peer
            string json = JsonSerializer.Serialize(peers, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }

        // Update or add a peer and save immediately
        public static void UpdateAndSavePeer(Peer peer)
        {
            var peers = LoadPeersFromJson();

            var existing = peers.FirstOrDefault(p =>
                p.EndPoint?.Address.Equals(peer.EndPoint?.Address) == true &&
                p.EndPoint?.Port == peer.EndPoint?.Port);

            if (existing != null)
            {
                existing.NodeName = peer.NodeName;
                existing.MacAddress = peer.MacAddress;
                existing.LastSeen = peer.LastSeen;
                existing.Status = peer.Status;
                existing.LeftGracefully = peer.LeftGracefully;
                existing.MissedHeartbeats = peer.MissedHeartbeats;
            }
            else
            {
                peers.Add(peer);
            }

            SavePeersToJson(peers);
        }
    }
}
