using System;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public class Publisher
    {
        private readonly Broker _broker;

        public Publisher(Broker broker)
        {
            _broker = broker;
        }

        public bool Publish(Event evt)
        {
            if (evt == null)
            {
                Log("-", "PUBLISH FAIL — событие null");
                return false;
            }

            Log(evt.ClientId, $"PUBLISH {evt.EventType} (EntityId={evt.EntityId})");

            try
            {
                bool ok = _broker.Deliver(evt);
                Log(evt.ClientId, ok ? "PUBLISH SUCCESS" : "PUBLISH FAIL — нет доставки");
                return ok;
            }
            catch (Exception ex)
            {
                Log(evt.ClientId, $"PUBLISH ERROR: {ex.Message}");
                return false;
            }
        }

        private static void Log(string clientId, string msg)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{clientId}] Publisher - {msg}");
        }
    }
}