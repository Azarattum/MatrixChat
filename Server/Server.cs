using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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
                "time"
            };

            Server.Disconnected += OnDisconnect;
            Server.GettedString += OnDataRecieved;
        }

        public void Start() {
            Server.Start();
        }

        private void OnDisconnect(TcpClient client) {
            Server.Send(Nicknames[client] + " disconnected from the server!");
            Nicknames.Remove(client);
        }

        private void OnDataRecieved(TcpClient sender, string data, byte flag) {
            if (data == null) return;

            //Connection head
            if (flag == 0) {
                Register(sender, data);
            }
            //Data recieved
            else if (flag == 1) {
                if (!Nicknames.ContainsKey(sender)) {
                    Server.Send(sender, "Register first!", 1);
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
                int index = interation % results.Length;
                string response = (isCommand? "/": "") + results[index];

                Server.Send(sender, response, 2);
            }
        }

        private void Evaluate(TcpClient client, string command, string[] args) {
            switch (command) {
                case "help":
                    Server.Send(client, "Commands: " + string.Join(", ", Commands));
                    break;
                case "list":
                    Server.Send(client, "Users online: " + string.Join(", ", Nicknames.Values));
                    break;
                case "msg":
                    string to = args[0].ToLower();
                    string message = String.Join(" ", args.TakeLast(args.Length - 1));

                    foreach (TcpClient user in Nicknames.Keys) {
                        byte flag = 3;
                        string name = Nicknames[user];
                        string prefix = "[" + Nicknames[client] + "->" + name + "]: ";
                        if (name.ToLower() == to) flag = 0;

                        Server.Send(user, prefix + message, flag);
                    }
                    break;
                default:
                    Server.Send(client, "Command not found!", 1);
                    break;
            }
        }

        private void Register(TcpClient client, string name) {
            if (String.IsNullOrEmpty(name)) {
                Server.Send(client, "Empty name!", 1);
                return;
            }
            if (Nicknames.Values.Any(x => x.ToLower() == name.ToLower())) {
                Server.Send(client, "Taken name!", 1);
                return;
            }
            if (!Regex.IsMatch(name, "[a-zA-Z0-9_-]+")) {
                Server.Send(client, "Invalid name!", 1);
                return;
            }

            if (Nicknames.ContainsKey(client)) {
                string oldName = Nicknames[client];
                Nicknames[client] = name;
                Server.Send(oldName + " changed his nickname to \"" + name + "\".");
            } else {
                Nicknames.Add(client, name);
                Server.Send(name + " connected to the server!");
            }

        }
    }
}