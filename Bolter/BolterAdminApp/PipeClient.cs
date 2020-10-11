using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Bolter.BolterAdminApp
{
    // At the moment IPC does work very well if the service was started a few minutes ago, but if we test it directly with install, it creates problems (not connected or pipebroken)
    public class PipeClient : IAdminCommands, IDisposable
    {
        public static string message = null;
        Process bridge = null;
        /// <summary>
        /// Triggered when a message must be send to the IpcServer
        /// </summary>
        private event EventHandler<StringArg> RequestMessageSend;
        NamedPipeClientStream client;


        private static void ConsoleClientWrite(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[CLIENT] ");
            Console.ResetColor();
            Console.Write(msg);
        }

        private static void ConsoleServerWriteLine(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("[SERVER] ");
            Console.ResetColor();
            Console.Write(msg + Environment.NewLine);
        }

        public bool IsConnected()
        {
            return client.IsConnected;
        }




        /// <summary>
        /// Start the client that connects to the remote process or the remote service
        /// The server must be up and running first
        /// </summary>
        private void Start()
        {
            client = new NamedPipeClientStream(".",Properties.Resources.named_pipes_name, PipeDirection.InOut);
            Console.WriteLine(Properties.Resources.connecting);
            client.Connect();
            Console.WriteLine(Properties.Resources.connected);
            StreamReader reader = new StreamReader(client);
            StreamWriter writer = new StreamWriter(client);
            this.RequestMessageSend += (s, arg) =>
            {
                ConsoleClientWrite("Sending message : " + arg.message + "... ");
                try
                {
                    writer.WriteLine(arg.message);
                    writer.Flush();
                    Console.Write("OK\n");
                    ConsoleServerWriteLine(reader.ReadLine());
                        // serveWriter.Flush();
                    }
                    // Catch the IOException that is raised if the pipe is broken
                    // or disconnected.
                    catch (IOException e)
                {
                    ConsoleClientWrite($"Error: {e.Message}");
                }
            };
            //SetAuth(client);
        }

        public void SendMessage(string msg)
        {
            RequestMessageSend?.Invoke(this, new StringArg(msg));
        }

        public void Stop()
        {
            try
            {
                client.Dispose();
                if (bridge != null)
                {
                    bridge.Kill();
                }
                ConsoleServerWriteLine(Properties.Resources.client_quit);
            }
            catch (Exception e)
            {

                Console.WriteLine(e);
            }
        }



        public void ConnectToAdminBolterService(int delayMs)
        {
            Console.WriteLine("[?] Connecting to the remote bolter service");
            Start();
            Task.Delay(delayMs).Wait();
        }

        // A bridge process is required to make ipc work with uac .... ( 3 processes : process executing the command - uac bridge process - uac process)
        public void ConnectToBridge(string ipcClientExePath, string bridgeExePath, string thisAppExePath)
        {

            bridge = new Process();
            if (thisAppExePath != null && bridgeExePath != null && ipcClientExePath != null)
            {
                if (thisAppExePath.EndsWith(".dll", StringComparison.Ordinal))
                {
                    thisAppExePath = thisAppExePath.Replace(".dll", ".exe", StringComparison.Ordinal);
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
                Start();
                Task.Delay(1000).Wait();
                bridge.WaitForExit();
            }
            else
            {
                throw new NullReferenceException("One of the paths is null");
            }
        }


        private void PipeClient_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        public void ConnectToBridge(string ipcClientExePath, string bridgeExePath)
        {
            ConnectToBridge(ipcClientExePath, bridgeExePath, Process.GetCurrentProcess().MainModule.FileName);
        }

        private void PipeClient_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        public void RequestInstallNtRights()
        {
            string json = "{name:InstallNtRights}";
            SendMessage(json);
        }

        public void RequestSetBatchAndCMDBlock(bool block)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("SetBatchAndCMDBlock"));
            o.Add("block", new JValue(block));
            SendMessage(o.ToString(Newtonsoft.Json.Formatting.None));
        }

        public void RequestPreventDateEditingW10(bool removePrivilege)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("PreventDateEditingW10"));
            o.Add("block", new JValue(removePrivilege));
            SendMessage(o.ToString(Newtonsoft.Json.Formatting.None));
        }

        public void RequestInstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service", bool autoStart = true)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("InstallService"));
            o.Add("serviceExeName", new JValue(serviceExeName));
            o.Add("serviceName", new JValue(serviceName));
            o.Add("autoStart", new JValue(autoStart));
            SendMessage(o.ToString());
        }

        public void RequestHideStartupsAppsFromSettings(bool hide)
        {
            string json = "{name:HideStartupsAppsFromSettings, hide:" + $"{hide}" + "}";
            SendMessage(json);
        }

        public void RequestDisableAllAdminRestrictions(string appPath)
        {
            string json = "{name:DisableAllAdminRestrictions, appPath:" + $"{appPath}" + "}";
            SendMessage(json);
        }

        public void RequestDisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock)
        {
            string json = "{name:DisableAllAdminRestrictions, appPath:" + $"{appPath}, foldersPathToUnlock:{foldersPathToUnlock}" + "}";
            SendMessage(json);
        }

        public void RequestDisableAllPossibleRestrictions(string appPath)
        {
            string json = "{name:DisableAllPossibleRestrictions, appPath:" + $"{appPath}" + "}";
            SendMessage(json);
        }

        public void RequestSetWebsiteBlocked(bool block, string domain)
        {
            string json = "{name:SetWebsiteBlocked," + $"block:{block}, domain:{domain}" + "}";
            SendMessage(json);
        }

        public void RequestSetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "useThisApp")
        {
            string json = "{name:SetStartupSafeMode," + $"autoStartEnabled:{autoStartEnabled}, applicationFullPath:{applicationFullPath}" + "}";
            SendMessage(json);
        }

        public void RequestUninstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service")
        {
            string json = "{name:UninstallService," + $" serviceExeName: {serviceExeName}, serviceName:{serviceName}" + "}";
            SendMessage(json);
        }

        public void Dispose()
        {
            if (client != null)
            {
                client.Close();
            }
        }


        private class StringArg : EventArgs
        {
            public readonly string message;

            public StringArg(string msg)
            {
                this.message = msg;
            }
        }
    }
}

