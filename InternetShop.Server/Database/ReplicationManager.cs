using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Database
{
    public class ReplicationManager
    {
        private static readonly object _lock = new object();

        public bool ReplicateStockChange(int productId, int newStock)
        {
            lock (_lock)
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replication -- START");

                    using (var conn = Database.CreateReplicaConnection())
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Products SET Stock = @stock WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@stock", newStock);
                        cmd.Parameters.AddWithValue("@id", productId);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows == 1)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replica -- UPDATE (ProductId={productId}, Stock={newStock})");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replication -- SUCCESS");
                            return true;
                        }

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replication -- FAIL");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Replication -- ERROR: {ex.Message}");
                    return false;
                }
            }
        }

        public (int primaryStock, int replicaStock)? GetProductStockBoth(int productId)
        {
            try
            {
                int primaryStock, replicaStock;

                using (var conn = new SqlConnection(Database.GetPrimaryConnectionStringForSync()))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT Stock FROM dbo.Products WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", productId);
                        var result = cmd.ExecuteScalar();
                        if (result == null) return null;
                        primaryStock = (int)result;
                    }
                }

                using (var conn = Database.CreateReplicaConnection())
                using (var cmd = new SqlCommand("SELECT Stock FROM dbo.Products WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", productId);
                    var result = cmd.ExecuteScalar();
                    if (result == null) return null;
                    replicaStock = (int)result;
                }

                return (primaryStock, replicaStock);
            }
            catch { return null; }
        }

        public bool CheckConsistency(out string report)
        {
            report = "";
            try
            {
                var primaryProducts = GetProductsFromPrimary();
                var replicaProducts = GetProductsFromReplica();

                if (primaryProducts.Count != replicaProducts.Count)
                {
                    report = $"Количество товаров не совпадает: Primary={primaryProducts.Count}, Replica={replicaProducts.Count}";
                    return false;
                }

                foreach (var p in primaryProducts)
                {
                    var rp = replicaProducts.Find(x => x.Id == p.Id);
                    if (rp == null)
                    {
                        report = $"Товар с Id={p.Id} отсутствует в Replica";
                        return false;
                    }
                    if (rp.Stock != p.Stock)
                    {
                        report = $"Несовпадение Stock для товара Id={p.Id}: Primary={p.Stock}, Replica={rp.Stock}";
                        return false;
                    }
                }

                report = "Primary и Replica согласованы";
                return true;
            }
            catch (Exception ex)
            {
                report = $"Ошибка проверки: {ex.Message}";
                return false;
            }
        }

        public void SynchronizeReplicaToPrimary()
        {
            lock (_lock)
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Synchronization (Replica → Primary) -- START");

                    foreach (var p in GetProductsFromReplica())
                        UpdateProductInPrimary(p);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Synchronization (Replica → Primary) -- SUCCESS");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Synchronization -- ERROR: {ex.Message}");
                }
            }
        }

        private List<Product> GetProductsFromPrimary()
        {
            var list = new List<Product>();
            using (var conn = Database.CreatePrimaryConnection())
            using (var cmd = new SqlCommand("SELECT Id, Name, Description, Price, Stock, Category FROM dbo.Products", conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    list.Add(new Product
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Price = reader.GetDecimal(3),
                        Stock = reader.GetInt32(4),
                        Category = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
            return list;
        }

        private List<Product> GetProductsFromReplica()
        {
            var list = new List<Product>();
            using (var conn = Database.CreateReplicaConnection())
            using (var cmd = new SqlCommand("SELECT Id, Name, Description, Price, Stock, Category FROM dbo.Products", conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    list.Add(new Product
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Price = reader.GetDecimal(3),
                        Stock = reader.GetInt32(4),
                        Category = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
            return list;
        }

        private void UpdateProductInPrimary(Product p)
        {
            using (var conn = new SqlConnection(Database.GetPrimaryConnectionStringForSync()))
            {
                conn.Open();

                using (var cmd = new SqlCommand(
                    "UPDATE dbo.Products SET Stock = @stock WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@stock", p.Stock);
                    cmd.Parameters.AddWithValue("@id", p.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}