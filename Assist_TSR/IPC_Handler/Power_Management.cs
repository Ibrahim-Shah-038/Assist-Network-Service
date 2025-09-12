using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Timers;
using Assist_Service.Models; // for your existing Peer class

namespace Assist_Service.IPC_Handler
{
    public class Power_Management
    {
        private static readonly string DevFilePath = @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\peers.json";
        private static readonly string ProdFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");

        private readonly Timer nodeRefreshTimer;
        private List<Peer> cachedPeers = new List<Peer>();

        public event Action<List<Peer>> OnPeersUpdated;

        public Power_Management()
        {
            nodeRefreshTimer = new Timer(5000); // refresh every 5 seconds
            nodeRefreshTimer.Elapsed += (s, e) => RefreshPeers();
            nodeRefreshTimer.Start();

            RefreshPeers(); // initial load
        }

        private void RefreshPeers()
        {
            try
            {
                string filePath = File.Exists(ProdFilePath) ? ProdFilePath : DevFilePath;

                if (!File.Exists(filePath))
                    return;

                string json = File.ReadAllText(filePath);
                var peers = JsonSerializer.Deserialize<List<Peer>>(json);

                if (peers != null)
                {
                    cachedPeers = peers;
                    OnPeersUpdated?.Invoke(cachedPeers);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading peers.json: " + ex.Message);
            }
        }

        /// <summary>
        /// Returns the latest peer list (safe copy).
        /// </summary>
        public List<Peer> GetPeers()
        {
            return new List<Peer>(cachedPeers);
        }
    }
}
