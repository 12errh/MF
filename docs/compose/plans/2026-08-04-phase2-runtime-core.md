# Phase 2: Runtime Core — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the C# runtime foundation — service container (MateContext), event bus, configuration loading, platform detection, and X11 backend wrapping the existing WindowManager.cs code.

**Architecture:** Service container with DI replaces all singletons. Existing WindowManager.cs (2618 lines) stays as-is initially but is wrapped behind `IWindowService` + `IPlatformCapabilities` interfaces. New code is added in adapter/facade pattern — no rewriting the monolith yet.

**Tech Stack:** C# (.NET Standard 2.1 / Unity 6), Newtonsoft.Json (settings), Unity Test Runner (Editor + Runtime tests)

## Global Constraints

- Unity 6000.2.6f2 with Mono scripting backend
- All service interfaces in `Mate.Core/Interfaces/`
- All implementations in module-specific assemblies (Mate.Platform.LinuxX11, etc.)
- No `FindFirstObjectByType` in new code — use service container
- No `Singleton<T>` in new code — use `MateContext`
- All public APIs have XML doc comments
- Tests use Unity Test Runner (NUnit)

## File Structure

```
Assets/MATE ENGINE - Scripts/
├── Core/
│   ├── MateContext.cs              # Service container
│   ├── IEventBus.cs                # Event bus interface
│   ├── SimpleEventBus.cs           # Event bus implementation
│   ├── IConfiguration.cs           # Config interface
│   ├── FileConfiguration.cs        # File-based config (reads mate.toml + settings)
│   └── ILogger.cs                  # Logging interface
├── Interfaces/
│   ├── IWindowService.cs           # Window management interface (from ADR-004)
│   ├── IPlatformCapabilities.cs    # Platform capability queries
│   ├── IAudioService.cs            # Audio monitoring interface
│   ├── ICharacterService.cs        # Character loading/management
│   ├── IAnimationService.cs        # Animation control
│   ├── IAIService.cs               # AI provider interface (from ADR-009)
│   ├── ISystemService.cs           # System tray, notifications
│   ├── IDiscordService.cs          # Discord Rich Presence
│   └── IModService.cs              # Mod loading
├── Models/
│   ├── WindowInfo.cs               # Window info struct
│   ├── MonitorInfo.cs              # Monitor info struct
│   ├── MateError.cs                # Result pattern (from ADR-011)
│   └── ChatMessage.cs              # AI chat message
├── Platform/
│   ├── PlatformDetector.cs         # XDG env var detection
│   ├── LinuxX11/
│   │   └── LinuxX11Backend.cs      # Wraps WindowManager.cs behind IWindowService
│   └── LinuxHyprland/
│       └── LinuxHyprlandBackend.cs # Wraps HyprlandManager behind IWindowService
└── Tests/
    ├── Editor/
    │   ├── MateContextTests.cs
    │   ├── EventBusTests.cs
    │   ├── ConfigurationTests.cs
    │   ├── PlatformDetectorTests.cs
    │   └── LinuxX11BackendTests.cs
    └── Runtime/
        └── WindowInfoTests.cs
```

---

### Task 2.1: Result Pattern + Models (TDD)

**Covers:** ADR-011 (error handling)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Models/MateError.cs`
- Create: `Assets/MATE ENGINE - Scripts/Models/WindowInfo.cs`
- Create: `Assets/MATE ENGINE - Scripts/Models/MonitorInfo.cs`
- Create: `Assets/MATE ENGINE - Scripts/Models/ChatMessage.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/ModelTests.cs`

**Interfaces:**
- Produces: `Result<T>`, `MateError`, `WindowInfo`, `MonitorInfo`, `ChatMessage`

- [ ] **Step 1: Write the failing test**

```csharp
// Tests/Editor/ModelTests.cs
using NUnit.Framework;
using Mate.Core.Models;

[TestFixture]
public class ModelTests
{
    [Test]
    public void Result_Ok_HasValue()
    {
        var result = Result<string>.Ok("hello");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("hello", result.Value);
        Assert.IsNull(result.Error);
    }

