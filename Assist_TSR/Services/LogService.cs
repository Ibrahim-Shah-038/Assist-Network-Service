using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Assist_TSR.Services
{
    public class LogService
    {
        private readonly TextBox _logTextBox;
        private readonly string _logFilePath;
        private long _lastFilePosition;
        private readonly object _fileLock = new object();

        public LogService(TextBox logTextBox)
        {
            _logTextBox = logTextBox;
            _logFilePath = ResolveLogPath();
            _lastFilePosition = 0;
        }

        public void LoadAllLogs()
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(_logFilePath))
                    {
                        using (var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream))
                        {
                            SafeSetText(reader.ReadToEnd());
                            _lastFilePosition = stream.Length;
                        }
                    }
                    else
                    {
                        SafeSetText($"[INFO] Log file not found at: {_logFilePath}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                SafeAppendText($"[LOG ERROR] {ex.Message}\n");
                Debug.WriteLine($"LoadAllLogs error: {ex}");
            }
        }

        public void LoadNewLogs()
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(_logFilePath))
                    {
                        var fileInfo = new FileInfo(_logFilePath);
                        if (fileInfo.Length > _lastFilePosition)
                        {
                            using (var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                stream.Seek(_lastFilePosition, SeekOrigin.Begin);
                                using (var reader = new StreamReader(stream))
                                {
                                    string newContent = reader.ReadToEnd();
                                    if (!string.IsNullOrEmpty(newContent))
                                    {
                                        SafeAppendText(newContent);
                                        _lastFilePosition = stream.Position;
                                        ScrollToBottom();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SafeAppendText($"[LOG ERROR] {ex.Message}\n");
                Debug.WriteLine($"LoadNewLogs error: {ex}");
            }
        }

        private string ResolveLogPath()
        {
            List<string> possiblePaths = new List<string>
            {
                @"E:\Assist\Assist_Service\Assist_Service\bin\Debug\service.log",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Assist",
                    "service.log"
                ),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service.log")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            string defaultPath = possiblePaths[1];
            Directory.CreateDirectory(Path.GetDirectoryName(defaultPath));
            return defaultPath;
        }

        private void SafeSetText(string text)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action(() => {
                    _logTextBox.Text = text;
                    ScrollToBottom();
                }));
            }
            else
            {
                _logTextBox.Text = text;
                ScrollToBottom();
            }
        }

        private void SafeAppendText(string text)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action(() => _logTextBox.AppendText(text)));
            }
            else
            {
                _logTextBox.AppendText(text);
            }
        }

        private void ScrollToBottom()
        {
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }
    }
}