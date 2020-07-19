using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Bolter.BolterAdminApp
{
    public class IpcServer
    {
        public static string message = null;
        Process bridge = null;
        private static NamedPipeServerStream server;

        public void SendToClient(string message)
        {
            Console.WriteLine("Sending message : " + message);
            try
            {
                var serveWriter = new StreamWriter(server);
                serveWriter.AutoFlush = true;
                serveWriter.WriteLine(message);
                // serveWriter.Flush();
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                Console.WriteLine("[SERVER] Error: {0}", e.Message);
            }
        }

        public void StartListeningToClient()
        {
            new Thread(() =>
            {
                try
                {
                    var serveReader = new StreamReader(server);
                    while (true)
                    {
                        if (!serveReader.EndOfStream)
                        {
                            Console.WriteLine("Received from server :" + serveReader.ReadLine());
                        }
                    }
                    // serveWriter.Flush();
                }
                // Catch the IOException that is raised if the pipe is broken
                // or disconnected.
                catch (IOException e)
                {
                    Console.WriteLine("[SERVER] Error: {0}", e.Message);
                }
            }).Start();
        }

        public void StopServer()
        {
            try
            {
                server.Dispose();
                if (bridge != null)
                {
                    bridge.Kill();
                }
            }
            catch (Exception e)
            {

                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine("[SERVER] Client quit. Server terminating.");
            }
        }

        /// <summary>
        /// Start the server that connects to the remote process or the remote service
        /// </summary>
        private void StartServer()
        {
            Task.Factory.StartNew(() =>
            {
                server = new NamedPipeServerStream("PipesOfPiece");
                server.WaitForConnection();
                Console.WriteLine("Connected!");
            });
        }

        public void ConnectToAdminBolterService(int delayMs)
        {
            StartServer();
            Task.Delay(delayMs).Wait();
        }

        // A bridge process is required to make ipc work with uac ....
        public void ConnectToClient(string ipcClientExePath, string bridgeExePath, string thisAppExePath)
        {

            bridge = new Process();
            if (thisAppExePath.EndsWith(".dll"))
            {
                thisAppExePath = thisAppExePath.Replace(".dll", ".exe");
            }
            bridge.StartInfo.FileName = bridgeExePath;


            // Pass the client process a handle to the server.
            bridge.StartInfo.ArgumentList.Add("uac:" + ipcClientExePath);
            bridge.StartInfo.ArgumentList.Add("p:" + thisAppExePath);


            bridge.StartInfo.UseShellExecute = true; // required for uac prompt
            bridge.StartInfo.Verb = "runas"; // will throw win32 exception if the process is not administrator
            bridge.OutputDataReceived += PipeClient_OutputDataReceived;
            bridge.ErrorDataReceived += PipeClient_ErrorDataReceived;
            try
            {
                bridge.Start();
            }
            catch (Exception)
            {
                Console.WriteLine($"The UAC was denied for {bridge.StartInfo.FileName}");
                throw;
            }
            StartServer();
            Task.Delay(1000).Wait();
            bridge.WaitForExit();
        }


        private void PipeClient_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        public void ConnectToClient(string ipcClientExePath, string bridgeExePath)
        {
            ConnectToClient(ipcClientExePath, bridgeExePath, System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        private void PipeClient_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}

