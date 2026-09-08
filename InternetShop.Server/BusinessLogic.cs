using System;
using System.Linq;
using System.Collections.Generic;
using InternetShop.Shared.Models;
using InternetShop.Shared.Enums;
using InternetShop.Shared.DTO;

namespace InternetShop.Server
{
    public class BusinessLogic
    {
        public Response GetProducts()
        {
            try
            {
                var products = DataStore.Products;
                return new Response
                {
                    Success = true,
                    Message = $"Найдено {products.Count} товаров",
                    Result = products,
                    Operation = OperationType.GetProducts.ToString()
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Ошибка при получении товаров: {ex.Message}",
                    Operation = OperationType.GetProducts.ToString()
                };
            }
        }

        public Response CreateOrder(object data)
        {
            try
            {
                if (data == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: заказ не может быть пустым",
                        Operation = OperationType.CreateOrder.ToString()
                    };
                }

                var order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(data.ToString());

                if (order.Items == null || !order.Items.Any())
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: заказ должен содержать хотя бы один товар",
                        Operation = OperationType.CreateOrder.ToString()
                    };
                }

                if (order.CustomerId <= 0)
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: укажите ID клиента",
                        Operation = OperationType.CreateOrder.ToString()
                    };
                }

                var customer = DataStore.Customers.FirstOrDefault(c => c.Id == order.CustomerId);
                if (customer == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Клиент с ID {order.CustomerId} не найден",
                        Operation = OperationType.CreateOrder.ToString()
                    };
                }

                if (string.IsNullOrWhiteSpace(order.ShippingAddress))
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: укажите адрес доставки",
                        Operation = OperationType.CreateOrder.ToString()
                    };
                }

                int itemId = 1;
                foreach (var item in order.Items)
                {
                    var product = DataStore.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product == null)
                    {
                        return new Response
                        {
                            Success = false,
                            Error = $"Товар с ID {item.ProductId} не найден",
                            Operation = OperationType.CreateOrder.ToString()
                        };
                    }

                    if (product.Stock < item.Quantity)
                    {
                        return new Response
                        {
                            Success = false,
                            Error = $"Недостаточно товара '{product.Name}' на складе. В наличии: {product.Stock}, запрошено: {item.Quantity}",
                            Operation = OperationType.CreateOrder.ToString()
                        };
                    }

                    item.Id = itemId++;
                    item.Price = product.Price;
                }

                var newOrder = new Order
                {
                    Id = DataStore.GetNextOrderId(),
                    CustomerId = order.CustomerId,
                    Items = order.Items,
                    CreatedAt = DateTime.Now,
                    Status = OrderStatus.Pending,
                    TotalPrice = order.Items.Sum(i => i.Price * i.Quantity),
                    ShippingAddress = order.ShippingAddress
                };

                DataStore.Orders.Add(newOrder);

                foreach (var item in order.Items)
                {
                    var product = DataStore.Products.First(p => p.Id == item.ProductId);
                    product.Stock -= item.Quantity;
                }

                LogEvent("OrderCreated", $"Заказ №{newOrder.Id} создан клиентом {customer.FirstName} {customer.LastName} на сумму {newOrder.TotalPrice} BYN");
                LogEvent("OrderCreated", $"  Адрес доставки: {newOrder.ShippingAddress}");

                foreach (var item in order.Items)
                {
                    var product = DataStore.Products.First(p => p.Id == item.ProductId);
                    LogEvent("StockUpdated", $"Товар '{product.Name}'. Остаток: {product.Stock}");
                }

                return new Response
                {
                    Success = true,
                    Message = $"Заказ №{newOrder.Id} успешно создан на сумму {newOrder.TotalPrice} BYN",
                    Result = newOrder,
                    Operation = OperationType.CreateOrder.ToString()
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Ошибка при создании заказа: {ex.Message}",
                    Operation = OperationType.CreateOrder.ToString()
                };
            }
        }
        public Response CancelOrder(object data)
        {
            try
            {
                if (data == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: укажите ID заказа",
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }

                int orderId = Convert.ToInt32(data);

                var order = DataStore.Orders.FirstOrDefault(o => o.Id == orderId);
                if (order == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Заказ с ID {orderId} не найден",
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Заказ №{orderId} уже отменен",
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }

                if (order.Status == OrderStatus.Paid)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Нельзя отменить оплаченный заказ №{orderId}",
                        Operation = OperationType.CancelOrder.ToString()
                    };
                }

                order.Status = OrderStatus.Cancelled;

                foreach (var item in order.Items)
                {
                    var product = DataStore.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.Quantity;
                        LogEvent("StockUpdated", $"Возврат товара '{product.Name}' на склад. Остаток: {product.Stock}");
                    }
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
            catch (FormatException)
            {
                return new Response
                {
                    Success = false,
                    Error = "Некорректные данные: ID заказа должен быть числом",
                    Operation = OperationType.CancelOrder.ToString()
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Ошибка при отмене заказа: {ex.Message}",
                    Operation = OperationType.CancelOrder.ToString()
                };
            }
        }

        public Response PayOrder(object data)
        {
            try
            {
                if (data == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: укажите ID заказа и способ оплаты",
                        Operation = OperationType.PayOrder.ToString()
                    };
                }

                var paymentRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString());

                if (!paymentRequest.ContainsKey("OrderId"))
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректные данные: укажите ID заказа",
                        Operation = OperationType.PayOrder.ToString()
                    };
                }

                int orderId = Convert.ToInt32(paymentRequest["OrderId"]);

                string paymentMethod = "Банковская карта";
                if (paymentRequest.ContainsKey("PaymentMethod") && paymentRequest["PaymentMethod"] != null)
                {
                    paymentMethod = paymentRequest["PaymentMethod"].ToString();
                }

                var order = DataStore.Orders.FirstOrDefault(o => o.Id == orderId);
                if (order == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Заказ с ID {orderId} не найден",
                        Operation = OperationType.PayOrder.ToString()
                    };
                }

                if (order.Status == OrderStatus.Paid)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Заказ №{orderId} уже оплачен",
                        Operation = OperationType.PayOrder.ToString()
                    };
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    return new Response
                    {
                        Success = false,
                        Error = $"Нельзя оплатить отмененный заказ №{orderId}",
                        Operation = OperationType.PayOrder.ToString()
                    };
                }

                var payment = new Payment
                {
                    Id = DataStore.GetNextPaymentId(),
                    OrderId = orderId,
                    Amount = order.TotalPrice,
                    PaymentMethod = paymentMethod,
                    PaymentDate = DateTime.Now,
                    IsSuccessful = true
                };

                DataStore.Payments.Add(payment);

                order.Status = OrderStatus.Paid;
                order.PaidAt = DateTime.Now;

                LogEvent("PaymentCompleted", $"Заказ №{orderId} оплачен на сумму {order.TotalPrice} BYN через {paymentMethod}");

                return new Response
                {
                    Success = true,
                    Message = $"Заказ №{orderId} успешно оплачен на сумму {order.TotalPrice} BYN через {paymentMethod}",
                    Result = new { Order = order, Payment = payment },
                    Operation = OperationType.PayOrder.ToString()
                };
            }
            catch (FormatException)
            {
                return new Response
                {
                    Success = false,
                    Error = "Некорректные данные: ID заказа должен быть числом",
                    Operation = OperationType.PayOrder.ToString()
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Ошибка при оплате заказа: {ex.Message}",
                    Operation = OperationType.PayOrder.ToString()
                };
            }
        }

        private void LogEvent(string eventName, string description)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{eventName}] {description} ({DateTime.Now:HH:mm:ss})");
            Console.ResetColor();
        }
    }
}