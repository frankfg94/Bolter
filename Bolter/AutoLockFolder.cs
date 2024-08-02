using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Timers;
using static Bolter.NonAdmin;

namespace Bolter
{
    public class AutoLockFolder : AbstractSecurity
    {
        public static HashSet<AutoLockFolder> foldersToLock;
        private static Timer folderLockTimer;
        public readonly string path;

        public class AutoLockChangedArgs : EventArgs
        {
            public FolderListAction FolderAction { get; set; }
            public AutoLockFolder folderToLock;
        }




        /// <summary>
        /// Triggered when a folder is added or removed in the collection <see cref="foldersToLock"/> 
        /// </summary>
        public static event EventHandler<AutoLockChangedArgs> AutoLockFolderListChanged;
        public static void OnAutoLockFolderListChanged(AutoLockChangedArgs args)
        {
            AutoLockFolderListChanged?.Invoke(foldersToLock, args);
        }

        public AutoLockFolder(DateTime startDate, DateTime endDate, string folderPath) : base(startDate, endDate)
        {

            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException("Directory not found: " + folderPath);
            path = folderPath;
            this.startDate = startDate;
            this.endDate = endDate;
        }

        public override string ToString()
        {
            return
            $"Folder: {path} " + Environment.NewLine +
            $"Locked: " + IsSecurityActive + Environment.NewLine +
            $"Lock Start date: " + startDate.ToLongDateString() + " | " + startDate.ToLongTimeString() + Environment.NewLine +
            $"Unlock date: " + endDate.ToLongDateString() + " | " + endDate.ToLongTimeString() + Environment.NewLine +
            $"Time remaining: " + TimeRemaining;
        }

        public override bool Equals(object obj)
        {
            AutoLockFolder folder = obj as AutoLockFolder;
            return obj != null && folder.path.Equals(path, StringComparison.Ordinal);
        }

        protected override void _PrepareEnableSecurity()
        {
            if (foldersToLock == null) { foldersToLock = new HashSet<AutoLockFolder> { this }; }
            else if (!foldersToLock.Contains(this))
            {
                foldersToLock.Add(this);
            }
            else
            {
                throw new InvalidOperationException("This folder is already added to the lock list");
            }
        }

        protected override void _EnableSecurity()
        {
            LockFolder();
        }

        protected override void _DisableSecurity()
        {
            UnlockFolder();
        }

