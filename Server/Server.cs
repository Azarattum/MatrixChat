using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Server {
    public class ServerManager {
        private AwesomeServer Server;
        private Dictionary<TcpClient, string> Nicknames;
        private string[] Commands;

        public ServerManager(int port) {
            Server = new AwesomeServer(port);
            Nicknames = new Dictionary<TcpClient, string>();
            Commands = new string[] {
                "help",
                "list",
                "clear",
                "msg",
                "post",
                "time",
                "leave"
            };

            Server.Disconnected += OnDisconnect;
            Server.GettedString += OnDataRecieved;
        }

        public void Start() {
            Server.Start();
        }

        private void OnDisconnect(TcpClient client) {
            if (Nicknames.ContainsKey(client)) {
                Server.Send(Nicknames[client] + " leaved the fortress!");
                Nicknames.Remove(client);
            }
        }

        private void OnDataRecieved(TcpClient sender, string data, byte flag) {
            if (data == null) return;
            Console.WriteLine("[" + sender.Client.Handle + ":" + flag + "]-->" + data);

            //Connection head
            if (flag == 0) {
                Register(sender, data);
            }
            //Data recieved
            else if (flag == 1) {
                if (!Nicknames.ContainsKey(sender)) {
                    Server.Send(sender, "Register first!", 1);
                    Server.Kick(sender);
                    return;
                }

                //Parse command
                if (data.StartsWith("/")) {
                    string[] parts = data.Split(" ");
                    string command = parts[0].Replace("/", "");
                    string[] args = parts.TakeLast(parts.Length - 1).ToArray();

                    Evaluate(sender, command, args);
                }
                //Treat as message
                else {
                    string prefix = "[" + Nicknames[sender] + "]: ";
                    Server.Send(prefix + data);
                }
            }
            //Autocompletion request
            else if (flag == 2) {
                string[] parts = data.Split(" ");
                string request = parts[0].Replace("/", "").ToLower();
                bool isCommand = parts[0].StartsWith("/");
                int interation = 0;
                if (!int.TryParse(parts[1], NumberStyles.Integer, null, out interation)) {
                    Server.Send(sender, "Wrong format!", 1);
                    return;
                }

                string[] results;
                if (isCommand) {
                    results = Commands.Where(x => x.ToLower().StartsWith(request))
                        .ToArray();
                } else {
                    results = Nicknames.Values.Where(x => x.ToLower().StartsWith(request))
                        .ToArray();
                }
                if (results.Length <= 0) return;
                int index = interation % results.Length;
                string response = (isCommand? "/": "") + results[index];

                Server.Send(sender, response, 2);
            }
        }

        private void Evaluate(TcpClient client, string command, string[] args) {
            switch (command) {
                case "help":
                    Server.Send(client, "Signs: " + string.Join(", ", Commands));
                    break;
                case "ls":
                case "list":
                    Server.Send(client, "Witchers in the room: " + string.Join(", ", Nicknames.Values));
                    break;
                case "w":
                case "msg":
                    if (args.Length <= 0) {
                        Server.Send(client, "You shouted into the void!", 1);
                        break;
                    }
                    string to = args[0].ToLower();
                    string message = String.Join(" ", args.TakeLast(args.Length - 1));
                    string name = Nicknames.Values.FirstOrDefault(x => x.ToLower() == to);
                    if (name == null) {
                        Server.Send(client, "No such witcher!", 1);
                        break;
                    }

                    foreach (TcpClient user in Nicknames.Keys) {
                        byte flag = 3;
                        string prefix = "[" + Nicknames[client] + "->" + name + "]: ";
                        if (Nicknames[user] == name || user == client) {
                            flag = 0;
                        }

                        Server.Send(user, prefix + message, flag);
                    }
                    break;
                case "post":
                    Random random = new Random();
                    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                    string key = new string(Enumerable.Repeat(chars, 10)
                        .Select(s => s[random.Next(s.Length)]).ToArray());

                    if (args.Length == 0) {
                        Server.Send(client, "Specify receivers (devided by ',')!", 1);
                        break;
                    }
                    string[] receivers = args[0].ToLower().Split(',');
                    string content = String.Join(" ", args.TakeLast(args.Length - 1));
                    if (String.IsNullOrEmpty(content)) {
                        Server.Send(client, "Specify content!", 1);
                        break;
                    }
                    string data = MasterCrypt.Encrypt(
                        string.Join(',', receivers) + " " + key + " " +
                        MasterCrypt.Encrypt(content, key, true), "p0St_k3Y42", true
                    );

                    foreach (TcpClient user in Nicknames.Keys) {
                        Server.Send(user, data, 4);
                    }
                    break;
                default:
                    Server.Send(client, "Sorry, you cannot cast that!", 1);
                    break;
            }
        }

        private void Register(TcpClient client, string name) {
            if (String.IsNullOrEmpty(name)) {
                Server.Send(client, "Empty name!", 1);
                Server.Kick(client);
                return;
            }
            if (Nicknames.Values.Any(x => x.ToLower() == name.ToLower())) {
                Server.Send(client, "Taken name!", 1);
                Server.Kick(client);
                return;
            }
            if (!Regex.IsMatch(name, "^[a-zA-Z0-9_-]+$")) {
                Server.Send(client, "Invalid name!", 1);
                Server.Kick(client);
                return;
            }

            if (Nicknames.ContainsKey(client)) {
                string oldName = Nicknames[client];
                Nicknames[client] = name;
                Server.Send(oldName + " changed his nickname to \"" + name + "\".");
            } else {
                Nicknames.Add(client, name);
                Server.Send(name + " entered the fortress!");
            }

        }
    }
}