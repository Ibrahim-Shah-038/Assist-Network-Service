using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using Assist_TSR.Event_Handler;
using Assist_TSR.Classes;

namespace Assist_TSR.Utilities
{
    public class Display_Config_User
    {
        Show show_message;
        // Display the configuration in a readable format
        public void DisplayConfig(List<RuleConfig> config)
        {
            show_message = new Show();
            if (config == null || config.Count == 0)
            {
                show_message.ShowMessage("Configuration", "No rules configured");
                return;
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine("Current Configuration Rules");
            result.AppendLine("==========================");

            foreach (var rule in config)
            {
                result.AppendLine($"\nSource Node: {rule.SourceNode}");
                result.AppendLine($"Trigger App: {rule.TriggerApp}");
                result.AppendLine("Target Nodes:");

                if (rule.TargetNodes != null)
                {
                    foreach (var target in rule.TargetNodes)
                    {
                        result.AppendLine($"- {target.NodeName} (Launch: {target.LaunchApp})");
                    }
                }
                else
                {
                    result.AppendLine("No target nodes configured");
                }
            }

            show_message.ShowMessage("Current Configuration", result.ToString());
        }
    }
}
