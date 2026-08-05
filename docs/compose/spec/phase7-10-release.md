---
feature: phase7-10-release
status: delivered
updated: 2026-08-05
branch: phase7-10-release
commits: fc12be7..7e0dc1f
---

# Phase 7-10: Build, DX, Hardening, Release

## Report

**What was built** — The release pipeline for the Mate Framework CLI/core is now complete. `mf runtime install <version>` validates and stages a runtime version (`RuntimeVersion`/`InstallStatus`/`install_runtime`/`remove_runtime`), `mf build` writes a reproducible `build-manifest.json` (runtime version, project name/version, timestamp, asset list), and `mf package` produces a self-describing tar.gz via `package_project` (archive staged in a temp dir to avoid tar's "file changed as we read it" failure; re-package skips the prior archive). Error messages are actionable (`NoDisplayServer`, `RuntimeMissing`, `ModelNotFound`, `OllamaNotRunning`, `InvalidVersion`, `SecurityViolation`), `mf doctor` now checks runtime install, display server, and permissions with machine-readable JSON output, and CLI performance benchmarks exist for `mf --help`, `mf new`, `mf doctor`. Security validators (`validate_path` path-traversal, `validate_url` HTTPS-only) ship with a `SECURITY.md` audit checklist. On the Unity side, a `HotReloadHandler` watches config/assets via FileSystemWatcher with debounce (code files ignored per ADR-013). Release docs and automation are in place: `docs/getting-started.md`, `CHANGELOG.md`, an extended CI pipeline (unit + integration jobs), and a tag-triggered `release.yml`.

**Verification** — `cargo test --workspace` PASS: 78 tests (62 mf-core, 13 mf bin, 3 integration), 0 failed; `cargo fmt --all -- --check` PASS; `cargo clippy --workspace --all-targets` PASS (0 warnings); `cargo bench -p mf --bench cli_benchmarks -- --test` PASS (mf_help/mf_new/mf_doctor all Success). Unity headless EditMode suite: 320 total, 286 passed, 34 failed — all 34 are PRE-EXISTING vendored UniGLTF/VRM/UniVRM10 failures (headless-incompatible), not this branch's code; `HotReloadHandlerTests` 6/6 pass. e2e: `mf build` writes `build/build-manifest.json`; `mf package` produces `demo.tar.gz`.

**Journey log** — (1) The plan's `package_project` referenced `build_result.manifest_version` which doesn't exist on `BuildResult`; runtime version is read from the parsed `mate.toml` instead. (2) Writing the archive inside the directory being tar'd makes tar fail ("file changed as we read it") — the archive is staged in a temp dir (uniquely named with pid + nanos, since tests run in parallel in one process) then copied; re-package also nests the prior `.tar.gz`, so `scan_assets` skips archives and `build-manifest.json`. (3) The plan's `install_runtime("99.99.99")` error test contradicts its own `is_valid()` (valid semver), so the invalid-version test uses a non-semver string. (4) Unity tests compile into a separate editor assembly, so `internal` test seams (`ShouldWatch`/`TriggerReload`) had to be `public`. (5) Review found the CI integration job broken two ways (relative binary path after `cd`, and a manifest assertion after `mf build`); fixing it drove the decision to have `mf build` write the manifest, which also keeps the plan's exit criteria and docs accurate.

## [S1] Problem

The Mate Framework CLI (`crates/mf-cli`, package `mf`) and core (`crates/mf-core`) exist and pass tests, but the release pipeline is incomplete: `mf runtime install` is a stub (prints "not implemented"), `mf build` produces no build manifest, `mf package` creates an archive but no manifest/runtime info, error messages are terse, `mf doctor` covers only manifest/assets/mods, there are no performance baselines, no security validators, no getting-started guide, no release CI, and no CHANGELOG. The Unity side lacks a hot-reload handler for config/assets (ADR-013: TOML + assets only, never code).

This phase implements the full release story from the plan `docs/compose/plans/2026-08-04-phase7-10-release.md`, adapted to the actual codebase where the plan's snippets are aspirational.

## [S2] Design

### P7.1 Runtime download & install (`crates/mf-core/src/runtime.rs`, `crates/mf-cli/src/commands/runtime.rs`)

- Add `RuntimeVersion(pub String)` tuple struct with:
  - `status() -> InstallStatus` — `NotInstalled` (no dir), `Installed` (player binary exists), `Incomplete` (dir but no player)
  - `cache_dir() -> PathBuf` — `runtime_cache_dir().join(version)`
  - `download_url() -> String` — GitHub Releases URL `https://github.com/mate-framework/mate-runtime/releases/download/v{version}/MateRuntime-linux-x64.tar.gz`
  - `remove() -> Result<(), MfError>` — remove dir if exists, no-op otherwise
  - `is_valid() -> bool` — parses as `semver::Version`
- Add `InstallStatus` enum (NotInstalled/Downloading/Installed/Incomplete), `Clone/Debug/PartialEq`.
- Add `install_runtime(version) -> Result<PathBuf, MfError>`: already-installed → Ok(cache_dir); invalid semver → `MfError::InvalidVersion`; otherwise return cache_dir (real download is deferred until a release exists — no network in tests; the CLI keeps its "releases not yet published" message for the download path).
- Add `remove_runtime(version) -> Result<(), MfError>`.
- Add `MfError::InvalidVersion(String)` variant.
- Wire `RuntimeCommands::Install` (currently prints "not_implemented") to accept an optional version argument and report install/status: if a version is given, run `install_runtime`; else print current guidance. Keep JSON output shape.
- Tests: status not-installed / cache_dir path / download_url shape / remove nonexistent ok / install missing-version error (T27).

### P7.2 BuildManifest (`crates/mf-core/src/build.rs`)

- `BuildManifest` struct `{ runtime_version, project_name, project_version, built_at, assets: Vec<String> }` with `Serialize/Deserialize`, `new()`, `add_asset()`, `write(dir)` → `build-manifest.json`, `read(dir)`.
- `built_at` via `chrono::Utc::now().to_rfc3339()`; add `chrono = "0.4"` to mf-core deps (serde_json already present).
- Tests: new sets fields, add_asset, write/read roundtrip, read fails for missing file, valid JSON (T28).

### P7.3 package_project (`crates/mf-core/src/build.rs`, `crates/mf-cli/src/commands/package.rs`)

- `PackageResult { archive_path, archive_size, includes_runtime, manifest }`.
- `build_project` writes `build-manifest.json` into the output dir (runtime version + project version + scanned asset list) — so `mf build` produces the manifest per the plan's exit criteria. **Adaptation**: the plan references `build_result.manifest_version`/`build_result.manifest` — actual `BuildResult` only has `manifest: String` (project name); runtime version comes from the parsed manifest via `parse_manifest`.
- `package_project(project_dir, output_dir) -> Result<PackageResult, MfError>`: build via `build_project` (which writes the manifest), read the manifest back, `tar -czf` the build dir, return archive metadata. `scan_assets` skips `.tar.gz` and `build-manifest.json` so re-package doesn't nest the prior archive.
- CLI `package.rs` delegates to `package_project` (build dir from current dir, `build` output dir) instead of hand-rolling tar.
- Tests: creates archive (exists, size>0, .tar.gz suffix), includes build manifest, works without assets (T29).

### P8.1 HotReloadHandler (C# Unity, `unity/Assets/MateFramework/Core/HotReloadHandler.cs` + tests in `unity/Assets/MateFramework/Tests/Editor/HotReloadHandlerTests.cs`)

**Adaptation**: plan paths `Assets/MATE ENGINE - Scripts/...` point into the vendored reference tree; our code lives under `Assets/MateFramework/`.

- `Mate.Core.HotReloadHandler : IDisposable`, constructor `(string projectDir, IEventBus eventBus)`:
  - `FileSystemWatcher` with `IncludeSubdirectories`, `NotifyFilter = LastWrite | FileName`, events on Changed/Created.
  - Extension whitelist: `.toml, .json, .vrm, .wav, .mp3, .anim` — code files (`.cs`) ignored (ADR-013).
  - 500 ms debounce `Timer` → publishes `ConfigReloadedEvent(Source)` via `IEventBus`, records `LastReloadTime = DateTime.UtcNow`.
  - Safe when dir doesn't exist (no-op watcher); `Dispose()` idempotent, stops watcher + timer.
- `ConfigReloadedEvent(string Source)` record in `Mate.Core`.
- Tests (EditMode, no real filesystem races — tests drive the handler's public surface): records last-reload time default, dispose stops watcher, ignores code files (direct extension-filter check via a test seam), publishes `ConfigReloadedEvent` when a whitelisted file changes. **Adaptation**: plan's tests use `Thread.Sleep(2000)` around FileSystemWatcher — flaky in headless EditMode; expose the extension filter as an internal method (`ShouldWatch(extension)`) so filtering is testable without filesystem timing.
- Verification: headless Unity EditMode run, `HotReloadHandlerTests` all pass (T30).

### P8.2 Actionable error messages (`crates/mf-core/src/error.rs`)

- Add variants with guidance:
  - `NoDisplayServer` — "display server not detected. Are you running X11 or Wayland? Set XDG_SESSION_TYPE"
  - `RuntimeMissing { version }` — "Unity player not found for runtime v{version}. Run `mf runtime install {version}`"
  - `ModelNotFound { path }` — "VRM model not found at {path}. Check your [character] model setting in mate.toml"
  - `OllamaNotRunning` — "Ollama not running. Start it with `ollama serve` or disable AI in mate.toml"
  - `ManifestNotFound` message already exists ("manifest not found at {path}") — extend with ". Run `mf new <name>` to create a project" and update the existing test's expected string.
  - `InvalidVersion(String)` (from P7.1) and `SecurityViolation(String)` (from P9.2).
- Tests: each new variant's `to_string()` contains the actionable hint (T31).

### P8.3 Enhanced `mf doctor` (`crates/mf-cli/src/commands/doctor.rs`)

- Refactor `run_inner` to collect checks into a vec (already does), extend with:
  - `runtime` check — `list_installed()`, report installed count + whether project's `runtime` version is present; if none installed, `error` with guidance "Run `mf runtime install`".
  - `display_server` check — env `XDG_SESSION_TYPE`/`XDG_CURRENT_DESKTOP`; missing → `warning` with "Set XDG_SESSION_TYPE" guidance.
  - `permissions` check — writability probe on project dir (create+remove temp file); failure → `warning`.
- Expose `run_doctor(dir) -> Result<String, MfError>` (human) and `run_doctor_json(dir) -> Result<String, MfError>` returning the JSON payload string, so tests can call the logic without capturing stdout. CLI `run` prints the result.
- The runtime check is a pure `check_runtime(installed, project_runtime)` helper so its guidance branches are testable without depending on the machine's real runtime cache.
- Tests: manifest missing reported (ok result), JSON output parses as array/object, check names include manifest/runtime/assets/display_server/permissions (T32).

### P9.1 Performance benchmarks (`crates/mf-cli/benches/cli_benchmarks.rs`)

- Criterion benches: `mf_help`, `mf_new`, `mf_doctor` (spawn `cargo run --` from a temp project dir for doctor).
- `criterion = { version = "0.5", features = ["html_reports"] }` dev-dep + `[[bench]]` entry on mf-cli.
- Verify with `cargo bench -p mf-cli -- --test` (runs benches once as tests — cheap, no full measurement loop) (T33).

### P9.2 Security validation (`crates/mf-core/src/security.rs` + `SECURITY.md`)

- `validate_path(project_dir, target) -> Result<(), MfError>` — canonicalize both; error `SecurityViolation` if target doesn't start with project. **Adaptation**: the plan's `validate_path_within_project` test passes a non-existent file (`assets/avatar.vrm` never created) — `canonicalize` fails on non-existent paths, so the test would break; create the file in the test (or use the existing build fixture). Test both within (ok) and escape (`/etc/passwd` → err).
- `validate_url(url)` — must start with `https://`, else `SecurityViolation`.
- `SECURITY.md` — security audit checklist: no hardcoded credentials, path traversal prevention, HTTPS-only URLs, configurable AI endpoints.
- Tests: within-project ok, escape err, https ok, http rejected (T34).

### P10.1 Getting started guide (`docs/getting-started.md`)

- Content per plan §10.1: prerequisites (Linux, Rust), install, `mf new`, add VRM, configure mate.toml, `mf dev`, `mf build`/`mf package` (T35).

### P10.2 CI/CD + CHANGELOG (`.github/workflows/`, `CHANGELOG.md`)

- Extend `.github/workflows/ci.yml` (exists: fmt/clippy/test) with an `integration-tests` job: `mf new`, `doctor`, `runtime status`, `build`, `capabilities` on a temp project (T36).
- Add `.github/workflows/release.yml` — tag-triggered (`v*`), `cargo build --release -p mf`, archive, softprops/action-gh-release with CHANGELOG reference.
- `CHANGELOG.md` — keep-a-changelog style, unreleased section summarizing 0.1.0 work to date.

### Cross-cutting

- All Rust tasks: `cargo test --workspace` must pass (baseline 36 + new; the flaky `process::tests::wait_returns_exit_code` ETXTBSY race is PRE-EXISTING and passes in isolation).
- Format: `cargo fmt`; clippy clean.
- Unity task: headless EditMode Test Runner (omit `-quit`), all `Mate.*` incl. `HotReloadHandlerTests` pass; the 34 vendored UniGLTF/VRM failures are PRE-EXISTING.

## [S3] Out of Scope

- Actual network download of runtime tarballs (no GitHub release exists yet; `install_runtime` returns the cache dir and the CLI keeps its "releases not yet published" message). `reqwest`/`tar` crates are NOT added (plan lists them; the plan's own snippet never downloads).
- Hot reload of C# code (ADR-013 — config/assets only).
- Windows/macOS packaging, editor plugin packaging, or signed artifacts.
- Native window/tray backends (Phase 5 deferred platform layer) — untouched.
- Publishing the v1.0 GitHub release itself (requires the repo/credentials; workflows + guide are the deliverable).
- Fixing the PRE-EXISTING flaky `wait_returns_exit_code` test (passes in isolation; unrelated to this phase's changes).

## Tasks

- [x] T27: P7.1 RuntimeVersion/InstallStatus/install/remove + CLI install arg — acceptance: `cargo test -p mf-core -- runtime` + new tests pass, `mf runtime install 1.0.0` reports install guidance (covers: S2-P7.1)
- [x] T28: P7.2 BuildManifest — acceptance: `cargo test -p mf-core -- build_manifest` passes (covers: S2-P7.2)
- [x] T29: P7.3 package_project + scan_assets + CLI wiring — acceptance: `cargo test -p mf-core -- package` passes, `mf package` writes build-manifest.json (covers: S2-P7.3; depends: T27, T28)
- [x] T30: P8.1 HotReloadHandler + tests (Unity) — acceptance: headless EditMode run shows HotReloadHandlerTests 4 pass (covers: S2-P8.1)
- [x] T31: P8.2 error variants — acceptance: `cargo test -p mf-core -- error` passes (covers: S2-P8.2)
- [x] T32: P8.3 doctor runtime/display/permissions + run_doctor/run_doctor_json — acceptance: `cargo test -p mf-cli -- doctor` passes (covers: S2-P8.3; depends: T31)
- [x] T33: P9.1 criterion benches — acceptance: `cargo bench -p mf-cli -- --test` runs green (covers: S2-P9.1)
- [x] T34: P9.2 security.rs + SECURITY.md — acceptance: `cargo test -p mf-core -- security` passes, SECURITY.md committed (covers: S2-P9.2; depends: T31)
- [x] T35: P10.1 getting-started.md — acceptance: file exists with install/create/config/run/build sections (covers: S2-P10.1)
- [x] T36: P10.2 ci.yml integration job + release.yml + CHANGELOG.md — acceptance: workflows valid YAML, CHANGELOG present (covers: S2-P10.2)
