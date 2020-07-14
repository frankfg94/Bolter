﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace Bolter
{
    public class Other
    {
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
            var oldColor = Console.BackgroundColor;
            Console.WriteLine("----------------// BOLTER WARNING //-------------------");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.WriteLine("-------------------------------------------------------");
            Console.ForegroundColor = oldColor;
        }
    }
}
