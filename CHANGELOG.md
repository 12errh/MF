# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
