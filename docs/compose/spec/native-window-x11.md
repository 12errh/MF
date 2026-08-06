---
feature: native-window-x11
status: delivered
updated: 2026-08-06
branch: native-window-x11
commits: 40dccd9..eca2e1c
---

# Native Window Backend (X11)

## Report

**What was built** — A native X11 window backend that applies the `mate.toml`
`[window]` settings to the Unity player window. `X11WindowBackend` (in
`unity/Assets/MateFramework/Platform/`) ports the reference engine's X11
windowing — position/size (`XMoveWindow` + `_NET_MOVERESIZE_WINDOW`),
always-on-top and hide-from-taskbar (`_NET_WM_STATE` client messages),
borderless (`_MOTIF_WM_HINTS`), window type, click-through (`XShape`
input-shaping thread over `XGetImage`), mouse position (`XQueryPointer`),
monitors (`XRandR`), and window discovery (by PID via `_NET_WM_PID`) — with all
reference-only dependencies (SaveLoadHandler, DBus, Hyprland/KWin, Singleton)
removed. `WindowService` implements the previously-declared `IWindowService`
as a thin adapter over the `IWindowBackend` seam, reads `[window]` via
`IConfiguration`, and applies it on `Initialize`. It is registered in
`BootstrapComposer` and initialized by `MateBootstrap`; `MateTomlConfig` maps
the six `[window]` keys. The port is a plain class constructed in the composer
(rather than a scene MonoBehaviour) so it is testable with a fake backend.

**Verification** — Unity EditMode headless: 313 passed / 34 failed, all 34
PRE-EXISTING vendored UniGLTF/UniVRM10/VRM headless-incompatible tests; all 7
new `WindowServiceTests` + window registration in `BootstrapComposerTests`
pass. `cargo test --workspace`: 79 pass (unaffected). Live e2e on GNOME
Wayland (XWayland): rebuilt player, ran `mf dev` with `always_on_top=true`,
`borderless` + `click_through=true`; `xprop` confirmed `_NET_WM_STATE_ABOVE`
and `_MOTIF_WM_HINTS` decorations=0 applied, model loads, player stays alive,
no exceptions. The `window_type` setting is applied via client-message but was
not separately e2e-verified (the default is `normal`).

**Journey log** — (1) The `Mate.System` namespace shadows global `System`, so
threading/diagnostics/interop references needed `global::` qualification. (2)
The project's C# 9 profile rejects `record structs` — the backend data
records became plain structs. (3) Review found the port's `XImage.data` read
used `Marshal.ReadIntPtr(img, 0)` (reads `width|height` as a pointer — a
near-certain player crash on click-through); fixed by reading the `XImage`
struct and its `data`/`bytes_per_line`. (4) Review also found
`_NET_CLIENT_LIST` items are format-32 (4-byte XIDs) but were read at
`IntPtr.Size` stride — fixed to `i * 4`. (5) E2E on GNOME Wayland works
because the player runs as an XWayland window; the window is maximized by
GNOME, so `initial_position` is superseded by WM behavior, but the window
state flags (above, borderless) apply correctly.

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
a plain class in `unity/Assets/MateFramework/Platform/` (constructed in
`BootstrapComposer`, not a scene MonoBehaviour — this keeps it testable),
**decoupled** from the reference's god-object and Wayland dependencies that
this framework does not use:

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
- `MateBootstrap` resolves `IWindowService` and calls `Initialize(IntPtr.Zero)`
  (the backend locates the player window by PID itself).
- `MateTomlConfig` maps `[window]` keys to the service keys the services read.

## [S3] Out of Scope

- Hyprland and KWin (Wayland) backends — deferred (user decision).
- System tray / notifications — `ISystemService` stays event-based (separate
  feature).
- Windows / macOS.

## Tasks

- [x] T1: Port the X11 windowing into `X11WindowBackend` (decoupled from reference deps) — acceptance: it compiles in the Unity project with no reference-only dependencies; the X11 P/Imports + window ops (position/size/topmost/borderless/type/taskbar/mouse/monitors/visible) present and callable (covers: D1, D2)
- [x] T2: Implement `IWindowService` adapter + `IWindowBackend` seam — acceptance: `WindowService` maps `Result`-based methods over a fake backend; Unity EditMode tests pass (7 `WindowServiceTests` pass) (covers: D2)
- [x] T3: Apply `[window]` config + wire into `BootstrapComposer`/`MateBootstrap`/`MateTomlConfig` — acceptance: config keys read; Unity Edit tests pass (covers: D3, D4)
- [x] T4: Rebuild the player, e2e — acceptance: player launches and applies `always_on_top`/window type from `mate.toml` (verified via `xprop`: `_NET_WM_STATE_ABOVE` + borderless applied; `click_through=true` keeps the player alive) (covers: D3, D4; depends: T1, T2, T3)