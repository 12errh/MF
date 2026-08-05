using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mate.Character;
using Mate.Core;
using Mate.Interfaces;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CharacterServiceTests
{
    private MateContext _ctx;
    private FakeVrmLoader _loader;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        _ctx.RegisterSingleton<IConfiguration>(new MockConfiguration());
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        _loader = new FakeVrmLoader();
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public void CharacterService_ImplementsICharacterService()
    {
        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _loader);
        Assert.IsInstanceOf<ICharacterService>(svc);
    }

    [Test]
    public void IsLoaded_False_WhenNoModel()
    {
        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _loader);
        Assert.IsFalse(svc.IsLoaded);
    }

    [Test]
    public void CurrentModel_Null_WhenNoModel()
    {
        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _loader);
        Assert.IsNull(svc.CurrentModel);
    }

    [Test]
    public async Task LoadModel_FailsWithNonexistentPath()
    {
        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _loader);
        var result = await svc.LoadModel("/nonexistent/avatar.vrm");
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Error.Contains("not found"));
    }

    [Test]
    public async Task UnloadModel_BeforeLoad_DoesNotThrow()
    {
        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(), _loader);
        var result = await svc.UnloadModel();
        Assert.IsTrue(result.IsSuccess);
    }

    [Test]
    public async Task LoadModel_WithValidFile_LoadsAndPublishesEvent()
    {
        string path = CreateTempVrmFile();

        var bus = _ctx.Resolve<IEventBus>();
        bool eventFired = false;
        string eventPath = null;
        bus.Subscribe<ModelLoadedEvent>(e => { eventFired = true; eventPath = e.Path; });

        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), bus, _loader);
        var result = await svc.LoadModel(path);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(svc.IsLoaded);
        Assert.IsNotNull(svc.CurrentModel);
        Assert.IsTrue(eventFired);
        Assert.AreEqual(path, eventPath);
    }

    [Test]
    public async Task UnloadModel_AfterLoad_UnloadsAndPublishesEvent()
    {
        string path = CreateTempVrmFile();

        var bus = _ctx.Resolve<IEventBus>();
        bool eventFired = false;
        bus.Subscribe<ModelUnloadedEvent>(_ => eventFired = true);

        var svc = new CharacterService(_ctx.Resolve<IConfiguration>(), bus, _loader);
        await svc.LoadModel(path);
        var result = await svc.UnloadModel();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(svc.IsLoaded);
        Assert.IsNull(svc.CurrentModel);
        Assert.IsTrue(eventFired);
        Assert.AreEqual(1, _loader.UnloadCount);
    }

    private string CreateTempVrmFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mate-character-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "avatar.vrm");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return path;
    }

    private class FakeVrmLoader : IVrmLoader
    {
        public int UnloadCount;

        public Task<GameObject> LoadAsync(string path) =>
            Task.FromResult(new GameObject("FakeModel"));

        public Task UnloadAsync(GameObject model)
        {
            UnloadCount++;
            if (model != null)
            {
                // EditMode tests cannot use Object.Destroy; DestroyImmediate is required.
                Object.DestroyImmediate(model);
            }
            return Task.CompletedTask;
        }
    }

    private class MockConfiguration : IConfiguration
    {
        private readonly Dictionary<string, object> _values = new();
        public float GetFloat(string key, float def) => _values.TryGetValue(key, out var v) && v is float f ? f : def;
        public int GetInt(string key, int def) => _values.TryGetValue(key, out var v) && v is int i ? i : def;
        public string GetString(string key, string def) => _values.TryGetValue(key, out var v) && v is string s ? s : def;
        public bool GetBool(string key, bool def) => _values.TryGetValue(key, out var v) && v is bool b ? b : def;
        public void Set(string key, object value) => _values[key] = value;
        public void Save() { }
        public void Reload() { }
    }
}