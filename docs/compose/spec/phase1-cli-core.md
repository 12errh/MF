---
feature: phase1-cli-core
status: delivered
updated: 2026-08-05
branch: phase1-cli-core
commits: 31a33ec..e0dde69
---

# Phase 1: CLI Core

## Report

**What was built** — `mf-core` gained three modules: `process` (spawn/kill/wait/is_running a Unity player with `--projectPath`), `watcher` (notify-based recursive watching classifying config/asset/mod/unknown events), and `build` (`build_project` validates the manifest and packages raw assets — `mate.toml`, `assets/`, `mods/`, `config/` — with no Unity Editor). `mf-cli` gained five commands: `mf dev` (resolve runtime, spawn the player, watch for changes, auto-restart on change or crash with a 5s time-windowed restart counter capped at 10, JSON event stream with `--json`), `mf runtime list/status/install`, `mf build [output]`, `mf package` (creates `<name>.tar.gz` via tar), and `mf capabilities` (desktop/session/Wayland/Hyprland feature detection).

**Verification** — `cargo build --workspace` PASS; `cargo test --workspace` PASS (49 tests: 37 mf-core + 9 mf-cli + 3 integration); `cargo clippy --workspace --all-targets -- -D warnings` PASS; `cargo fmt --all -- --check` PASS. CLI E2E verified: `new`/`doctor`/`runtime list`/`capabilities`/`build`/`package`/`--json`. `mf dev` with a fake player launches, restarts on file change, trips the 10-restart crash guard on rapid crashes, and resets the counter after a stable run.

**Journey log** —
- A plan `mf runtime`/`dev` code path used `mf_core::resolve_version`/`player_path`/etc. at the crate root, so `lib.rs` had to re-export the `runtime` module's functions (they were module-only in Phase 0).
- The plan's `package.rs` tar command (`tar -C build <manifest>`) referenced a `build/<name>` subdir that `build_project` never creates; fixed to `tar -czf <name>.tar.gz -C build .`.
- The plan's `mf dev` restart counter reset on every successful start; clippy's `unused_assignments` rejected that reset, but removing it turned the counter into a lifetime cap that killed the server after 10 edits. Fixed with a 5s time-window reset (`last_restart_at`) so only rapid consecutive restarts count — verified behaviorally.
- `wait_returns_exit_code` initially used `/bin/sh -c 'exit 3'`, but `spawn` prepends `--projectPath`, which `sh` rejects (exit 2). Switched to a temp executable script that ignores extra args.
- The plan's `mf dev` had a `DevExitReason::UserInterrupt` variant never constructed (no signal handler implemented); removed as dead code — Ctrl+C terminates via default signal behavior.

## [S1] Problem

Phase 0 delivered a Rust workspace and CLI skeleton, but `mf dev` is a stub and the CLI lacks the commands developers need to actually run and distribute a mate project. Phase 1 makes `mf dev` launch the Unity runtime, watch for file changes, and restart on crash; adds `mf runtime` (list/status/install), `mf build` and `mf package` (raw-asset packaging with no Unity Editor), and `mf capabilities` (platform feature detection).

## [S2] Design

- `mf-core` gains three modules:
  - `process`: `RuntimeLaunchConfig`, `RuntimeProcess` (`spawn`/`wait`/`kill`/`is_running`), and `build_launch_config`. Launch passes `--projectPath` plus project args; stdin null, stdout/stderr piped. Spawn fails with `MfError::RuntimeNotInstalled` when the player binary is missing.
  - `watcher`: `WatcherEvent` (`ConfigChanged`/`AssetChanged`/`ModChanged`/`Unknown`) and `ProjectWatcher` (notify-based, recursive, with `poll` and `wait_event`). Classifies events by file extension/path.
  - `build`: `BuildResult` and `build_project` — validates the manifest, creates the output dir, copies `mate.toml`, and recursively copies `assets/`, `mods/`, `config/`. No Unity Editor required (ADR-013).
- `mf-cli` gains commands:
  - `dev`: loads+validates the manifest, resolves the runtime version, spawns the player, watches for changes, and restarts on file change or crash (max 10 restarts), stopping on Ctrl+C. JSON event stream when `--json`.
  - `runtime` with subcommands `list`, `status`, `install` (install is a stub until releases exist).
  - `build [output]`: calls `build_project`, default output `build/`.
  - `package`: runs `build_project` then creates `<name>.tar.gz` via `tar`.
  - `capabilities`: reports desktop environment, session type, Wayland/Hyprland detection, and per-feature availability from env vars.
- Process management and file watching use the standard library + notify (crossbeam mpsc); no tokio dependency.
- Tests run without a real Unity runtime (error paths and hermetic temp dirs). No `unwrap()` in library code.

## [S3] Out of Scope

- Automatic runtime download (`mf runtime install` real implementation) — deferred until releases are published.
- Unity runtime integration and the C# codebase.
- Mod system, plugin system, hot reload beyond config/asset restart.
- Committing `refrence/` or planning docs.

## Tasks

- [x] T1: Process manager — acceptance: `cargo test -p mf-core` passes 2 process tests (covers: S2)
- [x] T2: File watcher — acceptance: `cargo test -p mf-core` passes 3 watcher tests (covers: S2)
- [x] T3: `mf dev` full implementation + `mf runtime` — acceptance: `cargo test --workspace` passes; `mf runtime list`/`status` produce output (covers: S2; depends: T1, T2)
- [x] T4: `mf build` + `mf package` — acceptance: `cargo test -p mf-core` passes 4 build tests; `mf build`/`package` create output (covers: S2; depends: T1)
- [x] T5: `mf capabilities` — acceptance: `cargo run -- capabilities` and `--json capabilities` produce output (covers: S2)