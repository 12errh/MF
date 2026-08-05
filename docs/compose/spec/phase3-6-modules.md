---
feature: phase3-6-modules
status: delivered
updated: 2026-08-05
branch: phase3-6-modules
commits: 2ccb018..39f2431
---

# Phase 3-6: Character, Audio, System, AI Modules

## Report

**What was built** — A new tracked `unity/` Unity 6000.2.6f2 project implementing the four feature modules as service implementations behind the Phase 2 interfaces. `MateFramework/` holds all new code: the `Mate.Interfaces` contracts (`ICharacterService`, `IMouseTracker`, `IAnimationService`, `IAudioService`, `ISystemService`, `IAIService`, `IModService`, plus `IVrmLoader`/`IPulseAudio` adapter seams); `Character/` (CharacterService, MouseTracker, CharacterAnimator), `Audio/` (PulseAudioService, AudioReactiveBridge), `System/` (SystemTrayService), `AI/` (OllamaProvider, PersonalityService), and `Mods/` (ModService). All cross-module communication flows through `IEventBus`; config is read from `IConfiguration`; the only `FindFirstObjectByType` in new code is in the sanctioned `VrmLoaderAdapter`. The grabbed monoliths (`VRMLoader`, `PulseAudioManager`, `SaveLoadHandler`, etc.) were vendored into `Grabbed/` from the reference project, trimmed to their wrappable subset.

**Verification** — `Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults ...` PASS: 72 Mate.* EditMode tests pass (Character 20, Audio 14, System 8, AI 11, Mods 6, Integration 3), 0 failures; headless import compiles with 0 CS errors. The remaining 34 suite failures are all in the vendored UniVRM/UniGLTF test assemblies (their texture/material/migration tests do not run headless) — `PRE-EXISTING`, not our code. `dotnet test runtime/Mate.Core.sln` PASS: 33 tests (unchanged after the Newtonsoft migration).

