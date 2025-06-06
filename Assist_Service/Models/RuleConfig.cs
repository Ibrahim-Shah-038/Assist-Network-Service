/******************************************************************************
* Module: Models/RuleConfig.cs
* Description: Rule configuration data model
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

using System.Collections.Generic;

namespace Assist_Service.Models
{
    /// <summary>
    /// Represents a trigger rule configuration
    /// </summary>
    public class RuleConfig
    {
        /// <summary>
        /// Source node that triggers the rule
        /// </summary>
        public string SourceNode { get; set; }

        /// <summary>
        /// Application that triggers the rule
        /// </summary>
        public string TriggerApp { get; set; }

        /// <summary>
        /// List of target nodes and applications
        /// </summary>
        public List<TargetNode> TargetNodes { get; set; }
    }
}