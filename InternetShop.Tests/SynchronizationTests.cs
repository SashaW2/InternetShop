using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InternetShop.Server;
using InternetShop.Shared.Models;
using InternetShop.Shared.Enums;
using InternetShop.Shared.DTO;
using Newtonsoft.Json;
using Xunit;

namespace InternetShop.Tests
{
    public class SynchronizationTests
    {
        private readonly BusinessLogic _logic;

        public SynchronizationTests()
        {
            _logic = new BusinessLogic();
            DataStore.ResetData();
        }

        // ============================================================
        // ТЕСТ 1: С lock — ровно один Success, один Fail
        // ============================================================
        [Fact]
        public void Test1_WithLock_OnlyOneClientSucceeds()
        {
            DataStore.ResetData();
            var product = DataStore.Products.Find(p => p.Id == 1);
            product.Stock = 1;

            var order1 = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem> { new OrderItem { ProductId = 1, Quantity = 1 } },
                ShippingAddress = "Адрес 1"
            };

            var order2 = new Order
            {
                CustomerId = 2,
                Items = new List<OrderItem> { new OrderItem { ProductId = 1, Quantity = 1 } },
                ShippingAddress = "Адрес 2"
            };

            var json1 = JsonConvert.SerializeObject(order1);
            var json2 = JsonConvert.SerializeObject(order2);

            var task1 = Task.Run(() => _logic.CreateOrder(json1, 1));
            var task2 = Task.Run(() => _logic.CreateOrder(json2, 2));

            Task.WaitAll(task1, task2);

            var response1 = task1.Result;
            var response2 = task2.Result;

            int successCount = 0;
            if (response1.Success) successCount++;
            if (response2.Success) successCount++;

            Console.WriteLine("=== С LOCK ===");
            Console.WriteLine($"Client 1: Success={response1.Success}, Error={response1.Error}");
            Console.WriteLine($"Client 2: Success={response2.Success}, Error={response2.Error}");
            Console.WriteLine($"Остаток: {product.Stock}");
            Console.WriteLine($"Заказов: {DataStore.Orders.Count}");

            Assert.Equal(1, successCount);
            Assert.Equal(0, product.Stock);
            Assert.Equal(1, DataStore.Orders.Count);
        }
    }
}