using System;
using InternetShop.Server;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using Xunit;

namespace InternetShop.Tests
{
    public class TimeSyncTests
    {
        // ============================================================
        // ТЕСТ 1: Синхронизация времени БЕЗ задержки
        // ============================================================
        [Fact]
        public void Test_TimeSync_WithoutDelay()
        {
            // Arrange
            var logic = new BusinessLogic();
            TimeSpan clientClockOffset = TimeSpan.FromSeconds(7);

            DateTime requestTimeUtc = DateTime.UtcNow;
            DateTime clientLocalBefore = requestTimeUtc + clientClockOffset;

            // Act
            var response = logic.GetServerTime();
            DateTime responseTimeUtc = DateTime.UtcNow;
            DateTime clientLocalAfter = responseTimeUtc + clientClockOffset;

            // Assert
            Assert.True(response.Success);
            Assert.NotNull(response.Result);

            var timeData = response.Result as TimeResponse;
            Assert.NotNull(timeData);
            Assert.Equal(0, timeData.ArtificialDelayMs);

            DateTime serverTime = timeData.ServerTimeUtc;

            TimeSpan rtt = responseTimeUtc - requestTimeUtc;
            TimeSpan networkDelay = TimeSpan.FromMilliseconds(rtt.TotalMilliseconds / 2.0);

            DateTime estimatedServerNow = serverTime + networkDelay;
            TimeSpan offset = estimatedServerNow - clientLocalAfter;
            DateTime corrected = clientLocalAfter + offset;

            Assert.InRange(offset.TotalSeconds, -7.5, -6.5);

            Assert.True(Math.Abs((estimatedServerNow - corrected).TotalMilliseconds) < 1);

            double diffBefore = (serverTime - clientLocalBefore).TotalSeconds;
            Assert.InRange(diffBefore, -8.0, -6.0);
        }

        // ============================================================
        // ТЕСТ 2: Синхронизация времени С задержкой
        // ============================================================
        [Fact]
        public void Test_TimeSync_WithDelay()
        {
            // Arrange
            DateTime serverTime = DateTime.UtcNow;
            TimeSpan clientClockOffset = TimeSpan.FromSeconds(7);

            DateTime requestTimeUtc = serverTime - TimeSpan.FromMilliseconds(500);
            DateTime responseTimeUtc = serverTime + TimeSpan.FromMilliseconds(500);

            DateTime clientLocalBefore = requestTimeUtc + clientClockOffset;
            DateTime clientLocalAfter = responseTimeUtc + clientClockOffset;

            // Act — расчёт по той же логике, что в клиенте
            TimeSpan rtt = responseTimeUtc - requestTimeUtc;
            TimeSpan networkDelay = TimeSpan.FromMilliseconds(rtt.TotalMilliseconds / 2.0);

            DateTime estimatedServerNow = serverTime + networkDelay;
            TimeSpan offset = estimatedServerNow - clientLocalAfter;
            DateTime corrected = clientLocalAfter + offset;

            // RTT = 1000 мс
            Assert.Equal(1000, rtt.TotalMilliseconds);

            // RTT/2 = 500 мс
            Assert.Equal(500, networkDelay.TotalMilliseconds);

            // Согласованность
            Assert.True(Math.Abs((estimatedServerNow - corrected).TotalMilliseconds) < 1);

            // Поправка ≈ -7 сек + 500 мс = -6.5 сек
            Assert.InRange(offset.TotalSeconds, -7.0, -6.0);

            // Разница "до синхронизации" ≈ -7 сек
            double diffBefore = (serverTime - clientLocalBefore).TotalSeconds;
            Assert.InRange(diffBefore, -7.5, -6.5);
        }
    }
}