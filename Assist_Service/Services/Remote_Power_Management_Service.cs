using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Newtonsoft.Json;
using System.Linq;

namespace Assist_Service.Services
{
    public class Remote_Power_Management_Service //: IDisposable
    {
        private List<Peer> _peers;
        private readonly object _peersLock;
        private bool _isRunning;
        private Thread _listenerThread;
        private UdpClient _udpServer;
        private readonly Remote_Power_Log PWR_Log = new Remote_Power_Log();
        private bool _disposed;
        private NodeConfig _nodeConfig;

        private const int ListenPort = 12349;
        private const int GoodByePort = 12350;
        private const int SleepPort = 12351;
        private bool _running_Pwr_Up;
        private CancellationTokenSource _cts_pwr_up;
        private Task _listenerTask;
        private bool isRunning = false;
        private UdpClient udpListener;
        private static readonly string DevFilePath =
            @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\peers.json";

        private static readonly string ProdFilePath =
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "peers.json");

        // -------------------------------
        // Start service
        // -------------------------------
        public void Start()
        {
            if (_isRunning) return; // prevent double start

            _isRunning = true;

            _listenerThread = new Thread(UdpListenerWorker)
            {
                IsBackground = true
            };
            _listenerThread.Start();

            StartGoodbyeListener();

            //power up
            _running_Pwr_Up = true;
            _cts_pwr_up = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenForPowerCommands(_cts_pwr_up.Token));
            PWR_Log.PWR_Log("✅ AssistWakeService started and listening on AssistWakePipe.");

