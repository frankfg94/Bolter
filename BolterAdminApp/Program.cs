using Bolter;
using System;
using System.IO;
using System.Linq;
using log4net;
using log4net.Config;
using System.ServiceProcess;
using System.Threading;
using System.Reflection;

namespace BolterAdminApp
{
    class Program
    {
        static string appUsingBolterPath = "unknown";
        private const string LOG_4_NET_FILE = "log4net.config";
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// This app must be run with administratives privileges because its purpose is to run Bolter.Admin static methods. <br/>
        /// Its main purpose is to install the bolter admin service (to avoid running the UAC prompt in the used apps) to do this, we can use <see cref="Admin.InstallService(string, string, string, bool, bool)"/><br/>
        /// <br/> Note : This is supposed to be started with the bridge process, but it can be used directly for debugging it or if we don't want to use the Bolter Service
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            System.AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException; ;

            // Load log4net configuration
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(LOG_4_NET_FILE));
            log.Debug("Started app with args : (\n" + string.Join("\n\t",args) + "\n)");


            Console.WriteLine("---------------------------------");
            Console.WriteLine("| Bolter admin command executor  |");
            Console.WriteLine("---------------------------------");
            try
            {
                ProcessStartHandling(args); // First feature, execute all commands passed directly into the app
                StartIPCClient(); // Second feature, create a server to keep the listener active (not needed bc of the service)
            }
            catch (Exception e)
            {
                log.Error(e);
                log.Info("Keeping this window open, because an error occured");
                Console.ReadLine();
                Environment.Exit(1);
            }
            finally
            {
                log.Info("Operations finished, closing admin bolter app...");
            }

        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            log.Error(e.ExceptionObject.ToString());
            Console.WriteLine("Press Enter to continue");
            Console.ReadLine();
            Environment.Exit(1);
        }

        private static void ProcessStartHandling(string[] args)
        {

            if (args.Length < 1)
            {
                throw new ArgumentNullException("No arguments. At least one argument is needed (the path of the app) prefixed with p:");
            }

            for (int i = 0; i < args.Length; i++)
            {
                log.Debug("Scanning arg : " + args[i]);
                if (i == 0)
                {
                    if (args[i].StartsWith("p:"))
                    {
                        // Store the path as the first param
                        appUsingBolterPath = Other.UnescapeCMD(args[0].Split(":").Last());
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
                        log.Info("Unblocked everything successfully, exiting");
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
            log.Info("Trying to execute command : " + commandName);
            switch(commandName.ToLower())
            {
                case "installadminservice":
                case "installservice":
                    Admin.InstallService();
                    break;
                default:
                    var msg = "Unknown command : " + commandName;
                    Other.Warn(msg);
                    log.Warn(msg);
                    break;
            }
        }
    }
}
