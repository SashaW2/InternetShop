using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using InternetShop.Shared.DTO;

namespace InternetShop.Client
{
    public class Client
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isConnected = false;
        public string ClientId { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        public TimeSpan ClockOffset { get; set; } = TimeSpan.Zero;
        public TimeSpan CalculatedOffset { get; set; } = TimeSpan.Zero;

        public DateTime GetLocalTime()
        {
            return DateTime.UtcNow + ClockOffset;
        }

        public DateTime GetCorrectedTime()
        {
            return GetLocalTime() + CalculatedOffset;
        }

        public void Connect(string ip, int port)
        {
            try
            {
                _tcpClient = new TcpClient(ip, port);
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                Console.WriteLine($"Подключено к {ip}:{port}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось подключиться к серверу: {ex.Message}");
            }
        }

        public Response SendRequest(Request request)
        {
            if (!_isConnected || _tcpClient == null || !_tcpClient.Connected)
            {
                throw new Exception("Соединение с сервером потеряно");
            }

            try
            {
                var requestJson = JsonConvert.SerializeObject(request);
                Console.WriteLine($"Отправка запроса: {requestJson}");

                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                _stream.Write(requestBytes, 0, requestBytes.Length);

                var buffer = new byte[8192];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    _isConnected = false;
                    throw new Exception("Сервер закрыл соединение");
                }

                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Получен ответ: {responseJson}");

                return JsonConvert.DeserializeObject<Response>(responseJson);
            }
            catch (SocketException ex)
            {
                _isConnected = false;
                throw new Exception($"Ошибка сетевого взаимодействия: {ex.Message}");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                throw new Exception($"Ошибка при обмене данными: {ex.Message}");
            }
        }

        public bool IsConnected()
        {
            return _isConnected && _tcpClient != null && _tcpClient.Connected;
        }

        public void Close()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine("Соединение с сервером закрыто");
        }
    }
}