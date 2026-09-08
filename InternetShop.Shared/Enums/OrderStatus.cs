using System;
using System.Collections.Generic;
using System.Text;

namespace InternetShop.Shared.Enums
{
    public enum OrderStatus
    {
        Pending,    // В обработке
        Paid,       // Оплачен
        Shipped,    // Отправлен
        Delivered,  // Доставлен
        Cancelled   // Отменен
    }
}
