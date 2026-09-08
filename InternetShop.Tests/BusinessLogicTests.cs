using System;
using System.Linq;
using System.Collections.Generic;
using InternetShop.Server;
using InternetShop.Shared.Models;
using InternetShop.Shared.Enums;
using InternetShop.Shared.DTO;
using Newtonsoft.Json;
using Xunit;

namespace InternetShop.Tests
{
    public class BusinessLogicTests
    {
        private readonly BusinessLogic _logic;

        public BusinessLogicTests()
        {
            _logic = new BusinessLogic();
            DataStore.ResetData();
        }

        // ============================================
        // ТЕСТ 1: Успешный сценарий
        // ============================================
        [Fact]
        public void Test1_SuccessfulScenario_CreateOrder_ReturnsSuccess()
        {
            // Arrange - создаем объект как в реальном клиенте
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            // Сериализуем как в клиенте
            var json = JsonConvert.SerializeObject(order);

            // Act - передаем как object (как приходит с клиента)
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.True(response.Success, $"Expected success but got error: {response.Error}");
            Assert.Contains("Заказ №", response.Message);
            Assert.NotNull(response.Result);

            var createdOrder = response.Result as Order;
            Assert.NotNull(createdOrder);
            Assert.Equal(1, createdOrder.Items.Count);
            Assert.Equal(2, createdOrder.Items[0].Quantity);
            Assert.Equal(1, createdOrder.CustomerId);
            Assert.Equal("г. Минск, ул. Тестовая, д. 1", createdOrder.ShippingAddress);
        }

        // ============================================
        // ТЕСТ 2: Неизвестная операция
        // ============================================
        [Fact]
        public void Test2_UnknownOperation_ReturnsError()
        {
            // Arrange
            var handler = new RequestHandler();
            var request = new Request
            {
                Operation = (OperationType)999,
                Data = null
            };
            var requestJson = JsonConvert.SerializeObject(request);

            // Act
            var response = handler.ProcessRequest(requestJson);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Неизвестная операция", response.Error);
        }

        // ============================================
        // ТЕСТ 3: Некорректные данные
        // ============================================
        [Fact]
        public void Test3_InvalidData_EmptyOrder_ReturnsError()
        {
            // Arrange - пустой заказ (нет товаров)
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>(),
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("заказ должен содержать хотя бы один товар", response.Error);
        }

        [Fact]
        public void Test3_InvalidData_EmptyAddress_ReturnsError()
        {
            // Arrange - пустой адрес
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 }
                },
                ShippingAddress = ""
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("укажите адрес доставки", response.Error);
        }

        [Fact]
        public void Test3_InvalidData_ProductNotFound_ReturnsError()
        {
            // Arrange - несуществующий товар
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 999, Quantity = 2 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Товар с ID 999 не найден", response.Error);
        }

        [Fact]
        public void Test3_InvalidData_CustomerNotFound_ReturnsError()
        {
            // Arrange - несуществующий клиент
            var order = new Order
            {
                CustomerId = 999,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Клиент с ID 999 не найден", response.Error);
        }

        [Fact]
        public void Test3_InvalidData_NotEnoughStock_ReturnsError()
        {
            // Arrange - недостаточно товара
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 999 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);

            // Act
            var response = _logic.CreateOrder(json);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Недостаточно товара", response.Error);
        }

        // ============================================
        // ТЕСТ 4: Отмена заказа (успешная)
        // ============================================
        [Fact]
        public void Test4_CancelOrder_Success_ReturnsSuccess()
        {
            // Arrange - сначала создаем заказ через JSON как клиент
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);
            var createResponse = _logic.CreateOrder(json);

            Assert.True(createResponse.Success, $"Failed to create order: {createResponse.Error}");

            var createdOrder = createResponse.Result as Order;
            Assert.NotNull(createdOrder);
            int orderId = createdOrder.Id;

            // Act - отменяем заказ (передаем как object)
            var response = _logic.CancelOrder(orderId);

            // Assert
            Assert.True(response.Success);
            Assert.Contains($"Заказ №{orderId} успешно отменен", response.Message);

            var cancelledOrder = response.Result as Order;
            Assert.NotNull(cancelledOrder);
            Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
        }

        // ============================================
        // ТЕСТ 4: Оплата заказа (успешная)
        // ============================================
        [Fact]
        public void Test4_PayOrder_Success_ReturnsSuccess()
        {
            // Arrange - сначала создаем заказ через JSON как клиент
            var order = new Order
            {
                CustomerId = 1,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 }
                },
                ShippingAddress = "г. Минск, ул. Тестовая, д. 1"
            };

            var json = JsonConvert.SerializeObject(order);
            var createResponse = _logic.CreateOrder(json);

            Assert.True(createResponse.Success, $"Failed to create order: {createResponse.Error}");

            var createdOrder = createResponse.Result as Order;
            Assert.NotNull(createdOrder);
            int orderId = createdOrder.Id;

            // Данные для оплаты как в клиенте
            var paymentData = new Dictionary<string, object>
            {
                { "OrderId", orderId },
                { "PaymentMethod", "Банковская карта" }
            };

            // Сериализуем как в клиенте
            var paymentJson = JsonConvert.SerializeObject(paymentData);

            // Act
            var response = _logic.PayOrder(paymentJson);

            // Assert
            Assert.True(response.Success);
            Assert.Contains($"Заказ №{orderId} успешно оплачен", response.Message);
        }

        // ============================================
        // ТЕСТ 4: Отмена несуществующего заказа
        // ============================================
        [Fact]
        public void Test4_CancelOrder_NotFound_ReturnsError()
        {
            // Act
            var response = _logic.CancelOrder(999);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("Заказ с ID 999 не найден", response.Error);
        }
    }
}