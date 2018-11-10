using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KazNet
{
    public abstract class ServerNetwork
    {
        ServerSocket serverSocket;

        public ServerNetwork(string _serverIP)
        {
            serverSocket = new ServerSocket(_serverIP, Connection, Disconnection, Decode);
        }

        public void Start()
        {
            serverSocket.Start(Status);
        }
        public void Stop()
        {
            serverSocket.Stop();
        }

        public void SendPacket(Socket _socket, List<byte> _packet)
        {
            SendPacket(_socket, _packet.ToArray());
        }
        public void SendPacket(Socket _socket, byte[] _packet)
        {
            serverSocket.SendPacket(_socket, _packet);
        }
        public void DisconnectSocket(Socket _socket)
        {
            serverSocket.Disconnect(_socket);
        }

        public abstract void Status(bool _status);
        public abstract void Connection(Socket _socket);
        public abstract void Disconnection(Socket _socket);
        public abstract void Decode(PacketData _packetData);
    }
}
