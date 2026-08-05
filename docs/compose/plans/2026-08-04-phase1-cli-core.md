# Phase 1: mf CLI Core — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `mf dev` actually launch the Unity runtime, watch for file changes, and restart on crash. Complete the CLI developer experience.

**Architecture:** Rust CLI with process management (tokio), file watching (notify crate), and runtime lifecycle. The CLI spawns the Unity player, forwards stdout/stderr, watches `mate.toml` and assets for changes, and restarts the player on crash or config change.

**Tech Stack:** Rust (edition 2024), clap 4, tokio (async runtime), notify 6 (file watching), serde/toml, nix (signals)

## Global Constraints

- All new crates in `crates/` directory
- All functions that can fail return `Result<T, MfError>` or `anyhow::Result`
- Process management uses tokio for async signal handling
- File watching uses crossbeam channels (notify crate default)
- All tests run without a real Unity runtime present (mock paths)
- No `unwrap()` in library code

## File Structure

```
crates/mf-cli/src/
├── main.rs
├── commands/
│   ├── mod.rs
│   ├── new.rs           (from Phase 0)
│   ├── doctor.rs         (from Phase 0)
│   ├── dev.rs            (NEW: full implementation)
│   ├── build.rs          (NEW: mf build)
│   ├── package.rs        (NEW: mf package)
│   └── runtime.rs        (NEW: mf runtime install/list/status)
crates/mf-core/src/
├── lib.rs
├── error.rs              (from Phase 0)
├── manifest.rs           (from Phase 0)
├── runtime.rs            (from Phase 0)
├── process.rs            (NEW: process manager)
├── watcher.rs            (NEW: file watcher)
└── build.rs              (NEW: build/package logic)
```

---

### Task 1.1: Process Manager (TDD)

**Covers:** ADR-002 (CLI manages runtime), process lifecycle

**Files:**
- Create: `crates/mf-core/src/process.rs`

**Interfaces:**
- Produces: `RuntimeProcess`, `start_runtime`, `kill_runtime`, `wait_for_exit`
- Consumes: `runtime_path` (from Phase 0)

- [ ] **Step 1: Write failing tests**

```rust
// crates/mf-core/src/process.rs
use std::path::PathBuf;
use std::process::Stdio;
use crate::MfError;

/// Configuration for launching the Unity runtime
pub struct RuntimeLaunchConfig {
    pub player_path: PathBuf,
    pub project_dir: PathBuf,
    pub project_args: Vec<String>,
}

/// Handle to a running Unity player process
pub struct RuntimeProcess {
    child: std::process::Child,
    pub pid: u32,
}

impl RuntimeProcess {
    /// Spawn the Unity player with the given config
    pub fn spawn(config: &RuntimeLaunchConfig) -> Result<Self, MfError> {
        if !config.player_path.exists() {
            return Err(MfError::RuntimeNotInstalled);
        }

        let mut cmd = std::process::Command::new(&config.player_path);
        cmd.args(&[
            "--projectPath",
            &config.project_dir.to_string_lossy(),
        ]);
        cmd.args(&config.project_args);
        cmd.stdin(Stdio::null());
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::piped());

        let child = cmd.spawn().map_err(|e| MfError::Io(format!("failed to spawn Unity player: {}", e)))?;
        let pid = child.id();

        Ok(Self { child, pid })
    }

    /// Wait for the process to exit, return exit code
    pub fn wait(&mut self) -> Result<i32, MfError> {
        self.child.wait()
            .map(|status| status.code().unwrap_or(-1))
            .map_err(|e| MfError::Io(format!("failed to wait on process: {}", e)))
    }

    /// Kill the process
    pub fn kill(&mut self) -> Result<(), MfError> {
        self.child.kill()
            .map_err(|e| MfError::Io(format!("failed to kill process: {}", e)))
    }

    /// Check if the process is still running
    pub fn is_running(&mut self) -> bool {
        matches!(self.child.try_wait(), Ok(None))
    }
}

/// Launch config from manifest + runtime path
pub fn build_launch_config(
    player_path: PathBuf,
    project_dir: PathBuf,
) -> RuntimeLaunchConfig {
    RuntimeLaunchConfig {
        player_path,
        project_dir,
        project_args: Vec::new(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn spawn_fails_when_player_missing() {
        let config = RuntimeLaunchConfig {
            player_path: PathBuf::from("/nonexistent/MateRuntime"),
            project_dir: PathBuf::from("/tmp/test"),
            project_args: vec![],
        };
        let result = RuntimeProcess::spawn(&config);
        assert!(result.is_err());
        match result.unwrap_err() {
            MfError::RuntimeNotInstalled => {}
            other => panic!("expected RuntimeNotInstalled, got {:?}", other),
        }
    }

    #[test]
    fn build_launch_config_sets_paths() {
        let config = build_launch_config(
            PathBuf::from("/runtime/player"),
            PathBuf::from("/project"),
        );
        assert_eq!(config.player_path, PathBuf::from("/runtime/player"));
        assert_eq!(config.project_dir, PathBuf::from("/project"));
        assert!(config.project_args.is_empty());
    }
}
```

