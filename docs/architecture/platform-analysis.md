# Platform Analysis: Mate-Engine-Linux-Port

## Platform Detection Architecture

The current detection flow:

```
EarlyEnvSet.InitBeforeAnything()
  [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
  -> Check XDG_SESSION_TYPE environment variable
  -> Check XDG_CURRENT_DESKTOP environment variable
  -> Set SDL_VIDEO_X11_VISUALID for transparency
  -> Check ARGB visual support

WindowManager.OnEnable()
  -> Parse XDG_CURRENT_DESKTOP into DesktopEnvironments enum
  -> Parse XDG_SESSION_TYPE into SessionTypes enum
  -> Select IWindowManagerImplementation based on DE
```

## Linux Desktop Environment Support

### X11 (Universal Linux)
**Status:** Primary target, fully implemented
**Implementation:** WindowManager.cs (raw X11 P/Invoke)
**Capabilities:**
- Window positioning, resizing, borderless
- Transparent windows (ARGB visual)
- Click-through input
- Always-on-top (_NET_WM_STATE_ABOVE)
- Hide from taskbar (_NET_WM_STATE_SKIP_TASKBAR)
- Window type setting (_NET_WM_WINDOW_TYPE)
- Mouse cursor tracking (XQueryPointer)
- Multi-monitor (XRandR)
- Window enumeration (XQueryTree)
- Window damage tracking (XDamage)
- Shared memory capture (SHM)
- Transient window hints
- Window class detection

### Hyprland (Wayland compositor)
**Status:** Full implementation
**Implementation:** HyprlandManager.cs + HyprlandDispatcher.cs + HyprlandEventReader.cs
**Communication:** Unix domain sockets
**Capabilities:**
- Window positioning (hyprctl dispatch)
- Window resizing
- Floating/pinning
- Workspace management
- Cursor position
- Monitor enumeration
- Layer shell support
- Client listing

### KDE/KWin (Wayland compositor)
**Status:** Full implementation
**Implementation:** KWinManager.cs
**Communication:** DBus (Tmds.DBus)
**Capabilities:**
- Window positioning via KWin scripting
- Window resizing
- Client listing
- Monitor enumeration
- Stacking order
- Fullscreen/maximized detection

### GNOME
**Status:** Not implemented
**Evidence:** No GNOME-specific code found
**Limitation:** GNOME restricts X11 window manipulation on Wayland

### Other X11
**Status:** Fallback mode
**Implementation:** Raw X11 in WindowManager.cs
**Capabilities:** Same as X11 universal

### Other Wayland
**Status:** Not supported (warning notification sent)
**Evidence:** DBusNotificationHelper sends warning about Wayland limitations

## Platform-Specific Feature Matrix

| Feature | X11 | Hyprland | KWin | Wayland (other) |
|---------|-----|----------|------|-----------------|
| Transparent window | Yes (ARGB visual) | Via XWayland | Via XWayland | No |
| Click-through | Yes | Yes | Yes | No |
| Always-on-top | Yes (_NET_WM_STATE_ABOVE) | Yes (hyprctl) | Yes (DBus) | No |
| Window positioning | Yes (XMoveWindow) | Yes (hyprctl) | Yes (KWin script) | No |
| Hide from taskbar | Yes (_NET_WM_STATE_SKIP_TASKBAR) | Yes (hyprctl) | Yes (DBus) | No |
| Mouse tracking | Yes (XQueryPointer) | Yes (hyprctl) | Yes (DBus) | No |
| Multi-monitor | Yes (XRandR) | Yes (hyprctl) | Yes (DBus) | No |
| Window enumeration | Yes (XQueryTree) | Yes (hyprctl) | Yes (DBus) | No |
| Borderless | Yes (MOTIF hints) | Yes (hyprctl) | Yes (DBus) | No |
| Window sitting | Yes | Yes (monitor sitting) | Yes | No |
| Desktop sitting | Yes (dock type) | Yes (dock type) | Yes | No |
| Screen capture | Yes (SHM/XGetImage) | Via XWayland | Via XWayland | No |
| System tray | Yes (AppIndicator) | Yes (AppIndicator) | Yes (AppIndicator) | Limited |
| Notifications | Yes (DBus) | Yes (DBus) | Yes (DBus) | Yes (DBus) |
| Audio monitoring | Yes (PulseAudio) | Yes (PulseAudio) | Yes (PulseAudio) | Yes (PulseAudio) |
| Discord RPC | Yes (named pipes) | Yes | Yes | Yes |
| VRM rendering | Yes | Yes | Yes | Yes |
| AI/LLM | Yes | Yes | Yes | Yes |

## Native Library Requirements

### Required (core functionality)
- libX11.so.6 — X11 protocol
- libXext.so.6 — X11 extensions
- libpulse.so.0 — Audio monitoring
- libgdk-3.so.0 — GTK integration (file dialogs, visual detection)

### Optional (enhanced features)
- libXrandr.so.2 — Multi-monitor (falls back to single monitor)
- libXdamage.so.1 — Damage tracking (optional optimization)
- libXcomposite.so.1 — Compositing (optional)
- libXcursor.so.1 — Cursor management (optional)
- libayatana-appindicator3.so.1 — System tray

### Wayland-specific
- Hyprland IPC socket — Hyprland compositor
- DBus (Tmds.DBus) — KWin integration, notifications

## Platform Abstraction Gaps

1. **No unified monitor API** — Each implementation returns monitors differently
2. **No unified window info** — IntPtr handles vs KWinClient objects
3. **Error handling varies** — X11 returns null/errors, Hyprland throws, KWin uses DBus errors
4. **No async in interface** — KWin DBus is async but interface is sync
5. **No capability query** — No way to ask "can this platform do X?"
6. **No fallback behavior** — If a method isn't supported, it silently fails
