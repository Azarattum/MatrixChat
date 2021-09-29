using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System;

class AwesomeClient
{
    //Events
    public delegate void BytesGetDelegate(byte[] data, byte flag);
    public event BytesGetDelegate GotBytes;
    public delegate void StringGetDelegate(string data, byte flag);
    public event StringGetDelegate GotString;
    public delegate void ServerClosedDelegate();
    public event ServerClosedDelegate ServerClosed;
    public delegate void ExceptionCatchedDelegate(Exception exception);
    public event ExceptionCatchedDelegate ExceptionCatched;

    //Status
    private bool IsConnected = false;
    private int Fine = 0;
    private const int CriticalFine = 10;
    public bool Connected { get { return IsConnected; } }

    //Crypting stuff
    public delegate byte[] CryptingDelegate(byte[] data, byte[] key);
    private CryptingDelegate Encrypt;
    private CryptingDelegate Decrypt;
    private byte[] Key;

    //Define client
    private string Ip;
    private int Port;
    private TcpClient Client;

    //Public Tools
    public AwesomeClient(string ip, int port)
    {
        Ip = ip;
        Port = port;
    }

    public bool Connect(int timeout = 4000)
    {
        if (IsConnected)
            return IsConnected;
        try
        {
            Client = new TcpClient(); //Create client
            IsConnected = Client.ConnectAsync(Ip, Port).Wait(timeout);
            if (!IsConnected)
            {
                Client.Close();
                return false;
            }
            //Start server reading thread
            Thread reader = new Thread(ReadServer);
            reader.Name = "ServerReader";
            reader.Start();
            //Start server checking thread
            Thread checker = new Thread(CheckServer);
            checker.Name = "ServerChecker";
            checker.Priority = ThreadPriority.BelowNormal;
            checker.Start();
            return IsConnected;
        }
        catch (Exception e)
        {
            Client.Close();
            ExceptionCatched?.Invoke(e);
            return false; //False if can't connect
        }
    }

    public void Disconnect()
    {
        CloseConnection(true);
    }

    public void SetupEncryption(CryptingDelegate encrypt, CryptingDelegate decrypt, byte[] key)
    {
        Encrypt = encrypt;
        Decrypt = decrypt;
        Key = key;
    }

    public void Send(byte[] data, byte flag = 0)
    {
        if (IsConnected && Client != null && Client.Connected)
        {
            try
            {
                BinaryWriter sender = new BinaryWriter(Client.GetStream());
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

    public void Send(string data, byte flag = 0)
    {
        Send(Encoding.UTF8.GetBytes(data), flag);
    }

    //Private Stuff
    private void ReadServer()
    {
        do
        {
            //Reading
            try
            {
                if (IsConnected && Client.Available >= 6)
                {
                    //Create binary reader for clint's stream
                    BinaryReader serverReader = new BinaryReader(Client.GetStream()); //Define reader
                    //Check for start byte
                    if (serverReader.ReadByte() != 0)
                    {
                        //Reset buffer
                        serverReader.ReadBytes(Client.Available);
                        continue;
                    }

                    //Read flag
                    byte flag = serverReader.ReadByte();
                    //Read size
                    int size = serverReader.ReadInt32();
                    if (size == -1)
                    {
                        CloseConnection(false);
                        ServerClosed?.Invoke();
                        continue;
                    }
                    //Read data
                    byte[] data = ReadBytes(serverReader, Client, size, 500);

                    //Invoke callbacks
                    if (Decrypt != null && data != null && (GotBytes != null || GotString != null))
                        data = Decrypt(data, Key);
                    GotBytes?.Invoke(data, flag);
                    GotString?.Invoke(Encoding.UTF8.GetString(data), flag);
                }
            }
            catch (IOException e) { ExceptionCatched?.Invoke(e); }
            catch (ObjectDisposedException e)
            {
                IsConnected = false;
                ExceptionCatched?.Invoke(e);
            }
            catch (OutOfMemoryException e)
            {
                new BinaryReader(Client.GetStream()).ReadBytes(Client.Available);
                ExceptionCatched?.Invoke(e);
            }

            Thread.Sleep(10);
        } while (IsConnected);
    }

    private void CheckServer()
    {
        do
        {
            //Checking fine
            if (Fine >= CriticalFine)
            {
                CloseConnection(false);
                ServerClosed?.Invoke();
                return;
            }
            else if (IsConnected)
                UpdateFine();
            Thread.Sleep(20);
        } while (IsConnected);
    }

    private void CloseConnection(bool useSignal = true)
    {
        if (!IsConnected)
            return;
        IsConnected = false;
        Fine = 0;
        //Disconnection signal
        if (useSignal)
            SendDisconnectionSignal();
        //Closing
        if (Client != null)
        {
            Client.Close();
            Client = null;
        }
    }

    private void SendDisconnectionSignal()
    {
        if (Client != null && Client.Connected)
        {
            try
            {
                BinaryWriter sender = new BinaryWriter(Client.GetStream());
                sender.Write((byte)0);
                sender.Write((byte)0);
                sender.Write(-1);
            }
            catch (IOException e) { ExceptionCatched?.Invoke(e); }
            catch (ObjectDisposedException e) { ExceptionCatched?.Invoke(e); }
        }
    }

    private void UpdateFine()
    {
        try
        {
            if (IsConnected && Client.Available == 0 && Client.Client.Poll(1000, SelectMode.SelectRead))
                Fine++;
            else
                Fine = 0;
        }
        catch (NullReferenceException e) { IsConnected = false; ExceptionCatched?.Invoke(e); }
        catch (ObjectDisposedException e) { ExceptionCatched?.Invoke(e); }
        catch (SocketException e) { ExceptionCatched?.Invoke(e); }
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

