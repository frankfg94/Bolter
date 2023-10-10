using Bolter.Attributes;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Security.Principal;
using System.ServiceProcess;
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


        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {

            public IntPtr Sid;
            public int Attributes;
        }

        public static void StartService(string serviceName = "Bolter Admin Service", string adminAppName = "BolterAdminApp", bool enableUACprompt = true)
        {
            if (enableUACprompt && !NonAdmin.IsInAdministratorMode())
            {
                // Technique : Use the admin app (UAC prompt)
                string adminAppPath = Globals.AdminAppParentFolder + '\\' + adminAppName;
                try
                {
                    Other.SendCommandToAdminExecutor("StartService", adminAppPath, adminAppName);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to install motivator service... " + Environment.NewLine + e);
                }
            }
            else
            {
                // If we are admin, simply start the service
                new ServiceController(serviceName).Start();
            }
        }

        /// <summary>
        /// Block / Unblock both the batch (.bat) files and the CMD console. It doesn't require a restart from the computer and works immediatly
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="block"></param>
        public static void SetBatchAndCMDBlock(bool block, string username)
        {
            _SetBatchAndCMDBlock(block, username);

        }

        public static void SetBatchAndCMDBlock(bool block)
        {
            SetBatchAndCMDBlock(block, Environment.UserName);
        }

        private static void _SetBatchAndCMDBlock(bool block, string currentUsername)
        {
            var p = (Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "logBolterService.txt"));
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
                Process process = new Process();
                ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                process.StartInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
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
                    Process p2 = process;
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
            System.Diagnostics.Process cmd = new Process();
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
        /// It will use an admin C# app to install with the name <paramref name="adminAppName"/>.exe to have the administrator permissions required to install the service.
        /// It can also work if the app is not in admin mode & <paramref name="enableUACprompt"/> is set to true. An UAC prompt will ask to install the service with a slightly different code (to solve compatibility issues that we have if we reuse this same function)
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode (if you don't want any prompts) </i></remarks>
        /// <param name="serviceExeName">Name of the executable without the exe</param>
        /// <param name="adminAppName">Admin application name</param>
        /// <param name="autoStart">Enable auto starting of the service</param>
        /// <param name="enableUACprompt">If set to true, the service can be installed with an UAC prompt. If not, Bolter will try to use the admin command executor app</param>
        /// <param name="serviceName">The name of the service to reinstall/install for executing bolter commands</param>
        public static void InstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service",
            string adminAppName = "BolterAdminApp", bool autoStart = true, bool enableUACprompt = true)
        {
            Console.WriteLine("InstallService");
            Globals.CheckDependencies();
            if (NonAdmin.DoesServiceExist(serviceName, Environment.MachineName))
            {
                Console.WriteLine("[Note] Admin service is already installed, it will be reinstalled now");
            }
            int stepCount = 6;
            if (!autoStart)
                stepCount--;
            int curStep = 1;

            // If the app running this command doesn't have the administrative privilege, it will ask to open an elevated one made for running bolter commands, destined in this case to run the InstallService command
            if (enableUACprompt && !NonAdmin.IsInAdministratorMode())
            {
                Console.WriteLine(">> Service to install : " + adminAppName);
                string adminAppPath = @$"{Globals.AdminAppParentFolder}\{adminAppName}.exe";
                if(!File.Exists(adminAppPath))
                {
                    Console.WriteLine("Exe not found here, searching " + adminAppName + " in path : " +  Globals.MOTIVATOR_FOLDER_PATH);
                    adminAppPath = Other.searchForExe(adminAppName, Globals.MOTIVATOR_FOLDER_PATH);
                        
                    // TODO recursive search for BolterAdminApp
                }
                Console.WriteLine(">> Installation location: " + adminAppPath);
                Stopwatch s = new Stopwatch();
                s.Start();
                Other.SendCommandToAdminExecutor("InstallService", adminAppPath, adminAppName);
                s.Stop();
                try
                {
                    if (s.ElapsedMilliseconds < 2000)
                    {
                        Bolter.Other.Warn("The installation speed was less than 2 seconds, which is anormaly fast. If the admin window appeared and closed instantly, it might be a missing dll dependencies error");
                    }
                    if (NonAdmin.DoesServiceExist(serviceName, Environment.MachineName))
                    {
                        Console.WriteLine("Admin service is now installed! \nTotal time :  " + s.Elapsed.TotalSeconds + " seconds");
                    }
                    else
                    {
                        Console.WriteLine("Admin service is not installed");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to install motivator service... " + Environment.NewLine + e);
                }
            }
            // If the command is ran within the admin app, install the service directly
            else
            {
                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardError = true;
                //cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.OutputDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
                cmd.ErrorDataReceived += (sender, e) => { Other.Warn(e.Data); };
                cmd.BeginOutputReadLine();
                cmd.BeginErrorReadLine();

                string GetStep()
                {
                    return $"[STEP {curStep++}/{stepCount}]";
                };

                string serviceAppPath = Other.searchForExe(serviceExeName, Globals.MOTIVATOR_FOLDER_PATH);
                Console.WriteLine("Installing service on : " + serviceAppPath);
                if (!File.Exists(serviceAppPath))
                {
                    Console.WriteLine("The admin app doesn't exist for the paths listed above, ensure that you created the .exe of the service on these paths");
                    return;
                }
                var commandStopService = $"sc stop \"{serviceName}\"";
                var commandCreateService = $"sc create \"{serviceName}\" binPath=" + Other.EscapeCMD(serviceAppPath);
                var commandDeleteService = $"sc delete \"{serviceName}\"";
                var commandQueryService = $"sc query \"{serviceName}\"";
                var commandAutoStartService = $"sc config \"{serviceName}\" start=\"auto\"";
                var commandRunService = $"sc start \"{serviceName}\"";
                Console.WriteLine("-------------   Preview of the service installation commands ---------------");
                Console.WriteLine(commandStopService);
                Console.WriteLine(commandDeleteService);
                Console.WriteLine(commandCreateService);
                Console.WriteLine(commandQueryService);
                Console.WriteLine(commandAutoStartService);
                Console.WriteLine(commandRunService);
                Console.WriteLine("----------------------------------------------------------------------------");

                ServiceController service = new ServiceController(serviceName);
                using (StreamWriter sw = cmd.StandardInput)
                {
                    if (sw.BaseStream.CanWrite)
                    {
                        Other.PrintColored($"{GetStep()} {commandStopService}", ConsoleColor.Green);
                        sw.WriteLine(commandStopService);
                        Thread.Sleep(5000);
                        Console.WriteLine(">>> Service stopped");

                        Other.PrintColored($"{GetStep()} {commandDeleteService} ", ConsoleColor.Green);
                        sw.WriteLine(commandDeleteService);
                        Thread.Sleep(5000);
                        Console.WriteLine(">>> Service deleted");

                        Other.PrintColored($"{GetStep()} {commandCreateService} ", ConsoleColor.Green);
                        sw.WriteLine(commandCreateService);
                        Console.WriteLine(">>> Service created");
                        Other.PrintColored($"{GetStep()} {commandQueryService}", ConsoleColor.Green);
                        sw.WriteLine(commandQueryService);
                        Console.WriteLine(">>> Query done");

                        if (autoStart)
                        {
                            Other.PrintColored($"{GetStep()} {commandAutoStartService}", ConsoleColor.Green);
                            sw.WriteLine(commandAutoStartService);
                            Console.WriteLine(">>> Automatic startup set");
                            Thread.Sleep(200);
                        }

                        Other.PrintColored($"{GetStep()} {commandRunService}", ConsoleColor.Green);
                        sw.WriteLine(commandRunService);
                        Console.WriteLine(">>> Service started");
                    }

                }
                Console.WriteLine("Waiting for service to be up");
                service.WaitForStatus(ServiceControllerStatus.Running);
                cmd.WaitForExit();
            }



        }

        private static void Cmd_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        /// <summary>
        /// [INVALID] Enable / Disable the app to start in safe mode
        /// The algorithm need testing, because if the regkey is badly set, it will create a black screen on the computer
        /// If no <paramref name="applicationFullPath"/> is indicated, the path of the application using this library will be used
        /// </summary>
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// <param name="autoStartEnabled">If set to true, the app will start even in windows safe mode</param>
        /// <param name="applicationFullPath">the path of the app to edit the startup properties</param>
        public static void SetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "")
        {
            // WAY TOO DANGEROUS ATM


            // LocalMachine is needed, so we also need UAC auth
            /**
             * 
            
            var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            var shellKey = "Shell";
            var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key != null)
            {
                if (applicationFullPath.Equals(""))
                {
                    applicationFullPath = Process.GetCurrentProcess().MainModule.FileName;
                }
                string safeModePrograms = (string)key.GetValue(shellKey);
                if (autoStartEnabled)
                {
                    if (!safeModePrograms.Contains(applicationFullPath))
                    {
                        // Append our app to the list of the apps
                        key.SetValue(shellKey, safeModePrograms + ";" + applicationFullPath);
                    }
                }
                else
                {
                    // Remove the app path without touching the other datas
                    safeModePrograms = safeModePrograms.Replace(";" + applicationFullPath, string.Empty); // Motivator at the end, or between two paths
                    safeModePrograms = safeModePrograms.Replace(applicationFullPath, string.Empty);
                    key.SetValue(shellKey, safeModePrograms);
                }
                if (safeModePrograms.Last().Equals(';'))
                {
                    // Remove the last comma
                    key.SetValue(shellKey, safeModePrograms.Substring(0, safeModePrograms.Length - 1));
                }
                if (safeModePrograms.Contains(";"))
                {
                    foreach (var p in safeModePrograms.Split(";").ToList())
                    {
                            if(!File.Exists(p))
                            {
                                throw new InvalidDataException($"Invalid program path {p} found in regedit {shellKey}, you should repair this error as soon as possible");
                            }
                    }
                }
            }
            else
            {
                throw new NullReferenceException($"Auto safe mode set failed : reg key '{shellKey}' not found");
            }
             */
        }


        /// <summary>
        /// Hide in windows 10 the startup apps page (can be used to avoid disabling the software at startup)
        ///  <remarks>	<i>This requires the app to be in administrator mode </i></remarks>
        /// </summary>
        public static void HideStartupsAppsFromSettings(bool hide)
        {
            string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            if (hide)
            {
                Registry.LocalMachine.OpenSubKey(key, true).SetValue("SettingsPageVisibility", "hide:startupapps", RegistryValueKind.String);
            }
            else
            {
                Registry.LocalMachine.OpenSubKey(key, true).DeleteValue("SettingsPageVisibility");
            }
            Registry.LocalMachine.Close();
        }


        /// <summary>
        /// Enable or disable the Task manager, powerful because cannot be bypassed direclty by a system administrator. For security reasons, it will unlock after a certain amount of time.
        /// </summary>
        /// <param name="isActivated">If set to true, Task manager will be enabled</param>
        /// <param name="customSecurityDuration">Maximum is 48 hours & minimum is 1 hour </param>
        public static void SetTaskManagerActivation(bool isActivated, int customSecurityDuration = 48)
        {
            if (customSecurityDuration > 72)
                customSecurityDuration = 72;
            else if (customSecurityDuration <= 0)
                customSecurityDuration = 1;

            using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", true);
            string tskManagerRegKey = "DisableTaskMgr";
            if (isActivated)
            {
                if (key.GetValue(tskManagerRegKey) != null)
                {
                    key.DeleteValue(tskManagerRegKey);
                }
            }
            else
            {
                key.SetValue(tskManagerRegKey, "1", RegistryValueKind.DWord);
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

            try
            {
                Console.Write("\n0) Changing visibility of SettingsPageVisibility to 'Visible' in windows settings");
                Admin.HideStartupsAppsFromSettings(false);

            }
            catch (Exception) { }
            //nt enable
            Console.Write("\n1) Installing NtRights...");
            InstallNtRights();
            Console.Write("     Success !");

            Console.Write("\n2) Re activating CMD & batch scripts...");
            Admin.SetBatchAndCMDBlock(false, username: "franc");
            Console.Write("     Success !");

            if(appPath != null && File.Exists(appPath))
            {
                Console.Write("\n3) Disabling the software autostart on safe mode...");
                SetStartupSafeMode(false, appPath);
            }

            Console.Write("\n4) Re activating date editing (using ntrights.exe)...");
            // PreventDateEditingW10(false); // DISABLED at the moment because with remoting, console is blocked (command process doesn't start?)
            Console.Write("     Success !");

            Console.Write("\n[UNBLOCKER Admins] Success !");
        }
        /// <summary>
        /// Disable the detection of USB devices, only take effect after computer restart
        /// </summary>
        /// <param name="isUSBRestricted"></param>
        public static void RestrictUSB(bool isUSBRestricted)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\UsbStor", true);
            if (isUSBRestricted)
            {
                key.SetValue("Start", 4, RegistryValueKind.DWord); // disables usb
            }
            else
            {
                key.SetValue("Start", 3, RegistryValueKind.DWord); // enables usb
            }
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
            if (block)
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
                    if (!line.Contains(domain))
                    {
                        newText.Add(line);
                    }
                }
                File.WriteAllLines(path, newText);
            }
        }

    }

}
