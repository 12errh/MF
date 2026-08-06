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
│  MateBootstrap (composition root) · MateContext           │
│  IEventBus · IConfiguration · services · HotReload        │
│  Character · Audio · System · AI · Mods                   │
├─────────────────────────────────────────────────────────┤
│                Mate.Core (.NET)                          │
│  Result pattern · event bus · service container          │
└─────────────────────────────────────────────────────────┘
```

### Status

> ⚠️ **Early development (v0.1.0).** The CLI, core, all four feature modules,
> and the Unity bootstrap (composition root + entry scene) are implemented and
> tested. A `v1.0.0` GitHub release carries the Unity runtime player, so
> `mf runtime install 1.0.0` downloads it and `mf dev` runs end-to-end: the
> character renders on a transparent window, always-on-top, animated, and named
> after your project. Native window backends are implemented for X11.

| Area | Status |
|------|--------|
| CLI (`mf new/doctor/dev/build/package/runtime/capabilities`) | ✅ Implemented & tested |
| Unity feature modules (Character, Audio, System, AI, Mods) | ✅ Implemented & tested |
| Unity bootstrap (composition root + entry scene) | ✅ Implemented & tested |
| Hot reload (config/assets) | ✅ Implemented |
| Error messages & `mf doctor` diagnostics | ✅ Implemented |
| Security validators (`validate_path`, `validate_url`) | ✅ Implemented |
| CI/CD (GitHub Actions) | ✅ Configured |
| Runtime download from GitHub Releases | ✅ Implemented (`mf runtime install`) |
| End-to-end `mf dev` (player + model load) | ✅ Verified |
| Native X11 window backend | ✅ Implemented (transparent window, always-on-top, borderless, click-through, project name title, idle animation) |
| Mouse tracking (character follows cursor) | ✅ Wired & working |
| Audio-reactive dancing | ✅ Wired & working |
| System tray & notifications | ✅ Wired & working (AppIndicator + notify-send) |
| Mods (`mods/<name>/mod.toml` discovery) | ✅ Wired & working |
| AI chat (Ollama) | ⚠️ Service-layer only — no chat GUI yet (planned next) |
| Hyprland / KWin (Wayland) backends | ⏳ Deferred |
| Windows / macOS support | ⏳ Planned (v2.0+) |

> **"Wired & working"** means the feature actually runs in the player (`mf dev`).
> **"Service-layer only"** means the service is implemented and unit-tested but
> has no interactive path in the player yet.

---

## ✨ Features

- **🖥️ Desktop companion** — transparent, always-on-top, borderless character
  window (X11 backend); the window is named after your project
- **🚶 Idle animation** — loaded characters break out of their T-pose and play
  a humanoid idle loop
- **👀 Mouse tracking** — the character's head and spine turn toward your cursor
  (sensitivity and max angles configurable in `mate.toml`)
- **💃 Audio-reactive dancing** — dances when an allowed app plays music
  (PulseAudio). The dance clip is configurable via `[animation] dance_animation`
  — supply your own clip or use the built-in default
- **💬 AI chat** — service layer (Ollama) implemented & tested; an interactive
  chat GUI is planned next (not yet wired into the player)
- **🪟 System tray & notifications** — tray icon (AppIndicator) and desktop
  notifications (notify-send), config-driven via `[system]`
- **🧩 Mod support** — drop-in mods with `mod.toml` manifests; v1 mods are
  config/asset overrides (no code execution)
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

# 4. Install the runtime (downloads the player release)
mf runtime install 1.0.0

# 5. Diagnose your setup
mf doctor

# 6. Run
mf dev
```

Full walkthrough: **[docs/getting-started.md](docs/getting-started.md)**

> `mf runtime install <version>` downloads the player binary from the GitHub
> release into `~/.mate-framework/runtimes/<version>/`. `mf dev` launches it
> from that cache with `--projectPath <dir>`.

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
| `mf runtime install <v>` | Download & install a runtime version from GitHub Releases |
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
│       │   ├── Bootstrap/  # Composition root: MateBootstrap, BootstrapComposer
│       │   ├── Character/  # CharacterService, MouseTracker, CharacterAnimator
│       │   ├── Audio/      # PulseAudioService, AudioReactiveBridge
│       │   ├── System/     # SystemTrayService
│       │   ├── AI/         # OllamaProvider, PersonalityService
│       │   ├── Mods/       # ModService
│       │   ├── Core/       # Copied Mate.Core + HotReloadHandler
│       │   ├── Editor/     # Scene builder tool
│       │   └── Scenes/     # Entry scene (Camera + MateBootstrap)
│       └── Grabbed/        # Vendored reference scripts + UniVRM packages
├── docs/                   # PRD, TRD, ADRs, plans, specs, getting-started
├── scripts/                # build-player.sh (reproducible Unity player build)
├── .github/workflows/      # CI + release pipelines
└── refrence/               # (gitignored) original reference engine
```

---

## 🧪 Testing

```bash
# Rust CLI + core (79 tests)
cargo test --workspace

# Formatting + lint
cargo fmt --all -- --check
cargo clippy --workspace --all-targets -- -D warnings

# Performance benchmarks (criterion)
cargo bench -p mf --bench cli_benchmarks

# .NET core (33 tests)
cd runtime && dotnet test Mate.Core.sln

# Unity EditMode tests (146 Mate.* tests pass; 340 total in suite)
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
