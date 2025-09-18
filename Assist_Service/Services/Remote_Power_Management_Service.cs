using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Assist_Service.Helpers;

namespace Assist_Service.Services
{
    public class Remote_Power_Management_Service : IDisposable
    {
        private bool _isRunning;
        private Thread _listenerThread;
        private UdpClient _udpServer;
        private readonly Remote_Power_Log PWR_Log = new Remote_Power_Log();
        private bool _disposed;

        private const int ListenPort = 12349;
        private const int BroadcastPort = 12349;

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

            PWR_Log.PWR_Log($"[Service] UDP listener thread started on port {ListenPort}");
        }

        // -------------------------------
        // Stop service
        // -------------------------------
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;

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
                    IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

                    string message = "GOODBYE";
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    udp.Send(data, data.Length, ep);

                    PWR_Log.PWR_Log("[Service] Sent GOODBYE broadcast.");
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

        // -------------------------------
        // IDisposable support
        // -------------------------------
        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