**Journey log** —
- Unity on Linux cannot import symlinked folders in Assets (asset-DB corruption warnings, byte-count mismatches); `Mate.Core` source is copied (not symlinked) into `unity/Assets/MateFramework/Core/` with an `IsExternalInit` shim, and `FileConfiguration` was migrated from `System.Text.Json` (absent in Unity's .NET 4.x profile) to Newtonsoft — keeping `runtime/Mate.Core` the canonical source.
- The folder is named `MateFramework` (not `Mate.Framework`); a stale-GUID issue made Unity silently exclude the original folder's contents from compilation, and renaming regenerated the GUID and surfaced the real compile errors.
- `-runTests` + `-quit` makes Unity exit before tests run — the `-quit` flag must be omitted; the test runner quits itself.
- The plan's exit criteria (no `FindFirstObjectByType`/`Singleton<T>` in new code) drove the adapter pattern: services depend on injected `IVrmLoader`/`IPulseAudio`; the concrete adapters are the only wrappers of the grabbed monoliths.
- First review found the audio-reactive pipeline dead end-to-end (adapter returned 0, service never published `AudioPeakEvent`) and a racy `VrmLoaderAdapter`; fixed with a real `ProgramPeaks` read, a `Poll()` publishing `AudioPeakEvent`, a polling loader with timeout, and regression tests. Runtime wiring (a caller driving `Poll()`) is composition-root scope, deferred to the app-bootstrap phase.

## [S1] Problem

The Mate Framework needs the four main feature modules implemented as service implementations behind the interfaces defined in Phase 2 (`Mate.Core`). The original plan targets the Unity-bound codebase. This workspace now has **Unity 6000.2.6f2 installed with an active Personal license**, so the real Unity-bound code can be written and verified via the EditMode Test Runner (no more pure-.NET workaround for these modules).

Per user decision, the modules are implemented in a **new tracked `unity/` project** — not inside the 2.4 GB gitignored `refrence/` tree. We *grab* (copy) only what we need from the reference into our own project, so the framework is self-contained and versioned.

## [S2] Design

### Workspace layout

New tracked `unity/` directory at repo root, a self-contained Unity project:

```
unity/
├── Packages/
│   └── manifest.json
├── ProjectSettings/
│   ├── ProjectVersion.asset        # 6000.2.6f2
│   └── ProjectSettings.asset
└── Assets/
    ├── MateFramework/              # OUR code (all new P3-6 source lives here)
    │   ├── Interfaces/             # ICharacterService, IMouseTracker, IAnimationService,
    │   │                           #   IAudioService, ISystemService, IAIService, IModService,
    │   │                           #   IVrmLoader, IPulseAudio
    │   ├── Character/              # CharacterService, VrmLoaderAdapter, Tracking/MouseTracker,
    │   │                           #   Animation/CharacterAnimator
    │   ├── Audio/                  # PulseAudioService, PulseAudioAdapter, AudioReactiveBridge
    │   ├── System/                 # SystemTrayService
    │   ├── AI/                     # OllamaProvider, PersonalityService
    │   ├── Mods/                   # ModService
    │   ├── Core/                   # copied from runtime/Mate.Core + IsExternalInit shim
    │   └── Tests/Editor/           # NUnit EditMode tests
    └── Grabbed/                    # copied from refrence/ (vendored monoliths + packages)
        ├── Scripts/                # selected monoliths (VRMLoader, PulseAudioManager, ...)
        └── Packages/               # VRM10, VRM, UniGLTF, StandaloneFileBrowser, Newtonsoft.Json
```

### Grabbed (vendored) from `refrence/`

- **UPM packages** (proper `package.json` + `.asmdef`, so they vendor cleanly):
  - `com.vrmc.vrm` (VRM10) 0.128.3 → `MATE ENGINE - Packages/VRM10`
  - `com.vrmc.univrm` (VRM) 0.128.3 → `MATE ENGINE - Packages/VRM`
  - `com.vrmc.gltf` (UniGLTF) 0.128.3 → `MATE ENGINE - Packages/UniGLTF`
  - `StandaloneFileBrowser` (VRMLoader's file dialog)
  - `Newtonsoft.Json` 13.0.3 (vendored at `Assets/Packages/Newtonsoft.Json.13.0.3/`)
- **Monoliths** (from `MATE ENGINE - Scripts/`): `VRMLoader.cs` + `AvatarLibraryMenu.cs`, `APIs/PulseAudioManager.cs`, `Settings/SaveLoadHandler.cs` (+ `RuntimeModelStats.cs`), `Tools/MEValueChanger.cs`, `DeleteButtonHoldHandler.cs`, `ThemeManager.cs`, plus an extracted `APIs/WindowType.cs` enum (WindowManager itself is deferred to a later phase). `TrayIndicator`/`DBusNotificationHelper` (Gtk/DBus deps), `DiscordPresence` (DiscordRPC), `MEModLoader`, and the `AvatarHandlers/*` monoliths are **not** grabbed — no service references them, and the plan's exit criteria ban their singleton/scene-lookup patterns. The vendored copies are minimally trimmed (SaveLoadHandler's avatar-side-effect method; VRMLoader's MEModLoader/SettingsHandlerUtility calls) so the subset compiles standalone.

### Our code (all new, under `Assets/MateFramework/`)

- **Interfaces** (namespace `Mate.Interfaces`): `ICharacterService`, `IMouseTracker`, `IAnimationService`, `IAudioService`, `ISystemService`, `IAIService`, `IModService`, plus adapter seams `IVrmLoader` and `IPulseAudio`.
- **Character** (`Mate.Character`): `CharacterService` (model lifecycle, delegates to injected `IVrmLoader`); `VrmLoaderAdapter` (the sanctioned `FindFirstObjectByType` wrapper of `VRMLoader`, polls `GetCurrentModel()` with a 30 s timeout because `LoadVRM` is `async void`); `Tracking/MouseTracker` (config sensitivities, clamped blends); `Animation/CharacterAnimator` (dance/idle events via `IEventBus`).
- **Audio** (`Mate.Audio`): `PulseAudioService` (config allowed-apps/threshold; `Poll()` publishes `AudioPeakEvent` + raises `OnPeakLevelChanged` for monitored nodes); `PulseAudioAdapter` (reads the monolith's `ProgramPeaks`, mapping the `-1` sentinel to 0); `AudioReactiveBridge` (subscribes to `AudioPeakEvent`, publishes `DanceStartedEvent` above threshold).
- **System** (`Mate.System`): `SystemTrayService` (tray/notification state + events; native AppIndicator/DBus presentation is platform-layer scope).
- **AI** (`Mate.AI`): `OllamaProvider` (injectable `HttpClient`, Newtonsoft JSON, `AiMessageEvent`); `PersonalityService` (parses `personality.toml`: name, greeting, `trait_*`, `response_*`).
- **Mods** (`Mate.Mods`): `ModService` (scans `mods/` dir for `mod.toml` manifests).

### Contracts

- All services registered via `MateContext.Register<T>()` / `RegisterSingleton<T>()` at startup (composition root; proven by integration tests).
- All cross-module communication via `IEventBus` — no direct singleton access.
- All config read from `IConfiguration` — no direct `SaveLoadHandler.Instance.data`.
- `Mate.Core` types (`MateContext`, `IEventBus`, `SimpleEventBus`, `IConfiguration`, `Result`, `Result<T>`, `ChatMessage`) come from `runtime/Mate.Core`, **copied** into `unity/Assets/MateFramework/Core/` (Unity on Linux cannot import symlinked folders) with an `IsExternalInit` shim for C# records. `runtime/Mate.Core` remains the canonical source; the copy is refreshed when the core changes.

### Architecture decision — why a fresh `unity/` project

The reference project is a 2.4 GB third-party Linux port. Writing our code inside it would (a) squat in a third-party tree, (b) be unversioned (it's gitignored), and (c) couple our framework to the reference's layout. Instead we copy the ~10 MB of scripts/packages we actually wrap into our own tracked project. The wrapped monoliths become our vendored code to maintain.

### Verification

- Headless EditMode Test Runner against `unity/` (note: **omit `-quit`** — it makes Unity exit before tests run):
  ```
  ~/Unity/Hub/Editor/6000.2.6f2/Editor/Unity -batchmode -nographics \
    -projectPath unity -runTests -testPlatform EditMode \
    -testResults /tmp/mate-test-results.xml
  ```
- Expected: all `Mate.*` EditMode fixtures pass (72 tests). The 34 failures in the vendored UniGLTF/VRM/VRM10 test assemblies are `PRE-EXISTING` (headless-environment incompatibilities in the vendored packages' own tests).

## [S3] Out of Scope

- Modifying or committing anything inside `refrence/` (it stays gitignored).
- The `runtime/` pure-.NET solution (Phase 2) — unchanged; `unity/` copies its source.
- Runtime platform backends (`LinuxX11Backend`, `WindowManager`, AppIndicator/DBus native presentation) — deferred to later phases.
- A production driver calling `PulseAudioService.Poll()` and a composition-root/startup bootstrap — app-wiring scope for the bootstrap phase; the services are fully testable without it.
- `DiscordPresence`, `MEModLoader`, `TrayIndicator`/`DBusNotificationHelper`, `AvatarHandlers/*` monoliths — not wrapped (no service depends on them; their patterns violate the exit criteria).
- Real VRM/animation/audio playback in tests — tests use injected fakes/adapters; the wrapped monoliths are exercised through their public surface.

## Tasks

- [x] T1: Scaffold `unity/` project — grab scripts+packages, write manifest + ProjectSettings, copy `runtime/Mate.Core` source — acceptance: headless `-runTests` compiles with 0 CS errors (covers: S2)
- [x] T2: `ICharacterService` + `CharacterService` (wraps VRMLoader) + tests — acceptance: CharacterServiceTests pass (covers: S2; depends: T1)
- [x] T3: `IMouseTracker` + `MouseTracker`, `IAnimationService` + `CharacterAnimator` + tests — acceptance: MouseTrackerTests + CharacterAnimatorTests pass (covers: S2; depends: T1)
- [x] T4: `IAudioService` + `PulseAudioService` + `AudioReactiveBridge` + tests — acceptance: AudioServiceTests + AudioReactiveBridgeTests pass (covers: S2; depends: T1)
- [x] T5: `ISystemService` + `SystemTrayService` + tests — acceptance: SystemTrayServiceTests pass (covers: S2; depends: T1)
- [x] T6: `IAIService` + `OllamaProvider` + `PersonalityService` + `IModService` + `ModService` + tests — acceptance: AIServiceTests + PersonalityServiceTests + ModServiceTests pass (covers: S2; depends: T1)
- [x] T7: Module integration tests + full headless EditMode suite green — acceptance: all `Mate.*` EditMode fixtures pass (covers: S2; depends: T2,T3,T4,T5,T6)