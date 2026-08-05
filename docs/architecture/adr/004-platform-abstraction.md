# ADR-004: Platform Abstraction Design

## Status
Accepted

## Context
The current `IWindowManagerImplementation` interface is a good start but is too narrow (window operations only) and lacks capability queries, error handling, and async support.

## Decision
Expand the platform abstraction into a layered design:

### Layer 1: Platform Capabilities
```csharp
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
```

### Layer 2: Window Service
```csharp
public interface IWindowService
{
    Task<WindowInfo> GetWindowInfo(IntPtr handle);
    Task SetPosition(Vector2Int position);
    Task<Vector2Int> GetPosition();
    Task SetSize(Vector2Int size);
    Task<Vector2Int> GetSize();
    Task SetAlwaysOnTop(bool value);
    Task SetBorderless(bool value);
    Task SetClickThrough(bool value);
    Task HideFromTaskbar(bool value);
    Task SetWindowType(WindowType type);
    Task<IntPtr> FindWindowByPid(int pid);
    Task<IntPtr[]> GetAllVisibleWindows();
    Task<MonitorInfo[]> GetAllMonitors();
    Task<MonitorInfo> GetMonitorFromWindow();
    Task<Vector2Int> GetMousePosition();
    Task<bool> IsVisible(IntPtr handle);
    Task<bool> IsFullscreen(IntPtr handle);
    Task<bool> IsMaximized(IntPtr handle);
}
```

### Layer 3: Platform Backend
```csharp
public interface IPlatformBackend : IPlatformCapabilities, IWindowService, IDisposable
{
    Task Initialize(IntPtr unityWindow);
    string DetectDesktopEnvironment();
    string DetectSessionType();
}
```

### Implementations
- `LinuxX11Backend` — Wraps existing WindowManager.cs X11 code
- `LinuxHyprlandBackend` — Wraps existing HyprlandManager
- `LinuxKWinBackend` — Wraps existing KWinManager

## Rationale
1. **Capability queries** — Code can check what's supported before calling
2. **Async support** — KWin DBus calls are async; interface should be too
3. **Typed results** — WindowInfo/MonitorInfo structs instead of raw IntPtrs
4. **Error handling** — Task<T> allows proper error propagation
5. **Testability** — Can mock backends for unit testing

## Consequences
- Existing IWindowManagerImplementation is migrated to IWindowService
- All callers updated to use async patterns
- Backends wrapped in adapter pattern over existing code
- Capability detection enables graceful degradation
