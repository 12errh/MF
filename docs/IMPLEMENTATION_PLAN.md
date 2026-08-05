# Implementation Plan: Mate Framework

## Overview

This plan transforms Mate-Engine-Linux-Port into the Mate Framework through incremental TDD-driven migration over 6 months (26 weeks).

**Prerequisites:**
- Rust toolchain installed
- Unity 6000.2.6f2 installed
- Linux development environment
- License is compatible (copyleft + non-commercial, matching framework license)

**TDD Workflow:** Every task follows: write failing test → verify fail → implement → verify pass → commit

**Detailed Plans:** Each phase has a detailed implementation plan with exact file paths, test code, and implementation code:

| Phase | Weeks | Plan File |
|-------|-------|-----------|
| Phase 0: Foundation | 1-2 | [`docs/compose/plans/2026-08-04-phase0-foundation.md`](compose/plans/2026-08-04-phase0-foundation.md) |
| Phase 1: CLI Core | 3-4 | [`docs/compose/plans/2026-08-04-phase1-cli-core.md`](compose/plans/2026-08-04-phase1-cli-core.md) |
| Phase 2: Runtime Core | 5-8 | [`docs/compose/plans/2026-08-04-phase2-runtime-core.md`](compose/plans/2026-08-04-phase2-runtime-core.md) |
| Phase 3-6: Feature Modules | 9-15 | [`docs/compose/plans/2026-08-04-phase3-6-modules.md`](compose/plans/2026-08-04-phase3-6-modules.md) |
| Phase 7-10: Build, DX, Release | 16-26 | [`docs/compose/plans/2026-08-04-phase7-10-release.md`](compose/plans/2026-08-04-phase7-10-release.md) |

---

## Summary: What Gets Built Per Phase

### Phase 0: Foundation (Weeks 1-2)
**What:** Rust workspace, manifest parser, CLI skeleton, error types, CI pipeline
**Tests:** 27+ tests (manifest parse/validate/roundtrip, error display, runtime resolution)
**Exit:** `cargo run -- new test && cargo run -- doctor` works

### Phase 1: CLI Core (Weeks 3-4)
**What:** `mf dev` (process manager + file watcher + auto-restart), `mf build`, `mf package`, `mf runtime`, `mf capabilities`
**Tests:** 40+ tests (process spawn, file watching, build pipeline, package creation)
**Exit:** `mf dev` launches Unity player, watches for changes, restarts on crash

### Phase 2: Runtime Core (Weeks 5-8)
**What:** C# service container (MateContext), event bus, platform detection, IWindowService adapter over WindowManager.cs, IPlatformCapabilities, FileConfiguration, Result<T> pattern
**Tests:** 24+ Unity Editor tests (DI resolution, event pub/sub, config read/write, adapter wrapping)
**Exit:** All singletons replaced by MateContext, all new code uses service interfaces

### Phase 3: Character Module (Weeks 9-11)
**What:** ICharacterService (wraps VRMLoader), MouseTracker (wraps AvatarMouseTracking), CharacterAnimator (wraps AvatarAnimatorController), event-driven communication
**Tests:** 15+ tests (model loading, mouse tracking config, animation state machine)
**Exit:** VRM loads via service, mouse tracks via IWindowService, dance triggers via IEventBus

### Phase 4: Audio Module (Week 12)
**What:** IAudioService (wraps PulseAudioManager), AudioReactiveBridge (event-driven dance trigger)
**Tests:** 8+ tests (audio monitoring, peak level detection, app filtering, bridge events)
**Exit:** Music-reactive dancing works via event bus, no direct singleton coupling

### Phase 5: System Module (Week 13)
**What:** ISystemService (wraps TrayIndicator + DBusNotificationHelper), notifications
**Tests:** 6+ tests (tray icon, notification sending)
**Exit:** System tray and notifications work through service interfaces

