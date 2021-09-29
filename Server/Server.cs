using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;

namespace Server
{
    public class ServerManager
    {
        private AwesomeServer Server;
        private Dictionary<TcpClient, string> Nicknames;
        private string[] Commands;

        public ServerManager(int port)
        {
            Server = new AwesomeServer(port);
            Nicknames = new Dictionary<TcpClient, string>();
            Commands = new string[] {
                "help",
                "list",
                "clear",
                "msg",
                "time",
                "leave",
                "memories"
            };

            byte[] key = Encoding.UTF8.GetBytes("M5NHKQHT");
            Server.SetupEncryption(MasterCrypt.Encrypt, MasterCrypt.Decrypt, key);
            Server.Disconnected += OnDisconnect;
            Server.GotString += OnDataRecieved;
        }

        public void Start()
        {
            Server.Start();
        }

        public void Stop()
        {
            Server.Send("The matrix is stopping...");
            foreach (TcpClient client in Nicknames.Keys)
                SaveMemory(client, null);
            Server.Stop();
        }

        private void OnDisconnect(TcpClient client)
        {
            if (Nicknames.ContainsKey(client))
            {
                SaveMemory(client, null);
                Server.Send(Nicknames[client] + " left the matrix!");
                Nicknames.Remove(client);
            }
        }

        private void OnDataRecieved(TcpClient sender, string data, byte flag)
        {
            if (data == null) return;
            Console.WriteLine("[" + sender.Client.Handle + ":" + flag + "]-->" + data);

            //Connection head
            if (flag == 0)
            {
                Register(sender, data);
            }
            //Data recieved
            else if (flag == 1)
            {
                if (!Nicknames.ContainsKey(sender))
                {
                    Server.Send(sender, "Register first!", 1);
                    Server.Kick(sender);
                    return;
                }

                //Parse command
                if (data.StartsWith("/"))
                {
                    string[] parts = data.Split(" ");
                    string command = parts[0].Replace("/", "");
                    string[] args = parts.TakeLast(parts.Length - 1).ToArray();

                    Evaluate(sender, command, args);
                }
                //Treat as message
                else
                {
                    string prefix = "[" + Nicknames[sender] + "]: ";
                    Server.Send(prefix + data);
                    SaveMemory(sender, data);
                }
            }
            //Autocompletion request
            else if (flag == 2)
            {
                string[] parts = data.Split(" ");
                string request = parts[0].Replace("/", "").ToLower();
                bool isCommand = parts[0].StartsWith("/");
                int interation = 0;
                if (!int.TryParse(parts[1], NumberStyles.Integer, null, out interation))
                {
                    Server.Send(sender, "Wrong format!", 1);
                    return;
                }

                string[] results;
                if (isCommand)
                {
                    results = Commands.Where(x => x.ToLower().StartsWith(request))
                        .ToArray();
                }
                else
                {
                    results = Nicknames.Values.Where(x => x.ToLower().StartsWith(request))
                        .ToArray();
                }
                if (results.Length <= 0) return;
                int index = interation % results.Length;
                string response = (isCommand ? "/" : "") + results[index];

                Server.Send(sender, response, 2);
            }
        }

        private void Evaluate(TcpClient client, string command, string[] args)
        {
            switch (command)
            {
                case "help":
                    Server.Send(client, "Your pills: " + string.Join(", ", Commands));
                    break;
                case "ls":
                case "list":
                    Server.Send(client, "People in the matrix: " + string.Join(", ", Nicknames.Values));
                    break;
                case "w":
                case "msg":
                    if (args.Length <= 0)
                    {
                        Server.Send(client, "You shouted into the void!", 1);
                        break;
                    }
                    string to = args[0].ToLower();
                    string message = String.Join(" ", args.TakeLast(args.Length - 1));
                    KeyValuePair<TcpClient, string> reciever = Nicknames
                        .FirstOrDefault(x => x.Value.ToLower() == to);

                    if (reciever.Equals(default))
                    {
                        Server.Send(client, "No such name!", 1);
                        break;
                    }

                    string prefix = "[" + Nicknames[client] + "->" + reciever.Value + "]: ";
                    Server.Send(client, prefix + message);
                    if (reciever.Key != client)
                        Server.Send(reciever.Key, prefix + message);
                    break;
                case "history":
                case "memories":
                    string memories = "  " + GetMemories(client).Replace("\n", "\n  ");
                    memories = memories.TrimEnd(" \n\r".ToCharArray());
                    Server.Send(client, "Things you remember:\n" + memories);
                    break;
                default:
                    Server.Send(client, "Sorry, there is no such pill!", 1);
                    break;
            }
        }

        private void Register(TcpClient client, string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                Server.Send(client, "Empty name!", 1);
                Server.Kick(client);
                return;
            }
            if (Nicknames.Values.Any(x => x.ToLower() == name.ToLower()))
            {
                Server.Send(client, "Taken name!", 1);
                Server.Kick(client);
                return;
            }
            if (!Regex.IsMatch(name, "^[a-zA-Z0-9_-]+$"))
            {
                Server.Send(client, "Invalid name!", 1);
                Server.Kick(client);
                return;
            }

            if (Nicknames.ContainsKey(client))
            {
                string oldName = Nicknames[client];
                Nicknames[client] = name;
                Server.Send(oldName + " changed his nickname to \"" + name + "\".");
            }
            else
            {
                Nicknames.Add(client, name);
                Server.Send(name + " entered the matrix!");
            }

        }

        private void SaveMemory(TcpClient client, string memory)
        {
            string name = Nicknames[client];
            if (!Directory.Exists("memories"))
                Directory.CreateDirectory("memories");

            string path = String.Format("./memories/{0}", name);
            if (memory == null)
            {
                File.Delete(path);
                return;
            }
            using (StreamWriter writer = new StreamWriter(path, true))
                writer.WriteLine(memory);
        }

        private string GetMemories(TcpClient client)
        {
            string name = Nicknames[client];
            string path = String.Format("./memories/{0}", name);
            if (!File.Exists(path)) return null;

            using (StreamReader reader = new StreamReader(path, true))
                return reader.ReadToEnd();
        }
    }
}