using System;
using System.Net;

namespace Assist_Service.Models
{
    public class Peer
    {
        public string NodeName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public DateTime LastSeen { get; set; }
    }
}