Update `crates/mf-core/src/lib.rs`:
```rust
pub mod error;
pub mod manifest;
pub mod process;
pub mod runtime;

pub use error::MfError;
pub use manifest::MateManifest;
pub use process::{RuntimeProcess, RuntimeLaunchConfig};
```

- [ ] **Step 2: Run tests — verify they pass**

```bash
cargo test -p mf-core
# Expected: 29 tests pass
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: process manager for Unity player lifecycle"
```

---

### Task 1.2: File Watcher (TDD)

**Covers:** Hot reload for config/assets (ADR-013)

**Files:**
- Create: `crates/mf-core/src/watcher.rs`

**Interfaces:**
- Produces: `ProjectWatcher`, `WatcherEvent`

- [ ] **Step 1: Add notify dependency**

```toml
# crates/mf-core/Cargo.toml (add to [dependencies])
notify = "6"
```

- [ ] **Step 2: Write the watcher implementation and tests**

```rust
// crates/mf-core/src/watcher.rs
use notify::{Event, EventKind, RecommendedWatcher, RecursiveMode, Watcher};
use std::path::{Path, PathBuf};
use std::sync::mpsc;

#[derive(Debug, Clone, PartialEq)]
pub enum WatcherEvent {
    ConfigChanged(String),   // mate.toml or personality.toml changed
    AssetChanged(String),    // VRM, animation, sound changed
    ModChanged(String),      // mod directory changed
    Unknown(String),
}

/// Watches a project directory for changes
pub struct ProjectWatcher {
    _watcher: RecommendedWatcher,
    rx: mpsc::Receiver<WatcherEvent>,
}

impl ProjectWatcher {
    /// Create a new watcher for the given project directory
    pub fn new(project_dir: &Path) -> Result<Self, crate::MfError> {
        let (tx, rx) = mpsc::channel();

        let mut watcher = RecommendedWatcher::new(
            move |result: Result<Event, notify::Error>| {
                if let Ok(event) = result {
                    let event = match event.kind {
                        EventKind::Create(_) | EventKind::Modify(_) | EventKind::Remove(_) => {
                            let path_str = event
                                .paths
                                .first()
                                .map(|p| p.display().to_string())
                                .unwrap_or_default();

                            if path_str.ends_with("mate.toml") || path_str.ends_with("personality.toml") {
                                WatcherEvent::ConfigChanged(path_str)
                            } else if path_str.ends_with(".vrm")
                                || path_str.ends_with(".anim")
                                || path_str.ends_with(".wav")
                                || path_str.ends_with(".mp3")
                            {
                                WatcherEvent::AssetChanged(path_str)
                            } else if path_str.contains("mods/") {
                                WatcherEvent::ModChanged(path_str)
                            } else {
                                WatcherEvent::Unknown(path_str)
                            }
                        }
                        _ => return,
                    };
                    let _ = tx.send(event);
                }
            },
            notify::Config::default(),
        )
        .map_err(|e| crate::MfError::Io(format!("failed to create file watcher: {}", e)))?;

        // Watch project root
        watcher
            .watch(project_dir, RecursiveMode::Recursive)
            .map_err(|e| crate::MfError::Io(format!("failed to watch directory: {}", e)))?;

        Ok(Self {
            _watcher: watcher,
            rx,
        })
    }

    /// Check for events (non-blocking)
    pub fn poll(&self) -> Option<WatcherEvent> {
        self.rx.try_recv().ok()
    }

    /// Block until an event arrives (with timeout)
    pub fn wait_event(&self, timeout_ms: u64) -> Option<WatcherEvent> {
        self.rx.recv_timeout(std::time::Duration::from_millis(timeout_ms)).ok()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;
    use std::fs;
    use std::thread;
    use std::time::Duration;

    #[test]
    fn watcher_detects_toml_change() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();

        // Write a file
        fs::write(tmp.path().join("mate.toml"), "[project]\nname = \"test\"\n").unwrap();

        // Wait for event
        let event = watcher.wait_event(2000);
        assert!(event.is_some(), "should detect mate.toml change");
        match event.unwrap() {
            WatcherEvent::ConfigChanged(path) => {
                assert!(path.contains("mate.toml"));
            }
            other => panic!("expected ConfigChanged, got {:?}", other),
        }
    }

    #[test]
    fn watcher_detects_vrm_change() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();

        fs::write(tmp.path().join("avatar.vrm"), b"fake-vrm").unwrap();

        let event = watcher.wait_event(2000);
        assert!(event.is_some());
        match event.unwrap() {
            WatcherEvent::AssetChanged(path) => {
                assert!(path.contains("avatar.vrm"));
            }
            other => panic!("expected AssetChanged, got {:?}", other),
        }
    }

    #[test]
    fn watcher_poll_returns_none_when_nothing() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();
        assert!(watcher.poll().is_none());
    }
}
```

