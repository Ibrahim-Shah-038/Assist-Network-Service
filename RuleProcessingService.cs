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
        private string _customConfigPath;

        public RuleProcessingService()
        {
            _customConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomConfigPath.txt");
        }

        /// <summary>
        /// Gets the currently loaded rules (thread-safe)
        /// </summary>
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

        /// <summary>
        /// Reloads rules from configuration file
        /// </summary>
        public void ReloadRules()
        {
            lock (_configLock)
            {
                _currentRules = null; // Force reload on next access
            }
        }

        /// <summary>
        /// Loads rules from either custom or default configuration file
        /// </summary>
        private List<RuleConfig> LoadRulesConfig()
        {
            string configPath = GetActiveConfigPath();

            try
            {
                if (File.Exists(configPath))
                {
                    return FileHelper.ReadJsonWithRetry<List<RuleConfig>>(configPath) ?? new List<RuleConfig>();
                }
            }
            catch (Exception ex)
            {
                // Log error or handle as needed
                throw new ApplicationException($"Failed to load rules from {configPath}: {ex.Message}");
            }

            return new List<RuleConfig>();
        }

        /// <summary>
        /// Gets the path to the active configuration file
        /// </summary>
        public string GetActiveConfigPath()
        {
            string customPath = GetStoredCustomPath();
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                return customPath;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RulesConfig.json");
        }

        /// <summary>
        /// Gets the stored custom configuration path
        /// </summary>
        private string GetStoredCustomPath()
        {
            try
            {
                if (File.Exists(_customConfigPath))
                {
                    return File.ReadAllText(_customConfigPath).Trim();
                }
            }
            catch
            {
                // Fall through to return null
            }
            return null;
        }

        /// <summary>
        /// Stores a custom configuration path
        /// </summary>
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

        /// <summary>
        /// Gets rules that apply to a specific application trigger
        /// </summary>
        public List<RuleConfig> GetRulesForTrigger(string sourceNode, string triggerApp)
        {
            var rules = GetCurrentRules();
            return rules.Where(r =>
                r.SourceNode.Equals(sourceNode, StringComparison.OrdinalIgnoreCase) &&
                r.TriggerApp.Equals(triggerApp, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Adds a new rule to the configuration
        /// </summary>
        public void AddRule(RuleConfig newRule)
        {
            lock (_configLock)
            {
                var rules = GetCurrentRules();
                rules.Add(newRule);
                SaveRules(rules);
            }
        }

        /// <summary>
        /// Removes a rule from the configuration
        /// </summary>
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

        /// <summary>
        /// Saves rules to the active configuration file
        /// </summary>
        private void SaveRules(List<RuleConfig> rules)
        {
            string configPath = GetActiveConfigPath();
            FileHelper.WriteJsonWithRetry(configPath, rules);
            _currentRules = rules; // Update cached rules
        }

        /// <summary>
        /// Gets the active configuration file name (without path)
        /// </summary>
        public string GetActiveConfigFileName()
        {
            return Path.GetFileName(GetActiveConfigPath());
        }
    }
}