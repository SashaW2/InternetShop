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
                    return new Response { Success = false, Error = "Некорректный формат запроса", Operation = "Unknown" };

                switch (request.Operation)
                {
                    case OperationType.GetProducts: return _logic.GetProducts();
                    case OperationType.CreateOrder: return _logic.CreateOrder(request.Data, clientId);
                    case OperationType.CancelOrder: return _logic.CancelOrder(request.Data, clientId);
                    case OperationType.PayOrder: return _logic.PayOrder(request.Data, clientId);
                    case OperationType.GetServerTime: return _logic.GetServerTime();
                    case OperationType.UpdateStock: return _logic.UpdateStock(request.Data, clientId);
                    default:
                        return new Response { Success = false, Error = $"Неизвестная операция: {request.Operation}", Operation = request.Operation.ToString() };
                }
            }
            catch (JsonException ex)
            {
                return new Response { Success = false, Error = $"Ошибка JSON: {ex.Message}", Operation = "Unknown" };
            }
            catch (Exception ex)
            {
                return new Response { Success = false, Error = $"Ошибка сервера: {ex.Message}", Operation = "Unknown" };
            }
        }
    }
}