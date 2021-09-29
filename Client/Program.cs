using System;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client
{
    class Program
    {
        static AwesomeClient Client;
        static bool TimeMode = false;
        static string Username;

        static void Main(string[] args)
        {
            const int port = 4242;

            Console.Write("What if you told me your name? \n  > ");
            Username = Reader.Read();
            Console.Write("Do you know your path, {0}? \n  > ", Username);
            string address = "localhost";
            string input = Reader.Read();
            if (!String.IsNullOrEmpty(input))
            {
                address = input;
            }

            Reader.TabPressed += OnTab;
            Client = new AwesomeClient(address, port);
            byte[] key = Encoding.UTF8.GetBytes("M5NHKQHT");
            Client.SetupEncryption(MasterCrypt.Encrypt, MasterCrypt.Decrypt, key);
            Client.GotString += OnMessage;

            Console.WriteLine("\nWalking down your path...");
            if (!Client.Connect())
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("The door seem to be closed!");
                return;
            }

            Client.Send(Username, 0);

            while (Client.Connected)
            {
                string message = Reader.Read(false);
                if (!Evaluate(message) && message.Length > 0)
                {
                    Client.Send(message, 1);
                    Console.Write("<< ");
                }
            }

            Console.WriteLine("\n\rYou woke up!");
        }

        static bool Evaluate(string message)
        {
            switch (message.ToLower().Split(" ")[0])
            {
                case "/clear":
                    Console.Clear();
                    Console.Write("<< ");
                    break;
                case "/leave":
                    Client.Disconnect();
                    break;
                case "/time":
                    TimeMode = !TimeMode;
                    Console.WriteLine("\rYou bend the time.");
                    Console.Write("<< ");
                    break;
                default:
                    return false;
            }

            return true;
        }

        static void OnMessage(string data, byte flag)
        {
            if (flag <= 1)
            {
                //Setup color
                if (flag == 1)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                }
                else
                {
                    if (!data.StartsWith("["))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                    }
                    else if (Regex.IsMatch(data, "^[[]\\S+->\\S+[]]: .*"))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    }
                }

                if (TimeMode)
                {
                    data = "|" + DateTime.Now.ToLongTimeString() + "|>" + data;
                }

                string space = String.Concat(
                    Enumerable.Repeat(" ", Console.BufferWidth - data.Split('\n').Last().Length)
                );
                Console.Write("\r" + data + space);
                Console.ResetColor();
                Console.Write("<< ");
                Reader.Update();
            }
            else if (flag == 2)
            {
                Reader.InsertWord(data);
            }
        }

        static void OnTab(string word, int iteration)
        {
            Client.Send(word + " " + iteration, 2);
        }
    }
}