using System;
using System.Collections.Generic;
using System.Text;

namespace InternetShop.Shared.DTO
{
    public class Response
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Operation { get; set; }
    }
}
