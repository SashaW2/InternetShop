using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InternetShop.Shared.Models;
using InternetShop.Shared.Enums;

namespace InternetShop.Server.Database.Repositories
{
    public class OrderRepository
    {
        public int Insert(SqlConnection conn, SqlTransaction tx, Order order)
        {
            int newOrderId;

            using (var cmd = new SqlCommand(
                @"INSERT INTO dbo.Orders (CustomerId, CreatedAt, Status, TotalPrice, ShippingAddress)
                  OUTPUT INSERTED.Id
                  VALUES (@cid, @created, @status, @total, @addr)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@cid", order.CustomerId);
                cmd.Parameters.AddWithValue("@created", order.CreatedAt);
                cmd.Parameters.AddWithValue("@status", (int)order.Status);
                cmd.Parameters.AddWithValue("@total", order.TotalPrice);
                cmd.Parameters.AddWithValue("@addr", order.ShippingAddress);
                newOrderId = (int)cmd.ExecuteScalar();
            }

            foreach (var item in order.Items)
            {
                using (var cmd = new SqlCommand(
                    @"INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, Price)
                      VALUES (@oid, @pid, @qty, @price)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@oid", newOrderId);
                    cmd.Parameters.AddWithValue("@pid", item.ProductId);
                    cmd.Parameters.AddWithValue("@qty", item.Quantity);
                    cmd.Parameters.AddWithValue("@price", item.Price);
                    cmd.ExecuteNonQuery();
                }
            }

            return newOrderId;
        }

        public List<Order> GetAll()
        {
            var result = new List<Order>();
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                @"SELECT Id, CustomerId, CreatedAt, Status, TotalPrice, ShippingAddress, PaidAt
                  FROM dbo.Orders ORDER BY Id", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new Order
                    {
                        Id = reader.GetInt32(0),
                        CustomerId = reader.GetInt32(1),
                        CreatedAt = reader.GetDateTime(2),
                        Status = (OrderStatus)reader.GetInt32(3),
                        TotalPrice = reader.GetDecimal(4),
                        ShippingAddress = reader.GetString(5),
                        PaidAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                    });
                }
            }
            return result;
        }

        public Order GetById(int id)
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                @"SELECT Id, CustomerId, CreatedAt, Status, TotalPrice, ShippingAddress, PaidAt
                  FROM dbo.Orders WHERE Id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    var order = new Order
                    {
                        Id = reader.GetInt32(0),
                        CustomerId = reader.GetInt32(1),
                        CreatedAt = reader.GetDateTime(2),
                        Status = (OrderStatus)reader.GetInt32(3),
                        TotalPrice = reader.GetDecimal(4),
                        ShippingAddress = reader.GetString(5),
                        PaidAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                    };
                    reader.Close();

                    using (var cmd2 = new SqlCommand(
                        "SELECT Id, ProductId, Quantity, Price FROM dbo.OrderItems WHERE OrderId = @oid", conn))
                    {
                        cmd2.Parameters.AddWithValue("@oid", id);
                        using (var r2 = cmd2.ExecuteReader())
                        {
                            while (r2.Read())
                            {
                                order.Items.Add(new OrderItem
                                {
                                    Id = r2.GetInt32(0),
                                    ProductId = r2.GetInt32(1),
                                    Quantity = r2.GetInt32(2),
                                    Price = r2.GetDecimal(3)
                                });
                            }
                        }
                    }
                    return order;
                }
            }
        }

        public bool UpdateStatus(SqlConnection conn, SqlTransaction tx, int orderId, OrderStatus status)
        {
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Orders SET Status = @st WHERE Id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@st", (int)status);
                cmd.Parameters.AddWithValue("@id", orderId);
                return cmd.ExecuteNonQuery() == 1;
            }
        }
    }
}