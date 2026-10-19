using System;
using System.Collections.Generic;
using System.Linq;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public class AnalyticsServiceSubscriber : Subscriber
    {
        private int _totalEvents = 0;
        private readonly Dictionary<string, int> _byType = new Dictionary<string, int>();
        private decimal _totalOrderAmount = 0m;

        public AnalyticsServiceSubscriber() : base("AnalyticsService")
        {
            DelayMs = 500;
            IsAvailable = true;
        }

        public override bool Handle(Event evt)
        {
            try
            {
                _totalEvents++;
                _byType.TryGetValue(evt.EventType, out int cur);
                _byType[evt.EventType] = cur + 1;

                if (evt.EventType == EventTypes.OrderCreated
                    && evt.Data != null
                    && evt.Data.ContainsKey("TotalPrice"))
                {
                    _totalOrderAmount += Convert.ToDecimal(evt.Data["TotalPrice"]);
                }

                var byTypeStr = string.Join(", ", _byType.Keys.OrderBy(k => k).Select(k => $"{k}={_byType[k]}"));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] AnalyticsService - [{evt.EventType}] (EntityId={evt.EntityId}). Всего событий={_totalEvents} ({byTypeStr}). Сумма заказов={_totalOrderAmount} BYN");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] AnalyticsService - ERROR: {ex.Message}");
                return false;
            }
        }
    }
}