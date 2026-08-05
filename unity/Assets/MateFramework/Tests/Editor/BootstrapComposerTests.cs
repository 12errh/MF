using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mate.AI;
using Mate.Audio;
using Mate.Bootstrap;
using Mate.Character;
using Mate.Character.Animation;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Interfaces;
using Mate.Mods;
using Mate.System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BootstrapComposerTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mate-boot-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void WriteToml(string content) => File.WriteAllText(Path.Combine(_dir, "mate.toml"), content);

    [Test]
    public void Compose_RegistersAllServices_WithFakeAdapters()
    {
        WriteToml("[project]\nname = \"demo\"\nruntime = \"1.0.0\"\n");
        var adapters = new BootstrapComposer.Adapters
        {
            VrmLoader = new FakeVrmLoader(),
            PulseAudio = new FakePulseAudio(),
        };

        using var ctx = BootstrapComposer.Compose(_dir, adapters);

        Assert.IsNotNull(ctx.Resolve<ICharacterService>());
        Assert.IsNotNull(ctx.Resolve<IMouseTracker>());
        Assert.IsNotNull(ctx.Resolve<IAnimationService>());
        Assert.IsNotNull(ctx.Resolve<IAudioService>());
        Assert.IsNotNull(ctx.Resolve<ISystemService>());
        Assert.IsNotNull(ctx.Resolve<IAIService>());
        Assert.IsNotNull(ctx.Resolve<IModService>());
    }

    [Test]
    public void Compose_ConfigBackedByMateToml()
    {
        WriteToml("[audio]\nthreshold = 0.6\n");
        using var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = new FakeVrmLoader(),
            PulseAudio = new FakePulseAudio(),
        });

        var config = ctx.Resolve<IConfiguration>();
        Assert.AreEqual(0.6f, config.GetFloat("soundThreshold", 0.2f), 0.001f);
    }

    [Test]
    public async Task LoadConfiguredModel_Success_WhenModelExists()
    {
        WriteToml("[character]\nmodel = \"assets/avatar.vrm\"\n");
        Directory.CreateDirectory(Path.Combine(_dir, "assets"));
        File.WriteAllText(Path.Combine(_dir, "assets", "avatar.vrm"), "fake");

        var fake = new FakeVrmLoader();
        using var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = fake,
            PulseAudio = new FakePulseAudio(),
        });

        await BootstrapComposer.LoadConfiguredModelAsync(ctx, _dir);
        Assert.IsTrue(fake.Loaded);
        Assert.IsTrue(ctx.Resolve<ICharacterService>().IsLoaded);
    }

    [Test]
    public async Task LoadConfiguredModel_NoThrow_WhenModelMissing()
    {
        WriteToml("[character]\nmodel = \"assets/missing.vrm\"\n");
        using var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = new FakeVrmLoader(),
            PulseAudio = new FakePulseAudio(),
        });

        await BootstrapComposer.LoadConfiguredModelAsync(ctx, _dir);
    }

    [Test]
    public async Task LoadConfiguredModel_NoOp_WhenNoModelConfigured()
    {
        WriteToml("[project]\nname = \"demo\"\n");
        var fake = new FakeVrmLoader();
        using var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = fake,
            PulseAudio = new FakePulseAudio(),
        });

        await BootstrapComposer.LoadConfiguredModelAsync(ctx, _dir);
        Assert.IsFalse(fake.Loaded);
    }

    [Test]
    public void Compose_WiresAudioToDance_Bridge()
    {
        WriteToml("[audio]\nthreshold = 0.3\nallowed_apps = [\"spotify\"]\n");
        using var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = new FakeVrmLoader(),
            PulseAudio = new FakePulseAudio(),
        });

        var bus = ctx.Resolve<IEventBus>();
        bool danced = false;
        bus.Subscribe<DanceStartedEvent>(_ => danced = true);

        bus.Publish(new AudioPeakEvent(0, 0.5f));
        Assert.IsTrue(danced);
    }

    [Test]
    public void Compose_Dispose_UnsubscribesAudioBridge()
    {
        WriteToml("[audio]\nthreshold = 0.3\n");
        var ctx = BootstrapComposer.Compose(_dir, new BootstrapComposer.Adapters
        {
            VrmLoader = new FakeVrmLoader(),
            PulseAudio = new FakePulseAudio(),
        });

        var bus = ctx.Resolve<IEventBus>();
        bool danced = false;
        bus.Subscribe<DanceStartedEvent>(_ => danced = true);

        bus.Publish(new AudioPeakEvent(0, 0.5f));
        Assert.IsTrue(danced, "bridge should react before dispose");

        ctx.Dispose();

        danced = false;
        bus.Publish(new AudioPeakEvent(0, 0.5f));
        Assert.IsFalse(danced, "bridge must unsubscribe on context dispose");
    }

    private class FakeVrmLoader : IVrmLoader
    {
        public bool Loaded;
        public Task<GameObject> LoadAsync(string path)
        {
            Loaded = true;
            return Task.FromResult(new GameObject("fake-model"));
        }

        public Task UnloadAsync(GameObject model)
        {
            if (model != null) Object.DestroyImmediate(model);
            return Task.CompletedTask;
        }
    }

    private class FakePulseAudio : IPulseAudio
    {
        public List<AudioProgramInfo> GetPlayingPrograms() => new();
        public float GetPeakLevel(uint nodeId) => 0f;
    }
}