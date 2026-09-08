using System;
using System.Collections.Generic;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;

namespace InternetShop.Client
{
    public static class RequestBuilder
    {
        public static Request BuildGetProducts()
        {
            return new Request
            {
                Operation = OperationType.GetProducts,
                Data = null
            };
        }

        public static Request BuildCreateOrder(int customerId, List<OrderItem> items, string address)
        {
            var order = new Order
            {
                CustomerId = customerId,
                Items = items,
                ShippingAddress = address,
                CreatedAt = DateTime.Now
            };

            return new Request
            {
                Operation = OperationType.CreateOrder,
                Data = order
            };
        }

        public static Request BuildCancelOrder(int orderId)
        {
            return new Request
            {
                Operation = OperationType.CancelOrder,
                Data = orderId
            };
        }

        public static Request BuildPayOrder(int orderId, string paymentMethod)
        {
            var paymentData = new Dictionary<string, object>
            {
                { "OrderId", orderId },
                { "PaymentMethod", paymentMethod }
            };

            return new Request
            {
                Operation = OperationType.PayOrder,
                Data = paymentData
            };
        }
    }
}