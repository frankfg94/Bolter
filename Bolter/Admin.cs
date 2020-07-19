using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

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
        /// Install a service automatically from the folder AdminBolterService as LocalSystem (highest possible privileges)
        /// </summary>
        /// <param name="serviceExeName">Name of the executable without the exe</param>
        public static void InstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service", bool autoStart = true)
        {
            System.Diagnostics.Process cmd = new System.Diagnostics.Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            //cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.OutputDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
            cmd.ErrorDataReceived += (sender, e) => { Bolter.Other.Warn(e.Data); };
            cmd.BeginOutputReadLine();
            cmd.BeginErrorReadLine();

            string solutionPath = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName;
            var path = solutionPath + @$"\AdminBolterService\bin\Release\netcoreapp3.1\publish\{serviceExeName}.exe";
            var commandCreateService = $"sc create \"{serviceName}\" binPath=" + path ;
            if(!File.Exists(path))
            {
                throw new FileNotFoundException("The file doesn't exist at :" + path);
            }


            using (StreamWriter sw = cmd.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    Console.WriteLine($"sc stop \"{serviceName}\"");
                    sw.WriteLine($"sc stop \"{serviceName}\"");
                    Thread.Sleep(5000);
                    Console.WriteLine(">>> Service stopped");

                    Console.WriteLine($"sc delete \"{serviceName}\"");
                    sw.WriteLine($"sc delete \"{serviceName}\"");
                    Thread.Sleep(5000);
                    Console.WriteLine(">>> Service deleted");

                    Console.WriteLine(commandCreateService);
                    sw.WriteLine(commandCreateService);
                    Console.WriteLine(">>> Service created");

                    Console.WriteLine($"sc query \"{serviceName}\"");
                    sw.WriteLine($"sc query \"{serviceName}\"");
                    Console.WriteLine(">>> Query done");

                    if (autoStart)
                    {
                        Console.WriteLine("sc config \"Bolter Admin Service\" start=\"auto\"");
                        sw.WriteLine("sc config \"Bolter Admin Service\" start=\"auto\"");
                        Console.WriteLine(">>> Automatic startup set");
                        Thread.Sleep(200);

                    }

                    Console.WriteLine($"sc start \"{serviceName}\"");
                    sw.WriteLine($"sc start \"{serviceName}\"");
                    Console.WriteLine(">>> Service started");
                }
            }
            cmd.WaitForExit();

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
                if (applicationFullPath.Equals(applicationFullPath))
                {
                    applicationFullPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                }
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
                if (safeModePrograms.Last().Equals(';'))
                {
                    // Remove the last comma
                    key.SetValue("Shell", safeModePrograms.Substring(0,safeModePrograms.Length - 1));
                }
            }
            else
            {
                Console.WriteLine("Auto safe mode set failed : reg key not found");
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
        public static void DisableAllAdminRestrictions(string appPath)
        {
            DisableAllAdminRestrictions(appPath, null);
        }

        /// <summary>
        ///Disable all the main securities at once : Folder Locking, SettingsPageVisibility, Batch & CMD, Date Editing, Safe Startup
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        public static void DisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock)
        {
            Console.WriteLine("Disabling all restrictions");
            Console.WriteLine("[UNBLOCKER Admins] We chose to unlock the computer with administrator commands");

            if(foldersPathToUnlock == null)
            {
                NonAdmin.UnlockAllFolders();
            }
            else
            {
                NonAdmin.UnlockFolders(foldersPathToUnlock);
            }

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
            Console.Write("\n1) Installing NtRights...");
            InstallNtRights();
            Console.Write("     Success !");

            Console.Write("\n2) Re activating CMD & batch scripts...");
            SetBatchAndCMDBlock(false);
            Console.Write("     Success !");

            Console.Write("\n3) Re activating date editing (using ntrights.exe)...");
            PreventDateEditingW10(false);
            Console.Write("     Success !");

            Console.Write("\n4) Disabling the software autostart on safe mode...");
            SetStartupSafeMode(false, appPath);

            Console.Write("\n[UNBLOCKER Admins] Success !");
        }
    
        /// <summary>
        /// Disables both admin & non admin restrictions
        /// </summary>
        /// <param name="appPath"></param>
        /// <param name="foldersPathToUnloc"></param>
        public static void DisableAllPossibleRestrictions(string appPath)
        {
            NonAdmin.DisableAllNonAdminRestrictions();
            DisableAllAdminRestrictions(appPath);
        }

        /// <summary>
        /// Block / Unblock a website for any browser by editing the windows host file
        /// </summary>
        /// <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="block"></param>
        /// <param name="domain">Simple domain name eg. www.google.fr</param>
        public static void SetWebsiteBlocked(bool block, string domain)
        {
            string path = @"C:\Windows\System32\drivers\etc\hosts";
            if(block)
            {
                StreamWriter sw = new StreamWriter(path, true);
                string sitetoblock = "\n 127.0.0.1 " + domain;
                sw.Write(sitetoblock);
                sw.Close();
            }
            else
            {
               var text = File.ReadAllLines(path);
                List<string> newText = new List<string>(); 
                foreach (var line in text)
                {
                    if(!line.Contains(domain))
                    {
                        newText.Add(line);
                    }
                }
                File.WriteAllLines(path,newText);
            }
        }   

}

}