### Phase 6: AI Module (Weeks 14-15)
**What:** IAIService + OllamaProvider (wraps ollama-unity), PersonalityService, IDiscordService, IModService
**Tests:** 12+ tests (Ollama API, personality parsing, Discord presence, mod loading)
**Exit:** AI chat works via provider abstraction, personality configurable via TOML

### Phase 7: Build & Package (Weeks 16-17)
**What:** Runtime download/install, build manifest JSON, package with bundled runtime
**Tests:** 8+ tests (download, extract, manifest generation, archive creation)
**Exit:** `mf build && mf package` creates self-contained runnable archive

### Phase 8: Developer Experience (Weeks 18-20)
**What:** Hot reload integration (C# FileSystemWatcher), comprehensive error messages, `mf doctor --fix`, integration test suite
**Tests:** 10+ tests (hot reload detection, error message formatting, doctor diagnostics)
**Exit:** Config changes hot-reloaded, all errors actionable, full integration test suite

### Phase 9: Hardening (Weeks 21-23)
**What:** Performance baselines, security audit, platform testing matrix, memory profiling
**Tests:** Performance benchmarks (< 500ms build, < 5s startup, < 1% idle CPU)
**Exit:** All benchmarks pass, security audit clean, platform matrix verified

### Phase 10: Documentation & Release (Weeks 24-26)
**What:** Getting started guide, API reference, platform matrix, release pipeline, demo project
**Tests:** CI/CD pipeline, release archive
**Exit:** v1.0 released with docs, binaries, and demo project

---

## Test Count Progression

| Phase | Plan File | Tasks | Tests | Types |
|-------|-----------|-------|-------|-------|
| Phase 0 | phase0-foundation.md | 6 | 27+ | Rust unit (manifest, error, runtime) |
| Phase 1 | phase1-cli-core.md | 5 | 12+ | Rust unit (process, watcher, build) |
| Phase 2 | phase2-runtime-core.md | 6 | 24 | C# Editor (container, events, config, adapter, models) |
| Phase 3 | phase3-6-modules.md | 4 | 21 | C# Editor (character, tracking, animation, integration) |
| Phase 4 | phase3-6-modules.md | 2 | 9 | C# Editor (audio, reactive bridge) |
| Phase 5 | phase3-6-modules.md | 1 | 4 | C# Editor (system tray, notifications) |
| Phase 6 | phase3-6-modules.md | 3 | 10 | C# Editor (AI, personality, mods) |
| Phase 7 | phase7-10-release.md | 3 | 17 | Rust (runtime, build manifest, package) |
| Phase 8 | phase7-10-release.md | 3 | 13 | Rust/C# (hot reload, errors, doctor) |
| Phase 9 | phase7-10-release.md | 2 | 10+ | Rust (benchmarks, security) |
| Phase 10 | phase7-10-release.md | 2 | 4 | CI/CD pipeline + integration |
| **Total** | **5 plan files** | **39** | **164+** | |

---

## Dependency Graph

```
Phase 0 (Foundation) ──> Phase 1 (CLI Core)
                     ──> Phase 2 (Runtime Core)
                          ├──> Phase 3 (Character)
                          │    ├──> Phase 4 (Audio)
                          │    └──> Phase 6 (AI)
                          └──> Phase 5 (System)
                               └──> Phase 7 (Build)
                                    └──> Phase 8 (DX)
                                         └──> Phase 9 (Hardening)
                                              └──> Phase 10 (Release)
```

Phases 3, 4, 5, 6 can run in parallel once Phase 2 is complete.
Phases 0+1 (Rust CLI) and Phase 2 (C# runtime) can run in parallel.

---

## Risk Mitigation

| Risk | Mitigation | Phase |
|------|-----------|-------|
| License incompatibility | Contact author, rewrite if needed | 0 |
| Unity player too large | Strip unused modules, use IL2CPP | 3 |
| P/Invoke bugs in wrapped code | Comprehensive testing, gradual migration | 2-3 |
| Performance regression | Profile every phase, compare baselines | 8-9 |
| Scope creep | Strict phase gates, P0-only for v1.0, no code extensibility in v1 (ADR-013) | All |
