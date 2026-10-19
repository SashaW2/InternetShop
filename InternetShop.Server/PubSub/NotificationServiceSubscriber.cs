using System;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public class NotificationServiceSubscriber : Subscriber
    {
        public NotificationServiceSubscriber() : base("NotificationService")
        {
            DelayMs = 2000;
            IsAvailable = true;
        }

        public override bool Handle(Event evt)
        {
            if (evt.EventType != EventTypes.OrderCreated &&
                evt.EventType != EventTypes.PaymentCompleted)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] NotificationService - SKIP (не подписан на {evt.EventType})");
                return false;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] NotificationService - Уведомление по {evt.EventType} (EntityId={evt.EntityId})");
            return true;
        }
    }
}