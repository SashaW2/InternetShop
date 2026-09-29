using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using InternetShop.Server.Database;
using InternetShop.Server.Database.Repositories;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;
using Microsoft.Data.SqlClient;

namespace InternetShop.Server
{
    public class BusinessLogic
    {
        private static readonly object _stockLock = new object();

        private readonly ProductRepository _products = new ProductRepository();
        private readonly OrderRepository _orders = new OrderRepository();
        private readonly PaymentRepository _payments = new PaymentRepository();
        private readonly CustomerRepository _customers = new CustomerRepository();

        public static bool SimulateError { get; set; } = false;

        public Response GetProducts()
        {
            try
            {
                var products = _products.GetAll();
                return new Response
                {
                    Success = true,
                    Message = $"Найдено {products.Count} товаров",
                    Result = products,
                    Operation = OperationType.GetProducts.ToString()
                };
            }
            catch (SqlException sqlEx)
            {
                return Fail($"Ошибка БД при получении товаров: {sqlEx.Message}", OperationType.GetProducts);
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при получении товаров: {ex.Message}", OperationType.GetProducts);
            }
        }

        public Response CreateOrder(object data, int clientId = 0)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → START Reserve (CreateOrder)");

                if (data == null)
                    return Fail("Некорректные данные: заказ не может быть пустым",
                                OperationType.CreateOrder);

                var order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(data.ToString());

                if (order == null)
                    return Fail("Некорректные данные: не удалось разобрать заказ",
                                OperationType.CreateOrder);

                if (order.Items == null || !order.Items.Any())
                    return Fail("Некорректные данные: заказ должен содержать хотя бы один товар",
                                OperationType.CreateOrder);

                if (order.CustomerId <= 0)
                    return Fail("Некорректные данные: укажите ID клиента",
                                OperationType.CreateOrder);

                if (string.IsNullOrWhiteSpace(order.ShippingAddress))
                    return Fail("Некорректные данные: укажите адрес доставки",
                                OperationType.CreateOrder);

                if (!_customers.Exists(order.CustomerId))
                    return Fail($"Клиент с ID {order.CustomerId} не найден",
                                OperationType.CreateOrder);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Wait for LOCK");

                lock (_stockLock)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → LOCK");

                    using (var conn = Database.Database.CreateConnection())
                    using (var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                    {
                        try
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → BEGIN TRANSACTION");

                            decimal totalPrice = 0;

                            foreach (var item in order.Items)
                            {
                                var product = _products.GetById(item.ProductId);
                                if (product == null)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ROLLBACK (товар не найден)");
                                    tx.Rollback();
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                    return Fail($"Товар с ID {item.ProductId} не найден",
                                                OperationType.CreateOrder);
                                }

                                if (item.Quantity <= 0)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ROLLBACK (некорректное количество)");
                                    tx.Rollback();
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                    return Fail($"Некорректное количество для товара '{product.Name}'",
                                                OperationType.CreateOrder);
                                }

                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Available = {product.Stock} (товар '{product.Name}')");

                                //Thread.Sleep(5000);

                                if (product.Stock < item.Quantity)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → FAIL (недостаточно товара)");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ROLLBACK");
                                    tx.Rollback();
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                    return Fail($"Недостаточно товара '{product.Name}'. В наличии: {product.Stock}, запрошено: {item.Quantity}",
                                                OperationType.CreateOrder);
                                }

