using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Serilog;

namespace FinalBet.Database
{
    public static class Connection
    {
        public static SqlConnection Con { get; private set; }

        public static string ConnectionString { get; private set; }

        public static bool IsSuccessful { get; private set; }

        public static void Initialize(string conString)
        {
            if (conString == null)
            {
                throw new Exception("Connection string is null");
            }
            if (conString.Length < 1)
            {
                throw new Exception("Connection string length < 0");
            }

            IsSuccessful = true;
            try
            {
                Con = new SqlConnection(conString);
                ConnectionString = conString;
                Con.Open();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Не удалось подключиться к базе данных. {@conString}",conString);
                IsSuccessful = false;
            }
            finally
            {
                Con.Close();
            }

            if (IsSuccessful && !File.Exists("connection"))
            {
                File.WriteAllText("connection", conString);
            }
        }


        private static void Open()
        {
            if (Con.State != ConnectionState.Open) Con.Open();
        }

        private static void Close()
        {
            if (Con.State != ConnectionState.Closed) Con.Close();
        }

        public static object ExecuteScalar(SqlCommand command)
        {
            Open();
            var res = command.ExecuteScalar();
            Close();
            return res;
        }
    }
}
