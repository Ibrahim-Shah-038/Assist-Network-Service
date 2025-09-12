/******************************************************************************
* Module: Models/Peer.cs
* Description: Peer node data model
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

using System;
using System.Net;


namespace Assist_Service.Models
{
    /// <summary>
    /// Represents a discovered peer node
    /// </summary>
    public class Peer
    {
        public string NodeName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public string MacAddress { get; set; }
        public string Status { get; set; }
        public DateTime LastSeen { get; set; }
        public bool LeftGracefully { get; set; } = false;
        public int MissedHeartbeats { get; set; } = 0;
    }
}