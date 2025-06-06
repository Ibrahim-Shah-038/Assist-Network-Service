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
        /// <summary>
        /// Name of the peer node
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// Network endpoint of the peer
        /// </summary>
        public IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Last time the peer was seen
        /// </summary>
        public DateTime LastSeen { get; set; }
    }
}