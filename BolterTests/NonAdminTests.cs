using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bolter;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Bolter.Tests
{
    [TestClass()]
    public class NonAdminTests
    {
        static string folder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static string filePath = "";

        /// <summary>
        /// Before method
        /// </summary>
        /// <param name="context"></param>
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            filePath = folder + "\\Test.txt";
            NonAdmin.ClearAutoClosePrograms();
            NonAdmin.UnlockFolder(filePath);
            NonAdmin.HideAndProtectFolder(filePath, false);
        }

        [TestMethod()]
        public void IsWindowsInSafeModeTest()
        {
            NonAdmin.IsWindowsInSafeMode();
        }

        [TestMethod()]
        public void CloseProgramTest()
        {
            Process newNotepad = new Process();
            newNotepad.StartInfo.FileName = "notepad.exe";
            newNotepad.Start();
            Thread.Sleep(2000);
            NonAdmin.CloseProgram("notepad");
            Assert.IsTrue(newNotepad.HasExited);
        }

        [TestMethod()]
        public void DoesServiceExistTest()
        {
            Assert.IsTrue(NonAdmin.DoesServiceExist("SysMain",Environment.MachineName));
        }

        /// <summary>
        /// I/O Test
        /// </summary>
        [TestMethod()]
        public void LockFolderTest()
        {
            try
            {
                File.WriteAllText(filePath,"Test");
                NonAdmin.LockFolder(filePath, false);
                NonAdmin.HideAndProtectFolder(filePath, true);
                File.Delete(filePath);
            }
            catch(Exception)
            {
                Assert.IsTrue(File.Exists(filePath));
            }
            finally
            {
                Assert.IsTrue(File.Exists(filePath));
            }
        }
        [TestMethod()]
        public void HideAndProtectFolderTest()
        {
            try
            {
                File.WriteAllText(filePath, "Test");
                NonAdmin.HideAndProtectFolder(filePath, true);
                File.Delete(filePath);
            }
            catch (Exception)
            {
                Assert.IsTrue(File.Exists(filePath));
            }
            finally
            {
                Assert.IsTrue(File.Exists(filePath));
            }
        }
        [Ignore]
        [TestMethod()]
        public void AddAutoLockFolderTest()
        {
            Assert.Fail();
        }
        [Ignore]
        [TestMethod()]
        public void GetTaskManagerActivationTest()
        {
            string taskmgr  = "taskmgr";
            bool enabled = NonAdmin.GetTaskManagerActivation();
            if(enabled)
            {
                Process.Start("CMD.exe", $"/C  {taskmgr}");
                Assert.IsTrue(Process.GetProcessesByName(taskmgr).Length > 0);
            }
        }
        [Ignore]
        [TestMethod()]
        public void GetIdleTimeTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void LockTheScreenTest()
        {
            Assert.Fail();
        }

        /// <summary>
        /// Type End to End, GUI
        /// </summary>
        [TestMethod()]
        public void CloseFileExplorerTest()
        {
            Process explorer = new Process();
            explorer.StartInfo.FileName = "explorer.exe";
            explorer.Start();
            Thread.Sleep(2000);
            NonAdmin.CloseFileExplorer();
            Assert.IsTrue(explorer.HasExited);
        }

        [Ignore]
        [TestMethod()]
        public void CloseFileExplorerTest1()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void GetFileExplorerPathsTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void ResumeProcessTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void RunRespawnableProgramTest()
        {
            string programName = "notepad";
            string programExeName = "notepad.exe";
            NonAdmin.RunRespawnableProgram(programExeName, DateTime.Now,DateTime.Now.AddMinutes(1));
            Thread.Sleep(2000);
            NonAdmin.CloseProgram(programName);
            Thread.Sleep(3000);

            // We check if the program has resurrected
            Assert.IsTrue(Process.GetProcessesByName(programName).Length > 0);
            NonAdmin.CloseProgram(programName);
        }

        [Ignore]
        [TestMethod()]
        public void RenameProcessTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void RenameProcessTest1()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void CreateLocalUserTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void LogoffThisUserTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void DeleteLocalUserTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void SwitchToSpecificAccountTest()
        {
            Assert.Fail();

        }

        [Ignore]
        [TestMethod()]
        public void MoveWindowToDesktopTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void SetTaskbarVisibleTest()
        {
            Assert.Fail();
        }

        [Ignore]
        [TestMethod()]
        public void MakeThisProgramRespawnableTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void AddAutoCloseProgramsTest()
        {
            int i = 0;
            NonAdmin.AutoCloseProgramListChanged += (s, e) =>
              {
                  if(e.ProgramAction == NonAdmin.ProgramListAction.Added)
                  {
                    i++;
                  }
              };
            NonAdmin.AddAutoCloseProgram("test", TimeSpan.Zero, new TimeSpan(23, 59, 59), false);
            NonAdmin.AddAutoCloseProgram("test2", TimeSpan.Zero, new TimeSpan(23, 59, 59), false);
            Assert.AreEqual(i,2);
            NonAdmin.ClearAutoClosePrograms();
        }

        [TestMethod()]
        public void IsInAdministratorModeTest()
        {
            // No way to be sure at the moment
            NonAdmin.IsInAdministratorMode();
        }
        [Ignore]
        [TestMethod()]
        public void DisableAllNonAdminRestrictionsTest()
        {
            Assert.Fail();
        }
    }
}