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

[animation]
dance_animation = "MateDance"   # the dance clip name (default; use your own)
idle_switch_time = 30
dance_switch_time = 15

[system]
tray_icon = "assets/icon.png"    # optional tray icon
tray_tooltip = "My Mate"
notifications = true

[mods]
mods_path = "mods/"
```

## Check Your Setup

```bash
mf doctor
```

`mf doctor` checks the manifest, assets, runtime install, display server, and
permissions, and tells you what to fix.

## Run

```bash
# Install the runtime player (downloads from GitHub Releases on first use)
mf runtime install 1.0.0

mf dev
```

`mf dev` starts the Unity runtime, watches your config and assets, and
restarts on change. Hot reload covers config/assets only — not C# code.

> The first `mf dev` needs a runtime installed. `mf runtime install <version>`
> downloads the player binary into `~/.mate-framework/runtimes/<version>/` and
> `mf dev` launches it with `--projectPath <dir>`.

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
mf runtime install 1.0.0   # download & install a version from GitHub Releases
```

## Platform Capabilities

```bash
mf capabilities
```

Shows what your desktop session supports (transparency, click-through, tray,
audio monitoring).

## Features & How to Configure Them

These features run in the player (`mf dev`) out of the box. Configure each via
`mate.toml`.

### Mouse tracking

The character's head and spine turn toward your cursor. Tune sensitivity and
the max rotation angles:

```toml
[character]
model = "assets/avatar.vrm"
head_sensitivity = 1.0   # how fast the head reacts
eye_sensitivity = 1.0
spine_sensitivity = 0.5
head_max_angle = 20      # degrees the head can turn
spine_max_angle = 10
```

### Audio-reactive dancing

When a monitored app (in `[audio] allowed_apps`) plays sound above
`threshold`, the character dances. The dance clip is **not hardcoded** —
`[animation] dance_animation` selects it (default `MateDance`, built into the
runtime). To use your own clip, drop it in the runtime's `Resources` folder and
set the name:

```toml
[animation]
dance_animation = "MyDanceClip"
```

### System tray & notifications

The player shows a tray icon on start (AppIndicator) and can send desktop
notifications (via `notify-send`). Configure the icon and tooltip:

```toml
[system]
tray_icon = "assets/icon.png"   # absolute or project-relative path
tray_tooltip = "My Mate"
```

### Mods

Drop a mod into `mods/` with a `mod.toml` manifest:

```
my-mate/mods/my-mod/mod.toml
```

```toml
name = "my-mod"
version = "1.0.0"
description = "Custom asset overrides"
```

v1 mods are **config + asset overrides only** — they are discovered and exposed,
but no code is executed (ADR-013). Code-extensible mods are planned for v2.

### AI chat (service layer only)

The Ollama provider is implemented and unit-tested, but v1 has **no chat GUI
yet** — there is no interactive way to chat from the player. An interactive
chat/context-menu UI is the planned next feature. Until then, treat AI chat as
service-layer-only.
