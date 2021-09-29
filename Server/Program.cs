using System;
using System.IO;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            const int port = 4242;
            ServerManager server = new ServerManager(port);
            server.Start();
            Console.WriteLine("Server started!");
            Console.ReadKey();
            server.Stop();
        }
    }
}