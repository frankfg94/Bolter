using Bolter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;


namespace AdminBolterService
{
    /// <summary>
    /// By default this is not a windows service, just a worker service
    /// 
    /// To transform this project into a Windows Service, 
    /// you just need to add the NuGet package Microsoft.Extensions.Hosting.WindowsServices,
    /// and add the method invocation UseWindowsService to the IHostBuilder fluent API
    /// 
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private static string appUsingBolterPath;
        private static NamedPipeClientStream client;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Trace.WriteLine("Entering start client");

            StartServerIpc();

            /*
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
            */
        }
        private static dynamic Conv(dynamic source, Type dest)
        {
            return Convert.ChangeType(source, dest);
        }
        private static void ExecuteUACCommand(string jsonStr)
        {
            string errMsg = "error";
            SendToServer("Server : received command");

            dynamic jsonObj = JObject.Parse(jsonStr);
            if (jsonObj == null || !jsonObj.name)
            {
                errMsg += "Dynamic json is null";
                throw new NullReferenceException(errMsg);
            };
            switch (jsonObj.name)
            {
                case "Hello":
                    SendToServer("Hello i'm the service and it's working!");
                    break;
                case "InstallNtRights": // OK
                    Admin.InstallNtRights();
                    break;
                case "SetBatchAndCMDBlock":
                    Admin.SetBatchAndCMDBlock(Conv(jsonObj.block, typeof(bool)));
                    break;
                case "PreventDateEditingW10":
                    Admin.PreventDateEditingW10(Conv(jsonObj.block, typeof(bool)));
                    break;
                case "SetStartupSafeMode":
                    Admin.SetStartupSafeMode(
                        autoStartEnabled: Conv(jsonObj.block, typeof(bool)),
                        applicationFullPath: Conv(jsonObj.applicationFullPath, typeof(string)));
                    break;
                case "InstallService":
                    Admin.InstallService(
                            serviceExeName: Conv(jsonObj.serviceExeName, typeof(string)),
                            serviceName: Conv(jsonObj.serviceName, typeof(string)));
                    break;
                case "UninstallService":
                    Admin.UninstallService(
                        serviceExeName: Conv(jsonObj.serviceExeName, typeof(string)),
                        serviceName: Conv(jsonObj.serviceName, typeof(string)));
                    break;
                case "HideStartupsAppsFromSettings":
                    Admin.HideStartupsAppsFromSettings(
                       hide: Conv(jsonObj.hide, typeof(bool))
                        );
                    break;
                case "DisableAllAdminRestrictions":
                    if (jsonObj.foldersPathToUnlock)
                    {
                        Admin.DisableAllAdminRestrictions(
                            appPath: Conv(jsonObj.appPath, typeof(string)),
                            foldersPathToUnlock: Conv(jsonObj.foldersPathToUnlock, typeof(string[]))
                            );
                    }
                    else
                    {
                        Admin.DisableAllAdminRestrictions(appPath: Conv(jsonObj.appPath, typeof(string)));
                    }
                    break;
                case "DisableAllPossibleRestrictions":
                    Admin.HideStartupsAppsFromSettings(
                        hide: Conv(jsonObj.hide, typeof(bool))
                        );
                    break;
                case "SetWebsiteBlocked":
                    Console.WriteLine("SetWebsiteBlocked is not supported yet, because we need to pass each website address");
                    break;
                default:
                    Other.Warn("Unknown command : " + jsonObj);
                    break;
            }
        }

        private static void SendToServer(string message)
        {
            Console.WriteLine("[Service/Admin app]Sending message : " + message);
            try
            {
                var serveWriter = new StreamWriter(client);
                serveWriter.AutoFlush = true;
                serveWriter.WriteLine(message);
                // serveWriter.Flush();
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                Console.WriteLine("[CLIENT] Error: {0}", e.Message);
            }
        }

        private static void StartServerIpc()
        {
            Console.WriteLine("Starting service server ipc...");
            client = new NamedPipeClientStream("PipesOfPiece");
            client.Connect();
            Console.WriteLine("Connected :0");

            StreamReader reader = new StreamReader(client);
            while (client.IsConnected)
            {
                try
                {
                    if (!reader.EndOfStream)
                    {
                        var serverMsg = reader.ReadLine();
                        File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\log.txt", "Received command : " + serverMsg);
                        Trace.WriteLine("received: " + serverMsg);
                        SendToServer("Okay received message");
                        if (serverMsg.Contains("unblock"))
                        {
                            Admin.DisableAllPossibleRestrictions(appUsingBolterPath);
                            Trace.WriteLine("Unblocked everything successfully");
                        }
                        else
                        {
                            ExecuteUACCommand(serverMsg);
                        }
                    }
                }

                catch (Exception e)
                {
                    Console.WriteLine(e);
                    SendToServer(Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace);
                }
            }
            StartServerIpc();
        }
    }
}
