using InternetShop.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

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
