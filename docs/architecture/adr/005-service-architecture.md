# ADR-005: Service Architecture

## Status
Accepted

## Context
The current codebase uses 10+ singletons with inconsistent patterns, a God Object (SaveLoadHandler.Instance.data), and FindFirstObjectByType calls everywhere. Dependencies are hidden and untestable.

## Decision
Introduce a lightweight service container pattern.

## Design

### Service Container
```csharp
public class MateContext : IDisposable
{
    private readonly Dictionary<Type, object> _services = new();
    
    public T Get<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var svc) ? (T)svc : null;
    }
    
    public void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }
    
    public void Dispose()
    {
        foreach (var svc in _services.Values.OfType<IDisposable>())
            svc.Dispose();
        _services.Clear();
    }
}
```

### Service Registration
```csharp
// In startup MonoBehaviour
public class MateBootstrap : MonoBehaviour
{
    void Awake()
    {
        var ctx = new MateContext();
        
        // Core
        ctx.Register<IConfiguration>(new FileConfiguration());
        ctx.Register<ILogger>(new UnityLogger());
        ctx.Register<IEventBus>(new SimpleEventBus());
        
        // Platform
        ctx.Register<IWindowService>(CreateWindowBackend());
        
        // Features
        ctx.Register<IAudioService>(new PulseAudioService());
        ctx.Register<ICharacterService>(new CharacterService());
        ctx.Register<IAnimationService>(new AnimationService());
        ctx.Register<IAIService>(CreateAIService());
        ctx.Register<ISystemService>(new SystemService());
        
        // Store in DontDestroyOnLoad
        DontDestroyOnLoad(gameObject);
    }
}
```

### Singleton Migration
- `SaveLoadHandler` → Split into `IConfiguration` (per-module config) + `SettingsData` removed
- `WindowManager` → `IWindowService` (wraps existing code)
- `PulseAudioManager` → `IAudioService`
- `TrayIndicator` → `ISystemService`
- `DiscordPresence` → `IDiscordService`
- `MEModLoader` → `IModService`

## Rationale
1. **Explicit dependencies** — Services are injected, not discovered
2. **Testable** — Mock any service for unit tests
3. **Replaceable** — Swap PulseAudio for PipeWire by changing registration
4. **Lifecycle-managed** — Container handles initialization and disposal
5. **No god objects** — Configuration split into per-module sections

## Consequences
- All singletons eliminated over 3-5 migration phases
- FindFirstObjectByType calls removed
- SettingsData decomposed into typed config sections
- Each module owns its own configuration
- Event bus replaces direct cross-module calls
