# The 90-Day Plan: Mate Framework

## The Question

> If you were personally responsible for taking this exact repository and turning it into a production-quality Mate Framework used by external developers, what would you do during the first 90 days, in exact dependency order, and why?

## Answer

### Day 1-5: Repository & Tooling Setup

License is compatible — MateEngine Pro License v2.0 (copyleft + non-commercial) matches the framework's own open-source + non-commercial nature.

1. Create `mate-framework/mf` Cargo workspace
2. Create `mate-framework/runtime` Unity project
3. Set up GitHub Actions CI for both
4. Create initial `mate.toml` schema definition
5. Write CONTRIBUTING.md, CODE_OF_CONDUCT.md

**Why now:** Every subsequent task needs these foundations. Parallel work streams (CLI + runtime) need separate repos with independent CI.

### Day 6-15: mf CLI Core (The Developer's First Touch)

The CLI is what developers interact with. Getting `mf new && mf dev` working creates the first end-to-end loop.

**Build order (dependency-driven):**
1. `mf new` — project scaffolding (writes mate.toml, assets/, config/)
2. `mf doctor` — diagnostic tool (validates environment)
3. `mf runtime` — runtime download/management
4. `mf dev` — starts Unity player with project
5. `mf build` — builds distributable
6. `mf package` — creates release archive

**Key decision:** The CLI starts Unity player as a child process with `--project-path` argument. No IPC for v1 — Unity reads `mate.toml` directly at startup.

**Why this order:** `mf new` is the first thing a developer runs. If it doesn't create a working project, nothing else matters. `mf dev` is the feedback loop. `mf build` and `mf package` are needed for any real usage.

### Day 16-35: Runtime Core + X11 Backend (The Hard Part)

This is the most technically challenging phase because it involves decomposing 2618-line WindowManager.cs and 30+ MonoBehaviours.

**Build order:**
1. **MateContext service container** — Replaces all singletons. This is the architectural foundation everything else plugs into.
2. **IWindowService interface** — Typed WindowInfo/MonitorInfo records. Not the implementation yet.
3. **PlatformDetector** — Port EarlyEnvSet.cs XDG detection. This determines which backend to load.
4. **LinuxX11Backend** — Wrap existing WindowManager.cs X11 code in IWindowService. DO NOT rewrite. Adapter pattern.
5. **LinuxHyprlandBackend** — Wrap existing HyprlandManager. Already implements IWindowManagerImplementation.
6. **LinuxKWinBackend** — Wrap existing KWinManager. Already implements IWindowManagerImplementation.

**Why this order:** The service container must exist before anything else. The interface must exist before the implementation. The detection must exist before backend selection. Each backend wraps existing tested code — this is migration, not rewrite.

**Critical constraint:** WindowManager.cs must be decomposed, not rewritten. Extract X11 P/Invoke declarations into a separate file. Extract monitor operations. Extract input handling. Keep the core window management flow intact.

### Day 36-50: Character Module (The User's Payoff)

This is what makes the framework visible and useful — a VRM character on the desktop.

**Build order:**
1. **VRM loading pipeline** — Port VRMLoader.cs. This is 553 lines of tested VRM 0.x + 1.0 loading. Wrap in ICharacterLoader.
2. **Character lifecycle** — Model load/unload, component injection from template prefab. Wrap in CharacterService.
3. **Mouse tracking** — Port AvatarMouseTracking.cs head/spine/eye IK. This is the feature that makes the character feel alive.
4. **Animation state machine** — Port AvatarAnimatorController.cs. Idle states, dance states, transitions.

**Why this order:** You need a model loaded before you can track mouse. You need mouse tracking before you can animate responses. Each layer builds on the previous.

**Critical decision:** The 20+ avatar handler MonoBehaviours (AvatarBigScreenHandler, AvatarFoodController, AvatarGravityController, etc.) are NOT part of v1.0 core. They become optional feature modules. Only three avatar systems are required: VRM loading, mouse tracking, animation state machine.

### Day 51-60: Audio + System Integration (The Polish)

**Build order:**
1. **PulseAudio service** — Port PulseAudioManager.cs P/Invoke. This is what makes dancing work.
2. **Audio-reactive bridge** — Connect audio peaks to animation triggers. Event-based, not polling.
3. **System tray** — Port TrayIndicator.cs. Small but important for desktop applications.
4. **Notifications** — Port DBusNotificationHelper.cs. Wayland warning, error notifications.

**Why this order:** Audio enables dancing, which is the second-most visible feature after mouse tracking. System tray and notifications are small, isolated modules that can be done in parallel.

### Day 61-70: AI + Discord (The Intelligence)

**Build order:**
1. **Ollama provider** — Simple HTTP client to Ollama API. The existing codebase has patterns to follow.
2. **Personality system** — Prompt templating from personality.toml.
3. **Chat UI** — Bubble/chat interface for AI conversation.
4. **Discord Rich Presence** — Port DiscordPresence.cs. Small, isolated.

**Why last in the core sequence:** AI and Discord are P1 features. The framework is useful without them. They're also the most likely to change (new LLM providers, updated Discord API).

### Day 71-80: Build, Package, Test (The Distribution)

**Build order:**
1. **`mf build` integration** — Unity batch mode build, AssetBundle compilation
2. **`mf package`** — Create distributable archive with runtime + project
3. **End-to-end testing** — `mf new` -> `mf dev` -> `mf build` -> `mf package` -> run
4. **Performance validation** — Memory < 150MB idle, FPS >= 60, CPU < 5% idle
5. **Error handling audit** — Every failure mode has a clear error message

### Day 81-90: Documentation & Release Prep

**Deliverables:**
1. Getting Started guide (5-minute tutorial)
2. API reference for all service interfaces
3. Platform compatibility matrix
4. Troubleshooting guide
5. Demo project (VRM character with idle + dance + AI chat)
6. Blog post / announcement draft

---

## Why This Order Is Correct

The dependency chain is:

```
Repo/CLI → Runtime Core → X11 Backend → Character → Audio → System → AI → Build → Docs
```

Each phase depends on the previous:
- **Without CLI:** No developer experience
- **Without runtime core:** No service architecture
- **Without X11 backend:** No window management
- **Without character:** No visible product
- **Without audio:** No music-reactive behavior
- **Without system integration:** No desktop-native feel
- **Without AI:** Still useful, but less differentiated
- **Without build/package:** Can't distribute
- **Without docs:** Can't onboard developers

## What Gets Cut If 90 Days Isn't Enough

Priority order (keep the most valuable):
1. AI chat (P1 — can be added in v1.1)
2. Discord (P1 — can be added in v1.1)
3. System tray (P1 — can be added in v1.1)
4. Notifications (P1 — can be added in v1.1)
5. Custom dances (P2 — future)
6. Mod system (P2 — future)
7. Plugin system (P3 — future)

## The Minimum Viable Framework (Day 90 deliverable)

If everything goes right, on Day 90 a developer can run:

```bash
mf new my-mate
cd my-mate
# Copy avatar.vrm into assets/
mf dev
```

And see a transparent VRM character on their Linux desktop that:
- Tracks mouse cursor with head/eyes
- Plays idle animations
- Dances when music plays
- Has a settings menu
- Can be built and distributed

That's the framework. Everything else is iteration.
