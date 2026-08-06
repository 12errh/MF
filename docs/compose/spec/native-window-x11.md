---
feature: native-window-x11
status: in-progress
updated: 2026-08-06
branch: native-window-x11
commits:
---

# Native Window Backend (X11)

## Report

## [S1] Problem

The framework runs end-to-end but the character renders in a **plain, normal
Unity window**. The `[window]` section of `mate.toml` (`transparent`,
`always_on_top`, `click_through`, `hide_from_taskbar`, `window_type`,
`initial_position`) is parsed but **not applied** — no native window backend
exists. The `IWindowService` interface is declared (`Mate.Core`) but has no
implementation and is not wired into `BootstrapComposer`. The reference engine
has a complete X11 window backend (`WindowManager.cs`, 2618 lines) that this
framework can reuse instead of rewriting from scratch.

## [S2] Design

### D1 — Port the X11 windowing core (decoupled)

Port the reference `WindowManager.cs` X11 implementation into the framework as
a MonoBehaviour in `unity/Assets/MateFramework/Platform/`, **decoupled** from
the reference's god-object and Wayland dependencies that this framework does
not use:

- **Removed**: `SaveLoadHandler`, `Singleton<>`, `Tmds.DBus`, Hyprland/KWin
  backends, `DBusNotificationHelper`, `EarlyEnvSet`, `GtkX11Helper`. These are
  all reference-only and must not leak into the port.
- **Kept**: the `WindowManager` core behavior needed by the framework.

The source of truth for what the framework's `WindowManager` covers is the
reference implementation (read at port time). The framework's `IWindowService`
interface (in `unity/Assets/MateFramework/`) is the **contract** — it defines
what the framework *needs* from the reference. Where the reference already
implements it, the port reuses it.

### D2 — Backend seam (testability, ADR-012/ADR-005)

The framework's established pattern is "grabbed monolith + injectable adapter"
(VRMLoader/PulseAudio). Window follows the same pattern:

- `IWindowBackend` — internal seam listing the operations `WindowService`
  needs (position, size, always-on-top, borderless, click-through,
  hide-from-taskbar, window type, mouse position, monitors, visible windows,
  initialize with the Unity window handle).
- `X11WindowBackend : IWindowBackend` — the ported X11 implementation.
- `WindowService : IWindowService` — thin adapter over `IWindowBackend`,
  mapping to the async `Result`-based contract and reading `mate.toml`
  `[window]` via `IConfiguration`. Testable with a fake backend.

### D3 — Config → window application

`WindowService` reads `[window]` from `IConfiguration` on initialize and
applies it to the Unity window:

- `always_on_top` → `SetAlwaysOnTop`
- `transparent` + `click_through` → `SetBorderless` + `SetClickThrough`
- `hide_from_taskbar` → `HideFromTaskbar`
- `window_type` → `SetWindowType` (normal/dock/desktop)
- `initial_position` → `SetPosition` (center or `x,y`)

### D4 — Wiring

- `BootstrapComposer` registers `IWindowService` (singleton) backed by the
  X11 backend.
- `MateBootstrap` creates the X11 backend GameObject (like it already does for
  `VRMLoader`/`PulseAudioManager`) and calls `Initialize` after the window exists.
- `MateTomlConfig` maps `[window]` keys to the service keys the services read.

## [S3] Out of Scope

- Hyprland and KWin (Wayland) backends — deferred (user decision).
- System tray / notifications — `ISystemService` stays event-based (separate
  feature).
- Windows / macOS.

## Tasks

- [ ] T1: Port the X11 windowing into `X11WindowBackend` (decoupled from reference deps) — acceptance: it compiles in the Unity project with no reference-only dependencies; the X11 P/Imports + window ops (position/size/topmost/borderless/type/taskbar/mouse/monitors/visible) present and callable (covers: D1, D2)
- [ ] T2: Implement `IWindowService` adapter + `IWindowBackend` seam — acceptance: `WindowService` maps `Result`-based methods over a fake backend; Unity EditMode tests pass (covers: D2)
- [ ] T3: Apply `[window]` config + wire into `BootstrapComposer`/`MateBootstrap`/`MateTomlConfig` — acceptance: config keys read; Unity Edit tests pass (covers: D3, D4)
- [ ] T4: Rebuild the player, e2e — acceptance: player launches and applies `always_on_top`/window type from `mate.toml` (verified via log or window state) (covers: D3, D4; depends: T1, T2, T3)