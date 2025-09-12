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
        //private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peers.json");
        private static readonly string FilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");

        public static void SavePeersToJson(List<Peer> peers)
        {
            var peerDtos = peers.Select(p => new
            {
                NodeName = p.NodeName,
                IPAddress = p.EndPoint?.Address.ToString(),
                MacAddress = p.MacAddress,
                Status = p.LeftGracefully ? "Offline" : "Online",
                LastSeen = p.LastSeen.ToString("o") // ISO format
            });

            string json = JsonSerializer.Serialize(peerDtos, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }
    }
}
