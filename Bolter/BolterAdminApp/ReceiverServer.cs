using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Bolter.BolterAdminApp
{

        public class ReceiverServer
        {
            private TcpClient comm;

            public ReceiverServer(TcpClient comm)
            {
                this.comm = comm;
            }

            bool run = true;

            /// <summary>
            ///  identifier of a message
            /// </summary>
            public static int msgGlobalId = 0;
            private static object lockGenId = new object();
            private static object lockSendMsg = new object();
            private static object lockEnterTopic = new object();

            public void doOperation()
            {
                while (run)
                {
                    try
                    {
                        if (!comm.Connected)
                        {
                            Console.WriteLine("Client disconnected from the server : " + comm.Client.RemoteEndPoint);
                            comm.Dispose();
                            run = false;
                        }
                        else
                        {
                            string data = Net.RcvMsg(comm.GetStream());
                            if (data is string msg)
                            {
                                Console.WriteLine("Signal received : " + msg);
                            Net.SendMsg(comm.GetStream(), "Received message : " + msg);
                            doOperationsAsUser(msg, comm);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        if (comm.Connected)
                        {
                            Console.WriteLine("Invalid command format " + ex);
                            Console.WriteLine(ex.StackTrace);
                            Net.SendMsg(comm.GetStream(), "[SERVER] Invalid command format " + ex.Message);
                            Console.WriteLine("--> Sent an invalid command format message to the client");
                        }
                    }
                }
            }

     

            /// <summary>
            ///  Analyse the commandPart property of the message passed in parameter (received from the client) and execute a server operation
            /// </summary>
            /// <param name="msg">The message that contains the command, and additionnal data such as bytes arrays for files</param>
            /// <param name="comm">The TcpClient of the client that sent this command</param>
            public void doOperationsAsUser(string jsonStr, TcpClient comm)
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
                    Net.SendMsg(comm.GetStream(),Properties.Resources.json_no_name);
                    return;
                };
            }
            catch (JsonReaderException)
            {
                Net.SendMsg(comm.GetStream(),$"Could not parse command, This string isn't a json object : {jsonStr}");
                return;
            }
            catch (InvalidCastException)
            {
                Net.SendMsg(comm.GetStream(),Properties.Resources.json_not_string);
                return;
            }
            // Doesn't work
            switch ((string)jsonObj.name)
            {
                case "ping":
                case "Ping":
                case "hello":
                case "Hello":
                    Net.SendMsg(comm.GetStream(),"Hello i'm the service and it's working!");
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
                    if (HasProperty(jsonObj,"foldersPathToUnlock"))
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
                    Admin.DisableAllPossibleRestrictions(
                       appPath: (string)jsonObj.appPath
                        );
                    break;
                case "SetWebsiteBlocked":
                    Console.WriteLine("SetWebsiteBlocked is not supported yet, because we need to pass each website address");
                    break;
                default:
                    commandFound = false;
                    Other.Warn("Unknown command : " + jsonObj);
                    Net.SendMsg(comm.GetStream(),"Unknown command : " + JObject.Parse(jsonStr).ToString(Formatting.None));
                    break;
            }
            if (commandFound)
            {
                Net.SendMsg(comm.GetStream(),">> Admin command executed : " + (string)jsonObj.name);
            }
        }


        public static bool HasProperty(dynamic obj, string name)
        {
            try
            {
                var value = obj[name];
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        // 1 . The server receives the message from User 1
        // 2 (HERE) . The server redirects the messsage to User 2
        // To find User 2, we use its id
        private void SendMessageToUser(Stream destStream, string json)
            {
                Net.SendMsg(destStream, json);
                Console.WriteLine("msg sent to client : " + json);
            }
        }
}
