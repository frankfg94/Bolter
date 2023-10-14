using Bolter.BolterAdminApp;
using Bolter.Program;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using WindowsDesktop;
using Timer = System.Timers.Timer;

namespace Bolter
{
    /// <summary>
    /// This class provides useful kiosk / security commands, it does not require the windows program to be in UAC/administrator mode
    /// </summary>
    public static class NonAdmin
    {
        #region wrappers & native c++ dependencies

        private enum ShowWindowEnum
        {
            Hide = 0,
            ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
            Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
            Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
            Restore = 9, ShowDefault = 10, ForceMinimized = 11
        };

        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // Code for the Task bar hiding
        [DllImport("user32.dll")]
        private static extern int FindWindow(string className, string windowText);
        [DllImport("user32.dll")]
        private static extern int GetDesktopWindow();
        [DllImport("user32.dll")]
        public static extern int FindWindowEx(int parentHandle, int childAfter, string className, int windowTitle);
        [DllImport("user32.dll")]
        private static extern int ShowWindow(int hwnd, int command);
        static int Handle
        {
            get
            {
                return FindWindow("Shell_TrayWnd", "");
            }
        }

        static int HandleOfStartButton
        {
            get
            {
                int handleOfDesktop = GetDesktopWindow();
                int handleOfStartButton = FindWindowEx(handleOfDesktop, 0, "button", 0);
                return handleOfStartButton;
            }
        }


        #region low level code pour désactiver ALT + TAB
        // Structure contain information about low-level keyboard input event
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public System.Windows.Forms.Keys key;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr extra;
        }

