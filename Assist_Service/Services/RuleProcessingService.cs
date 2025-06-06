using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assist_Service.Helpers;
using Assist_Service.Models;
using Newtonsoft.Json;

namespace Assist_Service.Services
{
    public class RuleProcessingService
    {
        private readonly object _configLock = new object();
        private List<RuleConfig> _currentRules;
        private readonly string _customConfigPath;

        public RuleProcessingService()
        {
            _customConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomConfigPath.txt");
        }

        public List<RuleConfig> GetCurrentRules()
        {
            lock (_configLock)
            {
                if (_currentRules == null)
                {
                    _currentRules = LoadRulesConfig();
                }
                return _currentRules;
            }
        }

        public void ReloadRules()
        {
            lock (_configLock)
            {
                _currentRules = null;
            }
        }

        public List<RuleConfig> GetRulesForTrigger(string sourceNode, string triggerApp)
        {
            var rules = GetCurrentRules();
            return rules.Where(r =>
                r.SourceNode.Equals(sourceNode, StringComparison.OrdinalIgnoreCase) &&
                r.TriggerApp.Equals(triggerApp, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void AddRule(RuleConfig newRule)
        {
            lock (_configLock)
            {
                var rules = GetCurrentRules();
                rules.Add(newRule);
                SaveRules(rules);
            }
        }

        public bool RemoveRule(Predicate<RuleConfig> match)
        {
            lock (_configLock)
            {
                var rules = GetCurrentRules();
                int removed = rules.RemoveAll(match);
                if (removed > 0)
                {
                    SaveRules(rules);
                    return true;
                }
                return false;
            }
        }

        public string GetActiveConfigFileName()
        {
            return Path.GetFileName(GetActiveConfigPath());
        }

        private List<RuleConfig> LoadRulesConfig()
        {
            string configPath = GetActiveConfigPath();
            try
            {
                return FileHelper.ReadJsonWithRetry<List<RuleConfig>>(configPath) ?? new List<RuleConfig>();
            }
            catch
            {
                return new List<RuleConfig>();
            }
        }

        private string GetActiveConfigPath()
        {
            string customPath = GetStoredCustomPath();
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                return customPath;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json");
        }

        private string GetStoredCustomPath()
        {
            try
            {
                return File.Exists(_customConfigPath) ? File.ReadAllText(_customConfigPath).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        public void StoreCustomPath(string path)
        {
            try
            {
                File.WriteAllText(_customConfigPath, path);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to store custom config path: {ex.Message}");
            }
        }

        private void SaveRules(List<RuleConfig> rules)
        {
            string configPath = GetActiveConfigPath();
            FileHelper.WriteJsonWithRetry(configPath, rules);
            _currentRules = rules;
        }
    }
}