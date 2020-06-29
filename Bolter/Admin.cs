using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Bolter
{

    /// <summary>
    /// This class provides useful kiosk / security commands, it requires the windows program to be in UAC/administrator mode
    /// To enable this, you can set the app to start in administrator for a C# program with the manifest file
    /// </summary>
    public static class Admin
    {
        /// <summary>
        /// Install the ntRights utility in System32, ntRights can be used to revoke certain rights from administrators such as the ability to change the system date.
        /// The only drawback for using this, is that it seems to be necessary to reboot the computer session to apply the changes.
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        public static void InstallNtRights()
        {
            try
            {
                if (!File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) + @"\ntrights.exe"))
                {
                    File.WriteAllBytes(Environment.SystemDirectory + @"\ntrights.exe", Properties.Resources.ntrights);
                }
                else
                {
                    Console.WriteLine("NtRights is already installed");
                }
                Console.WriteLine("NtRights is now installed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Install fail for NtRights");
                Console.ReadLine();
                throw ex;
            }
        }

        /// <summary>
        /// Block / Unblock both the batch (.bat) files and the CMD console. It doesn't require a restart from the computer and works immediatly
        /// Permet de bloquer à la fois les scripts Batch et la console CMD, nécéssite un appel administrateur
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="block"></param>
        public static void SetBatchAndCMDBlock(bool block)
        {
            string subKey = @"Software\Policies\Microsoft\Windows\System";
            var key = Registry.CurrentUser.CreateSubKey(subKey, true);
            if (key != null)
            {
                if (block)
                    key.SetValue("DisableCMD", 1); //  A 1 bloque CMD et les fichiers BAT , à 2 bloque CMD seulement
                else
                    key.SetValue("DisableCMD", 0); // Débloque CMD et les fichiers BAT
            }
        }

        /// <summary>
        /// Nécéssite le fichier ntrights.exe installé dans System32, cette fonction décide de si il est possible ou non pour utilisateur de modifier la date ou l'heure, nécéssite UAC
        /// Car c'est très puissant, ça peut en effet même bloquer les administrateurs . On est obligés de redémarrer la session pour voir les changements.
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="username"></param>
        public static void PreventDateEditingW10(bool removePrivilege)
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                process.StartInfo.UseShellExecute = false;
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Verb = "runas";
                startInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                SecurityIdentifier id = new SecurityIdentifier("S-1-5-32-544");

                string administratorGroupName = id.Translate(typeof(NTAccount)).Value;
                string strOutput;
                if (removePrivilege)
                {
                    startInfo.Arguments = string.Format("/C ntrights -U {0} -R SeSystemtimePrivilege", Environment.UserName);
                    process.StartInfo = startInfo;
                    process.StartInfo = startInfo;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    System.Diagnostics.Process p2 = process;
                    process.Start();
                    strOutput = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    Console.WriteLine(strOutput);
                    p2.StartInfo.Arguments = string.Format("/C ntrights -U {0} -R SeSystemtimePrivilege", administratorGroupName);
                    p2.Start();
                    strOutput = p2.StandardOutput.ReadToEnd();
                    p2.WaitForExit();
                    Console.WriteLine(strOutput);
                }
                else
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = string.Format("/C ntrights -U {0} +R SeSystemtimePrivilege", Environment.UserName);
                    process.StartInfo = startInfo;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    System.Diagnostics.Process p2 = process;
                    process.Start();
                    process.WaitForExit();
                    strOutput = process.StandardOutput.ReadToEnd();
                    Console.WriteLine(strOutput);
                    p2.StartInfo.Arguments = string.Format("/C ntrights -U {0} +R SeSystemtimePrivilege", administratorGroupName);
                    p2.Start();
                    strOutput = p2.StandardOutput.ReadToEnd();
                    p2.WaitForExit();
                    Console.WriteLine(strOutput);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadLine();
            }

        }

        /// <summary>
        /// 'Unsafe' unlock method
        /// </summary>
        /// <param name="folderPath"></param>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        private static void _UnlockFolder(string folderPath)
        {
            try
            {
                Console.WriteLine("Unlocking folder : " + folderPath);
                Console.WriteLine("This can take some time...");
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
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
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
        /// Enable / Disable the app to start in safe mode
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="autoStartEnabled"></param>
        /// <param name="applicationFullPath"></param>
        public static void SetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "useThisApp")
        {
            // LocalMachine is needed, so we also need UAC auth
            var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            var key = Registry.LocalMachine.OpenSubKey(keyPath,true);
            if(key != null)
            {
                string safeModePrograms = (string)key.GetValue("Shell");
                if (autoStartEnabled)
                {
                    if(!safeModePrograms.Contains(applicationFullPath))
                    // Append our app to the list of the apps
                    key.SetValue("Shell",safeModePrograms + ";" +applicationFullPath);
                }
                else
                    // Remove the app path without touching at the other datas
                    key.SetValue("Shell",safeModePrograms.Replace(";"+applicationFullPath,string.Empty));
            }
            else
            {
                Console.WriteLine("Auto safe mode set failed : reg key not found");
            }
        }

        /// <summary>
        /// Unlock all the folders paths indicated in parameter
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="foldersPathToUnlock"></param>
        public static void UnlockFolders(string[] foldersPathToUnlock)
        {
            foreach(string folderPath in foldersPathToUnlock)
            {
                Console.WriteLine("Unlocking folder : " + folderPath);
                UnlockFolder(folderPath);
            }
        }

        /// <summary>
        /// Hide in windows 10 the startup apps page (can be used to avoid disabling the software at startup)
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// </summary>
        public static void HideStartupsAppsFromSettings(bool hide)
        {
            if (hide)
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", true).SetValue("SettingsPageVisibility", "hide:startupapps", RegistryValueKind.String);
            else
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\", true).DeleteValue("SettingsPageVisibility");
        }

        /// <summary>
        ///Disable all the main securities at once : Folder Locking, SettingsPageVisibility, Batch & CMD, Date Editing, Safe Startup
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        public static void DisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock)
        {
            Console.WriteLine("Disabling all restrictions");
            Console.WriteLine("[UNBLOCKER Admins] We chose to unlock the computer with administrator commands");

            UnlockFolders(foldersPathToUnlock);

            try
            {
                Console.Write("\n0) Changing visibility of SettingsPageVisibility to 'Visible' in windows settings");
                RegistryKey explorerKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\", true);
                if (explorerKey.GetValueNames().Contains("Start"))
                {
                    explorerKey.DeleteValue("SettingsPageVisibility");
                    Console.Write("     Success !");
                }
                else
                {
                    Console.Write("     Couldn't delete SettingsPageVisibility value");
                }

            }
            catch (Exception) { }

            //nt enable
            Console.Write("\n1) Installing NtRights");
            InstallNtRights();
            Console.Write("     Success !");

            Console.WriteLine("\n2) Re activating CMD & batch scripts");
            SetBatchAndCMDBlock(false);
            Console.Write("     Success !");

            Console.WriteLine("\n3) Re activating date editing (using ntrights.exe)");
            PreventDateEditingW10(false);
            Console.Write("     Success !");

            Console.WriteLine("\n4) Disabling the software autostart on safe mode");
            SetStartupSafeMode(false, appPath);

            Console.WriteLine("\n[UNBLOCKER Admins] Success !");
        }
}

}
