# Capability Matrix: Mate Framework

## Platform Capability Model

Every platform operation should be classified as:

- **Portable** — Works identically on all platforms
- **Platform-specific** — Implementation varies but API is unified
- **Capability-dependent** — May not be available on some platforms
- **Impossible** — Cannot work on certain platforms (e.g., window manipulation on Wayland without compositor support)

## Feature Classification

### Core (Must work everywhere)
| Feature | Classification | Notes |
|---------|---------------|-------|
| Render VRM character | Portable | Unity rendering |
| Mouse position (global) | Platform-specific | X11: XQueryPointer, Wayland: compositor-dependent |
| Window position | Platform-specific | X11: XMoveWindow, Wayland: compositor-dependent |
| Transparent window | Capability-dependent | Requires ARGB visual or compositor support |
| Click-through | Capability-dependent | Requires input passthrough support |
| Always-on-top | Platform-specific | X11: _NET_WM_STATE_ABOVE, Wayland: compositor |
| Load VRM model | Portable | UniVRM handles format |
| Animation system | Portable | Unity Animator |
| Audio playback | Portable | Unity AudioSource |
| Settings persistence | Portable | JSON file |
| System tray | Platform-specific | Linux: AppIndicator, Windows: Shell_NotifyIcon |
| Notifications | Platform-specific | Linux: DBus, Windows: Toast |
| Discord integration | Portable | Named pipe protocol |
| AI/LLM chat | Portable | HTTP/WebSocket |
| Voice input | Platform-specific | Microphone access varies |
| Keyboard/mouse input | Portable | Unity Input |

### Desktop Integration (Linux-specific)
| Feature | Classification | Notes |
|---------|---------------|-------|
| Window sitting | Platform-specific | X11: dock type, Hyprland: floating |
| Hide from taskbar | Platform-specific | X11: _NET_WM_STATE_SKIP_TASKBAR |
| Window type (dock/desktop) | Platform-specific | X11: _NET_WM_WINDOW_TYPE |
| Monitor enumeration | Platform-specific | X11: XRandR, Wayland: compositor |
| Window enumeration | Platform-specific | X11: XQueryTree, Wayland: compositor |
| Screen capture | Platform-specific | X11: SHM, Wayland: portal |
| PulseAudio monitoring | Linux-specific | Requires libpulse |
| AppIndicator tray | Linux-specific | Requires libayatana-appindicator |

### Advanced Features
| Feature | Classification | Notes |
|---------|---------------|-------|
| Hot reload config | Portable | File watching |
| Hot reload C# code | Not feasible | Unity IL2CPP/Mono limitations |
| Mod loading | Portable | AssetBundle loading |
| Multi-instance | Portable | CLI args for savefile/datadir |
| Custom dance import | Portable | AnimationClip loading |
| Blendshape control | Portable | Unity blend shapes |
| DynamicBone physics | Portable | Unity DynamicBone |
| Particle effects | Portable | Unity ParticleSystem |

## Platform Backend Requirements

### Linux X11 Backend (v1.0)
- Window management: X11 P/Invoke (existing)
- Monitor detection: XRandR
- Audio: PulseAudio
- Tray: AppIndicator
- Notifications: DBus
- Input: X11 + Unity Input

### Linux Wayland Backend (v2.0+)
- Window management: Compositor-specific (Hyprland sockets, KWin DBus)
- Monitor detection: Compositor-specific
- Audio: PulseAudio (same as X11)
- Tray: AppIndicator (via XWayland) or StatusNotifierItem
- Notifications: DBus (same as X11)
- Input: Compositor-dependent

### Windows Backend (v3.0+)
- Window management: Win32 API (user32.dll)
- Monitor detection: EnumDisplayMonitors
- Audio: WasapiLoopbackCapture
- Tray: Shell_NotifyIcon
- Notifications: ToastNotificationManager
- Input: GetCursorPos, SetWindowPos

### macOS Backend (v4.0+)
- Window management: CoreGraphics, Accessibility API
- Monitor detection: CGGetActiveDisplayList
- Audio: CoreAudio
- Tray: NSStatusItem
- Notifications: UNUserNotificationCenter
- Input: CGEvent, NSEvent
