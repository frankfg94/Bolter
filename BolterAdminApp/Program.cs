using Bolter;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Documents;

namespace BolterAdminApp
{
    class Program
    {
        static string appUsingBolterPath = "unknown";
        bool quickTest = true;
        /// <summary>
        // THis is started with the bridge process
        /// </summary>
        /// <param name="args"></param>
        // TODO : create a service , so we will only one install with uac prompt at the beginning and we can also keep the IPC
        static void Main(string[] args)
        {
            Console.WriteLine("Started BolterAdminApp");
            try
            {
                ProcessStartHandling(args); // First feature, execute all commands
                StartIPCClient(); // Second feature, create a server to keep the listener active (not needed bc of the service)
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                new ManualResetEvent(false).WaitOne();
            }
            finally
            {
               // new ManualResetEvent(false).WaitOne();
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
                        ExecuteUACCommand(args[i]);
                    }
                }
            }
        }


        private static void StartIPCClient()
        {
      //      var client = new NamedPipeClientStream("PipesOfPiece");
      //      client.Connect(10000);
     //       new Thread(() =>
     //       {
    //            StreamReader reader = new StreamReader(client);
    //            while (true)
   //             {
   //                 if (!reader.EndOfStream)
  //                  {
  //                      var serverMsg = reader.ReadLine();
  //                      Console.WriteLine("received: " + serverMsg);
  //                      if (serverMsg.Contains("unblock"))
                        {
     //                       Admin.DisableAllPossibleRestrictions(appUsingBolterPath);
    //                        Console.WriteLine("Unblocked everything successfully");
                        }
      //                  else
                        {
      //                      ExecuteUACCommand(serverMsg);
                        }
//                    }
//                }
//            }).Start();
//           Thread.Sleep(200);
        }

        private static void ExecuteUACCommand(string commandName)
        {
            var serviceName = "Bolter Admin Service";
            var serviceExeName = "AdminBolterService";
            string projectDirPath = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName; // path for all visual studio projects
            var path = projectDirPath+@$"\Bolter\AdminBolterService\bin\Release\netcoreapp3.1\publish\{serviceExeName}.exe";
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Service can't be created because executable not found at " + path);
            }
            switch (commandName)
            {
                case "InstallAdminService":
                    System.Diagnostics.Process.Start("CMD.exe", $"/C sc stop \"{serviceName}\"");
                    Thread.Sleep(5000);
                    Console.WriteLine(">>> Service stopped");
                    System.Diagnostics.Process.Start("CMD.exe", $"/C sc delete \"{serviceName}\"");
                    Console.WriteLine(">>> Service deleted");
                    Thread.Sleep(5000);
                    var commandCreateService = $"/C sc create \"{serviceName}\" binPath=" + path;
                    System.Diagnostics.Process.Start("CMD.exe", commandCreateService);
                    Console.WriteLine(">>> Service created");
                    Console.WriteLine("Path :" + path);
                    Thread.Sleep(5000);
                    System.Diagnostics.Process.Start("CMD.exe", $"/C sc config \"{serviceName}\" start=\"auto\"");
                    Console.WriteLine(">>> Automatic startup set");
                    Thread.Sleep(200);
                    System.Diagnostics.Process.Start("CMD.exe", $"/C sc query \"{serviceName}\"");
                    Thread.Sleep(3000);
                    ServiceController service = new ServiceController(serviceName);

                    if ((service.Status.Equals(ServiceControllerStatus.Stopped)) ||

                        (service.Status.Equals(ServiceControllerStatus.StopPending)))

                        service.Start();
                    Console.WriteLine(">>> Service started");
                    Thread.Sleep(5000);
                    break;
                case "InstallNtRights":
                    Admin.InstallNtRights();
                    break;
                case "SetBatchAndCMDBlock":
                    Admin.SetBatchAndCMDBlock(true, Environment.UserName);
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
