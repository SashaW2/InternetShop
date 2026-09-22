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
        private static int _clientClockOffsetSeconds = 0;

        static void Main(string[] args)
        {
            Console.Title = "КЛИЕНТ Интернет-магазина";
            Console.WriteLine("========================================");
            Console.WriteLine("   КЛИЕНТ ИНТЕРНЕТ-МАГАЗИНА");
            Console.WriteLine("========================================");

            Console.Write("Введите смещение часов клиента в секундах (например, 7 или -5): ");
            if (int.TryParse(Console.ReadLine(), out int offsetSeconds))
            {
                _clientClockOffsetSeconds = offsetSeconds;
            }

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
                Console.WriteLine("5. Синхронизировать время с сервером");
                Console.WriteLine("6. Показать локальное время клиента");
                Console.WriteLine("7. Выход");
                Console.WriteLine("========================================");
                Console.Write("Выберите действие: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        HandleGetProducts();
                        break;
                    case "2":
                        HandleCreateOrder();
                        break;
                    case "3":
                        HandleCancelOrder();
                        break;
                    case "4":
                        HandlePayOrder();
                        break;
                    case "5":
                        HandleTimeSync();
                        break;
                    case "6":
                        ShowLocalTime();
                        break;
                    case "7":
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
                _client.ClockOffset = TimeSpan.FromSeconds(_clientClockOffsetSeconds);
                Console.WriteLine($"Смещение часов клиента: {_client.ClockOffset.TotalSeconds:+0;-0;0} сек");
                _client.Connect("127.0.0.1", 3000);
                _isConnected = true;
                Console.WriteLine("Подключение к серверу установлено!");
                Console.WriteLine("Нажмите любую клавишу для начала работы...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                Console.WriteLine("1. Проверьте, запущен ли сервер");
                Console.WriteLine("2. Проверьте правильность IP и порта");
                Console.WriteLine("\nНажмите любую клавишу для выхода...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static void CheckConnection()
        {
            if (!_isConnected || !_client.IsConnected())
            {
                Console.WriteLine("Соединение с сервером потеряно.");
                Console.WriteLine("Попробуйте перезапустить клиент.");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static void HandleGetProducts()
        {
            CheckConnection();
            if (!_isConnected) return;

            try
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] Запрос списка товаров...");
                var startTime = DateTime.Now;

                var request = RequestBuilder.BuildGetProducts();
                var response = _client.SendRequest(request);

                var endTime = DateTime.Now;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Ответ получен. Время обработки: {(endTime - startTime).TotalSeconds:F2} сек");
                DisplayResponse(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _isConnected = false;
            }
        }

        private static void HandleCreateOrder()
        {
            CheckConnection();
            if (!_isConnected) return;

            try
            {
                Console.Write("Введите ID клиента: ");
                int customerId = int.Parse(Console.ReadLine());

                var items = new List<OrderItem>();
                bool addMore = true;

                while (addMore)
                {
                    Console.WriteLine("\n--- Добавление товара ---");
                    Console.Write("Введите ID товара (0 - закончить): ");
                    int productId = int.Parse(Console.ReadLine());

                    if (productId == 0)
                    {
                        if (items.Count == 0)
                        {
                            Console.WriteLine("Добавьте хотя бы один товар!");
                            continue;
                        }
                        break;
                    }

                    Console.Write("Введите количество: ");
                    int quantity = int.Parse(Console.ReadLine());

                    items.Add(new OrderItem
                    {
                        ProductId = productId,
                        Quantity = quantity
                    });

                    Console.WriteLine($"Товар добавлен. Всего товаров в заказе: {items.Count}");
                    Console.Write("Добавить еще товар? (y/n): ");
                    addMore = Console.ReadLine()?.ToLower() == "y";
                }

                if (items.Count == 0)
                {
                    Console.WriteLine("Заказ не может быть пустым!");
                    return;
                }

                Console.Write("Введите адрес доставки: ");
                string address = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(address))
                {
                    Console.WriteLine("Ошибка: адрес доставки обязателен");
                    return;
                }

                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] Всего товаров в заказе: {items.Count}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Отправка запроса на создание заказа...");

                var request = RequestBuilder.BuildCreateOrder(customerId, items, address);

                var startTime = DateTime.Now;
                var response = _client.SendRequest(request);
                var endTime = DateTime.Now;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Ответ получен. Время обработки: {(endTime - startTime).TotalSeconds:F2} сек");
                DisplayResponse(response);
            }
            catch (FormatException)
            {
                Console.WriteLine("Ошибка: введите корректные числа");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _isConnected = false;
            }
        }

        private static void HandleCancelOrder()
        {
            CheckConnection();
            if (!_isConnected) return;

            try
            {
                Console.Write("Введите ID заказа: ");
                int orderId = int.Parse(Console.ReadLine());

                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] Отправка запроса на отмену заказа №{orderId}...");

                var request = RequestBuilder.BuildCancelOrder(orderId);

                var startTime = DateTime.Now;
                var response = _client.SendRequest(request);
                var endTime = DateTime.Now;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Ответ получен. Время обработки: {(endTime - startTime).TotalSeconds:F2} сек");
                DisplayResponse(response);
            }
            catch (FormatException)
            {
                Console.WriteLine("Ошибка: введите корректный ID");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _isConnected = false;
            }
        }

        private static void HandlePayOrder()
        {
            CheckConnection();
            if (!_isConnected) return;

            try
            {
                Console.Write("Введите ID заказа: ");
                int orderId = int.Parse(Console.ReadLine());

                Console.Write("Введите способ оплаты (Банковская карта/Наличные/Онлайн): ");
                string paymentMethod = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(paymentMethod))
                {
                    paymentMethod = "Банковская карта";
                }

                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] Отправка запроса на оплату заказа №{orderId}...");

                var request = RequestBuilder.BuildPayOrder(orderId, paymentMethod);

                var startTime = DateTime.Now;
                var response = _client.SendRequest(request);
                var endTime = DateTime.Now;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Ответ получен. Время обработки: {(endTime - startTime).TotalSeconds:F2} сек");
                DisplayResponse(response);
            }
            catch (FormatException)
            {
                Console.WriteLine("Ошибка: введите корректный ID");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _isConnected = false;
            }
        }

        private static void HandleTimeSync()
        {
            CheckConnection();
            if (!_isConnected) return;

            try
            {
                Console.WriteLine("\n========== СИНХРОНИЗАЦИЯ ВРЕМЕНИ ==========");

                // 1. Локальное время клиента ДО синхронизации
                DateTime localBefore = _client.GetLocalTime();
                Console.WriteLine($"Локальное время клиента (до синхр.): {localBefore:HH:mm:ss.fff}");

                // 2. Запрос времени у сервера
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss.fff}] Отправка TIME_REQUEST...");

                DateTime requestTime = DateTime.UtcNow;
                var request = RequestBuilder.BuildGetServerTime();
                var response = _client.SendRequest(request);
                DateTime responseTime = DateTime.UtcNow;

                if (!response.Success)
                {
                    Console.WriteLine($"Ошибка сервера: {response.Error}");
                    return;
                }

                // 3. Разбор ответа
                var timeData = Newtonsoft.Json.JsonConvert.DeserializeObject<InternetShop.Shared.DTO.TimeResponse>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(response.Result));

                if (timeData == null || timeData.ServerTimeUtc == default)
                {
                    Console.WriteLine("Ошибка: сервер вернул некорректные данные времени");
                    return;
                }

                DateTime serverTime = timeData.ServerTimeUtc;

                // 4. RTT и оценка задержки
                TimeSpan rtt = responseTime - requestTime;
                TimeSpan networkDelay = TimeSpan.FromMilliseconds(rtt.TotalMilliseconds / 2.0);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] TIME_RESPONSE получен");
                Console.WriteLine($"Server Time (UTC):       {serverTime:HH:mm:ss.fff}");
                Console.WriteLine($"Client Local Time:       {localBefore:HH:mm:ss.fff}");
                Console.WriteLine($"Request Time (UTC):      {requestTime:HH:mm:ss.fff}");
                Console.WriteLine($"Response Time (UTC):     {responseTime:HH:mm:ss.fff}");
                Console.WriteLine($"RTT:                     {rtt.TotalMilliseconds:F0} мс");
                Console.WriteLine($"Network delay (RTT/2):   {networkDelay.TotalMilliseconds:F0} мс");
                Console.WriteLine($"Искусственная задержка:  {timeData.ArtificialDelayMs} мс");

                // 5. Расчет поправки
                //    Оценка времени сервера в момент приёма ответа = serverTime + RTT/2
                //    Offset = (serverTime + RTT/2) - localTime(в момент приёма)
                DateTime estimatedServerNow = serverTime + networkDelay;
                DateTime localNow = _client.GetLocalTime();

                TimeSpan offset = estimatedServerNow - localNow;
                _client.CalculatedOffset = offset;

                // 6. Вывод результата
                Console.WriteLine($"\nВычисленная поправка:    {offset.TotalMilliseconds:F0} мс ({offset.TotalSeconds:F2} сек)");
                Console.WriteLine($"Разница до синхронизации: {(serverTime - localBefore).TotalSeconds:F2} сек");

                DateTime corrected = _client.GetCorrectedTime();
                Console.WriteLine($"Скорректированное время клиента: {corrected:HH:mm:ss.fff}");
                Console.WriteLine($"Разница после синхронизации:    {(serverTime + networkDelay - corrected).TotalMilliseconds:F0} мс");

                Console.WriteLine("===========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка синхронизации времени: {ex.Message}");
            }
        }

        private static void ShowLocalTime()
        {
            Console.WriteLine($"\nЛокальное время клиента (с учётом смещения): {_client.GetLocalTime():HH:mm:ss.fff}");
            Console.WriteLine($"Скорректированное время: {_client.GetCorrectedTime():HH:mm:ss.fff}");
            Console.WriteLine($"Текущая поправка: {_client.CalculatedOffset.TotalSeconds:F2} сек");
        }

        private static void DisplayResponse(Response response)
        {
            if (response.Success)
            {
                Console.WriteLine($"Успешно: {response.Message}");
                if (response.Result != null)
                {
                    Console.WriteLine($"Результат: {Newtonsoft.Json.JsonConvert.SerializeObject(response.Result, Newtonsoft.Json.Formatting.Indented)}");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.Error}");
            }
        }
    }
}