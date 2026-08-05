---
feature: unity-bootstrap
status: in-progress
updated: 2026-08-05
branch: unity-bootstrap
commits: # filled at delivery
---

# Unity Bootstrap — Composition Root & Scene

## Report

## [S1] Problem

The framework cannot run end-to-end. `mf dev` launches a Unity player with
`--projectPath <dir>` (`crates/mf-core/src/process.rs:29`), but the `unity/`
project has **no scene, no MonoBehaviour, and no composition root** — it is a
library of services with no entry point. `VrmLoaderAdapter` requires a
`VRMLoader` component in the scene (`FindFirstObjectByType<VRMLoader>()`) and
`PulseAudioAdapter` requires a `PulseAudioManager` component; neither exists
at runtime. Services also read camelCase config keys (`soundThreshold`,
`allowedApps`, `danceSwitchTime`, `ai.model`) while the project manifest
`mate.toml` uses snake_case sections (`[audio] threshold = 0.5`, …), so no
config source currently feeds the services from a real project.

## [S2] Design

### Architecture

A `MateBootstrap : MonoBehaviour` is the composition root. The scene (created
by an editor tool) contains a Camera, the bootstrap object, and nothing else —
the bootstrap creates the `VRMLoader` and `PulseAudioManager` scene objects
it needs (they are `MonoBehaviour`s the grabbed monoliths require).

Two layers keep it testable:

1. **Pure, testable layer** (`Mate.Core`/`Mate.Bootstrap` namespaces):
   - `MateTomlConfig : IConfiguration` — parses `mate.toml` (naive
     section-aware line parser, same pattern as `PersonalityService`) and maps
     manifest keys to the service keys.
   - `BootstrapComposer.Compose(projectDir, adapters?) -> MateContext` —
     registers every service into a fresh `MateContext` (mirrors
     `ModuleIntegrationTests.AllServices_RegisterAndResolve_ViaMateContext`).
   - `BootstrapArgs.ParseProjectPath(string[] args)` — extracts `--projectPath`
     value with an `MATE_PROJECT_DIR` env fallback (editor dev).
2. **Thin Unity adapter** (`MateBootstrap` MonoBehaviour):
   - `Awake`: parse args → create missing scene objects (Camera + audio
     listener, `VRMLoader`, `PulseAudioManager`) → `BootstrapComposer.Compose`
     → load the `[character] model` from config.
   - `Update`: drive `PulseAudioService.Poll()` and the mouse tracker.
   - `OnDestroy`: dispose the `MateContext` (disposes IDisposable singletons).

### Config key mapping (`MateTomlConfig`)

`mate.toml` (per `crates/mf-core/src/manifest.rs`) → service keys. Keys marked
**extension** are not part of the manifest schema — users may add them to
`mate.toml`, and they are served via raw dotted-key lookup; missing keys fall
back to the service's own defaults.

| mate.toml path | Service key | Kind | Read by |
|---|---|---|---|
| `[audio] threshold` | `soundThreshold` | manifest | PulseAudioService, AudioReactiveBridge |
| `[audio] allowed_apps` | `allowedApps` (comma-joined) | manifest | PulseAudioService |
| `[animation] dance_switch_time` | `danceSwitchTime` | manifest | CharacterAnimator |
| `[animation] idle_switch_time` | `idleSwitchTime` | manifest | CharacterAnimator |
| `[animation] dance_animation` | `danceAnimation` | extension | CharacterAnimator |
| `[character] head_sensitivity` | `headSensitivity` | extension | MouseTracker |
| `[character] eye_sensitivity` | `eyeSensitivity` | extension | MouseTracker |
| `[character] spine_sensitivity` | `spineSensitivity` | extension | MouseTracker |
| `[character] model` | `modelPath` | manifest | Bootstrap (loads model) |
| `[ai] model` | `ai.model` | manifest | OllamaProvider |
| `[ai] base_url` | `ai.baseUrl` | extension | OllamaProvider |
| `[ai] enabled` | `ai.enabled` | manifest | OllamaProvider |

