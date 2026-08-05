using System.Collections.Generic;
using Mate.Character;
using Mate.Character.Animation;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class CharacterModuleTests
{
    [Test]
    public void MateContext_ResolvesICharacterService()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        ctx.Register<ICharacterService>(() => new CharacterService(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));

        var svc = ctx.Resolve<ICharacterService>();
        Assert.IsNotNull(svc);
        Assert.IsInstanceOf<CharacterService>(svc);
        ctx.Dispose();
    }

    [Test]
    public void MateContext_ResolvesIMouseTracker()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        ctx.Register<IMouseTracker>(() => new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));

        var tracker = ctx.Resolve<IMouseTracker>();
        Assert.IsNotNull(tracker);
        ctx.Dispose();
    }

    [Test]
    public void MateContext_ResolvesIAnimationService()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        ctx.Register<IAnimationService>(() => new CharacterAnimator(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));

        var anim = ctx.Resolve<IAnimationService>();
        Assert.IsNotNull(anim);
        ctx.Dispose();
    }

    [Test]
    public void CharacterModule_AllServicesCommunicateViaEventBus()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool danceStarted = false;
        bool danceStopped = false;
        bus.Subscribe<DanceStartedEvent>(_ => danceStarted = true);
        bus.Subscribe<DanceStoppedEvent>(_ => danceStopped = true);

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.TriggerDance();
        Assert.IsTrue(danceStarted);

        animator.StopDance();
        Assert.IsTrue(danceStopped);
        ctx.Dispose();
    }

    [Test]
    public void CharacterModule_ServicesShareMateContextSingletons()
    {
        var ctx = new MateContext();
        var config = new MockConfig();
        var bus = new SimpleEventBus();
        ctx.RegisterSingleton<IConfiguration>(config);
        ctx.RegisterSingleton<IEventBus>(bus);

        ctx.Register<ICharacterService>(() => new CharacterService(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));
        ctx.Register<IMouseTracker>(() => new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));
        ctx.Register<IAnimationService>(() => new CharacterAnimator(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>()));

        Assert.AreSame(config, ctx.Resolve<IConfiguration>());
        Assert.AreSame(bus, ctx.Resolve<IEventBus>());
        Assert.IsNotNull(ctx.Resolve<ICharacterService>());
        Assert.IsNotNull(ctx.Resolve<IMouseTracker>());
        Assert.IsNotNull(ctx.Resolve<IAnimationService>());
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