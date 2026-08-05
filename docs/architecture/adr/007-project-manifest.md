# ADR-007: Project Manifest Format

## Status
Accepted

## Context
The framework needs a project manifest to describe what features a project uses, where assets are, and how to configure the runtime.

## Decision
Use TOML as the project manifest format (`mate.toml`).

## Rationale
1. **Human-readable** — TOML is cleaner than JSON for configuration
2. **Rust-native** — `mf` CLI (Rust) has first-class TOML support via `toml` crate
3. **Typed sections** — TOML tables map naturally to feature configurations
4. **Comment-friendly** — TOML supports comments, JSON does not
5. **No schema complexity** — Simple enough for v1, extensible for v2

## Schema

```toml
[project]
name = "my-mate"
version = "0.1.0"
runtime = "1.0.0"
author = "Developer Name"
description = "My desktop mate"

[character]
model = "assets/avatar.vrm"          # Path to VRM file
scale = 1.0                           # Character scale
fallback_model = ""                   # Fallback if model fails

[window]
transparent = true
always_on_top = true
click_through = false
hide_from_taskbar = false
window_type = "normal"               # normal, dock, desktop
initial_position = "center"          # center, cursor, x,y

[audio]
enabled = true
threshold = 0.2
allowed_apps = ["firefox", "spotify", "vlc"]
volume = 1.0

[animation]
idle_count = 10
dance_count = 5
idle_switch_time = 10.0
idle_transition_time = 1.0
dance_switch_time = 15.0
dance_transition_time = 2.0
enable_dancing = true
enable_dance_switch = false

[ai]
enabled = false
provider = "ollama"                  # ollama, llmunity, openai
model = "phi3:mini"
context_length = 4096
prompt_file = "config/personality.toml"
system_prompt = ""

[discord]
enabled = false
app_id = ""
details = ""
state = ""

[system]
tray_icon = "assets/icon.png"
tray_tooltip = "My Mate"
notifications = true
start_with_desktop = false

[mods]
enabled = true
mods_path = "mods/"

[performance]
fps_limit = 90
enable_bloom = false
enable_ambient_occlusion = false
graphics_quality = 1                 # 0=Low, 1=Medium, 2=High
```

## Validation
The `mf` CLI validates the manifest:
- Required fields present
- File paths exist
- Values in valid ranges
- Unknown keys warned (not errored)
- Version compatibility checked

## Consequences
- `mf new` generates a template `mate.toml`
- `mf dev` reads manifest to configure runtime
- `mf build` validates manifest before building
- `mf doctor` checks manifest validity
- Runtime reads manifest at startup
