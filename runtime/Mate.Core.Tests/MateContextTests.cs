using System;
using NUnit.Framework;
using Mate.Core;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class MateContextTests
    {
        [Test]
        public void Register_And_Resolve()
        {
            var ctx = new MateContext();
            ctx.Register<IEventBus>(() => new SimpleEventBus());

            var bus = ctx.Resolve<IEventBus>();
            Assert.IsNotNull(bus);
            Assert.IsInstanceOf<SimpleEventBus>(bus);
        }

        [Test]
        public void Resolve_Unregistered_Throws()
        {
            var ctx = new MateContext();
            Assert.Throws<InvalidOperationException>(() => ctx.Resolve<IEventBus>());
        }

        [Test]
        public void Register_Singleton_ReturnsSameInstance()
        {
            var ctx = new MateContext();
            var bus = new SimpleEventBus();
            ctx.RegisterSingleton<IEventBus>(bus);

            var resolved1 = ctx.Resolve<IEventBus>();
            var resolved2 = ctx.Resolve<IEventBus>();
            Assert.AreSame(resolved1, resolved2);
        }

        [Test]
        public void Register_Factory_CreatesNewEachTime()
        {
            var ctx = new MateContext();
            int count = 0;
            ctx.Register<IEventBus>(() => { count++; return new SimpleEventBus(); });

            ctx.Resolve<IEventBus>();
            ctx.Resolve<IEventBus>();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void Dispose_CallsDisposeOnRegisteredServices()
        {
            var ctx = new MateContext();
            var disposable = new DisposableService();
            ctx.RegisterSingleton<IDisposable>(disposable);

            ctx.Dispose();
            Assert.IsTrue(disposable.WasDisposed);
        }

        [Test]
        public void EventBus_Integration()
        {
            var ctx = new MateContext();
            ctx.Register<IEventBus>(() => new SimpleEventBus());

            var bus = ctx.Resolve<IEventBus>();
            string received = null;
            bus.Subscribe<string>(s => received = s);
            bus.Publish("integration test");

            Assert.AreEqual("integration test", received);
        }

        private class DisposableService : IDisposable
        {
            public bool WasDisposed { get; private set; }
            public void Dispose() => WasDisposed = true;
        }
    }
}