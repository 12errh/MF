using System.Collections.Generic;
using Mate.Character.Animation;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class CharacterAnimatorTests
{
    [Test]
    public void CharacterAnimator_ImplementsIAnimationService()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<IAnimationService>(animator);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_NotDancing_ByDefault()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.IsFalse(animator.IsDancing);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_DanceSwitchTime_FromConfig()
    {
        var config = new MockConfig();
        config.Set("danceSwitchTime", 5.0f);
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(config);
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.AreEqual(5.0f, animator.DanceSwitchTime, 0.001f);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_IdleSwitchTime_FromConfig()
    {
        var config = new MockConfig();
        config.Set("idleSwitchTime", 10.0f);
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(config);
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.AreEqual(10.0f, animator.IdleSwitchTime, 0.001f);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_TriggerDance_PublishesEvent()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool danceEvent = false;
        string danceType = null;
        bus.Subscribe<DanceStartedEvent>(e => { danceEvent = true; danceType = e.DanceType; });

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.TriggerDance();

        Assert.IsTrue(danceEvent);
        Assert.IsTrue(animator.IsDancing);
        Assert.AreEqual("dance_0", danceType);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_StopDance_PublishesEvent()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool stoppedEvent = false;
        bus.Subscribe<DanceStoppedEvent>(_ => stoppedEvent = true);

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.TriggerDance();
        animator.StopDance();

        Assert.IsTrue(stoppedEvent);
        Assert.IsFalse(animator.IsDancing);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_SetIdleState_PublishesEvent()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        bool idleChanged = false;
        int idleIndex = -1;
        bus.Subscribe<IdleChangedEvent>(e => { idleChanged = true; idleIndex = e.Index; });

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.SetIdleState(3);

        Assert.IsTrue(idleChanged);
        Assert.AreEqual(3, idleIndex);
        ctx.Dispose();
    }

    [Test]
    public void CharacterAnimator_TriggerDance_Twice_PublishesOnce()
    {
        var bus = new SimpleEventBus();
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(bus);

        int danceEventCount = 0;
        bus.Subscribe<DanceStartedEvent>(_ => danceEventCount++);

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.TriggerDance();
        animator.TriggerDance();

        Assert.AreEqual(1, danceEventCount);
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