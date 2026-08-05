# Technical Requirements Document: Mate Framework

## System Architecture

### High-Level Architecture

```
┌──────────────────────────────────────────────────────┐
│                  mf CLI (Rust)                        │
│  new | dev | build | package | doctor | capabilities │
├──────────────────────────────────────────────────────┤
│              Process Manager (Rust)                    │
│  Launch Unity Player | Watch Files | Reload Config    │
├──────────────────────────────────────────────────────┤
│            Mate Runtime (Unity Player)                │
│  ┌──────────┐  ┌──────────┐  ┌────────────────────┐  │
│  │ MateCore  │  │ Services │  │  Platform Backends  │  │
│  │ Context   │──│ Registry │──│  X11 | Hypr | KWin  │  │
│  │ Config    │  │ Events   │  │                     │  │
│  │ Logger    │  │ Modules  │  │  P/Invoke (existing)│  │
│  └──────────┘  └──────────┘  └────────────────────┘  │
├──────────────────────────────────────────────────────┤
│              Feature Modules (C# DLLs)                │
│  Window | Character | Audio | Animation | AI | System │
│  Discord | Mods                                        │
├──────────────────────────────────────────────────────┤
│              Unity Engine                              │
│  Rendering (URP) | Animation | Physics | UI | Audio    │
└──────────────────────────────────────────────────────┘
```

### Component Specifications

#### 1. mf CLI (Rust)

**Binary size target:** < 10MB
**Dependencies:**
- clap (argument parsing)
- toml + serde (config)
- tokio (async runtime)
- notify (file watching)
- reqwest (HTTP for runtime download)
- indicatif (progress bars)
- colored (terminal output)

**Commands:**
| Command | Description | Implementation |
|---------|-------------|----------------|
| `mf new <name>` | Create project scaffold | Write template files |
| `mf dev` | Start dev mode | Start Unity player + file watcher |
| `mf build` | Build project | Unity batch mode build |
| `mf package` | Package for distribution | Create distributable archive |
| `mf doctor` | Diagnose issues | Check deps, paths, versions |
| `mf capabilities` | Show platform features | Query runtime capabilities |
| `mf add <module>` | Enable feature module | Edit mate.toml |
| `mf remove <module>` | Disable feature module | Edit mate.toml |
| `mf runtime` | Manage runtime versions | Download/update runtime |
| `mf plugins list` | List available plugins | Scan plugins directory |

**Process management:**
- Start Unity player as child process
- Monitor stdout/stderr
- Detect crashes and restart
- Forward signals (SIGTERM → graceful shutdown)

**File watching:**
- Watch `mate.toml` for config changes
- Watch `config/` for AI prompt changes
- Watch `assets/` for asset changes
- Trigger runtime restart on critical changes
- Trigger config reload on non-critical changes

#### 2. Mate Runtime (Unity Player)

**Binary size target:** < 200MB
**Unity version:** 6000.2.6f2 (or latest stable Unity 6)
**Scripting backend:** Mono (for dynamic loading)
**Target:** Linux x64

**Assembly structure:**
```
Mate.Core.dll              (~50KB)
Mate.Window.dll            (~200KB)
Mate.Character.dll         (~300KB)
Mate.Audio.dll             (~100KB)
Mate.Animation.dll         (~150KB)
Mate.AI.dll                (~200KB)
Mate.System.dll            (~100KB)
Mate.Discord.dll           (~50KB)
Mate.Mods.dll              (~50KB)
Mate.Platform.LinuxX11.dll (~500KB)
Mate.Platform.LinuxHyprland.dll (~100KB)
Mate.Platform.LinuxKWin.dll (~100KB)
```

**Startup sequence:**
1. Parse command line arguments (--project-path, --mate-runtime, --dev-mode)
2. Load mate.toml from project path
3. Initialize MateContext with configuration
4. Register platform backend based on detection
5. Initialize required services based on manifest
6. Load character model from project
7. Start main loop

**Config hot-reload:**
- File watcher monitors mate.toml
- On change: parse, validate, apply delta
- No restart needed for most settings
- Character model change triggers model reload

#### 3. Platform Backend (Linux X11)

**Source files to preserve:**
- WindowManager.cs (2618 lines) — decomposed
- EarlyEnvSet.cs (pre-scene initialization)
- GtkX11Helper.cs (GDK/X11 bridge)
- HyprlandManager.cs (Hyprland backend)
- HyprlandDispatcher.cs (Hyprland commands)
- HyprlandEventReader.cs (Hyprland events)
- KWinManager.cs (KWin backend)

**Decomposition plan:**
1. Extract X11 DllImport declarations into LinuxX11Imports.cs
2. Extract X11 window operations into LinuxX11WindowService.cs
3. Extract X11 monitor operations into LinuxX11MonitorService.cs
4. Extract X11 input handling into LinuxX11InputService.cs
5. Keep WindowManager.cs as orchestrator (reduced to ~500 lines)
6. Wrap HyprlandManager in LinuxHyprlandBackend.cs
7. Wrap KWinManager in LinuxKWinBackend.cs

