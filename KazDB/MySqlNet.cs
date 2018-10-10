using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazDB
{
    public class MySqlNet
    {
        bool isRunning = false;
        bool isConnected = false;
        MySqlConnection connection;
        MySqlConnectionStringBuilder connectionString = new MySqlConnectionStringBuilder();

        public delegate void StatusMethod(bool _status);
        StatusMethod statusMethod;

        public bool IsConnected()
        {
            return isConnected;
        }
        public MySqlNet(string _host, uint _port, string _dataBase, string _user, string _password, bool _pooling = true)
        {
            connectionString.Server = _host;
            connectionString.Port = _port;
            connectionString.Database = _dataBase;
            connectionString.UserID = _user;
            connectionString.Password = _password;
            connectionString.Pooling = _pooling;
        }

        public void Start(StatusMethod _statusMethod = null)
        {
            if (!isRunning)
            {
                statusMethod = _statusMethod;
                isRunning = true;
                ConnectToDB();
            }
        }
        public void Stop()
        {
            if (connection != null)
            {
                connection.Close();
                connection = null;
                Console.WriteLine("DatabaseNet: Close connection with SQL");
            }
            isConnected = false;
            isRunning = false;
        }

        void ConnectToDB()
        {
            try
            {
                connection = new MySqlConnection(connectionString.ConnectionString);
                connection.Open();
                Console.WriteLine("DataBaseNet: SQL Connection status: " + connection.State);
                isConnected = true;
                statusMethod?.Invoke(isConnected);
            }
            catch (MySqlException ex)
            {
                isConnected = false;
                statusMethod?.Invoke(isConnected);
                Console.WriteLine("DatabaseNet: " + ex.Message);
            }
        }
        public MySqlCommand SQLRequest(string _cmd)
        {
            try
            {
                MySqlCommand sqlResult;
                if (connection != null)
                    sqlResult = new MySqlCommand(_cmd, connection);
                else
                    sqlResult = null;
                return sqlResult;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("DatabaseNet: " + ex.Message);
                return null;
            }
        }
    }
}
