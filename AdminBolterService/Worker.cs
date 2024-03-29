using Bolter;
using Bolter.BolterAdminApp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
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
        private static readonly string appUsingBolterPath;
        private static NamedPipeClientStream client;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        readonly bool tcpEnabled = true;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Trace.WriteLine("Entering start client");
            if (tcpEnabled)
            {
                var server = new TcpServer();
                server.Start(new IPAddress(new byte[] { 127, 0, 0, 1 }), 8976);
            }
            else
            {
                StartServerIpc();
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
