using System;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public class LoggerSubscriber : Subscriber
    {
        public LoggerSubscriber() : base("Logger")
        {
            DelayMs = 1000;
            IsAvailable = true;
        }

        public override bool Handle(Event evt)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] Logger - [{evt.EventType}] EntityId={evt.EntityId}, Timestamp={evt.Timestamp:HH:mm:ss.fff}");
            return true;
        }
    }
}