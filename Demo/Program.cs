using Bolter;
using Bolter.BolterAdminApp;
using System;
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
            Console.WriteLine("This is the demo (console version)");
            // folderLockTest();
            // Admin.SetStartupSafeMode(false);
            IpcAdminServiceTest();
            // IPCTest();
            new ManualResetEvent(false).WaitOne();
        }

        static void IpcAdminServiceTest()
        {
            Admin.InstallService();
            var server = new IpcServer();
            server.ConnectToAdminBolterService(30 * 1000); // We give 30 seconds for the service to load
            server.StartListeningToClient();
            server.SendToClient("Hello");

            // Auto free
            var timer = new System.Timers.Timer(10000);
            timer.Elapsed += (s, e) => server.StopServer();
            timer.AutoReset = false;
            timer.Start();
        }
        static void  IPCTest()
        {
            var server =  new IpcServer();
            // TODO relative paths
            var bridgePath = @"C:\Users\franc\source\repos\Bolter\BridgeProcess\bin\Debug\netcoreapp3.1\BridgeProcess.exe";
            var adminAppPath = @"C:\Users\franc\source\repos\Bolter\Bolter\Resources\BolterAdminApp.exe";
            server.ConnectToClient(adminAppPath,bridgePath);
            Console.WriteLine("Connected");
            server.SendToClient("unblock");
            server.SendToClient("ooo");
            Thread.Sleep(2000);
            server.StopServer();
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
