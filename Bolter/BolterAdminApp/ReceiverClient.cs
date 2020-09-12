﻿using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bolter.BolterAdminApp
{
    /// <summary>
    /// TCP Client side listener for Bolter
    /// </summary>
    public class ReceiverClient : IAdminCommands
    {
        private const bool DEBUG_SHOW_RCV_COMMANDS = false;
        Process bridge;
        TcpClient comm;
        Thread commandThread;
        void ListenForClientCommands()
        {
            while (true)
            {
                keyboardCommand = Console.ReadLine();
                // We send data here
                if (keyboardCommand != string.Empty)
                {

                    var msg = keyboardCommand;

                    Net.SendMsg(comm.GetStream(), msg);
                    if (comm.GetStream().DataAvailable)
                    {
                        var responseMsg = Net.RcvMsg(comm.GetStream());
                        if (responseMsg != null)
                        {
                            try
                            {
                                DoOperationClientSide(responseMsg);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error parsing & processing in client : " + ex);
                            }
                        }
                    }
                }
            }
        }

        string keyboardCommand = "";
        public void JoinServerConsole(string IP_SERVER_ADDRESS, int PORT)
        {
            new Thread(() => {
            Console.WriteLine("[CLIENT] Creating TcpClient");
            comm = new TcpClient(IP_SERVER_ADDRESS, PORT);
            Console.WriteLine("[CLIENT] Connection OK");
            Console.WriteLine("Welcome! Listening for messages from the server ");
            commandThread = new Thread(ListenForClientCommands);
            commandThread.Start();
            while (true)
            {
                if (keyboardCommand != "" || comm.GetStream().DataAvailable)
                {
                    var keyboardTemp = keyboardCommand;
                    // On reçoit quelque chose, comme par exemple un message
                    var msg = Net.RcvMsg(comm.GetStream());
                    {
                        try
                        {
                            DoOperationClientSide(msg);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error parsing & processing in client : " + ex);
                        }
                    }
                }
            }

            }).Start();
        }

        public void ConnectToBridge(string ipcClientExePath, string bridgeExePath)
        {
            ConnectToBridge(ipcClientExePath, bridgeExePath, System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        public void SendMessage(string json)
        {
            while(comm == null)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Sending message : " + json);
            Net.SendMsg(comm.GetStream(), json);
        }

        public void RequestInstallNtRights()
        {
            JObject o = new JObject();
            o.Add("name", new JValue("InstallNtRights"));
            SendMessage(o.ToString());
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
            JObject o = new JObject();
            o.Add("name", new JValue("HideStartupsAppsFromSettings"));
            o.Add("hide", new JValue(hide));
            SendMessage(o.ToString());
        }

        public void RequestDisableAllAdminRestrictions(string appPath)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("DisableAllAdminRestrictions"));
            o.Add("appPath", new JValue(appPath));
            SendMessage(o.ToString());
        }

        public void RequestDisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("DisableAllAdminRestrictions"));
            o.Add("appPath", new JValue(appPath));
            o.Add("foldersPathToUnlock", new JValue(foldersPathToUnlock));
            SendMessage(o.ToString());
        }

        public void RequestSetWebsiteBlocked(bool block, string domain)
        {
            JObject o = new JObject();
            o.Add("name", new JValue("SetWebsiteBlocked"));
            o.Add("block", new JValue(block));
            o.Add("domain", new JValue(domain));
            SendMessage(o.ToString());
        }

        public void RequestSetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "useThisApp")
        {
            JObject o = new JObject();
            o.Add("name", new JValue("SetStartupSafeMode"));
            o.Add("autoStartEnabled", new JValue(autoStartEnabled));
            o.Add("applicationFullPath", new JValue(applicationFullPath));
            SendMessage(o.ToString());
        }

        public void RequestUninstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service")
        {
            JObject o = new JObject();
            o.Add("name", new JValue("UninstallService"));
            o.Add("serviceExeName", new JValue(serviceExeName));
            o.Add("serviceName", new JValue(serviceName));
            SendMessage(o.ToString());
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
                bridge.OutputDataReceived += TcpClient_OutputDataReceived;
                bridge.ErrorDataReceived += TcpClient_ErrorDataReceived;
                try
                {
                    bridge.Start();
                }
                catch (Exception)
                {
                    Console.WriteLine($"The UAC was denied for {bridge.StartInfo.FileName}");
                    throw;
                }
                // Start();
                Task.Delay(1000).Wait();
                bridge.WaitForExit();
            }
            else
            {
                throw new NullReferenceException("One of the paths is null");
            }
        }

        private void TcpClient_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void TcpClient_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        void DoOperationClientSide(string msg)
        {
            try
            {
                switch (msg)
                {
                    default:
                        string err = "[SERVER] Response :" + msg;
                        Console.WriteLine(err);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Invalid command format " + ex);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
