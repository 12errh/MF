# Codebase Analysis: Mate-Engine-Linux-Port

## Repository Overview

**Repository:** `Marksonthegamer/Mate-Engine-Linux-Port`
**Product:** MateEngineX (Desktop Mate / Desktop Pet)
**Unity Version:** 6000.2.6f2 (Unity 6)
**Scripting Backend:** Mono (Release)
**Platform Target:** Linux (X11 primary, partial Hyprland/KWin Wayland)
**License:** MateEngine Pro License v2.0 (Copyleft + Non-commercial) — Compatible with Mate Framework (also open-source + non-commercial)
**Total C# Files:** ~1357
**Total Asset Files:** ~4,429 (non-meta)

## What This Repository Is

An **unofficial Linux port** of shinyflvre's MateEngine — a desktop pet application. Built by a single developer (Marksonthegamer) as a community project. It renders a VRM character on the desktop with transparency, responds to mouse interaction, dances to music, integrates with AI/LLM for conversation, supports system tray, Discord presence, and runs across X11 and some Wayland compositors.

## What Already Works

1. **X11 transparent window management** — 2600+ line WindowManager.cs with ~70 DllImport bindings
2. **Hyprland compositor integration** — Full IWindowManagerImplementation via Unix sockets
3. **KWin/KDE integration** — DBus-based window management
4. **VRM loading** — Both VRM 0.x and 1.0 via vendored UniVRM
5. **Audio-reactive dancing** — PulseAudio monitoring with per-app audio peak detection
6. **AI chat** — LLMUnity integration (local LLM) + Ollama integration
7. **System tray** — Ayatana AppIndicator via P/Invoke
8. **Desktop notifications** — DBus org.freedesktop.Notifications
9. **Discord Rich Presence** — DiscordRPC library
10. **Mouse tracking** — Head, spine, eye tracking with bone IK
11. **Mod system** — StreamingAssets-based mod loading
12. **Settings persistence** — JSON-based settings with migration
13. **Multi-instance support** — `--savefile` and `--datadir` CLI args
14. **Localization** — Unity Localization package integration
15. **Desktop environment detection** — XDG_CURRENT_DESKTOP, XDG_SESSION_TYPE parsing

## What Should Be Preserved

- **WindowManager.cs X11 layer** — Battle-tested, handles edge cases for multiple compositors
- **HyprlandManager** — Clean IWindowManagerImplementation, Unix socket architecture
- **KWinManager** — DBus integration pattern
- **IWindowManagerImplementation interface** — Correct abstraction boundary
- **PulseAudioManager** — Working audio monitoring pipeline
- **TrayIndicator** — Working system tray
- **VRMLoader** — VRM 0.x + 1.0 dual loading with error recovery
- **AvatarMouseTracking** — Sophisticated IK tracking system
- **SaveLoadHandler.SettingsData schema** — Defines the complete settings model

## What Should Be Refactored

- **Singleton pattern** — 10+ singletons with inconsistent patterns; needs service container
- **SaveLoadHandler.Instance.data** — God object accessed by 30+ classes
- **WindowManager.cs** — 2618 lines, mixes X11 P/Invoke, window management, input handling, rendering
- **MonoBehaviour dependency injection** — FindFirstObjectByType / FindObjectOfType everywhere
- **Avatar handler proliferation** — 30+ MonoBehaviour avatar handlers, most tightly coupled
- **Settings coupling** — Every handler directly reads SaveLoadHandler.Instance.data

## What Should Be Redesigned

- **Project structure** — Flat MonoBehaviour soup → layered architecture
- **Configuration model** — Monolithic SettingsData → typed, validated config sections
- **Platform abstraction** — IWindowManagerImplementation is a start, needs expansion
- **Developer API** — No public API exists; everything is internal Unity wiring
- **Runtime distribution** — Currently requires Unity Editor to build

## What Should Be Removed

- **Third-party shader suites** (Poiyomi, lilToon, Mochie) — Not needed for framework
- **uWindowCapture** — Windows-only, irrelevant for Linux-first
- **UMotion** — Animation authoring tool, not runtime
- **DynamicBone** — Runtime physics, not framework concern
- **SystemTray WinAPI.cs** — Windows-only tray code
- **SystemTray Windows structs** — Not needed for Linux
- **Ollama Unity** — Can be replaced with direct HTTP integration
