using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Database.Repositories
{
    public class ProductRepository
    {
        public List<Product> GetAll()
        {
            var result = new List<Product>();
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT Id, Name, Description, Price, Stock, Category FROM dbo.Products ORDER BY Id", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(Map(reader));
                }
            }
            return result;
        }

        public Product GetById(int id)
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT Id, Name, Description, Price, Stock, Category FROM dbo.Products WHERE Id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public bool TryDecreaseStock(SqlConnection conn, SqlTransaction tx, int productId, int quantity)
        {
            using (var cmd = new SqlCommand(
                @"UPDATE dbo.Products
                  SET Stock = Stock - @qty
                  WHERE Id = @id AND Stock >= @qty", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", productId);
                cmd.Parameters.AddWithValue("@qty", quantity);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public void IncreaseStock(SqlConnection conn, SqlTransaction tx, int productId, int quantity)
        {
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Products SET Stock = Stock + @qty WHERE Id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", productId);
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.ExecuteNonQuery();
            }
        }

        private static Product Map(SqlDataReader r) => new Product
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            Description = r.IsDBNull(2) ? null : r.GetString(2),
            Price = r.GetDecimal(3),
            Stock = r.GetInt32(4),
            Category = r.IsDBNull(5) ? null : r.GetString(5),
        };
    }
}