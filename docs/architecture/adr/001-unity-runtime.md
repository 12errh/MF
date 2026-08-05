# ADR-001: Unity as Runtime Engine

## Status
Accepted

## Context
The existing application is built entirely on Unity 6 (6000.2.6f2) with Mono scripting backend. The question is whether Unity should remain the runtime for the Mate Framework, or whether we should rewrite the rendering/character system in a different engine.

## Decision
Unity remains the runtime engine for the Mate Framework. The `mf` CLI manages the runtime as a prebuilt binary. Developers never interact with Unity directly.

## Rationale

### Evidence for keeping Unity
1. **Working VRM pipeline** — UniVRM (vendored) handles VRM 0.x and 1.0 loading, including SpringBone, LookAt, MToon materials. Rewriting this would be 6-12 months of work.
2. **Working animation system** — AnimatorController with blend trees, state machines, and coroutine-based transitions are production-ready.
3. **Working X11 integration** — WindowManager.cs (2618 lines) with ~70 P/Invoke bindings is battle-tested across multiple compositors.
4. **Working audio pipeline** — PulseAudio integration for music-reactive dancing works.
5. **Working character IK** — AvatarMouseTracking with head, spine, and eye tracking uses Unity's bone system effectively.
6. **Shader ecosystem** — Poiyomi Toon, lilToon, and Mochie shaders are Unity-specific and deeply integrated.

### Evidence against rewriting
1. **Estimated rewrite cost** — Rewriting VRM, animation, rendering, and IK in Rust/C++ would be 12-18 months for a single developer.
2. **No technical reason** — Unity works correctly for this use case. The "rewrite in Rust" impulse has no concrete technical justification.
3. **Asset ecosystem** — VRM models, shaders, and animations are Unity-native formats.

### Risks of keeping Unity
1. Unity licensing (requires runtime distribution)
2. Unity version lock (6000.2.6f2)
3. Binary size (Unity player is large)
4. Update cadence tied to Unity releases

## Consequences
- Runtime is distributed as a prebuilt Unity player (~100MB)
- Developers interact only with `mf` CLI and project files
- Unity version upgrades are managed by the framework team
- Framework assemblies can be hot-reloaded in development mode
- C# code hot reload is NOT feasible (Unity limitation)

## Alternatives Considered
1. **Rewrite in Godot** — Rejected: Godot's VRM support is immature, no existing integration
2. **Rewrite in Bevy/egui** — Rejected: No VRM support, would need custom renderer
3. **Use raw OpenGL/Vulkan** — Rejected: Would lose all animation/physics/VRM infrastructure
