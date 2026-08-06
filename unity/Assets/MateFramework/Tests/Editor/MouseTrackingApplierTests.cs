using System.Collections.Generic;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MouseTrackingApplierTests
{
    [Test]
    public void Applier_NoModel_DoesNotThrow()
    {
        var go = new GameObject("Applier");
        var applier = go.AddComponent<MouseTrackingApplier>();
        var config = new MockConfig();
        var tracker = new FakeTracker();
        var character = new FakeCharacter { Model = null };

        applier.Bind(config, tracker, character);
        Assert.DoesNotThrow(() => applier.Update());
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Applier_RotatesHeadBone_FromSignedBlend()
    {
        var go = new GameObject("Applier");
        var applier = go.AddComponent<MouseTrackingApplier>();

        var model = new GameObject("Model");
        var head = new GameObject("Head");
        head.transform.SetParent(model.transform);

        var config = new MockConfig();
        config.Set("headMaxAngle", 20f);
        var tracker = new FakeTracker { HeadYaw = 1f, HeadPitch = 0f };
        var character = new FakeCharacter { Model = model };

        applier.Bind(config, tracker, character);
        applier.Update();

        // Cursor fully right of center => head yawed +20° about Y.
        var euler = head.transform.localRotation.eulerAngles;
        Assert.AreEqual(20f, euler.y, 0.01f);
        Assert.AreEqual(0f, euler.x, 0.01f);

        Object.DestroyImmediate(model);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Applier_NegativeYaw_RotatesOppositeDirection()
    {
        var go = new GameObject("Applier");
        var applier = go.AddComponent<MouseTrackingApplier>();

        var model = new GameObject("Model");
        var head = new GameObject("Head");
        head.transform.SetParent(model.transform);

        var config = new MockConfig();
        config.Set("headMaxAngle", 20f);
        var tracker = new FakeTracker { HeadYaw = -1f, HeadPitch = 0f };
        var character = new FakeCharacter { Model = model };

        applier.Bind(config, tracker, character);
        applier.Update();

        // eulerAngles normalizes -20° to 340°; assert the signed equivalent.
        var y = head.transform.localRotation.eulerAngles.y;
        if (y > 180f) y -= 360f;
        Assert.AreEqual(-20f, y, 0.01f);

        Object.DestroyImmediate(model);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Applier_MaxAngle_FromConfig()
    {
        var go = new GameObject("Applier");
        var applier = go.AddComponent<MouseTrackingApplier>();

        var model = new GameObject("Model");
        var head = new GameObject("Head");
        head.transform.SetParent(model.transform);

        var config = new MockConfig();
        config.Set("headMaxAngle", 45f);
        var tracker = new FakeTracker { HeadYaw = 1f, HeadPitch = 0f };
        var character = new FakeCharacter { Model = model };

        applier.Bind(config, tracker, character);
        applier.Update();

        Assert.AreEqual(45f, head.transform.localRotation.eulerAngles.y, 0.01f);

        Object.DestroyImmediate(model);
        Object.DestroyImmediate(go);
    }

    private class FakeTracker : IMouseTracker
    {
        public float HeadYaw;
        public float HeadPitch;

        public MouseBlendValues GetBlendValues()
        {
            var v = new MouseBlendValues();
            v.HeadYaw = HeadYaw;
            v.HeadPitch = HeadPitch;
            v.HeadBlend = Mathf.Abs(HeadYaw);
            return v;
        }

        public void Update() { }
    }

    private class FakeCharacter : ICharacterService
    {
        public GameObject Model;
        public bool IsLoaded => Model != null;
        public GameObject CurrentModel => Model;
        public event System.Action OnModelLoaded;
        public event System.Action OnModelUnloaded;
        public System.Threading.Tasks.Task<Result> LoadModel(string path) => System.Threading.Tasks.Task.FromResult(Result.Ok());
        public System.Threading.Tasks.Task<Result> UnloadModel() => System.Threading.Tasks.Task.FromResult(Result.Ok());
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