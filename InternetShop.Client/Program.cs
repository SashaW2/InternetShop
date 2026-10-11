using System;
using System.Collections.Generic;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;

namespace InternetShop.Client
{
    class Program
    {
        private static Client _client;
        private static bool _isConnected = false;

        static void Main(string[] args)
        {
            Console.Title = "КЛИЕНТ Интернет-магазина";
            Console.WriteLine("========================================");
            Console.WriteLine("   КЛИЕНТ ИНТЕРНЕТ-МАГАЗИНА");
            Console.WriteLine("========================================");

            ConnectToServer();

            while (true)
            {
                Console.Clear();
                Console.WriteLine("========================================");
                Console.WriteLine("        ИНТЕРНЕТ-МАГАЗИН");
                Console.WriteLine("========================================");
                Console.WriteLine("1. Показать товары");
                Console.WriteLine("2. Создать заказ");
                Console.WriteLine("3. Отменить заказ");
                Console.WriteLine("4. Оплатить заказ");
                Console.WriteLine("5. Изменить остаток товара");
                Console.WriteLine("6. Выход");
                Console.WriteLine("========================================");
                Console.Write("Выберите действие: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": HandleGetProducts(); break;
                    case "2": HandleCreateOrder(); break;
                    case "3": HandleCancelOrder(); break;
                    case "4": HandlePayOrder(); break;
                    case "5": HandleUpdateStock(); break;
                    case "6":
                        _client.Close();
                        Console.WriteLine("До свидания!");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор");
                        break;
                }

                Console.WriteLine("\nНажмите любую клавишу для продолжения...");
                Console.ReadKey();
            }
        }

        private static void ConnectToServer()
        {
            try
            {
                _client = new Client();
                _client.Connect("127.0.0.1", 3000);
                _isConnected = true;
                Console.WriteLine("Подключение к серверу установлено!");
                Console.WriteLine("Нажмите любую клавишу для начала работы...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static void CheckConnection()
        {
            if (!_isConnected || !_client.IsConnected())
            {
                Console.WriteLine("Соединение потеряно.");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static void HandleGetProducts()
        {
            CheckConnection();
            try
            {
                var request = RequestBuilder.BuildGetProducts();
                var response = _client.SendRequest(request);
                DisplayResponse(response);
            }
            catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); _isConnected = false; }
        }

        private static void HandleCreateOrder()
        {
            CheckConnection();
            try
            {
                Console.Write("Введите ID клиента: ");
                int customerId = int.Parse(Console.ReadLine());

                var items = new List<OrderItem>();
                while (true)
                {
                    Console.Write("Введите ID товара (0 - закончить): ");
                    int productId = int.Parse(Console.ReadLine());
                    if (productId == 0) break;

                    Console.Write("Введите количество: ");
                    int quantity = int.Parse(Console.ReadLine());

                    items.Add(new OrderItem { ProductId = productId, Quantity = quantity });
                }

                if (items.Count == 0) { Console.WriteLine("Заказ пуст!"); return; }

                Console.Write("Введите адрес доставки: ");
                string address = Console.ReadLine();

                var request = RequestBuilder.BuildCreateOrder(customerId, items, address);
                var response = _client.SendRequest(request);
                DisplayResponse(response);
            }
            catch (FormatException) { Console.WriteLine("Ошибка: введите корректные числа"); }
            catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); _isConnected = false; }
        }

        private static void HandleCancelOrder()
        {
            CheckConnection();
            try
            {
                Console.Write("Введите ID заказа: ");
                int orderId = int.Parse(Console.ReadLine());

                var request = RequestBuilder.BuildCancelOrder(orderId);
                var response = _client.SendRequest(request);
                DisplayResponse(response);
            }
            catch (FormatException) { Console.WriteLine("Ошибка: введите корректный ID"); }
            catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); _isConnected = false; }
        }

        private static void HandlePayOrder()
        {
            CheckConnection();
            try
            {
                Console.Write("Введите ID заказа: ");
                int orderId = int.Parse(Console.ReadLine());

                Console.Write("Введите способ оплаты: ");
                string method = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(method)) method = "Банковская карта";

                var request = RequestBuilder.BuildPayOrder(orderId, method);
                var response = _client.SendRequest(request);
                DisplayResponse(response);
            }
            catch (FormatException) { Console.WriteLine("Ошибка: введите корректный ID"); }
            catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); _isConnected = false; }
        }

        private static void HandleUpdateStock()
        {
            CheckConnection();
            try
            {
                Console.Write("Введите ID товара: ");
                int productId = int.Parse(Console.ReadLine());

                Console.Write("Введите новый остаток: ");
                int newStock = int.Parse(Console.ReadLine());

                var request = RequestBuilder.BuildUpdateStock(productId, newStock);
                var response = _client.SendRequest(request);
                DisplayResponse(response);
            }
            catch (FormatException) { Console.WriteLine("Ошибка: введите корректные числа"); }
            catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); _isConnected = false; }
        }

        private static void DisplayResponse(Response response)
        {
            if (response.Success)
            {
                Console.WriteLine($"Успешно: {response.Message}");
                if (response.Result != null)
                    Console.WriteLine($"Результат: {Newtonsoft.Json.JsonConvert.SerializeObject(response.Result, Newtonsoft.Json.Formatting.Indented)}");
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.Error}");
            }
        }
    }
}