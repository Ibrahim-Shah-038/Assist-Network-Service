using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Assist_TSR.Classes
{

    internal class File_Helper_Class
    {
    }

    // Class For Thread-Safe File Operations

    public static class FileHelper
    {
        private static readonly object _fileLock = new object();

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

        public static T ReadJsonWithRetry<T>(string path, int retries = 3, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        string json = File.ReadAllText(path);
                        return JsonConvert.DeserializeObject<T>(json);
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
