using Bolter;
using System;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This is the demo (console version)");
            Admin.SetWebsiteBlocked(false, "www.google.fr");
            Console.ReadLine();
        }
    }
}
