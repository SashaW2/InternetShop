using System;
using InternetShop.Server.Database;

namespace InternetShop.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "СЕРВЕР Интернет-магазина";
            Console.WriteLine("========================================");
            Console.WriteLine("   ЗАПУСК СЕРВЕРА ИНТЕРНЕТ-МАГАЗИНА");
            Console.WriteLine("========================================");

            if (!Database.Database.TestPrimaryConnection(out string primaryError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Не удалось подключиться к Primary: {primaryError}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключение к Primary установлено.");
            Console.ResetColor();

            if (!Database.Database.TestReplicaConnection(out string replicaError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Не удалось подключиться к Replica: {replicaError}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключение к Replica установлено.");
            Console.ResetColor();

            var rm = new ReplicationManager();
            if (rm.CheckConsistency(out string report))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Проверка начального состояния: {report}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Внимание! {report}");
            }
            Console.ResetColor();

            try
            {
                var server = new Server(3000);
                Database.Database.SetPrimaryAvailable(false);
                Database.Database.SetReplicaAvailable(false);
                new ReplicationManager().SynchronizeReplicaToPrimary();
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}