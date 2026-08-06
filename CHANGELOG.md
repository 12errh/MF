# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Native X11 window backend (`IWindowService`):
  - `X11WindowBackend` — ported X11 windowing: position/size, always-on-top,
    borderless (`_MOTIF_WM_HINTS`), click-through (`XShape` input shaping),
    window type, hide-from-taskbar, mouse position, monitor enumeration
    (`XRandR`), and window discovery by PID.
  - `WindowService` — adapter over the `IWindowBackend` seam; applies the
    `mate.toml [window]` settings and names the window after the project.
  - `mf dev` detects a 32-bit ARGB visual and sets `SDL_VIDEO_X11_VISUALID`
    so the player opens a transparent window.
  - Idle animation: `MateAnimatorBuilder` builds a minimal AnimatorController
    (single Idle state bound to a humanoid idle clip) so characters break out
    of their T-pose; the loaded model is rotated to face the camera and the
    camera grounds the character at the bottom of the window.
- Real runtime download: `mf runtime install <version>` now downloads the
  player tarball from GitHub Releases (`12errh/MF`) via `reqwest`, extracts it
  with `tar`/`flate2`, and verifies the player binary lands in the expected
  layout. New `DownloadFailed` error variant reports the failed URL and HTTP
  status.
- `scripts/build-player.sh` — reproducible Unity Linux x64 player build into
  the runtime cache.
- First `v1.0.0` GitHub release carrying `MateRuntime-linux-x64.tar.gz`, so
  `mf dev` runs end-to-end out of the box.
- Unity bootstrap (composition root):
  - `MateBootstrap` MonoBehaviour — entry point that parses `--projectPath`,
    creates the scene objects the runtime needs, composes the `MateContext`,
    loads the configured VRM model, and drives audio polling + mouse tracking.
  - `BootstrapComposer` — registers all services as singletons.
  - `MateTomlConfig` — maps `mate.toml` (snake_case sections) to the service
    config keys.
  - `Tools/Mate/Create Bootstrap Scene` editor tool + committed entry scene.
- Phase 7 (Build & Package):
  - `mf runtime install <version>` stages a runtime version with validation;
    `RuntimeVersion` status/install/remove in `mf-core`.
  - `BuildManifest` JSON (`build-manifest.json`) written by `mf build` with
    runtime version, project name/version, timestamp, and asset list.
  - `mf package` now produces a self-describing tar.gz via `package_project`
    (build manifest included).
- Phase 8 (Developer Experience):
  - `HotReloadHandler` (Unity) — FileSystemWatcher with debounce for
    config/assets reload; code files ignored (ADR-013).
  - Actionable error messages: display server, missing runtime, missing model,
    Ollama not running, invalid version, security violations.
  - Enhanced `mf doctor`: runtime install, display server, and permissions
    checks with fix guidance; machine-readable JSON output.
- Phase 9 (Hardening):
  - CLI performance benchmarks (criterion): `mf --help`, `mf new`, `mf doctor`.
  - Security validators: path-traversal prevention and HTTPS-only URL checks.
  - `SECURITY.md` audit checklist.
- Phase 10 (Release):
  - `docs/getting-started.md` developer guide.
  - CI pipeline with unit + integration tests; tag-triggered release workflow
    publishing the CLI binary to GitHub Releases.

### Fixed

- `mf package` no longer fails when the archive is written into the directory
  being tar'd (archive is staged in a temp dir first).
- Runtime download CLI no longer claims "not implemented" for staged installs.
- `mf runtime install` creates the runtime cache parent directory, so the first
  install on a fresh machine succeeds (previously failed "No such file or
  directory").
- Unity player no longer fails to load the model: the bootstrap now creates a
  `CustomModelOutput` node the grabbed `VRMLoader` needs to parent loaded
  models (previously a null reference in a minimal scene).
- `cargo fmt` import ordering in `runtime.rs`.
