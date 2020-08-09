﻿using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Bolter.BolterAdminApp
{
    // At the moment IPC does work very well if the service was started a few minutes ago, but if we test it directly with install, it creates problems (not connected or pipebroken)
    public class IpcClient : IPCAdminInterface
    {
        public static string message = null;
        Process bridge = null;
        private event EventHandler<StringArg> RequestMessageSend;
        NamedPipeClientStream client;


        private void ConsoleClientWrite(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[CLIENT] ");
            Console.ResetColor();
            Console.Write(msg );
        }

        private void ConsoleServerWriteLine(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("[SERVER] ");
            Console.ResetColor();
            Console.Write(msg + Environment.NewLine);
        }

        /// <summary>
        /// Start the server that connects to the remote process or the remote service
        /// </summary>
        public void StartTheClient()
        {
             client = new NamedPipeClientStream("PipesOfPiece");
                Console.WriteLine("Connecting...");
                client.Connect();
                Console.WriteLine("Connected!");
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
                Console.WriteLine("[SERVER] Client quit. Server terminating.");
            }
            catch (Exception e)
            {

                Console.WriteLine(e);
            }
        }



        public void ConnectToAdminBolterService(int delayMs)
        {
            StartTheClient();
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
            StartTheClient();
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

        public void RequestInstallNtRights()
        {
            string json = "{name:InstallNtRights}";
            SendMessage(json);
        }

        public void RequestSetBatchAndCMDBlock(bool block)
        {
            JObject o = new JObject();
            o.Add("name",new JValue("SetBatchAndCMDBlock"));
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
            string json = "{name:DisableAllAdminRestrictions, appPath:" + $"{appPath}, foldersPathToUnlock:{foldersPathToUnlock}"+  "}";
            SendMessage(json);
        }

        public void RequestDisableAllPossibleRestrictions(string appPath)
        {
            string json = "{name:DisableAllPossibleRestrictions, appPath:" + $"{appPath}" + "}";
            SendMessage(json);
        }

        public void RequestSetWebsiteBlocked(bool block, string domain)
        {
            string json = "{name:SetWebsiteBlocked,"+ $"block:{block}, domain:{domain}"+"}";
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

