# Target Architecture: Mate Framework

## Architecture Principles

1. **Unity as runtime, not framework** — Developers never touch Unity Editor
2. **Service-oriented** — Clear service boundaries, no god objects
3. **Platform-abstracted** — One API, multiple backends
4. **Module-based** — Optional features are optional modules
5. **CLI-driven** — Everything managed via `mf` CLI
6. **File-based configuration** — TOML/JSON config, no binary settings

## Target Architecture Diagram

```
Developer Machine
  |
  |-- mf CLI (Rust)
  |     |-- mf new <name>        (create project)
  |     |-- mf dev               (dev mode with hot reload)
  |     |-- mf build             (build project)
  |     |-- mf package           (package for distribution)
  |     |-- mf doctor            (diagnose issues)
  |     |-- mf capabilities      (show platform capabilities)
  |
  |-- Mate Project (developer content)
  |     |-- mate.toml            (project manifest)
  |     |-- src/                 (developer scripts, if any)
  |     |-- assets/              (models, animations, sounds)
  |     |-- config/              (AI prompts, personality, etc.)
  |     |-- plugins/             (optional extensions)
  |
  |-- Mate Runtime (prebuilt Unity player)
  |     |-- Mate.Core            (lifecycle, config, logging)
  |     |-- Mate.Window          (window management)
  |     |-- Mate.Character       (VRM, avatar system)
  |     |-- Mate.Audio           (audio monitoring)
  |     |-- Mate.Animation       (animation system)
  |     |-- Mate.AI              (LLM integration)
  |     |-- Mate.System          (tray, notifications)
  |     |-- Mate.Discord         (Discord RPC)
  |     |-- Mate.Mods            (mod loading)
  |     |-- Mate.Platform.LinuxX11   (X11 backend)
  |     |-- Mate.Platform.LinuxHyprland (Hyprland backend)
  |     |-- Mate.Platform.LinuxKWin (KWin backend)
  |
  |-- Platform Backend Layer
        |-- X11 P/Invoke (existing)
        |-- Hyprland Unix sockets (existing)
        |-- KWin DBus (existing)
```

## Service Architecture

### Core Services

```csharp
// Application lifecycle and context
public interface IMateContext
{
    IWindowService Window { get; }
    ICharacterService Character { get; }
    IAudioService Audio { get; }
    IAnimationService Animation { get; }
    IAIService AI { get; }
    ISystemService System { get; }
    IConfiguration Configuration { get; }
    ILogger Logger { get; }
    IEventBus Events { get; }
}

// Window management
public interface IWindowService
{
    WindowInfo GetWindowInfo();
    void SetPosition(Vector2Int position);
    Vector2Int GetPosition();
    void SetSize(Vector2Int size);
    Vector2Int GetSize();
    void SetAlwaysOnTop(bool value);
    void SetBorderless(bool value);
    void SetClickThrough(bool value);
    void HideFromTaskbar(bool value);
    MonitorInfo[] GetMonitors();
    MonitorInfo GetMonitorFromWindow();
    Vector2Int GetMousePosition();
    bool IsTransparentSupported();
    bool IsClickThroughSupported();
    PlatformCapability GetCapabilities();
}

// Character management
public interface ICharacterService
{
    GameObject LoadModel(string path);
    void UnloadModel();
    GameObject GetCurrentModel();
    void SetAnimationParameter(string name, float value);
    void SetAnimationParameter(string name, bool value);
    event Action<GameObject> OnModelLoaded;
    event Action OnModelUnloaded;
}

// Audio monitoring
public interface IAudioService
{
    AudioProgram[] GetPlayingPrograms();
    float GetPeakLevel(uint nodeId);
    void MonitorStream(uint nodeId);
    bool IsAudioReactive { get; }
}

// Animation
public interface IAnimationService
{
    void SetIdle(bool value);
    void SetDancing(bool value);
    void SetDragging(bool value);
    void SetDanceIndex(int index);
    void SetIdleIndex(int index);
    void SetParameter(string name, float value);
    event Action<string, float> OnParameterChanged;
}

// AI/LLM
public interface IAIService
{
    Task<string> SendMessage(string message);
    void SetPrompt(string prompt);
    string GetPrompt();
    bool IsEnabled { get; }
    event Action<string> OnMessageReceived;
}

// System integration
public interface ISystemService
{
    void SetTrayIcon(Texture2D icon, string tooltip);
    void SetTrayMenu(TrayMenuItem[] items);
    void ShowNotification(string title, string body, int timeoutMs = 5000);
    void AddToStartup(bool enable);
}

// Configuration
public interface IConfiguration
{
    T Get<T>(string key, T defaultValue = default);
    void Set<T>(string key, T value);
    void Save();
    void Load();
    event Action<string> OnChanged;
}

// Event bus
public interface IEventBus
{
    void Publish<T>(T eventData);
    void Subscribe<T>(Action<T> handler);
    void Unsubscribe<T>(Action<T> handler);
}
```

## Configuration Model

### Project Manifest (mate.toml)
```toml
[project]
name = "my-mate"
version = "0.1.0"
runtime = "1.0.0"

[character]
model = "assets/avatar.vrm"
scale = 1.0

[audio]
enabled = true
threshold = 0.2
allowed_apps = ["firefox", "spotify"]

[ai]
enabled = true
model = "phi3:mini"
context_length = 4096

[window]
transparent = true
always_on_top = true
click_through = false
hide_from_taskbar = false

[animation]
idle_count = 10
dance_count = 5
idle_switch_time = 10.0
dance_switch_time = 15.0

[discord]
enabled = true
app_id = ""

[system]
tray_icon = "assets/icon.png"
notifications = true
```

### Runtime Settings (settings.json, per-instance)
```json
{
  "window_size_state": "Normal",
  "avatar_size": 1.0,
  "fps_limit": 90,
  "ui_hue_shift": 0.0,
  "ui_saturation": 1.0,
  "selected_particle_theme": "Standard"
}
```

## Runtime Distribution Model

```
mf CLI (Rust binary, ~5MB)
  |
  |-- Downloads runtime on first use
  |     |-- MateRuntime-linux-x64-1.0.0.tar.gz (~100MB)
  |     |-- Contains: Unity player + Mate assemblies + native libs
  |
  |-- Creates project
  |     |-- mate.toml (manifest)
  |     |-- assets/ (user content)
  |     |-- config/ (AI config)
  |
  |-- Dev mode
  |     |-- Starts Unity player with project path
  |     |-- Watches for file changes
  |     |-- Reloads config on change
  |     |-- (Hot reload of C# not feasible in v1)
  |
  |-- Build mode
  |     |-- Compiles project into AssetBundle
  |     |-- Packages with runtime
  |     |-- Creates standalone executable
```

## Developer Workflow

### v1.0 (MVP)
```bash
# Install
curl -fsSL https://get.mateframework.dev | sh

# Create project
mf new my-mate
cd my-mate

# Add features
mf add character --model path/to/avatar.vrm
mf add audio --threshold 0.2
mf add ai --model phi3:mini

# Develop
mf dev

# Build
mf build --output ./build
```

### v2.0 (Extended)
```bash
# Plugin system
mf add plugin custom-behavior
mf remove plugin custom-behavior

# Multi-platform
mf build --platform linux-x64
mf build --platform windows-x64

# Package
mf package --name "My Mate" --version 1.0.0
```
