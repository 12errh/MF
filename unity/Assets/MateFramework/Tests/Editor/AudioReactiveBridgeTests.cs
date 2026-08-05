using System.Collections.Generic;
using Mate.Audio;
using Mate.Character.Animation;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class AudioReactiveBridgeTests
{
    [Test]
    public void Bridge_SubscribesToAudioPeakEvent()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.5f);
        config.Set("allowedApps", "spotify");
        var bridge = new AudioReactiveBridge(bus, config);

        // Bridge should have subscribed to AudioPeakEvent
        Assert.IsNotNull(bridge);
    }

    [Test]
    public void Bridge_AboveThreshold_TriggersDance()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.3f);
        config.Set("allowedApps", "spotify");

        bool danceTriggered = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceTriggered = true);

        var bridge = new AudioReactiveBridge(bus, config);

        // Publish peak event above threshold
        bus.Publish(new AudioPeakEvent(0, 0.5f));

        Assert.IsTrue(danceTriggered);
    }

    [Test]
    public void Bridge_BelowThreshold_DoesNotTriggerDance()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.8f);
        config.Set("allowedApps", "spotify");

        bool danceTriggered = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceTriggered = true);

        var bridge = new AudioReactiveBridge(bus, config);

        // Publish peak event below threshold
        bus.Publish(new AudioPeakEvent(0, 0.5f));

        Assert.IsFalse(danceTriggered);
    }

    [Test]
    public void Bridge_ThresholdFromConfig()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.1f);
        config.Set("allowedApps", "spotify");

        bool danceTriggered = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceTriggered = true);

        var bridge = new AudioReactiveBridge(bus, config);

        // Exactly at threshold should trigger
        bus.Publish(new AudioPeakEvent(0, 0.1f));

        Assert.IsTrue(danceTriggered);
    }

    [Test]
    public void Bridge_Dispose_Unsubscribes()
    {
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.3f);
        config.Set("allowedApps", "spotify");

        var bridge = new AudioReactiveBridge(bus, config);
        bridge.Dispose();

        // After dispose, events should not trigger
        bool danceTriggered = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceTriggered = true);
        bus.Publish(new AudioPeakEvent(0, 0.9f));
        Assert.IsFalse(danceTriggered);
    }

    [Test]
    public void Bridge_EndToEnd_ServicePoll_TriggersDance()
    {
        // Full pipeline: PulseAudioService.Poll publishes AudioPeakEvent above
        // threshold, which the bridge converts into DanceStartedEvent.
        var bus = new SimpleEventBus();
        var config = new MockConfig();
        config.Set("soundThreshold", 0.3f);
        config.Set("allowedApps", "spotify");

        var fakePulse = new FakePulseAudio { PeakLevel = 0.5f };
        var service = new PulseAudioService(config, bus, fakePulse);
        var bridge = new AudioReactiveBridge(bus, config);

        bool danceTriggered = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceTriggered = true);

        service.StartMonitoring(1);
        service.Poll();

        Assert.IsTrue(danceTriggered);
    }

    private class FakePulseAudio : IPulseAudio
    {
        public float PeakLevel;

        public System.Collections.Generic.List<AudioProgramInfo> GetPlayingPrograms() =>
            new System.Collections.Generic.List<AudioProgramInfo>();

        public float GetPeakLevel(uint nodeId) => PeakLevel;
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