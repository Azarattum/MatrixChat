using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

class AwesomeServer
{
    //Events
    public delegate void ConnectionDelegate(TcpClient connectedClient); //Connection event
    public event ConnectionDelegate Connected; 
    public event ConnectionDelegate Disconnected; //Disconnection event
    public delegate void BytesGetDelegate(TcpClient sender, byte[] data, byte flag); //Bytes get event
    public event BytesGetDelegate GettedBytes;
    public delegate void StringGetDelegate(TcpClient sender, string data, byte flag); //String get event
    public event StringGetDelegate GettedString;
    public delegate void ExceptionCatchedDelegate(Exception exception); //Exception catched event
    public event ExceptionCatchedDelegate ExceptionCatched;

    //Main server's listener
    private TcpListener Listener;
    private List<TcpClient> _Clients = new List<TcpClient>();
    private Dictionary<TcpClient, int> Fines = new Dictionary<TcpClient, int>();
    private const int CriticalFine = 10;
    public TcpClient[] Clients { get { return _Clients.ToArray(); } } //Readonly public clients

    //Status
    private bool IsRunning = false;
    private int ReadingThreadsCount = 1;
    public bool Listening { get { return IsRunning; } } //Readonly listening property

    //Crypting stuff
    public delegate byte[] CryptingDelegate(byte[] data, byte[] key);
    private CryptingDelegate Encrypt;
    private CryptingDelegate Decrypt;
    private byte[] Key;

    //Toggling stuff
    public AwesomeServer(int port)
    {
        //Initialize listener
        Listener = new TcpListener(IPAddress.Any,port);
        ReadingThreadsCount = Environment.ProcessorCount;
    }

    public void Start()
    {
        if (IsRunning)
            return;
        IsRunning = true;
        Listener.Start(); //Start server
        Listener.BeginAcceptTcpClient(AcceptClient, null); //Starting listening for clients
    }

    public void Stop()
    {
        IsRunning = false;
        if (Listener != null) //Stopping listener
            Listener.Stop();
        //Disconnect all clients
        while(_Clients.Count > 0)
        { 
            DisconnectClient(_Clients[0], true);
        }
        Fines.Clear();
    }

    //Encryption
    public void SetupEncryption(CryptingDelegate encrypt, CryptingDelegate decrypt, byte[] key)
    {
        Encrypt = encrypt;
        Decrypt = decrypt;
        Key = key;
    }

    //Sending
    public void Send(TcpClient to, byte[] data, byte flag = 0)
    {
        if (IsRunning && to != null && to.Connected)
        {
            try
            {
                BinaryWriter sender = new BinaryWriter(to.GetStream());
                //Checking if encryption is enabled
                if (Encrypt != null && data != null)
                    data = Encrypt.Invoke(data, Key);
                sender.Write((byte)0);
                sender.Write(flag);
                sender.Write((data != null) ? data.Length : 0);
                if (data != null)
                    sender.Write(data, 0, data.Length);
            }
            catch (IOException e) { ExceptionCatched?.Invoke(e); }
            catch (ObjectDisposedException e) { ExceptionCatched?.Invoke(e); }
        }
    }

    public void Send(TcpClient to, string data, byte flag = 0)
    {
        Send(to, Encoding.UTF8.GetBytes(data), flag);
    }

    public void Send(byte[] data, byte flag = 0)
    {
        for (int i = 0; i < _Clients.Count; i++)
            Send(_Clients[i], data, flag);
    }

    public void Send(string data, byte flag = 0)
    {
        for (int i = 0; i < _Clients.Count; i++)
            Send(_Clients[i], Encoding.UTF8.GetBytes(data), flag);
    }

    //Administration

    public void Kick(TcpClient client)
    {
        DisconnectClient(client, true);
    }

    //Private methods
    private void AcceptClient(IAsyncResult ar)
    {
        try
        {
            TcpClient client = Listener.EndAcceptTcpClient(ar);
            //Register client
            Fines.Add(client, 0);
            _Clients.Add(client);
            //Connection callback
            Connected?.Invoke(client);
            //Start reading and checking threads
            if (_Clients.Count <= ReadingThreadsCount)
                StartHandlers(_Clients.Count-1);
            Thread.Sleep(5);
            //Keep listening
            if (IsRunning)
                Listener.BeginAcceptTcpClient(AcceptClient, null);
        }
        catch (SocketException e)
        {
            //Keep listening
            if (IsRunning)
                Listener.BeginAcceptTcpClient(AcceptClient, null);
            ExceptionCatched?.Invoke(e);
        }
        catch (ObjectDisposedException e)
        {
            ExceptionCatched?.Invoke(e);
        }
        catch (InvalidOperationException e)
        {
            if (IsRunning) throw;
            else ExceptionCatched?.Invoke(e);
        }
    }

    private void StartHandlers(int threadNumber)
    {
        //Data reader
        Thread reader = new Thread(() => { ReadClients(threadNumber, ReadingThreadsCount); });
        reader.Name = "ClientsReader" + threadNumber;
        reader.Start();
        //Exit if it's not the first iteration
        if (threadNumber > 0)
            return;
        //Connection checker
        Thread checker = new Thread(CheckClients);
        checker.Name = "ClientsChecker";
        checker.Priority = ThreadPriority.BelowNormal;
        checker.Start();
    }

