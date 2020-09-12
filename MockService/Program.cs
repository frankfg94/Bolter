using Bolter;
using Bolter.BolterAdminApp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Threading;

namespace MockService
{
    // This programs run like a service, it aims to test the ipc system faster
    class Program
    {
        enum MockMode
        {
            Tcp,
            Pipe
        }
        static void Main(string[] args)
        {
            Console.WriteLine(">>> This is the mock service <<<");
            var mode = MockMode.Tcp;
            if(mode == MockMode.Pipe)
            {
                var server = new PipeServer();
                server.Start();
            }
            else if (mode == MockMode.Tcp)
            {
                var server = new TcpServer();
                server.Start(new IPAddress(new byte[] { 127, 0, 0, 1 }), 8976);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
   


    }
}