using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternetShop.Shared.DTO;

namespace InternetShop.Server.PubSub
{
    public class Broker
    {
        private const int MaxAttempts = 2;
        private const int RetryDelayMs = 500;

        private readonly Dictionary<string, List<Subscriber>> _subscriptions
            = new Dictionary<string, List<Subscriber>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Subscriber, Queue<Event>> _pending
            = new Dictionary<Subscriber, Queue<Event>>();

        private readonly object _lock = new object();

        public void Subscribe(string eventType, Subscriber subscriber)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("Тип события не может быть пустым", nameof(eventType));
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var list))
                {
                    list = new List<Subscriber>();
                    _subscriptions[eventType] = list;
                }

                if (!list.Contains(subscriber))
                    list.Add(subscriber);

                if (!_pending.ContainsKey(subscriber))
                    _pending[subscriber] = new Queue<Event>();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Broker - SUBSCRIBE {eventType} → {subscriber.Name}");
            }
        }

        public bool Deliver(Event evt)
        {
            if (evt == null)
            {
                Log("-", "Broker - EVENT INVALID (null)");
                return false;
            }

            evt.PublishedAtUtc = DateTime.UtcNow;
            Log(evt.ClientId, $"Broker - EVENT RECEIVED {evt.EventType} (EntityId={evt.EntityId}) at {DateTime.Now:HH:mm:ss.fff}");

            List<Subscriber> targets;
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(evt.EventType, out var list) || list.Count == 0)
                {
                    Log(evt.ClientId, $"Broker - NO SUBSCRIBERS for {evt.EventType}");
                    return false;
                }
                targets = new List<Subscriber>(list);
            }

            var tasks = targets.Select(sub => DeliverToSubscriberAsync(sub, evt)).ToArray();

            try { Task.WaitAll(tasks); }
            catch (Exception ex) { Log(evt.ClientId, $"Broker - DELIVER AGGREGATE ERROR: {ex.Message}"); }

            var results = tasks.Select(t => t.Result).ToList();
            int okCount = results.Count(r => r.Result == DeliveryResult.Success);
            int failCount = results.Count(r => r.Result == DeliveryResult.Failed);
            int unavCount = results.Count(r => r.Result == DeliveryResult.Unavailable);
            int queued = _pending.Values.Sum(q => q.Count);

            Log(evt.ClientId, $"Broker - SUMMARY {evt.EventType}: success={okCount}, failed={failCount}, unavailable={unavCount}, queued={queued}");

            return results.Any(r => r.Result == DeliveryResult.Success);
        }

        private async Task<(Subscriber Sub, DeliveryResult Result)> DeliverToSubscriberAsync(Subscriber sub, Event evt)
        {
            try
            {
                await FlushPendingAsync(sub);

                var result = await TryDeliverWithRetriesAsync(sub, evt);

                if (result != DeliveryResult.Success)
                {
                    lock (_lock)
                    {
                        _pending[sub].Enqueue(Clone(evt));
                    }
                    Log(evt.ClientId, $"Broker - QUEUED for {sub.Name} ({evt.EventType}, EntityId={evt.EntityId})");
                }

                return (sub, result);
            }
            catch (Exception ex)
            {
                Log(evt.ClientId, $"Broker - {sub.Name} → ERROR: {ex.Message}");
                return (sub, DeliveryResult.Failed);
            }
        }

        private async Task FlushPendingAsync(Subscriber sub)
        {
            List<Event> snapshot;
            lock (_lock)
            {
                if (!_pending.TryGetValue(sub, out var queue) || queue.Count == 0)
                    return;
                snapshot = queue.ToList();
                queue.Clear();
            }

            var failed = new List<Event>();

            foreach (var next in snapshot)
            {
                Log(next.ClientId, $"Broker - FLUSH {sub.Name}: повторная доставка {next.EventType} (EntityId={next.EntityId})");
                var r = await TryDeliverWithRetriesAsync(sub, next);

                if (r != DeliveryResult.Success)
                    failed.Add(next);
            }

            lock (_lock)
            {
                foreach (var e in failed)
                    _pending[sub].Enqueue(e);
            }
        }

        private async Task<DeliveryResult> TryDeliverWithRetriesAsync(Subscriber sub, Event evt)
        {
            DeliveryResult lastResult = DeliveryResult.Failed;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                evt.Attempt = attempt;
                Log(evt.ClientId, $"Broker → {sub.Name} (attempt {attempt}/{MaxAttempts})");

                var result = await sub.HandleAsync(evt);
                lastResult = result;

                if (result == DeliveryResult.Success)
                    return result;

                Log(evt.ClientId, $"Broker - {sub.Name} attempt {attempt} → {result}");

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelayMs);
                    Log(evt.ClientId, $"Broker - retry {sub.Name} after {RetryDelayMs} ms");
                }
            }

            return lastResult;
        }

        private static Event Clone(Event e)
        {
            return new Event
            {
                EventType = e.EventType,
                EntityId = e.EntityId,
                Timestamp = e.Timestamp,
                Data = e.Data,
                PublishedAtUtc = e.PublishedAtUtc,
                Attempt = 1,
                ClientId = e.ClientId
            };
        }

        private static void Log(string clientId, string msg)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{clientId}] {msg}");
        }
    }
}