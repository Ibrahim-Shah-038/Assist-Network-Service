using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace Assist_Service.Helpers
{
    public static class FileHelper
    {
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Writes object as JSON to file with retry mechanism and atomic file replacement
        /// </summary>
        /// <param name="path">Target file path</param>
        /// <param name="data">Object to serialize</param>
        /// <param name="retries">Number of retry attempts</param>
        /// <param name="delay">Delay between retries in ms</param>
        public static void WriteJsonWithRetry(string path, object data, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        string tempPath = path + ".tmp";
                        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                        File.WriteAllText(tempPath, json);
                        File.Replace(tempPath, path, null);
                    }
                    return;
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    LogError($"File write error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads JSON file with retry mechanism
        /// </summary>
        /// <typeparam name="T">Type to deserialize into</typeparam>
        /// <param name="path">Source file path</param>
        /// <param name="retries">Number of retry attempts</param>
        /// <param name="delay">Delay between retries in ms</param>
        /// <returns>Deserialized object or default if file doesn't exist</returns>
        public static T ReadJsonWithRetry<T>(string path, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        if (!File.Exists(path))
                        {
                            return default;
                        }

                        string json = File.ReadAllText(path);
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    LogError($"File read error: {ex.Message}");
                    throw;
                }
            }
            return default;
        }

        /// <summary>
        /// Creates directory if it doesn't exist
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                LogError($"Directory creation error: {ex.Message}");
                throw;
            }
        }

        private static void LogError(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file_helper.log");
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";

                lock (_fileLock)
                {
                    File.AppendAllText(logPath, logMessage);
                }
            }
            catch
            {
                // Fallback if logging fails
            }
        }
    }
}