using System;
using Microsoft.Data.SqlClient;
using Xunit;
using InternetShop.Server.Database;

namespace InternetShop.Tests
{
    [Collection("Sequential")]
    public class ReplicationTests
    {
        private readonly ReplicationManager _rm = new ReplicationManager();
        private const int TestProductId = 2;

        private int GetOriginalStock()
        {
            using (var conn = new SqlConnection(Database.GetPrimaryConnectionStringForSync()))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Stock FROM dbo.Products WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", TestProductId);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private void RestoreOriginalStock(int originalStock)
        {
            using (var conn = new SqlConnection(Database.GetPrimaryConnectionStringForSync()))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Products SET Stock = @s WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@s", originalStock);
                    cmd.Parameters.AddWithValue("@id", TestProductId);
                    cmd.ExecuteNonQuery();
                }
            }

            using (var conn = new SqlConnection(Database.GetReplicaConnectionStringForSync()))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Products SET Stock = @s WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@s", originalStock);
                    cmd.Parameters.AddWithValue("@id", TestProductId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdatePrimaryStock(int newStock)
        {
            using (var conn = new SqlConnection(Database.GetPrimaryConnectionStringForSync()))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE dbo.Products SET Stock = @s WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@s", newStock);
                    cmd.Parameters.AddWithValue("@id", TestProductId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [Fact]
        public void InitialState_Consistent()
        {
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);
            Assert.True(_rm.CheckConsistency(out string report), report);
        }

        [Fact]
        public void ReplicateStockChange_MakesCopiesEqual()
        {
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);

            int originalStock = GetOriginalStock();

            try
            {
                int newStock = originalStock + 100;
                UpdatePrimaryStock(newStock);
                _rm.ReplicateStockChange(TestProductId, newStock);

                var stocks = _rm.GetProductStockBoth(TestProductId);
                Assert.NotNull(stocks);
                Assert.Equal(stocks.Value.primaryStock, stocks.Value.replicaStock);
            }
            finally
            {
                RestoreOriginalStock(originalStock);
            }
        }

        [Fact]
        public void ChangeData_HandledCorrectly()
        {
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);

            int originalStock = GetOriginalStock();

            try
            {
                int newStock = originalStock + 7;
                UpdatePrimaryStock(newStock);
                _rm.ReplicateStockChange(TestProductId, newStock);

                var stocks = _rm.GetProductStockBoth(TestProductId);
                Assert.NotNull(stocks);
                Assert.Equal(newStock, stocks.Value.primaryStock);
                Assert.Equal(newStock, stocks.Value.replicaStock);
            }
            finally
            {
                RestoreOriginalStock(originalStock);
            }
        }

        [Fact]
        public void PrimaryUnavailable_DetectedCorrectly()
        {
            Database.SetPrimaryAvailable(false);
            Assert.False(Database.IsPrimaryAvailable());
            Database.SetPrimaryAvailable(true);
        }

        [Fact]
        public void ReplicaAvailable_SelectedWhenPrimaryDown()
        {
            Database.SetPrimaryAvailable(false);
            Database.SetReplicaAvailable(true);

            using (var conn = Database.CreatePrimaryConnection())
                Assert.NotNull(conn);

            Database.SetPrimaryAvailable(true);
        }

        [Fact]
        public void Failover_RequestProcessedThroughReplica()
        {
            Database.SetPrimaryAvailable(false);

            using (var conn = Database.CreatePrimaryConnection())
                Assert.NotNull(conn);

            Database.SetPrimaryAvailable(true);
        }

        [Fact]
        public void PrimaryRecovery_AvailableAgain()
        {
            Database.SetPrimaryAvailable(false);
            Assert.False(Database.IsPrimaryAvailable());

            Database.SetPrimaryAvailable(true);
            Assert.True(Database.IsPrimaryAvailable());
        }

        [Fact]
        public void SyncAfterRecovery_DataConsistent()
        {
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);

            int originalStock = GetOriginalStock();

            try
            {
                _rm.SynchronizeReplicaToPrimary();
                Assert.True(_rm.CheckConsistency(out string report), report);
            }
            finally
            {
                RestoreOriginalStock(originalStock);
            }
        }

        [Fact]
        public void BothUnavailable_Throws()
        {
            Database.SetPrimaryAvailable(false);
            Database.SetReplicaAvailable(false);
            Assert.Throws<Exception>(() => { using (var conn = Database.CreatePrimaryConnection()) { } });
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);
        }

        [Fact]
        public void DataConsistent_AfterFullSync()
        {
            Database.SetPrimaryAvailable(true);
            Database.SetReplicaAvailable(true);

            int originalStock = GetOriginalStock();

            try
            {
                _rm.SynchronizeReplicaToPrimary();

                var stocks = _rm.GetProductStockBoth(TestProductId);
                Assert.NotNull(stocks);
                Assert.Equal(stocks.Value.primaryStock, stocks.Value.replicaStock);
            }
            finally
            {
                RestoreOriginalStock(originalStock);
            }
        }
    }

    [CollectionDefinition("Sequential", DisableParallelization = true)]
    public class SequentialCollection { }
}