                                item.Price = product.Price;
                                totalPrice += product.Price * item.Quantity;
                            }

                            order.CreatedAt = DateTime.Now;
                            order.Status = OrderStatus.Pending;
                            order.TotalPrice = totalPrice;

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op1: INSERT Order + OrderItems");
                            int newOrderId = _orders.Insert(conn, tx, order);
                            order.Id = newOrderId;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op1 OK (заказ №{newOrderId})");

                            if (SimulateError)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → !!! ИСКУССТВЕННАЯ ОШИБКА между операциями !!!");
                                tx.Rollback();
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ROLLBACK выполнен (заказ №{newOrderId} отменён)");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                return Fail("Искусственная ошибка между операциями (с транзакцией: изменения отменены)",
                                            OperationType.CreateOrder);
                            }

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op2: UPDATE Stock");
                            foreach (var item in order.Items)
                            {
                                bool ok = _products.TryDecreaseStock(conn, tx, item.ProductId, item.Quantity);
                                if (!ok)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → FAIL (не удалось уменьшить остаток)");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ROLLBACK");
                                    tx.Rollback();
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                    return Fail($"Не удалось уменьшить остаток товара ID={item.ProductId}",
                                                OperationType.CreateOrder);
                                }
                            }
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op2 OK");

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → COMMIT");
                            tx.Commit();
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → COMMIT выполнен");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → SUCCESS (заказ №{newOrderId})");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");

                            LogEvent("OrderCreated",
                                     $"Заказ №{newOrderId} создан на сумму {order.TotalPrice} BYN");

                            return new Response
                            {
                                Success = true,
                                Message = $"Заказ №{newOrderId} успешно создан на сумму {order.TotalPrice} BYN",
                                Result = order,
                                Operation = OperationType.CreateOrder.ToString()
                            };
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ИСКЛЮЧЕНИЕ: {ex.Message} → ROLLBACK");
                                tx.Rollback();
                            }
                            catch { }

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                            return Fail($"Ошибка внутри транзакции: {ex.Message}",
                                        OperationType.CreateOrder);
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                return Fail($"Ошибка целостности данных: {sqlEx.Message}",
                            OperationType.CreateOrder);
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при создании заказа: {ex.Message}",
                            OperationType.CreateOrder);
            }
        }

        public Response CreateOrderWithoutTran(object data, int clientId = 0)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → START Reserve (CreateOrder)");

                if (data == null)
                    return Fail("Некорректные данные: заказ не может быть пустым",
                                OperationType.CreateOrder);

                var order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(data.ToString());

                if (order == null)
                    return Fail("Некорректные данные: не удалось разобрать заказ",
                                OperationType.CreateOrder);

                if (order.Items == null || !order.Items.Any())
                    return Fail("Некорректные данные: заказ должен содержать хотя бы один товар",
                                OperationType.CreateOrder);

                if (order.CustomerId <= 0)
                    return Fail("Некорректные данные: укажите ID клиента",
                                OperationType.CreateOrder);

                if (string.IsNullOrWhiteSpace(order.ShippingAddress))
                    return Fail("Некорректные данные: укажите адрес доставки",
                                OperationType.CreateOrder);

                if (!_customers.Exists(order.CustomerId))
                    return Fail($"Клиент с ID {order.CustomerId} не найден",
                                OperationType.CreateOrder);

                lock (_stockLock)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → LOCK");

                    using (var conn = Database.Database.CreateConnection())
                    {
                        decimal totalPrice = 0;

                        foreach (var item in order.Items)
                        {
                            var product = _products.GetById(item.ProductId);
                            if (product == null)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK (товар не найден)");
                                return Fail($"Товар с ID {item.ProductId} не найден",
                                            OperationType.CreateOrder);
                            }

                            if (item.Quantity <= 0)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK (некорректное количество)");
                                return Fail($"Некорректное количество для товара '{product.Name}'",
                                            OperationType.CreateOrder);
                            }

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Available = {product.Stock} (товар '{product.Name}')");

                            if (product.Stock < item.Quantity)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → FAIL (недостаточно товара)");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                return Fail($"Недостаточно товара '{product.Name}'. В наличии: {product.Stock}, запрошено: {item.Quantity}",
                                            OperationType.CreateOrder);
                            }

                            item.Price = product.Price;
                            totalPrice += product.Price * item.Quantity;
                        }

                        order.CreatedAt = DateTime.Now;
                        order.Status = OrderStatus.Pending;
                        order.TotalPrice = totalPrice;

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op1: INSERT Order + OrderItems");
                        int newOrderId = _orders.Insert(conn, null, order);
                        order.Id = newOrderId;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op1 OK (заказ №{newOrderId})");

                        if (SimulateError)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → !!! ИСКУССТВЕННАЯ ОШИБКА между операциями !!!");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                            return Fail("Искусственная ошибка между операциями (без транзакции: заказ сохранён, остаток НЕ уменьшен)",
                                        OperationType.CreateOrder);
                        }

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op2: UPDATE Stock");
                        foreach (var item in order.Items)
                        {
                            bool ok = _products.TryDecreaseStock(conn, null, item.ProductId, item.Quantity);
                            if (!ok)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → FAIL (не удалось уменьшить остаток)");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");
                                return Fail($"Не удалось уменьшить остаток товара ID={item.ProductId}",
                                            OperationType.CreateOrder);
                            }
                        }
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → Op2 OK");

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → SUCCESS (заказ №{newOrderId})");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → UNLOCK");

                        LogEvent("OrderCreated",
                                 $"Заказ №{newOrderId} создан на сумму {order.TotalPrice} BYN");

                        return new Response
                        {
                            Success = true,
                            Message = $"Заказ №{newOrderId} успешно создан на сумму {order.TotalPrice} BYN",
                            Result = order,
                            Operation = OperationType.CreateOrder.ToString()
                        };
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                return Fail($"Ошибка целостности данных: {sqlEx.Message}",
                            OperationType.CreateOrder);
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при создании заказа: {ex.Message}",
                            OperationType.CreateOrder);
            }
        }

        public Response CancelOrder(object data)
        {
            try
            {
                if (data == null)
                    return Fail("Некорректные данные: укажите ID заказа",
                                OperationType.CancelOrder);

                int orderId = Convert.ToInt32(data);

                lock (_stockLock)
                {
                    var order = _orders.GetById(orderId);
                    if (order == null)
                        return Fail($"Заказ с ID {orderId} не найден",
                                    OperationType.CancelOrder);

                    if (order.Status == OrderStatus.Cancelled)
                        return Fail($"Заказ №{orderId} уже отменен",
                                    OperationType.CancelOrder);

                    if (order.Status == OrderStatus.Paid)
                        return Fail($"Нельзя отменить оплаченный заказ №{orderId}",
                                    OperationType.CancelOrder);

                    using (var conn = Database.Database.CreateConnection())
                    {
                        _orders.UpdateStatus(conn, null, orderId, OrderStatus.Cancelled);

                        foreach (var item in order.Items)
                            _products.IncreaseStock(conn, null, item.ProductId, item.Quantity);
                    }

                    LogEvent("OrderCancelled", $"Заказ №{orderId} отменен");

                    return new Response
                    {
                        Success = true,
                        Message = $"Заказ №{orderId} успешно отменен",
                        Result = order,
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }
            }
            catch (FormatException)
            {
                return Fail("Некорректные данные: ID заказа должен быть числом",
                            OperationType.CancelOrder);
            }
            catch (SqlException sqlEx)
            {
                return Fail($"Ошибка целостности данных: {sqlEx.Message}",
                            OperationType.CancelOrder);
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при отмене заказа: {ex.Message}",
                            OperationType.CancelOrder);
            }
        }

        public Response PayOrder(object data)
        {
            try
            {
                if (data == null)
                    return Fail("Некорректные данные: укажите ID заказа и способ оплаты",
                                OperationType.PayOrder);

                var paymentRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString());

                if (paymentRequest == null || !paymentRequest.ContainsKey("OrderId"))
                    return Fail("Некорректные данные: укажите ID заказа",
                                OperationType.PayOrder);

                int orderId = Convert.ToInt32(paymentRequest["OrderId"]);

                string method = "Банковская карта";
                if (paymentRequest.ContainsKey("PaymentMethod") && paymentRequest["PaymentMethod"] != null)
                    method = paymentRequest["PaymentMethod"].ToString();

                var order = _orders.GetById(orderId);
                if (order == null)
                    return Fail($"Заказ с ID {orderId} не найден", OperationType.PayOrder);

                if (order.Status == OrderStatus.Paid)
                    return Fail($"Заказ №{orderId} уже оплачен", OperationType.PayOrder);

                if (order.Status == OrderStatus.Cancelled)
                    return Fail($"Нельзя оплатить отмененный заказ №{orderId}", OperationType.PayOrder);

                var payment = new Payment
                {
                    OrderId = orderId,
                    Amount = order.TotalPrice,
                    PaymentMethod = method,
                    PaymentDate = DateTime.Now,
                    IsSuccessful = true
                };

                using (var conn = Database.Database.CreateConnection())
                {
                    payment.Id = _payments.Insert(conn, null, payment);
                    _orders.UpdateStatus(conn, null, orderId, OrderStatus.Paid);

                    using (var cmd = new SqlCommand("UPDATE dbo.Orders SET PaidAt = @d WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@d", DateTime.Now);
                        cmd.Parameters.AddWithValue("@id", orderId);
                        cmd.ExecuteNonQuery();
                    }
                }

                order.Status = OrderStatus.Paid;
                order.PaidAt = DateTime.Now;

                LogEvent("PaymentCompleted",
                         $"Заказ №{orderId} оплачен на {order.TotalPrice} BYN через {method}");

                return new Response
                {
                    Success = true,
                    Message = $"Заказ №{orderId} успешно оплачен на сумму {order.TotalPrice} BYN через {method}",
                    Result = new { Order = order, Payment = payment },
                    Operation = OperationType.PayOrder.ToString()
                };
            }
            catch (FormatException)
            {
                return Fail("Некорректные данные: ID заказа должен быть числом",
                            OperationType.PayOrder);
            }
            catch (SqlException sqlEx)
            {
                return Fail($"Ошибка целостности данных: {sqlEx.Message}",
                            OperationType.PayOrder);
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при оплате заказа: {ex.Message}",
                            OperationType.PayOrder);
            }
        }

        public Response GetServerTime()
        {
            try
            {
                DateTime serverTime = DateTime.UtcNow;
                int artificialDelayMs = 1000;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SERVER TIME → зафиксировано {serverTime:HH:mm:ss.fff} (UTC), задержка ответа {artificialDelayMs} мс");

                System.Threading.Thread.Sleep(artificialDelayMs);

                var timeResponse = new TimeResponse
                {
                    ServerTimeUtc = serverTime,
                    ArtificialDelayMs = artificialDelayMs
                };

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SERVER TIME → отправка ответа (задержка {artificialDelayMs} мс прошла)");

                return new Response
                {
                    Success = true,
                    Message = $"Время сервера: {serverTime:HH:mm:ss.fff} UTC",
                    Result = timeResponse,
                    Operation = OperationType.GetServerTime.ToString()
                };
            }
            catch (Exception ex)
            {
                return Fail($"Ошибка при получении времени сервера: {ex.Message}",
                            OperationType.GetServerTime);
            }
        }

        private static Response Fail(string message, OperationType op) => new Response
        {
            Success = false,
            Error = message,
            Operation = op.ToString()
        };

        private void LogEvent(string eventName, string description)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{eventName}] {description} ({DateTime.Now:HH:mm:ss})");
            Console.ResetColor();
        }
    }
}