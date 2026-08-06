---
feature: e2e-release
status: delivered
updated: 2026-08-05
branch: e2e-release
commits: 9952d05..8983e4e
---

# E2E Release: Runtime Player Build, Download, Publish, and Verify

## Report

**What was built** — The framework now runs end-to-end. A reproducible `scripts/build-player.sh` builds the Unity Linux player into `~/.mate-framework/runtimes/<v>/MateRuntime/MateRuntime` (matching `player_path()`). `install_runtime` in `crates/mf-core/src/runtime.rs` performs a real download: `download_url()` points at the `12errh/MF` repo, and `install_runtime` fetches the release tarball with `reqwest::blocking` (rustls-tls), extracts it with `tar` + `flate2`, verifies the player binary, and surfaces a new `MfError::DownloadFailed { url, status }` on HTTP errors. A `v1.0.0` GitHub release was published carrying `MateRuntime-linux-x64.tar.gz` (27.8MB).

**Verification** — `cargo test --workspace`: 79 passed (63 mf-core + 13 mf bin + 3 integration). `cargo clippy --workspace`: clean. Unity EditMode headless: 306 passed / 34 failed, all 34 PRE-EXISTING vendored UniGLTF/UniVRM10/VRM headless-incompatible tests, 0 `Mate.*` failures. `.NET runtime/Mate.Core.Tests`: 33 passed. Live E2E: `mf new e2e-demo` → copied `Zome.vrm` → `mf runtime install 1.0.0` downloaded from `github.com/12errh/MF` and extracted → `mf dev` spawned the player with `--projectPath .`, the VRM model loaded with no error in `Player.log`. A clean-state re-test confirmed the first install on a machine with no `~/.mate-framework` succeeds.

**Journey log** — (1) The biggest unknown was the Unity player build; it succeeded on the first try because the Bootstrap scene was already in `EditorBuildSettings` — no custom build method needed. (2) The first live run failed to load the model: `VRMLoader.cs:247` null-refs `customModelOutput`, which the minimal bootstrap scene lacked — the importer works, only the scene wiring was missing; fixed in `MateBootstrap.EnsureVrmLoader`. (3) Review caught a latent fresh-install bug: the temp archive was staged without creating `~/.mate-framework`, so the first install on a clean machine would fail; the E2E only passed because the build had pre-created the dir. (4) `pkill` on `mf dev` hangs because it restarts the player; kill `mf dev` first, then the player process. (5) The 34 Unity test failures are vendored UniGLTF/UniVRM10/VRM tests that are headless-incompatible — a stable PRE-EXISTING baseline, not regressions.

## [S1] Problem

The framework cannot run end-to-end. Three blockers, verified in the prior session:

1. No Unity player binary exists anywhere — `mf dev` fails with "Unity player not found".
2. `install_runtime` is a stub: `download_url()` points at `github.com/mate-framework/mate-runtime` (a repo that does not exist) and the function returns the cache dir without downloading anything.
3. No GitHub release carries the player, so even a fixed downloader would 404.
4. The bootstrap scene now exists (committed on `main`, `9952d05`), so a player build is possible — but nothing builds it.

Result: `mf new` → `mf runtime install 1.0.0` → `mf dev` cannot show a character.

## [S2] Design

### D1 — Player build (local, one-time)

Unity 6000.2.6f2 is installed at `~/Unity/Hub/Editor/6000.2.6f2/Editor/Unity` and licensed. The Bootstrap scene is in `EditorBuildSettings`. Build the player with:

```
"$UNITY" -batchmode -nographics -quit \
  -projectPath unity \
  -buildLinux64Player <out>/MateRuntime/MateRuntime \
  -logFile <out>/build.log
```

The output binary lands at `<out>/MateRuntime/MateRuntime`. The cache layout that `player_path()` expects is `~/.mate-framework/runtimes/<v>/MateRuntime/MateRuntime`, so the build output directory should be that cache path (or be moved there).

A reproducible script `scripts/build-player.sh` records the exact command so step 1 is repeatable and the log is captured.

### D2 — Runtime download (mf-core)

`download_url()` in `crates/mf-core/src/runtime.rs` changes its host from `mate-framework/mate-runtime` to `12errh/MF`. The URL pattern stays:

```
https://github.com/12errh/MF/releases/download/v{version}/MateRuntime-linux-x64.tar.gz
```

`install_runtime(version)` performs a real download when the version is not installed and is a valid semver:

1. Validate the URL with the existing `security::validate_url` (https check).
2. Download the tarball to a temp file in the cache dir using `reqwest::blocking` (follow redirects — GitHub release assets redirect to objects.githubusercontent.com).
3. On HTTP error, surface a new `MfError` variant `DownloadFailed { url, status }` so the CLI can print the failed URL.
4. Extract with `tar` + `flate2` into `<cache>/<version>/`, then verify the player binary exists (`MateRuntime/MateRuntime`).
5. On failure, remove the staged archive so a retry starts fresh; the cache parent dir is created before download so a fresh machine installs cleanly.

