using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using InternetShop.Shared.DTO;

namespace InternetShop.Server
{
    public class Server
    {
        private TcpListener _listener;
        private RequestHandler _handler = new RequestHandler();
        private bool _isRunning = true;
        private readonly int _port;

        public Server(int port = 8888)
        {
            _port = port;
        }

        public void Start()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                Console.WriteLine($"Сервер запущен на порту {_port}");
                Console.WriteLine($"IP-адрес: {GetLocalIPAddress()}");
                Console.WriteLine($"Время запуска: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                Console.WriteLine("Ожидание подключений...");
                Console.WriteLine("========================================");

                while (_isRunning)
                {
                    try
                    {
                        var client = _listener.AcceptTcpClient();
                        Console.WriteLine($"Клиент подключился: {client.Client.RemoteEndPoint}");

                        var thread = new Thread(() => HandleClient(client));
                        thread.Start();
                    }
                    catch (SocketException ex)
                    {
                        if (_isRunning)
                            Console.WriteLine($"Ошибка сокета: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка сервера: {ex.Message}");
            }
            finally
            {
                _listener?.Stop();
                Console.WriteLine("Сервер остановлен");
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    while (true)
                    {
                        try
                        {
                            var buffer = new byte[8192];
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0)
                            {
                                Console.WriteLine("Клиент закрыл соединение");
                                break;
                            }

                            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Console.WriteLine($"Получен запрос: {requestJson}");

                            Response response = null;

                            try
                            {
                                response = _handler.ProcessRequest(requestJson);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка обработки запроса: {ex.Message}");
                                response = new Response
                                {
                                    Success = false,
                                    Error = $"Внутренняя ошибка сервера: {ex.Message}",
                                    Operation = "Unknown"
                                };
                            }

                            if (response == null)
                            {
                                response = new Response
                                {
                                    Success = false,
                                    Error = "Неизвестная ошибка",
                                    Operation = "Unknown"
                                };
                            }

                            var responseJson = JsonConvert.SerializeObject(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                            stream.Write(responseBytes, 0, responseBytes.Length);
                            Console.WriteLine($"Отправлен ответ: {responseJson}");
                            Console.WriteLine("----------------------------------------");
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"Ошибка ввода-вывода: {ex.Message}");
                            break;
                        }
                        catch (SocketException ex)
                        {
                            Console.WriteLine($"Ошибка сокета: {ex.Message}");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка обработки: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки клиента: {ex.Message}");
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}