using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bolter.BolterAdminApp
{
    public class TcpServer 
    {
       
        public void Start(IPAddress iPAddress, int port)
        {
            TcpListener l = new TcpListener(iPAddress, port);
            l.Start();
            Console.WriteLine("Server Started ! ");
            if(!Bolter.NonAdmin.IsInAdministratorMode())
            {
                Bolter.Other.Warn("This server doesn't have administrator privileges, it might not behave as expected");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Administrator privilege ON");
                Console.ResetColor();
            }
            while (true)
            {
                var comm = l.AcceptTcpClient();
                Console.WriteLine("Connection established for endpoint : " + comm.Client.RemoteEndPoint);
                new Thread(new ReceiverServer(comm).doOperation).Start();
            }
        }
    }
}