Update `crates/mf-core/src/lib.rs`:
```rust
pub mod error;
pub mod manifest;
pub mod process;
pub mod runtime;
pub mod watcher;

pub use error::MfError;
pub use manifest::MateManifest;
pub use process::{RuntimeProcess, RuntimeLaunchConfig};
pub use watcher::{ProjectWatcher, WatcherEvent};
```

- [ ] **Step 3: Run tests — verify they pass**

```bash
cargo test -p mf-core
# Expected: 32 tests pass
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: file watcher for project config and asset changes"
```

---

### Task 1.3: `mf dev` Full Implementation (TDD)

**Covers:** Core developer experience

**Files:**
- Modify: `crates/mf-cli/src/commands/dev.rs`
- Create: `crates/mf-cli/src/commands/runtime.rs`

**Interfaces:**
- Consumes: `RuntimeProcess`, `ProjectWatcher`, `resolve_version`, `player_path` (from Tasks 1.1, 1.2, Phase 0)

- [ ] **Step 1: Write the full dev command**

```rust
// crates/mf-cli/src/commands/dev.rs
use mf_core::{MfError, RuntimeProcess, ProjectWatcher, WatcherEvent};
use mf_core::{parse_manifest, resolve_version, player_path, build_launch_config};
use std::path::Path;
use std::thread;
use std::time::Duration;

pub fn run(json: bool) -> anyhow::Result<()> {
    run_inner(Path::new("."), json)
}

fn run_inner(dir: &Path, json: bool) -> anyhow::Result<()> {
    // 1. Load and validate manifest
    let manifest_path = dir.join("mate.toml");
    if !manifest_path.exists() {
        return Err(anyhow::anyhow!("mate.toml not found. Run `mf new <name>` first."));
    }

    let content = std::fs::read_to_string(&manifest_path)?;
    let manifest = parse_manifest(&content)
        .map_err(|e| anyhow::anyhow!("{}", e))?;
    mf_core::validate_manifest(&manifest)
        .map_err(|e| anyhow::anyhow!("{}", e))?;

    // 2. Resolve runtime
    let version = resolve_version(&manifest.project.runtime)
        .map_err(|e| anyhow::anyhow!("{}", e))?;
    let player = player_path(&version);

    if !player.exists() {
        return Err(anyhow::anyhow!(
            "Unity player not found at {}. Run `mf runtime install {}`",
            player.display(),
            version
        ));
    }

    if !json {
        println!("Starting '{}' with runtime v{}...", manifest.project.name, version);
        println!("Player: {}", player.display());
        println!("Project: {}", dir.display());
        println!("Press Ctrl+C to stop.\n");
    }

    // 3. Start file watcher
    let watcher = ProjectWatcher::new(dir)
        .map_err(|e| anyhow::anyhow!("failed to start file watcher: {}", e))?;

    // 4. Main loop: start -> watch -> restart on crash/change
    let mut restart_count = 0;
    const MAX_RESTARTS: u32 = 10;

    loop {
        let config = build_launch_config(player.clone(), dir.to_path_buf());
        let mut process = match RuntimeProcess::spawn(&config) {
            Ok(p) => {
                restart_count = 0; // reset on successful start
                if json {
                    println!("{}", serde_json::json!({
                        "event": "started",
                        "pid": p.pid,
                        "runtime": version,
                    }));
                } else {
                    println!("[mf] Runtime started (PID: {})", p.pid);
                }
                p
            }
            Err(e) => {
                return Err(anyhow::anyhow!("failed to start runtime: {}", e));
            }
        };

        // Wait for process exit or file change
        let exit_code = wait_for_event_or_exit(&mut process, &watcher, json);

        // Kill process if still running
        let _ = process.kill();

        match exit_code {
            DevExitReason::FileChanged(path) => {
                restart_count += 1;
                if restart_count > MAX_RESTARTS {
                    return Err(anyhow::anyhow!(
                        "too many restarts ({}) — something is wrong. Stopping.",
                        MAX_RESTARTS
                    ));
                }
                if json {
                    println!("{}", serde_json::json!({
                        "event": "restarting",
                        "reason": "file_changed",
                        "path": path,
                        "restart_count": restart_count,
                    }));
                } else {
                    println!("[mf] Change detected: {} (restart #{})", path, restart_count);
                }
                thread::sleep(Duration::from_millis(500)); // debounce
            }
            DevExitReason::ProcessCrashed(code) => {
                restart_count += 1;
                if restart_count > MAX_RESTARTS {
                    return Err(anyhow::anyhow!(
                        "runtime crashed too many times ({}). Last exit code: {}",
                        MAX_RESTARTS, code
                    ));
                }
                if json {
                    println!("{}", serde_json::json!({
                        "event": "restarting",
                        "reason": "crash",
                        "exit_code": code,
                        "restart_count": restart_count,
                    }));
                } else {
                    println!("[mf] Runtime crashed (exit: {}). Restarting... (#{})", code, restart_count);
                }
                thread::sleep(Duration::from_secs(1));
            }
            DevExitReason::UserInterrupt => {
                if json {
                    println!("{}", serde_json::json!({ "event": "stopped" }));
                } else {
                    println!("[mf] Stopped.");
                }
                return Ok(());
            }
        }
    }
}

enum DevExitReason {
    FileChanged(String),
    ProcessCrashed(i32),
    UserInterrupt,
}

/// Wait until the process exits OR a relevant file change occurs.
fn wait_for_event_or_exit(
    process: &mut RuntimeProcess,
    watcher: &ProjectWatcher,
    _json: bool,
) -> DevExitReason {
    let check_interval = Duration::from_millis(100);

    loop {
        // Check for file change
        if let Some(event) = watcher.poll() {
            match event {
                WatcherEvent::ConfigChanged(path) | WatcherEvent::AssetChanged(path) => {
                    return DevExitReason::FileChanged(path);
                }
                WatcherEvent::ModChanged(path) => {
                    return DevExitReason::FileChanged(path);
                }
                WatcherEvent::Unknown(_) => {}
            }
        }

        // Check if process is still running
        if !process.is_running() {
            let code = process.wait().unwrap_or(-1);
            return DevExitReason::ProcessCrashed(code);
        }

        thread::sleep(check_interval);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;
    use std::fs;

    #[test]
    fn dev_fails_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_err());
        assert!(result.unwrap_err().to_string().contains("mate.toml not found"));
    }

    #[test]
    fn dev_fails_with_invalid_manifest() {
        let tmp = TempDir::new().unwrap();
        fs::write(tmp.path().join("mate.toml"), "not valid {{{{").unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_err());
    }

    #[test]
    fn dev_json_output_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), true);
        assert!(result.is_err());
    }
}
```

