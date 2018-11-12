using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KazNet
{
    public abstract class ClientNetwork
    {
        ClientSocket clientSocket;

        public ClientNetwork(string _serverIP)
        {
            clientSocket = new ClientSocket(_serverIP, Connection, Disconnection, Decode);
        }

        public void Start()
        {
            clientSocket.Start(Status);
        }
        public void Stop()
        {
            clientSocket.Stop();
        }

        public void SendPacket(List<byte> _packet)
        {
            SendPacket(_packet.ToArray());
        }
        public void SendPacket(byte[] _packet)
        {
            clientSocket.SendPacket(_packet);
        }
        public void DisconnectSocket()
        {
            clientSocket.Disconnect();
        }

        public abstract void Status(bool _status);
        public abstract void Connection(Socket _socket);
        public abstract void Disconnection(Socket _socket);
        public abstract void Decode(PacketData _packetData);
    }
}
