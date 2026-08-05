# Getting Started with Mate Framework

Mate Framework turns a VRM avatar into a desktop companion: a transparent,
always-on-top window that reacts to your mouse, your music, and your voice —
powered by a configurable AI backend.

## Prerequisites

- Linux (X11 or Wayland)
- Rust toolchain (see [rustup.rs](https://rustup.rs))
- Unity 6000.2.6f2 (only needed to develop the runtime itself, not to run projects)
- Optional: [Ollama](https://ollama.com) for the AI companion

## Install

```bash
cargo install --path crates/mf-cli
```

This builds the `mf` CLI. Verify it works:

```bash
mf --help
```

## Create a Project

```bash
mf new my-mate
cd my-mate
```

This scaffolds:

```
my-mate/
├── mate.toml      - project manifest
├── assets/        - VRM models, textures, sounds
├── mods/          - optional mod assets
└── config/        - personality.toml and other config
```

## Add a VRM Model

```bash
cp ~/Downloads/avatar.vrm my-mate/assets/
```

## Configure

Edit `mate.toml`:

```toml
[project]
name = "my-mate"
runtime = "1.0.0"

[character]
model = "assets/avatar.vrm"

[window]
transparent = true
click_through = true
always_on_top = true
initial_position = "100,200"

[audio]
enabled = true
threshold = 0.5
allowed_apps = ["firefox", "spotify"]

[ai]
enabled = true
provider = "ollama"
model = "phi3:mini"
```

## Check Your Setup

```bash
mf doctor
```

`mf doctor` checks the manifest, assets, runtime install, display server, and
permissions, and tells you what to fix.

## Run

```bash
mf dev
```

`mf dev` starts the Unity runtime, watches your config and assets, and
restarts on change. Hot reload covers config/assets only — not C# code.

## Build & Package

```bash
mf build
# Copies assets + manifest into build/, writes build-manifest.json

mf package
# Creates my-mate.tar.gz with the build manifest inside
```

## Manage Runtimes

```bash
mf runtime list      # installed versions
mf runtime status    # cache location + versions
mf runtime install 1.0.0
```

## Platform Capabilities

```bash
mf capabilities
```

Shows what your desktop session supports (transparency, click-through, tray,
audio monitoring).
