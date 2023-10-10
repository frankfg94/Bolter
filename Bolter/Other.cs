using System;
using System.Diagnostics;
using System.IO;

namespace Bolter
{
    public static class Other
    {
        private const string Value = "----------------// BOLTER WARNING //-------------------";
        private const string Line = "-------------------------------------------------------";
        /// <summary>
        /// This function tries to connect to a google address that needs internet connection, 
        /// if that fails, then the value false will be returned
        /// </summary>
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new System.Net.WebClient())
                using (client.OpenRead("http://clients3.google.com/generate_204"))
                {
                    return true;
                }
            }
            catch
            {
                return false;

            }
        }

        /// <summary>
        /// Add double quotes around a string.
        /// We need to escape the path with double quotes to avoid errors if the path passed to cmd contains spaces
        /// </summary>
        /// <param name="cmdPath"></param>
        /// <returns></returns>
        public static string EscapeCMD(string cmdPath)
        {
            return "\"" + cmdPath + "\"";
        }

        /// <summary>
        /// Remove double quotes around a string.
        /// </summary>
        public static string UnescapeCMD(string cmdPath)
        {
            if (cmdPath.StartsWith("\"") && cmdPath.EndsWith("\""))
            {
                return cmdPath.Substring(cmdPath[1], cmdPath[cmdPath.Length - 2]);
            }
            return cmdPath;
        }
        /// <param name="message"></param>
        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Value);
            Console.WriteLine(message);
            Console.WriteLine(Line);
            Console.ResetColor();
        }

        public static void PrintColored(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Vérifie si l'adresse URL entrée existe bel et bien, et que le site est en état de marche
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static bool UrlAddressIsExisting(string url)
        {
            return Uri.IsWellFormedUriString(url, UriKind.Absolute);
        }

        /// <summary>
        /// Generate a process that will be started ran as administrator (if needed with a prompt dialog)
        /// </summary>
        /// <param name="processPath"></param>
        /// <returns></returns>
        public static Process GenerateAdminProcess(string processPath)
        {
            Process cmd = new Process();
            cmd.StartInfo = new ProcessStartInfo // Run as UAC
            {
                FileName = processPath,
                Verb = "runas",
                UseShellExecute = true,
            };
            return cmd;
        }

        /// <summary>
        /// Returns the path of an Exe to search in a folder
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="folderToSearchRecursively"></param>
        /// <returns></returns>
        public static string searchForExe(string fileName, string folderToSearchRecursively)
        {
            if (!fileName.EndsWith(".exe")) fileName = fileName + ".exe";
            foreach (string file in Directory.GetFiles(folderToSearchRecursively, "*.exe", SearchOption.AllDirectories))
            {
                Console.WriteLine(file);
                if (Path.GetFileName(file).Equals(fileName))
                {
                    return file;
                }
            }
            return null;
        }
        /// <summary>
        /// Send commands to a Bolter CLI with admin privileges, used from <see cref="NonAdmin"/> to Run <see cref="Admin"/> commands
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="adminAppPath"></param>
        /// <param name="adminAppName"></param>
        public static void SendCommandToAdminExecutor(string commandName, string adminAppPath, string adminAppName)
        {
            Console.WriteLine("===> Using Bolter Admin Command Executor");
            Process cmd = GenerateAdminProcess(adminAppPath);
            Console.WriteLine("Sending p:" + Other.EscapeCMD(Process.GetCurrentProcess().MainModule.FileName));
            cmd.StartInfo.ArgumentList.Add("p:" + Other.EscapeCMD(Process.GetCurrentProcess().MainModule.FileName));
            cmd.StartInfo.ArgumentList.Add(commandName);
            Console.WriteLine("Checking " + adminAppPath);

            if (!File.Exists(adminAppPath))
            {
                // If the file is not found for the first path, motivator will try getting the Admin app executable in the same folder of motivator console executable
                adminAppPath = Path.Join(Directory.GetParent(System.Reflection.Assembly.GetEntryAssembly().Location).FullName, adminAppName + ".exe");
                cmd.StartInfo.FileName = adminAppName;
                Console.WriteLine("Checking " + adminAppPath);
                if (!File.Exists(adminAppPath))
                {
                    Bolter.Other.Warn("[X] Failed to find the UAC service installer on all listed paths, service can't be started");
                    return;
                }
            }
            Console.WriteLine("Opening UAC authorized window for Installing Service");
            cmd.Start();
            cmd.WaitForExit();
        }
    }
}
