using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace BridgeProcess
{
    /// <summary>
    /// I really didn't want to do this, but you can't use IPC with run as administator with shellexecute, so i had to create a process with execute to false
    /// </summary>
    class Program
    {
        static bool bolterAppPathFound = false;
        private static bool uacAppPathFound = false;
        static string pathAppThatUsesBolter = null;
        static string uacAppPath = null;
        static bool quickTest = false; // if you test this program alone, it is normal that it will failed, because it will try to listen to a server

        // TODO : Big problem exe doesn't start
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Bridge started!");
                if (quickTest)
                {
                    args = new string[2];
                    args[0] = "uac:C:\\Users\\franc\\source\\repos\\Bolter\\Bolter\\Resources\\BolterAdminApp.exe";
                    args[1] = "p:" + AppDomain.CurrentDomain.BaseDirectory + "Demo.exe";
                }
                foreach (var arg in args)
                {
                    // Path of the administrator process, so that we can execute our commands directly in it
                    if (arg.StartsWith("uac:") && !uacAppPathFound)
                    {
                        uacAppPathFound = true;
                        uacAppPath = arg.Split(":").Last().Trim();
                        Console.WriteLine("UAC path : " + uacAppPath);
                    }

                    // Path of the main bolter app, it is required because we need the path for some methods to make them work correctly
                    if (arg.StartsWith("p:") && !bolterAppPathFound)
                    {
                        bolterAppPathFound = true;
                        pathAppThatUsesBolter = arg.Split(":").Last().Trim();
                        Console.WriteLine("Main app path : " + pathAppThatUsesBolter);
                    }
                };

                if (!bolterAppPathFound)
                {
                    throw new ArgumentException("You need to provide one argument with the p: prefix to indicate the main process / ipc server path");
                }
                if (!uacAppPathFound)
                {
                    throw new ArgumentException("You need to provide one argument with the uac: prefix to indicate the uac path");
                }
                RunUacProcess(uacAppPath, pathAppThatUsesBolter);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            } 
        }

        static void RunUacProcess(string uacProcessPath, string appThatUsesBolterPath)
        {
            Process p = new Process();
            if (!File.Exists(uacProcessPath))
            {
                Console.WriteLine("File doesn't exist");
            }
            p.StartInfo.FileName = uacProcessPath;
            p.StartInfo.Verb = "runas";
            p.StartInfo.ArgumentList.Add("p:"+appThatUsesBolterPath);
            //  p.StartInfo.WorkingDirectory = Path.GetDirectoryName(uacProcessPath);
            // p.StartInfo.RedirectStandardOutput = true;
            // p.StartInfo.RedirectStandardError = true;
            Console.WriteLine("Starting ipc client at : " + uacProcessPath);
            p.Start();
        }

        private static void data_received(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}
