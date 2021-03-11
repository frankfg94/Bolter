using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bolter;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace Bolter.Tests
{
    [TestClass()]
    public class AdminTests
    {

        [TestMethod()]
        public void SetStartupSafeModeTest()
        {
            if (!NonAdmin.IsInAdministratorMode())
            {
                throw new UnauthorizedAccessException("We need to run these tests with UAC enabled");
            }
            var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            var shellKey = "Shell";
            var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
            string initial = (string)key.GetValue(shellKey);
            Admin.SetStartupSafeMode(true);
            Admin.SetStartupSafeMode(true); // Try to put 2 times the same path
            string edited = (string)key.GetValue(shellKey);
            Console.WriteLine("Edited to " + edited);
            Admin.SetStartupSafeMode(false);
            string revert = (string)key.GetValue(shellKey);
            Console.WriteLine("Reverted to " + revert);
            Assert.AreEqual(initial, revert);
        }
    }
}