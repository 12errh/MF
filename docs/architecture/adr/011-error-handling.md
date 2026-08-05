# ADR-011: Error Handling Strategy

## Status
Accepted

## Context
The current codebase has inconsistent error handling — some methods throw, some return null, some silently fail. The framework needs a unified error model.

## Decision
Two-tier error handling: Rust CLI uses Result<T, E>, C# runtime uses exceptions + Result pattern.

### Rust CLI (mf)
```rust
// All commands return Result
fn cmd_new(args: NewArgs) -> anyhow::Result<()> { ... }
fn cmd_dev(args: DevArgs) -> anyhow::Result<()> { ... }

// Errors are human-readable with --json support
#[derive(Debug, thiserror::Error)]
enum MfError {
    #[error("manifest not found at {path}")]
    ManifestNotFound { path: PathBuf },
    
    #[error("invalid manifest: {reason}")]
    ManifestInvalid { reason: String },
    
    #[error("runtime not installed, run `mf runtime install`")]
    RuntimeNotInstalled,
    
    #[error("Unity player crashed with exit code {code}")]
    UnityCrashed { code: i32 },
}
```

### C# Runtime (Unity)
```csharp
// Service methods return Result<T>
public class Result<T>
{
    public T Value { get; }
    public bool IsSuccess { get; }
    public string Error { get; }
    
    public static Result<T> Ok(T value) => new() { Value = value, IsSuccess = true };
    public static Result<T> Fail(string error) => new() { Error = error, IsSuccess = false };
}

// Services return Results, not throw
public async Task<Result<WindowInfo>> GetWindowInfo(IntPtr handle)
{
    // ...
}
```

### Error Propagation
```
Platform error (X11 returns null)
  -> Backend wraps in Result.Fail("X11: window not found")
  -> Service propagates to caller
  -> Runtime logs error
  -> If critical: shows notification
  -> If non-critical: degrades gracefully
```

### Graceful Degradation
- Transparent window fails → fall back to opaque window
- Audio monitoring fails → disable music-reactive features
- AI unavailable → disable chat
- System tray fails → continue without tray
- Mouse tracking fails → disable IK

## Consequences
- All errors are typed and messageable
- No silent failures
- `mf doctor` can diagnose common issues
- Runtime continues operating with reduced features on non-critical failures