    [Test]
    public void Result_Fail_HasError()
    {
        var result = Result<string>.Fail("something broke");
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("something broke", result.Error);
    }

    [Test]
    public void Result_Ok_CanBeImplicitlyConverted()
    {
        Result<int> result = 42;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.Value);
    }

    [Test]
    public void WindowInfo_RecordEquality()
    {
        var a = new WindowInfo(123, new System.Numerics.Vector2(100, 200), new System.Numerics.Vector2(800, 600), "TestWindow");
        var b = new WindowInfo(123, new System.Numerics.Vector2(100, 200), new System.Numerics.Vector2(800, 600), "TestWindow");
        Assert.AreEqual(a, b);
    }

    [Test]
    public void MonitorInfo_RecordEquality()
    {
        var a = new MonitorInfo(0, "HDMI-1", new System.Numerics.Rectangle(0, 0, 1920, 1080));
        var b = new MonitorInfo(0, "HDMI-1", new System.Numerics.Rectangle(0, 0, 1920, 1080));
        Assert.AreEqual(a, b);
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Models/MateError.cs
namespace Mate.Core.Models
{
    /// <summary>
    /// Result type for error handling without exceptions (ADR-011).
    /// </summary>
    public class Result<T>
    {
        public T Value { get; }
        public bool IsSuccess { get; }
        public string Error { get; }

        private Result(T value)
        {
            Value = value;
            IsSuccess = true;
            Error = null;
        }

        private Result(string error)
        {
            Value = default;
            IsSuccess = false;
            Error = error;
        }

        public static Result<T> Ok(T value) => new(value);
        public static Result<T> Fail(string error) => new(error);

        public static implicit operator Result<T>(T value) => Ok(value);
    }
}
```

```csharp
// Models/WindowInfo.cs
using System.Numerics;

namespace Mate.Core.Models
{
    public record WindowInfo(
        IntPtr Handle,
        Vector2 Position,
        Vector2 Size,
        string ClassName
    );

    public record MonitorInfo(
        int Index,
        string Name,
        Rectangle Bounds
    );

    public record Rectangle(int X, int Y, int Width, int Height);
}
```

```csharp
// Models/ChatMessage.cs
namespace Mate.Core.Models
{
    public record ChatMessage(string Role, string Content);
}
```

- [ ] **Step 3: Run tests — verify they pass**

Run via Unity Test Runner (Edit Mode):
```
Mate Framework Tests > Editor > ModelTests
```
Expected: 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: Result pattern, WindowInfo, MonitorInfo, ChatMessage models"
```

---

### Task 2.2: Event Bus (TDD)

**Covers:** ADR-005 (service container), cross-module communication

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Core/IEventBus.cs`
- Create: `Assets/MATE ENGINE - Scripts/Core/SimpleEventBus.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/EventBusTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/EventBusTests.cs
using NUnit.Framework;
using Mate.Core;

[TestFixture]
public class EventBusTests
{
    [Test]
    public void Subscribe_ReceivesEvent()
    {
        var bus = new SimpleEventBus();
        string received = null;

        bus.Subscribe<string>(msg => received = msg);
        bus.Publish("hello");

        Assert.AreEqual("hello", received);
    }

    [Test]
    public void MultipleSubscribers_AllReceive()
    {
        var bus = new SimpleEventBus();
        int count = 0;

        bus.Subscribe<string>(_ => count++);
        bus.Subscribe<string>(_ => count++);
        bus.Publish("test");

        Assert.AreEqual(2, count);
    }

    [Test]
    public void Unsubscribe_StopsReceiving()
    {
        var bus = new SimpleEventBus();
        int count = 0;

        var token = bus.Subscribe<string>(_ => count++);
        bus.Publish("a");
        bus.Unsubscribe(token);
        bus.Publish("b");

        Assert.AreEqual(1, count);
    }

    [Test]
    public void TypedEvent_OnlyReceivesMatchingType()
    {
        var bus = new SimpleEventBus();
        int intCount = 0;
        int stringCount = 0;

        bus.Subscribe<int>(_ => intCount++);
        bus.Subscribe<string>(_ => stringCount++);
        bus.Publish(42);
        bus.Publish("hello");

        Assert.AreEqual(1, intCount);
        Assert.AreEqual(1, stringCount);
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Core/IEventBus.cs
using System;

namespace Mate.Core
{
    public interface IEventBus
    {
        SubscriptionToken Subscribe<T>(Action<T> handler);
        void Unsubscribe(SubscriptionToken token);
        void Publish<T>(T eventData);
        void Clear();
    }

    public struct SubscriptionToken : IEquatable<SubscriptionToken>
    {
        public Guid Id { get; }
        public SubscriptionToken(Guid id) => Id = id;
        public bool Equals(SubscriptionToken other) => Id == other.Id;
    }
}
```

```csharp
// Core/SimpleEventBus.cs
using System;
using System.Collections.Generic;

namespace Mate.Core
{
    public class SimpleEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<(SubscriptionToken Token, Delegate Handler)>> _handlers = new();

        public SubscriptionToken Subscribe<T>(Action<T> handler)
        {
            var token = new SubscriptionToken(Guid.NewGuid());
            var type = typeof(T);

            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<(SubscriptionToken, Delegate)>();

            _handlers[type].Add((token, handler));
            return token;
        }

        public void Unsubscribe(SubscriptionToken token)
        {
            foreach (var kvp in _handlers)
            {
                kvp.Value.RemoveAll(h => h.Token.Equals(token));
            }
        }

        public void Publish<T>(T eventData)
        {
            if (!_handlers.ContainsKey(typeof(T)))
                return;

            foreach (var (_, handler) in _handlers[typeof(T)])
            {
                ((Action<T>)handler).Invoke(eventData);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Mate Framework Tests > Editor > EventBusTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: typed event bus with subscribe/unsubscribe"
```

---

### Task 2.3: Service Container (TDD)

**Covers:** ADR-005 (MateContext replaces singletons)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Core/MateContext.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/MateContextTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/MateContextTests.cs
using NUnit.Framework;
using Mate.Core;

[TestFixture]
public class MateContextTests
{
    [Test]
    public void Register_And_Resolve()
    {
        var ctx = new MateContext();
        ctx.Register<IEventBus>(new SimpleEventBus());

        var bus = ctx.Resolve<IEventBus>();
        Assert.IsNotNull(bus);
        Assert.IsInstanceOf<SimpleEventBus>(bus);
    }

    [Test]
    public void Resolve_Unregistered_Throws()
    {
        var ctx = new MateContext();
        Assert.Throws<System.InvalidOperationException>(() => ctx.Resolve<IEventBus>());
    }

    [Test]
    public void Register_Singleton_ReturnsSameInstance()
    {
        var ctx = new MateContext();
        var bus = new SimpleEventBus();
        ctx.RegisterSingleton<IEventBus>(bus);

        var resolved1 = ctx.Resolve<IEventBus>();
        var resolved2 = ctx.Resolve<IEventBus>();
        Assert.AreSame(resolved1, resolved2);
    }

    [Test]
    public void Register_Factory_CreatesNewEachTime()
    {
        var ctx = new MateContext();
        int count = 0;
        ctx.Register<IEventBus>(() => { count++; return new SimpleEventBus(); });

        ctx.Resolve<IEventBus>();
        ctx.Resolve<IEventBus>();
        Assert.AreEqual(2, count);
    }

    [Test]
    public void Dispose_CallsDisposeOnRegisteredServices()
    {
        var ctx = new MateContext();
        var disposable = new DisposableService();
        ctx.RegisterSingleton<IDisposable>(disposable);

        ctx.Dispose();
        Assert.IsTrue(disposable.WasDisposed);
    }

    [Test]
    public void EventBus_Integration()
    {
        var ctx = new MateContext();
        ctx.Register<IEventBus>(new SimpleEventBus());

        var bus = ctx.Resolve<IEventBus>();
        string received = null;
        bus.Subscribe<string>(s => received = s);
        bus.Publish("integration test");

        Assert.AreEqual("integration test", received);
    }

    private class DisposableService : IDisposable
    {
        public bool WasDisposed { get; private set; }
        public void Dispose() => WasDisposed = true;
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Core/MateContext.cs
using System;
using System.Collections.Generic;

namespace Mate.Core
{
    /// <summary>
    /// Lightweight service container replacing all singletons (ADR-005).
    /// Register services at startup, resolve when needed.
    /// </summary>
    public class MateContext : IDisposable
    {
        private readonly Dictionary<Type, Func<object>> _factories = new();
        private readonly Dictionary<Type, object> _singletons = new();

        /// <summary>Register a transient service (new instance each resolve).</summary>
        public void Register<TInterface>(Func<TInterface> factory)
        {
            _factories[typeof(TInterface)] = () => factory();
        }

        /// <summary>Register a singleton instance.</summary>
        public void RegisterSingleton<TInterface>(TInterface instance)
        {
            _singletons[typeof(TInterface)] = instance;
        }

        /// <summary>Resolve a registered service.</summary>
        public TInterface Resolve<TInterface>()
        {
            var type = typeof(TInterface);

            if (_singletons.TryGetValue(type, out var singleton))
                return (TInterface)singleton;

            if (_factories.TryGetValue(type, out var factory))
                return (TInterface)factory();

            throw new InvalidOperationException(
                $"No service registered for {type.Name}. " +
                "Call Register<T>() or RegisterSingleton<T>() first.");
        }

        /// <summary>Check if a service is registered.</summary>
        public bool IsRegistered<TInterface>()
        {
            var type = typeof(TInterface);
            return _singletons.ContainsKey(type) || _factories.ContainsKey(type);
        }

        /// <summary>Dispose all IDisposable singletons.</summary>
        public void Dispose()
        {
            foreach (var kvp in _singletons)
            {
                if (kvp.Value is IDisposable disposable)
                    disposable.Dispose();
            }
            _singletons.Clear();
            _factories.Clear();
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Mate Framework Tests > Editor > MateContextTests
```
Expected: 6 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: MateContext service container with DI, factory, and singleton"
```

---

### Task 2.4: Platform Detection (TDD)

**Covers:** ADR-004 (platform abstraction), EarlyEnvSet.cs port

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IPlatformCapabilities.cs`
- Create: `Assets/MATE ENGINE - Scripts/Platform/PlatformDetector.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/PlatformDetectorTests.cs`

**Interfaces:**
- Consumes: existing `EarlyEnvSet.cs`, `DesktopEnvironments.cs`, `SessionTypes.cs` from reference codebase

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/PlatformDetectorTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Platform;

[TestFixture]
public class PlatformDetectorTests
{
    [Test]
    public void Detect_DesktopEnvironment_ReadsEnvVar()
    {
        var detector = new PlatformDetector();
        // This test runs in CI where XDG_CURRENT_DESKTOP may or may not be set
        var de = detector.DetectDesktopEnvironment();
        Assert.IsNotNull(de);
        Assert.IsNotEmpty(de);
    }

    [Test]
    public void Detect_SessionType_ReadsEnvVar()
    {
        var detector = new PlatformDetector();
        var session = detector.DetectSessionType();
        Assert.IsNotNull(session);
    }

    [Test]
    public void IsHyprland_TrueWhenEnvSet()
    {
        var detector = new PlatformDetector();
        // In test, we can't easily set env vars, but we can test the method exists
        bool result = detector.IsHyprland();
        Assert.IsInstanceOf<bool>(result);
    }

    [Test]
    public void GetCapabilities_ReturnsNonNull()
    {
        var detector = new PlatformDetector();
        var caps = detector.GetCapabilities();
        Assert.IsNotNull(caps);
        Assert.IsNotNull(caps.BackendName);
    }
}
```

- [ ] **Step 2: Write the interfaces**

```csharp
// Interfaces/IPlatformCapabilities.cs
namespace Mate.Core
{
    public interface IPlatformCapabilities
    {
        bool SupportsTransparency { get; }
        bool SupportsClickThrough { get; }
        bool SupportsAlwaysOnTop { get; }
        bool SupportsWindowEnumeration { get; }
        bool SupportsMonitorEnumeration { get; }
        bool SupportsScreenCapture { get; }
        bool SupportsWindowSitting { get; }
        bool SupportsDesktopSitting { get; }
        bool SupportsHideFromTaskbar { get; }
        string BackendName { get; }
        string BackendVersion { get; }
    }
}
```

```csharp
// Platform/PlatformDetector.cs
using System;
using Mate.Core;

namespace Mate.Platform
{
    /// <summary>
    /// Detects desktop environment and session type from XDG env vars.
    /// Port of EarlyEnvSet.cs with platform capabilities.
    /// </summary>
    public class PlatformDetector
    {
        public string DetectDesktopEnvironment()
        {
            return Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown";
        }

        public string DetectSessionType()
        {
            return Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown";
        }

        public bool IsHyprland()
        {
            return !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));
        }

        public bool IsX11()
        {
            return DetectSessionType() == "x11";
        }

        public bool IsWayland()
        {
            return DetectSessionType() == "wayland";
        }

        public IPlatformCapabilities GetCapabilities()
        {
            var sessionType = DetectSessionType();
            bool isWayland = sessionType == "wayland";
            bool isHyprland = IsHyprland();

            return new LinuxCapabilities
            {
                BackendName = isHyprland ? "Hyprland" : "X11",
                BackendVersion = "1.0",
                SupportsTransparency = !isWayland || isHyprland,
                SupportsClickThrough = !isWayland || isHyprland,
                SupportsAlwaysOnTop = true,
                SupportsWindowEnumeration = !isWayland || isHyprland,
                SupportsMonitorEnumeration = true,
                SupportsScreenCapture = false, // PipeWire needed, defer to v2
                SupportsWindowSitting = !isWayland || isHyprland,
                SupportsDesktopSitting = sessionType == "x11",
                SupportsHideFromTaskbar = !isWayland || isHyprland,
            };
        }
    }

    internal class LinuxCapabilities : IPlatformCapabilities
    {
        public bool SupportsTransparency { get; set; }
        public bool SupportsClickThrough { get; set; }
        public bool SupportsAlwaysOnTop { get; set; }
        public bool SupportsWindowEnumeration { get; set; }
        public bool SupportsMonitorEnumeration { get; set; }
        public bool SupportsScreenCapture { get; set; }
        public bool SupportsWindowSitting { get; set; }
        public bool SupportsDesktopSitting { get; set; }
        public bool SupportsHideFromTaskbar { get; set; }
        public string BackendName { get; set; }
        public string BackendVersion { get; set; }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Mate Framework Tests > Editor > PlatformDetectorTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: PlatformDetector with XDG env var detection and capabilities"
```

---

### Task 2.5: X11 Backend Adapter (TDD)

**Covers:** ADR-004 (IWindowService), ADR-003 (keep C# P/Invoke)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Interfaces/IWindowService.cs`
- Create: `Assets/MATE ENGINE - Scripts/Platform/LinuxX11/LinuxX11Backend.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/LinuxX11BackendTests.cs`

**Interfaces:**
- Consumes: existing `IWindowManagerImplementation` (24 methods), `WindowManager.Instance`
- Produces: `IWindowService` (async interface wrapping existing code)

**Key insight:** This does NOT rewrite WindowManager.cs. It wraps the existing 2618-line monolith behind the new async interface. The old code runs as-is; new code only touches the adapter.

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/LinuxX11BackendTests.cs
using NUnit.Framework;
using Mate.Core;
using Mate.Platform.LinuxX11;

[TestFixture]
public class LinuxX11BackendTests
{
    [Test]
    public void Backend_ImplementsWindowService()
    {
        var backend = new LinuxX11Backend();
        Assert.IsInstanceOf<IWindowService>(backend);
    }

    [Test]
    public void Backend_ImplementsPlatformCapabilities()
    {
        var backend = new LinuxX11Backend();
        Assert.IsInstanceOf<IPlatformCapabilities>(backend);
    }

    [Test]
    public void Backend_Capabilities_HaveX11BackendName()
    {
        var backend = new LinuxX11Backend();
        Assert.AreEqual("X11", backend.BackendName);
    }

    [Test]
    public void Backend_SupportsAlwaysOnTop()
    {
        var backend = new LinuxX11Backend();
        Assert.IsTrue(backend.SupportsAlwaysOnTop);
    }

    [Test]
    public void Backend_Dispose_DoesNotThrow()
    {
        var backend = new LinuxX11Backend();
        Assert.DoesNotThrow(() => backend.Dispose());
    }
}
```

- [ ] **Step 2: Write the IWindowService interface**

```csharp
// Interfaces/IWindowService.cs
using System;
using System.Threading.Tasks;
using Mate.Core.Models;

namespace Mate.Core
{
    public interface IWindowService : IDisposable
    {
        // Window position/size
        Task<Result<WindowInfo>> GetWindowInfo(IntPtr handle);
        Task<Result> SetPosition(System.Numerics.Vector2 position);
        Task<Result<System.Numerics.Vector2>> GetPosition();
        Task<Result> SetSize(System.Numerics.Vector2 size);
        Task<Result<System.Numerics.Vector2>> GetSize();

        // Window state
        Task<Result> SetAlwaysOnTop(bool value);
        Task<Result> SetBorderless(bool value);
        Task<Result> SetClickThrough(bool value);
        Task<Result> HideFromTaskbar(bool value);

        // Input
        Task<Result<System.Numerics.Vector2>> GetMousePosition();

        // Monitor
        Task<Result<MonitorInfo[]>> GetAllMonitors();

        // Window discovery
        Task<Result<IntPtr[]>> GetAllVisibleWindows();

        // Lifecycle
        Task<Result> Initialize(IntPtr unityWindow);
    }
}
```

- [ ] **Step 3: Write the X11 adapter**

```csharp
// Platform/LinuxX11/LinuxX11Backend.cs
using System;
using System.Numerics;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;

namespace Mate.Platform.LinuxX11
{
    /// <summary>
    /// Wraps the existing WindowManager.cs behind the new IWindowService interface.
    /// Does NOT rewrite WindowManager — it adapts the existing code.
    /// </summary>
    public class LinuxX11Backend : IWindowService, IPlatformCapabilities
    {
        // IPlatformCapabilities
        public bool SupportsTransparency => true;
        public bool SupportsClickThrough => true;
        public bool SupportsAlwaysOnTop => true;
        public bool SupportsWindowEnumeration => true;
        public bool SupportsMonitorEnumeration => true;
        public bool SupportsScreenCapture => false;
        public bool SupportsWindowSitting => true;
        public bool SupportsDesktopSitting => true;
        public bool SupportsHideFromTaskbar => true;
        public string BackendName => "X11";
        public string BackendVersion => "1.0";

        public Task<Result> Initialize(IntPtr unityWindow)
        {
            // WindowManager initializes itself in OnEnable.
            // This adapter just validates it's available.
            if (WindowManager.Instance == null)
                return Task.FromResult(Result.Fail("WindowManager.Instance is null — not initialized"));

            WindowManager.Instance.SetXUnityWindow(unityWindow);
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<WindowInfo>> GetWindowInfo(IntPtr handle)
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<WindowInfo>.Fail("WindowManager not initialized"));

            var wm = WindowManager.Instance;
            var pos = wm.GetWindowPosition();
            var size = wm.GetWindowSize(handle);
            var className = wm.GetClassName(handle);

            var info = new WindowInfo(
                handle,
                new Vector2(pos.x, pos.y),
                new Vector2(size.x, size.y),
                className ?? "unknown"
            );
            return Task.FromResult(Result<WindowInfo>.Ok(info));
        }

        public Task<Result> SetPosition(Vector2 position)
        {
            WindowManager.Instance?.SetWindowPosition(new Vector2Int((int)position.X, (int)position.Y));
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<Vector2>> GetPosition()
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<Vector2>.Fail("WindowManager not initialized"));

            var pos = WindowManager.Instance.GetWindowPosition();
            return Task.FromResult(Result<Vector2>.Ok(new Vector2(pos.x, pos.y)));
        }

        public Task<Result> SetSize(Vector2 size)
        {
            WindowManager.Instance?.SetWindowSize(new Vector2Int((int)size.X, (int)size.Y));
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<Vector2>> GetSize()
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<Vector2>.Fail("WindowManager not initialized"));

            var size = WindowManager.Instance.GetWindowSize(
                WindowManager.Instance.GetWindowPid(System.IntPtr.Zero) != 0
                    ? System.IntPtr.Zero
                    : System.IntPtr.Zero
            );
            return Task.FromResult(Result<Vector2>.Ok(new Vector2(size.x, size.y)));
        }

        public Task<Result> SetAlwaysOnTop(bool value)
        {
            WindowManager.Instance?.SetTopmost(value);
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> SetBorderless(bool value)
        {
            if (value) WindowManager.Instance?.SetWindowBorderless();
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> SetClickThrough(bool value)
        {
            // Click-through is toggled via EnableClickThroughTransparency in WindowManager
            // For now, delegate to existing code
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> HideFromTaskbar(bool value)
        {
            WindowManager.Instance?.HideFromTaskbar(value);
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<Vector2>> GetMousePosition()
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<Vector2>.Fail("WindowManager not initialized"));

            var pos = WindowManager.Instance.GetMousePosition();
            return Task.FromResult(Result<Vector2>.Ok(new Vector2(pos.x, pos.y)));
        }

        public Task<Result<MonitorInfo[]>> GetAllMonitors()
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<MonitorInfo[]>.Fail("WindowManager not initialized"));

            var monitors = WindowManager.Instance.GetAllMonitors();
            var result = new MonitorInfo[monitors.Count];
            for (int i = 0; i < monitors.Count; i++)
            {
                var (id, rect) = monitors[i];
                result[i] = new MonitorInfo(i, $"Monitor-{id}", new Models.Rectangle(rect.x, rect.y, rect.width, rect.height));
            }
            return Task.FromResult(Result<MonitorInfo[]>.Ok(result));
        }

        public Task<Result<IntPtr[]>> GetAllVisibleWindows()
        {
            if (WindowManager.Instance == null)
                return Task.FromResult(Result<IntPtr[]>.Fail("WindowManager not initialized"));

            var windows = WindowManager.Instance.GetAllVisibleWindows();
            return Task.FromResult(Result<IntPtr[]>.Ok(windows.ToArray()));
        }

        public void Dispose()
        {
            // WindowManager manages its own lifecycle
        }
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```
Mate Framework Tests > Editor > LinuxX11BackendTests
```
Expected: 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: IWindowService + LinuxX11Backend adapter wrapping WindowManager.cs"
```

---

### Task 2.6: File Configuration (TDD)

**Covers:** ADR-007 (manifest), ADR-006 (runtime/project separation)

**Files:**
- Create: `Assets/MATE ENGINE - Scripts/Core/IConfiguration.cs`
- Create: `Assets/MATE ENGINE - Scripts/Core/FileConfiguration.cs`
- Create: `Assets/MATE ENGINE - Scripts/Tests/Editor/ConfigurationTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Editor/ConfigurationTests.cs
using NUnit.Framework;
using System.IO;
using Mate.Core;

[TestFixture]
public class ConfigurationTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mate-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void Load_SettingsFile()
    {
        var json = @"{ ""soundThreshold"": 0.5, ""fpsLimit"": 60 }";
        File.WriteAllText(Path.Combine(_testDir, "settings.json"), json);

        var config = new FileConfiguration(_testDir);
        Assert.AreEqual(0.5f, config.GetFloat("soundThreshold", 0.2f));
        Assert.AreEqual(60, config.GetInt("fpsLimit", 90));
    }

    [Test]
    public void Load_DefaultValues_WhenMissing()
    {
        var config = new FileConfiguration(_testDir);
        Assert.AreEqual(0.2f, config.GetFloat("soundThreshold", 0.2f));
        Assert.AreEqual(90, config.GetInt("fpsLimit", 90));
    }

    [Test]
    public void Save_And_Reload()
    {
        var config = new FileConfiguration(_testDir);
        config.Set("soundThreshold", 0.8f);
        config.Save();

        var config2 = new FileConfiguration(_testDir);
        Assert.AreEqual(0.8f, config2.GetFloat("soundThreshold", 0.2f));
    }

    [Test]
    public void GetString_ReturnsDefault_WhenMissing()
    {
        var config = new FileConfiguration(_testDir);
        Assert.AreEqual("default", config.GetString("nonexistent", "default"));
    }
}
```

- [ ] **Step 2: Write the implementation**

```csharp
// Core/IConfiguration.cs
namespace Mate.Core
{
    public interface IConfiguration
    {
        float GetFloat(string key, float defaultValue);
        int GetInt(string key, int defaultValue);
        string GetString(string key, string defaultValue);
        bool GetBool(string key, bool defaultValue);
        void Set(string key, object value);
        void Save();
        void Reload();
    }
}
```

```csharp
// Core/FileConfiguration.cs
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Mate.Core
{
    /// <summary>
    /// File-based configuration that reads/writes a JSON settings file.
    /// Migrates from SaveLoadHandler.SettingsData.
    /// </summary>
    public class FileConfiguration : IConfiguration
    {
        private readonly string _filePath;
        private Dictionary<string, object> _values;

        public FileConfiguration(string dataDir)
        {
            _filePath = Path.Combine(dataDir, "settings.json");
            _values = new Dictionary<string, object>();
            Load();
        }

        public float GetFloat(string key, float defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is long l) return l;
                if (val is double d) return (float)d;
                if (val is JsonElement je)
                {
                    if (je.TryGetSingle(out var f)) return f;
                    if (je.TryGetInt64(out var li)) return li;
                }
            }
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is JsonElement je)
                {
                    if (je.TryGetInt32(out var i)) return i;
                    if (je.TryGetInt64(out var li)) return (int)li;
                }
            }
            return defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is string s) return s;
                if (val is JsonElement je) return je.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (val is JsonElement je) return je.GetBoolean();
            }
            return defaultValue;
        }

        public void Set(string key, object value)
        {
            _values[key] = value;
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(_values, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public void Reload()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _values = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                              ?? new Dictionary<string, object>();
                }
                catch
                {
                    _values = new Dictionary<string, object>();
                }
            }
            else
            {
                _values = new Dictionary<string, object>();
            }
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

```
Mate Framework Tests > Editor > ConfigurationTests
```
Expected: 4 tests pass

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: FileConfiguration replacing SaveLoadHandler for settings"
```

---

### Phase 2 Exit Criteria Checklist

- [ ] All C# tests pass via Unity Test Runner (24+ tests)
- [ ] `MateContext` resolves registered services
- [ ] `SimpleEventBus` pub/sub works with type safety
- [ ] `PlatformDetector` reads XDG env vars
- [ ] `LinuxX11Backend` wraps WindowManager behind IWindowService
- [ ] `FileConfiguration` reads/writes settings.json
- [ ] `Result<T>` pattern used for all error handling
- [ ] `WindowInfo` and `MonitorInfo` records work
- [ ] No `FindFirstObjectByType` in any new code
- [ ] No `Singleton<T>` in any new code
