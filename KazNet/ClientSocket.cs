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
    public class ClientSocket
    {
        Socket socket;
        string ipAddress;
        int port;
        int bufferSize;
        byte[] buffer;
        bool isRunning = false;

        Thread clientThread;

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

        public Socket GetSocket()
        {
            return socket;
        }
        public bool IsRunning()
        {
            return isRunning;
        }

        public ClientSocket(string _ipAddress, int _port, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024)
        {
            ipAddress = _ipAddress;
            port = _port;
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            buffer = new byte[_bufferSize];
        }
        public ClientSocket(string _ipAddressPort, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024)
        {
            ipAddress = _ipAddressPort.Split(new char[] { ':' })[0];
            port = int.Parse(_ipAddressPort.Split(new char[] { ':' })[1]);
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            buffer = new byte[_bufferSize];
        }
        public void ChangeIpAddress(string _ipAddress, int _port)
        {
            ipAddress = _ipAddress;
            port = _port;
        }
        public void ChangeIpAddress(string _ipAddressPort)
        {
            ipAddress = _ipAddressPort.Split(new char[] { ':' })[0];
            port = int.Parse(_ipAddressPort.Split(new char[] { ':' })[1]);
        }

        public void Start(StatusMethod _statusMethod = null)
        {
            if (!isRunning)
            {
                isRunning = true;
                statusMethod = _statusMethod;
                StartClientThread();
            }
            else
            {
                Console.WriteLine("ClientNet: Server already running");
                _statusMethod?.Invoke(false);
            }
        }
        public void Stop()
        {
            StopClientThread();
        }
        #region Threads
        void StartClientThread()
        {
            clientThread = new Thread(Connect);
            clientThread.Start();
        }
        void StopClientThread()
        {
            isRunning = false;
            if (clientThread != null)
            {
                clientThread.Join();
                clientThread = null;
                Console.WriteLine("ClientNet: Stop client thread");
            }
            StopQueueThread();
            Disconnect();
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
                Console.WriteLine("ClientNet: Stop queue thread");
            }
            packetQueue.Clear();
        }
        #endregion
        #region ClientHandler
        void Connect()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ipAddress), port), new AsyncCallback(AcceptConnection), socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientNet: 1. " + ex.Message);
                StopClientThread();
                statusMethod?.Invoke(false);
            }
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            try
            {
                Socket serverSocket = (Socket)_asyncResult.AsyncState;
                serverSocket.EndConnect(_asyncResult);
                statusMethod?.Invoke(true);
                connectMethod?.Invoke(serverSocket);
                StartQueueThread();
                serverSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), serverSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientNet: 2. " + ex.Message);
                StopClientThread();
                statusMethod?.Invoke(false);
            }
        }
        void ReceivePacket(IAsyncResult _asyncResult)
        {
            Socket serverSocket = (Socket)_asyncResult.AsyncState;
            try
            {
                int packetSize = serverSocket.EndReceive(_asyncResult);
                if (packetSize > 0)
                {
                    byte[] packet = new byte[packetSize];
                    Array.Copy(buffer, packet, packetSize);
                    do
                    {
                        int lenght = BitConverter.ToInt16(packet, 0);
                        byte[] packetTemp = new byte[lenght];
                        Array.Copy(packet, 2, packetTemp, 0, lenght);
                        AddPacket(new PacketData(serverSocket, packetTemp));
                        lenght += 2;
                        packetTemp = new byte[packet.Length - lenght];
                        Array.Copy(packet, lenght, packetTemp, 0, packet.Length - lenght);
                        packet = packetTemp;
                    }
                    while (packet.Length > 0);
                    serverSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), serverSocket);
                }
                else
                {
                    StopClientThread();
                    disconnectMethod?.Invoke(socket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientNet: 3. " + ex.Message);
                StopClientThread();
                disconnectMethod?.Invoke(socket);
            }
        }
        public void SendPacket(List<byte> _packet)
        {
            SendPacket(_packet.ToArray());
        }
        public void SendPacket(byte[] _packet)
        {
            List<byte> packet = new List<byte>();
            packet.AddRange(Packet.Encode((short)_packet.Length));
            packet.AddRange(_packet);
            socket.BeginSend(packet.ToArray(), 0, packet.Count, SocketFlags.None, new AsyncCallback(SendPacketComplete), socket);
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
                Console.WriteLine("ClientNet: 4. " + ex.Message);
            }
        }
        public void Disconnect()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
                Console.WriteLine("ClientNet: Close socket");
            }
        }
        #endregion
        #region QueueHandler
        void StartQueue()
        {
            while (isRunning)
                if (packetQueue.Count > 0)
                    decodePacketMethod?.Invoke(packetQueue.Dequeue());
                else
                    packetQueueCheck.WaitOne();
        }
        void AddPacket(PacketData _packetData)
        {
            packetQueue.Enqueue(_packetData);
            packetQueueCheck.Set();
        }
        #endregion
    }
}
