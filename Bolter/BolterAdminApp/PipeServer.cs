using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace Bolter.BolterAdminApp
{
    // TODO : create a controller like implementation
    public class PipeServer : IDisposable
    {
        private NamedPipeServerStream server;

        /// <summary>
        /// Takes a json as an input & tries to execute an Admin command with it
        /// </summary>
        /// <param name="jsonStr"></param>
        public void ExecuteUACCommand(string jsonStr)
        {
            // SendToClient("Server : received command");
            dynamic jsonObj = null;
            bool commandFound = true;
            try
            {
                jsonObj = JObject.Parse(jsonStr);
                var s2 = (string)jsonObj.name;
                if (s2 == null)
                {
                    SendToClient(Properties.Resources.json_no_name);
                    return;
                };
            }
            catch (JsonReaderException)
            {
                SendToClient($"Could not parse command, This string isn't a json object : {jsonStr}");
                return;
            }
            catch (InvalidCastException)
            {
                SendToClient(Properties.Resources.json_not_string);
                return;
            }
            // Doesn't work
            switch ((string)jsonObj.name)
            {
                case "Hello":
                    SendToClient("Hello i'm the service and it's working!");
                    break;
                case "InstallNtRights": // OK
                    Admin.InstallNtRights();
                    break;
                case "SetBatchAndCMDBlock":
                    Admin.SetBatchAndCMDBlock((bool)jsonObj.block);
                    break;
                case "PreventDateEditingW10":
                    Admin.PreventDateEditingW10((bool)jsonObj.block);
                    break;
                case "SetStartupSafeMode":
                    Admin.SetStartupSafeMode(
                        autoStartEnabled: (bool)jsonObj.block,
                        applicationFullPath: (string)jsonObj.applicationFullPath);
                    break;
                case "InstallService":
                    Admin.InstallService(
                            serviceExeName: (string)jsonObj.serviceExeName,
                            serviceName: (string)jsonObj.serviceName);
                    break;
                case "UninstallService":
                    Admin.UninstallService(
                        serviceExeName: (string)jsonObj.serviceExeName,
                        serviceName: (string)jsonObj.serviceName);
                    break;
                case "HideStartupsAppsFromSettings":
                    Admin.HideStartupsAppsFromSettings(
                       hide: (bool)jsonObj.hide
                        );
                    break;
                case "DisableAllAdminRestrictions":
                    if (jsonObj.foldersPathToUnlock)
                    {
                        Admin.DisableAllAdminRestrictions(
                            appPath: (string)jsonObj.appPath,
                            foldersPathToUnlock: (string[])jsonObj.foldersPathToUnlock
                            );
                    }
                    else
                    {
                        Admin.DisableAllAdminRestrictions(appPath: (string)jsonObj.appPath);
                    }
                    break;
                case "DisableAllPossibleRestrictions":
                    Admin.HideStartupsAppsFromSettings(
                        hide: (bool)jsonObj.hide
                        );
                    break;
                case "SetWebsiteBlocked":
                    Console.WriteLine("SetWebsiteBlocked is not supported yet, because we need to pass each website address");
                    break;
                default:
                    commandFound = false;
                    Other.Warn("Unknown command : " + jsonObj);
                    SendToClient("Unknown command : " + JObject.Parse(jsonStr).ToString(Formatting.None));
                    break;
            }
            if (commandFound)
            {
                SendToClient(">> Admin command executed : " + (string)jsonObj.name);
            }
        }

        private void SendToClient(string message)
        {
            Console.Write($"[Service/Admin app]Sending message ( {message} ) ");
            try
            {
                server.WaitForPipeDrain();
                var serveWriter = new StreamWriter(server);
                serveWriter.AutoFlush = true;
                serveWriter.WriteLine(message);
                Console.Write(" OK\n");
                server.WaitForPipeDrain();

                // serveWriter.Flush();
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                Console.WriteLine("[Service/Admin app] Error: {0}", e.Message);
            }
        }

        /// <summary>
        /// Start the server, it must be started just befoe the client
        /// </summary>
        public void Start()
        {
            Console.WriteLine("Starting mock service, waiting for clients...");
            using (server = new NamedPipeServerStream(Properties.Resources.named_pipes_name, PipeDirection.InOut, 2))
            {

                server.WaitForConnection();
                server.WaitForPipeDrain();
                Console.WriteLine("Connected to the client");

                using StreamReader reader = new StreamReader(server);
                while (true)
                {
                    try
                    {
                        if (!reader.EndOfStream)
                        {
                            Console.WriteLine("Waiting for client data...");
                            var serverMsg = reader.ReadLine();
                            Trace.WriteLine("received client command");
                            File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\log.txt", "Received command : " + serverMsg + " " + DateTime.Now + Environment.NewLine);
                            if (serverMsg.Contains("unblock", StringComparison.OrdinalIgnoreCase))
                            {
                                Admin.DisableAllPossibleRestrictions("");
                                Trace.WriteLine("Unblocked everything successfully");
                            }
                            else
                            {
                                ExecuteUACCommand(serverMsg);
                            }
                        }
                        else
                        {
                            // Restart if no client to enable reconnectio
                            server.Disconnect();
                            reader.Close();
                            Start();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        try
                        {
                            SendToClient(Environment.NewLine + e.Message + Environment.NewLine + e.StackTrace);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

        }

        public void Dispose()
        {
            if (server != null)
            {
                server.Disconnect();
                server.Dispose();
            }

        }
    }
}
