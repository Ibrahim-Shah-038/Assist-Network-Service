using System.Collections.Generic;

namespace Assist_TSR.Models
{
    public class NodeConfig
    {
        public string NodeName { get; set; }
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }
}