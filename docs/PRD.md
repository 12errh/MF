# Product Requirements Document: Mate Framework

## Product Vision

Mate Framework enables developers to create desktop-mate / desktop-character / AI-agent applications without understanding Unity internals or platform-specific desktop APIs. Developers interact through a CLI (`mf`) and a configuration file (`mate.toml`).

## Target Users

### Primary: Indie Developers
- Create desktop pets for themselves and their community
- Want VRM characters on their desktop
- Need AI chat functionality
- Don't want to learn Unity

### Secondary: AI/Agent Builders
- Create AI-powered desktop agents
- Need transparent window + character + LLM
- Want rapid prototyping
- Need cross-platform support

### Tertiary: Content Creators
- Create branded desktop companions
- Need Discord integration
- Want audio-reactive features
- Need customization (themes, animations)

## Success Metrics

| Metric | Target |
|--------|--------|
| Time to first running mate | < 5 minutes |
| `mf new` to `mf dev` | < 30 seconds |
| Memory usage (idle) | < 150MB |
| Memory usage (AI chat) | < 300MB |
| FPS (transparent window) | >= 60 |
| Supported platforms (v1.0) | Linux X11 |
| Supported platforms (v2.0) | Linux Wayland (Hyprland, KWin) |
| Supported platforms (v3.0) | Windows, macOS |

## Feature Requirements

### P0 (Must Have for v1.0)

1. **Project scaffolding** — `mf new` creates a complete project with template
2. **Dev mode** — `mf dev` starts runtime with live config reload
3. **VRM character loading** — Load and render VRM models
4. **Transparent window** — Character renders on desktop with transparency
5. **Click-through** — Mouse clicks pass through to desktop
6. **Always-on-top** — Character stays above other windows
7. **Mouse tracking** — Character follows mouse cursor
8. **Idle animations** — Character has idle states
9. **Dance animations** — Character dances when music plays
10. **Settings persistence** — Settings saved to disk
11. **Platform detection** — Auto-detect X11/Hyprland/KWin
12. **X11 backend** — Full X11 window management

### P1 (Should Have for v1.0)

13. **System tray** — System tray icon with menu
14. **Notifications** — Desktop notification support
15. **AI chat** — Chat with character via Ollama
16. **Audio monitoring** — Per-app audio detection
17. **Build command** — `mf build` creates distributable
18. **Doctor command** — `mf doctor` diagnoses issues
19. **Capabilities command** — `mf capabilities` shows features
20. **Discord Rich Presence** — Show status in Discord

### P2 (Nice to Have for v1.0)

21. **Mod support** — Load custom sounds/animations
22. **Custom dances** — Import custom dance animations
23. **Multi-monitor** — Proper multi-monitor support
24. **Keyboard shortcuts** — Configurable hotkeys
25. **Localization** — Multi-language support

### P3 (Future)

26. **Plugin system** — Load C# plugins at runtime (v2.0, see ADR-010/013)
27. **Hot reload** — Reload config without restart (already works for TOML/assets in v1)
28. **Voice input** — Speech-to-text for character
29. **Screen capture** — Character reacts to screen content
30. **Multi-platform** — Windows, macOS, Wayland

## Non-Functional Requirements

### Performance
- Idle memory: < 150MB
- AI chat memory: < 300MB
- Startup time: < 5 seconds
- FPS: >= 60 (transparent window)
- CPU idle: < 5%
- CPU dancing: < 15%

### Security
- No network access without user consent
- AI model execution sandboxed
- File system access limited to project directory
- No credential storage without encryption

### Usability
- CLI output is human-readable and machine-parseable (--json)
- All commands have --help
- Errors include actionable guidance
- First-run experience is guided

### Distribution
- Single binary for `mf` CLI (~5MB)
- Runtime downloaded on first use (~100MB)
- Project files are text-only (TOML, JSON)
- Cross-compilation supported

## Licensing

Mate-Engine-Linux-Port uses MateEngine Pro License v2.0 (copyleft, non-commercial). The Mate Framework is also open-source and non-commercial, making the licenses compatible. The framework must also be released under the same copyleft + non-commercial terms with source disclosure.
