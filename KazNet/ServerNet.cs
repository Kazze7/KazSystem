using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KazNet
{    public class ServerNet
    {
        bool isRunning = false;
        bool isOpen = true;

        int port;
        int bufferSize;
        int backLog;
        AutoResetEvent nextClient = new AutoResetEvent(false);

        Thread serverThread;
        Thread queueThread;
        Queue<PacketData> packetQueue = new Queue<PacketData>();
        AutoResetEvent packetQueueCheck = new AutoResetEvent(false);
        public delegate void DecodePacketMethod(PacketData _packetData);
        DecodePacketMethod decodePacketMethod;
        public delegate void ConnectMethod(Socket _socket);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(Socket _socket);
        DisconnectMethod disconnectMethod;
        public delegate void StatusMethod(bool _status);
        StatusMethod statusMethod;

        Dictionary<Socket, Client> clients = new Dictionary<Socket, Client>();

        public bool IsRunning()
        {
            return isRunning;
        }
        public bool IsOpen(){
            return isOpen;
        }
        public void SetIsOpen(bool _isOpen){
            isOpen = _isOpen;
        }

        public ServerNet(int _port, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024, int _backLog = 100)
        {
            port = _port;
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            backLog = _backLog;
        }
        public ServerNet(string _ipAddressPort, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024, int _backLog = 100)
        {
            port = int.Parse(_ipAddressPort.Split(new char[] { ':' })[1]);
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            backLog = _backLog;
        }

        public void Start(StatusMethod _statusMethod = null)
        {
            statusMethod = null;
            if (!isRunning)
            {
                isRunning = true;
                statusMethod = _statusMethod;
                serverThread = new Thread(StartListener);
                serverThread.Start();
                queueThread = new Thread(StartQueue);
                queueThread.Start();
            }
            else
            {
                Console.WriteLine("ServerNet: Server already running");
            }
        }
        public void Stop()
        {
            isRunning = false;
            DisconnectAll();
            nextClient.Set();
            if (serverThread != null)
            {
                serverThread.Join();
                serverThread = null;
                //serverThread.Abort();
                Console.WriteLine("ServerNet: Stop server thread");
            }
            packetQueueCheck.Set();
            if (queueThread != null)
            {
                queueThread.Join();
                queueThread = null;
                //queueThread.Abort();
                Console.WriteLine("ServerNet: Stop queue thread");
            }
            packetQueue.Clear();
        }

        void StartListener()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.Listen(backLog);

                statusMethod?.Invoke(true);

                nextClient.Set();
                while (isRunning)
                {
                    socket.BeginAccept(new AsyncCallback(AcceptConnection), socket);
                    nextClient.WaitOne();
                }
            }
            catch (Exception ex)
            {
                statusMethod?.Invoke(false);
                Console.WriteLine("ServerNet: 1. " + ex.ToString());
                Stop();
            }
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            nextClient.Set();
            try
            {
                Socket socket = (Socket)_asyncResult.AsyncState;
                Socket clientSocket = socket.EndAccept(_asyncResult);
                if(isOpen)
                {
                    Client client = new Client(clientSocket, bufferSize);
                    if(!clients.ContainsKey(clientSocket))
                        clients.Add(clientSocket, client);
                    connectMethod?.Invoke(clientSocket);
                    clientSocket.BeginReceive(client.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                }
                else
                {
                    Disconnect(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 2. " + ex.ToString());
            }
        }
        void ReceivePacket(IAsyncResult _asyncResult)
        {
            Client client = (Client)_asyncResult.AsyncState;
            Socket clientSocket = client.socket;
            try
            {
                int packetSize = clientSocket.EndReceive(_asyncResult);
                if (packetSize > 0)
                {
                    byte[] packet = new byte[packetSize];
                    Array.Copy(client.buffer, packet, packetSize);
                    do
                    {
                        int lenght = BitConverter.ToInt16(packet, 0);
                        byte[] packetTemp = new byte[lenght];
                        Array.Copy(packet, 2, packetTemp, 0, lenght);
                        AddPacket(new PacketData(clientSocket, packetTemp));
                        lenght += 2;
                        packetTemp = new byte[packet.Length - lenght];
                        Array.Copy(packet, lenght, packetTemp, 0, packet.Length - lenght);
                        packet = packetTemp;
                    }
                    while (packet.Length > 0);
                    clientSocket.BeginReceive(client.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                }
                else
                {
                    //Console.WriteLine("ServerNet: Client " + clientSocket.RemoteEndPoint + " disconnect");
                    Disconnect(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 3. " + ex.ToString());
            }
        }
        public void SendPacket(Socket _socket, byte[] _packet)
        {
            List<byte> packet = new List<byte>();
            packet.AddRange(Packet.Encode((short)_packet.Length));
            packet.AddRange(_packet);
            _socket.BeginSend(packet.ToArray(), 0, packet.Count, SocketFlags.None, new AsyncCallback(SendPacketComplete), _socket);
        }
        void SendPacketComplete(IAsyncResult _asyncResult)
        {
            //Socket clientSocket;
            try
            {
                Socket clientSocket = (Socket)_asyncResult.AsyncState;
                clientSocket.EndSend(_asyncResult);
                //int packetSize = clientSocket.EndSend(_asyncResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 4. " + ex.ToString());
            }
        }
        public void Disconnect(Socket _socket)
        {
            disconnectMethod?.Invoke(_socket);
            if(clients.ContainsKey(_socket))
                clients.Remove(_socket);
            if(_socket != null)
                _socket.Close();
        }
        void DisconnectAll(){
            List<Socket> sockets = new List<Socket>(clients.Keys);
            for(int i = 0; i < sockets.Count(); i++)
            {
                Disconnect(sockets[i]);
            }
            sockets = null;
        }
        public void AddPacket(PacketData _packetData)
        {
            packetQueue.Enqueue(_packetData);
            packetQueueCheck.Set();
        }
        void StartQueue()
        {
            while (isRunning)
            {
                if (packetQueue.Count > 0)
                    decodePacketMethod?.Invoke(packetQueue.Dequeue());
                else
                    packetQueueCheck.WaitOne();
            }
        }

        class Client
        {
            public Socket socket;
            public byte[] buffer;

            public Client(Socket _socket, int _bufferSize)
            {
                socket = _socket;
                buffer = new byte[_bufferSize];
            }
        }
    }
}
