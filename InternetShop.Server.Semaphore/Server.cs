using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.Semaphore
{
    public class Server
    {
        private TcpListener _listener;
        private RequestHandler _handler = new RequestHandler();
        private bool _isRunning = true;
        private readonly int _port;
        private int _clientCounter = 0;
        private readonly object _counterLock = new object();

        public Server(int port = 3000)
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

                        int clientId;
                        lock (_counterLock)
                        {
                            clientId = ++_clientCounter;
                        }

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ПОДКЛЮЧЕН ({client.Client.RemoteEndPoint})");

                        _ = Task.Run(() => HandleClientAsync(client, clientId));
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

        private async Task HandleClientAsync(TcpClient client, int clientId)
        {
            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            int? taskIdAtStart = Task.CurrentId;
            int threadIdAtStart = System.Threading.Thread.CurrentThread.ManagedThreadId;

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
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                            if (bytesRead == 0)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ОТКЛЮЧЕН (соединение закрыто)");
                                break;
                            }

                            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            var request = JsonConvert.DeserializeObject<Request>(requestJson);

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → START | " +
                                $"Операция: {request?.Operation} | " +
                                $"Task ID: {taskIdAtStart} | " +
                                $"Поток: {threadIdAtStart}");

                            Response response = null;

                            try
                            {
                                response = _handler.ProcessRequest(requestJson, clientId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ОШИБКА: {ex.Message}");
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

                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → END | " +
                                $"Операция: {request?.Operation} | Успех: {response.Success} | " +
                                $"Task ID: {taskIdAtStart} | " +
                                $"Поток: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                            Console.WriteLine("----------------------------------------");
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ОШИБКА ВВОДА-ВЫВОДА: {ex.Message}");
                            break;
                        }
                        catch (SocketException ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ОШИБКА СОКЕТА: {ex.Message}");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → ОШИБКА ОБРАБОТКИ: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client {clientId} → КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
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