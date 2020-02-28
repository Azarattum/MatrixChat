using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client {
    class Program {
        static AwesomeClient Client;
        static bool TimeMode = false;

        static void Main(string[] args) {
            const int port = 4242;

            Console.Write("Username: ");
            string username = Reader.Read();
            Console.Write("Address: ");
            ///TEMP!
            string address = "localhost"; //Reader.Read();
            Console.WriteLine();

            Reader.TabPressed += OnTab;
            Client = new AwesomeClient(address, port);
            Client.GettedString += OnMessage;

            Console.WriteLine("Connecting...");
            if (!Client.Connect()) {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Unable to connect to the server!");
                return;
            }
            Console.WriteLine();

            Client.Send(username, 0);

            while (Client.Connected) {
                string message = Reader.Read(false);
                if (!Evaluate(message) && message.Length > 0) {
                    Client.Send(message, 1);
                    Console.Write("<< ");
                }
            }

            Console.WriteLine("\n\rThe connection was closed!");
        }

        static bool Evaluate(string message) {
            switch (message.ToLower().Split(" ") [0]) {
                case "/clear":
                    Console.Clear();
                    Console.Write("<< ");
                    break;
                case "/leave":
                    Client.Disconnect();
                    break;
                case "/time":
                    TimeMode = !TimeMode;
                    Console.WriteLine("\rTime mode toggled.");
                    break;
                default:
                    return false;
            }

            return true;
        }

        static void OnMessage(string data, byte flag) {
            if (flag <= 1) {
                //Setup color
                if (flag == 1) {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                } else {
                    if (!data.StartsWith("[")) {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                    } else if (Regex.IsMatch(data, "^[[]\\S+->\\S+[]]: .*")) {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    }
                }

                if (TimeMode) {
                    data = "|" + DateTime.Now.ToLongTimeString() + "|>" + data;
                }

                string space = String.Concat(
                    Enumerable.Repeat(" ", Console.BufferWidth - data.Length)
                );
                Console.WriteLine("\r" + data + space);
                Console.ResetColor();
                Console.Write("<< ");
                Reader.Update();
            } else if (flag == 2) {
                Reader.InsertWord(data);
            }
        }

        static void OnTab(string word, int iteration) {
            Client.Send(word + " " + iteration, 2);
        }
    }
}