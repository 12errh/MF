<div align="center">

# 🎭 Mate Framework

**Turn a VRM avatar into a desktop companion.**

A framework for building desktop-mate / AI-agent applications that live on your
Linux desktop — a transparent character that follows your mouse, dances to your
music, and chats with you through a local AI backend. No Unity experience
required.

[![License: MIT OR Apache-2.0](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue.svg)](LICENSE-MIT)
[![Rust](https://img.shields.io/badge/rust-1.85%2B-orange.svg)](https://rustup.rs)
[![Unity](https://img.shields.io/badge/unity-6000.2.6f2-darkgrey.svg)](https://unity.com)

</div>

---

## What is Mate Framework?

Mate Framework gives developers a **CLI (`mf`)** and a **config file
(`mate.toml`)** to create desktop companion applications, without touching
Unity internals or platform-specific desktop APIs. The heavy lifting —
window rendering, VRM model loading, animation, audio reactivity, and AI
chat — is handled by the framework's Unity runtime.

```
┌─────────────────────────────────────────────────────────┐
│                    mf CLI (Rust)                         │
│  new · dev · build · package · doctor · runtime · capa   │
├─────────────────────────────────────────────────────────┤
│                  Mate Runtime (Unity)                    │
│  MateContext · IEventBus · services · IConfiguration     │
│  Character · Audio · System · AI · Mods · HotReload      │
├─────────────────────────────────────────────────────────┤
│                Mate.Core (.NET)                          │
│  Result pattern · event bus · service container          │
└─────────────────────────────────────────────────────────┘
```

### Status

> ⚠️ **Early development (v0.1.0).** The CLI, core, and all four feature
> modules are implemented and tested. The **runtime binary download is
> staged but not yet published** — a GitHub release with the Unity runtime
> is the remaining piece before end-to-end `mf dev` works out of the box.

| Area | Status |
|------|--------|
| CLI (`mf new/doctor/dev/build/package/runtime/capabilities`) | ✅ Implemented & tested |
| Unity feature modules (Character, Audio, System, AI, Mods) | ✅ Implemented & tested |
| Hot reload (config/assets) | ✅ Implemented |
| Error messages & `mf doctor` diagnostics | ✅ Implemented |
| Security validators (`validate_path`, `validate_url`) | ✅ Implemented |
| CI/CD (GitHub Actions) | ✅ Configured |
| Runtime download from GitHub Releases | 🚧 Staged — pending first release |
| Native window backends (X11/Hyprland/KWin) | ⏳ Deferred |
| Windows / macOS support | ⏳ Planned (v2.0+) |

---

## ✨ Features

- **🖥️ Desktop companion** — transparent, always-on-top character window
- **👀 Mouse tracking** — the character follows your cursor
- **💃 Audio-reactive dancing** — dances when monitored apps play music
  (PulseAudio)
- **💬 AI chat** — configurable backend (Ollama), with personality profiles
- **🪟 System tray & notifications** — service layer ready for native backends
- **🧩 Mod support** — drop-in mods with `mod.toml` manifests
- **♻️ Hot reload** — config/assets reload on change; code changes are never
  hot-reloaded by design (ADR-013)
- **🔍 `mf doctor`** — diagnoses manifest, assets, runtime, display server,
  and permissions with actionable guidance
- **📦 Reproducible builds** — `mf build` writes `build-manifest.json`;
  `mf package` creates a self-describing `tar.gz`

---

## 📦 Install

### Prerequisites

- **Linux** (X11 or Wayland)
- **Rust toolchain** — [rustup.rs](https://rustup.rs)
- **Unity 6000.2.6f2** — only needed to *develop* the runtime, not to run
  projects

### Build the CLI

```bash
git clone https://github.com/12errh/MF.git
cd MF
cargo build --release -p mf
# binary at target/release/mf
```

Or install directly:

```bash
cargo install --path crates/mf-cli
```

Verify:

```bash
mf --help
```

---

## 🚀 Quick Start

```bash
# 1. Create a project
mf new my-mate
cd my-mate

# 2. Drop in a VRM model
cp ~/Downloads/avatar.vrm assets/

# 3. Configure mate.toml
#    [character] model = "assets/avatar.vrm"

# 4. Diagnose your setup
mf doctor

# 5. Run
mf dev
```

Full walkthrough: **[docs/getting-started.md](docs/getting-started.md)**

> ⚠️ `mf dev` launches the Unity runtime from the local runtime cache
> (`~/.mate-framework/runtimes/`). Until the first runtime release is
> published, place the player binary at the path `mf runtime status`
> reports, or run the `unity/` project directly from the Unity Editor.

---

## 🧭 CLI Reference

| Command | Description |
|---------|-------------|
| `mf new <name>` | Scaffold a new project (manifest + assets/mods/config dirs) |
| `mf doctor` | Check manifest, assets, runtime, display server, permissions |
| `mf dev` | Run the project with file watching + auto-restart |
| `mf build` | Copy assets + manifest into `build/`, write `build-manifest.json` |
| `mf package` | Create `<name>.tar.gz` with the build manifest inside |
| `mf runtime list` | List installed runtime versions |
| `mf runtime status` | Show cache location and installed versions |
| `mf runtime install <v>` | Validate/stage a runtime version |
| `mf capabilities` | Report what your desktop session supports |

Every command supports `--json` for machine-readable output.

---

## 🗂️ Repository Layout

```
├── crates/
│   ├── mf-core/            # Library: manifest, build, runtime, errors, security, watcher
│   └── mf-cli/             # The `mf` binary (clap CLI)
├── runtime/
│   └── Mate.Core/          # .NET core: event bus, MateContext, Result pattern
│                          #   (canonical source; copied into the Unity project)
├── unity/
│   └── Assets/
│       ├── MateFramework/  # Unity runtime: services, interfaces, tests
│       │   ├── Character/  # CharacterService, MouseTracker, CharacterAnimator
│       │   ├── Audio/      # PulseAudioService, AudioReactiveBridge
│       │   ├── System/     # SystemTrayService
│       │   ├── AI/         # OllamaProvider, PersonalityService
│       │   ├── Mods/       # ModService
│       │   └── Core/       # Copied Mate.Core + HotReloadHandler
│       └── Grabbed/        # Vendored reference scripts + UniVRM packages
├── docs/                   # PRD, TRD, ADRs, plans, specs, getting-started
├── .github/workflows/      # CI + release pipelines
└── refrence/               # (gitignored) original reference engine
```

---

## 🧪 Testing

```bash
# Rust CLI + core (78 tests)
cargo test --workspace

# Formatting + lint
cargo fmt --all -- --check
cargo clippy --workspace --all-targets -- -D warnings

# Performance benchmarks (criterion)
cargo bench -p mf --bench cli_benchmarks

# .NET core (33 tests)
cd runtime && dotnet test Mate.Core.sln

# Unity EditMode tests (84 Mate.* tests pass; 320 total in suite)
#   via Unity Test Runner, or headless:
#   <Unity 6000.2.6f2> -batchmode -nographics -projectPath unity \
#     -runTests -testPlatform EditMode -testResults results.xml
```

> The vendored UniGLTF/VRM/VRM10 packages ship their own test assemblies;
> ~34 of their tests do not run headless. These failures are **pre-existing
> in the vendored packages**, not in Mate Framework code.

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Step-by-step first project |
| [PRD](docs/PRD.md) | Product requirements & vision |
| [TRD](docs/TRD.md) | Technical requirements & system architecture |
| [Architecture Index](docs/INDEX.md) | Codebase analysis, ADRs, module boundaries |
| [Implementation Plan](docs/IMPLEMENTATION_PLAN.md) | 10-phase roadmap |
| [Risk Register](docs/RISK_REGISTER.md) | Risks & mitigations |
| [Security](SECURITY.md) | Security audit checklist |
| [Changelog](CHANGELOG.md) | Release history |

---

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines. This project is in
early development — bug reports, feature ideas, and pull requests are welcome.

---

## 📄 License

Dual-licensed under **MIT OR Apache-2.0** — see [LICENSE-MIT](LICENSE-MIT)
and [LICENSE-APACHE](LICENSE-APACHE) for details.
