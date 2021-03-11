using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bolter.BolterAdminApp;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.Threading;

namespace Bolter.BolterAdminApp.Tests
{
    [TestClass()]
    public class ReceiverClientTests
    {
        private const string IP_SERVER_ADDRESS = "127.0.0.1";
        private const int PORT = 8976;

        [TestMethod()]
        public void RequestSetStartupSafeModeTest()
        {
            if(NonAdmin.DoesServiceExist("Bolter Admin Service", Environment.MachineName))
            {
                var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                var shellKey = "Shell";
                var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                
                ReceiverClient client = new ReceiverClient();
                client.ConnectToBolterService(IP_SERVER_ADDRESS, PORT);
                
                Thread.Sleep(1000);
                string initial = (string)key.GetValue(shellKey);

                Console.WriteLine("Initial value :" + initial);
                client.RequestSetStartupSafeMode(true, System.Reflection.Assembly.GetEntryAssembly().Location);

                Thread.Sleep(1000);
                string edited = (string)key.GetValue(shellKey);
                
                Console.WriteLine("Edited value :" + edited);
                client.RequestSetStartupSafeMode(false, System.Reflection.Assembly.GetEntryAssembly().Location);

                Thread.Sleep(1000);
                string revert = (string)key.GetValue(shellKey);
                
                Console.WriteLine("Revert value :" + revert);
                Assert.AreEqual(initial, revert);
            }
            else
            {
                Assert.Fail("The Bolter Service must be installed to run this test");
            }
        }
    }
}