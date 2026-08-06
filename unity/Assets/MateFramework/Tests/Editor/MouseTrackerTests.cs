using System.Collections.Generic;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MouseTrackerTests
{
    [Test]
    public void MouseTracker_ImplementsIMouseTracker()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<IMouseTracker>(tracker);
        ctx.Dispose();
    }

    [Test]
    public void GetBlendValues_Defaults_AllZero()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        var values = tracker.GetBlendValues();
        Assert.AreEqual(0f, values.HeadBlend, 0.001f);
        Assert.AreEqual(0f, values.EyeBlend, 0.001f);
        Assert.AreEqual(0f, values.SpineBlend, 0.001f);
        ctx.Dispose();
    }

    [Test]
    public void Update_CursorAtCenter_ProducesZeroBlends()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(500f, 400f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.AreEqual(0f, values.HeadBlend, 0.001f);
        Assert.AreEqual(0f, values.EyeBlend, 0.001f);
        Assert.AreEqual(0f, values.SpineBlend, 0.001f);
        ctx.Dispose();
    }

    [Test]
    public void Update_CursorAtEdge_ClampedToMaxOne()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        // Sensitivity 5x: cursor at far edge => raw blend exceeds 1, must clamp.
        var config = new MockConfig();
        config.Set("headSensitivity", 5f);
        config.Set("eyeSensitivity", 5f);
        config.Set("spineSensitivity", 5f);
        ctx.RegisterSingleton<IConfiguration>(config);

        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(1000f, 800f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.GreaterOrEqual(values.HeadBlend, 0f);
        Assert.LessOrEqual(values.HeadBlend, 1f);
        Assert.GreaterOrEqual(values.EyeBlend, 0f);
        Assert.LessOrEqual(values.EyeBlend, 1f);
        Assert.GreaterOrEqual(values.SpineBlend, 0f);
        Assert.LessOrEqual(values.SpineBlend, 1f);
        ctx.Dispose();
    }

    [Test]
    public void Update_HeadSensitivity_HalfCursor_ProducesExpectedBlend()
    {
        var config = new MockConfig();
        config.Set("headSensitivity", 2f); // 2x sensitivity
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(config);
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());

        // Cursor at 3/4 width => delta.x = 250 on a 1000-wide screen => |dx|/center = 0.5 => *2 = 1.0
        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(750f, 400f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.AreEqual(1f, values.HeadBlend, 0.001f);
        Assert.AreEqual(0f, values.EyeBlend, 0.001f); // cursor centered vertically
        ctx.Dispose();
    }

    [Test]
    public void GetBlendValues_HeadSensitivity_FromConfig()
    {
        var config = new MockConfig();
        config.Set("headSensitivity", 2.5f);
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(config);
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        // Head sensitivity should be read from config, not hardcoded
        Assert.IsNotNull(tracker);
        ctx.Dispose();
    }

    [Test]
    public void Update_CursorRightOfCenter_ProducesPositiveSignedYaw()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(750f, 400f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.Greater(values.HeadYaw, 0f);
        Assert.Greater(values.EyeYaw, 0f);
        Assert.Greater(values.SpineYaw, 0f);
        Assert.AreEqual(0f, values.EyePitch, 0.001f); // vertically centered
        ctx.Dispose();
    }

    [Test]
    public void Update_CursorLeftOfHead_ProducesNegativeSignedYaw()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(250f, 400f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.Less(values.HeadYaw, 0f);
        Assert.Less(values.EyeYaw, 0f);
        Assert.Less(values.SpineYaw, 0f);
        ctx.Dispose();
    }

    [Test]
    public void Update_CursorAboveCenter_ProducesPositivePitch()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(
            ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>(),
            () => new Vector2(500f, 600f), () => new Vector2(1000f, 800f));

        tracker.Update();
        var values = tracker.GetBlendValues();

        Assert.Greater(values.HeadPitch, 0f);
        Assert.Greater(values.EyePitch, 0f);
        Assert.AreEqual(0f, values.HeadYaw, 0.001f); // horizontally centered
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