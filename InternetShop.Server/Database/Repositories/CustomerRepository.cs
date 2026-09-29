using System;
using Microsoft.Data.SqlClient;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Database.Repositories
{
    public class CustomerRepository
    {
        public bool Exists(int customerId)
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Customers WHERE Id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", customerId);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public Customer GetById(int customerId)
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                @"SELECT Id, FirstName, LastName, Email, Phone, RegisteredAt
                  FROM dbo.Customers WHERE Id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", customerId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new Customer
                    {
                        Id = reader.GetInt32(0),
                        FirstName = reader.GetString(1),
                        LastName = reader.GetString(2),
                        Email = reader.GetString(3),
                        Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
                        RegisteredAt = reader.GetDateTime(5)
                    };
                }
            }
        }
    }
}