# Module Boundaries: Mate Framework

## Proposed Module Architecture

Based on analysis of the existing codebase, here are the natural module boundaries:

### Module: Core
**Purpose:** Application lifecycle, service container, configuration, logging
**Current classes:** SaveLoadHandler, FPSLimiter, GCCollect, MemoryTrim
**Dependencies:** None (foundation)
**Public API:** MateContext, IConfiguration, ILogger
**Can be optional:** No (required)
**Estimated complexity:** M
**Migration difficulty:** S

### Module: Window
**Purpose:** Window management, transparency, positioning, input routing
**Current classes:** WindowManager, IWindowManagerImplementation, HyprlandManager, KWinManager, UniWinCore, UniWindowController, EarlyEnvSet, GtkX11Helper
**Dependencies:** Core
**Public API:** IWindowService, IMonitorService
**Can be optional:** No (required for desktop character)
**Estimated complexity:** L
**Migration difficulty:** L (2618-line monolith to decompose)

### Module: Character
**Purpose:** VRM loading, avatar management, component injection
**Current classes:** VRMLoader, AvatarLibraryMenu, AvatarAnimatorController, AvatarMouseTracking, AvatarWindowHandler, AvatarBigScreenHandler, AvatarDragSoundHandler, AvatarGravityController, AvatarSwayController, AvatarHideHandler, AvatarBubbleHandler, AvatarRebindHandler, AvatarSleepController, AvatarTaskbarController, AvatarFoodController, AvatarRandomMessages, AvatarParticleHandler, AvatarStateObjector, AvatarScaleController, AvatarBigScreenTimer, AvatarBigScreenToggleHandler, AvatarBigScreenTouchHandler, AvatarDanceShapeConverter, AvatarDanceSync, AvatarSyncDanceTools, AvatarDancePlayerTools, AvatarDancePlayerUtils, AvatarDanceSafetyZone, AvatarAnimatorReceiver, AvatarRebindHandler, AvatarSyncDanceTools, AvatarWindowHandler, ChibiToggle, FixedPosition, HandHolder, IKFix, AccessoiresHandler, UniversalBlendshapes, MEVoicePack
**Dependencies:** Core, Window, Audio, Animation
**Public API:** ICharacterService, ICharacterLoader
**Can be optional:** No (core feature)
**Estimated complexity:** XL
**Migration difficulty:** XL (30+ tightly coupled MonoBehaviours)

### Module: Audio
**Purpose:** Audio monitoring, sound playback, music-reactive behavior
**Current classes:** PulseAudioManager, AvatarDragSoundHandler, PetVoiceReactionHandler, MEVoicePack
**Dependencies:** Core
**Public API:** IAudioService, IAudioMonitor
**Can be optional:** Yes (can run without audio monitoring)
**Estimated complexity:** M
**Migration difficulty:** M

### Module: AI
**Purpose:** LLM integration, chat, memory, prompts
**Current classes:** AISystemPromptBinder, LLMUnity/*, ollama-unity/*
**Dependencies:** Core
**Public API:** IAIService, IAgentService
**Can be optional:** Yes (AI is optional feature)
**Estimated complexity:** L
**Migration difficulty:** L (need to abstract LLMUnity/Ollama)

### Module: Animation
**Purpose:** Animation state machine, dance system, custom animations
**Current classes:** AvatarDancePlayer, AvatarDanceShapeConverter, AvatarDanceSync, AvatarSyncDanceTools, AvatarDancePlayerTools, AvatarDancePlayerUtils, AvatarDanceSafetyZone, BlendTreeLooper, BlendshapeManager, BlendshapeUIBlock
**Dependencies:** Core, Character
**Public API:** IAnimationService
**Can be optional:** Yes
**Estimated complexity:** M
**Migration difficulty:** M

### Module: System
**Purpose:** System tray, notifications, desktop integration
**Current classes:** TrayIndicator, DBusNotificationHelper, SystemTray, RemoveTaskbarApp, SystemStartHandler, LinuxSpecificSettings
**Dependencies:** Core, Window
**Public API:** ITrayService, INotificationService
**Can be optional:** Yes
**Estimated complexity:** M
**Migration difficulty:** M

### Module: Discord
**Purpose:** Discord Rich Presence
**Current classes:** DiscordPresence
**Dependencies:** Core
**Public API:** IDiscordService
**Can be optional:** Yes
**Estimated complexity:** S
**Migration difficulty:** S

### Module: Mods
**Purpose:** Mod loading, streaming assets management
**Current classes:** MEModLoader, MEModHandler, MEModInitializer (Editor)
**Dependencies:** Core, Character
**Public API:** IModService
**Can be optional:** Yes
**Estimated complexity:** S
**Migration difficulty:** S

### Module: Settings
**Purpose:** Settings UI, menu system, configuration UI
**Current classes:** MenuActions, AvatarSettingsMenu, SettingsHandler*, TutorialMenu, ThemeManager, LanguageDropdownHandler, KeyBindHandler, SettingsMenuPosition
**Dependencies:** Core
**Public API:** ISettingsUIService
**Can be optional:** Yes
**Estimated complexity:** M
**Migration difficulty:** M

## Circular Dependency Analysis

### Current Circular Risks
1. **Character -> Window** (AvatarMouseTracking uses WindowManager.Instance) — Acceptable, Window is lower-level
2. **Character -> Audio** (AvatarAnimatorController uses PulseAudioManager) — Acceptable
3. **Character -> Settings** (All avatar handlers read SaveLoadHandler) — Should be inverted via events

### Proposed Dependency Direction
```
Core (no deps)
  -> Window
  -> Audio
  -> System (tray, notifications)
  -> Discord
  -> Mods
  -> AI
  -> Settings UI
  -> Animation
  -> Character (depends on Core, Window, Audio, Animation)
```

## Module Communication Patterns

### Current (Tightly Coupled)
- Direct singleton access: `SaveLoadHandler.Instance.data.xxx`
- Direct singleton access: `WindowManager.Instance.GetMousePosition()`
- Static method calls: `MenuActions.IsMovementBlocked()`
- FindFirstObjectByType: `FindFirstObjectByType<AvatarAnimatorController>()`

### Proposed (Decoupled)
- Event bus for cross-module communication
- Service interfaces for module APIs
- Configuration sections per module
- Dependency injection for testability
