using System.Collections.Generic;

namespace Assist_TSR.Models
{
    public class Rule
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }
}