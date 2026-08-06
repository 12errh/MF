using Mate.Core;
using Mate.System;
using NUnit.Framework;

[TestFixture]
public class SystemTrayReactionTests
{
    [Test]
    public void Reaction_TrayIconShownEvent_CallsNativeShowIcon()
    {
        var bus = new SimpleEventBus();
        var native = new FakeNativeTray();
        var reaction = new SystemTrayReaction(bus, native);

        bus.Publish(new TrayIconShownEvent("icon.png", "Tooltip"));

        Assert.IsTrue(native.Shown);
        Assert.AreEqual("icon.png", native.IconPath);
        Assert.AreEqual("Tooltip", native.Tooltip);
        reaction.Dispose();
    }

    [Test]
    public void Reaction_TrayIconHidden_CallsNativeHideIcon()
    {
        var bus = new SimpleEventBus();
        var native = new FakeNativeTray();
        var reaction = new SystemTrayReaction(bus, native);

        bus.Publish(new TrayIconHiddenEvent());

        Assert.IsTrue(native.Hidden);
        reaction.Dispose();
    }

    [Test]
    public void Reaction_NotificationShown_CallsNativeNotify()
    {
        var bus = new SimpleEventBus();
        var native = new FakeNativeTray();
        var reaction = new SystemTrayReaction(bus, native);

        bus.Publish(new NotificationShownEvent("Title", "Message"));

        Assert.IsTrue(native.Notified);
        Assert.AreEqual("Title", native.NotificationTitle);
        Assert.AreEqual("Message", native.NotificationMessage);
        reaction.Dispose();
    }

    [Test]
    public void Reaction_Dispose_Unsubscribes()
    {
        var bus = new SimpleEventBus();
        var native = new FakeNativeTray();
        var reaction = new SystemTrayReaction(bus, native);
        reaction.Dispose();

        bus.Publish(new TrayIconShownEvent("icon.png", "Tooltip"));

        Assert.IsFalse(native.Shown);
    }

    [Test]
    public void Reaction_EndToEnd_ServiceShowTrayIcon_ReachesNative()
    {
        // Full pipeline: SystemTrayService.ShowTrayIcon publishes TrayIconShownEvent
        // which the reaction forwards to the native layer.
        var bus = new SimpleEventBus();
        var native = new FakeNativeTray();
        var config = new MockConfig();
        var service = new SystemTrayService(config, bus);
        var reaction = new SystemTrayReaction(bus, native);

        service.ShowTrayIcon("icon.png", "My Mate");

        Assert.IsTrue(native.Shown);
        reaction.Dispose();
    }

    private class FakeNativeTray : INativeTray
    {
        public bool Shown;
        public bool Hidden;
        public bool Notified;
        public string IconPath;
        public string Tooltip;
        public string NotificationTitle;
        public string NotificationMessage;

        public void ShowIcon(string iconPath, string tooltip)
        {
            Shown = true;
            IconPath = iconPath;
            Tooltip = tooltip;
        }

        public void HideIcon() => Hidden = true;

        public void Notify(string title, string message)
        {
            Notified = true;
            NotificationTitle = title;
            NotificationMessage = message;
        }
    }

    private class MockConfig : Mate.Core.IConfiguration
    {
        private readonly System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}