using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bolter.BolterAdminApp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Bolter.BolterAdminApp.Tests
{
    [TestClass()]
    public class IpcServerTests
    {
        PipeClient client;
        PipeServer server;

        /// <summary>
        /// This methods tests the connection between the client & the server, if the server is started first
        /// </summary>
        [TestMethod()]
        public void TestClientToServerConnection()
        {
            Thread clientThd = new Thread(() =>
            {
                PipeClient client = new PipeClient();
                client.ConnectToAdminBolterService(4000);
                Assert.Equals(client.IsConnected(), true);
            });
            Thread serverThd = new Thread(() =>
            {
                PipeServer server = new PipeServer();
                server.Start();
            });

            serverThd.Start();
            Thread.Sleep(5000);
            clientThd.Start();
        }
        [TestMethod()]
        public void DisposeTest()
        {
            server = new PipeServer();
            server.Dispose();
        }

        /// <summary>
        /// This function should timeout if fail
        /// </summary>
        [TestMethod()]
        public void SendMessageTest()
        {
            TearDown();
            Thread clientThd = new Thread(()  =>
            {
                PipeClient client = new PipeClient();
                client.ConnectToAdminBolterService(4000);
                client.SendMessage("Hey");
                Assert.Equals(client.IsConnected(), true);
            });
            Thread serverThd = new Thread(() =>
            {
                PipeServer server = new PipeServer();
                server.Start();
            });

            serverThd.Start();
            Thread.Sleep(5000);
            clientThd.Start();
        }


        [TestCleanup]
        public void TearDown()
        {
            if(server != null)
            {
                server.Dispose();
                server = null;
            }
            if(client != null)
            {
                client.Dispose();
                client = null;
            }
        }
    }

}