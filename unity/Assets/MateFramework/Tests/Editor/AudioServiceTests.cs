using System.Collections.Generic;
using Mate.Audio;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class AudioServiceTests
{
    private MateContext _ctx;
    private FakePulseAudio _pulse;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        var config = new MockConfig();
        config.Set("allowedApps", "spotify,firefox");
        config.Set("soundThreshold", 0.3f);
        _ctx.RegisterSingleton<IConfiguration>(config);
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        _pulse = new FakePulseAudio();
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public void AudioService_ImplementsIAudioService()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsInstanceOf<IAudioService>(svc);
    }

    [Test]
    public void AudioService_NotMonitoring_ByDefault()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsFalse(svc.IsMonitoring);
    }

    [Test]
    public void AudioService_GetPeakLevel_Zero_ForUnmonitoredNode()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        float level = svc.GetPeakLevel(999);
        Assert.AreEqual(0f, level, 0.001f);
    }

    [Test]
    public void AudioService_GetPeakLevel_DelegatesToPulse_WhenMonitored()
    {
        _pulse.PeakLevel = 0.75f;
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        svc.StartMonitoring(1);
        Assert.AreEqual(0.75f, svc.GetPeakLevel(1), 0.001f);
    }

    [Test]
    public void AudioService_AllowedApps_FromConfig()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsTrue(svc.IsAllowedApp("spotify"));
        Assert.IsTrue(svc.IsAllowedApp("firefox"));
        Assert.IsFalse(svc.IsAllowedApp("unknown"));
    }

    [Test]
    public void AudioService_Threshold_FromConfig()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.AreEqual(0.3f, svc.Threshold, 0.001f);
    }

    [Test]
    public void AudioService_IsAppPlaying_True_ForPlayingAllowedApp()
    {
        _pulse.Programs.Add(new AudioProgramInfo("spotify", "spotify", 100, 0.5));
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsTrue(svc.IsAppPlaying("spotify"));
    }

    [Test]
    public void AudioService_IsAppPlaying_False_ForAllowedButNotPlaying()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsFalse(svc.IsAppPlaying("spotify"));
    }

    [Test]
    public void AudioService_IsAppPlaying_False_ForNotAllowedApp()
    {
        _pulse.Programs.Add(new AudioProgramInfo("chrome", "chrome", 200, 0.5));
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        Assert.IsFalse(svc.IsAppPlaying("chrome"));
    }

    [Test]
    public void AudioService_GetPlayingAudioPrograms_ReturnsProcessIds()
    {
        _pulse.Programs.Add(new AudioProgramInfo("spotify", "spotify", 100, 0.5));
        _pulse.Programs.Add(new AudioProgramInfo("firefox", "firefox", 200, 0.2));
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        var ids = svc.GetPlayingAudioPrograms();
        Assert.AreEqual(2, ids.Length);
        Assert.Contains(100, ids);
        Assert.Contains(200, ids);
    }

    [Test]
    public void AudioService_StartStopMonitoring_TogglesState()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        svc.StartMonitoring(1);
        Assert.IsTrue(svc.IsMonitoring);
        svc.StopMonitoring(1);
        Assert.IsFalse(svc.IsMonitoring);
    }

    [Test]
    public void AudioService_Poll_PublishesPeakEvent_ForMonitoredNode()
    {
        _pulse.PeakLevel = 0.5f;
        var bus = _ctx.Resolve<IEventBus>();
        bool eventFired = false;
        int eventNode = -1;
        float eventLevel = 0f;
        bus.Subscribe<AudioPeakEvent>(e => { eventFired = true; eventNode = e.NodeId; eventLevel = e.Level; });

        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), bus, _pulse);
        svc.StartMonitoring(7);
        svc.Poll();

        Assert.IsTrue(eventFired);
        Assert.AreEqual(7, eventNode);
        Assert.AreEqual(0.5f, eventLevel, 0.001f);
    }

    [Test]
    public void AudioService_Poll_DoesNotPublish_ForUnmonitoredNode()
    {
        _pulse.PeakLevel = 0.5f;
        var bus = _ctx.Resolve<IEventBus>();
        bool eventFired = false;
        bus.Subscribe<AudioPeakEvent>(_ => eventFired = true);

        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), bus, _pulse);
        svc.Poll();

        Assert.IsFalse(eventFired);
    }

    [Test]
    public void AudioService_Poll_FiresOnPeakLevelChanged()
    {
        _pulse.PeakLevel = 0.6f;
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _pulse);
        svc.StartMonitoring(1);

        bool fired = false;
        svc.OnPeakLevelChanged += (node, level) => { fired = true; };
        svc.Poll();

        Assert.IsTrue(fired);
    }

    private class FakePulseAudio : IPulseAudio
    {
        public float PeakLevel;
        public readonly List<AudioProgramInfo> Programs = new();

        public List<AudioProgramInfo> GetPlayingPrograms() => Programs;

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