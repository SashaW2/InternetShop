using System;
using Microsoft.Data.SqlClient;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Database.Repositories
{
    public class PaymentRepository
    {
        public int Insert(SqlConnection conn, SqlTransaction tx, Payment p)
        {
            using (var cmd = new SqlCommand(
                @"INSERT INTO dbo.Payments (OrderId, Amount, PaymentMethod, PaymentDate, IsSuccessful)
                  OUTPUT INSERTED.Id
                  VALUES (@oid, @amt, @method, @date, @ok)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@oid", p.OrderId);
                cmd.Parameters.AddWithValue("@amt", p.Amount);
                cmd.Parameters.AddWithValue("@method", p.PaymentMethod);
                cmd.Parameters.AddWithValue("@date", p.PaymentDate);
                cmd.Parameters.AddWithValue("@ok", p.IsSuccessful);
                return (int)cmd.ExecuteScalar();
            }
        }
    }
}