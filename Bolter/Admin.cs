using Bolter.Attributes;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace Bolter
{
    [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
    /// <summary>
    /// This class provides useful kiosk / security commands, it requires the windows program to be in UAC/administrator mode
    /// To enable this, you can set the app to start in administrator for a C# program with the manifest file
    /// </summary>
    public static class Admin
    {
        /// <summary>
        /// Access token for impersonation
        /// </summary>
        public static SafeAccessTokenHandle phToken;
        private static IntPtr adminToken;
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

        enum TOKEN_INFORMATION_CLASS
        {
            TokenUser,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken = 19,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            TokenIsAppContainer,
            TokenCapabilities,
            TokenAppContainerSid,
            TokenAppContainerNumber,
            TokenUserClaimAttributes,
            TokenDeviceClaimAttributes,
            TokenRestrictedUserClaimAttributes,
            TokenRestrictedDeviceClaimAttributes,
            TokenDeviceGroups,
            TokenRestrictedDeviceGroups,
            TokenSecurityAttributes,
            TokenIsRestricted,
            TokenProcessTrustLevel,
            TokenPrivateNameSpace,
            TokenSingletonAttributes,
            TokenBnoIsolation,
            TokenChildProcessFlags,
            TokenIsLessPrivilegedAppContainer,
            TokenIsSandboxed,
            TokenOriginatingProcessTrustLevel,
            MaxTokenInfoClass
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(
    IntPtr TokenHandle,
    TOKEN_INFORMATION_CLASS TokenInformationClass,
    IntPtr TokenInformation,
    uint TokenInformationLength,
    out uint ReturnLength);


        public struct TOKEN_USER
        {
            public SID_AND_ATTRIBUTES User;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {

            public IntPtr Sid;
            public int Attributes;
        }



        private static IntPtr GetAdminToken(ref IntPtr restrictedToken)
        {
            /*
            bool Result;
            IntPtr hLinkedToken = IntPtr.Zero;
            uint iRetSize;
            //token = WindowsIdentity.GetCurrent().Token;
            //Result = GetTokenInformation(WindowsIdentity.GetCurrent().Token, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, TokenInfLength, out TokenInfLength);
            Result = GetTokenInformation(restrictedToken, TOKEN_INFORMATION_CLASS.TokenLinkedToken, hLinkedToken,1000, out iRetSize);

            if (!Result)
            {
                int ret = Marshal.GetLastWin32Error();
                // throw new System.ComponentModel.Win32Exception(ret);
                Console.WriteLine("Error " + 24);
            }
            */

            return restrictedToken;
        }

        /// <summary>
        /// Block / Unblock both the batch (.bat) files and the CMD console. It doesn't require a restart from the computer and works immediatly
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="block"></param>
        public static void SetBatchAndCMDBlock(bool block, string username)
        {
            /*
            string currentUsername = "";
            if(phToken != null)
            {
                Console.WriteLine("Before impersonation username : " + WindowsIdentity.GetCurrent().Name);
                if(adminToken == null)
                {
                    var handle = phToken.DangerousGetHandle();
                    adminToken = GetAdminToken(ref handle);
                }
                Console.WriteLine("Token :" + adminToken.ToInt64());
                WindowsIdentity.RunImpersonated(
                new SafeAccessTokenHandle(adminToken),
                // User action
                () =>
                {
                    // Check the identity.
                    currentUsername = Environment.UserName;
                    Console.WriteLine("During impersonation username : " + WindowsIdentity.GetCurrent().Name);
                }
                );
            }
            else
            {
                _SetBatchAndCMDBlock(block);
            }
            */
            _SetBatchAndCMDBlock(block, username);

        }

        private static void _SetBatchAndCMDBlock(bool block, string currentUsername)
        {
            var p = (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "logBolterService.txt"));
            try
            {
                NTAccount f = new NTAccount(currentUsername);
                SecurityIdentifier s = (SecurityIdentifier)f.Translate(typeof(SecurityIdentifier));
                String sidString = s.ToString();

                string subKey = @"\Software\Policies\Microsoft\Windows\System";
                    var key = Registry.Users.CreateSubKey(sidString + subKey, true);
                    if (key != null)
                    {
                        File.AppendAllText(p, sidString);
                        Console.WriteLine("Setting :" + key.Name);
                        Console.WriteLine(key.ToString());
                        
                        if (block)
                            key.SetValue("DisableCMD", 1); //  A 1 bloque CMD et les fichiers BAT , à 2 bloque CMD seulement
                        else
                            key.SetValue("DisableCMD", 0); // Débloque CMD et les fichiers BAT
                    }
                    else
                    {
                        throw new NullReferenceException("Error : key is null for registry current user");
                    }
            }
            catch (Exception ex)
            {
                File.AppendAllText(p, ex.ToString());
                File.AppendAllText(p, ex.Message);
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Nécéssite le fichier ntrights.exe installé dans System32, cette fonction décide de si il est possible ou non pour utilisateur de modifier la date ou l'heure, nécéssite UAC
        /// Car c'est très puissant, ça peut en effet même bloquer les administrateurs . On est obligés de redémarrer la session pour voir les changements.
        /// PROBLEM ! This doesn't work when doing remoting (block the execution of the code)
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

        [RequireAdministrator]
        public static void UninstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service")
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
                }
            }
            cmd.WaitForExit();

        }



        /// <summary>
        /// Install a service automatically from the folder AdminBolterService as LocalSystem (highest possible privileges)
        /// At the moment, it checks the service in the publish folder of the project, it must be fixed for production usage.
        /// It can also work if the app is not in admin mode & <paramref name="enableUACprompt"/> is set to true. An UAC prompt will ask to install the service with a slightly different code (to solve compatibility issues that we have if we reuse this same function)
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode (if you don't want any prompts) </i></remarks>
        /// <param name="serviceExeName">Name of the executable without the exe</param>
        public static void InstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service", bool autoStart = true, bool enableUACprompt = true)
        {
            //cmd.StartInfo.CreateNoWindow = true;
            if(enableUACprompt && !NonAdmin.IsInAdministratorMode())
            {
                string projectDirPath = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName; // The path to all visual studio projects
                var adminAppPath = projectDirPath + @$"\Bolter\BolterAdminApp\bin\Debug\netcoreapp3.1\BolterAdminApp.exe";
                System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                cmd.StartInfo.FileName = adminAppPath;
                cmd.StartInfo.Verb = "runas";
                cmd.StartInfo.UseShellExecute = true;
                cmd.StartInfo.ArgumentList.Add("p:"+ Process.GetCurrentProcess().MainModule.FileName);
                cmd.StartInfo.ArgumentList.Add("InstallAdminService");
                cmd.Start();
                cmd.WaitForExit();
                Console.WriteLine("Motivator Service is now installed!");
            }
            else
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

                string projectDirPath = Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName; // The path to all visual studio projects
                var path = projectDirPath + @$"\Bolter\AdminBolterService\bin\Release\netcoreapp3.1\publish\{serviceExeName}.exe";
                var commandCreateService = $"sc create \"{serviceName}\" binPath=" + path;
                if (!File.Exists(path))
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



        }

        /// <summary>
        /// [SEMI-VALIDATED] Enable / Disable the app to start in safe mode
        /// The algorithm need testing, because if the regkey is badly set, it will create a black screen on the computer
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
                if (applicationFullPath.Equals("useThisApp"))
                {
                    applicationFullPath = Process.GetCurrentProcess().MainModule.FileName;
                }
                string safeModePrograms = (string)key.GetValue("Shell");
                if (autoStartEnabled)
                {
                    if(!safeModePrograms.Contains(applicationFullPath))
                    // Append our app to the list of the apps
                    key.SetValue("Shell",safeModePrograms + ";" +applicationFullPath);
                }
                else
                {
                    // Remove the app path without touching the other datas
                    var before = safeModePrograms; 
                    var after = safeModePrograms.Replace(";" + applicationFullPath, string.Empty);
                    var bolterBlacklistedStrings = new string[] { "Bolter", "Motivator"};
                    // If the remove 1 couldn't work, try the second technique, remove the path
                    if(before.Equals(after))
                    {
                        // First fail, remove 2 removes all the paths containing the critical/blacklisted strings
                        foreach (var path in safeModePrograms.Split(";"))
                        {
                            foreach (var blackListString in bolterBlacklistedStrings)
                            {
                                if (path.Contains(blackListString))
                                {
                                    after = safeModePrograms.Replace(";" + path, string.Empty);
                                }
                            }
                        }
                        // If the remove 2 failed 
                        if(before.Equals(after))
                        {
                            Other.Warn("We couldn't remove the autostart precisely, instead we choose the reset the regedit command to 'explorer' ");
                            key.SetValue("Shell", "explorer");
                        }
                        else
                        {
                            // Removal 2 success
                            key.SetValue("Shell",after);
                        }
                    }
                    else
                    {
                        // Removal 1 sucesss
                        key.SetValue("Shell",after);
                    }

                }
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
        /// Enable or disable the Task manager, powerful because cannot be bypassed direclty by a system administrator. For security reasons, it will unlock after a certain amount of time.
        /// </summary>
        /// <param name="isActivated"></param>
        /// <param name="customSecurityDuration">Maximum is 48 hours & minimum is 1 hour </param>
        public static void SetTaskManagerActivation(bool isActivated, int customSecurityDuration = 48)
        {
            if (customSecurityDuration > 72)
                customSecurityDuration = 72;
            else if (customSecurityDuration <= 0)
                customSecurityDuration = 1;

            Microsoft.Win32.RegistryKey key;
            key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", true);
            if (isActivated)
            {
                key.DeleteValue("DisableTaskMgr");
                key.Close();
            }
            else 
            {
                key.SetValue("DisableTaskMgr", "1", RegistryValueKind.DWord);
                key.Close();
                EnableTaskManagerSecurity(customSecurityDuration); // Très important, il faut toujours avoir une issue de secours, surtout pour un système aussi crucial
            }
        }



        /// <summary>
        /// Auto re-enable the Task manager after the indicated number of hours
        /// </summary>
        /// <param name="hoursBeforeUnlock"></param>
        private static void EnableTaskManagerSecurity(int hoursBeforeUnlock)
        {
            var t = new System.Timers.Timer
            {
                Interval = hoursBeforeUnlock * 3600 * 1000,
                AutoReset = false
            };
            t.Elapsed += (s, e) => SetTaskManagerActivation(true);
            t.Start();
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
            Console.WriteLine("[BOLTER] Disabling all Admin restrictions");
            Console.WriteLine("[UNBLOCKER Admins] We chose to unlock the computer with administrator commands");
            Console.WriteLine(">> Unlocking Task manager");
            SetTaskManagerActivation(true);


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
            Admin.SetBatchAndCMDBlock(false, username: "franc");
            Console.Write("     Success !");

            Console.Write("\n3) Disabling the software autostart on safe mode...");
            SetStartupSafeMode(false, appPath);

            Console.Write("\n4) Re activating date editing (using ntrights.exe)...");
            // PreventDateEditingW10(false); // DISABLED at the moment because with remoting, console is blocked (command process doesn't start?)
            Console.Write("     Success !");

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
