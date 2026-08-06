---
feature: wire-features-end-to-end
status: delivered
updated: 2026-08-05
branch: feat/wire-features
commits: 98acc40..2e7d07c
---

# Wire Features End-to-End

## Report

**What was built** — Previously, the README claimed mouse tracking, audio
reactivity, system tray, and notifications, but an audit showed these services
were registered yet inert in the running player. This feature wired each one
end-to-end so `mf dev` actually performs the advertised behavior.

Mouse tracking now produces signed yaw/pitch (not just magnitude) and a new
`MouseTrackingApplier` rotates the loaded character's head and spine toward the
cursor, with sensitivity and max angles configurable via `mate.toml`.
Audio-reactive dancing is wired through `PulseAudioService.DiscoverAndMonitor()`
(which starts monitoring allowed apps so `Poll()` emits real peaks), an
`AudioReactiveBridge` that publishes `DanceStartedEvent`, and a `DanceReaction`
consumer that plays the configured dance clip (default `MateDance`, built by the
`MateDanceBuilder` editor tool; selectable via `[animation] dance_animation`).
System tray and notifications are wired through a `LinuxNativeTray`
(AppIndicator + `notify-send`) behind an `INativeTray` seam, with a
`SystemTrayReaction` forwarding events; the bootstrap shows the tray icon on
start. Mods are now actually loaded (`ModService.LoadMods()` in the bootstrap).
AI chat remains service-layer-only (no GUI) and is documented as such.

**Verification** — Rust: `cargo test --workspace` → 79 passed, 0 failed.
Unity EditMode: full suite → 370 total, 336 passed, 34 failed (all 34 are the
documented PRE-EXISTING vendored UniGLTF/VRM/UniVRM10 headless failures); 139
framework (non-vendored) tests passed, 0 failed. New tests added for the
applier, dance reaction, tray reaction, and adapter NodeId mapping.

**Journey log** —
- The core defect was architectural: services were interface-first and
  unit-tested, but the composition root only drove window + model + idle. Tests
  passing gave a false sense of completeness. The fix was wiring the bootstrap,
  not changing service logic.
- `MouseTracker` originally produced magnitude-only blends (abs of offset), which
  cannot express direction — the character could never "look at" the cursor. It
  needed signed yaw/pitch.
- Audio monitoring was inert because `StartMonitoring` was never called, so
  `_monitoredNodes` stayed empty and `Poll()` published nothing. `NodeId` had to
  be threaded through `AudioProgramInfo` to map apps to monitor nodes.
- AppIndicator's `set_icon_full` takes a themed icon name, not a filesystem path
  — a custom icon needs `app_indicator_set_icon_theme_path(dir)` + the bare
  filename. This was caught in review.
- The `Mate.System` namespace shadows `System`, so `StringComparison` had to be
  `global::System.StringComparison` in tracking code.

## [S1] Problem

The README claims mouse tracking, audio-reactive dancing, system tray, and
notifications are implemented, but the running player does nothing with them.
An audit of the composition root (`MateBootstrap`) and the services found that
several features are **registered but never driven**:

- **Mouse tracking**: `MouseTracker.Update()` computes blend values, but
  `GetBlendValues()` is never read by any runtime code (only tests). The
  character never responds to the cursor.
- **Audio-reactive dancing**: `PulseAudioService.Poll()` iterates
  `_monitoredNodes`, but `StartMonitoring()` is never called, so the set is
  empty and no `AudioPeakEvent` ever fires. Even when it would,
  `AudioReactiveBridge` publishes `DanceStartedEvent`, but nothing subscribes
  to actually start a dance.
- **System tray / notifications**: `SystemTrayService.ShowTrayIcon()` and
  `ShowNotification()` are never invoked, and no native consumer exists for
  the `TrayIconShownEvent`/`NotificationShownEvent` they publish.
- **Mods**: `ModService.LoadMods()` is never called; mods are scanned manifests
  that nothing loads.
- **AI chat**: `OllamaProvider` is registered but there is no input path to
  actually chat (no GUI).

The result: the README overclaims and each feature is inert in the real player.

## [S2] Design

Wire each service end-to-end so the running player actually performs the
advertised behavior, and make the README honest about what works where.

### Mouse tracking

`MouseTracker` computes magnitude-only blend values (0..1) from the absolute
cursor offset. To make the character actually track the cursor, the tracker
must also produce **signed** direction values (-1..1). Extend
`MouseBlendValues` with signed fields and compute them in `Update()` while
keeping the existing magnitude fields (backward compatible for tests).

Add a `MouseTrackingApplier` MonoBehaviour that:
- resolves `IMouseTracker` and `ICharacterService` from the `MateContext`;
- on model load, finds the model's head and spine bones (via the model's
  `Animator` humanoid bones when present, else by bone-name lookup under the
  model root);
- in `Update()`, reads `GetBlendValues()` and rotates the head/spine bones by
  `signed * maxAngle` where `maxAngle` comes from config
  (`character.head_max_angle`, `character.spine_max_angle`, defaults 20°/10°).

