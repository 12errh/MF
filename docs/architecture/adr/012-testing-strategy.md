# ADR-012: Testing Strategy

## Status
Accepted

## Context
The current codebase has zero automated tests. The framework needs testing at multiple levels.

## Decision
Three-level testing strategy: CLI tests (Rust), Runtime unit tests (C#), Integration tests (end-to-end).

### Level 1: CLI Tests (Rust, Cargo test)
- Manifest parser tests
- TOML validation tests
- File path resolution tests
- Command argument parsing tests
- Runtime version comparison tests
- Error message formatting tests

```
tests/
  manifest_test.rs
  runtime_test.rs
  process_test.rs
```

### Level 2: Runtime Unit Tests (C#, Unity Test Runner)
- Service container tests
- Configuration loading tests
- Platform detection tests
- Event bus tests
- WindowInfo/MonitorInfo struct tests

```
Tests/
  Editor/
    ServiceContainerTests.cs
    ConfigurationTests.cs
    PlatformDetectionTests.cs
  Runtime/
    EventBusTests.cs
```

### Level 3: Integration Tests (End-to-End)
- `mf new` creates valid project
- `mf dev` starts and stops cleanly
- `mf doctor` detects issues
- `mf build` creates output
- Runtime loads VRM model
- Runtime positions window correctly
- Runtime tracks mouse
- Runtime plays idle animation

```
tests/
  integration/
    test_new_project.sh
    test_dev_mode.sh
    test_build.sh
    test_window_management.sh
```

### Level 4: Platform Tests (Manual + Automated)
- Test on X11 (GNOME, KDE, i3, sway)
- Test on Hyprland
- Test on KWin Wayland
- Test multi-monitor
- Test different VRM models
- Test different audio setups

## Test-Driven Development
- CLI: TDD (write tests first)
- Runtime: Unit tests alongside implementation
- Integration: After each phase completion
- Platform: Manual testing after each phase

## Consequences
- CI runs all Rust tests on every push
- CI runs C# tests when Unity project changes
- Integration tests run on weekly schedule
- Platform tests run before each release
