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

        public static void Warn(string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Value);
            Console.WriteLine(message);
            Console.WriteLine(Line);
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
