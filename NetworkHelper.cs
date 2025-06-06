using System.Net;
using System.Net.Sockets;

namespace Assist_Service.Helpers
{
    public static class NetworkHelper
    {
        public static IPAddress GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                return host.AddressList.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork);
            }
            catch
            {
                return IPAddress.Loopback;
            }
        }
    }
}