# ADR-010: Mod System Design

## Status
Accepted

## Context
The current MEModLoader loads sounds from StreamingAssets/Mods/ folder. It supports chibi sounds, drag sounds, hover reactions, and AssetBundle loading for .me files. This is a simple but working system.

## Decision
Keep mod system simple in v1.0, expand in v2.0.

## v1.0 Design
- Mods live in project `mods/` directory
- Each mod is a folder with `mod.toml` manifest
- Mods can contain: sounds, animations, textures
- `MEModLoader` pattern preserved (folder conventions)

```
mods/
  custom-sounds/
    mod.toml
    sounds/
      drag.wav
      hover.wav
  custom-dance/
    mod.toml
    animations/
      custom_dance.anim
```

```toml
# mods/custom-sounds/mod.toml
[mod]
name = "Custom Sounds"
version = "1.0.0"
author = "Developer"
description = "Custom sound effects"

[sounds]
drag = "sounds/drag.wav"
hover = "sounds/hover.wav"
```

## v2.0 Design (Future)
- Plugin system with C# assemblies
- Plugin API (Mate.Plugins namespace)
- Plugin manifest with dependencies
- Plugin sandboxing (restricted file/network access)
- Hot-reload support for plugins

## Consequences
- v1.0 mod system is folder-convention-based (same as current)
- No C# plugin loading in v1.0
- Mods are portable (text files + assets)
- `mf add mod <name>` enables a mod in mate.toml