Dependencies added to `crates/mf-core/Cargo.toml`: `reqwest` (blocking, rustls-tls, no default features to keep the binary lean), `tar`, `flate2`.

### D3 — Release artifact (local publish)

The tarball must extract to the exact layout `MateRuntime/MateRuntime` (a directory `MateRuntime` containing the player binary `MateRuntime`, plus its `_Data` sibling) so `player_path()` resolves. Build into a staging dir `<staging>/MateRuntime/MateRuntime`, then:

```
tar -czf MateRuntime-linux-x64.tar.gz -C <staging> MateRuntime
```

Publish with the authenticated `gh` CLI (account `12errh`, repo `12errh/MF`):

```
gh release create v1.0.0 --title "Mate Framework v1.0.0" \
  --notes "Runtime player build" MateRuntime-linux-x64.tar.gz
```

The `release.yml` workflow stays unchanged (it publishes the Rust CLI only; adding Unity player builds to CI requires license activation secrets that are not set up).

### D4 — E2E verification

1. `mf new demo-e2e` (or reuse a temp dir) — creates `mate.toml` with `runtime = "1.0.0"` and `model = "assets/avatar.vrm"`.
2. Copy a real VRM (from `refrence/Mate-Engine-Linux-Port/Assets/MATE ENGINE - Avatar/Zome.vrm`) into the project's `assets/` as `avatar.vrm`.
3. `mf runtime install 1.0.0` — downloads from the published release, extracts, verifies player exists.
4. `mf dev` — spawns the player with `--projectPath <dir>`; the player window opens and the character renders.
5. Success = player process starts and stays alive (checked via `mf dev --json` events or the running PID) and the model loads without an error in the player log.

Display is Wayland (`XDG_SESSION_TYPE=wayland`, `WAYLAND_DISPLAY=wayland-0`) — the player runs under the user's session.

## [S3] Out of Scope

- CI building of the Unity player (`release.yml` extension needs Unity license secrets; not available).
- Native window backends (X11/Hyprland/KWin tray, transparency) — deferred from prior work.
- Windows/macOS runtime builds.
- Checksum/signature verification of downloads (no signing infrastructure yet).

## Tasks

- [x] T1: Build the Linux player locally into `~/.mate-framework/runtimes/1.0.0/` via a reproducible `scripts/build-player.sh` — acceptance: `~/.mate-framework/runtimes/1.0.0/MateRuntime/MateRuntime` exists and runs without crashing; verified player launches with PulseAudio init (covers: D1)
- [x] T2: Create `MateRuntime-linux-x64.tar.gz` with exact `MateRuntime/` layout — acceptance: `tar -tzf` lists `MateRuntime/MateRuntime` and `MateRuntime/MateRuntime_Data/` (verified) (covers: D3)
- [x] T3: Fix `download_url()` host and implement real download in `install_runtime` with `DownloadFailed` error variant — acceptance: `cargo test --workspace` passes (79 tests: 63 core + 13 mf + 3 integration); URL test asserts `12errh/MF` (covers: D2)
- [x] T4: Publish release `v1.0.0` on `12errh/MF` with `MateRuntime-linux-x64.tar.gz` — acceptance: `gh release view v1.0.0` shows the asset (27.8MB); `curl -I` on the asset URL returns 200 (verified) (covers: D3; depends: T2)
- [x] T5: E2E — `mf new`, install 1.0.0 via real download, `mf dev` starts player — acceptance: `mf dev` spawns a live player PID with the VRM model loaded and no fatal error in the log (verified) (covers: D4; depends: T1, T3, T4)

### E2E-discovered fix (in scope for T5)

During T5, the first player run failed to load the model: `VRMLoader.cs:247` dereferences `customModelOutput.transform`, which is null in the minimal bootstrap scene. The importer succeeded; only the scene wiring was missing. Fixed in `MateBootstrap.EnsureVrmLoader` to create a `CustomModelOutput` child under the loader. After rebuild, the model loads with no error. This is a durable fix recorded here so the reviewed range `9952d05..3a40795` is understood to include it.

### Review fix (in scope for T3)

Reviewer found a latent fresh-install bug: `install_runtime` staged the temp archive at `~/.mate-framework/runtimes/.{version}.{pid}.tar.gz` without creating the parent, so the first install on a clean machine failed (`Io: No such file or directory`). Fixed in `8983e4e`: `download()` creates `dest.parent()` before writing, and the staged archive is removed on any download/extract failure. Re-verified with a clean-state install (no `~/.mate-framework`) succeeding.
