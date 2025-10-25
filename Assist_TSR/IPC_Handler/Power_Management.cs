using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Assist_Service.Helpers;
using Assist_Service.Models; // for Peer class
using Assist_TSR.Utilities;
using Newtonsoft.Json;


namespace Assist_Service.IPC_Handler
{
    public class Power_Management
    {
        private static readonly string DevFilePath =
            @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\peers.json";

        private static readonly string ProdFilePath =
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");

        private readonly string peersFilePath =
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");

        private readonly System.Timers.Timer nodeRefreshTimer;
        private List<Peer> cachedPeers = new List<Peer>();
        private readonly PeerSelectionManager selectionManager = new PeerSelectionManager();

        public event Action<List<Peer>> OnPeersUpdated;

        
        private const int BroadcastPort = 12349;
        private TSR_Power_Log PWR_Log = new TSR_Power_Log();
        private UdpClient _udpSender;

        private string filePath;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        public Power_Management()
        {
            // Pick Prod first, otherwise Dev
            filePath = File.Exists(ProdFilePath) ? ProdFilePath : DevFilePath;

            nodeRefreshTimer = new System.Timers.Timer(5000); // refresh every 5 seconds
            nodeRefreshTimer.Elapsed += (s, e) => RefreshPeers();
            nodeRefreshTimer.Start();

            _udpSender = new UdpClient();

            RefreshPeers(); // initial load
            StartSleepPipeListener();
        }

        private void RefreshPeers()
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                string json = File.ReadAllText(filePath);

                // ✅ Use Newtonsoft.Json so IPEndPointConverter works
                var peers = JsonConvert.DeserializeObject<List<Peer>>(json);

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

            string jsonContent = File.ReadAllText(peersFilePath);
            var allPeers = JsonConvert.DeserializeObject<List<Peer>>(jsonContent);

            // Debug log
            PWR_Log.PWR_Log($"Loaded {allPeers?.Count ?? 0} peers from file");
            if (allPeers != null)
            {
                foreach (var peer in allPeers)
                {
                    PWR_Log.PWR_Log($"Peer: {peer.NodeName}, EndPoint: {peer.IPAddress}:{peer.EndPoint?.Port}");
                }
            }

            var selectedPeers = allPeers?
                .FindAll(p => selectionManager.SelectedPeers.Contains(p.NodeName))
                ?? new List<Peer>();

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

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(peer.IPAddress), 12349);

                    int bytesSent = udpClient.Send(buffer, buffer.Length, remoteEP);

