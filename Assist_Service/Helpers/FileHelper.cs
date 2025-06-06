/******************************************************************************
* Module: Helpers/FileHelper.cs
* Description: File operations helper
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace Assist_Service.Helpers
{
    /// <summary>
    /// Provides thread-safe file operations with retry logic
    /// </summary>
    public static class FileHelper
    {
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Writes object as JSON with retry logic
        /// </summary>
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
            }
        }

        /// <summary>
        /// Reads JSON file with retry logic
        /// </summary>
        public static T ReadJsonWithRetry<T>(string path, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        if (!File.Exists(path)) return default;
                        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                    }
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delay);
                }
            }
            return default;
        }
    }
}