- [ ] **Step 2: Write the `mf runtime` command**

```rust
// crates/mf-cli/src/commands/runtime.rs
use mf_core::{is_installed, list_installed, runtime_path};
use std::path::Path;

pub fn run(subcommand: &str, json: bool) -> anyhow::Result<()> {
    match subcommand {
        "list" => run_list(json),
        "status" => run_status(json),
        "install" => {
            if json {
                println!("{}", serde_json::json!({
                    "status": "not_implemented",
                    "message": "automatic runtime download will be available when releases are published"
                }));
            } else {
                println!("Automatic runtime download will be available when releases are published.");
                println!("For now, manually place the runtime at: {}", runtime_path("1.0.0").display());
            }
            Ok(())
        }
        _ => Err(anyhow::anyhow!("unknown runtime subcommand: {subcommand}")),
    }
}

fn run_list(json: bool) -> anyhow::Result<()> {
    let versions = list_installed();
    if json {
        println!("{}", serde_json::json!({ "versions": versions }));
    } else {
        if versions.is_empty() {
            println!("No runtimes installed. Run `mf runtime install` to download.");
        } else {
            println!("Installed runtimes:");
            for v in &versions {
                let path = runtime_path(v);
                let player = path.join("MateRuntime").join("MateRuntime");
                let status = if player.exists() { "ok" } else { "incomplete" };
                println!("  v{v} ({status}) — {}", path.display());
            }
        }
    }
    Ok(())
}

fn run_status(json: bool) -> anyhow::Result<()> {
    let versions = list_installed();
    let cache_dir = mf_core::runtime_cache_dir();

    if json {
        println!("{}", serde_json::json!({
            "cache_dir": cache_dir.display().to_string(),
            "count": versions.len(),
            "versions": versions,
        }));
    } else {
        println!("Runtime cache: {}", cache_dir.display());
        println!("Installed: {} version(s)", versions.len());
        for v in &versions {
            println!("  v{v}");
        }
    }
    Ok(())
}
```