**Native library dependencies (Linux):**
```
libX11.so.6         (required)
libXext.so.6        (required)
libpulse.so.0       (optional, for audio)
libgdk-3.so.0       (optional, for visual detection)
libXrandr.so.2      (optional, for multi-monitor)
libXdamage.so.1     (optional, for damage tracking)
libXcomposite.so.1  (optional, for compositing)
libXcursor.so.1     (optional, for cursor)
libayatana-appindicator3.so.1 (optional, for tray)
```

#### 4. Character Module

**Source files to preserve:**
- VRMLoader.cs (553 lines) — VRM 0.x + 1.0 loading
- AvatarAnimatorController.cs — animation state machine
- AvatarMouseTracking.cs — IK head/spine/eye tracking

**Components to preserve:**
- AnimatorController setup
- Bone IK system
- VRM LookAt integration

**Components to remove from core (make optional):**
- AvatarBigScreenHandler (optional module)
- AvatarFoodController (optional module)
- AvatarGravityController (optional module)
- AvatarSwayController (optional module)
- AvatarHideHandler (optional module)
- AvatarBubbleHandler (optional module)
- AvatarRandomMessages (optional module)
- AvatarSleepController (optional module)
- 20+ other avatar handlers

#### 5. Audio Module

**Source files to preserve:**
- PulseAudioManager.cs — PulseAudio P/Invoke and monitoring

**Dependencies:**
- libpulse.so.0

**Features:**
- Monitor audio peaks per-app
- Detect which apps are playing audio
- Audio-reactive animation triggers

**Migration:**
- Extract PulseAudio P/Invoke into AudioNativeImports.cs
- Extract monitoring logic into PulseAudioService.cs
- Implement IAudioService interface

#### 6. AI Module

**Source files to preserve:**
- AISystemPromptBinder.cs — prompt management
- Ollama integration patterns (HTTP-based)

**Source files to potentially rewrite:**
- LLMUnity integration (tightly coupled to Unity, complex)

**Migration:**
- Create OllamaProvider using HttpClient (simple HTTP API)
- Create LLMUnityProvider wrapping existing integration
- Abstract behind IAIService interface

#### 7. System Module

**Source files to preserve:**
- TrayIndicator.cs — system tray
- DBusNotificationHelper.cs — notifications

**Native library dependencies:**
- libayatana-appindicator3.so.1 (tray)
- DBus session (notifications)

## Data Models

### WindowInfo
```csharp
public record WindowInfo(
    IntPtr Handle,
    string Title,
    int ProcessId,
    Vector2Int Position,
    Vector2Int Size,
    bool IsVisible,
    bool IsFullscreen,
    bool IsMaximized,
    string ClassName
);
```

### MonitorInfo
```csharp
public record MonitorInfo(
    int Id,
    string Name,
    Vector2Int Position,
    Vector2Int Size,
    float Scale,
    bool IsPrimary
);
```

### AudioProgram
```csharp
public record AudioProgram(
    uint NodeId,
    string Name,
    string ApplicationName,
    float PeakLevel,
    bool IsPlaying
);
```

### CharacterConfig
```csharp
public record CharacterConfig(
    string ModelPath,
    float Scale,
    string FallbackModel
);
```

### AnimationConfig
```csharp
public record AnimationConfig(
    int IdleCount,
    int DanceCount,
    float IdleSwitchTime,
    float DanceSwitchTime,
    bool EnableDancing,
    bool EnableDanceSwitch
);
```

## Build System

### mf CLI Build
```bash
# Development
cargo build

# Release
cargo build --release
# Binary: target/release/mf

# Cross-compile
cross build --release --target aarch64-unknown-linux-gnu
```

### Unity Runtime Build
```bash
# Framework team only (not developer-facing)
# Unity batch mode build for creating the prebuilt runtime
unity -batchmode -nographics -projectPath . \
  -executeMethod MateBuild.BuildLinux \
  -buildTarget Linux64 \
  -quit

# Output: build/MateRuntime-linux-x64/
# This is distributed as a release artifact, not built by developers
```

### Project Build
```bash
mf build
# 1. Validate mate.toml (required fields, path existence, value ranges)
# 2. Resolve runtime version (download if not cached)
# 3. Copy prebuilt Unity player + Mate assemblies from runtime cache
# 4. Copy raw project assets (VRM, animations, sounds, textures) as-is
# 5. Copy config files (mate.toml, personality.toml, etc.)
# 6. Write manifest with asset paths and runtime version
# 7. Output: build/<project-name>-linux-x64/ (ready to run)
```

**Note:** `mf build` does NOT require Unity Editor. No AssetBundle compilation.
Raw assets are loaded directly at runtime by the prebuilt runtime.
See ADR-013 for full rationale.
