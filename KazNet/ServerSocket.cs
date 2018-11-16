using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KazNet
{
    public class ServerSocket
    {
        Socket socket;
        int port;
        int bufferSize;
        int backLog;
        bool isRunning = false;
        bool isOpen = true;

        Thread serverThread;
        AutoResetEvent nextClient = new AutoResetEvent(false);
        Dictionary<Socket, Client> clients = new Dictionary<Socket, Client>();

        Thread queueThread;
        AutoResetEvent packetQueueCheck = new AutoResetEvent(false);
        Queue<PacketData> packetQueue = new Queue<PacketData>();

        public delegate void StatusMethod(bool _status);
        StatusMethod statusMethod;
        public delegate void ConnectMethod(Socket _socket);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(Socket _socket);
        DisconnectMethod disconnectMethod;
        public delegate void DecodePacketMethod(PacketData _packetData);
        DecodePacketMethod decodePacketMethod;

        public bool IsRunning()
        {
            return isRunning;
        }
        public bool IsOpen()
        {
            return isOpen;
        }
        public void SetIsOpen(bool _isOpen)
        {
            isOpen = _isOpen;
        }

        public ServerSocket(int _port, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024, int _backLog = 100)
        {
            port = _port;
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            backLog = _backLog;
        }
        public ServerSocket(string _ipAddressPort, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024, int _backLog = 100)
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
            if (!isRunning)
            {
                isRunning = true;
                statusMethod = _statusMethod;
                StartServerThread();
            }
            else
            {
                Console.WriteLine("ServerNet: Server already running");
                _statusMethod?.Invoke(false);
            }
        }
        public void Stop()
        {
            StopServerThread();
        }

        #region Threads
        void StartServerThread()
        {
            serverThread = new Thread(StartListener);
            serverThread.Start();
        }
        void StopServerThread()
        {
            isRunning = false;
            DisconnectAll();
            nextClient.Set();
            if (serverThread != null)
            {
                serverThread.Join();
                serverThread = null;
                Console.WriteLine("ServerNet: Stop server thread");
            }
            StopQueueThread();
            if (socket != null)
            {
                socket.Close();
                socket = null;
                Console.WriteLine("ServerNet: Close socket");
            }
        }
        void StartQueueThread()
        {
            queueThread = new Thread(StartQueue);
            queueThread.Start();
        }
        void StopQueueThread()
        {
            packetQueueCheck.Set();
            if (queueThread != null)
            {
                queueThread.Join();
                queueThread = null;
                Console.WriteLine("ServerNet: Stop queue thread");
            }
            packetQueue.Clear();
        }
        #endregion
        #region ServerHandler
        void StartListener()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.Listen(backLog);

                statusMethod?.Invoke(true);
                StartQueueThread();

                nextClient.Set();
                while (isRunning)
                {
                    socket.BeginAccept(new AsyncCallback(AcceptConnection), socket);
                    nextClient.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 1. " + ex.Message);
                StopServerThread();
                statusMethod?.Invoke(false);
            }
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            nextClient.Set();
            try
            {
                Socket clientSocket = (Socket)_asyncResult.AsyncState;
                clientSocket = clientSocket.EndAccept(_asyncResult);
                if(isOpen)
                {
                    Client client = new Client(clientSocket, bufferSize);
                    AddClient(client);
                    clientSocket.BeginReceive(client.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                }
                else
                {
                    Disconnect(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 2. " + ex.Message);
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
                    Disconnect(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 3. " + ex.Message);
                Disconnect(clientSocket);
            }
        }

        public void SendPacket(Socket _socket, List<byte> _packet)
        {
            SendPacket(_socket, _packet.ToArray());
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
            try
            {
                Socket socket = (Socket)_asyncResult.AsyncState;
                socket.EndSend(_asyncResult);
                //int packetSize = clientSocket.EndSend(_asyncResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerNet: 4. " + ex.Message);
            }
        }

        void AddClient(Client _client)
        {
            if (!clients.ContainsKey(_client.socket))
            {
                clients.Add(_client.socket, _client);
                connectMethod?.Invoke(_client.socket);
            }
        }
        void RemoveClient(Socket _socket)
        {
            if (clients.ContainsKey(_socket))
            {
                clients.Remove(_socket);
                disconnectMethod?.Invoke(_socket);
            }
        }
        public void Disconnect(Socket _socket)
        {
            RemoveClient(_socket);
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }
        void DisconnectAll(){
            List<Socket> sockets = new List<Socket>(clients.Keys);
            for(int i = 0; i < sockets.Count(); i++)
            {
                Disconnect(sockets[i]);
            }
            sockets = null;
        }
        #endregion
        #region QueueHandler
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
        void AddPacket(PacketData _packetData)
        {
            packetQueue.Enqueue(_packetData);
            packetQueueCheck.Set();
        }
        #endregion

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
