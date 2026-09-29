using System;

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
            Console.WriteLine();

            if (!InternetShop.Server.Database.Database.TestConnection(out string dbError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Не удалось подключиться к БД: {dbError}");
                Console.ResetColor();
                Console.WriteLine("Проверьте, что SQL Server запущен и БД InternetShopDB создана.");
                Console.ReadKey();
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключение к БД установлено.");
            Console.ResetColor();

            try
            {
                var server = new Server(3000);
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