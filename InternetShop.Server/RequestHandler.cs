using System;
using Newtonsoft.Json;
using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;

namespace InternetShop.Server
{
    public class RequestHandler
    {
        private BusinessLogic _logic = new BusinessLogic();

        public Response ProcessRequest(string requestJson, int clientId = 0)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<Request>(requestJson);

                if (request == null)
                {
                    return new Response
                    {
                        Success = false,
                        Error = "Некорректный формат запроса",
                        Operation = "Unknown"
                    };
                }

                switch (request.Operation)
                {
                    case OperationType.GetProducts:
                        return _logic.GetProducts();

                    case OperationType.CreateOrder:
                        return _logic.CreateOrder(request.Data, clientId);

                    case OperationType.CancelOrder:
                        return _logic.CancelOrder(request.Data);

                    case OperationType.PayOrder:
                        return _logic.PayOrder(request.Data);

                    default:
                        return new Response
                        {
                            Success = false,
                            Error = $"Неизвестная операция: {request.Operation}",
                            Operation = request.Operation.ToString()
                        };
                }
            }
            catch (JsonException ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Ошибка парсинга JSON: {ex.Message}",
                    Operation = "Unknown"
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Внутренняя ошибка сервера: {ex.Message}",
                    Operation = "Unknown"
                };
            }
        }
    }
}