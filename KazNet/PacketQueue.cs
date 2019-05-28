using System.Collections.Generic;
using System.Threading;

namespace KazNet
{
    public class PacketQueue
    {
        bool isRunning = false;

        Thread queueThread;
        AutoResetEvent packetQueueCheck = new AutoResetEvent(false);
        Queue<PacketData> packetQueue = new Queue<PacketData>();
        public delegate void DecodePacketMethod(PacketData _packetData);
        DecodePacketMethod decodePacketMethod;

        public PacketQueue(DecodePacketMethod _decodePacket = null)
        {
            decodePacketMethod = _decodePacket;
        }

        public void Start()
        {
            if (!isRunning)
            {
                isRunning = true;
                queueThread = new Thread(StartQueue);
                queueThread.Start();
            }
        }
        public void Stop()
        {
            if (isRunning)
            {
                isRunning = false;
                packetQueueCheck.Set();
                if (queueThread != null)
                {
                    queueThread.Join();
                    queueThread = null;
                }
                packetQueue.Clear();
            }
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
        public void AddPacket(PacketData _packetData)
        {
            packetQueue.Enqueue(_packetData);
            packetQueueCheck.Set();
        }
    }
}
