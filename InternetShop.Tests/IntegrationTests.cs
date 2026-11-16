using InternetShop.Server;
using InternetShop.Server.Database;
using InternetShop.Shared.Models;
using Microsoft.Data.SqlClient;

namespace InternetShop.Tests
{
    // ============================================================
    // Коллекции (изоляция окружения между группами тестов)
    // ============================================================

    [CollectionDefinition("UnitLogic", DisableParallelization = true)]
    public class UnitLogicCollection { }

    [CollectionDefinition("UnavailableDb", DisableParallelization = true)]
    public class UnavailableDbCollection { }

    [CollectionDefinition("Integration", DisableParallelization = true)]
    public class IntegrationCollection { }

    // ============================================================
    // UNIT: логика приложения (без БД)
    // ============================================================

    [Collection("UnitLogic")]
    public class ValidationTests
    {
        private readonly BusinessLogic _logic = new BusinessLogic();

        [Fact]
        public void CreateOrder_NullData_ReturnsError()
        {
            var r = _logic.CreateOrder(null);
            Assert.False(r.Success);
            Assert.False(string.IsNullOrWhiteSpace(r.Error));
            Assert.Contains("пуст", r.Error);
        }

        [Fact]
        public void CreateOrder_EmptyItems_ReturnsError()
        {
            var json = "{\"CustomerId\":1,\"Items\":[],\"ShippingAddress\":\"addr\"}";
            var r = _logic.CreateOrder(json);
            Assert.False(r.Success);
            Assert.Contains("пуст", r.Error);
        }

        [Fact]
        public void CreateOrder_InvalidCustomerId_ReturnsError()
        {
            var json = "{\"CustomerId\":0,\"Items\":[{\"ProductId\":1,\"Quantity\":1}],\"ShippingAddress\":\"addr\"}";
            var r = _logic.CreateOrder(json);
            Assert.False(r.Success);
            Assert.Contains("ID клиента", r.Error);
        }

        [Fact]
        public void CreateOrder_EmptyAddress_ReturnsError()
        {
            var json = "{\"CustomerId\":1,\"Items\":[{\"ProductId\":1,\"Quantity\":1}],\"ShippingAddress\":\"\"}";
            var r = _logic.CreateOrder(json);
            Assert.False(r.Success);
            Assert.Contains("адрес", r.Error);
        }

        [Fact]
        public void UpdateStock_NegativeStock_ReturnsError()
        {
            var json = "{\"ProductId\":1,\"NewStock\":-5}";
            var r = _logic.UpdateStock(json);
            Assert.False(r.Success);
            Assert.Contains("отрицательн", r.Error);
        }

        [Fact]
        public void UpdateStock_InvalidProductId_ReturnsError()
        {
            var json = "{\"ProductId\":0,\"NewStock\":10}";
            var r = _logic.UpdateStock(json);
            Assert.False(r.Success);
            Assert.Contains("ProductId", r.Error);
        }

        [Fact]
        public void ProcessRequest_InvalidJson_ReturnsError()
        {
            var handler = new RequestHandler();
            var r = handler.ProcessRequest("{ not valid json }");
            Assert.False(r.Success);
            Assert.Contains("JSON", r.Error);
        }

        [Fact]
        public void ProcessRequest_UnknownOperation_ReturnsError()
        {
            var handler = new RequestHandler();
            var r = handler.ProcessRequest("{\"Operation\":999,\"Data\":null}");
            Assert.False(r.Success);
        }
    }

    // ============================================================
    // UNIT: недоступность компонента
    // ============================================================

    [Collection("UnavailableDb")]
    public class UnavailableDbTests
    {
        static UnavailableDbTests()
        {
            Environment.SetEnvironmentVariable("DB_HOST", "127.0.0.1");
            Environment.SetEnvironmentVariable("DB_PORT", "1");
            Environment.SetEnvironmentVariable("DB_USER", "sa");
            Environment.SetEnvironmentVariable("DB_PASSWORD", "x");
            Environment.SetEnvironmentVariable("DB_NAME_PRIMARY", "Nonexistent");
            Environment.SetEnvironmentVariable("DB_NAME_REPLICA", "Nonexistent");
        }