Update `crates/mf-cli/src/commands/mod.rs`:
```rust
pub mod build;
pub mod dev;
pub mod doctor;
pub mod new;
pub mod package;
pub mod runtime;
```

Update `crates/mf-cli/src/main.rs` to add runtime subcommand:
```rust
// Add to Commands enum:
/// Manage runtime versions
Runtime {
    #[command(subcommand)]
    command: RuntimeCommands,
},

#[derive(Subcommand)]
enum RuntimeCommands {
    /// List installed runtime versions
    List,
    /// Show runtime status
    Status,
    /// Install a runtime version
    Install,
}
```

Add match arm:
```rust
Commands::Runtime { command } => {
    let subcmd = match &command {
        RuntimeCommands::List => "list",
        RuntimeCommands::Status => "status",
        RuntimeCommands::Install => "install",
    };
    commands::runtime::run(subcmd, cli.json)
}
```

- [ ] **Step 3: Run tests — verify they pass**

```bash
cargo test --workspace
# Expected: all tests pass
```

- [ ] **Step 4: Verify manually**

```bash
cargo run -- new dev-test
cd dev-test
cargo run -- doctor
cargo run -- runtime status
cargo run -- runtime list
cargo run -- --json runtime list
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: mf dev with file watching and auto-restart, mf runtime commands"
```