            PWR_Log.PWR_Log($"[Service] UDP listener thread started on port {ListenPort}");
        }

        // -------------------------------
        // Stop service
        // -------------------------------
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _running_Pwr_Up = false;

            try
            {
                _udpServer?.Close();
                _udpServer?.Dispose();
                _udpServer = null;
            }
            catch { }

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                if (!_listenerThread.Join(2000)) // wait max 2 sec
                {
                    try { _listenerThread.Interrupt(); } catch { }
                }
                _listenerThread = null;
            }

            StopGoodbyeListener();

            _cts_pwr_up.Cancel();
            _listenerTask?.Wait();
            PWR_Log.PWR_Log("🛑 AssistWakeService stopped.");

            PWR_Log.PWR_Log("[Service] Stopped.");
        }

        // -------------------------------
        // Worker: continuously listens
        // -------------------------------
        private void UdpListenerWorker()
        {
            try
            {
                _udpServer = new UdpClient(ListenPort);
                PWR_Log.PWR_Log($"[Service] Listening on {_udpServer.Client.LocalEndPoint}");

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (_isRunning)
                {
                    byte[] data = _udpServer.Receive(ref remoteEP);
                    string command = Encoding.UTF8.GetString(data).Trim();

                    PWR_Log.PWR_Log($"[Service] Received '{command}' from {remoteEP}");

                    if (command.Equals("SHUTDOWN", StringComparison.OrdinalIgnoreCase))
                    {
                        PWR_Log.PWR_Log("[Service] Shutdown command received.");
                        BroadcastGoodbye();
                        ExecuteShutdown();
                    }
                    else if (command.Equals("SLEEP", StringComparison.OrdinalIgnoreCase))
                    {
                        PWR_Log.PWR_Log("[Service] Sleep command received.");
                        BroadcastSleeping();   // optional: notify before sleeping
                        ExecuteSleep();       // ✅ implement this to put system to sleep
                    }
                    else
                    {
                        PWR_Log.PWR_Log($"[Service] Unknown command '{command}' ignored.");
                    }
                }
            }
            catch (SocketException ex)
            {
                if (_isRunning)
                    PWR_Log.PWR_Log($"[Service] UDP socket error: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Happens when service is stopping
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    PWR_Log.PWR_Log($"[Service] Listener error: {ex.Message}");
            }
        }

        // -------------------------------
        // Broadcast GOODBYE
        // -------------------------------
        private void BroadcastGoodbye()
        {
            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    udp.EnableBroadcast = true;
                    IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, GoodByePort);

                    // Include node name and MAC address in the message
                    _nodeConfig = LoadNodeConfig();
                    string nodeName = _nodeConfig.NodeName;
                    string macAddress = NetworkHelper.GetMacAddress(); // Your method to get MAC
                    string message = $"GOODBYE:{nodeName}:{macAddress}";

                    byte[] data = Encoding.UTF8.GetBytes(message);
                    udp.Send(data, data.Length, ep);

                    PWR_Log.PWR_Log($"[Service] Sent GOODBYE broadcast: {nodeName}, {macAddress}");
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[Service] Broadcast error: {ex.Message}");
            }
        }

        private NodeConfig LoadNodeConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeConfig.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _nodeConfig = JsonConvert.DeserializeObject<NodeConfig>(json);  // ← Changed here
                    PWR_Log.PWR_Log($"Loaded NodeName: {_nodeConfig.NodeName}");
                }
                else
                {
                    _nodeConfig = new NodeConfig { NodeName = Environment.MachineName };
                    PWR_Log.PWR_Log($"Config not found, using: {_nodeConfig.NodeName}");
                }

                return _nodeConfig;  // ← Added return
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"Error loading config: {ex.Message}");
                _nodeConfig = new NodeConfig { NodeName = Environment.MachineName };
                return _nodeConfig;  // ← Added return
            }
        }

        private void BroadcastSleeping()
        {
            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    udp.EnableBroadcast = true;
                    IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, SleepPort);

                    string message = "SLEEPING"; // ✅ notify others this peer is going to sleep
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    udp.Send(data, data.Length, ep);

                    PWR_Log.PWR_Log("[Service] Sent SLEEPING broadcast.");
                }
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[Service] Broadcast error: {ex.Message}");
            }
        }

        // -------------------------------
        // Execute system shutdown
        // -------------------------------
        private void ExecuteShutdown()
        {
            try
            {
                Thread.Sleep(500); // give GOODBYE time to send

                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /f /t 0",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                PWR_Log.PWR_Log("[Service] Shutdown command executed.");
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[Service] Shutdown error: {ex.Message}");
            }
        }

        private void ExecuteSleep()
        {
            try
            {
                using (NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", "sleep_exe", PipeDirection.Out))
                {
                    pipeClient.Connect(2000); // wait up to 2s to connect
                    byte[] messageBytes = Encoding.UTF8.GetBytes("SLEEP");
                    pipeClient.Write(messageBytes, 0, messageBytes.Length);
                    pipeClient.Flush();
                }

                PWR_Log.PWR_Log("[Service] Sent sleep request via Named Pipe.");
            }
            catch (Exception ex)
            {
                PWR_Log.PWR_Log($"[Service] Sleep error (pipe): {ex.Message}");
            }
        }

        // POWER UP SHUTDOWN NODES

        private void ListenForPowerCommands(CancellationToken token)
        {
            while (_running_Pwr_Up && !token.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("AssistWakePipe", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();

                        using (StreamReader reader = new StreamReader(pipeServer))
                        {
                            string command = reader.ReadLine();
                            if (!string.IsNullOrEmpty(command))
                            {
                                PWR_Log.PWR_Log($"📥 Received command: {command}");
                                HandleCommand(command);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PWR_Log.PWR_Log($"❌ Error in ListenForPipeCommands: {ex.Message}");
                }
            }
        }

        private void HandleCommand(string command)
        {
            if (command.StartsWith("POWERUP|"))
            {
                string mac = command.Split('|')[1];
                PWR_Log.PWR_Log($"⚡ Sending WOL packet to {mac}...");
                try
                {
                    // Format MAC address to required format (XX:XX:XX:XX:XX:XX)
                    string formattedMac = FormatMacAddress(mac);
                    PWR_Log.PWR_Log($"📡 Formatted MAC: {formattedMac}");

                    WakeOnLan.SendMagicPacket(formattedMac);
                    PWR_Log.PWR_Log($"✅ WOL packet sent successfully to {mac}");
                }
                catch (Exception ex)
                {
                    PWR_Log.PWR_Log($"❌ Failed to send WOL packet: {ex.Message}");
                }
            }
            else
            {
                PWR_Log.PWR_Log($"⚠ Unknown command received: {command}");
            }
        }

        private string FormatMacAddress(string mac)
        {
            // Remove any existing separators (colons, hyphens, spaces)
            mac = mac.Replace(":", "").Replace("-", "").Replace(" ", "");

            // Validate length
            if (mac.Length != 12)
                throw new ArgumentException($"Invalid MAC address length: {mac.Length}. Expected 12 characters.");

            // Validate hex characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(mac, "^[0-9A-Fa-f]{12}$"))
                throw new ArgumentException("MAC address contains invalid characters. Only hex digits allowed.");

            // Insert colons every 2 characters: 68E43B308203 -> 68:E4:3B:30:82:03
            return string.Join(":",
                Enumerable.Range(0, 6)
                .Select(i => mac.Substring(i * 2, 2).ToUpper()));
        }

        // -------------------------------
        // GOODBYE LISTENER
        // -------------------------------

        private void StartGoodbyeListener()
        {
            udpListener = new UdpClient(GoodByePort);
            isRunning = true;
            Thread listenerThread = new Thread(ListenForGoodbye);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        private void ListenForGoodbye()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, SleepPort);

            while (isRunning)
            {
                try
                {
                    byte[] data = udpListener.Receive(ref ep);
                    string message = Encoding.UTF8.GetString(data);

                    if (message.StartsWith("GOODBYE"))
                    {
                        string[] parts = message.Split(':');
                        string nodeName = parts.Length > 1 ? parts[1] : null;
                        string macAddress = parts.Length > 2 ? parts[2] : null;

                        PWR_Log.PWR_Log($"[Service] Received GOODBYE from {ep.Address} - Node: {nodeName}, MAC: {macAddress}");

                        // Update both file storage AND in-memory peers
                        UpdateLocalPeerStatus(nodeName, macAddress, ep.Address.ToString());
                        PeerFileStorage.MarkPeerOffline(ep.Address.ToString(), macAddress, nodeName);
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        PWR_Log.PWR_Log($"[Service] Goodbye listener error: {ex.Message}");
                }
            }
        }

        private void UpdateLocalPeerStatus(string nodeName, string macAddress, string ipAddress)
        {
            lock (_peersLock)
            {
                Peer peer = null;

                if (!string.IsNullOrEmpty(macAddress))
                {
                    peer = _peers.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.MacAddress) &&
                        p.MacAddress.Equals(macAddress, StringComparison.OrdinalIgnoreCase));
                }

                if (peer == null && !string.IsNullOrEmpty(nodeName))
                {
                    peer = _peers.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.NodeName) &&
                        p.NodeName.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
                }

                if (peer == null && !string.IsNullOrEmpty(ipAddress))
                {
                    peer = _peers.FirstOrDefault(p =>
                        (!string.IsNullOrEmpty(p.IPAddress) && p.IPAddress.Equals(ipAddress)) ||
                        (p.EndPoint != null && p.EndPoint.Address.ToString() == ipAddress)); // FIX HERE
                }

                if (peer != null)
                {
                    peer.Status = "Offline";
                    peer.LastSeen = DateTime.Now;
                    peer.LeftGracefully = true;
                    PWR_Log.PWR_Log($"[UpdateLocalPeerStatus] Updated in-memory peer: {peer.NodeName} to Offline");
                }
            }
        }

        private void StopGoodbyeListener()
        {
            isRunning = false;
            udpListener?.Close();
            udpListener = null;
            PWR_Log.PWR_Log("[Service] Goodbye listener stopped.");
        }

        // -------------------------------
        // WAKE ON LAN CLASSES
        // -------------------------------

        public static class WakeOnLan
        {
            public static void SendMagicPacket(string macAddress)
            {
                if (string.IsNullOrWhiteSpace(macAddress))
                    throw new ArgumentException("MAC Address cannot be empty");

                byte[] macBytes = ParseMacAddress(macAddress);

                // Magic packet = 6 x 0xFF followed by 16 repetitions of MAC
                byte[] packet = new byte[6 + (16 * macBytes.Length)];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 6; i < packet.Length; i += macBytes.Length)
                    Buffer.BlockCopy(macBytes, 0, packet, i, macBytes.Length);

                using (UdpClient client = new UdpClient())
                {
                    client.EnableBroadcast = true;
                    IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, 9); // Port 9 is standard for WOL
                    client.Send(packet, packet.Length, ep);
                }
            }

            private static byte[] ParseMacAddress(string mac)
            {
                string[] hex = mac.Split(':', '-');
                if (hex.Length != 6)
                    throw new ArgumentException("Invalid MAC address format. Use format: 00:11:22:33:44:55");

                byte[] bytes = new byte[6];
                for (int i = 0; i < 6; i++)
                    bytes[i] = Convert.ToByte(hex[i], 16);

                return bytes;
            }

            

            // -------------------------------
            // IDisposable support
            // -------------------------------
            /*public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }*/
        }
    }
    }
