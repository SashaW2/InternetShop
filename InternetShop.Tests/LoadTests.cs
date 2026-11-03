using System.Diagnostics;
using InternetShop.Server;
using Xunit.Abstractions;

namespace InternetShop.Tests
{
    [Collection("Integration")]
    public class LoadTests
    {
        private readonly ITestOutputHelper _out;

        public LoadTests(ITestOutputHelper o)
        {
            _out = o;

            Environment.SetEnvironmentVariable("DB_HOST", "localhost");
            Environment.SetEnvironmentVariable("DB_PORT", "1435");
            Environment.SetEnvironmentVariable("DB_USER", "sa");
            Environment.SetEnvironmentVariable("DB_PASSWORD", "IntShop!Pass2026");
            Environment.SetEnvironmentVariable("DB_NAME_PRIMARY", "InternetShopDB_Primary");
            Environment.SetEnvironmentVariable("DB_NAME_REPLICA", "InternetShopDB_Replica");
        }

        [Fact]
        public void Parallel_GetProducts_1000_Requests()
        {
            var logic = new BusinessLogic();
            const int n = 1000;
            int ok = 0, fail = 0;

            var sw = Stopwatch.StartNew();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
            {
                var r = logic.GetProducts();
                if (r.Success) Interlocked.Increment(ref ok);
                else Interlocked.Increment(ref fail);
            });
            sw.Stop();

            double rps = n * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds);
            _out.WriteLine($"Нагрузка: {n} запросов | успех={ok} | ошибок={fail} | время={sw.ElapsedMilliseconds} мс | RPS≈{rps:F1}");

            Assert.True(ok > n * 0.95, "Более 95% запросов должны быть успешны");
        }
    }
}