---

### Task 1.4: `mf build` (ADR-013 compliant)

**Covers:** ADR-013 (no Unity Editor required), build pipeline

**Files:**
- Create: `crates/mf-core/src/build.rs`
- Create: `crates/mf-cli/src/commands/build.rs`

**Interfaces:**
- Produces: `build_project()`, `BuildResult`
- Consumes: `MateManifest`, `parse_manifest`, `validate_manifest`

- [ ] **Step 1: Write failing tests for build**

```rust
// crates/mf-core/src/build.rs
use std::path::{Path, PathBuf};
use crate::MfError;

#[derive(Debug)]
pub struct BuildResult {
    pub output_dir: PathBuf,
    pub files_copied: usize,
    pub manifest: String,
}

/// Build a project — validate + package raw assets (no Unity Editor needed)
pub fn build_project(project_dir: &Path, output_dir: &Path) -> Result<BuildResult, MfError> {
    // 1. Load and validate manifest
    let manifest_path = project_dir.join("mate.toml");
    if !manifest_path.exists() {
        return Err(MfError::ManifestNotFound {
            path: manifest_path.display().to_string(),
        });
    }

    let content = std::fs::read_to_string(&manifest_path)
        .map_err(|e| MfError::Io(e.to_string()))?;
    let manifest = crate::manifest::parse_manifest(&content)?;
    crate::manifest::validate_manifest(&manifest)?;

    // 2. Create output directory
    std::fs::create_dir_all(output_dir)
        .map_err(|e| MfError::Io(e.to_string()))?;

    // 3. Copy manifest
    std::fs::create_dir_all(output_dir.join("config"))
        .map_err(|e| MfError::Io(e.to_string()))?;
    std::fs::copy(&manifest_path, output_dir.join("mate.toml"))
        .map_err(|e| MfError::Io(e.to_string()))?;

    let mut files_copied = 1;

    // 4. Copy raw assets (VRM, animations, sounds)
    let assets_dir = project_dir.join("assets");
    if assets_dir.is_dir() {
        files_copied += copy_dir_recursive(&assets_dir, &output_dir.join("assets"))?;
    }

    // 5. Copy mods
    let mods_dir = project_dir.join("mods");
    if mods_dir.is_dir() {
        files_copied += copy_dir_recursive(&mods_dir, &output_dir.join("mods"))?;
    }

    // 6. Copy config files (personality.toml etc)
    let config_dir = project_dir.join("config");
    if config_dir.is_dir() {
        files_copied += copy_dir_recursive(&config_dir, &output_dir.join("config"))?;
    }

    Ok(BuildResult {
        output_dir: output_dir.to_path_buf(),
        files_copied,
        manifest: manifest.project.name.clone(),
    })
}

fn copy_dir_recursive(src: &Path, dst: &Path) -> Result<usize, MfError> {
    let mut count = 0;
    std::fs::create_dir_all(dst)
        .map_err(|e| MfError::Io(e.to_string()))?;

    for entry in std::fs::read_dir(src)
        .map_err(|e| MfError::Io(e.to_string()))?
    {
        let entry = entry.map_err(|e| MfError::Io(e.to_string()))?;
        let src_path = entry.path();
        let dst_path = dst.join(entry.file_name());

        if src_path.is_dir() {
            count += copy_dir_recursive(&src_path, &dst_path)?;
        } else {
            std::fs::copy(&src_path, &dst_path)
                .map_err(|e| MfError::Io(e.to_string()))?;
            count += 1;
        }
    }
    Ok(count)
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;
    use std::fs;

    fn setup_project(dir: &Path) {
        fs::write(
            dir.join("mate.toml"),
            r#"
[project]
name = "build-test"
runtime = "1.0.0"
"#,
        )
        .unwrap();
        fs::create_dir_all(dir.join("assets")).unwrap();
        fs::write(dir.join("assets/avatar.vrm"), b"fake-vrm").unwrap();
        fs::create_dir_all(dir.join("config")).unwrap();
        fs::write(dir.join("config/personality.toml"), "[personality]\nname = \"Luna\"\n").unwrap();
    }

    #[test]
    fn build_fails_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let out = TempDir::new().unwrap();
        let result = build_project(tmp.path(), out.path());
        assert!(result.is_err());
        match result.unwrap_err() {
            MfError::ManifestNotFound { .. } => {}
            other => panic!("expected ManifestNotFound, got {:?}", other),
        }
    }

    #[test]
    fn build_copies_assets() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        setup_project(project.path());

        let result = build_project(project.path(), output.path()).unwrap();
        assert!(result.files_copied >= 3); // manifest + avatar.vrm + personality.toml
        assert_eq!(result.manifest, "build-test");

        assert!(output.path().join("mate.toml").exists());
        assert!(output.path().join("assets/avatar.vrm").exists());
        assert!(output.path().join("config/personality.toml").exists());
    }

    #[test]
    fn build_without_mods_dir_works() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        // No mods dir
        fs::write(
            project.path().join("mate.toml"),
            r#"
[project]
name = "no-mods"
runtime = "1.0.0"
"#,
        )
        .unwrap();

        let result = build_project(project.path(), output.path()).unwrap();
        assert!(result.files_copied >= 1);
    }

    #[test]
    fn build_with_subdirs() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        setup_project(project.path());
        fs::create_dir_all(project.path().join("assets/sounds")).unwrap();
        fs::write(project.path().join("assets/sounds/drag.wav"), b"fake").unwrap();

        let result = build_project(project.path(), output.path()).unwrap();
        assert!(result.files_copied >= 4);
        assert!(output.path().join("assets/sounds/drag.wav").exists());
    }
}
```

