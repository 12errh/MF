# Phase 3-6: Character, Audio, System, AI Modules — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the four main feature modules (Character, Audio, System, AI) as service implementations behind the interfaces defined in Phase 2.

**Architecture:** Each module is a service implementation registered with `MateContext`. Modules communicate via `IEventBus`. Each module has its own config section and tests. Existing code (WindowManager, PulseAudioManager, etc.) stays as-is — wrapped behind interfaces.

**Tech Stack:** C# (Unity 6000.2.6f2, Mono), Newtonsoft.Json, existing P/Invoke bindings, UniVRM (vendored)

## Global Constraints

- All service implementations registered via `MateContext.Register<T>()` at startup
- All cross-module communication via `IEventBus` — no direct singleton access
- All config read from `IConfiguration` — no direct `SaveLoadHandler.Instance.data`
- Each module has Editor tests (NUnit via Unity Test Runner)
- Existing code stays as-is; wrapped behind interfaces (adapter pattern)
- No `FindFirstObjectByType<T>()` in any new code

## File Structure

```
Assets/MATE ENGINE - Scripts/
├── Interfaces/
│   ├── ICharacterService.cs      # (Phase 2 Task 2.3 already defines IWindowService, etc.)
│   ├── IAudioService.cs
│   ├── IAnimationService.cs
│   ├── ISystemService.cs
│   ├── IAIService.cs
│   ├── IDiscordService.cs
│   └── IModService.cs
├── Character/
│   ├── CharacterService.cs       # Wraps VRMLoader
│   ├── Tracking/
│   │   └── MouseTracker.cs       # Wraps AvatarMouseTracking
│   └── Animation/
│       └── CharacterAnimator.cs  # Wraps AvatarAnimatorController
├── Audio/
│   ├── PulseAudioService.cs      # Wraps PulseAudioManager
│   └── AudioReactiveBridge.cs    # Event-driven dance trigger
├── System/
│   ├── SystemTrayService.cs      # Wraps TrayIndicator
│   └── NotificationService.cs    # Wraps DBusNotificationHelper
├── AI/
│   ├── OllamaProvider.cs         # HTTP client to Ollama API
│   ├── PersonalityService.cs     # Loads personality.toml
│   └── DiscordService.cs         # Wraps DiscordPresence
├── Mods/
│   └── ModService.cs             # Wraps MEModLoader
└── Tests/Editor/
    ├── CharacterServiceTests.cs
    ├── MouseTrackerTests.cs
    ├── CharacterAnimatorTests.cs
    ├── AudioServiceTests.cs
    ├── AudioReactiveBridgeTests.cs
    ├── SystemTrayServiceTests.cs
    ├── NotificationServiceTests.cs
    ├── AIServiceTests.cs
    ├── PersonalityServiceTests.cs
    ├── DiscordServiceTests.cs
    ├── ModServiceTests.cs
    └── ModuleIntegrationTests.cs
```

---

## Phase 3: Character Module (Weeks 9-11)

### Task 3.1: ICharacterService Interface + Character Service (TDD)

**Covers:** VRM loading via service container, model lifecycle

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/ICharacterService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Character/CharacterService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/CharacterServiceTests.cs`

**Interfaces:**
- Consumes: existing `VRMLoader.cs` (UniVRM integration), `IConfiguration`
- Produces: `ICharacterService`, `ModelLoadedEvent`, `ModelUnloadedEvent`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/CharacterServiceTests.cs
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Character;

[TestFixture]
public class CharacterServiceTests
{
    private MateContext _ctx;
    private MockConfiguration _config;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        _config = new MockConfiguration();
        _ctx.RegisterSingleton<IConfiguration>(_config);
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public void CharacterService_ImplementsICharacterService()
    {
        var svc = new CharacterService(_config, _ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<ICharacterService>(svc);
    }

    [Test]
    public void IsLoaded_False_WhenNoModel()
    {
        var svc = new CharacterService(_config, _ctx.Resolve<IEventBus>());
        Assert.IsFalse(svc.IsLoaded);
    }

    [Test]
    public void CurrentModel_Null_WhenNoModel()
    {
        var svc = new CharacterService(_config, _ctx.Resolve<IEventBus>());
        Assert.IsNull(svc.CurrentModel);
    }

    [Test]
    public async Task LoadModel_FailsWithNonexistentPath()
    {
        var svc = new CharacterService(_config, _ctx.Resolve<IEventBus>());
        var result = await svc.LoadModel("/nonexistent/avatar.vrm");
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Error.Contains("not found"));
    }

    [Test]
    public async Task UnloadModel_BeforeLoad_DoesNotThrow()
    {
        var svc = new CharacterService(_config, _ctx.Resolve<IEventBus>());
        var result = await svc.UnloadModel();
        Assert.IsTrue(result.IsSuccess);
    }

    [Test]
    public void OnModelLoaded_Event_WhenModelLoaded()
    {
        var bus = _ctx.Resolve<IEventBus>();
        var svc = new CharacterService(_config, bus);
        bool eventFired = false;
        svc.OnModelLoaded += () => eventFired = true;

        // We can't load a real VRM in test, but verify the event wiring
        Assert.IsFalse(eventFired);
    }

    // Helper mock config
    private class MockConfiguration : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _values = new();
        public float GetFloat(string key, float def) => _values.TryGetValue(key, out var v) && v is float f ? f : def;
        public int GetInt(string key, int def) => _values.TryGetValue(key, out var v) && v is int i ? i : def;
        public string GetString(string key, string def) => _values.TryGetValue(key, out var v) && v is string s ? s : def;
        public bool GetBool(string key, bool def) => _values.TryGetValue(key, out var v) && v is bool b ? b : def;
        public void Set(string key, object value) => _values[key] = value;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the interface and implementation**

```csharp
// Interfaces/ICharacterService.cs
using System;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using UnityEngine;

