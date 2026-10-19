using System;
using System.Collections.Generic;
using System.IO;
using InternetShop.Server.Database.Repositories;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Models;

namespace InternetShop.Server.PubSub
{
    public class StockServiceSubscriber : Subscriber
    {
        private readonly ProductRepository _products = new ProductRepository();
        private readonly Dictionary<int, int> _demoAttempts = new Dictionary<int, int>();
        private readonly string _flagPath;

        public StockServiceSubscriber() : base("StockService")
        {
            DelayMs = 3000;
            _flagPath = Path.Combine(AppContext.BaseDirectory, "stock_available.txt");
            RefreshAvailability();
        }

        public override async System.Threading.Tasks.Task<DeliveryResult> HandleAsync(Event evt)
        {
            RefreshAvailability();
            return await base.HandleAsync(evt);
        }

        public override bool Handle(Event evt)
        {
            if (evt.EventType != EventTypes.OrderCreated)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] StockService - SKIP (не подписан на {evt.EventType})");
                return false;
            }

            try
            {
                _demoAttempts.TryGetValue(evt.EntityId, out int attempt);
                attempt++;
                _demoAttempts[evt.EntityId] = attempt;

                if (attempt < 2)
                    throw new InvalidOperationException($"demo fail #{attempt}");

                if (evt.Data == null || !evt.Data.ContainsKey("Items"))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] StockService - WARN: в событии нет Items, пропуск");
                    return false;
                }

                var itemsJson = Newtonsoft.Json.JsonConvert.SerializeObject(evt.Data["Items"]);
                var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<OrderItem>>(itemsJson);

                foreach (var item in items)
                {
                    var product = _products.GetById(item.ProductId);
                    if (product == null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] StockService - FAIL: товар {item.ProductId} не найден");
                        return false;
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] StockService - Остаток товара '{product.Name}': {product.Stock} (списано {item.Quantity})");
                }

                _demoAttempts.Remove(evt.EntityId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] StockService - ERROR: {ex.Message}");
                return false;
            }
        }

        private void RefreshAvailability()
        {
            try
            {
                if (!File.Exists(_flagPath))
                {
                    IsAvailable = false;
                    return;
                }

                var text = File.ReadAllText(_flagPath).Trim().ToLowerInvariant();
                IsAvailable = text == "true";
            }
            catch
            {
                IsAvailable = false;
            }
        }
    }
}