- [ ] **Step 2: Write the CLI build command**

```rust
// crates/mf-cli/src/commands/build.rs
use std::path::Path;

pub fn run(output: Option<&str>, json: bool) -> anyhow::Result<()> {
    let project_dir = Path::new(".");
    let output_dir = match output {
        Some(o) => std::path::PathBuf::from(o),
        None => std::path::PathBuf::from("build"),
    };

    if !json {
        println!("Building project...");
    }

    let result = mf_core::build::build_project(project_dir, &output_dir)
        .map_err(|e| anyhow::anyhow!("{}", e))?;

    if json {
        println!("{}", serde_json::json!({
            "status": "ok",
            "manifest": result.manifest,
            "output": result.output_dir.display().to_string(),
            "files_copied": result.files_copied,
        }));
    } else {
        println!("Build complete!");
        println!("  Project: {}", result.manifest);
        println!("  Output: {}", result.output_dir.display());
        println!("  Files copied: {}", result.files_copied);
    }

    Ok(())
}
```

- [ ] **Step 3: Add build and package to CLI**

Update `main.rs`:
```rust
Commands::Build { output } => commands::build::run(output.as_deref(), cli.json),
Commands::Package => commands::package::run(cli.json),
```

```rust
// crates/mf-cli/src/commands/package.rs
use std::path::Path;

pub fn run(json: bool) -> anyhow::Result<()> {
    // 1. Build first
    let build_dir = Path::new("build");
    let result = mf_core::build::build_project(Path::new("."), build_dir)
        .map_err(|e| anyhow::anyhow!("{}", e))?;

    // 2. Create archive
    let archive_name = format!("{}.tar.gz", result.manifest);

    // Use tar command for now
    let status = std::process::Command::new("tar")
        .args(["-czf", &archive_name, "-C", "build", &result.manifest])
        .status()
        .map_err(|e| anyhow::anyhow!("failed to run tar: {}", e))?;

    if !status.success() {
        return Err(anyhow::anyhow!("tar failed with exit code {}", status.code().unwrap_or(-1)));
    }

    if json {
        println!("{}", serde_json::json!({
            "status": "ok",
            "archive": archive_name,
        }));
    } else {
        println!("Package created: {archive_name}");
    }

    Ok(())
}
```

