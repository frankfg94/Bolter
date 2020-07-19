using Bolter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            //System.Diagnostics.Debugger.Launch();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Trace.WriteLine("Entering start client");
            StartClient();
            /*
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
            */
        }

        private static void EnableUacSecurity(string commandName)
        {
            Trace.WriteLine("Entered " + commandName);
            switch (commandName)
            {
                case "Hello":
                    SendToServer("Hello i'm the service and it's working!");
                    break;
                case "InstallNtRights":
                    Admin.InstallNtRights();
                    break;
                case "SetBatchAndCMDBlock":
                    Admin.SetBatchAndCMDBlock(true);
                    break;
                case "PreventDateEditingW10":
                    Admin.PreventDateEditingW10(true);
                    break;
                case "SetStartupSafeMode":
                    Admin.SetStartupSafeMode(true);
                    break;
                case "SetWebsiteBlocked":
                    Console.WriteLine("SetWebsiteBlocked is not supported yet, because we need to pass each website address");
                    break;
                default:
                    Other.Warn("Unknown command : " + commandName);
                    break;
            }
        }

        private static void SendToServer(string message)
        {
            Console.WriteLine("Sending message : " + message);
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

        private static void StartClient()
        {
            Console.WriteLine("Starting client...");
            client = new NamedPipeClientStream("PipesOfPiece");
            client.Connect();
            Console.WriteLine("Connected :0");

                StreamReader reader = new StreamReader(client);
                while (true)
                {
                    if (!reader.EndOfStream)
                    {
                        var serverMsg = reader.ReadLine();
                        File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\log.txt", "Received command : " + serverMsg);
                        Trace.WriteLine("received: " + serverMsg);
                        if (serverMsg.Contains("unblock"))
                        {
                            Admin.DisableAllPossibleRestrictions(appUsingBolterPath);
                            Trace.WriteLine("Unblocked everything successfully");
                        }
                        else
                        {
                            EnableUacSecurity(serverMsg);
                        }
                    }
                }
        }
    }
}
