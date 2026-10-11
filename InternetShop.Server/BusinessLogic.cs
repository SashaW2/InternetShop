using System;
using System.Collections.Generic;
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
        private readonly ReplicationManager _replicationManager = new ReplicationManager();

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
            catch (SqlException sqlEx) { return Fail($"Ошибка БД: {sqlEx.Message}", OperationType.GetProducts); }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.GetProducts); }
        }

        public Response UpdateStock(object data, int clientId = 0)
        {
            try
            {
                if (data == null)
                    return Fail("Укажите ProductId и NewStock", OperationType.UpdateStock);

                var req = Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateStockRequest>(data.ToString());

                if (req == null) return Fail("Некорректные данные", OperationType.UpdateStock);
                if (req.ProductId <= 0) return Fail("ProductId должен быть > 0", OperationType.UpdateStock);
                if (req.NewStock < 0) return Fail("Остаток не может быть отрицательным", OperationType.UpdateStock);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} -- Update Stock (ProductId={req.ProductId}, NewStock={req.NewStock})");

                lock (_stockLock)
                {
                    var product = _products.GetById(req.ProductId);
                    if (product == null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- FAIL (товар не найден)");
                        return Fail($"Товар с ID {req.ProductId} не найден", OperationType.UpdateStock);
                    }

                    using (var conn = Database.Database.CreatePrimaryConnection())
                    using (var cmd = new SqlCommand("UPDATE dbo.Products SET Stock = @stock WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@stock", req.NewStock);
                        cmd.Parameters.AddWithValue("@id", req.ProductId);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows != 1)
                            return Fail("Не удалось обновить остаток", OperationType.UpdateStock);
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- UPDATE (ProductId={req.ProductId}, Stock={req.NewStock})");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- SUCCESS");

                    if (Database.Database.IsPrimaryAvailable() && Database.Database.IsReplicaAvailable())
                    {
                        _replicationManager.ReplicateStockChange(req.ProductId, req.NewStock);
                    }

                    var stocks = _replicationManager.GetProductStockBoth(req.ProductId);
                    if (stocks.HasValue)
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Проверка: Primary={stocks.Value.primaryStock}, Replica={stocks.Value.replicaStock}");

                    return new Response
                    {
                        Success = true,
                        Message = $"Остаток товара Id={req.ProductId} обновлён до {req.NewStock}",
                        Result = new { ProductId = req.ProductId, NewStock = req.NewStock },
                        Operation = OperationType.UpdateStock.ToString()
                    };
                }
            }
            catch (SqlException sqlEx) { return Fail($"Ошибка БД: {sqlEx.Message}", OperationType.UpdateStock); }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.UpdateStock); }
        }

        public Response CreateOrder(object data, int clientId = 0)
        {
            try
            {
                if (data == null) return Fail("Заказ не может быть пустым", OperationType.CreateOrder);

                var order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(data.ToString());

                if (order == null) return Fail("Некорректные данные", OperationType.CreateOrder);
                if (order.Items == null || !order.Items.Any()) return Fail("Заказ пуст", OperationType.CreateOrder);
                if (order.CustomerId <= 0) return Fail("Укажите ID клиента", OperationType.CreateOrder);
                if (string.IsNullOrWhiteSpace(order.ShippingAddress)) return Fail("Укажите адрес", OperationType.CreateOrder);
                if (!_customers.Exists(order.CustomerId)) return Fail($"Клиент {order.CustomerId} не найден", OperationType.CreateOrder);

                lock (_stockLock)
                {
                    using (var conn = Database.Database.CreatePrimaryConnection())
                    using (var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                    {
                        try
                        {
                            decimal totalPrice = 0;

                            foreach (var item in order.Items)
                            {
                                var product = _products.GetById(item.ProductId);
                                if (product == null) { tx.Rollback(); return Fail($"Товар {item.ProductId} не найден", OperationType.CreateOrder); }
                                if (item.Quantity <= 0) { tx.Rollback(); return Fail("Количество > 0", OperationType.CreateOrder); }
                                if (product.Stock < item.Quantity) { tx.Rollback(); return Fail($"Недостаточно '{product.Name}'", OperationType.CreateOrder); }

                                item.Price = product.Price;
                                totalPrice += product.Price * item.Quantity;
                            }

                            order.CreatedAt = DateTime.Now;
                            order.Status = OrderStatus.Pending;
                            order.TotalPrice = totalPrice;

                            int newOrderId = _orders.Insert(conn, tx, order);
                            order.Id = newOrderId;

                            foreach (var item in order.Items)
                            {
                                bool ok = _products.TryDecreaseStock(conn, tx, item.ProductId, item.Quantity);
                                if (!ok) { tx.Rollback(); return Fail("Не удалось уменьшить остаток", OperationType.CreateOrder); }
                            }

                            tx.Commit();

                            if (Database.Database.IsPrimaryAvailable() && Database.Database.IsReplicaAvailable())
                            {
                                foreach (var item in order.Items)
                                {
                                    var product = _products.GetById(item.ProductId);
                                    if (product != null)
                                    {
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- UPDATE (ProductId={product.Id}, Stock={product.Stock})");
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- SUCCESS");
                                        _replicationManager.ReplicateStockChange(product.Id, product.Stock);
                                    }
                                }
                            }

                            LogEvent("OrderCreated", $"Заказ №{newOrderId} на {order.TotalPrice} BYN");

                            return new Response
                            {
                                Success = true,
                                Message = $"Заказ №{newOrderId} создан на {order.TotalPrice} BYN",
                                Result = order,
                                Operation = OperationType.CreateOrder.ToString()
                            };
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); } catch { }
                            return Fail($"Ошибка транзакции: {ex.Message}", OperationType.CreateOrder);
                        }
                    }
                }
            }
            catch (SqlException sqlEx) { return Fail($"Ошибка БД: {sqlEx.Message}", OperationType.CreateOrder); }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.CreateOrder); }
        }

        public Response CancelOrder(object data, int clientId = 0)
        {
            try
            {
                if (data == null) return Fail("Укажите ID заказа", OperationType.CancelOrder);

                int orderId = Convert.ToInt32(data);

                lock (_stockLock)
                {
                    var order = _orders.GetById(orderId);
                    if (order == null) return Fail($"Заказ {orderId} не найден", OperationType.CancelOrder);
                    if (order.Status == OrderStatus.Cancelled) return Fail("Уже отменен", OperationType.CancelOrder);
                    if (order.Status == OrderStatus.Paid) return Fail("Нельзя отменить оплаченный", OperationType.CancelOrder);

                    using (var conn = Database.Database.CreatePrimaryConnection())
                    {
                        _orders.UpdateStatus(conn, null, orderId, OrderStatus.Cancelled);
                        foreach (var item in order.Items)
                            _products.IncreaseStock(conn, null, item.ProductId, item.Quantity);
                    }

                    if (Database.Database.IsPrimaryAvailable() && Database.Database.IsReplicaAvailable())
                    {
                        foreach (var item in order.Items)
                        {
                            var product = _products.GetById(item.ProductId);
                            if (product != null)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- UPDATE (ProductId={product.Id}, Stock={product.Stock})");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Primary -- SUCCESS");
                                _replicationManager.ReplicateStockChange(product.Id, product.Stock);
                            }
                        }
                    }

                    LogEvent("OrderCancelled", $"Заказ №{orderId} отменен");

                    return new Response
                    {
                        Success = true,
                        Message = $"Заказ №{orderId} отменен",
                        Result = order,
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }
            }
            catch (FormatException) { return Fail("ID должен быть числом", OperationType.CancelOrder); }
            catch (SqlException sqlEx) { return Fail($"Ошибка БД: {sqlEx.Message}", OperationType.CancelOrder); }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.CancelOrder); }
        }

        public Response PayOrder(object data)
        {
            try
            {
                if (data == null) return Fail("Укажите ID заказа", OperationType.PayOrder);

                var paymentRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString());
                if (paymentRequest == null || !paymentRequest.ContainsKey("OrderId"))
                    return Fail("Укажите ID заказа", OperationType.PayOrder);

                int orderId = Convert.ToInt32(paymentRequest["OrderId"]);
                string method = "Банковская карта";
                if (paymentRequest.ContainsKey("PaymentMethod") && paymentRequest["PaymentMethod"] != null)
                    method = paymentRequest["PaymentMethod"].ToString();

                var order = _orders.GetById(orderId);
                if (order == null) return Fail($"Заказ {orderId} не найден", OperationType.PayOrder);
                if (order.Status == OrderStatus.Paid) return Fail("Уже оплачен", OperationType.PayOrder);
                if (order.Status == OrderStatus.Cancelled) return Fail("Отменен", OperationType.PayOrder);

                var payment = new Payment
                {
                    OrderId = orderId,
                    Amount = order.TotalPrice,
                    PaymentMethod = method,
                    PaymentDate = DateTime.Now,
                    IsSuccessful = true
                };

                using (var conn = Database.Database.CreatePrimaryConnection())
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

                LogEvent("PaymentCompleted", $"Заказ №{orderId} оплачен");

                return new Response
                {
                    Success = true,
                    Message = $"Заказ №{orderId} оплачен на {order.TotalPrice} BYN",
                    Result = new { Order = order, Payment = payment },
                    Operation = OperationType.PayOrder.ToString()
                };
            }
            catch (FormatException) { return Fail("ID должен быть числом", OperationType.PayOrder); }
            catch (SqlException sqlEx) { return Fail($"Ошибка БД: {sqlEx.Message}", OperationType.PayOrder); }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.PayOrder); }
        }

        public Response GetServerTime()
        {
            try
            {
                DateTime serverTime = DateTime.UtcNow;
                System.Threading.Thread.Sleep(1000);

                return new Response
                {
                    Success = true,
                    Message = $"Время сервера: {serverTime:HH:mm:ss.fff} UTC",
                    Result = new TimeResponse { ServerTimeUtc = serverTime, ArtificialDelayMs = 1000 },
                    Operation = OperationType.GetServerTime.ToString()
                };
            }
            catch (Exception ex) { return Fail($"Ошибка: {ex.Message}", OperationType.GetServerTime); }
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