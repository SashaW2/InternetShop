using System;
using System.Collections.Generic;
using System.Text;

namespace InternetShop.Shared.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }  // Card, Cash, etc.
        public DateTime PaymentDate { get; set; }
        public bool IsSuccessful { get; set; }
    }
}
