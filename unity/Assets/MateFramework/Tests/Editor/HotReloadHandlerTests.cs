using System;
using System.IO;
using Mate.Core;
using NUnit.Framework;

[TestFixture]
public class HotReloadHandlerTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "mate-hotreload-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void HotReloadHandler_RecordsMinTime_Initially()
    {
        var bus = new SimpleEventBus();
        var handler = new HotReloadHandler(_testDir, bus);
        Assert.AreEqual(DateTime.MinValue, handler.LastReloadTime);
        handler.Dispose();
    }

    [Test]
    public void HotReloadHandler_TriggerReload_PublishesConfigReloadedEvent()
    {
        var bus = new SimpleEventBus();
        bool reloaded = false;
        string source = null;
        bus.Subscribe<ConfigReloadedEvent>(e => { reloaded = true; source = e.Source; });

        var handler = new HotReloadHandler(_testDir, bus);
        handler.TriggerReload("settings");

        Assert.IsTrue(reloaded);
        Assert.AreEqual("settings", source);
        Assert.AreNotEqual(DateTime.MinValue, handler.LastReloadTime);
        handler.Dispose();
    }

    [Test]
    public void HotReloadHandler_Dispose_IsIdempotentAndStopsWatcher()
    {
        var bus = new SimpleEventBus();
        var handler = new HotReloadHandler(_testDir, bus);
        Assert.DoesNotThrow(() => handler.Dispose());
        Assert.DoesNotThrow(() => handler.Dispose());
        Assert.DoesNotThrow(() => handler.TriggerReload("settings"));
    }

    [Test]
    public void HotReloadHandler_ShouldWatch_IgnoresCodeFiles()
    {
        var bus = new SimpleEventBus();
        var handler = new HotReloadHandler(_testDir, bus);

        Assert.IsTrue(handler.ShouldWatch(".toml"));
        Assert.IsTrue(handler.ShouldWatch(".json"));
        Assert.IsTrue(handler.ShouldWatch(".vrm"));
        Assert.IsTrue(handler.ShouldWatch(".wav"));
        Assert.IsTrue(handler.ShouldWatch(".mp3"));
        Assert.IsTrue(handler.ShouldWatch(".anim"));
        Assert.IsTrue(handler.ShouldWatch(".TOML")); // case-insensitive

        Assert.IsFalse(handler.ShouldWatch(".cs"));
        Assert.IsFalse(handler.ShouldWatch(".dll"));
        Assert.IsFalse(handler.ShouldWatch(""));
        Assert.IsFalse(handler.ShouldWatch(null));
        handler.Dispose();
    }

    [Test]
    public void HotReloadHandler_FileSystemWatcher_DetectsWhitelistedFileChange()
    {
        var bus = new SimpleEventBus();
        bool reloaded = false;
        bus.Subscribe<ConfigReloadedEvent>(_ => reloaded = true);

        var handler = new HotReloadHandler(_testDir, bus, debounceMs: 50);
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), "{ \"fpsLimit\": 60 }");

        // FileSystemWatcher fires asynchronously; poll up to 2s for the event.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!reloaded && DateTime.UtcNow < deadline)
        {
            System.Threading.Thread.Sleep(20);
        }

        Assert.IsTrue(reloaded, "expected ConfigReloadedEvent after settings.json change");
        handler.Dispose();
    }

    [Test]
    public void HotReloadHandler_MissingDir_ConstructsSafely()
    {
        var bus = new SimpleEventBus();
        var handler = new HotReloadHandler(
            Path.Combine(_testDir, "does-not-exist"),
            bus
        );
        Assert.DoesNotThrow(() => handler.Dispose());
    }
}
