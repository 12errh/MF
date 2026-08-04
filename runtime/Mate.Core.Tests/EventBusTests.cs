using NUnit.Framework;
using Mate.Core;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class EventBusTests
    {
        [Test]
        public void Subscribe_ReceivesEvent()
        {
            var bus = new SimpleEventBus();
            string received = null;

            bus.Subscribe<string>(msg => received = msg);
            bus.Publish("hello");

            Assert.AreEqual("hello", received);
        }

        [Test]
        public void MultipleSubscribers_AllReceive()
        {
            var bus = new SimpleEventBus();
            int count = 0;

            bus.Subscribe<string>(_ => count++);
            bus.Subscribe<string>(_ => count++);
            bus.Publish("test");

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Unsubscribe_StopsReceiving()
        {
            var bus = new SimpleEventBus();
            int count = 0;

            var token = bus.Subscribe<string>(_ => count++);
            bus.Publish("a");
            bus.Unsubscribe(token);
            bus.Publish("b");

            Assert.AreEqual(1, count);
        }

        [Test]
        public void TypedEvent_OnlyReceivesMatchingType()
        {
            var bus = new SimpleEventBus();
            int intCount = 0;
            int stringCount = 0;

            bus.Subscribe<int>(_ => intCount++);
            bus.Subscribe<string>(_ => stringCount++);
            bus.Publish(42);
            bus.Publish("hello");

            Assert.AreEqual(1, intCount);
            Assert.AreEqual(1, stringCount);
        }

        [Test]
        public void Clear_RemovesAllHandlers()
        {
            var bus = new SimpleEventBus();
            int count = 0;
            bus.Subscribe<string>(_ => count++);
            bus.Clear();
            bus.Publish("x");
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Publish_Before_Subscribe_NoOp()
        {
            var bus = new SimpleEventBus();
            Assert.DoesNotThrow(() => bus.Publish("before"));
        }
    }
}