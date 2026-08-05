using System.Collections.Generic;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Interfaces;
using Mate.System;
using NUnit.Framework;

[TestFixture]
public class SystemTrayServiceTests
{
    [Test]
    public void SystemTrayService_ImplementsISystemService()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<ISystemService>(svc);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_ShowNotification_EmptyTitle_DoesNotThrow()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.DoesNotThrowAsync(() => svc.ShowNotification("", "test message"));
        ctx.Dispose();
    }

    [Test]
    public async Task SystemTrayService_ShowNotification_ReturnsOk_WithDefaultTitle()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        var result = await svc.ShowNotification("", "Body");
        Assert.IsTrue(result.IsSuccess);
        ctx.Dispose();
    }

    [Test]
    public async Task SystemTrayService_ShowNotification_EmptyMessage_Fails()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        var result = await svc.ShowNotification("Title", "");
        Assert.IsFalse(result.IsSuccess);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_ShowNotification_PublishesEvent()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool eventFired = false;
        string title = null, message = null;
        bus.Subscribe<NotificationShownEvent>(e => { eventFired = true; title = e.Title; message = e.Message; });

        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), bus);
        svc.ShowNotification("Hello", "World");

        Assert.IsTrue(eventFired);
        Assert.AreEqual("Hello", title);
        Assert.AreEqual("World", message);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_ShowTrayIcon_OnlyPublishesOnce()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        int shownCount = 0;
        bus.Subscribe<TrayIconShownEvent>(_ => shownCount++);

        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), bus);
        svc.ShowTrayIcon("icon.png", "Tooltip");
        svc.ShowTrayIcon("icon.png", "Tooltip");

        Assert.AreEqual(1, shownCount);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_HideTrayIcon_PublishesEvent()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool hidden = false;
        bus.Subscribe<TrayIconHiddenEvent>(_ => hidden = true);

        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), bus);
        svc.ShowTrayIcon("icon.png", "Tooltip");
        svc.HideTrayIcon();

        Assert.IsTrue(hidden);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_Dispose_DoesNotThrow()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.DoesNotThrow(() => svc.Dispose());
        ctx.Dispose();
    }

    private class MockConfig : IConfiguration
    {
        private readonly Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}