        /// <summary>
        /// Start, Stop or Edit the program auto closer. Can be started automatically with <see cref="LockFolder(string, bool)"/>
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="lockDelayMilliseconds">In milliseconds</param>
        public static void SetFolderAutoLockerTimer(bool enabled, int lockDelayMilliseconds = 5000)
        {
            // Creating & init
            if (folderLockTimer == null)
            {
                folderLockTimer = new Timer();
                folderLockTimer.Elapsed += (s, e) =>
                {
                    long now = DateTime.Now.Ticks;
                    foreach (AutoLockFolder folder in foldersToLock.ToList())
                    {
                        if (folder.startDate.Ticks < now && now < folder.endDate.Ticks)
                        {
                            folder.LockFolder();
                            HideAndProtectFolder(folder.path, true);
                        }
                        else
                        {
                            HideAndProtectFolder(folder.path, false);
                            folder.UnlockFolder();
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
                if (foldersToLock.Count == 0)
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
        /// 'Safe' unlock method. Unlocks a folder that is locked by the windows security system.
        /// </summary>
        /// <param name="folderPath"></param>
        public void UnlockFolder()
        {

            if (Directory.Exists(this.path))
            {
                Console.WriteLine("Unlocking folder : " + path);
                Console.WriteLine("This can take some time...");
                // First remove it from the autolock list if necessary
                RemoveAutoLockFolder(path);
                if (fsLocks != null && fsLocks.ContainsKey(path))
                {
                    fsLocks.Remove(path);
                }
                DirectoryInfo dInfo = new DirectoryInfo(path);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                string adminUserName = Environment.UserName;// getting your adminUserName
                FileSystemAccessRule fsa2 = new FileSystemAccessRule(adminUserName, FileSystemRights.ListDirectory | FileSystemRights.Delete, AccessControlType.Deny);
                dSecurity.RemoveAccessRule(fsa2);
                dInfo.SetAccessControl(dSecurity);
                File.SetAttributes(path, FileAttributes.Normal);
            }
            else
            {
                Console.WriteLine($"Error : {path} is not a directory, so we cannot lock it");
            }
        }

        /// <summary>
        /// Unlock all previoulsy locked folders added with <see cref="NonAdmin.AddAutoLockFolder(string, DateTime, DateTime, bool, int)"/> 
        /// <param name="disableAutoLocker">Also completely disable the auto locker, for performances purposes</param>
        /// </summary>
        public static void UnlockAll(bool disableAutoLocker = true)
        {
            Console.WriteLine("Unlocking base directory : " + AppDomain.CurrentDomain.BaseDirectory);
            AutoLockFolder f = new AutoLockFolder(DateTime.Now, DateTime.Now.AddMinutes(1), AppDomain.CurrentDomain.BaseDirectory);
            f.DisableSecurity();

            if (foldersToLock != null)
            {
                foreach (AutoLockFolder folder in foldersToLock.ToList())
                {
                    folder.UnlockFolder();
                }
                Console.WriteLine("Unlocked " + foldersToLock.Count + " folders");
                if (disableAutoLocker)
                {
                    AutoLockFolder.SetFolderAutoLockerTimer(false, 5000);
                }
            }
            else
            {
                Console.WriteLine("Didn't found any folder registered to unlock");
            }
        }

        /// <summary>
        /// Unlock all the folders paths indicated in parameter
        /// </summary>
        /// <param name="foldersPathToUnlock"></param>
        public static void UnlockFolders(string[] foldersPathToUnlock)
        {
            List<AutoLockFolder> folders = foldersToLock.ToList().FindAll((f) => foldersPathToUnlock.Contains(f.path));
            UnlockFolders(folders);
        }

        public static void UnlockFolders(List<AutoLockFolder> folders)
        {
            if (folders == null) throw new NullReferenceException("No folders");
            folders.ForEach(f => f.UnlockFolder());
        }

        /// <summary>
        /// Remove a folder eligible for automatic locking. The auto locker will be updated immediatly. Used automatically by the UnlockFolder method. The folder won't be unlocked but, the library won't try to lock it anymore.
        /// This is marked as internal because usage outside the library is not recommended, for clarity it is better to use the UnlockFolder directly.
        /// </summary>
        /// <param name="folderPath"></param>
        internal static void RemoveAutoLockFolder(string folderPath)
        {
            if (foldersToLock != null)
            {
                AutoLockFolder folderToRemove = null;
                foreach (AutoLockFolder folder in foldersToLock)
                {
                    if (folder.path.Equals(folderPath))
                    {
                        folderToRemove = folder;
                    }
                }
                if (foldersToLock.Remove(folderToRemove))
                {
                    OnAutoLockFolderListChanged(new AutoLockChangedArgs
                    {
                        FolderAction = FolderListAction.Removed,
                        folderToLock = folderToRemove
                    });
                }
            }
        }

        /// <summary>
        /// Lock the folder with windows security. It cannot be entered by any user after that, nor deleted. Should use <see cref="AddAutoLockFolder(string, DateTime, DateTime, bool, int)"/> if possible, because the unlocking is manual and not handled by the auto lock system
        /// </summary>
        /// <param name="folderPath">The path of the folder to lock</param>
        /// <param name="silent">log info to the console if set to true</param>
        public void LockFolder(bool fileStreamLock = true)
        {
            DirectoryInfo dInfo = new DirectoryInfo(path);
            DirectorySecurity dSecurity = dInfo.GetAccessControl();
            FileSystemAccessRule fsa = new FileSystemAccessRule(Environment.UserName, FileSystemRights.ListDirectory | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Deny);
            dSecurity.AddAccessRule(fsa);
            dInfo.SetAccessControl(dSecurity);

            // One more security, in case the security rule is removed too quickly, the folder can be maintained alive by a file "in use" inside it. A filestream is used to maintain an open connection with a file
            if (fileStreamLock)
            {
                NonAdmin.SetFileStreamAntiDelete(path, fileStreamLock);
            }
        }


        /// <summary>
        /// Automatically re-lock a folder after a certain period of time. Very powerful. Use with caution
        /// </summary>
        /// <param name="path">The path of the folder to lock</param>
        /// <param name="beginDate">The date from which the folder will be locked</param>
        /// <param name="endDate">The date from which the folder will stop being locked</param>
        /// <param name="lockEnabled">Lock (true) or Unlock (false)</param>
        /// <param name="autoLockDelayMilliseconds">The speed of relocking, if the folder has few files, a small value is required </param>
        private static void AddAutoLockFolder(string path, DateTime beginDate, DateTime endDate, bool autoStartAutoLocker, int autoLockDelayMilliseconds = 5000)
        {

            AutoLockFolder folder = new AutoLockFolder( beginDate, endDate, path);

            if (foldersToLock.Add(folder))
            {
                OnAutoLockFolderListChanged(new AutoLockChangedArgs
                {
                    FolderAction = FolderListAction.Added,
                    folderToLock = folder
                });
            }

            // Start the auto locker
            if (autoStartAutoLocker && (folderLockTimer == null || !folderLockTimer.Enabled))
            {
                SetFolderAutoLockerTimer(true, autoLockDelayMilliseconds);
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsSecurityActive, startDate, endDate, TimeRemaining, Kind, path);
        }
    }
}
