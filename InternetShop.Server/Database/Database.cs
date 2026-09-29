using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace InternetShop.Server.Database
{
    public static class Database
    {
        private const string ConnectionString =
            @"Server=localhost;Database=InternetShopDB;User Id=sa;Password=2006;TrustServerCertificate=true;";

        public static SqlConnection CreateConnection()
        {
            var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static bool TestConnection(out string error)
        {
            error = null;
            try
            {
                using (var conn = CreateConnection())
                {
                    return conn.State == ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void ResetLabData()
        {
            using (var conn = CreateConnection())
            using (var cmd = new SqlCommand("EXEC dbo.ResetLabData", conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}