using BolterRespawner.Properties;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace BolterRespawner
{
    class Program
    {
        #region Windows API low level

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        #endregion

        // This program is a console program converted to windows prorgam (see in project properties), to make it function even if CMD is blocked
        // This programs also copies its files to bolter after each visual studio build

        /// <summary>
        /// TODO : add unit testing for such a powerful feature
        /// This is a run & forget program, at the moment it can be interrupted by killing it in C# with process class
        /// </summary>
        /// <param name="args">The first argument must be a path, the other arguments must be process names</param>
        static void Main(string[] args)
        {
            TestWithSampleProgram(@"C:\Program Files (x86)\Windscribe\WindscribeLauncher.exe", new string[] {"Windscribe", "wsappcontrol", "WindscribeInstallHelper" } , ref args, TimeSpan.FromMinutes(2));
            if(args.Length < 1) // No args
            {
                throw new InvalidOperationException("You need at least one argument, which is the path for the program to restart");
            }
            else if (args.Length == 1 ) // No connected processes are given in the args
            {
                Bolter.Other.Warn("You should provide if possible multiple process names, to make sure the program won't be respawned indefinitely");
            }
            string appToRespawnPath = args[0].Trim().Replace("\r\n", string.Empty); // path cleaning & assigning
            var connectedProcesses = new string[args.Length - 1]; // We create an array that is of size -1
            for (int i = 1; i < args.Length; i++) // we assign all the processes names (every arg except the first)
            {
                connectedProcesses[i - 1] = args[i];
            }
            try
            {
                System.Timers.Timer aTimer = new System.Timers.Timer();

                // Start the respawn loop
                aTimer.Elapsed += (s,e) => {
                    Console.WriteLine("Verif du processus : " + appToRespawnPath);
                    var processName = appToRespawnPath.Split("\\").Last().Replace(".exe","");
                    var initialProcess = Process.GetProcessesByName(processName);
                    Console.WriteLine("Path is " + appToRespawnPath);
                    if (File.Exists(appToRespawnPath))
                    {
                        if(initialProcess.Length == 0 )
                        {
                            if (!OneOfThoseProcessIsRunning(connectedProcesses))
                            {
                                //Console.WriteLine("Relance Assistant");
                                try
                                {
                                    Console.WriteLine(new Uri(appToRespawnPath).LocalPath);
                                    Process p = new Process();
                                    p.StartInfo.UseShellExecute = false;
                                    p.StartInfo.Arguments = "p="+ Process.GetCurrentProcess().MainModule.FileName;  // Le shell execute ne fonctionne pas pour le moment
                                    p.StartInfo.FileName = appToRespawnPath;
                                    Console.WriteLine($"{appToRespawnPath} is not started --> Starting process with arguments: ({0})", p.StartInfo.Arguments);
                                    Console.WriteLine("We start the .exe :" + p.StartInfo.FileName);
                                    p.Start();
                                    Bolter.NonAdmin.ResumeProcess(appToRespawnPath);
                                }
                                catch (Win32Exception ex)
                                {

                                    Console.WriteLine(ex.StackTrace);
                                    throw ex;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error file doesn't exists at : " + appToRespawnPath);
                        Console.WriteLine("Stopping timer");
                        File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\logWatcher.txt", "Le chemin n'existe pas à {" + appToRespawnPath + "}");
                        (s as System.Timers.Timer).Stop();
                        Environment.Exit(0);

                    }
                };
                aTimer.Interval = 1000; // Hardcoded the interval for the moment, 1s is fast enough
                aTimer.Enabled = true;
                aTimer.Start();

                // Prevent the program from terminating automatically
                new ManualResetEvent(false).WaitOne();
            }
            catch (Exception ex)
            {

                File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\logWatcher.txt", Environment.NewLine + Environment.NewLine + ex.StackTrace + Environment.NewLine + Environment.NewLine);
            }
        }

        /// <summary>
        ///  Call this function at the beginning of the Main method the override the program to be tested
        /// </summary>
        /// <param name="programPath">The path of the program to restart when the program executed at this path is closed OR when all the processes in <paramref name="connectedProcessesName"/> aren't running</param>
        /// <param name="args">The reference to the main method args, to override them</param>
        /// <param name="connectedProcessesName">Often, the program that you want to start might not stay opened, for example the executable of the program path you indicate will just be an updater that points to the main program that will always be alive.
        /// This updater will then be closed, to avoid often restarting the program with the path indicated in <paramref name="programPath"/></em> you should provide additionnal process names, including if possible the name of the process that will maintain the program alive (eg Windscribe uses WindscribeLauncher for starting, Winscribe is the main executable so we should have Windscribe as one of the connected process names </param>
        private static void TestWithSampleProgram(string programPath, string[] connectedProcessesName, ref string[] args, TimeSpan testDuration)
        {
            args = new string[connectedProcessesName.Length +1];
            for (int i = 0; i < connectedProcessesName.Length + 1; i++)
            {
                if(i== 0)
                {
                    args[0] = programPath; // This process will be started
                }
                else
                {
                    args[i] = connectedProcessesName[i-1]; // The processes that will indicate (if they are running) that the process doesn't need to be started
                }
            }
            System.Timers.Timer t = new System.Timers.Timer { 
                Interval = testDuration.TotalMilliseconds,
                AutoReset = false};
            t.Elapsed += (s,e) => {
                Console.WriteLine("Test finished! Closing");
                Environment.Exit(0);
            };
            t.Start();
        }

        public static bool OneOfThoseProcessIsRunning(string[] processesNames)
        {
            foreach (var processName in processesNames)
            {
                if( Process.GetProcessesByName(processName).Length > 0 )
                {
                    return true;
                }
            }
            return false;
        }
    }
}
