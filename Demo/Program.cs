using Bolter;
using Bolter.BolterAdminApp;
using Matrix;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowsDesktop;

namespace Demo
{
    class Program
    {
        static string IP_SERVER_ADDRESS = "127.0.0.1";
        static int PORT = 8976;
        static void Main(string[] args)
        {
            Console.WriteLine("This is the demo client (console version), user : " + Environment.UserName);
            // folderLockTest();
            // Admin.SetStartupSafeMode(false);
            // RealServiceTest(false);
            // IPCAdminBridgeTest();
            // TcpAdminBridgeTest();
            // InstallService();
            // NonAdmin.SetBatchAndCmdBlock(true);
            if (!NonAdmin.IsInAdministratorMode())
            {
                ReceiverClient cl = new ReceiverClient();
                Admin.InstallService();
                // cl.RequestImpersonation(4);
                Console.WriteLine("[MATRIX] Requesting remote service admin commands");
                // Use the service or bridge admin app
                cl.ConnectToBolterService(IP_SERVER_ADDRESS, PORT);
                // cl.RequestSetBatchAndCMDBlock(false);
                // cl.RequestPreventDateEditingW10(true);
                // cl.RequestSetBatchAndCMDBlock(false);
                // AdminSandbox.RequestRemoteCommands(cl);
                Console.WriteLine("[MATRIX] All commands requested");
                // Thread.Sleep(20000);
                Console.WriteLine("Now unblocking");
                cl.RequestDisableAllAdminRestrictions(AppDomain.CurrentDomain.BaseDirectory);
                Console.WriteLine("Sleeping 20s");
            }
            else
            {
               Admin.SetBatchAndCMDBlock(false, "franc");
               Admin.PreventDateEditingW10(true);
               Admin.SetBatchAndCMDBlock(false, "franc");
            }
            Console.WriteLine("Unblocked Admin");
            NonAdmin.DisableAllNonAdminRestrictions();
            //cl.RequestDisableAllAdminRestrictions(AppDomain.CurrentDomain.BaseDirectory);
            // RealServiceTestTCP(true);
            new ManualResetEvent(false).WaitOne();
        }

        private static void InstallService()
        {
                Admin.InstallService();
            if(NonAdmin.IsInAdministratorMode())
            {
            };
        }

        private static void TcpAdminBridgeTest()
        {
         
            var tcpClient = new ReceiverClient();
            // TODO relative paths
            var bridgePath = @"C:\Users\franc\source\repos\Bolter\BridgeProcess\bin\Debug\netcoreapp3.1\BridgeProcess.exe";
            var mockServiceApp = @"C:\Users\franc\source\repos\Bolter\MockService\bin\Debug\netcoreapp3.1\MockService.exe";
            tcpClient.ConnectToBolterService(IP_SERVER_ADDRESS, PORT);

            // Info commands
            tcpClient.SendMessage("unblocksqdsqd");
            tcpClient.SendMessage("ooo");
            tcpClient.RequestInstallService();
            Console.ReadLine();
            Bolter.NonAdmin.DisableAllNonAdminRestrictions();
            tcpClient.RequestDisableAllAdminRestrictions(AppDomain.CurrentDomain.BaseDirectory);

        }
        static void RealServiceTestTCP(bool install)
        {
            string IP_SERVER_ADDRESS = "127.0.0.1";
            int PORT = 8976;

            if (install)
            {
                if(Bolter.NonAdmin.IsInAdministratorMode())
                {
                Admin.InstallService();
                } else
                {
                    throw new InvalidOperationException("Need the demo to be in Admin mode to install the service");
                }
            }

            var client = new ReceiverClient();
            client.ConnectToBolterService(IP_SERVER_ADDRESS, PORT);
            // client.SendMessage("First_msg");
            // client.SendMessage("2nd_msg");
            // client.SendMessage("last msg");
            // client.SendMessage("2nd_msg_2");
            int LOGON32_LOGON_BATCH = 4; // DOESNT WORK
            int LOGON32_LOGON_NETWORK = 3;
            int LOGON32_LOGON_INTERACTIVE = 2;
            int LOGON32_LOGON_NETWORK_CLEARTEXT = 8;
            int LOGON32_LOGON_SERVICE = 5;
            int type = LOGON32_LOGON_INTERACTIVE;
            client.RequestImpersonation(type);
            Thread.Sleep(5000);
            client.RequestSetBatchAndCMDBlock(true);

            //client.WaitAndPrintResponse();
            // client.SendToService("{ \"name\":\"SetBatchAndCMDBlock\",\"block\":false}");
            ///client.SendToService("Last msg");
            // client.RequestSetBatchAndCMDBlock(false);
            // client.RequestSetBatchAndCMDBlock(true);

            // Auto free
            var timer = new System.Timers.Timer(20000);
            timer.Elapsed += (s, e) =>
            {
                // Reconnect test
                // client.RequestDisableAllAdminRestrictions(AppDomain.CurrentDomain.BaseDirectory);
                client.SendMessage("Hello");
                Thread.Sleep(3000);
                client.SendMessage("Hello2");
                if (install)
                    Admin.UninstallService();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        static void RealServiceTestPipes(bool install)
        {
            if(install)
            Admin.InstallService();
            var client = new PipeClient();
            client.ConnectToAdminBolterService(4  * 1000); // We give 30 seconds for the service to load
            client.SendMessage("First_msg");
            client.SendMessage("2nd_msg");
            client.SendMessage("last msg"); 
            client.SendMessage("2nd_msg_2");
            client.RequestSetBatchAndCMDBlock(false);

            //client.WaitAndPrintResponse();
            // client.SendToService("{ \"name\":\"SetBatchAndCMDBlock\",\"block\":false}");
            ///client.SendToService("Last msg");
            // client.RequestSetBatchAndCMDBlock(false);
            // client.RequestSetBatchAndCMDBlock(true);

            // Auto free
            var timer = new System.Timers.Timer(10000);
            timer.Elapsed += (s, e) =>
            {
                client.RequestSetBatchAndCMDBlock(false);
                // Reconnect test
                client.Stop();
                client = new PipeClient();
                client.ConnectToAdminBolterService(4 * 1000);
                client.SendMessage("Hello");
                Thread.Sleep(3000);
                client.SendMessage("Hello2");
                if (install)
                    Admin.UninstallService();
            };
            timer.AutoReset = false;
            timer.Start();
        }


        static void  IPCAdminBridgeTest()
        {
            var server =  new PipeClient();
            // TODO relative paths
            var bridgePath = @"C:\Users\franc\source\repos\Bolter\BridgeProcess\bin\Debug\netcoreapp3.1\BridgeProcess.exe";
            var adminAppPath = @"C:\Users\franc\source\repos\Bolter\Bolter\Resources\BolterAdminApp.exe";
            server.ConnectToBridge(adminAppPath,bridgePath);
            Console.WriteLine("Connected");
            server.SendMessage("unblock");
            server.SendMessage("ooo");
            Thread.Sleep(2000);
            server.Stop();
        }

        static void folderLockTest()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\BUNKER";
            Directory.CreateDirectory(path);
            NonAdmin.LockFolder(path, false);
            Thread.Sleep(20000);
            NonAdmin.UnlockFolder(path);
            Console.ReadLine();
        }
    }
}