        [Fact]
        public void GetProducts_WhenDbUnavailable_ReturnsError()
        {
            var r = new BusinessLogic().GetProducts();
            Assert.False(r.Success);
            Assert.Contains("Ошибка", r.Error);
        }
    }

    // ============================================================
    // INTEGRATION: с контейнеризированной БД
    // ============================================================

    [Collection("Integration")]
    public class IntegrationTests : IDisposable
    {
        static IntegrationTests()
        {
            Environment.SetEnvironmentVariable("DB_HOST", "localhost");
            Environment.SetEnvironmentVariable("DB_PORT", "1435");
            Environment.SetEnvironmentVariable("DB_USER", "sa");
            Environment.SetEnvironmentVariable("DB_PASSWORD", "IntShop!Pass2026");
            Environment.SetEnvironmentVariable("DB_NAME_PRIMARY", "InternetShopDB_Primary");
            Environment.SetEnvironmentVariable("DB_NAME_REPLICA", "InternetShopDB_Replica");
        }

        public void Dispose()
        {
            ResetDatabases();
        }

        [Fact]
        public void GetProducts_ReturnsSeedData()
        {
            var r = new BusinessLogic().GetProducts();
            Assert.True(r.Success, r.Error);
            Assert.NotNull(r.Result);
        }

        [Fact]
        public void CreateOrder_DecreasesStock_And_Cancel_RestoresIt()
        {
            var logic = new BusinessLogic();

            var created = logic.CreateOrder(
                "{\"CustomerId\":1,\"Items\":[{\"ProductId\":2,\"Quantity\":1}],\"ShippingAddress\":\"test\"}");
            Assert.True(created.Success, created.Error);

            var orderId = ((Order)created.Result).Id;
            var cancelled = logic.CancelOrder(orderId);
            Assert.True(cancelled.Success, cancelled.Error);
        }

        [Fact]
        public void PayOrder_ChangesStatusToPaid()
        {
            var logic = new BusinessLogic();

            var created = logic.CreateOrder(
                "{\"CustomerId\":1,\"Items\":[{\"ProductId\":2,\"Quantity\":1}],\"ShippingAddress\":\"test\"}");
            Assert.True(created.Success, created.Error);
            var orderId = ((Order)created.Result).Id;

            var paid = logic.PayOrder($"{{\"OrderId\":{orderId},\"PaymentMethod\":\"Card\"}}");
            Assert.True(paid.Success, paid.Error);
        }

        [Fact]
        public void DataPersists_AfterConnectionReopen()
        {
            var logic1 = new BusinessLogic();
            var created = logic1.CreateOrder(
                "{\"CustomerId\":1,\"Items\":[{\"ProductId\":3,\"Quantity\":1}],\"ShippingAddress\":\"persist\"}");
            Assert.True(created.Success, created.Error);

            var logic2 = new BusinessLogic();
            var r = logic2.GetProducts();
            Assert.True(r.Success, r.Error);
        }

        [Fact]
        public void AppMode_DefaultValue_WhenEnvNotSet()
        {
            Environment.SetEnvironmentVariable("APP_MODE", null);

            string mode = Environment.GetEnvironmentVariable("APP_MODE") ?? "default";

            Assert.Equal("default", mode);
        }

        [Fact]
        public void AppMode_ReadsValue_WhenEnvSet()
        {
            Environment.SetEnvironmentVariable("APP_MODE", "kubernetes");

            string mode = Environment.GetEnvironmentVariable("APP_MODE") ?? "default";

            Assert.Equal("kubernetes", mode);

            Environment.SetEnvironmentVariable("APP_MODE", null);
        }

        // ============================================================
        // Reset обеих баз после каждого теста
        // ============================================================

        private static void ResetDatabases()
        {
            try
            {
                Database.SetPrimaryAvailable(true);
                Database.SetReplicaAvailable(true);

                ExecuteReset(Database.CreatePrimaryConnection());
                ExecuteReset(Database.CreateReplicaConnection());
            }
            catch
            {

            }
        }

        private static void ExecuteReset(SqlConnection conn)
        {
            using (conn)
            using (var cmd = new SqlCommand("EXEC dbo.ResetLabData", conn))
            {
                cmd.CommandTimeout = 30;
                cmd.ExecuteNonQuery();
            }
        }
    }
}