Unknown keys are ignored; missing keys fall back to the service's own
defaults. `Get*` lookup order: mapped value → raw dotted key → default.

### Error behavior

- Missing/invalid `mate.toml`: log error, compose with defaults (services
  already default safely), do not crash the player.
- `[character] model` missing or file absent: `CharacterService.LoadModel`
  returns `Result.Fail` — logged, player continues running (no model).
- No `--projectPath` and no env fallback: use
  `Directory.GetCurrentDirectory()`; still compose.

### Scene creation (editor tool)

`unity/Assets/MateFramework/Editor/MateSceneBuilder.cs`:
- Menu `Tools/Mate/Create Bootstrap Scene` — creates/opens a scene with a
  `Camera` + `AudioListener`, a `MateBootstrap` GameObject, and adds it to
  `EditorBuildSettings` so a player build has an entry scene.
- The generated scene is **committed** (`Scenes/Bootstrap.unity`) so a fresh
  clone can build a player without re-running the tool. (Hand-authored YAML
  GUIDs are fragile; the tool-generated scene avoids that.)
- Batchmode-safe: the confirmation dialog is skipped when
  `Application.isBatchMode`, so CI/CLI can invoke it headlessly.

### Testing

- `MateTomlConfigTests` (EditMode): parse a real `mate.toml` fixture;
  mapped keys resolve; snake_case `[audio] allowed_apps = ["a","b"]` becomes
  `allowedApps` "a,b"; missing keys fall back to defaults; invalid file → all
  defaults.
- `BootstrapArgsTests` (EditMode): `--projectPath` parsing, env fallback,
  no-arg default.
- `BootstrapComposerTests` (EditMode): compose with fake adapters; all
  services resolve; `[character] model` file present → `LoadModel` succeeds
  (with a fake `IVrmLoader`); model missing → `Result.Fail`, no throw.
- Verification: headless EditMode suite — all `Mate.*` (existing 84 + new)
  pass; the 34 vendored UniGLTF/VRM failures remain PRE-EXISTING.

## [S3] Out of Scope

- Building/publishing the Linux player (`-buildLinux64Player` + release
  workflow) — the next phase after this; the scene builder produces the entry
  scene it needs.
- Real VRM asset loading in tests (no `.vrm` in repo) — tests use fakes and
  graceful missing-model paths.
- Native window backends (X11/Hyprland/KWin), tray, notifications — still
  deferred (Phase 5 platform layer).
- PlayMode tests (need the player/play mode; EditMode coverage is sufficient
  for the composition logic).
- TOML schema beyond the keys in the table above; unknown keys ignored.

## Tasks

- [ ] T1: `MateTomlConfig` (parse + map `mate.toml` → service keys) + tests —
      acceptance: MateTomlConfigTests pass headless (covers: S2-Config; S2-Testing)
- [ ] T2: `BootstrapArgs.ParseProjectPath` + tests — acceptance:
      BootstrapArgsTests pass (covers: S2-Architecture; S2-Testing)
- [ ] T3: `BootstrapComposer.Compose` (register all services + model load) +
      tests — acceptance: BootstrapComposerTests pass (covers: S2-Architecture;
      S2-Error; S2-Testing)
- [ ] T4: `MateBootstrap` MonoBehaviour (scene objects, Update loop, dispose)
      + `MateSceneBuilder` editor tool — acceptance: headless compile 0 CS
      errors, `Tools/Mate/Create Bootstrap Scene` creates scene with Camera +
      MateBootstrap and adds to build settings (covers: S2-Architecture;
      S2-Scene)
- [ ] T5: Full headless EditMode suite green (existing 84 + new Mate.* tests,
      0 failures; 34 vendored PRE-EXISTING) — acceptance: `-runTests
      -testPlatform EditMode` shows all Mate.* pass (covers: S2-Testing;
      depends: T1,T2,T3,T4)
