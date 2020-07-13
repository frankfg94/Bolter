using Bolter;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowsDesktop;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This is the demo (console version)");
            // folderLockTest();
            NonAdmin.SetTaskbarVisible(true);
        }


        static void folderLockTest()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\BUNKER";
            Directory.CreateDirectory(path);
            NonAdmin.LockFolder(path, false);
            Thread.Sleep(20000);
            NonAdmin.UnlockFolder(path);
            Console.ReadLine();
        }
    }
}