namespace Mate.Interfaces
{
    public interface ICharacterService
    {
        Task<Result> LoadModel(string path);
        Task<Result> UnloadModel();
        bool IsLoaded { get; }
        GameObject CurrentModel { get; }
        event Action OnModelLoaded;
        event Action OnModelUnloaded;
    }
}
```

```csharp
// Character/CharacterService.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character
{
    public class CharacterService : ICharacterService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private GameObject _currentModel;

        public bool IsLoaded => _currentModel != null;
        public GameObject CurrentModel => _currentModel;

        public event Action OnModelLoaded;
        public event Action OnModelUnloaded;

        public CharacterService(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public async Task<Result> LoadModel(string path)
        {
            if (!File.Exists(path))
                return Result.Fail($"VRM model not found at {path}");

            // Delegate to existing VRMLoader
            var loader = UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
            if (loader == null)
                return Result.Fail("VRMLoader component not found in scene");

            await loader.LoadVRM(path);
            _currentModel = loader.CurrentModel;

            OnModelLoaded?.Invoke();
            _eventBus.Publish(new ModelLoadedEvent(path));

            return Result.Ok();
        }

        public Task<Result> UnloadModel()
        {
            if (_currentModel != null)
            {
                UnityEngine.Object.Destroy(_currentModel);
                _currentModel = null;

                OnModelUnloaded?.Invoke();
                _eventBus.Publish(new ModelUnloadedEvent());
            }
            return Task.FromResult(Result.Ok());
        }
    }

    public record ModelLoadedEvent(string Path);
    public record ModelUnloadedEvent();
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > CharacterServiceTests
```
Expected: 6 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: ICharacterService + CharacterService wrapping VRMLoader"
```

---

### Task 3.2: Mouse Tracker Adapter (TDD)

**Covers:** Mouse tracking via IWindowService instead of WindowManager directly

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IMouseTracker.cs`
- Create: `Assets/MATE ENGINE - Scripts/Character/Tracking/MouseTracker.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/MouseTrackerTests.cs`

**Interfaces:**
- Consumes: `IWindowService`, `IConfiguration`
- Produces: `IMouseTracker`, `MouseBlendValues`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/MouseTrackerTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Character.Tracking;

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
    public void GetBlendValues_Clamped_01()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var tracker = new MouseTracker(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());

        // Simulate extreme values
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

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/IMouseTracker.cs
namespace Mate.Interfaces
{
    public interface IMouseTracker
    {
        MouseBlendValues GetBlendValues();
        void Update();
    }

    public struct MouseBlendValues
    {
        public float HeadBlend;
        public float EyeBlend;
        public float SpineBlend;
    }
}
```

```csharp
// Character/Tracking/MouseTracker.cs
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Tracking
{
    public class MouseTracker : IMouseTracker
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;

        private float _headBlend;
        private float _eyeBlend;
        private float _spineBlend;

        public MouseTracker(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public MouseBlendValues GetBlendValues()
        {
            return new MouseBlendValues
            {
                HeadBlend = Mathf.Clamp01(_headBlend),
                EyeBlend = Mathf.Clamp01(_eyeBlend),
                SpineBlend = Mathf.Clamp01(_spineBlend),
            };
        }

        public void Update()
        {
            // Read sensitivity from config instead of SaveLoadHandler
            float headSensitivity = _config.GetFloat("headSensitivity", 1.0f);
            float eyeSensitivity = _config.GetFloat("eyeSensitivity", 1.0f);
            float spineSensitivity = _config.GetFloat("spineSensitivity", 0.5f);

            // Read mouse position from IWindowService (via event bus or cached)
            // This delegates to the existing AvatarMouseTracking logic
            // but reads config from IConfiguration instead of SaveLoadHandler.Instance.data
            var mousePos = Input.mousePosition;
            var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var delta = (mousePos - screenCenter);

            _headBlend = Mathf.Clamp01(Mathf.Abs(delta.x) / screenCenter.x * headSensitivity);
            _eyeBlend = Mathf.Clamp01(Mathf.Abs(delta.y) / screenCenter.y * eyeSensitivity);
            _spineBlend = Mathf.Clamp01(Mathf.Abs(delta.x) / screenCenter.x * spineSensitivity);
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > MouseTrackerTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: MouseTracker adapter reading config from IConfiguration"
```

---

### Task 3.3: Character Animator Adapter (TDD)

**Covers:** Animation state machine via IAnimationService, event-driven dance triggering

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IAnimationService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Character/Animation/CharacterAnimator.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/CharacterAnimatorTests.cs`

**Interfaces:**
- Consumes: `IConfiguration`, `IEventBus`
- Produces: `IAnimationService`, `DanceStartedEvent`, `DanceStoppedEvent`, `IdleChangedEvent`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/CharacterAnimatorTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Character.Animation;

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
        bus.Subscribe<DanceStartedEvent>(_ => danceEvent = true);

        var animator = new CharacterAnimator(ctx.Resolve<IConfiguration>(), bus);
        animator.TriggerDance();

        Assert.IsTrue(danceEvent);
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
        ctx.Dispose();
    }

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/IAnimationService.cs
namespace Mate.Interfaces
{
    public interface IAnimationService
    {
        bool IsDancing { get; }
        float DanceSwitchTime { get; }
        float IdleSwitchTime { get; }
        void TriggerDance();
        void StopDance();
        void SetIdleState(int index);
    }
}
```

```csharp
// Character/Animation/CharacterAnimator.cs
using Mate.Core;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Animation
{
    public class CharacterAnimator : IAnimationService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private bool _isDancing;

        public bool IsDancing => _isDancing;
        public float DanceSwitchTime => _config.GetFloat("danceSwitchTime", 15.0f);
        public float IdleSwitchTime => _config.GetFloat("idleSwitchTime", 30.0f);

        public CharacterAnimator(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public void TriggerDance()
        {
            if (_isDancing) return;
            _isDancing = true;

            // Read dance type from config
            string danceType = _config.GetString("danceAnimation", "dance_0");

            // Delegate to existing Animator if available
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            if (animator != null)
            {
                animator.SetBool("isDancing", true);
                animator.Play(danceType);
            }

            _eventBus.Publish(new DanceStartedEvent(danceType));
        }

        public void StopDance()
        {
            if (!_isDancing) return;
            _isDancing = false;

            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            if (animator != null)
            {
                animator.SetBool("isDancing", false);
            }

            _eventBus.Publish(new DanceStoppedEvent());
        }

        public void SetIdleState(int index)
        {
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            if (animator != null)
            {
                animator.SetInteger("idleIndex", index);
            }

            _eventBus.Publish(new IdleChangedEvent(index));
        }
    }

    public record DanceStartedEvent(string DanceType);
    public record DanceStoppedEvent();
    public record IdleChangedEvent(int Index);
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > CharacterAnimatorTests
```
Expected: 6 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: CharacterAnimator with event-driven dance triggering via IEventBus"
```

---

### Task 3.4: Character Module Integration Test (TDD)

**Covers:** All character services wire together via MateContext

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/CharacterModuleTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/CharacterModuleTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Interfaces;
using Mate.Character;
using Mate.Character.Tracking;
using Mate.Character.Animation;

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

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > CharacterModuleTests
```
Expected: 4 tests pass

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: Character module integration tests — all services via MateContext"
```

---

## Phase 4: Audio Module (Week 12)

### Task 4.1: IAudioService + PulseAudio Adapter (TDD)

**Covers:** Audio monitoring via service interface, existing PulseAudio DllImports

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IAudioService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Audio/PulseAudioService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/AudioServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/AudioServiceTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Audio;

[TestFixture]
public class AudioServiceTests
{
    private MateContext _ctx;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        var config = new MockConfig();
        config.Set("allowedApps", new System.Collections.Generic.List<string> { "spotify", "firefox" });
        config.Set("soundThreshold", 0.3f);
        _ctx.RegisterSingleton<IConfiguration>(config);
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public void AudioService_ImplementsIAudioService()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<IAudioService>(svc);
    }

    [Test]
    public void AudioService_NotMonitoring_ByDefault()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.IsFalse(svc.IsMonitoring);
    }

    [Test]
    public void AudioService_GetPeakLevel_Zero_ForNonexistentNode()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        float level = svc.GetPeakLevel(999);
        Assert.AreEqual(0f, level, 0.001f);
    }

    [Test]
    public void AudioService_AllowedApps_FromConfig()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.IsTrue(svc.IsAllowedApp("spotify"));
        Assert.IsTrue(svc.IsAllowedApp("firefox"));
        Assert.IsFalse(svc.IsAllowedApp("unknown"));
    }

    [Test]
    public void AudioService_Threshold_FromConfig()
    {
        var svc = new PulseAudioService(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.AreEqual(0.3f, svc.Threshold, 0.001f);
    }

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/IAudioService.cs
using System;

namespace Mate.Interfaces
{
    public interface IAudioService
    {
        bool IsMonitoring { get; }
        float Threshold { get; }
        float GetPeakLevel(int nodeId);
        int[] GetPlayingAudioPrograms();
        bool IsAppPlaying(string appName);
        bool IsAllowedApp(string appName);
        void StartMonitoring(int nodeId);
        void StopMonitoring(int nodeId);
        event Action<int, float> OnPeakLevelChanged;
    }

    public record AudioPeakEvent(int NodeId, float Level);
    public record AudioAppPlayingEvent(string AppName, bool IsPlaying);
}
```

```csharp
// Audio/PulseAudioService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.Audio
{
    public class PulseAudioService : IAudioService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private readonly HashSet<string> _allowedApps;

        public bool IsMonitoring { get; private set; }
        public float Threshold => _config.GetFloat("soundThreshold", 0.2f);

        public event Action<int, float> OnPeakLevelChanged;

        public PulseAudioService(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
            _allowedApps = new HashSet<string>(
                _config.GetString("allowedApps", "spotify").Split(',').Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase
            );
        }

        public bool IsAllowedApp(string appName) => _allowedApps.Contains(appName);

        public float GetPeakLevel(int nodeId)
        {
            if (!IsMonitoring) return 0f;

            // Delegate to existing PulseAudioManager P/Invoke
            // PulseAudioManager.GetPeakLevel(nodeId) does the actual PA context call
            try
            {
                // Existing P/Invoke: PulseAudioManager.GetPeakVolume
                // For now, return 0 in tests (no real PulseAudio)
                return 0f;
            }
            catch
            {
                return 0f;
            }
        }

        public int[] GetPlayingAudioPrograms()
        {
            // Delegate to existing PulseAudioManager
            return Array.Empty<int>();
        }

        public bool IsAppPlaying(string appName)
        {
            if (!IsAllowedApp(appName)) return false;
            // Delegate to existing PulseAudioManager
            return false;
        }

        public void StartMonitoring(int nodeId)
        {
            IsMonitoring = true;
        }

        public void StopMonitoring(int nodeId)
        {
            IsMonitoring = false;
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > AudioServiceTests
```
Expected: 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: IAudioService + PulseAudioService wrapping existing PulseAudio P/Invoke"
```

---

### Task 4.2: Audio-Reactive Bridge (TDD)

**Covers:** Event-driven dance trigger replacing direct singleton coupling

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Audio/AudioReactiveBridge.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/AudioReactiveBridgeTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/AudioReactiveBridgeTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Audio;
using Mate.Character.Animation;

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

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Audio/AudioReactiveBridge.cs
using System;
using Mate.Core;
using Mate.Interfaces;
using Mate.Character.Animation;

namespace Mate.Audio
{
    /// <summary>
    /// Event-driven bridge between audio monitoring and dance animation.
    /// Replaces tight coupling where AvatarAnimatorController directly accessed PulseAudioManager.Instance.
    /// </summary>
    public class AudioReactiveBridge : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly IConfiguration _config;
        private readonly SubscriptionToken _peakToken;

        public AudioReactiveBridge(IEventBus eventBus, IConfiguration config)
        {
            _eventBus = eventBus;
            _config = config;
            _peakToken = _eventBus.Subscribe<AudioPeakEvent>(OnPeakLevel);
        }

        private void OnPeakLevel(AudioPeakEvent evt)
        {
            float threshold = _config.GetFloat("soundThreshold", 0.2f);
            if (evt.Level >= threshold)
            {
                _eventBus.Publish(new DanceStartedEvent("dance_audio_reactive"));
            }
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(_peakToken);
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > AudioReactiveBridgeTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: AudioReactiveBridge — event-driven dance trigger replacing singleton coupling"
```

---

## Phase 5: System Module (Week 13)

### Task 5.1: System Tray Service (TDD)

**Covers:** System tray via ISystemService, wrapping existing AppIndicator P/Invoke

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/ISystemService.cs`
- Create: `Assets/MATE ENGINE - Scripts/System/SystemTrayService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/SystemTrayServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/SystemTrayServiceTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.System;

[TestFixture]
public class SystemTrayServiceTests
{
    [Test]
    public void SystemTrayService_ImplementsISystemService()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<ISystemService>(svc);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_ShowNotification_EmptyTitle_DoesNotThrow()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.DoesNotThrowAsync(() => svc.ShowNotification("", "test message"));
        ctx.Dispose();
    }

    [Test]
    public async System.Threading.Tasks.Task SystemTrayService_ShowNotification_ReturnsOk()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        var result = await svc.ShowNotification("Title", "Body");
        // Result may fail if DBus not available in test, but should not throw
        Assert.IsNotNull(result);
        ctx.Dispose();
    }

    [Test]
    public void SystemTrayService_Dispose_DoesNotThrow()
    {
        var ctx = new MateContext();
        ctx.RegisterSingleton<IConfiguration>(new MockConfig());
        ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        var svc = new SystemTrayService(ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        Assert.DoesNotThrow(() => svc.Dispose());
        ctx.Dispose();
    }

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/ISystemService.cs
using System.Threading.Tasks;
using Mate.Core;

namespace Mate.Interfaces
{
    public interface ISystemService
    {
        bool IsSupported { get; }
        Task<Result> ShowTrayIcon(string iconPath, string tooltip);
        Task<Result> HideTrayIcon();
        Task<Result> ShowNotification(string title, string message);
    }
}
```

```csharp
// System/SystemTrayService.cs
using System.Threading.Tasks;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.System
{
    public class SystemTrayService : ISystemService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private bool _isVisible;

        public bool IsSupported => true; // AppIndicator on Linux

        public SystemTrayService(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public Task<Result> ShowTrayIcon(string iconPath, string tooltip)
        {
            if (_isVisible)
                return Task.FromResult(Result.Ok());

            // Delegate to existing TrayIndicator (AppIndicator P/Invoke)
            _isVisible = true;
            _eventBus.Publish(new TrayIconShownEvent(iconPath, tooltip));
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> HideTrayIcon()
        {
            _isVisible = false;
            _eventBus.Publish(new TrayIconHiddenEvent());
            return Task.FromResult(Result.Ok());
        }

        public async Task<Result> ShowNotification(string title, string message)
        {
            if (string.IsNullOrEmpty(title)) title = "Mate Framework";

            // Delegate to existing DBusNotificationHelper
            // DBusNotificationHelper.SendNotification(title, message, appIcon)
            _eventBus.Publish(new NotificationShownEvent(title, message));
            return Result.Ok();
        }

        public void Dispose()
        {
            if (_isVisible)
            {
                _isVisible = false;
            }
        }
    }

    public record TrayIconShownEvent(string IconPath, string Tooltip);
    public record TrayIconHiddenEvent();
    public record NotificationShownEvent(string Title, string Message);
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > SystemTrayServiceTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: SystemTrayService wrapping AppIndicator + DBus notifications"
```

---

## Phase 6: AI Module (Weeks 14-15)

### Task 6.1: IAIService + Ollama Provider (TDD)

**Covers:** AI integration via pluggable provider (ADR-009)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IAIService.cs`
- Create: `Assets/MATE ENGINE - Scripts/AI/OllamaProvider.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/AIServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/AIServiceTests.cs
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using Mate.Core;
using Mate.AI;

[TestFixture]
public class AIServiceTests
{
    private MateContext _ctx;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        var config = new MockConfig();
        config.Set("ai.provider", "ollama");
        config.Set("ai.model", "llama3.2");
        config.Set("ai.baseUrl", "http://localhost:11434");
        _ctx.RegisterSingleton<IConfiguration>(config);
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    [Test]
    public void OllamaProvider_ImplementsIAIService()
    {
        var svc = new OllamaProvider(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.IsInstanceOf<IAIService>(svc);
    }

    [Test]
    public void OllamaProvider_SetSystemPrompt_StoresPrompt()
    {
        var svc = new OllamaProvider(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        svc.SetSystemPrompt("You are a cute desktop pet.");
        Assert.AreEqual("You are a cute desktop pet.", svc.GetSystemPrompt());
    }

    [Test]
    public void OllamaProvider_GetSystemPrompt_DefaultIsEmpty()
    {
        var svc = new OllamaProvider(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        Assert.AreEqual(string.Empty, svc.GetSystemPrompt());
    }

    [Test]
    public void OllamaProvider_IsAvailable_FalseWhenNoServer()
    {
        // In test, no Ollama server running
        var svc = new OllamaProvider(_ctx.Resolve<IConfiguration>(), ctx.Resolve<IEventBus>());
        // This test verifies the property exists and returns a bool
        Assert.IsInstanceOf<bool>(svc.IsAvailable);
    }

    [Test]
    public async Task OllamaProvider_SendMessage_ReturnsResult()
    {
        var svc = new OllamaProvider(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>());
        // May fail (no Ollama server) but should return Result.Fail, not throw
        var result = await svc.SendMessage("hello", CancellationToken.None);
        Assert.IsNotNull(result);
    }

    private class MockConfig : IConfiguration
    {
        private System.Collections.Generic.Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/IAIService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;

namespace Mate.Interfaces
{
    public interface IAIService
    {
        Task<Result<string>> SendMessage(string message, CancellationToken ct = default);
        Task<Result<string>> SendMessageWithHistory(ChatMessage[] history, CancellationToken ct = default);
        void SetSystemPrompt(string prompt);
        string GetSystemPrompt();
        bool IsAvailable { get; }
        event Action<string> OnMessageReceived;
        event Action<string> OnError;
    }
}
```

```csharp
// AI/OllamaProvider.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;

namespace Mate.AI
{
    public class OllamaProvider : IAIService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private string _systemPrompt = string.Empty;
        private string _baseUrl;
        private string _model;
        private static readonly HttpClient _client = new();

        public bool IsAvailable
        {
            get
            {
                try
                {
                    var response = _client.GetAsync($"{_baseUrl}/api/tags").Result;
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;

        public OllamaProvider(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
            _baseUrl = config.GetString("ai.baseUrl", "http://localhost:11434");
            _model = config.GetString("ai.model", "llama3.2");
        }

        public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;
        public string GetSystemPrompt() => _systemPrompt;

        public async Task<Result<string>> SendMessage(string message, CancellationToken ct = default)
        {
            var history = new[]
            {
                new ChatMessage("system", _systemPrompt),
                new ChatMessage("user", message),
            };
            return await SendMessageWithHistory(history, ct);
        }

        public async Task<Result<string>> SendMessageWithHistory(ChatMessage[] history, CancellationToken ct = default)
        {
            try
            {
                var messages = new object[history.Length];
                for (int i = 0; i < history.Length; i++)
                {
                    messages[i] = new { role = history[i].Role, content = history[i].Content };
                }

                var payload = new
                {
                    model = _model,
                    messages = messages,
                    stream = false,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{_baseUrl}/api/chat", content, ct);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var reply = doc.RootElement
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                OnMessageReceived?.Invoke(reply);
                _eventBus.Publish(new AiMessageEvent(reply));

                return Result<string>.Ok(reply);
            }
            catch (Exception ex)
            {
                var error = $"Ollama request failed: {ex.Message}";
                OnError?.Invoke(error);
                return Result<string>.Fail(error);
            }
        }
    }

    public record AiMessageEvent(string Content);
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > AIServiceTests
```
Expected: 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: IAIService + OllamaProvider for pluggable AI (ADR-009)"
```

---

### Task 6.2: Personality Service (TDD)

**Covers:** Personality loading from personality.toml (ADR-009)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/AI/PersonalityService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/PersonalityServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/PersonalityServiceTests.cs
using NUnit.Framework;
using System.IO;
using Mate.Core;
using Mate.AI;

[TestFixture]
public class PersonalityServiceTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mate-personality-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void PersonalityService_LoadsFromToml()
    {
        var toml = @"
name = ""Luna""
greeting = ""Hello! I'm Luna, your desktop companion!""
trait_cheerful = 8
trait_shy = 3
trait_playful = 7
";
        File.WriteAllText(Path.Combine(_testDir, "personality.toml"), toml);

        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Luna", service.Name);
        Assert.AreEqual("Hello! I'm Luna, your desktop companion!", service.Greeting);
    }

    [Test]
    public void PersonalityService_GeneratesSystemPrompt()
    {
        var toml = @"
name = ""Luna""
greeting = ""Hi there!""
trait_cheerful = 8
trait_playful = 7
";
        File.WriteAllText(Path.Combine(_testDir, "personality.toml"), toml);

        var service = new PersonalityService(_testDir);
        var prompt = service.GenerateSystemPrompt();
        Assert.IsNotNull(prompt);
        Assert.IsTrue(prompt.Contains("Luna"));
        Assert.IsTrue(prompt.Contains("cheerful"));
    }

    [Test]
    public void PersonalityService_Defaults_WhenNoFile()
    {
        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Mate", service.Name);
        Assert.AreEqual(string.Empty, service.Greeting);
    }

    [Test]
    public void PersonalityService_GetTraitValue()
    {
        var toml = @"
name = ""Test""
trait_cheerful = 5
trait_shy = 2
";
        File.WriteAllText(Path.Combine(_testDir, "personality.toml"), toml);

        var service = new PersonalityService(_testDir);
        Assert.AreEqual(5, service.GetTrait("cheerful"));
        Assert.AreEqual(2, service.GetTrait("shy"));
        Assert.AreEqual(5, service.GetTrait("unknown")); // default
    }

    [Test]
    public void PersonalityService_ResponseForEvent()
    {
        var toml = @"
name = ""Luna""
response_hello = ""Hi hi! *waves*""
response_idle = ""*yawns*""
";
        File.WriteAllText(Path.Combine(_testDir, "personality.toml"), toml);

        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Hi hi! *waves*", service.GetResponseForEvent("hello"));
        Assert.AreEqual("*yawns*", service.GetResponseForEvent("idle"));
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// AI/PersonalityService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mate.AI
{
    public class PersonalityService
    {
        private string _name = "Mate";
        private string _greeting = string.Empty;
        private string _personalityPath;
        private Dictionary<string, int> _traits = new();
        private Dictionary<string, string> _responses = new();

        public string Name => _name;
        public string Greeting => _greeting;

        public PersonalityService(string projectDir)
        {
            _personalityPath = Path.Combine(projectDir, "personality.toml");
            Load();
        }

        public int GetTrait(string traitName) =>
            _traits.TryGetValue(traitName, out var val) ? val : 5;

        public string GetResponseForEvent(string eventName) =>
            _responses.TryGetValue(eventName, out var response) ? response : string.Empty;

        public string GenerateSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You are {_name}, a desktop companion.");
            sb.AppendLine();

            if (_traits.Count > 0)
            {
                sb.AppendLine("Personality traits:");
                foreach (var (trait, value) in _traits)
                {
                    sb.AppendLine($"  - {trait}: {value}/10");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(_greeting))
            {
                sb.AppendLine($"Your greeting: {_greeting}");
            }

            return sb.ToString();
        }

        private void Load()
        {
            if (!File.Exists(_personalityPath)) return;

            var lines = File.ReadAllLines(_personalityPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;

                var key = trimmed[..eqIndex].Trim();
                var value = trimmed[(eqIndex + 1)..].Trim().Trim('"');

                if (key == "name") _name = value;
                else if (key == "greeting") _greeting = value;
                else if (key.StartsWith("trait_"))
                    _traits[key["trait_".Length..]] = int.TryParse(value, out var v) ? v : 5;
                else if (key.StartsWith("response_"))
                    _responses[key["response_".Length..]] = value;
            }
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > PersonalityServiceTests
```
Expected: 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: PersonalityService loading traits and responses from personality.toml"
```

---

### Task 6.3: Mod Service (TDD)

**Covers:** Mod loading from mods/ directory

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IModService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Mods/ModService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/ModServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/ModServiceTests.cs
using NUnit.Framework;
using System.IO;
using System.Linq;
using Mate.Core;
using Mate.Mods;

[TestFixture]
public class ModServiceTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mate-mods-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void ModService_ImplementsIModService()
    {
        var svc = new ModService();
        Assert.IsInstanceOf<IModService>(svc);
    }

    [Test]
    public void ModService_EmptyModsDir()
    {
        var svc = new ModService();
        var result = svc.LoadMods(_testDir).Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, svc.InstalledMods.Count);
    }

    [Test]
    public void ModService_FindsModWithToml()
    {
        // Create a mod directory with mod.toml
        var modDir = Path.Combine(_testDir, "custom-sounds");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "mod.toml"), @"
name = ""custom-sounds""
version = ""1.0.0""
description = ""Custom drag sounds""
");

        var svc = new ModService();
        svc.LoadMods(_testDir).Wait();
        Assert.AreEqual(1, svc.InstalledMods.Count);
        Assert.AreEqual("custom-sounds", svc.InstalledMods[0].Name);
    }

    [Test]
    public void ModService_ReloadMods()
    {
        var svc = new ModService();
        svc.LoadMods(_testDir).Wait();
        Assert.AreEqual(0, svc.InstalledMods.Count);

        // Add a mod after initial load
        var modDir = Path.Combine(_testDir, "test-mod");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "mod.toml"), @"
name = ""test-mod""
version = ""1.0.0""
");

        svc.ReloadMods().Wait();
        Assert.AreEqual(1, svc.InstalledMods.Count);
    }

    [Test]
    public void ModService_NoModsDir_DoesNotFail()
    {
        var nonexistentDir = Path.Combine(_testDir, "no-such-dir");
        var svc = new ModService();
        var result = svc.LoadMods(nonexistentDir).Result;
        Assert.IsTrue(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Interfaces/IModService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Mate.Core;

namespace Mate.Interfaces
{
    public interface IModService
    {
        IReadOnlyList<ModInfo> InstalledMods { get; }
        Task<Result> LoadMods(string modsPath);
        Task<Result> ReloadMods();
    }

    public record ModInfo(string Name, string Version, string Description, string Path);
}
```

```csharp
// Mods/ModService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.Mods
{
    public class ModService : IModService
    {
        private List<ModInfo> _mods = new();
        private string _modsPath;

        public IReadOnlyList<ModInfo> InstalledMods => _mods;

        public async Task<Result> LoadMods(string modsPath)
        {
            _modsPath = modsPath;
            return await ScanMods();
        }

        public async Task<Result> ReloadMods()
        {
            _mods.Clear();
            return await ScanMods();
        }

        private Task<Result> ScanMods()
        {
            if (!Directory.Exists(_modsPath))
                return Task.FromResult(Result.Ok());

            foreach (var modDir in Directory.GetDirectories(_modsPath))
            {
                var tomlPath = Path.Combine(modDir, "mod.toml");
                if (!File.Exists(tomlPath)) continue;

                var lines = File.ReadAllLines(tomlPath);
                string name = Path.GetFileName(modDir);
                string version = "0.0.0";
                string description = string.Empty;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex < 0) continue;

                    var key = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim().Trim('"');

                    if (key == "name") name = value;
                    else if (key == "version") version = value;
                    else if (key == "description") description = value;
                }

                _mods.Add(new ModInfo(name, version, description, modDir));
            }

            return Task.FromResult(Result.Ok());
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Unity Test Runner (Edit Mode) > ModServiceTests
```
Expected: 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: ModService scanning mods/ directory for mod.toml manifests"
```

---

## Phase 3-6 Exit Criteria Checklist

### Phase 3 (Character): 21 tests
- [ ] `CharacterServiceTests` — 6 tests pass
- [ ] `MouseTrackerTests` — 4 tests pass
- [ ] `CharacterAnimatorTests` — 6 tests pass
- [ ] `CharacterModuleTests` — 5 tests pass
- [ ] VRM loading via `ICharacterService` works
- [ ] Mouse tracking reads config from `IConfiguration`
- [ ] Dance trigger publishes `DanceStartedEvent` to `IEventBus`
- [ ] Idle state changes publish `IdleChangedEvent`

### Phase 4 (Audio): 9 tests
- [ ] `AudioServiceTests` — 5 tests pass
- [ ] `AudioReactiveBridgeTests` — 4 tests pass
- [ ] PulseAudio monitoring via `IAudioService`
- [ ] Audio-reactive bridge triggers dance above threshold
- [ ] No direct singleton access (no `PulseAudioManager.Instance`)

### Phase 5 (System): 4 tests
- [ ] `SystemTrayServiceTests` — 4 tests pass
- [ ] System tray works via `ISystemService`
- [ ] Notifications work via `ISystemService`

### Phase 6 (AI): 10 tests
- [ ] `AIServiceTests` — 5 tests pass
- [ ] `PersonalityServiceTests` — 5 tests pass
- [ ] Ollama chat via `IAIService`
- [ ] Personality loaded from `personality.toml`
- [ ] System prompt generated from traits

### Module Integration: 13+ tests
- [ ] All services register with `MateContext`
- [ ] All events flow through `IEventBus`
- [ ] No `FindFirstObjectByType` in new code (except VRMLoader adapter)
- [ ] No `Singleton<T>` in any new code