        [DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);
        internal const int SM_CLEANBOOT = 67;
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int smIndex);
        //System level functions to be used for hook and unhook keyboard input
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(System.Windows.Forms.Keys key);


        //Declaring Global objects
        private static IntPtr ptrHook;
        private static LowLevelKeyboardProc objKeyboardProcess;


        #region resume process
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
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        #endregion

        #endregion

        // Virtual desktop management
        [DllImport("user32.dll")]
        private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice, IntPtr pDevmode, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);
        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDesktop(int dwThreadId);


        public static void TryDisableAllPossibleRestrictions(string serviceName, string appPath, string ip, int port, string machineName)
        {
            NonAdmin.DisableAllNonAdminRestrictions();
            if (NonAdmin.DoesServiceExist(serviceName, machineName))
            {
                ReceiverClient client = new ReceiverClient();
                client.ConnectToBolterService(ip, port);
                client.RequestDisableAllAdminRestrictions(appPath);
            }
            else
            {
                Console.WriteLine($"Could not disable admin restrictions : '{serviceName}' not found");
            }
        }

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool SwitchDesktop(IntPtr hDesktop);

        enum DesktopAccess : uint
        {
            DesktopReadobjects = 0x0001,
            DesktopCreatewindow = 0x0002,
            DesktopCreatemenu = 0x0004,
            DesktopHookcontrol = 0x0008,
            DesktopJournalrecord = 0x0010,
            DesktopJournalplayback = 0x0020,
            DesktopEnumerate = 0x0040,
            DesktopWriteobjects = 0x0080,
            DesktopSwitchdesktop = 0x0100,

            GenericAll = DesktopReadobjects | DesktopCreatewindow | DesktopCreatemenu |
                         DesktopHookcontrol | DesktopJournalrecord | DesktopJournalplayback |
                         DesktopEnumerate | DesktopWriteobjects | DesktopSwitchdesktop
        }

        // Disconnect session
        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSDisconnectSession(IntPtr hServer, int sessionId, bool bWait);

        [DllImport("user32")]
        private static extern void LockWorkStation();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("Kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO Dummy);

        [DllImport("User32.dll")]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);

        #endregion

        #region AutoCloser & AutoLocker event handling

        public enum FolderListAction
        {
            Added = 0,
            Removed = 1
        }

        public enum ProgramListAction
        {
            Added = 0,
            Removed = 1
        }


        #endregion

        /// <summary>
        /// Enable or disable windows automatic foreground switch
        /// </summary>
        public static bool SwitchWindowEnabled = true;
        /// <summary>
        /// Enable or diable the automatic folder locker
        /// </summary>
        public static bool AutoStartAutoLocker = true;

        /// <summary>
        /// Return true if the current windows session is in safe mode
        /// </summary>
        /// <returns></returns>
        public static bool IsWindowsInSafeMode()
        {
            return GetSystemMetrics(SM_CLEANBOOT) != 0;
        }

        /// <summary>
        /// Close the process with its name
        /// </summary>
        /// <param name="programName"></param>
        public static void CloseProgram(string programName)
        {
            if (programName == null) return;
            // TODO : optimize if mediocre perf (88% of the profiler)
            foreach (var process in Process.GetProcessesByName(programName.Split(".exe")[0]))
            {
                process.Kill();
            }
            Console.WriteLine("Closed program : " + programName);
        }

        /// <summary>
        /// Return true if the service exists on the given machine
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="machineName"></param>
        /// <returns></returns>
        public static bool DoesServiceExist(string serviceName, string machineName)
        {
            ServiceController[] services = ServiceController.GetServices(machineName);
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);
            return service != null;
        }

        /// <summary>
        /// Callback to ignore or enable Alt + Tab
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wp"></param>
        /// <param name="lp"></param>
        /// <returns></returns>
        private static IntPtr captureKey(int nCode, IntPtr wp, IntPtr lp)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT objKeyInfo = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lp, typeof(KBDLLHOOKSTRUCT));
                if (objKeyInfo.key == System.Windows.Forms.Keys.RWin || objKeyInfo.key == System.Windows.Forms.Keys.LWin || objKeyInfo.key == System.Windows.Forms.Keys.Alt || objKeyInfo.key == System.Windows.Forms.Keys.Tab) // Disabling Windows keys
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(ptrHook, nCode, wp, lp);
        }

        /// <summary>
        /// Returns true if the current window is in foreground (directly visible and in fronts of the other windows)
        /// </summary>
        /// <returns></returns>
        public static bool IsInForeground()
        {
            var curActivatedHandle = GetForegroundWindow(); // Obtient l'application en premier plan
            if (curActivatedHandle == IntPtr.Zero)
                return false; // Si aucune application n'est en premier plan

            var motivatorId = Process.GetCurrentProcess().Id;
            GetWindowThreadProcessId(curActivatedHandle, out int curProcessId); // On obtient l'id de l'application actuellement en premier plan
            return curProcessId == motivatorId;
        }

        private static Process bProcess = null;

        /// <summary>
        /// Bring the main window of the application in the foreground if it is not already in the foreground
        /// </summary>
        /// <param name="ignoreAlreadyForeground">Force bringing window to the foreground</param>
        public static void BringMainWindowToFront(bool force = false)
        {
            // We detect if we can use the foreground system
            if (SwitchWindowEnabled || force)
            {
                if (!IsInForeground() || force)
                {
                    if (bProcess == null)
                    {
                        // get the process
                        bProcess = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).FirstOrDefault();
                    }

                    // check if the window is hidden / minimized
                    if (bProcess.MainWindowHandle == IntPtr.Zero)
                    {
                        // the window is hidden so try to restore it before setting focus.
                        ShowWindow(bProcess.Handle, ShowWindowEnum.Maximize);
                    }

                    // Required to make the function SetForegroundWindow work
                    SimulateAltKey();
                    // set user the focus to the window
                    SetForegroundWindow(bProcess.MainWindowHandle);
                }
            }
        }
        public static Dictionary<string, FileStream> fsLocks = new Dictionary<string, FileStream>();
        /// <summary>
        /// Prevent the deletion of folder or file for the given path to be deleted by indicating the message 'this file is being used by another process'. Works only while the calling application is alive. (the app that uses this function)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lockWithFileStream"></param>
        internal static void SetFileStreamAntiDelete(string path, bool lockWithFileStream)
        {
            Console.WriteLine($"Starting filestream lock for {path}");
            string lockFilePath = null;
            string lockFileName = "\\lock.Bolter";
            // path is Folder
            if (Directory.Exists(path))
            {
                if (lockWithFileStream)
                {
                    File.Create(path + lockFileName).Close();
                    lockFilePath = path + lockFileName;
                }
                else
                {
                    lockFilePath = path + lockFileName;
                }
            }
            // path is file
            else
            {
                lockFilePath = path;
            }

            // Locking the folder / file
            if (lockWithFileStream)
            {
                if (fsLocks == null)
                {
                    fsLocks = new Dictionary<string, FileStream>();
                }
                if (!fsLocks.ContainsKey(lockFilePath))
                {
                    FileStream fs = new FileStream(lockFilePath, FileMode.Open);
                    fs.Lock(0, 0);
                    fsLocks.Add(lockFilePath, fs);
                }
                else
                {
                    Console.WriteLine($"File at {lockFilePath} is already locked with a filestream");
                }
            }
            // Unlocking the folder / file
            else
            {
                if (fsLocks != null)
                {
                    if (fsLocks.ContainsKey(lockFilePath))
                    {
                        Console.WriteLine($"Unlocking Filestream thread for {lockFilePath}");
                        fsLocks.Remove(lockFilePath);
                    }
                    else
                    {
                        Console.WriteLine("Cannot unlock filestream for path : " + lockFilePath + " because not part of the registered paths");
                    }
                }
            }
        }



        /// <summary>
        /// The selected folder will be hidden at a system level, it will also be read only
        /// </summary>
        /// <param name="path"></param>
        /// <param name="enabled"></param>
        public static void HideAndProtectFolder(string path, bool enabled)
        {
            if (enabled)
            {
                File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);
            }
            else
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }


        /// <summary>
        /// Return true if the Task manager is enabled in this session. The Task manager can be disabled even in the administrator session.
        /// </summary>
        /// <returns></returns>
        public static bool IsTaskManagerEnabled()
        {
            string subKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";

            using (RegistryKey myKey = Registry.CurrentUser.OpenSubKey(subKey, false))
            {
                if (myKey != null)
                {
                    int disableTaskMgrValue = (int)myKey.GetValue("DisableTaskMgr", 0);
                    return disableTaskMgrValue == 0;
                }
            }

            // Par défaut, retourne vrai si la clé n'existe pas.
            return true;
        }

        /// <summary>
        /// Disable the key Alt & Tab for the current window
        /// </summary>
        public static void SetAltTabEnabled(bool enabled)
        {
            if (!enabled)
            {
                ProcessModule objCurrentModule = Process.GetCurrentProcess().MainModule; //Get Current Module
                objKeyboardProcess = new LowLevelKeyboardProc(captureKey); //Assign callback function each time keyboard proces
                ptrHook = SetWindowsHookEx(13, objKeyboardProcess, GetModuleHandle(objCurrentModule.ModuleName), 0); //Setting Hook of Keyboard Process for current module
            }
            else
            {
                // Freeing the alt tab hook
                if (ptrHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(ptrHook);
                    ptrHook = IntPtr.Zero;
                }
            }
        }


        /// <summary>
        /// Get the idle / inactivity time in milliseconds from the system (for example, the last time from which the system did detect a mouse movement or a key press), useful to see if the user is not using the computer / doing nothing
        /// </summary>
        /// <returns></returns>
        public static uint GetIdleTime()
        {
            LASTINPUTINFO LastUserAction = new LASTINPUTINFO();
            LastUserAction.cbSize = (uint)Marshal.SizeOf(LastUserAction);
            GetLastInputInfo(ref LastUserAction);
            return (uint)Environment.TickCount - LastUserAction.dwTime;
        }

        /// <summary>
        /// Lock the windows computer screen
        /// </summary>
        public static void LockTheScreen()
        {
            LockWorkStation();
        }

        /// <summary>
        /// Simulate the alt key, necessary to put a window to the foregound
        /// </summary>
        private static void SimulateAltKey()
        {
            // il faut simuler la touche Alt pour que la fonction SetForegroundWindow fonctionne
            keybd_event(0, 0, 0, 0);
        }

        /// <summary>
        /// Enable / Disable app start on windows boot 
        /// </summary>
        /// <returns>True if the command succeeded </returns>
        /// <param name="isStartup"></param>
        public static bool SetStartup(bool isStartup)
        {
            try
            {
                RegistryKey rk = null;
                rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (isStartup == true)
                {
                    rk.SetValue("Motivator", Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    rk.DeleteValue("Motivator", false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR à SetStartup(false)\n\n" + ex);
                return false;
            }
        }

        /// <summary>
        /// Close completely the file explorer
        /// </summary>
        public static void CloseFileExplorer()
        {
            var shellWindows = new SHDocVw.ShellWindows();
            foreach (SHDocVw.InternetExplorer ie in shellWindows)
            {
                ie.Quit();
            }
        }

        /// <summary>
        /// Close a specific window of the file explorer
        /// </summary>
        /// <param name="specificWindowUrl"></param>
        public static void CloseFileExplorer(string specificWindowUrl)
        {
            var shellWindows = new SHDocVw.ShellWindows();
            foreach (SHDocVw.InternetExplorer ie in shellWindows)
            {
                if (ie.LocationURL.Equals(specificWindowUrl))
                {
                    ie.Quit();
                }
            }
        }

        /// <summary>
        /// Get all the paths from current file explorer program (explorer.exe)
        /// </summary>
        /// <returns></returns>
        public static string[] GetFileExplorerPaths()
        {
            var shellWindows = new SHDocVw.ShellWindows();
            var tab = new string[shellWindows.Count];
            int index = 0;
            foreach (SHDocVw.InternetExplorer ie in shellWindows)
            {
                tab[index] = ie.LocationURL;
                index++;
            }
            return tab;
        }

        /// <summary>
        /// Resume process if paused (for example in the resources monitor)
        /// </summary>
        /// <param name="processName"></param>
        public static void ResumeProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return;

            var process = Process.GetProcessesByName(processName);

            foreach (Process p in process)
            {
                foreach (ProcessThread pT in p.Threads)
                {
                    if (pT.ThreadState == System.Diagnostics.ThreadState.Wait)
                    {
                        IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                        if (pOpenThread == IntPtr.Zero)
                        {
                            continue;
                        }

                        var suspendCount = 0;
                        do
                        {
                            suspendCount = ResumeThread(pOpenThread);
                        } while (suspendCount > 0);

                        CloseHandle(pOpenThread);
                    }
                }
            }
        }

        /// <summary>
        /// Enable or disable any app respawn by this app
        /// </summary>
        public static bool respawnEnabled = true;

        /// <summary>
        /// Create an app and watch for its lifetime, if it is killed or suspended/ it will be restarted by this app
        /// </summary>
        /// <param name="programPath">The program path to indicate the program to make respawnable</param>
        /// <param name="startDate">the date from which the program can be respawned (security)</param>
        /// <param name="endDate">the date from which the program won't be respawned anymore (security)</param>
        public static void RunRespawnableProgram(string programPath, DateTime startDate, DateTime endDate, int respawnDelayMilliseconds = 1000, bool resumeIfSuspended = true)
        {
            Console.WriteLine("[WATCHER] Enabling respawnable process with an interval of : " + respawnDelayMilliseconds / 1000 + "s");
            Timer t = new Timer();
            t.Interval = respawnDelayMilliseconds;
            t.Elapsed += (sender, e) =>
            {
                if (respawnEnabled)
                {
                    var date = DateTime.Now;
                    if (date > startDate && date < endDate)
                    {
                        RespawnAppIfNeeded(programPath);

                        if (resumeIfSuspended)
                            ResumeProcess(programPath);
                    }
                    else
                    {
                        (sender as Timer).Stop();
                    }
                }
            };
            t.Start();
        }

        /// <summary>
        /// Start app if it is detected in the process list
        /// </summary>
        /// <param name="processPath"></param>
        private static void RespawnAppIfNeeded(string processPath, string arguments = "")
        {
            var runningProcessName = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processPath));
            if (runningProcessName.Length == 0)
            {
                Console.WriteLine("Starting respawnable process : " + processPath);
                new Process
                {
                    StartInfo =
                    {
                        FileName = processPath,
                        Arguments = arguments
                    }
                }.Start();
            }
        }
        // TODO check if it works
        public static void RenameProcess(string processName, string newName)
        {
            Process p = Process.GetProcessesByName(processName).FirstOrDefault();
            if (p != null)
            {
                SetWindowText(p.MainWindowHandle, newName);
            }
            else
            {
                Console.WriteLine($" Couldn't rename process {processName} to {newName}");
            }
        }
        public static void RenameProcess(Process p, string newName)
        {
            SetWindowText(p.MainWindowHandle, newName);
        }

        // TODO : check if it works
        public static void CreateLocalUser(string username, string password, string description, string userGroupName = "Users")
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName.ToString());
            DirectoryEntry newUser = localDirectory.Children.Add(username, "user");
            newUser.Invoke("SetPassword", new object[] { password });
            newUser.Invoke("Put", new object[] { description });
            newUser.CommitChanges();

            // Add the user to a group
            var grp = localDirectory.Children.Find(userGroupName, "group");
            if (grp != null)
            {
                grp.Invoke("Add", new object[] { newUser.Path.ToString() });
                Console.WriteLine("User created : " + username);

            }
            else
            {
                Console.WriteLine($"Group {userGroupName} not found. Could not create user");
            }
            localDirectory.Dispose();
        }



        const int WTS_CURRENT_SESSION = -1;
        static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

        public static void LogoffThisUser()
        {
            if (!WTSDisconnectSession(WTS_CURRENT_SERVER_HANDLE,
                 WTS_CURRENT_SESSION, false))
                throw new Win32Exception();
        }

        public static void DeleteLocalUser(string username)
        {
            DirectoryEntry localDirectory = new DirectoryEntry("WinNT://" + Environment.MachineName.ToString());
            DirectoryEntries users = localDirectory.Children;
            DirectoryEntry user = users.Find(username);
            users.Remove(user);
            localDirectory.Dispose();
            Console.WriteLine("User removed : " + username);
        }

        public static void SwitchToSpecificAccount(string accountName)
        {
            // Probably not possible
            throw new NotImplementedException();
        }

        // Here i made the choice for this library to only create one virtual desktop. Because we won't need more for BolterBox


        private static Guid baseDesktopHandle = Guid.Empty;
        public static Guid createdDesktopHandle = Guid.Empty; // Bolter virtual desktop


        #region VirtualDesktop Library / unstable

        /// <summary>
        /// Works only for WPF or Winforms, not .NET core console
        /// </summary>
        public static void CreateNewVirtualDesktop()
        {
            if (createdDesktopHandle == Guid.Empty)
            {
                createdDesktopHandle = VirtualDesktop.Create().Id;
            }
        }

        public static void ClearAllVirtualDesktops()
        {
            if (VirtualDesktop.IsSupported)
            {
                foreach (var desktop in VirtualDesktop.GetDesktops())
                {
                    desktop.Remove();
                }
            }
        }

        /// <summary>
        /// Works only for WPF or Winforms, not .NET core console
        /// </summary>
        public static void CloseCreatedVirtualDesktop()
        {
            var desktop = VirtualDesktop.FromId(createdDesktopHandle);
            if (desktop != null)
            {
                desktop.Remove();
                createdDesktopHandle = Guid.Empty;
            }
        }

        /// <summary>
        /// Works only for WPF or Winforms, not .NET core console
        /// </summary>
        public static void SwitchToVirtualDesktop(bool bolterDesktop)
        {
            var id = bolterDesktop ? createdDesktopHandle : baseDesktopHandle;
            var desktop = VirtualDesktop.FromId(id);
            if (desktop != null)
            {
                desktop.Switch();
            }
            else
            {
                Console.WriteLine("Could not switch to virtual desktop because the bolter virtual desktop is not created");
            }
        }

        /// <summary>
        /// Works only for WPF or Winforms, not .NET core console
        /// </summary>
        public static void MoveWindowToDesktop(IntPtr windowHandle, bool bolterDesktop)
        {
            var id = bolterDesktop ? createdDesktopHandle : baseDesktopHandle;
            VirtualDesktopHelper.MoveToDesktop(windowHandle, VirtualDesktop.FromId(id));
        }

        #endregion

        /// <summary>
        /// Hide or Unhide the windows Task bar. It can be hidden by other ways such as maximizing an existing window, but this method can be used as an additionnal security
        /// </summary>
        /// <param name="isVisible"></param>
        public static void SetTaskbarVisible(bool isVisible)
        {
            if (isVisible)
            {
                ShowWindow(Handle, 1);
                ShowWindow(HandleOfStartButton, 1);
            }
            else
            {
                ShowWindow(Handle, 0);
                ShowWindow(HandleOfStartButton, 0);
            }
        }
        private static int respawnerProcessId = -1;

        /// <summary>
        /// Enable the program to be respawned by another process, it means that if it closed, it will automatically restart
        /// </summary>
        /// <param name="canRespawn"></param>
        /// <param name="verificatorProcesses"></param>
        public static void MakeThisProgramRespawnable(bool canRespawn, string[] verificatorProcesses)
        {
            if (canRespawn)
            {
                Process p = new Process();
                respawnerProcessId = p.Id;

                // Adding the exe path for the program to be respawned
                p.StartInfo.ArgumentList.Add(Process.GetCurrentProcess().MainModule.FileName);
                // Adding the processes linked to the exe path that tell the program not to be respawned

                if (verificatorProcesses == null)
                {
                    verificatorProcesses = Array.Empty<string>();
                }

                foreach (var path in verificatorProcesses)
                {
                    p.StartInfo.ArgumentList.Add(path);
                }

                p.StartInfo.FileName = Environment.CurrentDirectory + @"Bolter\Resources\BolterRespawner.exe";
                p.Start();
            }
            else if (respawnerProcessId != -1)
            {
                Process.GetProcessById(respawnerProcessId).Kill();
                respawnerProcessId = -1;
            }
            else
            {
                Console.WriteLine("Respawner must be opened before closing it first --> No need to close the respawner");
            }
            // throw new NotImplementedException("TODO : Create project BolterWatcher");
        }


        /// <summary>
        /// Returns true if the program is started in administrator mode
        /// </summary>
        /// <returns></returns>
        public static bool IsInAdministratorMode()
        {
            bool isElevated = false;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return isElevated;
        }

        /// <summary>
        /// Disable all possible restrictions for the class <see cref="NonAdmin"/>
        /// </summary>
        public static void DisableAllNonAdminRestrictions()
        {
            SetTaskbarVisible(true);
            SetStartup(false);
            ProgramToClose.DisposeProcessWatcher();
            ClearAllVirtualDesktops();
            AutoLockFolder.UnlockAll();
            MakeThisProgramRespawnable(false, null);
            SetAltTabEnabled(true);
            Console.WriteLine("[BOLTER] Finished disabling Non Admin restrictions");
        }
    }
}
