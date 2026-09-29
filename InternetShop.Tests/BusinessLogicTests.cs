using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InternetShop.Server;
using InternetShop.Server.Database;
using InternetShop.Shared.Models;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Xunit;

namespace InternetShop.Tests
{
    public class BusinessLogicTests : IDisposable
    {
        private readonly BusinessLogic _logic = new BusinessLogic();

        public BusinessLogicTests()
        {
            Database.ResetLabData();
            BusinessLogic.SimulateError = false;
        }

        public void Dispose()
        {
            BusinessLogic.SimulateError = false;
            Database.ResetLabData();
        }

        private static Order MakeOrder(int productId = 2, int quantity = 1, int customerId = 1)
        {
            return new Order
            {
                CustomerId = customerId,
                ShippingAddress = "г. Минск, ул. Ленина 1",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = productId, Quantity = quantity }
                }
            };
        }

        private static string ToJson(Order order) => JsonConvert.SerializeObject(order);

        private List<Product> GetProductsList()
        {
            var response = _logic.GetProducts();
            return response.Result as List<Product> ?? new List<Product>();
        }

        private static int GetOrdersCountFromDb()
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Orders", conn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        // ============================================================
        //  ТЕСТ 1. Корректность операции с общим ресурсом
        // ============================================================
        [Fact]
        public void CreateOrder_WithAvailableResource_ReturnsSuccess()
        {
            var order = MakeOrder(productId: 1, quantity: 1);

            var response = _logic.CreateOrder(ToJson(order), clientId: 1);

            Assert.True(response.Success, $"Ожидался Success, получено: {response.Error}");

            var products = GetProductsList();
            var asus = products.First(p => p.Id == 1);
            Assert.Equal(0, asus.Stock);
            Assert.Equal(1, GetOrdersCountFromDb());
        }

        // ============================================================
        //  ТЕСТ 2. Невозможность получить один ресурс двумя
        //          конкурентными запросами
        // ============================================================
        [Fact]
        public void CreateOrder_TwoConcurrentRequests_OnlyOneSucceeds()
        {
            var json1 = ToJson(MakeOrder(productId: 1, quantity: 1));
            var json2 = ToJson(MakeOrder(productId: 1, quantity: 1));

            using (var barrier = new Barrier(2))
            {
                var task1 = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    return _logic.CreateOrder(json1, clientId: 1);
                });

                var task2 = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    return _logic.CreateOrder(json2, clientId: 2);
                });

                Task.WaitAll(task1, task2);

                int successCount = (task1.Result.Success ? 1 : 0) +
                                   (task2.Result.Success ? 1 : 0);
                Assert.Equal(1, successCount);
            }

            var products = GetProductsList();
            var asus = products.First(p => p.Id == 1);
            Assert.Equal(0, asus.Stock);

            Assert.Equal(1, GetOrdersCountFromDb());
        }

        // ============================================================
        //  ТЕСТ 3. Успешное выполнение составной операции (COMMIT)
        // ============================================================
        [Fact]
        public void CreateOrder_CompositeOperation_BothPartsApplied()
        {
            var order = MakeOrder(productId: 2, quantity: 3);

            var response = _logic.CreateOrder(ToJson(order), clientId: 1);

            Assert.True(response.Success, $"Ожидался Success, получено: {response.Error}");

            var products = GetProductsList();
            var samsung = products.First(p => p.Id == 2);

            Assert.Equal(25 - 3, samsung.Stock);
            Assert.Equal(1, GetOrdersCountFromDb());
        }

        // ============================================================
        //  ТЕСТ 4. ROLLBACK: изменения откатились полностью
        // ============================================================
        [Fact]
        public void CreateOrder_WithTransaction_Error_DoesNotChangeDb()
        {
            BusinessLogic.SimulateError = true;

            var order = MakeOrder(productId: 2, quantity: 2);

            var response = _logic.CreateOrder(ToJson(order), clientId: 1);

            Assert.False(response.Success);

            var products = GetProductsList();
            var samsung = products.First(p => p.Id == 2);

            Assert.Equal(25, samsung.Stock);
            Assert.Equal(0, GetOrdersCountFromDb());
        }

        // ============================================================
        //  ТЕСТ 5. Без транзакции: заказ остался, Stock не изменился
        // ============================================================
        [Fact]
        public void CreateOrderWithoutTran_WithError_PersistsPartialChanges()
        {
            BusinessLogic.SimulateError = true;

            var order = MakeOrder(productId: 2, quantity: 2);

            var response = _logic.CreateOrderWithoutTran(ToJson(order), clientId: 1);

            Assert.False(response.Success);

            var products = GetProductsList();
            var samsung = products.First(p => p.Id == 2);

            Assert.Equal(25, samsung.Stock);
            Assert.Equal(1, GetOrdersCountFromDb());
        }
    }
}