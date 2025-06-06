using System.Collections.Generic;

namespace Assist_Service.Models
{
    public class RuleConfig
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }
}