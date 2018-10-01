using System;
using System.Net.Sockets;

namespace KazNet
{
    public class PacketData
    {        
        public Socket socket;
        public byte[] packet;
        public PacketData(Socket _socket, byte[] _packet)
        {
            socket = _socket;
            packet = _packet;
        }
    }
}
