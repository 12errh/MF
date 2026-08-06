using System.Collections.Generic;
using Mate.Audio;
using Mate.Character.Animation;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class DanceReactionTests
{
    [Test]
    public void DanceReaction_DanceStartedEvent_PlaysConfiguredClip()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("danceAnimation", "MyDance");
        var driver = new FakeDriver();

        var reaction = new DanceReaction(bus, config, driver);
        bus.Publish(new DanceStartedEvent("dance_audio_reactive"));

        Assert.AreEqual("MyDance", driver.LastDanceClip);
        reaction.Dispose();
    }

    [Test]
    public void DanceReaction_DanceStartedEvent_DefaultsToMateDance()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig(); // no danceAnimation set
        var driver = new FakeDriver();

        var reaction = new DanceReaction(bus, config, driver);
        bus.Publish(new DanceStartedEvent("dance_audio_reactive"));

        Assert.AreEqual("MateDance", driver.LastDanceClip);
        reaction.Dispose();
    }

    [Test]
    public void DanceReaction_DanceStoppedEvent_ReturnsToIdle()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        var driver = new FakeDriver();

        var reaction = new DanceReaction(bus, config, driver);
        bus.Publish(new DanceStartedEvent("x"));
        bus.Publish(new DanceStoppedEvent());

        Assert.IsTrue(driver.ReturnedToIdle);
        reaction.Dispose();
    }

    [Test]
    public void DanceReaction_Dispose_Unsubscribes()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        var driver = new FakeDriver();

        var reaction = new DanceReaction(bus, config, driver);
        reaction.Dispose();
        bus.Publish(new DanceStartedEvent("x"));

        Assert.IsNull(driver.LastDanceClip);
    }

    [Test]
    public void DanceReaction_EndToEnd_AudioPeak_DanceStarted_CustomClip()
    {
        // Full pipeline: audio peak -> bridge -> DanceStartedEvent -> reaction
        // plays the configured clip.
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.3f);
        config.Set("danceAnimation", "HipHop");
        var driver = new FakeDriver();

        var bridge = new AudioReactiveBridge(bus, config);
        var reaction = new DanceReaction(bus, config, driver);

        bus.Publish(new AudioPeakEvent(0, 0.5f));

        Assert.AreEqual("HipHop", driver.LastDanceClip);
        reaction.Dispose();
        bridge.Dispose();
    }

    private class FakeDriver : IAnimatorDriver
    {
        public string LastDanceClip;
        public bool ReturnedToIdle;

        public void PlayDance(string clipName)
        {
            LastDanceClip = clipName;
            ReturnedToIdle = false;
        }

        public void PlayIdle()
        {
            ReturnedToIdle = true;
        }
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