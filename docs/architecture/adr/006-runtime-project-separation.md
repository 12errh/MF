# ADR-006: Runtime/Project Separation

## Status
Accepted

## Context
Currently, everything (runtime code, developer content, settings, assets) lives in one Unity project. The framework needs to separate what the framework provides from what the developer provides.

## Decision
Strict separation between Mate Runtime and Mate Project.

## Design

### Mate Runtime (Framework Team Maintains)
```
MateRuntime/
  Mate.Core.dll              (lifecycle, config, events)
  Mate.Window.dll            (window management)
  Mate.Character.dll         (VRM, avatar)
  Mate.Audio.dll             (audio monitoring)
  Mate.Animation.dll         (animation system)
  Mate.AI.dll                (LLM integration)
  Mate.System.dll            (tray, notifications)
  Mate.Discord.dll           (Discord RPC)
  Mate.Mods.dll              (mod loading)
  Mate.Platform.LinuxX11.dll (X11 backend)
  Mate.Platform.LinuxHyprland.dll (Hyprland backend)
  Mate.Platform.LinuxKWin.dll (KWin backend)
  UnityPlayer               (prebuilt Unity player)
  Data/                     (Unity data files)
  Plugins/                  (native libraries)
```

### Mate Project (Developer Creates)
```
my-mate/
  mate.toml                 (project manifest)
  src/                      (developer scripts, optional)
  assets/
    avatar.vrm              (character model)
    animations/             (custom animations)
    sounds/                 (sound effects)
    textures/               (custom textures)
  config/
    ai.toml                 (AI configuration)
    personality.toml        (character personality)
    audio.toml              (audio settings)
  plugins/                  (optional extensions)
  mods/                     (user mods)
```

### Runtime Loading Flow
```
mf dev
  -> mf reads mate.toml
  -> mf starts Unity player with:
     --project-path ./my-mate
     --mate-runtime ./MateRuntime/
  -> Unity player loads Mate.Runtime assembly
  -> Runtime reads mate.toml
  -> Runtime loads assets from project
  -> Runtime starts services
```

## Rationale
1. **Developer simplicity** — Developers only touch project files
2. **Framework control** — Runtime updates don't break projects
3. **Clean boundaries** — No framework code in project, no project code in runtime
4. **Versioning** — Runtime version in mate.toml, framework can update independently
5. **Distribution** — Runtime bundled, project files are portable

## Consequences
- Runtime is a prebuilt binary (~100MB)
- Project files are text-based (TOML, JSON, VRM, animations)
- Hot reload works for config and assets, not for C# code
- v1 has no code extensibility — config + asset mods only (see ADR-013)
- Code-level extensibility deferred to v2 plugin system
- Build process packages raw assets with runtime (no AssetBundle compilation needed)
