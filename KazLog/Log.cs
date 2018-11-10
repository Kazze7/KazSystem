using System;

namespace KazLog
{
    public static class Log
    {
        public static void Message(string _message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss.fff") + " - " + _message);
        }
    }
}