- [ ] **Step 4: Run tests**

```bash
cargo test --workspace
# Expected: all tests pass (including 4 new build tests)
```

- [ ] **Step 5: Verify manually**

```bash
cd /tmp && cargo run -- new build-test
cd build-test && mkdir -p assets && touch assets/test.vrm
cargo run -- build
cargo run -- package
ls *.tar.gz
# Expected: build-test.tar.gz exists
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: mf build and mf package with raw asset packaging (no Unity Editor)"
```

---

### Task 1.5: `mf capabilities` Command

**Covers:** Platform capability reporting

**Files:**
- Create: `crates/mf-cli/src/commands/capabilities.rs`

- [ ] **Step 1: Implement capabilities command**

```rust
// crates/mf-cli/src/commands/capabilities.rs
use std::env;

pub fn run(json: bool) -> anyhow::Result<()> {
    let desktop = env::var("XDG_CURRENT_DESKTOP").unwrap_or_default();
    let session = env::var("XDG_SESSION_TYPE").unwrap_or_default();
    let hyprland = env::var("HYPRLAND_INSTANCE_SIGNATURE").is_ok();
    let wayland = session.contains("wayland");

    let capabilities = serde_json::json!({
        "platform": {
            "desktop_environment": desktop,
            "session_type": session,
            "is_wayland": wayland,
            "is_hyprland": hyprland,
        },
        "features": {
            "transparency": !wayland || hyprland,
            "click_through": session == "x11" || hyprland,
            "always_on_top": true,
            "system_tray": !wayland || session == "x11",
            "notifications": true,
            "audio_monitoring": session == "x11",
        },
        "runtime_required": "Unity 6000.2.6f2",
        "cli_version": env!("CARGO_PKG_VERSION"),
    });

    if json {
        println!("{}", serde_json::to_string_pretty(&capabilities)?);
    } else {
        println!("Platform capabilities:");
        println!("  Desktop: {desktop}");
        println!("  Session: {session}");
        println!("  Transparency: {}", capabilities["features"]["transparency"]);
        println!("  Click-through: {}", capabilities["features"]["click_through"]);
        println!("  Always-on-top: {}", capabilities["features"]["always_on_top"]);
        println!("  System tray: {}", capabilities["features"]["system_tray"]);
        println!("  Notifications: {}", capabilities["features"]["notifications"]);
        println!("  Audio monitoring: {}", capabilities["features"]["audio_monitoring"]);
    }

    Ok(())
}
```

Add to `main.rs`:
```rust
/// Show platform capabilities
Capabilities,
```

- [ ] **Step 2: Verify**

```bash
cargo run -- capabilities
cargo run -- --json capabilities
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: mf capabilities command for platform feature detection"
```

---

### Phase 1 Exit Criteria Checklist

- [ ] `cargo build --workspace` succeeds
- [ ] `cargo test --workspace` — all tests pass (40+ tests)
- [ ] `cargo clippy --workspace --all-targets -- -D warnings` — clean
- [ ] `cargo run -- new my-mate` — creates project
- [ ] `cargo run -- doctor` — validates project
- [ ] `cargo run -- runtime list` — shows installed runtimes
- [ ] `cargo run -- capabilities` — shows platform info
- [ ] `cargo run -- build` — creates build output
- [ ] `cargo run -- package` — creates tar.gz
- [ ] `mf dev` with a valid project + runtime — launches and watches
- [ ] `mf dev` with file change — restarts automatically
- [ ] `mf --json` flag works for all commands
- [ ] All error messages are human-readable
- [ ] Integration tests pass
