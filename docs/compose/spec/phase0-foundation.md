---
feature: phase0-foundation
status: delivered
updated: 2026-08-05
branch: phase0-foundation
commits: fd46d34..2094484
---

# Phase 0: Foundation

## Report

**What was built** — A Rust workspace foundation for the Mate Framework with two crates: `mf-core` (domain logic) and `mf-cli` (the `mf` binary). `mf-core` provides the `MfError` error enum (thiserror), a full `MateManifest` schema with `parse_manifest` / `validate_manifest` / `default_manifest` covering ten config sections with serde defaults, and a `runtime` module for cache-dir/path resolution, semver-ordered installed-version listing, and `resolve_version`. `mf-cli` implements `mf new` (project scaffolding with a validated project name), `mf doctor` (manifest + asset validation), and a `dev` stub, all with a global `--json` flag via clap. A GitHub Actions CI pipeline runs fmt, clippy (`-D warnings`), and the workspace test suite.

**Verification** — `cargo build --workspace` PASS; `cargo test --workspace` PASS (35 tests: 26 mf-core + 6 mf-cli + 3 integration); `cargo clippy --workspace --all-targets -- -D warnings` PASS; `cargo fmt --all -- --check` PASS. End-to-end CLI verified: `mf new test-project` creates a valid structure, `mf doctor` validates it, `mf --help` lists all commands. Runtime version resolution verified by test (semver ordering: 1.10.0 > 1.9.0).

**Journey log** —
- The plan's stated test counts were inaccurate: the manifest module contains 15 tests, not 18, so the true core total is 26, not 27+.
- `resolve_version` initially used lexicographic sort (plan code), which mis-orders multi-digit versions (1.10.0 < 1.9.0). Fixed with the `semver` crate; regression test added.
- Runtime tests initially read the real `$HOME/.mate-framework/runtimes`, making them non-hermetic. Refactored to injectable cache-dir functions (`*_in`) so tests use `tempfile` and never touch the real cache.
- Integration tests must live directly under `crates/mf-cli/tests/` (not a subdirectory) for cargo to auto-discover them; `CARGO_BIN_EXE_mf` and `mf_core` are both available there.
- `mf new` accepted path-traversal names (`../x`, `a/b`); added `validate_project_name` rejecting empty, `.`, `..`, and `/`/`\`.

## [S1] Problem

The Mate Framework has no implementation. The repository contains only planning documents and a 2.4GB Unity reference codebase (`refrence/`). No CLI feature (Phase 1+) can be built until a solid Rust foundation exists: a Cargo workspace, a domain crate with error types and a project-manifest schema, an `mf` CLI skeleton, a diagnostic command, a runtime-version resolver, and a CI pipeline.

## [S2] Design

- Rust workspace at repo root with two crates: `crates/mf-core` (domain logic) and `crates/mf-cli` (binary named `mf`).
- Rust edition 2024, MSRV 1.85.
- `mf-core` exposes:
  - `MfError` (via `thiserror`) with variants for manifest-not-found, invalid-manifest, runtime-not-installed, Unity-crash, I/O, and template errors.
  - `MateManifest` + `parse_manifest` / `validate_manifest` / `default_manifest` (serde + toml). Sections: `project`, `character`, `window`, `audio`, `animation`, `ai`, `discord`, `system`, `mods`, `performance`, each with serde defaults.
  - `runtime` module: cache-dir/path resolution, installed-version listing, and `resolve_version`.
- `mf-cli` uses clap derive with subcommands `new`, `doctor`, `dev` (dev is a Phase-1 stub) and a global `--json` flag.
- All public types derive `Debug`, `Clone`, `Serialize`, `Deserialize`. Fallible functions return `Result<T, MfError>`. No `unwrap()` in library code.
- CI: GitHub Actions — `fmt --check`, `clippy -D warnings`, `cargo test --workspace` on push/PR to `main`.
- Tests: 26 unit tests in `mf-core` (3 error + 15 manifest + 8 runtime), 6 unit tests in `mf-cli` (4 doctor + 2 new), 3 integration tests for `mf new` / `doctor`.

## [S3] Out of Scope

- Phase 1+ features: real `mf dev` (process management, file watching, auto-restart), `mf build`, `mf package`, `mf runtime install`, `mf capabilities`.
- Unity runtime integration and the C# codebase.
- Committing the 2.4GB `refrence/` directory.

## Tasks

- [x] T1: Cargo workspace + `MfError` type — acceptance: `cargo test -p mf-core` passes 3 error tests (covers: S2)
- [x] T2: Manifest schema with parse/validate/roundtrip — acceptance: `cargo test -p mf-core` passes 15 manifest tests (covers: S2; depends: T1)
- [x] T3: CLI skeleton (`new`, `doctor`, `dev`, `--help`, `--json`) + integration tests — acceptance: `cargo run -- --help` lists commands; `cargo test --workspace` passes 3 integration tests (covers: S2; depends: T2)
- [x] T4: `mf doctor` full manifest + asset validation — acceptance: `cargo test -p mf` passes 4 doctor tests (covers: S2; depends: T3)
- [x] T5: CI pipeline — acceptance: `.github/workflows/ci.yml` exists; `fmt`/`clippy`/`test` pass locally (covers: S2; depends: T3)
- [x] T6: Runtime manager with version resolution — acceptance: `cargo test -p mf-core` passes 8 runtime tests (covers: S2; depends: T2)