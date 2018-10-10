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
    public class ClientNet
    {
        bool isRunning = false;

        Socket socket;
        string ipAddress;
        int port;
        int bufferSize;
        byte[] buffer;

        Thread clientThread;
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

        public Socket GetSocket()
        {
            return socket;
        }
        public bool IsRunning()
        {
            return isRunning;
        }

        public ClientNet(string _ipAddress, int _port, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024)
        {
            ipAddress = _ipAddress;
            port = _port;
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            buffer = new byte[_bufferSize];
        }
        public ClientNet(string _ipAddressPort, ConnectMethod _connect = null, DisconnectMethod _disconnect = null, DecodePacketMethod _decodePacket = null, int _bufferSize = 1024)
        {
            ipAddress = _ipAddressPort.Split(new char[] { ':' })[0];
            port = int.Parse(_ipAddressPort.Split(new char[] { ':' })[1]);
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodePacketMethod = _decodePacket;
            bufferSize = _bufferSize;
            buffer = new byte[_bufferSize];
        }
        
        public void Start(StatusMethod _statusMethod = null)
        {
            if (!isRunning)
            {
                isRunning = true;
                statusMethod = _statusMethod;
                clientThread = new Thread(Connect);
                clientThread.Start();
                queueThread = new Thread(StartQueue);
                queueThread.Start();
            }
            else
            {
                Console.WriteLine("ClientNet: Server already running");
            }
        }
        public void Stop()
        {
            isRunning = false;
            if (clientThread != null)
            {
                clientThread.Join();
                clientThread = null;
                //clientThread.Abort();
                Console.WriteLine("ClientNet: Stop client thread");
            }
            packetQueueCheck.Set();
            if (queueThread != null)
            {
                queueThread.Join();
                queueThread = null;
                //queueThread.Abort();
                Console.WriteLine("ClientNet: Stop queue thread");
            }
            if (socket != null)
            {
                socket.Close();
                socket = null;
                Console.WriteLine("ClientNet: Close socket");
            }
            packetQueue.Clear();
        }

        void Connect()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ipAddress), port), new AsyncCallback(AcceptConnection), socket);
            }
            catch (Exception ex)
            {
                statusMethod?.Invoke(false);
                Console.WriteLine("ClientNet: 1. " + ex.ToString());
                Stop();
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
                serverSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), serverSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientNet: 2. " + ex.ToString());
                statusMethod?.Invoke(false);
                Stop();
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
                    //Console.WriteLine("ClientNet: Server " + socket.RemoteEndPoint + " disconnect");
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClientNet: 3. " + ex.ToString());
                Disconnect();
                Stop();
            }
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
                Console.WriteLine("ClientNet: 4. " + ex.ToString());
                Stop();
            }
        }
        public void Disconnect()
        {
            disconnectMethod?.Invoke(socket);
            if(socket != null){
                socket.Close();
            }
        }
        void AddPacket(PacketData _packetData)
        {
            packetQueue.Enqueue(_packetData);
            packetQueueCheck.Set();
        }
        void StartQueue()
        {
            while (isRunning)
                if (packetQueue.Count > 0)
                    decodePacketMethod?.Invoke(packetQueue.Dequeue());
                else
                    packetQueueCheck.WaitOne();
        }
    }
}
