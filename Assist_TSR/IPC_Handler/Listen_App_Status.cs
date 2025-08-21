using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Assist_TSR.Forms;
using Assist_TSR.Classes;

namespace Assist_TSR.IPC_Handler
{
    public class Listen_App_Status
    {
        private readonly List<AppStatus> _statuses;
        private readonly Action _updateTreeViewAction;
        private readonly object _lockObject;

        public Listen_App_Status(List<AppStatus> statuses, Action updateTreeViewAction, object lockObject)
        {
            _statuses = statuses;
            _updateTreeViewAction = updateTreeViewAction;
            _lockObject = lockObject;
        }

        public void StartListeningToAppStatusPipe()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("AssistStatusPipe", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();

                        using (StreamReader reader = new StreamReader(pipeServer))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 3)
                                {
                                    string node = parts[0];
                                    string app = parts[1];
                                    string status = parts[2];

                                    lock (_lockObject)
                                    {
                                        var existing = _statuses.Find(s => s.NodeName == node && s.AppName == app);
                                        if (existing != null)
                                            existing.Status = status;
                                        else
                                            _statuses.Add(new AppStatus { NodeName = node, AppName = app, Status = status });
                                    }

                                    _updateTreeViewAction?.Invoke();
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
