using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Assist_TSR.IPC_Handler;
using Assist_TSR.Classes;
using System.IO;

namespace Assist_TSR.Event_Handler
{
    public class Read_Rules
    {
        Get_Rules_Path get_rules_path;
        Rule_Class get_rule_class;
        public List<Rule> ReadExistingRules()
        {
            get_rules_path = new Get_Rules_Path();
            get_rule_class = new Rule_Class();
            string filePath = get_rules_path.GetRulesConfigFilePath();

            if (!File.Exists(filePath))
            {
                return new List<Rule>();
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<Rule>>(json) ?? new List<Rule>();
        }
    }
}
