using System;
using System.Collections.Generic;
using System.Text;

namespace InternetShop.Shared.Enums
{
    public enum OperationType
    {
        GetProducts = 1,
        CreateOrder = 2,
        CancelOrder = 3,
        PayOrder = 4,
        GetServerTime = 5
    }
}
