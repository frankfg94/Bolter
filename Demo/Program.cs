using Bolter;
using System;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This is the demo (console version)");
            NonAdmin.RunRespawnableProgram(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", DateTime.Now, DateTime.Now.AddMinutes(2));
            Console.ReadLine();
        }
    }
}
