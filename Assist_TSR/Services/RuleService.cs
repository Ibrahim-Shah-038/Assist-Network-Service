using Assist_TSR.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Assist_TSR.Services
{
    public class RuleService
    {
        private readonly ConfigService _configService;

        public RuleService(ConfigService configService)
        {
            _configService = configService;
        }

        public Rule CreateRuleFromSelection(
            Dictionary<string, (bool isSourceNode, List<string> applications)> nodeApplications,
            string sourceNode,
            string triggerApp)
        {
            return new Rule
            {
                SourceNode = sourceNode,
                TriggerApp = triggerApp,
                TargetNodes = GetTargetNodes(nodeApplications, sourceNode)
            };
        }

        public void SaveRule(Rule rule)
        {
            var existingRules = ReadExistingRules();
            existingRules.RemoveAll(r => r.SourceNode == rule.SourceNode);
            existingRules.Add(rule);

            string json = JsonConvert.SerializeObject(existingRules, Formatting.Indented);
            UpdateRulesConfigFile(json);
        }

        private List<Rule> ReadExistingRules()
        {
            string filePath = _configService.GetRulesConfigFilePath();
            return File.Exists(filePath)
                ? JsonConvert.DeserializeObject<List<Rule>>(File.ReadAllText(filePath))
                : new List<Rule>();
        }

        private List<TargetNode> GetTargetNodes(
            Dictionary<string, (bool isSourceNode, List<string> applications)> nodeApplications,
            string sourceNode)
        {
            return nodeApplications
                .Where(kvp => kvp.Key != sourceNode && !kvp.Value.isSourceNode)
                .Select(kvp => new TargetNode
                {
                    NodeName = kvp.Key,
                    LaunchApp = kvp.Value.applications.FirstOrDefault(),
                    LaunchArguments = "/q"
                })
                .ToList();
        }

        private void UpdateRulesConfigFile(string jsonContent)
        {
            string filePath = _configService.GetRulesConfigFilePath();
            string directory = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string tempFilePath = Path.Combine(directory, Guid.NewGuid() + ".tmp");

            try
            {
                File.WriteAllText(tempFilePath, jsonContent);
                File.Delete(filePath);
                File.Move(tempFilePath, filePath);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }
    }
}