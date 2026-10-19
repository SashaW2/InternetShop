using System;
using System.Collections.Generic;
using InternetShop.Server.PubSub;
using InternetShop.Shared.DTO;
using Xunit;

namespace InternetShop.Server.Tests
{
    internal class RecordingSubscriber : Subscriber
    {
        public int ReceivedCount { get; private set; }
        public Event LastEvent { get; private set; }

        public RecordingSubscriber(string name = "Recorder") : base(name) { }

        public override bool Handle(Event evt)
        {
            ReceivedCount++;
            LastEvent = evt;
            return true;
        }
    }

    internal class FailingSubscriber : Subscriber
    {
        public FailingSubscriber(string name = "Failing") : base(name) { }

        public override bool Handle(Event evt)
            => throw new InvalidOperationException("simulated failure");
    }

    internal class UnavailableSubscriber : Subscriber
    {
        public UnavailableSubscriber(string name = "Unavailable") : base(name)
        {
            IsAvailable = false;
        }

        public override bool Handle(Event evt) => true;
    }

    // Новый подписчик для Части 4 — только в тестах
    internal class AnalyticsStubSubscriber : Subscriber
    {
        public int ReceivedCount { get; private set; }

        public AnalyticsStubSubscriber() : base("AnalyticsService") { }

        public override bool Handle(Event evt)
        {
            if (evt.EventType != EventTypes.OrderCreated) return false;
            ReceivedCount++;
            return true;
        }
    }

    public class PubSubTests
    {
        // 1. Корректное создание события
        [Fact]
        public void Event_Creation_IsCorrect()
        {
            var evt = new Event
            {
                EventType = EventTypes.OrderCreated,
                EntityId = 125,
                Timestamp = DateTime.Now,
                Data = new Dictionary<string, object> { { "TotalPrice", 100m } }
            };

            Assert.Equal(EventTypes.OrderCreated, evt.EventType);
            Assert.Equal(125, evt.EntityId);
            Assert.NotEqual(default(DateTime), evt.Timestamp);
            Assert.NotNull(evt.Data);
        }

        // 2. Корректная публикация события
        [Fact]
        public void Publish_DeliversToBroker()
        {
            var broker = new Broker();
            var sub = new RecordingSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, sub);
            var publisher = new Publisher(broker);

            var ok = publisher.Publish(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });

            Assert.True(ok);
            Assert.Equal(1, sub.ReceivedCount);
        }

        // 3. Регистрация Subscriber
        [Fact]
        public void Subscribe_RegistersSubscriber()
        {
            var broker = new Broker();
            var sub = new RecordingSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, sub);

            broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });

            Assert.Equal(1, sub.ReceivedCount);
        }

        // 4. Получение события подписанным Subscriber
        [Fact]
        public void Subscribed_ReceivesEvent()
        {
            var broker = new Broker();
            var sub = new RecordingSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, sub);

            broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 7 });

            Assert.Equal(1, sub.ReceivedCount);
            Assert.Equal(7, sub.LastEvent.EntityId);
        }

        // 5. Отсутствие доставки неподписанному Subscriber
        [Fact]
        public void NonSubscribed_DoesNotReceiveEvent()
        {
            var broker = new Broker();
            var sub = new RecordingSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, sub);

            broker.Deliver(new Event { EventType = EventTypes.OrderCancelled, EntityId = 1 });

            Assert.Equal(0, sub.ReceivedCount);
        }

        // 6. Доставка одного события нескольким Subscriber
        [Fact]
        public void OneEvent_DeliveredToSeveralSubscribers()
        {
            var broker = new Broker();
            var a = new RecordingSubscriber("A");
            var b = new RecordingSubscriber("B");
            var c = new RecordingSubscriber("C");
            broker.Subscribe(EventTypes.OrderCreated, a);
            broker.Subscribe(EventTypes.OrderCreated, b);
            broker.Subscribe(EventTypes.OrderCreated, c);

            broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });

            Assert.Equal(1, a.ReceivedCount);
            Assert.Equal(1, b.ReceivedCount);
            Assert.Equal(1, c.ReceivedCount);
        }

        // 7. Обработка нескольких типов событий
        [Fact]
        public void SeveralEventTypes_AreHandled()
        {
            var broker = new Broker();
            var logger = new RecordingSubscriber("Logger");
            var stock = new RecordingSubscriber("Stock");
            broker.Subscribe(EventTypes.OrderCreated, logger);
            broker.Subscribe(EventTypes.OrderCancelled, logger);
            broker.Subscribe(EventTypes.PaymentCompleted, logger);
            broker.Subscribe(EventTypes.OrderCreated, stock);

            broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });
            broker.Deliver(new Event { EventType = EventTypes.OrderCancelled, EntityId = 1 });
            broker.Deliver(new Event { EventType = EventTypes.PaymentCompleted, EntityId = 1 });

            Assert.Equal(3, logger.ReceivedCount);
            Assert.Equal(1, stock.ReceivedCount); 
        }

        // 8. Корректная обработка ошибки Subscriber
        [Fact]
        public void SubscriberError_IsHandled()
        {
            var broker = new Broker();
            var bad = new FailingSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, bad);

            var ex = Record.Exception(() =>
                broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 }));

            Assert.Null(ex);
        }

        // 9. Продолжение работы Broker при отказе Subscriber
        [Fact]
        public void Broker_ContinuesAfterSubscriberFailure()
        {
            var broker = new Broker();
            var bad = new UnavailableSubscriber();
            var good = new RecordingSubscriber("Good");
            broker.Subscribe(EventTypes.OrderCreated, bad);
            broker.Subscribe(EventTypes.OrderCreated, good);

            var ok = broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });

            Assert.True(ok);
            Assert.Equal(1, good.ReceivedCount);
        }

        // 10. Добавление нового Subscriber
        [Fact]
        public void NewSubscriber_CanBeAdded()
        {
            var broker = new Broker();
            var analytics = new AnalyticsStubSubscriber();

            broker.Subscribe(EventTypes.OrderCreated, analytics);

            var ok = broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 1 });

            Assert.True(ok);
        }

        // 11. Получение события новым Subscriber
        [Fact]
        public void NewSubscriber_ReceivesEvent()
        {
            var broker = new Broker();
            var analytics = new AnalyticsStubSubscriber();
            broker.Subscribe(EventTypes.OrderCreated, analytics);

            broker.Deliver(new Event { EventType = EventTypes.OrderCreated, EntityId = 55 });

            Assert.Equal(1, analytics.ReceivedCount);
        }
    }
}