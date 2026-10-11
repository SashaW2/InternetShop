using System;
using System.Collections.Generic;
using System.Text;

namespace InternetShop.Shared.DTO
{
    public class UpdateStockRequest
    {
        public int ProductId { get; set; }
        public int NewStock { get; set; }
    }
}