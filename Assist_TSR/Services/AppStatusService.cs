using Assist_TSR.Models;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Assist_TSR.Services
{
    public class AppStatusService
    {
        private readonly List<AppStatus> _statuses = new List<AppStatus>();

        public void StartListeningToAppStatusPipe(UpdateStatusDelegate updateCallback)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using (var pipeServer = new NamedPipeServerStream("AssistStatusPipe", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();

                        using (var reader = new StreamReader(pipeServer))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                var parts = line.Split(':');
                                if (parts.Length == 3)
                                {
                                    var status = new AppStatus
                                    {
                                        NodeName = parts[0],
                                        AppName = parts[1],
                                        Status = parts[2]
                                    };

                                    lock (_statuses)
                                    {
                                        var existing = _statuses.Find(s =>
                                            s.NodeName == status.NodeName &&
                                            s.AppName == status.AppName);

                                        if (existing != null)
                                            existing.Status = status.Status;
                                        else
                                            _statuses.Add(status);
                                    }

                                    updateCallback?.Invoke();
                                }
                            }
                        }
                    }
                }
            });
        }

        public List<AppStatus> GetStatuses() => _statuses;
    }

    public delegate void UpdateStatusDelegate();
}