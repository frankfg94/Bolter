using Bolter;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;

namespace BolterAdminApp
{
    class Program
    {
        static string appUsingBolterPath = "unknown";

        // TODO : create a service , so we will only one install with uac prompt at the beginning and we can also keep the IPC
        static void Main(string[] args)
        {
            Console.WriteLine("Started BolterAdminApp");
            try
            {
                ProcessStartHandling(args);
                StartClient();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                new ManualResetEvent(false).WaitOne();
            }

        }

        private static void ProcessStartHandling(string[] args)
        {
            if (args.Length < 1)
            {
                throw new Exception("No arguments. At least one argument is needed (the path of the app) prefixed with p:");
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (i == 0)
                {
                    if (args[0].StartsWith("p:"))
                    {
                        // Store the path as the first param
                        appUsingBolterPath = args[0].Split(":").Last();
                    }
                    else
                    {
                        // We need the path as the first argument because it is needed for many commands, it must assigned before commands start running
                        throw new InvalidOperationException("Path must be indicated as the first argument with prefix p:");
                    }
                }
                else
                {
                    if (args[i].Contains("unblock"))
                    {
                        Bolter.Admin.DisableAllPossibleRestrictions(appUsingBolterPath);
                        Console.WriteLine("Unblocked everything successfully, exiting");
                        return;
                    }
                    else
                    {
                        EnableUacSecurity(args[i]);
                    }
                }
            }
        }


        private static void StartClient()
        {
            var client = new NamedPipeClientStream("PipesOfPiece");
            client.Connect(10000);
            new Thread(() =>
            {
                StreamReader reader = new StreamReader(client);
                while (true)
                {
                    if (!reader.EndOfStream)
                    {
                        var serverMsg = reader.ReadLine();
                        Console.WriteLine("received: " + serverMsg);
                        if (serverMsg.Contains("unblock"))
                        {
                            Admin.DisableAllPossibleRestrictions(appUsingBolterPath);
                            Console.WriteLine("Unblocked everything successfully");
                        }
                        else
                        {
                            EnableUacSecurity(serverMsg);
                        }
                    }
                }
            }).Start();
            Thread.Sleep(200);
        }

        private static void EnableUacSecurity(string commandName)
        {
            switch (commandName)
            {
                case "InstallAdminService":
                    Admin.InstallService();
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
                    Bolter.Other.Warn("Unknown command : " + commandName);
                    break;
            }
        }
    }
}
