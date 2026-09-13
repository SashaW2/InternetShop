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
    public class RaceConditionTests
    {
        private readonly BusinessLogic _logic;

        public RaceConditionTests()
        {
            _logic = new BusinessLogic();
            DataStore.ResetData();
        }

        // ============================================
        // ТЕСТ 1: Один клиент — проверка корректности
        // ============================================
        [Fact]
        public void Test1_OneClient_CreateOrder_Success()
        {
            // Arrange
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 1 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.True(response.Success, $"Expected success but got: {response.Error}");
            Assert.Contains("Заказ №", response.Message);

            // Проверяем, что товар списан
            var product = DataStore.Products.Find(p => p.Id == 1);
            Assert.NotNull(product);
            Assert.Equal(0, product.Stock); // БЫЛО 1, СТАЛО 0
        }

        // ============================================
        // ТЕСТ 2: Два клиента — последовательная обработка
        // ============================================
        [Fact]
        public void Test2_TwoClients_SequentialProcessing()
        {
            // Arrange — сначала сбрасываем и устанавливаем Stock = 2
            DataStore.ResetData();
            var product = DataStore.Products.Find(p => p.Id == 1);
            product.Stock = 2;

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

            // Act — последовательно
            var response1 = _logic.CreateOrder(JsonConvert.SerializeObject(order1));
            var response2 = _logic.CreateOrder(JsonConvert.SerializeObject(order2));

            // Assert
            Assert.True(response1.Success);
            Assert.True(response2.Success);
            Assert.Equal(0, product.Stock);
        }

        // ============================================
        // ТЕСТ 3: Параллельная обработка (демонстрация)
        // ============================================
        [Fact]
        public void Test3_ParallelProcessing_TwoTasksRunConcurrently()
        {
            // Arrange
            DataStore.ResetData();
            var logic1 = new BusinessLogic();
            var logic2 = new BusinessLogic();

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

            // Act — запускаем параллельно
            var task1 = Task.Run(() => logic1.CreateOrder(json1));
            var task2 = Task.Run(() => logic2.CreateOrder(json2));

            Task.WaitAll(task1, task2);

            var response1 = task1.Result;
            var response2 = task2.Result;

            // Assert — оба должны быть успешными (Race Condition!)
            // В реальной системе только один должен быть успешным
            Console.WriteLine($"Response 1: Success={response1.Success}, Error={response1.Error}");
            Console.WriteLine($"Response 2: Success={response2.Success}, Error={response2.Error}");
            Console.WriteLine($"Product Stock: {DataStore.Products.Find(p => p.Id == 1)?.Stock}");

            // При Race Condition оба могут получить SUCCESS, хотя товар был только 1!
            // Это и есть демонстрация проблемы
        }

        // ============================================
        // ТЕСТ 4: Race Condition — два клиента, один товар
        // ============================================
        [Fact]
        public void Test4_RaceCondition_TwoClients_OneItem()
        {
            // Arrange — товар только 1!
            DataStore.ResetData();
            var product = DataStore.Products.Find(p => p.Id == 1);
            product.Stock = 1;

            var logic1 = new BusinessLogic();
            var logic2 = new BusinessLogic();

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

            // Act — запускаем параллельно
            var task1 = Task.Run(() => logic1.CreateOrder(json1));
            var task2 = Task.Run(() => logic2.CreateOrder(json2));

            Task.WaitAll(task1, task2);

            var response1 = task1.Result;
            var response2 = task2.Result;

            // Assert — при Race Condition оба могут быть SUCCESS!
            Console.WriteLine("=== РЕЗУЛЬТАТЫ ЭКСПЕРИМЕНТА RACE CONDITION ===");
            Console.WriteLine($"Client 1: Success={response1.Success}, Message={response1.Message}, Error={response1.Error}");
            Console.WriteLine($"Client 2: Success={response2.Success}, Message={response2.Message}, Error={response2.Error}");
            Console.WriteLine($"Остаток товара: {DataStore.Products.Find(p => p.Id == 1)?.Stock}");
            Console.WriteLine($"Заказов создано: {DataStore.Orders.Count}");

            // Проблема: оба клиента могли получить SUCCESS, хотя товар был только 1!
            // Это и есть Race Condition — результат зависит от порядка выполнения.

            // Если оба получили SUCCESS — Race Condition воспроизведён!
            if (response1.Success && response2.Success)
            {
                Console.WriteLine("!!! RACE CONDITION ОБНАРУЖЕН: оба клиента забронировали один товар !!!");
            }
        }
    }
}