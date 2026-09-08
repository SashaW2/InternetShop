namespace InternetShop.Tests
{
    public class ClientTests
    {
        // ============================================
        // ТЕСТ 4: Недоступный сервер
        // ============================================
        [Fact]
        public void Test4_ServerUnavailable_ThrowsException()
        {
            // Arrange
            var client = new InternetShop.Client.Client();
            string ip = "127.0.0.1";
            int port = 9999;

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => client.Connect(ip, port));
            Assert.Contains("Не удалось подключиться к серверу", exception.Message);
        }
    }
}