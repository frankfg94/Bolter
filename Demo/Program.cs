using Bolter;
using Bolter.BolterAdminApp;
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
        static void Main(string[] args)
        {
            Console.WriteLine("This is the demo client (console version)");
            // folderLockTest();
            // Admin.SetStartupSafeMode(false);
            // RealServiceTest(false);
            // IPCAdminBridgeTest();
            TcpAdminBridgeTest();
            new ManualResetEvent(false).WaitOne();
        }

        private static void TcpAdminBridgeTest()
        {
            string IP_SERVER_ADDRESS = "127.0.0.1";
            int PORT = 8976;
            var tcpClient = new ReceiverClient();
            // TODO relative paths
            var bridgePath = @"C:\Users\franc\source\repos\Bolter\BridgeProcess\bin\Debug\netcoreapp3.1\BridgeProcess.exe";
            var mockServiceApp = @"C:\Users\franc\source\repos\Bolter\MockService\bin\Debug\netcoreapp3.1\MockService.exe";
            tcpClient.ConnectToBridge(mockServiceApp, bridgePath);
            tcpClient.JoinServerConsole(IP_SERVER_ADDRESS, PORT);

            // Info commands
            tcpClient.SendMessage("unblocksqdsqd");
            tcpClient.SendMessage("ooo");
            tcpClient.RequestInstallService();
            Console.ReadLine();
            Bolter.NonAdmin.DisableAllNonAdminRestrictions();
            tcpClient.RequestDisableAllAdminRestrictions(AppDomain.CurrentDomain.BaseDirectory);

        }

        static void RealServiceTest(bool install)
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
