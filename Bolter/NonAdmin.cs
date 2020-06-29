﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Timers;
using System.Windows.Documents;

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
            foreach (var process in Process.GetProcessesByName(programName))
            {
                process.Kill();
            }
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

        // Timer that closes the programs from the programsToClose List
        internal static Timer closeProgramsTimer;
        internal static HashSet<ProgramToClose> programsToClose;
        internal static HashSet<AutoLockFolder> foldersToLock;
        /// <summary>
        /// Closes a program automatically between two periods of this day. The auto closer will be updated immediatly.
        /// </summary>
        /// <param name="programName"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="autoStartAutoCloser">If this is set to false, you will also have to call SetProgramAutoCloser to true to start the program closer</param>
        public static void AddAutoCloseProgram(string programName, TimeSpan startTime, TimeSpan endTime, bool autoStartAutoCloser = true)
        {
            if (startTime > endTime)
                throw new Exception("We cannot have a start time superior to an end time");

            if (programsToClose == null)
                programsToClose = new HashSet<ProgramToClose>();

            programsToClose.Add(new ProgramToClose(programName, startTime, endTime));

            // Start the auto closer
            if (autoStartAutoCloser && (closeProgramsTimer == null || !closeProgramsTimer.Enabled))
            {
                SetProgramAutoCloser(true);
            }
        }

        /// <summary>
        /// Remove a folder eligible for automatic locking. The auto locker will be updated immediatly. Used automatically by the UnlockFolder method. The folder won't be unlocked but, the library won't try to lock it anymore.
        /// This is marked as internal because usage outside the library is not recommended, for clarity it is better to use the UnlockFolder directly.
        /// </summary>
        /// <param name="folderPath"></param>
        internal static void RemoveAutoLockFolder(string folderPath)
        {
            AutoLockFolder folderToRemove = null;
            foreach (var folder in foldersToLock)
            {
                if (folder.path.Equals(folderPath))
                {
                    folderToRemove = folder;
                }
            }
            foldersToLock.Remove(folderToRemove);
        }

        /// <summary>
        /// Unlock all the folders paths indicated in parameter
        /// </summary>
        /// <param name="foldersPathToUnlock"></param>
        public static void UnlockFolders(string[] foldersPathToUnlock)
        {
            foreach (string folderPath in foldersPathToUnlock)
            {
                Console.WriteLine("Unlocking folder : " + folderPath);
                UnlockFolder(folderPath);
            }
        }

        /// <summary>
        /// Remove a program eligible for automatic closing. The auto closer will be updated immediatly.
        /// </summary>
        /// <param name="programName"></param>
        public static void RemoveAutoCloseProgram(string programName)
        {
            ProgramToClose prgmToClose = null;
            foreach (var prgm in programsToClose)
            {
                if(prgm.programName.Equals(programName))
                {
                    prgmToClose = prgm;
                }
            }
            programsToClose.Remove(prgmToClose);

        }

        /// <summary>
        /// Start, Stop or Edit the folder auto locker. Auto lock is powerful, use with caution.
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="closeDelay"></param>
        public static void SetProgramAutoCloser(bool enabled, int closeDelay = 200)
        {
            // Creating & init
            if (closeProgramsTimer == null)
            {
                closeProgramsTimer = new Timer();
                closeProgramsTimer.Elapsed += (s, e) =>
                {
                    long now = DateTime.Now.TimeOfDay.Ticks;
                    foreach (var program in programsToClose)
                    {
                        if (program.startTime.Ticks < now && now < program.endTime.Ticks)
                        {
                            CloseProgram(program.programName);
                        }
                    }

                    // Stop & free the global timer if no programs are registered
                    if (programsToClose == null || programsToClose.Count == 0)
                    {
                        closeProgramsTimer.Stop();
                        closeProgramsTimer.Dispose();
                        Console.WriteLine("/// Auto Closer Disabled ///");
                    }
                };
            }
            // Updating
            closeProgramsTimer.Interval = closeDelay;
            if (enabled)
            {
                closeProgramsTimer.Start();
                closeProgramsTimer.AutoReset = true;
                Console.WriteLine("/// Auto Closer Enabled ///");
            }
            else
            {
                closeProgramsTimer.Stop();
                Console.WriteLine("/// Auto Closer Disabled ///");
            }
        }

        /// <summary>
        /// Start, Stop or Edit the program auto closer
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="lockDelayMilliseconds">In milliseconds</param>
        public static void SetFolderAutoLocker(bool enabled, int lockDelayMilliseconds = 5000)
        {
            // Creating & init
            if (folderLockTimer == null)
            {
                folderLockTimer = new Timer();
                folderLockTimer.Elapsed += (s, e) =>
                {
                    long now = DateTime.Now.Ticks;
                    foreach (var folder in foldersToLock.ToList())
                    {
                        if (folder.startDate.Ticks < now && now < folder.endDate.Ticks)
                        {
                            LockFolder(folder.path,false);
                            HideAndProtectFolder(folder.path,true);
                        }
                        else
                        {
                            HideAndProtectFolder(folder.path, false);
                            UnlockFolder(folder.path);
                        }
                    }

                    // Stop & free the global timer if no folders are registered
                    if (foldersToLock == null || foldersToLock.Count == 0)
                    {
                        folderLockTimer.Stop();
                        folderLockTimer.Dispose();
                    }
                };
            }
            // Updating the timer
            folderLockTimer.Interval = lockDelayMilliseconds;
            if (enabled)
            {
                if(foldersToLock.Count == 0 )
                {
                    Console.WriteLine("[Warning] Started the Auto Folder Locker, but no folders are registered");
                }
                folderLockTimer.AutoReset = true;
                folderLockTimer.Start();
                Console.WriteLine("/// Auto Locker Enabled ///");
            }
            else
            {
                folderLockTimer.Stop();
                Console.WriteLine("/// Auto Locker Disabled ///");
            }
        }


        /// <summary>
        /// 'Unsafe' unlock method
        /// </summary>
        /// <param name="folderPath"></param>
        private static void _UnlockFolder(string folderPath)
        {
            try
            {
                Console.WriteLine("Unlocking folder : " + folderPath);
                Console.WriteLine("This can take some time...");
                // First remove it from the autolock list if necessary
                NonAdmin.RemoveAutoLockFolder(folderPath);
                DirectoryInfo dInfo = new DirectoryInfo(folderPath);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                string adminUserName = Environment.UserName;// getting your adminUserName
                FileSystemAccessRule fsa2 = new FileSystemAccessRule(adminUserName, FileSystemRights.ListDirectory | FileSystemRights.Delete, AccessControlType.Deny);
                dSecurity.RemoveAccessRule(fsa2);
                dInfo.SetAccessControl(dSecurity);
                Console.WriteLine("Unlocked");
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex);
                Console.ReadLine();
            }
        }

        /// <summary>
        /// 'Safe' unlock method. Unlocks a folder that is locked by the windows security system.
        /// </summary>
        /// <param name="folderPath"></param>
        public static void UnlockFolder(string folderPath)
        {
            try
            {
                string path = folderPath;
                if (Directory.Exists(path))
                {
                    string adminUserName = Environment.UserName;    // getting your adminUserName
                    if (Directory.Exists(path))
                    {
                        _UnlockFolder(path);
                        File.SetAttributes(path, FileAttributes.Normal);
                    }
                    else
                    {
                        Console.WriteLine("Error : {0} is not a directory, so we cannot lock it", path);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Unlock all previoulsy locked folders added with <see cref="NonAdmin.AddAutoLockFolder(string, DateTime, DateTime, bool, int)"/> 
        /// </summary>
        public static void UnlockAllFolders()
        {
            foreach (var folder in NonAdmin.foldersToLock)
            {
                UnlockFolder(folder.path);
            }
            Console.WriteLine("Unlocked " + NonAdmin.foldersToLock + " folders");
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
        /// <summary>
        /// Lock the folder with windows security. It cannot be entered by any user after that, nor deleted.
        /// </summary>
        /// <param name="folderPath">The path of the folder to lock</param>
        /// <param name="silent">log info to the console if set to true</param>
        public static void LockFolder(string folderPath, bool silent)
        {
            try
            {
                DirectoryInfo dInfo = new DirectoryInfo(folderPath);
                var dSecurity = dInfo.GetAccessControl();
                if(!silent)
                    Console.WriteLine("LOCKING FOLDER : " + folderPath);
                FileSystemAccessRule fsa = new FileSystemAccessRule(Environment.UserName, FileSystemRights.ListDirectory | FileSystemRights.Delete, AccessControlType.Deny);
                dSecurity.AddAccessRule(fsa);
                dInfo.SetAccessControl(dSecurity);
                if(!silent)
                    Console.WriteLine("Folder Locked successfully : " + folderPath);
            }
            catch (Exception e){
                Console.WriteLine("Lock failed : " + e.Message);
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

        static Timer folderLockTimer;

        /// <summary>
        /// Automatically re-lock a folder after a certain period of time. Very powerful. Use with caution
        /// </summary>
        /// <param name="path"></param>
        /// <param name="beginDate"></param>
        /// <param name="endDate"></param>
        /// <param name="lockEnabled"></param>
        public static void AddAutoLockFolder(string path, DateTime beginDate, DateTime endDate, bool autoStartAutoLocker, int autoLockDelay = 5000)
        {

            if (beginDate > endDate)
                throw new Exception("We cannot have a start time superior to an end time");

            if (foldersToLock == null)
                foldersToLock = new HashSet<AutoLockFolder>();

            foldersToLock.Add(new AutoLockFolder(path, beginDate, endDate));

            // Start the auto locker
            if (autoStartAutoLocker && (folderLockTimer == null || !folderLockTimer.Enabled))
            {
                SetFolderAutoLocker(true, autoLockDelay);
            }
        }

        /// <summary>
        /// Returns true if the task manager is enabled in this session. The task manager can be disabled even in the administrator session.
        /// </summary>
        /// <returns></returns>
        public static bool GetTaskManagerActivation()
        {
            RegistryKey regkey;
            string subKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            string result = "";
            RegistryKey myKey = Registry.CurrentUser.OpenSubKey(subKey, false);
            if (myKey.GetValue("DisableTaskMgr") != null)
            {
                Console.WriteLine("Activating Task Manager");
                regkey = Registry.CurrentUser.CreateSubKey(subKey);
                result = regkey.GetValue("DisableTaskMgr").ToString();
                regkey.Close();
            }
            if (result == "1") return true;
            return false;
        }

        /// <summary>
        /// Enable or disable the task manager, powerful because cannot be bypassed direclty by a system administrator. For security reasons, it will unlock after a certain amount of time.
        /// </summary>
        /// <param name="isActivated"></param>
        /// <param name="customSecurityDuration">Maximum is 48 hours & minimum is 1 hour </param>
        public static void SetTaskManagerActivation(bool isActivated, int customSecurityDuration = 48)
        {
            RegistryKey regkey;
            string subKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            if (customSecurityDuration > 72)
                customSecurityDuration = 72;
            else if (customSecurityDuration <= 0)
                customSecurityDuration = 1;

            if (isActivated)
            {
                RegistryKey myKey = Registry.CurrentUser.OpenSubKey(subKey, false);
                if (myKey.GetValue("DisableTaskMgr") != null)
                {
                    Console.WriteLine("Activating Task Manager");
                    regkey = Registry.CurrentUser.CreateSubKey(subKey, true);
                    regkey.DeleteValue("DisableTaskMgr", false);
                    regkey.Close();
                }
            }
            else if (!isActivated)
            {
                Console.WriteLine("Disabling Task Manager");
                regkey = Registry.CurrentUser.CreateSubKey(subKey, true);
                regkey.SetValue("DisableTaskMgr", 1);
                regkey.Close();
            }
            else
                Console.WriteLine("DisableTaskMgr not found error");


            if (!isActivated)
                EnableTaskManagerSecurity(customSecurityDuration); // Très important, il faut toujours avoir une issue de secours, surtout pour un système aussi crucial
        }

        /// <summary>
        /// Auto re-enable the task manager after the indicated number of hours
        /// </summary>
        /// <param name="hoursBeforeUnlock"></param>
        private static void EnableTaskManagerSecurity(int hoursBeforeUnlock)
        {
            Timer t = new Timer
            {
                Interval = hoursBeforeUnlock * 3600 * 1000,
                AutoReset = false
            };
            t.Elapsed += (s, e) => SetTaskManagerActivation(true);
            t.Start();
        }

        /// <summary>
        /// Disable the key Alt & Tab for the current window
        /// </summary>
        public static void DisableAltTab(bool disable)
        {
            if (disable)
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
        /// Gets the idle / inactivity time in milliseconds from the system (for example, the last time from which the system did detect a mouse movement or a key press), useful to see if the user is not using the computer / doing nothing
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
                    rk.SetValue("Motivator", System.Reflection.Assembly.GetEntryAssembly().Location);
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
            var process = Process.GetProcessesByName(processName);

            if (processName == string.Empty)
                return;
            foreach (Process p in process)
            {
                foreach (ProcessThread pT in p.Threads)
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
                if(respawnEnabled)
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
                new Process { 
                    StartInfo = 
                    {
                        FileName = processPath,
                        Arguments = arguments
                    }               
                }.Start();
            }
        }
    }
}
