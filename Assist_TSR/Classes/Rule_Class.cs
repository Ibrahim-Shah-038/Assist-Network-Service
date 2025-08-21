using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;

namespace Assist_TSR.Classes
{

    public class Rule
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    public class TargetNode
    {
        public string NodeName { get; set; }
        public string LaunchApp { get; set; }
        public string LaunchArguments { get; set; }
    }

    public class NodeConfig
    {
        public string NodeName { get; set; }
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    public class AppStatus
    {
        public string NodeName { get; set; }
        public string AppName { get; set; }
        public string Status { get; set; }
    }

    public class RuleConfig
    {
        public string SourceNode { get; set; }
        public string TriggerApp { get; set; }
        public List<TargetNode> TargetNodes { get; set; }
    }

    public class Rule_Class
    {
    }
}
