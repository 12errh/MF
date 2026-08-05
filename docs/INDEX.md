# Mate Framework — Architecture Document Index

## Document Map

All documents are based on deep analysis of the `refrence/Mate-Engine-Linux-Port` Unity codebase (Unity 6000.2.6f2, 1,357 C# files, 4,429 assets).

---

## Core Analysis

| Document | Path | Description |
|----------|------|-------------|
| Codebase Analysis | `architecture/codebase-analysis.md` | Repository overview, what works, what to preserve/refactor/remove |
| Current Architecture | `architecture/current-architecture.md` | Six-layer architecture, singleton dependency graph, god objects |
| Dependency Map | `architecture/dependency-map.md` | Critical god objects ranked, dependency chains, cross-cutting concerns |
| Platform Analysis | `architecture/platform-analysis.md` | X11/Hyprland/KWin/GNOME support matrix, native library requirements |
| Capability Matrix | `architecture/capability-matrix.md` | Feature classification (portable/platform-specific/capability-dependent), backend requirements |
| Module Boundaries | `architecture/module-boundaries.md` | 10 proposed modules with dependencies, communication patterns |

## Target Architecture

| Document | Path | Description |
|----------|------|-------------|
| Target Architecture | `architecture/target-architecture.md` | Service interfaces, config model, distribution model, developer workflow |

## Architecture Decision Records

| ADR | Title | Decision |
|-----|-------|----------|
| 001 | `architecture/adr/001-unity-runtime.md` | Unity remains runtime engine |
| 002 | `architecture/adr/002-rust-cli.md` | Rust for mf CLI |
| 003 | `architecture/adr/003-native-layer.md` | Keep existing C# P/Invoke, no Rust native layer |
| 004 | `architecture/adr/004-platform-abstraction.md` | Layered platform abstraction with capabilities |
| 005 | `architecture/adr/005-service-architecture.md` | Lightweight service container, eliminate singletons |
| 006 | `architecture/adr/006-runtime-project-separation.md` | Strict runtime vs project separation |
| 007 | `architecture/adr/007-project-manifest.md` | TOML manifest (mate.toml) |
| 008 | `architecture/adr/008-modular-architecture.md` | Optional feature modules |
| 009 | `architecture/adr/009-ai-architecture.md` | Pluggable AI providers |
| 010 | `architecture/adr/010-mod-system.md` | Simple mod system v1, plugin system v2 |
| 011 | `architecture/adr/011-error-handling.md` | Result pattern, graceful degradation |
| 012 | `architecture/adr/012-testing-strategy.md` | Four-level testing strategy |
| 013 | `architecture/adr/013-build-pipeline-and-extensibility.md` | Build pipeline (no Unity Editor) + v1 extensibility model |

## Product & Technical

| Document | Path | Description |
|----------|------|-------------|
| PRD | `PRD.md` | Product requirements, success metrics, feature priorities |
| TRD | `TRD.md` | Technical requirements, system architecture, data models, build system |
| Implementation Plan | `IMPLEMENTATION_PLAN.md` | 10-phase, 26-week migration plan with dependency graph |
| Risk Register | `RISK_REGISTER.md` | 15 risks with probability, impact, severity, mitigation |
| 90-Day Plan | `NINETY_DAY_PLAN.md` | Exact dependency-ordered first 90 days |

## Detailed TDD Implementation Plans

Each phase has a detailed plan with exact file paths, test code, implementation code, and commit messages.

| Plan | Weeks | Description |
|------|-------|-------------|
| Phase 0: Foundation | 1-2 | Cargo workspace, manifest parser, error types, CLI skeleton, CI |
| Phase 1: CLI Core | 3-4 | `mf dev` (process mgmt + file watcher + auto-restart), `mf build`, `mf package`, `mf runtime`, `mf capabilities` |
| Phase 2: Runtime Core | 5-8 | MateContext service container, event bus, platform detection, IWindowService adapter, FileConfiguration, Result<T> |
| Phase 3-6: Feature Modules | 9-15 | Character (VRM + tracking + animation), Audio (PulseAudio + reactive), System (tray + notifications), AI (Ollama + personality + Discord + mods) |
| Phase 7-10: Build, DX, Release | 16-26 | Runtime management, hot reload, error handling, performance hardening, documentation, v1.0 release |

All plan files are in: `docs/compose/plans/`

---

## Key Findings Summary

### God Objects (must decompose)
1. **SaveLoadHandler.Instance.data** — 50+ fields, 30+ consumers. Split into per-module configs.
2. **WindowManager.cs** — 2618 lines, 70+ DllImports. Decompose into X11 imports + window service + input handler.

### Good Abstractions (preserve)
1. **IWindowManagerImplementation** — 25+ methods, platform-agnostic. Expand with capabilities.
2. **HyprlandManager** — Clean Unix socket architecture. Wrap in adapter.
3. **KWinManager** — Clean DBus architecture. Wrap in adapter.

### Critical Blocker
1. **License** — MateEngine Pro License v2.0 is copyleft + non-commercial. Must resolve before any public release.

### Estimated Scope
- **Phase 0-10**, 26 weeks total
- **v1.0 target:** Linux X11, VRM + mouse tracking + idle/dance + AI chat + system tray
- **Minimum viable framework:** `mf new` → `mf dev` → transparent VRM character on desktop
