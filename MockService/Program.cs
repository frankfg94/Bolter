using Bolter;
using Bolter.BolterAdminApp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace MockService
{
    // This programs run like a service, it aims to test the ipc system faster
    class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine(">>> This is the mock service <<<");
            PipeServer server = new PipeServer();
            server.Start();
        }
   


    }
}