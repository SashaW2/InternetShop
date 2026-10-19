using System;
using System.Threading.Tasks;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public abstract class Subscriber
    {
        public string Name { get; }
        public int DelayMs { get; set; } = 0;
        public bool IsAvailable { get; set; } = true;

        protected Subscriber(string name)
        {
            Name = name;
        }

        public abstract bool Handle(Event evt);

        public virtual async Task<DeliveryResult> HandleAsync(Event evt)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - START обработки {evt.EventType} (EntityId={evt.EntityId})");

            if (!IsAvailable)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - UNAVAILABLE");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - END обработки {evt.EventType} (EntityId={evt.EntityId}), результат=UNAVAILABLE");
                return DeliveryResult.Unavailable;
            }

            try
            {
                if (DelayMs > 0)
                    await Task.Delay(DelayMs);

                bool ok = Handle(evt);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - END обработки {evt.EventType} (EntityId={evt.EntityId}), результат={(ok ? "SUCCESS" : "FAIL")}");
                return ok ? DeliveryResult.Success : DeliveryResult.Failed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - ERROR: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{evt.ClientId}] {Name} - END обработки {evt.EventType} (EntityId={evt.EntityId}), результат=FAIL (exception)");
                return DeliveryResult.Failed;
            }
        }
    }

    public enum DeliveryResult
    {
        Success,
        Failed,
        Unavailable
    }
}