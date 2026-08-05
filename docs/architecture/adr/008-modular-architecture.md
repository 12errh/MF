# ADR-008: Modular Feature Architecture

## Status
Accepted

## Context
The current application is monolithic — every feature is always loaded. The framework should support optional features to reduce binary size and memory usage.

## Decision
Features are loaded as optional modules based on project manifest.

## Module List

### Core Module (Always Loaded)
- Application lifecycle
- Configuration
- Logging
- Event bus
- Service container

### Window Module (Always Loaded)
- Platform detection
- Window management
- Monitor management
- Input handling

### Character Module (Always Loaded)
- VRM loading
- Avatar management
- Component injection

### Audio Module (Optional: audio.enabled)
- PulseAudio monitoring
- Per-app audio detection
- Music-reactive behavior

### Animation Module (Optional: always loaded if character loaded)
- Idle state machine
- Dance system
- Drag detection
- Blendshape control

### AI Module (Optional: ai.enabled)
- LLM integration (Ollama, LLMUnity)
- Chat system
- Personality prompts
- Memory

### System Module (Optional: system.tray_icon set)
- System tray
- Notifications
- Startup integration

### Discord Module (Optional: discord.enabled)
- Rich Presence

### Mods Module (Optional: mods.enabled)
- Mod loading
- StreamingAssets management

## Loading Pattern
```csharp
// Runtime loads modules based on manifest
if (config.AI.Enabled)
    context.Register<IAIService>(new OllamaService(config.AI));

if (config.Audio.Enabled)
    context.Register<IAudioService>(new PulseAudioService());

if (config.Discord.Enabled)
    context.Register<IDiscordService>(new DiscordService(config.Discord));
```

## Rationale
1. **Reduced footprint** — AI module is ~50MB, only loaded if needed
2. **Faster startup** — Fewer services to initialize
3. **Clearer dependencies** — Each module declares its dependencies
4. **Testable** — Can test modules in isolation
5. **Extensible** — New modules can be added without modifying core

## Consequences
- Modules are compiled as separate assemblies
- Runtime conditionally loads assemblies
- `mf add <module>` enables a module in manifest
- `mf remove <module>` disables a module
- Module availability is checked at startup