    private void CheckClients()
    {
        do
        {
            //Retrieve client from parameter passed to thread
            for (int i = 0; i < _Clients.Count; i++)
            {
                try
                {
                    if (_Clients.Count <= i || _Clients[i] == null || !IsRunning)
                        continue;

                    TcpClient client = _Clients[i];
                    UpdateFine(client);

                    try
                    {
                        //Scnanning client for fines
                        while (client != null && Fines.ContainsKey(client) && Fines[client] > 0 && IsRunning)
                        {
                            if (Fines[client] >= CriticalFine && IsRunning)
                                DisconnectClient(client, false); //Disconnecting client
                            else
                                UpdateFine(client);
                            Thread.Sleep(1);
                        }
                    }
                    catch (KeyNotFoundException e) { ExceptionCatched?.Invoke(e); }
                }
                //For preventing thread-unsafe collections causing exceptions
                catch (ArgumentOutOfRangeException e) { ExceptionCatched?.Invoke(e); }
            }
            Thread.Sleep(3);
        } while (IsRunning && _Clients.Count > 0);
    }

    private void ReadClients(int threadNumber, int threadsLength)
    {
        do
        {
            for (int i = threadNumber; i < _Clients.Count; i += threadsLength) 
            {
                try
                {
                    //Check existance
                    if (i < 0 || _Clients.Count <= i || _Clients[i] == null)
                        continue;

                    //Define client
                    TcpClient client = _Clients[i];

                    //Check availability
                    if (client.Available >= 6)
                    {
                        try
                        {
                            //Reset fine
                            Fines[client] = 0;
                            //Create binary reader for clint's stream
                            BinaryReader clientReader = new BinaryReader(client.GetStream());
                            //Check for start byte
                            if (clientReader.ReadByte() != 0)
                            {
                                //Reset buffer
                                clientReader.ReadBytes(client.Available);
                                continue;
                            }

                            //Read flag
                            byte flag = clientReader.ReadByte();
                            //Read size
                            int size = clientReader.ReadInt32();
                            if (size == -1)
                            {
                                DisconnectClient(client, false);
                                continue;
                            }
                            //Read data
                            byte[] data = ReadBytes(clientReader,client, size, 500);

                            //Invoke callbacks
                            if (Decrypt != null && data != null && (GettedBytes != null || GettedString != null))
                                data = Decrypt(data, Key);
                            GettedBytes?.Invoke(client, data, flag);
                            GettedString?.Invoke(client, Encoding.UTF8.GetString(data), flag);
                        }
                        catch (IOException e) { ExceptionCatched?.Invoke(e); }
                        catch (OutOfMemoryException e)
                        {
                            new BinaryReader(client.GetStream()).ReadBytes(client.Available);
                            ExceptionCatched?.Invoke(e);
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            new BinaryReader(client.GetStream()).ReadBytes(client.Available);
                            ExceptionCatched?.Invoke(e);
                        }
                    }
                }
                //For preventing thread-unsafe collections causing exceptions
                catch (ArgumentOutOfRangeException e) { ExceptionCatched?.Invoke(e); } 
            }
            Thread.Sleep(1);
        } while (IsRunning && _Clients.Count > threadNumber);
    }

    private void DisconnectClient(TcpClient client, bool useSignal = true)
    {
        //Check client existance
        if (client == null)
            return;

        //Check in clients list
        if (_Clients.Contains(client))
        {
            _Clients.Remove(client);
            Disconnected?.Invoke(client);
        }

        //Check in fines dictionary
        if (Fines.ContainsKey(client))
            Fines.Remove(client);

        //Close connection
        if (client != null)
        {
            if (useSignal)
                SendDisconnectionSignal(client);
            client.Close();
            client = null;
        }
    }

    private void SendDisconnectionSignal(TcpClient client)
    {
        if (client != null && client.Connected)
        {
            try
            {
                BinaryWriter sender = new BinaryWriter(client.GetStream());
                sender.Write((byte)0);
                sender.Write((byte)0);
                sender.Write(-1);
            }
            catch (IOException e) { ExceptionCatched?.Invoke(e); }
            catch (ObjectDisposedException e) { ExceptionCatched?.Invoke(e); }
        }
    }

    private void UpdateFine(TcpClient client)
    {
        if (!Fines.ContainsKey(client))
            return;
        try
        {
            if (client.Available == 0 && client.Client.Poll(1000, SelectMode.SelectRead))
                Fines[client]++;
            else
                Fines[client] = 0;
        }
        catch (SocketException e) { ExceptionCatched?.Invoke(e); }
        catch (KeyNotFoundException e) { ExceptionCatched?.Invoke(e); }
        catch (ObjectDisposedException e) { ExceptionCatched?.Invoke(e); }
    }

    private byte[] ReadBytes(BinaryReader reader, TcpClient client, long count, int timeout = 500)
    {
        //Define
        byte[] bytes = new byte[count];
        long readed = 0;
        DateTime dataUnavailable = new DateTime(0);
        //Reading
        while (readed < count)
        {
            if (client.Available > 0)
            {
                //Define count bytes to read
                long readCount = client.Available;
                //If count > data
                if (readCount + readed > count)
                    readCount = count - readed;
                //Read all available bytes
                byte[] buffer = reader.ReadBytes((int)readCount);
                //Copy to destination
                buffer.CopyTo(bytes, readed);
                readed += buffer.Length;
                //Reset unavailable time
                dataUnavailable = new DateTime(0);
            }
            else
            {
                //If count < data
                if (dataUnavailable.Ticks == 0)
                    dataUnavailable = DateTime.Now;
                //Check timeout
                else if ((DateTime.Now - dataUnavailable).CompareTo(new TimeSpan(timeout * 10000)) == 1)
                {
                    ExceptionCatched?.Invoke(new Exception("Reading bytes was stoped by timeout."));
                    break;
                }
            }
        }
        //Return result
        return bytes;
    }
}
