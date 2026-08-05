# ADR-003: Existing C# Native Layer vs Rust Native Layer

## Status
Accepted

## Context
The existing application has extensive C# P/Invoke bindings for X11 (~70 DllImports in WindowManager.cs), PulseAudio (~18 DllImports), AppIndicator (6 DllImports), and GDK (2 DllImports). The question is whether to keep these or rewrite in Rust.

## Decision
Keep the existing C# P/Invoke layer. Do NOT rewrite native integrations in Rust.

## Rationale

### Evidence for keeping C# native layer
1. **Battle-tested code** — WindowManager.cs has been refined across multiple Linux compositors (X11, Hyprland, KWin). The 2618-line file handles edge cases that would take months to rediscover.
2. **Working Hyprland integration** — HyprlandManager + HyprlandDispatcher + HyprlandEventReader form a complete IPC system via Unix sockets. This is production-ready.
3. **Working KWin integration** — KWinManager uses DBus (Tmds.DBus) for KDE window management. Working and tested.
4. **Working PulseAudio monitoring** — PulseAudioManager monitors audio peaks per-application. Essential for music-reactive dancing.
5. **Working system tray** — TrayIndicator uses AppIndicator P/Invoke. Works on GTK-based desktops.
6. **Unity interop** — C# P/Invoke runs inside the Unity player process. There's no benefit to moving this to a separate Rust process.

### Evidence against Rust native layer
1. **IPC overhead** — A Rust native layer would need to communicate with Unity via IPC (pipes, sockets, shared memory). This adds complexity and latency.
2. **No concrete benefit** — The P/Invoke bindings work correctly. Rust would not be faster for these operations.
3. **Maintainability** — Keeping everything in C# means one language, one build system, one set of dependencies.
4. **Scope creep** — Rewriting native layer in Rust would delay the framework by 3-6 months.

### When Rust native layer might be justified
- If we need to support platforms without Unity (unlikely)
- If P/Invoke performance becomes a bottleneck (not observed)
- If we need to share native code with the `mf` CLI (possible but not necessary)

## Consequences
- Native integrations remain as C# P/Invoke
- `mf` CLI is the only Rust component
- Runtime is distributed as a Unity player with embedded P/Invoke
- Future platform backends (Windows, macOS) can also use C# P/Invoke
- No IPC complexity between CLI and runtime for native operations

## Alternatives Considered
1. **Rust native library loaded by Unity** — Rejected: Adds build complexity, no performance benefit
2. **Rust process with IPC** — Rejected: Adds latency, complexity, process management
3. **C/C++ native library** — Rejected: Same as Rust but with more memory safety risks
