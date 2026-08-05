# Current Architecture: Mate-Engine-Linux-Port

## High-Level Architecture

The current application is a monolithic Unity project with the following layers:

### Layer 1: Native Platform Layer (P/Invoke)
- X11/XLib bindings in WindowManager.cs (~70 DllImports)
- PulseAudio bindings in PulseAudioManager.cs (~18 DllImports)
- AppIndicator bindings in TrayIndicator.cs (6 DllImports)
- GDK/X11 bridge in GtkX11Helper.cs (2 DllImports)
- libc memory management in MemoryTrim.cs (2 DllImports)

### Layer 2: Platform Integration
- WindowManager.cs (2618 lines) — Central hub, X11 window management
- HyprlandManager.cs — Hyprland Wayland compositor via Unix sockets
- KWinManager.cs — KDE KWin via DBus
- UniWinCore.cs / UniWindowController.cs — Cross-platform window abstraction (originally from UniWinC)

### Layer 3: Core Systems
- SaveLoadHandler.cs — Settings persistence (JSON via Newtonsoft.Json)
- EarlyEnvSet.cs — Pre-scene-load initialization
- DBusNotificationHelper.cs — Desktop notifications
- TrayIndicator.cs — System tray
- PulseAudioManager.cs — Audio monitoring

### Layer 4: Character System
- VRMLoader.cs — VRM 0.x + 1.0 model loading
- AvatarAnimatorController.cs — Animation state machine
- AvatarMouseTracking.cs — IK tracking (head, spine, eye)
- AvatarBigScreenHandler.cs — Window sitting
- AvatarWindowHandler.cs — Window position tracking
- 20+ additional AvatarHandler MonoBehaviours

### Layer 5: Features
- DiscordPresence.cs — Discord RPC
- AISystemPromptBinder.cs — AI prompt management
- LLMUnity/ — Local LLM integration
- ollama-unity/ — Ollama API integration
- MEModLoader.cs — Mod loading system
- AvatarDancePlayer.cs — Custom dance system
- PetVoiceReactionHandler.cs — Voice reactions

### Layer 6: UI / Settings
- SettingsHandler* classes (10+ files) — UI bindings
- AvatarSettingsMenu.cs — Settings menu
- ThemeManager.cs — UI theming
- TutorialMenu.cs — Onboarding

## Dependency Graph: Core Singletons

```
SaveLoadHandler.Instance (god object)
    reads/written by:
    - WindowManager
    - HyprlandManager
    - VRMLoader
    - AvatarAnimatorController (via ApplyAllSettingsToAllAvatars)
    - AvatarBigScreenHandler
    - AvatarMouseTracking (indirectly)
    - DiscordPresence
    - LinuxSpecificSettings
    - All SettingsHandler* classes (10+)
    - AvatarSettingsMenu
    - AvatarTaskbarController
    - AvatarFoodController
    - AvatarGravityController
    - AvatarSwayController
    - AvatarHideHandler
    - AvatarRandomMessages
    - AvatarRebindHandler
    - AvatarSleepController
    - AvatarDanceShapeConverter
    - AvatarParticleHandler
    - AvatarBubbleHandler
    - AvatarBigScreenToggleHandler
    - AvatarBigScreenTimer
    - AvatarBigScreenTouchHandler
    - FPSLimiter
    - MonitorHelper
    - SwingController
    - TutorialMenu
    - ThemeManager
    - MEModHandler
    - AllowedAppsManager
    - KeyBindHandler
    - LanguageDropdownHandler
    - RuntimeModelStats
    - StartWithX11 handling

WindowManager.Instance (second god object)
    reads/written by:
    - AvatarMouseTracking (GetMousePosition, GetWindowPosition)
    - AvatarWindowHandler (GetWindowPosition, SetWindowPosition)
    - AvatarBigScreenHandler (GetWindowPosition, GetMousePosition)
    - AvatarGravityController
    - AvatarSwayController
    - AvatarHideHandler
    - UniWindowController (Linux redirects)
    - EarlyEnvSet
    - GtkX11Helper
    - DesktopAmbientProbe
    - MoveToPrimaryScreen
    - LinuxSpecificSettings
    - AvatarDanceSafetyZone

PulseAudioManager.Instance
    reads by:
    - AvatarAnimatorController (GetPlayingAudioPrograms, ProgramPeaks, StartMonitoringStream)

DiscordPresence.Instance
    reads by:
    - (standalone, reads SaveLoadHandler)

MEModLoader.Instance
    reads by:
    - VRMLoader (AssignHandlersForCurrentAvatar)

Singleton<HyprlandManager>.Instance
    reads by:
    - WindowManager (delegates to it)

Singleton<DBusNotificationHelper>.Instance
    reads by:
    - WindowManager (sends Wayland warning)

TrayIndicator.Instance
    reads by:
    - (standalone, called from scene setup)
```

