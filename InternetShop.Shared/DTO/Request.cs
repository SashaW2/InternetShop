using InternetShop.Shared.Enums;

namespace InternetShop.Shared.DTO
{
    public class Request
    {
        public OperationType Operation { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ClientId { get; set; }
    }
}