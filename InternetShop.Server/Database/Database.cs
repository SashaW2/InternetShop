using System.Data;
using Microsoft.Data.SqlClient;

namespace InternetShop.Server.Database
{
    public static class Database
    {
        private static string BuildConnectionString(string database)
        {
            var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
            var user = Environment.GetEnvironmentVariable("DB_USER") ?? "sa";
            var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "2006";
            return $"Server={host},{port};Database={database};User Id={user};Password={pass};TrustServerCertificate=true;";
        }

        private static string DatabasePrimaryName =>
            Environment.GetEnvironmentVariable("DB_NAME_PRIMARY") ?? "InternetShopDB_Primary";
        private static string DatabaseReplicaName =>
            Environment.GetEnvironmentVariable("DB_NAME_REPLICA") ?? "InternetShopDB_Replica";

        private static string PrimaryConnectionString => BuildConnectionString(DatabasePrimaryName);
        private static string ReplicaConnectionString => BuildConnectionString(DatabaseReplicaName);

        private static bool _primaryAvailable = true;
        private static bool _replicaAvailable = true;
        private static bool _failoverLogged = false;

        public static bool IsPrimaryAvailable() => _primaryAvailable;
        public static bool IsReplicaAvailable() => _replicaAvailable;

        public static string GetPrimaryConnectionStringForSync() => PrimaryConnectionString;
        public static string GetReplicaConnectionStringForSync() => ReplicaConnectionString;

        public static void SetPrimaryAvailable(bool available)
        {
            _primaryAvailable = available;
            _failoverLogged = false;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary → {(available ? "AVAILABLE" : "UNAVAILABLE")}");
        }

        public static void SetReplicaAvailable(bool available)
        {
            _replicaAvailable = available;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replica → {(available ? "AVAILABLE" : "UNAVAILABLE")}");
        }

        public static SqlConnection CreatePrimaryConnection()
        {
            if (!_primaryAvailable)
            {
                if (!_replicaAvailable)
                    throw new Exception("Оба экземпляра недоступны");

                if (!_failoverLogged)
                {
                    _failoverLogged = true;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- CHECK");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- UNAVAILABLE");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failover -- START");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replica -- CHECK");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replica -- AVAILABLE");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Request redirected to Replica");
                }

                var connR = new SqlConnection(ReplicaConnectionString);
                connR.Open();
                return connR;
            }

            var conn = new SqlConnection(PrimaryConnectionString);
            conn.Open();
            return conn;
        }

        public static SqlConnection CreateReplicaConnection()
        {
            if (!_replicaAvailable)
                throw new Exception("Replica недоступна");

            var conn = new SqlConnection(ReplicaConnectionString);
            conn.Open();
            return conn;
        }

        public static SqlConnection CreateConnection() => CreatePrimaryConnection();

        public static bool TestPrimaryConnection(out string error)
        {
            error = null;
            try
            {
                using (var conn = new SqlConnection(PrimaryConnectionString))
                {
                    conn.Open();
                    return conn.State == ConnectionState.Open;
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static bool TestReplicaConnection(out string error)
        {
            error = null;
            try
            {
                using (var conn = new SqlConnection(ReplicaConnectionString))
                {
                    conn.Open();
                    return conn.State == ConnectionState.Open;
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}