                    PWR_Log.PWR_Log(
                        $"✅ Shutdown command ({bytesSent} bytes) sent to Node: {peer.NodeName} at {peer.IPAddress}:{remoteEP.Port}"
                    );
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log(
                    $"❌ Failed to send shutdown command to {peer.NodeName} ({peer.IPAddress}): {ex.Message}"
                );
            }
        }

        // Sleep Nodes
        public bool SleepSelectedNodes(PeerSelectionManager selectionManager)
        {
            string peersFilePath = File.Exists(DevFilePath) ? DevFilePath : ProdFilePath;
            if (!File.Exists(peersFilePath))
                throw new FileNotFoundException($"peers.json not found at {peersFilePath}");

            string jsonContent = File.ReadAllText(peersFilePath);
            var allPeers = JsonConvert.DeserializeObject<List<Peer>>(jsonContent);

            // Debug log
            PWR_Log.PWR_Log($"Loaded {allPeers?.Count ?? 0} peers from file");
            if (allPeers != null)
            {
                foreach (var peer in allPeers)
                {
                    PWR_Log.PWR_Log($"Peer: {peer.NodeName}, EndPoint: {peer.IPAddress}:{peer.EndPoint?.Port}");
                }
            }

            var selectedPeers = allPeers?
                .FindAll(p => selectionManager.SelectedPeers.Contains(p.NodeName))
                ?? new List<Peer>();

            if (selectedPeers.Count == 0)
            {
                PWR_Log.PWR_Log("⚠️ No peers selected for sleep.");
                return false;
            }

            foreach (var peer in selectedPeers)
            {
                SendSleepCommand(peer);
            }

            return true;
        }

        private void SendSleepCommand(Peer peer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(peer.IPAddress))
                    throw new Exception("Peer has no valid IP configured.");

                using (UdpClient udpClient = new UdpClient())
                {
                    string message = "SLEEP";
                    byte[] buffer = Encoding.UTF8.GetBytes(message);

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(peer.IPAddress), 12349);

                    int bytesSent = udpClient.Send(buffer, buffer.Length, remoteEP);

                    PWR_Log.PWR_Log(
                        $"✅ Sleep command ({bytesSent} bytes) sent to Node: {peer.NodeName} at {peer.IPAddress}:{remoteEP.Port}"
                    );
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log(
                    $"❌ Failed to send sleep command to {peer.NodeName} ({peer.IPAddress}): {ex.Message}"
                );
            }
        }

        private void StartSleepPipeListener()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        using (NamedPipeServerStream pipeServer =
                            new NamedPipeServerStream("sleep_exe", PipeDirection.In))
                        {
                            pipeServer.WaitForConnection();

                            using (StreamReader reader = new StreamReader(pipeServer, Encoding.UTF8))
                            {
                                string message = reader.ReadToEnd().Trim();

                                if (message.Equals("SLEEP", StringComparison.OrdinalIgnoreCase))
                                {
                                        ExecuteSleepCommand();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // optional: log or handle errors
                        Debug.WriteLine($"Pipe listener error: {ex.Message}");
                    }
                }
            });
        }

        private void ExecuteSleepCommand()
        {
            try
            {
                if (!LockWorkStation())
                {
                    int err = Marshal.GetLastWin32Error();
                    PWR_Log.PWR_Log($"[WinForms] Lock failed. Error code: {err}");
                }
                else
                {
                    PWR_Log.PWR_Log("[WinForms] PC locked immediately.");
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[WinForms] Lock error: {ex.Message}");
            }
        }

        // POWER_UP SLEEPING NODES

        public bool PowerUpSelectedNodes(PeerSelectionManager selectionManager)
        {
            string peersFilePath = File.Exists(DevFilePath) ? DevFilePath : ProdFilePath;
            if (!File.Exists(peersFilePath))
                throw new FileNotFoundException($"peers.json not found at {peersFilePath}");

            string jsonContent = File.ReadAllText(peersFilePath);
            var allPeers = JsonConvert.DeserializeObject<List<Peer>>(jsonContent);

            // Debug log
            PWR_Log.PWR_Log($"Loaded {allPeers?.Count ?? 0} peers from file for Power Up");
            if (allPeers != null)
            {
                foreach (var peer in allPeers)
                {
                    PWR_Log.PWR_Log($"Peer: {peer.NodeName}, EndPoint: {peer.IPAddress}:{peer.EndPoint?.Port}");
                }
            }

            var selectedPeers = allPeers?
                .FindAll(p => selectionManager.SelectedPeers.Contains(p.NodeName))
                ?? new List<Peer>();

            if (selectedPeers.Count == 0)
            {
                PWR_Log.PWR_Log("⚠️ No peers selected for power up.");
                return false;
            }

            foreach (var peer in selectedPeers)
            {
                SendPowerUpCommand(peer);
            }

            return true;
        }

        private void SendPowerUpCommand(Peer peer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(peer.MacAddress))
                    throw new Exception("Peer has no valid MAC address configured.");

                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "AssistWakePipe", PipeDirection.Out))
                {
                    pipeClient.Connect(2000); // timeout in ms (2 seconds)

                    using (StreamWriter writer = new StreamWriter(pipeClient))
                    {
                        writer.AutoFlush = true;

                        // Format: POWERUP|MACADDRESS
                        string message = $"POWERUP|{peer.MacAddress}";
                        writer.WriteLine(message);

                        PWR_Log.PWR_Log($"✅ Sent POWERUP request for {peer.NodeName} ({peer.MacAddress}) to service via AssistWakePipe");
                    }
                }
            }
            catch (TimeoutException)
            {
                PWR_Log.PWR_Log($"❌ Timeout while connecting to AssistWakePipe for {peer.NodeName}");
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"❌ Failed to send POWERUP command for {peer.NodeName}: {ex.Message}");
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