Wire it into `MateBootstrap.Update()` alongside the existing `_mouse.Update()`.

### Audio-reactive dance

1. `AudioProgramInfo` gains a `NodeId` so the service can map playing programs
   to monitor nodes. `PulseAudioAdapter` populates it from the grabbed
   `AudioProgram.NodeId`.
2. `PulseAudioService` gains `DiscoverAndMonitor()`: queries playing programs,
   and for each allowed app calls `StartMonitoring(nodeId)`. `Poll()` is
   unchanged (reads monitored nodes). The bootstrap calls
   `DiscoverAndMonitor()` on start and periodically.
3. A `DanceReaction` consumer subscribes to `DanceStartedEvent`/
   `DanceStoppedEvent` and drives the character's `Animator` to a Dance state.
   The dance clip is **not hardcoded**: `[animation] dance_animation` config
   selects the clip name (default `MateDance`). A dev supplies their own clip
   by dropping it in `Assets/Resources` and setting `dance_animation`. A
   default `MateDance.anim` clip is built by editor tool `MateDanceBuilder`
   (procedural bob/sway) so the feature works out of the box.

### 3. System tray & notifications

Implement a native consumer that subscribes to the existing
`TrayIconShownEvent`/`TrayIconHiddenEvent`/`NotificationShownEvent`:

- **Tray**: AppIndicator P/Invoke (port the reference `TrayIndicator.cs` core:
  `app_indicator_new`, `set_status`, `set_icon`, `set_menu`).
- **Notifications**: use the `notify-send` command (no extra native library).

`MateBootstrap` calls `ShowTrayIcon(config)` on start. The native consumer is
behind an `INativeTray` interface so EditMode tests exercise the event→call
mapping without native calls.

### 4. Mods

`MateBootstrap` calls `LoadMods(modsPath)` on start and exposes
`InstalledMods`. Per ADR-013, v1 mods are **metadata + asset overrides only**
(no code execution). A dev adds a mod by creating `mods/<name>/mod.toml` plus
any asset overrides; the service scans and exposes them. Document this.

### 5. AI chat

Deferred. `OllamaProvider` stays registered and testable, but there is no
interactive input path in v1. The README and this spec mark AI chat as
**service-layer only, GUI planned next**. No misleading "chat" claim.

### Documentation honesty

Update the README `Features` section and the status table to distinguish:
- **Wired & working** (verified): window, model load, idle, mouse tracking,
  audio-reactive dance, system tray, notifications, mods.
- **Service-layer only** (registered/tested, no player path yet): AI chat.

## [S3] Out of Scope

- A GUI / right-click context menu / chat UI (planned as a follow-up feature).
- Code-extensible mods (deferred; ADR-013 keeps v1 to config + asset mods).
- Wayland (Hyprland/KWin) window backends (already deferred separately).
- Discord RPC.
- Windows/macOS native tray/notify.

## Tasks

- [x] T1: Add signed direction to `MouseBlendValues` + `MouseTracker` — acceptance: `MouseTrackerTests` extended; `GetBlendValues()` returns signed fields within -1..1 and existing magnitude tests still pass (covers: S2)
- [x] T2: Add `MouseTrackingApplier` that applies blends to model bones — acceptance: new EditMode test proves the applier rotates a fake model's head bone from a signed blend; wired in `MateBootstrap` (covers: S2; depends: T1)
- [x] T3: Add `NodeId` to `AudioProgramInfo` + map in `PulseAudioAdapter` — acceptance: adapter test maps a grabbed `AudioProgram`'s NodeId into `AudioProgramInfo` (covers: S2)
- [x] T4: Add `PulseAudioService.DiscoverAndMonitor()` and call from bootstrap — acceptance: EditMode test shows `DiscoverAndMonitor` starts monitoring allowed playing apps; `Poll()` then publishes `AudioPeakEvent` (covers: S2; depends: T3)
- [x] T5: Add `DanceReaction` consumer + `MateDance.anim` build tool + `[animation] dance_animation` wiring — acceptance: EditMode test shows `DanceStartedEvent` triggers a dance state on a fake Animator; `MateDanceBuilder` is an editor tool; config selects the clip (covers: S2; depends: T4)
- [x] T6: Add native tray/notification consumer behind `INativeTray`; call `ShowTrayIcon` at bootstrap — acceptance: EditMode test verifies `TrayIconShownEvent` maps to a native call through `INativeTray`; bootstrap invokes `ShowTrayIcon` (covers: S2)
- [x] T7: Wire `LoadMods` in bootstrap + expose `InstalledMods` — acceptance: bootstrap loads a `mods/<name>/mod.toml` and it appears in `InstalledMods`; EditMode test (covers: S2)
- [x] T8: Update README status table + Features to distinguish wired vs service-layer-only; document mod authoring and AI-chat deferral — acceptance: README no longer claims AI chat is interactive; each feature marked accurately (covers: S2)