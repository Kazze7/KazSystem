using System;
using System.Collections.Generic;
using System.Text;

namespace KazNet
{
    public static class Packet
    {
        #region Encode

        public static byte Encode(bool _bool)
        {
            try
            {
                return Convert.ToByte(_bool);
            }
            catch
            {
                Console.WriteLine("Packet: Problem bool decode: " + _bool);
                return 0;
            }
        }
        public static byte[] Encode(string _string)
        {
            try
            {
                List<byte> packet = new List<byte>();
                packet.AddRange(BitConverter.GetBytes((short)_string.Length));
                packet.AddRange(Encoding.Unicode.GetBytes(_string));
                return packet.ToArray();
            }
            catch
            {
                Console.WriteLine("Packet: Problem string decode: " + _string);
                return new byte[0];
            }
        }
        public static byte[] Encode(int _int)
        {
            try
            {
                return BitConverter.GetBytes(_int);
            }
            catch
            {
                Console.WriteLine("Packet: Problem int decode: " + _int);
                return new byte[0];
            }
        }
        public static byte[] Encode(short _short)
        {
            try
            {
                return BitConverter.GetBytes(_short);
            }
            catch
            {
                Console.WriteLine("Packet: Problem short decode: " + _short);
                return new byte[0];
            }
        }
        public static byte[] Encode(float _float)
        {
            try
            {
                return BitConverter.GetBytes(_float);
            }
            catch
            {
                Console.WriteLine("Packet: Problem float decode: " + _float);
                return new byte[4];
            }
        }
        public static byte[] EncodeIpAddressV4(string _ipAddress, int _port)
        {
            try
            {
                List<byte> packet = new List<byte>();
                string[] ipAddress = _ipAddress.Split(new char[] { '.' });
                packet.Add(byte.Parse(ipAddress[0]));
                packet.Add(byte.Parse(ipAddress[1]));
                packet.Add(byte.Parse(ipAddress[2]));
                packet.Add(byte.Parse(ipAddress[3]));
                packet.AddRange(BitConverter.GetBytes((ushort)_port));
                return packet.ToArray();
            }
            catch
            {
                Console.WriteLine("Packet: Problem IP4 string + int decode: " + _ipAddress + ":" + _port);
                return new byte[0];
            }
        }
        public static byte[] EncodeIpAddressV4(string _ipAddressPort)
        {
            try
            {
                List<byte> packet = new List<byte>();
                string[] ipAddress = _ipAddressPort.Split(new char[] { '.' });
                packet.Add(byte.Parse(ipAddress[0]));
                packet.Add(byte.Parse(ipAddress[1]));
                packet.Add(byte.Parse(ipAddress[2]));
                packet.Add(byte.Parse(ipAddress[3].Split(new char[] { ':' })[0]));
                packet.AddRange(BitConverter.GetBytes(ushort.Parse(ipAddress[3].Split(new char[] { ':' })[1])));
                return packet.ToArray();
            }
            catch
            {
                Console.WriteLine("Packet: Problem IP4 string encode: " + _ipAddressPort);
                return new byte[0];
            }
        }
        #endregion

        #region Decode
        public static string DecodeString(byte[] _packet, ref int _index)
        {
            string text = string.Empty;
            try
            {
                int lenght = BitConverter.ToInt16(_packet, _index) * 2;
                _index += 2;
                text = Encoding.Unicode.GetString(_packet, _index, lenght);
                _index += lenght;
            }
            catch
            {
                Console.WriteLine("Packet: Problem string decode: " + BitConverter.ToString(_packet));
            }
            return text;
        }
        public static bool DecodeBool(byte[] _packet, ref int _index)
        {
            bool result = false;
            try
            {
                result = Convert.ToBoolean(_packet[_index]);
                _index++;
            }
            catch
            {
                Console.WriteLine("Packet: Problem bool decode: " + BitConverter.ToString(_packet));
            }
            return result;
        }
        public static int DecodeInt8(byte[] _packet, ref int _index)
        {
            int result = 0;
            try
            {
                result = Convert.ToInt32(_packet[_index]);
                _index++;
            }
            catch
            {
                Console.WriteLine("Packet: Problem int8 decode: " + BitConverter.ToString(_packet));
            }
            return result;
        }
        public static int DecodeInt16(byte[] _packet, ref int _index)
        {
            int result = 0;
            try
            {
                result = BitConverter.ToInt16(_packet, _index);
                _index += 2;
            }
            catch
            {
                Console.WriteLine("Packet: Problem int16 decode: " + BitConverter.ToString(_packet));
            }
            return result;
        }
        public static int DecodeInt32(byte[] _packet, ref int _index)
        {
            int result = 0;
            try
            {
                result = BitConverter.ToInt32(_packet, _index);
                _index += 4;
            }
            catch
            {
                Console.WriteLine("Packet: Problem int32 decode: " + BitConverter.ToString(_packet));
            }
            return result;
        }
        public static float DecodeFloat(byte[] _packet, ref int _index)
        {
            float result = 0.0f;
            try
            {
                result = BitConverter.ToSingle(_packet, _index);
                _index += 4;
            }
            catch
            {
                Console.WriteLine("Packet: Problem float decode: " + BitConverter.ToString(_packet));
            }
            return result;
        }
        public static string DecodeIpAddressV4(byte[] _packet, ref int _index)
        {
            string text = string.Empty;
            try
            {
                text += _packet[_index].ToString();
                text += ".";
                text += _packet[_index + 1].ToString();
                text += ".";
                text += _packet[_index + 2].ToString();
                text += ".";
                text += _packet[_index + 3].ToString();
                text += ":";
                text += BitConverter.ToUInt16(_packet, _index + 4).ToString();
                _index += 6;
            }
            catch
            {
                Console.WriteLine("Packet: Problem IP4 decode: " + BitConverter.ToString(_packet));
            }
            return text;
        }
        #endregion
    }
}