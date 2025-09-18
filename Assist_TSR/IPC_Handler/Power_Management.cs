using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Timers;
using Assist_Service.Helpers;
using Assist_Service.Models; // for your existing Peer class
using Assist_TSR.Utilities;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Assist_Service.IPC_Handler
{
    public class Power_Management
    {
        private static readonly string DevFilePath = @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\peers.json";
        private static readonly string ProdFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");
        private readonly string peersFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");
        private readonly System.Timers.Timer nodeRefreshTimer;
        private List<Peer> cachedPeers = new List<Peer>();
        private readonly PeerSelectionManager selectionManager = new PeerSelectionManager();
        public event Action<List<Peer>> OnPeersUpdated;
        private UdpClient udpListener;
        private const int BroadcastPort = 12349;
        private Remote_Power_Log PWR_Log = new Remote_Power_Log();
        private UdpClient _udpSender;

        public Power_Management()
        {
            nodeRefreshTimer = new System.Timers.Timer(5000); // refresh every 5 seconds
            nodeRefreshTimer.Elapsed += (s, e) => RefreshPeers();
            nodeRefreshTimer.Start();
            _udpSender = new UdpClient();

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

        public bool ShutdownSelectedNodes(PeerSelectionManager selectionManager)
        {
            string peersFilePath = File.Exists(DevFilePath) ? DevFilePath : ProdFilePath;
            if (!File.Exists(peersFilePath))
                throw new FileNotFoundException($"peers.json not found at {peersFilePath}");

            // ✅ Use custom converter for IPEndPoint
            var settings = new JsonSerializerSettings
            {
                Converters = { new IPtoStringConverter() }
            };

            string jsonContent = File.ReadAllText(peersFilePath);
            var allPeers = JsonConvert.DeserializeObject<List<Peer>>(jsonContent);

            // Debug: Log all loaded peers
            PWR_Log.PWR_Log($"Loaded {allPeers?.Count ?? 0} peers from file");
            if (allPeers != null)
            {
                foreach (var peer in allPeers)
                {
                    PWR_Log.PWR_Log($"Peer: {peer.NodeName}, EndPoint: {peer.IPAddress}:{peer.EndPoint?.Port}");
                }
            }

            var selectedPeers = allPeers?.FindAll(p => selectionManager.SelectedPeers.Contains(p.NodeName)) ?? new List<Peer>();

            if (selectedPeers.Count == 0)
            {
                PWR_Log.PWR_Log("⚠️ No peers selected for shutdown.");
                return false;
            }

            foreach (var peer in selectedPeers)
            {
                SendShutdownCommand(peer);
            }

            return true;
        }

        private void SendShutdownCommand(Peer peer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(peer.IPAddress))
                    throw new Exception("Peer has no valid IP configured.");

                using (UdpClient udpClient = new UdpClient())
                {
                    string message = "SHUTDOWN";
                    byte[] buffer = Encoding.UTF8.GetBytes(message);

                    // Build remote endpoint using peer.IPAddress (IPv4) and shutdown port
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(peer.IPAddress), 12349);

                    // ✅ Actual send
                    int bytesSent = udpClient.Send(buffer, buffer.Length, remoteEP);

                    // ✅ Success log
                    PWR_Log.PWR_Log(
                        $"✅ Shutdown command ({bytesSent} bytes) sent to Node: {peer.NodeName} at {peer.IPAddress}:{remoteEP.Port}"
                    );
                }
            }
            catch (Exception ex)
            {
                // ❌ Failure log
                PWR_Log.PWR_Log(
                    $"❌ Failed to send shutdown command to {peer.NodeName} ({peer.IPAddress}): {ex.Message}"
                );
            }
        }


        private void StartGoodbyeListener()
        {
            udpListener = new UdpClient(BroadcastPort);
            Thread listenerThread = new Thread(ListenForGoodbye);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        private void ListenForGoodbye()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, BroadcastPort);

            while (true)
            {
                try
                {
                    byte[] data = udpListener.Receive(ref ep);
                    string message = Encoding.UTF8.GetString(data);

                    if (message == "GOODBYE")
                    {
                        Console.WriteLine($"[Client] Received GOODBYE from {ep.Address}");

                        // Update peers.json to set that peer offline
                        MarkPeerOffline(ep.Address.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Goodbye listener error: {ex.Message}");
                }
            }
        }

        private void MarkPeerOffline(string ipAddress)
        {
            string peersFilePath = File.Exists(DevFilePath) ? DevFilePath : ProdFilePath;

            if (!File.Exists(peersFilePath))
                return;

            var peers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Peer>>(File.ReadAllText(peersFilePath));

            foreach (var peer in peers)
            {
                if (peer.EndPoint?.Address.ToString() == ipAddress)
                {
                    peer.Status = "Offline";
                    break;
                }
            }

            File.WriteAllText(peersFilePath,
                Newtonsoft.Json.JsonConvert.SerializeObject(peers, Newtonsoft.Json.Formatting.Indented));

            Console.WriteLine($"[Client] Updated peer {ipAddress} -> Offline (Path: {peersFilePath})");
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