## Key Architectural Observations

### 1. The SaveLoadHandler Problem
SaveLoadHandler is a **God Object**. Its `data` field (SettingsData) contains 50+ fields covering:
- Animation parameters (soundThreshold, idleSwitchTime, danceSwitchTime)
- Visual settings (bloom, ambientOcclusion, dayNight)
- Audio settings (petVolume, effectsVolume, menuVolume)
- IK settings (headBlend, eyeBlend, spineBlend)
- UI settings (uiHueShift, uiSaturation)
- Feature flags (enableDiscordRPC, enableDancing, enableMouseTracking)
- AI settings (selectedModelPath, contextLength, ollamaModel)
- Platform settings (startWithX11, useKWinApi, allowHyprlandMonitorSitting)
- Mod states (Dictionary of mod enable/disable)
- Accessory states (Dictionary of accessory enable/disable)
- Light settings (Dictionary of intensities, saturations, hues)
- Alarm/Timer data (lists of entries)
- Window state (windowSizeState, windowType)

This single class is accessed by 30+ MonoBehaviours. Any framework must decompose this.

### 2. The MonoBehaviour Soup Problem
There are 30+ avatar handler MonoBehaviours, each loaded via FindFirstObjectByType or direct reference. They communicate through:
- Direct singleton access (SaveLoadHandler.Instance.data.xxx)
- Animator parameters (SetBool, SetFloat)
- Static method calls (MenuActions.IsMovementBlocked)
- Direct component references

### 3. The WindowManager Monolith
WindowManager.cs is 2618 lines and handles:
- X11 initialization and display management
- Window finding by PID
- Window positioning, resizing, borderless mode
- Mouse cursor tracking
- Click-through transparency
- Monitor enumeration
- Window stacking order
- XRandR multi-monitor
- XDamage tracking
- SHM image capture
- Drag-and-drop window movement
- Pointer event handling
- Compositor-specific window types

### 4. Platform Detection Pattern
The current detection flow is:
1. EarlyEnvSet runs before scene load (sets environment variables)
2. WindowManager.OnEnable checks XDG_CURRENT_DESKTOP
3. Instantiates appropriate IWindowManagerImplementation
4. Falls back to raw X11 if no compositor detected

### 5. The VRM Loading Pipeline
VRMLoader supports three model formats:
1. .vrm files (VRM 0.x and 1.0 via UniVRM)
2. .me files (Unity AssetBundles)
3. .prefab references (DLC/built-in models)
It injects components from a template prefab into loaded models.

## File Counts by Subsystem

| Subsystem | Files | Lines (est.) |
|-----------|-------|-------------|
| APIs (Window, PulseAudio, DBus, Tray) | ~15 | ~4,500 |
| Avatar Handlers | ~35 | ~5,000 |
| Settings | ~35 | ~4,000 |
| Tools/Utilities | ~25 | ~2,000 |
| VRM Loading | ~5 | ~1,500 |
| Mod SDK | ~5 | ~500 |
| System Tray | ~10 | ~1,500 |
| Editor Scripts | ~18 | ~3,000 |
| Third-party (LLMUnity, ollama, DiscordRPC, UniVRM, etc.) | ~1200+ | ~100,000+ |

## Native Library Dependencies

| Library | Used By | Purpose |
|---------|---------|---------|
| libX11.so.6 | WindowManager, EarlyEnvSet | X11 window management |
| libXext.so.6 | WindowManager | SHM, shape extensions |
| libXrender.so.1 | EarlyEnvSet | Visual format detection |
| libXdamage.so.1 | WindowManager | Damage tracking |
| libXrandr.so.2 | WindowManager | Multi-monitor |
| libXcomposite.so.1 | WindowManager | Compositing |
| libXcursor.so.1 | WindowManager | Cursor management |
| libpulse.so.0 | PulseAudioManager | Audio monitoring |
| libayatana-appindicator3.so.1 | TrayIndicator | System tray |
| libgdk-3.so.0 | GtkX11Helper | GTK/X11 bridge |
| libc.so.6 | EarlyEnvSet, MemoryTrim | Environment, memory |
| libdl.so | LLMUnity | Dynamic loading |
| libusearch_c.so | LLMUnity/RAG | Vector search |
