using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Timers;

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
            if(cmdPath.StartsWith("\"") && cmdPath.EndsWith("\""))
            {
                return cmdPath.Substring(cmdPath[1], cmdPath[cmdPath.Length - 2]);
            }
            return cmdPath;
        }
        /// <param name="message"></param>
        public static void Warn(string message)
        {
            var oldColor = Console.ForegroundColor;
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
            /*
            if (string.IsNullOrWhiteSpace(url))
                return false;
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "HEAD";
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                //Any exception will returns false.
                return false;
            }
            */
        }
    }
}
