/******************************************************************************
* Module: Models/TargetNode.cs
* Description: Target node data model
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

namespace Assist_Service.Models
{
    /// <summary>
    /// Represents a target node configuration
    /// </summary>
    public class TargetNode
    {
        /// <summary>
        /// Name of the target node
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// Application to launch on the target node
        /// </summary>
        public string LaunchApp { get; set; }
        public string LaunchArguments { get; set; }
    }
}