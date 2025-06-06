/******************************************************************************
* Module: Helpers/NetworkHelper.cs
* Description: Network-related helper functions
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Assist_Service.Helpers
{
    /// <summary>
    /// Provides network-related helper methods
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Gets the local IPv4 address
        /// </summary>
        public static IPAddress GetLocalIPAddress()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            }
            catch
            {
                return IPAddress.Loopback;
            }
        }
    }
}