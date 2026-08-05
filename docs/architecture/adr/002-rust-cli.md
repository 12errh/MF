# ADR-002: Rust for mf CLI

## Status
Accepted

## Context
The Mate Framework needs a CLI tool (`mf`) for project creation, development, building, and packaging. The question is what language to implement it in.

## Decision
The `mf` CLI is implemented in Rust.

## Rationale

### Evidence for Rust
1. **Single binary distribution** — `mf` compiles to a single static binary with no runtime dependencies. This is critical for developer onboarding (`curl | sh` install).
2. **Cross-compilation** — Rust compiles to linux-x64, linux-arm64, windows-x64, macos-x64 from a single codebase.
3. **Existing Rust in project** — `Plugins/kdotool-main/` is already a Rust project (KDE Wayland tool). The team has Rust experience.
4. **No Unity dependency** — The CLI manages Unity but doesn't run inside it. Rust is the right tool for system tooling.
5. **Developer experience** — Cargo ecosystem for CLI argument parsing, TOML config, file watching, HTTP downloads.

### Evidence against alternatives
1. **C# (dotnet CLI)** — Requires .NET runtime installation, larger binary, slower startup
2. **Go** — Good single binary, but less ergonomic for system programming
3. **Python** — Requires runtime, slower, packaging complexity
4. **Node.js** — Requires runtime, larger, slower startup

## Consequences
- `mf` is a ~5MB static binary
- No runtime dependencies for the CLI itself
- Cross-platform builds from CI
- Can shell out to Unity batch mode for builds
- Can watch files and manage processes
- Can download and manage runtime versions

## Scope
The Rust CLI handles:
- `mf new` — Project scaffolding
- `mf dev` — Start development server
- `mf build` — Build project
- `mf package` — Package for distribution
- `mf doctor` — Diagnose issues
- `mf capabilities` — Show platform capabilities
- Runtime download and management
- Project manifest parsing (TOML)
- File watching for hot reload
- Process management (start/stop Unity player)

NOT handled by Rust:
- Character rendering (Unity)
- Window management (existing C# P/Invoke)
- Animation (Unity Animator)
- Audio monitoring (existing C# PulseAudio)
