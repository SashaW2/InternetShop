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