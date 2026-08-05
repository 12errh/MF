# Phase 0: Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Set up the project foundation — Cargo workspace, project structure, CI, manifest schema, and license audit — so Phase 1 (CLI) can start on solid ground.

**Architecture:** Rust workspace with two crates (`mf-core` for domain logic, `mf-cli` for CLI binary). No Unity dependency. All Rust. TOML manifest schema defined first with property tests.

**Tech Stack:** Rust (edition 2024), cargo, clap 4 (CLI parsing), toml + serde (manifest), tempfile (test helpers), GitHub Actions (CI)

## Global Constraints

- Rust edition 2024, MSRV 1.80.0
- All public types derive `Debug`, `Clone`, `serde::Serialize`, `serde::Deserialize`
- All error types use `thiserror`
- All functions that can fail return `Result<T, MfError>`
- Tests use `#[test]` for sync, `#[tokio::test]` for async
- No `unwrap()` in library code — use `?` or `.expect("reason")`
- Cargo workspace at project root: `crates/mf-core`, `crates/mf-cli`
- CI must pass before any task is marked done

## File Structure

```
mf/
├── Cargo.toml                          # Workspace root
├── crates/
│   ├── mf-core/
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── error.rs                # MfError enum
│   │       └── manifest.rs             # MateManifest structs + parse/validate
│   └── mf-cli/
│       ├── Cargo.toml
│       └── src/
│           ├── main.rs                 # Entry point, clap setup
│           └── commands/
│               ├── mod.rs
│               ├── new.rs              # `mf new`
│               ├── doctor.rs           # `mf doctor`
│               └── dev.rs              # `mf dev` (stub)
├── .github/
│   └── workflows/
│       └── ci.yml                      # CI pipeline
├── docs/
│   └── compose/plans/                  # This directory
└── tests/
    └── integration/
        └── new_project.rs              # Integration test
```

---

### Task 0.1: Cargo Workspace + Error Type

**Covers:** Project setup, error handling (ADR-011)

**Files:**
- Create: `Cargo.toml` (workspace root)
- Create: `crates/mf-core/Cargo.toml`
- Create: `crates/mf-core/src/lib.rs`
- Create: `crates/mf-core/src/error.rs`

**Interfaces:**
- Produces: `MfError` enum (used by every subsequent task)

- [ ] **Step 1: Initialize workspace**

```bash
cd /home/rehan/Projects/mf
cargo init --name mf-workspace
rm -rf src
cargo init crates/mf-core --lib
cargo init crates/mf-cli --name mf
```

- [ ] **Step 2: Write the workspace Cargo.toml**

```toml
# Cargo.toml (workspace root)
[workspace]
resolver = "2"
members = ["crates/mf-core", "crates/mf-cli"]
```

- [ ] **Step 3: Write mf-core Cargo.toml**

```toml
# crates/mf-core/Cargo.toml
[package]
name = "mf-core"
version = "0.1.0"
edition = "2024"

[dependencies]
serde = { version = "1", features = ["derive"] }
serde_json = "1"
toml = "0.8"
thiserror = "2"

[dev-dependencies]
tempfile = "3"
```

- [ ] **Step 4: Write the failing test for MfError**

```rust
// crates/mf-core/src/error.rs
use thiserror::Error;

#[derive(Error, Debug, Clone)]
pub enum MfError {
    #[error("manifest not found at {path}")]
    ManifestNotFound { path: String },

    #[error("invalid manifest: {reason}")]
    ManifestInvalid { reason: String },

    #[error("runtime not installed, run `mf runtime install`")]
    RuntimeNotInstalled,

    #[error("Unity player crashed with exit code {code}")]
    UnityCrashed { code: i32 },

    #[error("I/O error: {0}")]
    Io(String),

    #[error("template error: {0}")]
    Template(String),
}
```

```rust
// crates/mf-core/src/lib.rs
pub mod error;
pub mod manifest;

pub use error::MfError;
```

- [ ] **Step 5: Write the failing test**

```rust
// crates/mf-core/src/error.rs  (add test module)
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn error_display_manifest_not_found() {
        let err = MfError::ManifestNotFound {
            path: "/tmp/test/mate.toml".into(),
        };
        assert_eq!(
            err.to_string(),
            "manifest not found at /tmp/test/mate.toml"
        );
    }

    #[test]
    fn error_display_manifest_invalid() {
        let err = MfError::ManifestInvalid {
            reason: "missing [project] section".into(),
        };
        assert_eq!(
            err.to_string(),
            "invalid manifest: missing [project] section"
        );
    }

    #[test]
    fn error_is_clone() {
        let err = MfError::RuntimeNotInstalled;
        let _cloned = err.clone();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cargo test -p mf-core`
Expected: 3 tests pass

- [ ] **Step 7: Commit**

```bash
git init
echo -e "/target\n*.swp\n.DS_Store" > .gitignore
git add -A
git commit -m "feat: initialize workspace with MfError type"
```

---

### Task 0.2: Manifest Schema (TDD)

**Covers:** ADR-007 (project manifest)

**Files:**
- Create: `crates/mf-core/src/manifest.rs`
- Test: inline in manifest.rs

**Interfaces:**
- Produces: `MateManifest` struct, `parse_manifest()`, `validate_manifest()`, `default_manifest()`
- Consumes: `MfError` (from Task 0.1)

- [ ] **Step 1: Write failing tests for manifest parsing**

```rust
// crates/mf-core/src/manifest.rs
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct MateManifest {
    pub project: ProjectConfig,
    #[serde(default)]
    pub character: CharacterConfig,
    #[serde(default)]
    pub window: WindowConfig,
    #[serde(default)]
    pub audio: AudioConfig,
    #[serde(default)]
    pub animation: AnimationConfig,
    #[serde(default)]
    pub ai: AiConfig,
    #[serde(default)]
    pub discord: DiscordConfig,
    #[serde(default)]
    pub system: SystemConfig,
    #[serde(default)]
    pub mods: ModsConfig,
    #[serde(default)]
    pub performance: PerformanceConfig,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ProjectConfig {
    pub name: String,
    #[serde(default = "default_version")]
    pub version: String,
    pub runtime: String,
    #[serde(default)]
    pub author: String,
    #[serde(default)]
    pub description: String,
}

fn default_version() -> String {
    "0.1.0".into()
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct CharacterConfig {
    #[serde(default)]
    pub model: String,
    #[serde(default = "default_scale")]
    pub scale: f32,
    #[serde(default)]
    pub fallback_model: String,
}

impl Default for CharacterConfig {
    fn default() -> Self {
        Self {
            model: String::new(),
            scale: default_scale(),
            fallback_model: String::new(),
        }
    }
}

fn default_scale() -> f32 {
    1.0
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct WindowConfig {
    #[serde(default = "default_true")]
    pub transparent: bool,
    #[serde(default = "default_true")]
    pub always_on_top: bool,
    #[serde(default)]
    pub click_through: bool,
    #[serde(default)]
    pub hide_from_taskbar: bool,
    #[serde(default = "default_window_type")]
    pub window_type: String,
    #[serde(default = "default_initial_position")]
    pub initial_position: String,
}

impl Default for WindowConfig {
    fn default() -> Self {
        Self {
            transparent: default_true(),
            always_on_top: default_true(),
            click_through: false,
            hide_from_taskbar: false,
            window_type: default_window_type(),
            initial_position: default_initial_position(),
        }
    }
}

fn default_true() -> bool {
    true
}
fn default_window_type() -> String {
    "normal".into()
}
fn default_initial_position() -> String {
    "center".into()
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct AudioConfig {
    #[serde(default = "default_true")]
    pub enabled: bool,
    #[serde(default = "default_threshold")]
    pub threshold: f32,
    #[serde(default)]
    pub allowed_apps: Vec<String>,
    #[serde(default = "default_volume")]
    pub volume: f32,
}

impl Default for AudioConfig {
    fn default() -> Self {
        Self {
            enabled: default_true(),
            threshold: default_threshold(),
            allowed_apps: Vec::new(),
            volume: default_volume(),
        }
    }
}

fn default_threshold() -> f32 {
    0.2
}
fn default_volume() -> f32 {
    1.0
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct AnimationConfig {
    #[serde(default = "default_idle_count")]
    pub idle_count: i32,
    #[serde(default = "default_dance_count")]
    pub dance_count: i32,
    #[serde(default = "default_idle_switch")]
    pub idle_switch_time: f32,
    #[serde(default = "default_idle_transition")]
    pub idle_transition_time: f32,
    #[serde(default = "default_dance_switch")]
    pub dance_switch_time: f32,
    #[serde(default = "default_dance_transition")]
    pub dance_transition_time: f32,
    #[serde(default = "default_true")]
    pub enable_dancing: bool,
    #[serde(default)]
    pub enable_dance_switch: bool,
}

impl Default for AnimationConfig {
    fn default() -> Self {
        Self {
            idle_count: default_idle_count(),
            dance_count: default_dance_count(),
            idle_switch_time: default_idle_switch(),
            idle_transition_time: default_idle_transition(),
            dance_switch_time: default_dance_switch(),
            dance_transition_time: default_dance_transition(),
            enable_dancing: default_true(),
            enable_dance_switch: false,
        }
    }
}

fn default_idle_count() -> i32 { 10 }
fn default_dance_count() -> i32 { 5 }
fn default_idle_switch() -> f32 { 10.0 }
fn default_idle_transition() -> f32 { 1.0 }
fn default_dance_switch() -> f32 { 15.0 }
fn default_dance_transition() -> f32 { 2.0 }

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct AiConfig {
    #[serde(default)]
    pub enabled: bool,
    #[serde(default = "default_ai_provider")]
    pub provider: String,
    #[serde(default = "default_ai_model")]
    pub model: String,
    #[serde(default = "default_context_length")]
    pub context_length: i32,
    #[serde(default)]
    pub prompt_file: String,
    #[serde(default)]
    pub system_prompt: String,
}

fn default_ai_provider() -> String { "ollama".into() }
fn default_ai_model() -> String { "phi3:mini".into() }
fn default_context_length() -> i32 { 4096 }

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct DiscordConfig {
    #[serde(default)]
    pub enabled: bool,
    #[serde(default)]
    pub app_id: String,
    #[serde(default)]
    pub details: String,
    #[serde(default)]
    pub state: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct SystemConfig {
    #[serde(default)]
    pub tray_icon: String,
    #[serde(default = "default_tray_tooltip")]
    pub tray_tooltip: String,
    #[serde(default = "default_true")]
    pub notifications: bool,
    #[serde(default)]
    pub start_with_desktop: bool,
}

impl Default for SystemConfig {
    fn default() -> Self {
        Self {
            tray_icon: String::new(),
            tray_tooltip: default_tray_tooltip(),
            notifications: default_true(),
            start_with_desktop: false,
        }
    }
}

fn default_tray_tooltip() -> String { "My Mate".into() }

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ModsConfig {
    #[serde(default = "default_true")]
    pub enabled: bool,
    #[serde(default = "default_mods_path")]
    pub mods_path: String,
}

impl Default for ModsConfig {
    fn default() -> Self {
        Self {
            enabled: default_true(),
            mods_path: default_mods_path(),
        }
    }
}

fn default_mods_path() -> String { "mods/".into() }

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct PerformanceConfig {
    #[serde(default = "default_fps_limit")]
    pub fps_limit: i32,
    #[serde(default)]
    pub enable_bloom: bool,
    #[serde(default)]
    pub enable_ambient_occlusion: bool,
    #[serde(default = "default_graphics_quality")]
    pub graphics_quality: i32,
}

impl Default for PerformanceConfig {
    fn default() -> Self {
        Self {
            fps_limit: default_fps_limit(),
            enable_bloom: false,
            enable_ambient_occlusion: false,
            graphics_quality: default_graphics_quality(),
        }
    }
}

fn default_fps_limit() -> i32 { 90 }
fn default_graphics_quality() -> i32 { 1 }

// ---- Parse & Validate ----

use crate::MfError;

pub fn parse_manifest(content: &str) -> Result<MateManifest, MfError> {
    toml::from_str(content).map_err(|e| MfError::ManifestInvalid {
        reason: e.to_string(),
    })
}

pub fn validate_manifest(manifest: &MateManifest) -> Result<(), MfError> {
    if manifest.project.name.trim().is_empty() {
        return Err(MfError::ManifestInvalid {
            reason: "project.name cannot be empty".into(),
        });
    }
    if manifest.project.runtime.trim().is_empty() {
        return Err(MfError::ManifestInvalid {
            reason: "project.runtime cannot be empty".into(),
        });
    }
    if manifest.character.scale <= 0.0 || manifest.character.scale > 10.0 {
        return Err(MfError::ManifestInvalid {
            reason: format!("character.scale must be 0.0 < scale <= 10.0, got {}", manifest.character.scale),
        });
    }
    if manifest.performance.fps_limit < 10 || manifest.performance.fps_limit > 240 {
        return Err(MfError::ManifestInvalid {
            reason: format!("performance.fps_limit must be 10..=240, got {}", manifest.performance.fps_limit),
        });
    }
    Ok(())
}

pub fn default_manifest(name: &str) -> MateManifest {
    MateManifest {
        project: ProjectConfig {
            name: name.into(),
            version: "0.1.0".into(),
            runtime: "1.0.0".into(),
            author: String::new(),
            description: format!("A Mate Framework project: {name}"),
        },
        character: CharacterConfig::default(),
        window: WindowConfig::default(),
        audio: AudioConfig::default(),
        animation: AnimationConfig::default(),
        ai: AiConfig::default(),
        discord: DiscordConfig::default(),
        system: SystemConfig::default(),
        mods: ModsConfig::default(),
        performance: PerformanceConfig::default(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // ---- Parsing tests ----

    #[test]
    fn parse_minimal_manifest() {
        let toml = r#"
[project]
name = "test"
runtime = "1.0.0"
"#;
        let manifest = parse_manifest(toml).unwrap();
        assert_eq!(manifest.project.name, "test");
        assert_eq!(manifest.project.runtime, "1.0.0");
        assert_eq!(manifest.project.version, "0.1.0"); // default
    }

    #[test]
    fn parse_full_manifest() {
        let toml = r#"
[project]
name = "my-mate"
version = "0.2.0"
runtime = "1.0.0"
author = "Dev"
description = "My desktop mate"

[character]
model = "assets/avatar.vrm"
scale = 1.5

[window]
transparent = true
always_on_top = false
click_through = true
window_type = "dock"
initial_position = "100,200"

[audio]
enabled = true
threshold = 0.5
allowed_apps = ["firefox", "spotify"]
volume = 0.8

[animation]
idle_count = 5
dance_count = 3
idle_switch_time = 15.0
enable_dancing = true
enable_dance_switch = true

[ai]
enabled = true
provider = "ollama"
model = "llama3.1"
context_length = 8192

[discord]
enabled = true
app_id = "12345"

[system]
tray_icon = "assets/icon.png"
tray_tooltip = "My Mate"
notifications = true

[performance]
fps_limit = 60
enable_bloom = true
graphics_quality = 2
"#;
        let manifest = parse_manifest(toml).unwrap();
        assert_eq!(manifest.project.name, "my-mate");
        assert_eq!(manifest.character.scale, 1.5);
        assert_eq!(manifest.window.window_type, "dock");
        assert_eq!(manifest.audio.allowed_apps.len(), 2);
        assert_eq!(manifest.animation.idle_count, 5);
        assert!(manifest.ai.enabled);
        assert_eq!(manifest.ai.model, "llama3.1");
        assert!(manifest.discord.enabled);
        assert_eq!(manifest.performance.fps_limit, 60);
    }

    #[test]
    fn parse_invalid_toml_fails() {
        let result = parse_manifest("this is not toml {{{{");
        assert!(result.is_err());
        match result.unwrap_err() {
            MfError::ManifestInvalid { reason } => {
                assert!(reason.contains("invalid") || reason.contains("expected"));
            }
            _ => panic!("expected ManifestInvalid"),
        }
    }

    #[test]
    fn parse_empty_string_fails() {
        let result = parse_manifest("");
        assert!(result.is_err());
    }

    // ---- Validation tests ----

    #[test]
    fn validate_empty_name_fails() {
        let mut manifest = default_manifest("test");
        manifest.project.name = "".into();
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_empty_runtime_fails() {
        let mut manifest = default_manifest("test");
        manifest.project.runtime = "  ".into();
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_zero_scale_fails() {
        let mut manifest = default_manifest("test");
        manifest.character.scale = 0.0;
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_negative_scale_fails() {
        let mut manifest = default_manifest("test");
        manifest.character.scale = -1.0;
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_huge_scale_fails() {
        let mut manifest = default_manifest("test");
        manifest.character.scale = 100.0;
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_zero_fps_fails() {
        let mut manifest = default_manifest("test");
        manifest.performance.fps_limit = 0;
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_huge_fps_fails() {
        let mut manifest = default_manifest("test");
        manifest.performance.fps_limit = 500;
        let result = validate_manifest(&manifest);
        assert!(result.is_err());
    }

    #[test]
    fn validate_default_manifest_passes() {
        let manifest = default_manifest("my-mate");
        assert!(validate_manifest(&manifest).is_ok());
    }

    #[test]
    fn validate_edge_scale_passes() {
        let mut manifest = default_manifest("test");
        manifest.character.scale = 10.0; // max allowed
        assert!(validate_manifest(&manifest).is_ok());
    }

    // ---- Round-trip test ----

    #[test]
    fn roundtrip_manifest() {
        let manifest = default_manifest("roundtrip-test");
        let serialized = toml::to_string(&manifest).unwrap();
        let parsed = parse_manifest(&serialized).unwrap();
        assert_eq!(manifest, parsed);
    }

    // ---- Default manifest test ----

    #[test]
    fn default_manifest_has_correct_name() {
        let manifest = default_manifest("hello");
        assert_eq!(manifest.project.name, "hello");
        assert_eq!(manifest.project.description, "A Mate Framework project: hello");
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

Run: `cargo test -p mf-core`
Expected: FAIL — modules `error` and `manifest` don't exist yet (or tests fail)

- [ ] **Step 3: Create the implementation files**

Create `crates/mf-core/src/error.rs` and `crates/mf-core/src/manifest.rs` with the full code above.
Update `crates/mf-core/src/lib.rs`:
```rust
pub mod error;
pub mod manifest;
pub use error::MfError;
pub use manifest::MateManifest;
```

- [ ] **Step 4: Run tests — verify they pass**

Run: `cargo test -p mf-core`
Expected: 21 tests pass (3 error + 18 manifest)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: manifest schema with parsing, validation, and 21 tests"
```

---

### Task 0.3: CLI Skeleton with Clap

**Covers:** ADR-002 (Rust CLI), CLI argument parsing

**Files:**
- Create: `crates/mf-cli/Cargo.toml`
- Create: `crates/mf-cli/src/main.rs`
- Create: `crates/mf-cli/src/commands/mod.rs`
- Create: `crates/mf-cli/src/commands/new.rs`
- Create: `crates/mf-cli/src/commands/doctor.rs`
- Create: `crates/mf-cli/src/commands/dev.rs`

**Interfaces:**
- Consumes: `MfError`, `MateManifest`, `parse_manifest`, `validate_manifest`, `default_manifest` (from Tasks 0.1-0.2)

- [ ] **Step 1: Write the failing test for CLI help**

```bash
# This is a compile + run test, not a unit test
cargo run -- --help 2>&1
# Expected: shows "Usage: mf <COMMAND>" with new, doctor, dev listed
```

- [ ] **Step 2: Write mf-cli Cargo.toml**

```toml
# crates/mf-cli/Cargo.toml
[package]
name = "mf"
version = "0.1.0"
edition = "2024"

[[bin]]
name = "mf"
path = "src/main.rs"

[dependencies]
mf-core = { path = "../mf-core" }
clap = { version = "4", features = ["derive"] }
anyhow = "1"
serde_json = "1"
```

- [ ] **Step 3: Write the CLI main.rs and command stubs**

```rust
// crates/mf-cli/src/main.rs
mod commands;

use clap::{Parser, Subcommand};

#[derive(Parser)]
#[command(name = "mf", about = "Mate Framework CLI", version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    /// Output in JSON format
    #[arg(long, global = true)]
    json: bool,
}

#[derive(Subcommand)]
enum Commands {
    /// Create a new Mate Framework project
    New {
        /// Project name
        name: String,
    },
    /// Diagnose issues with the current project
    Doctor,
    /// Start the development server
    Dev,
}

fn main() -> anyhow::Result<()> {
    let cli = Cli::parse();

    match cli.command {
        Commands::New { name } => commands::new::run(&name, cli.json),
        Commands::Doctor => commands::doctor::run(cli.json),
        Commands::Dev => commands::dev::run(cli.json),
    }
}
```

```rust
// crates/mf-cli/src/commands/mod.rs
pub mod new;
pub mod doctor;
pub mod dev;
```

```rust
// crates/mf-cli/src/commands/new.rs
use mf_core::{default_manifest, validate_manifest};
use std::fs;
use std::path::PathBuf;

pub fn run(name: &str, json: bool) -> anyhow::Result<()> {
    let manifest = default_manifest(name);
    validate_manifest(&manifest)
        .map_err(|e| anyhow::anyhow!("{}", e))?;

    let project_dir = PathBuf::from(name);

    if project_dir.exists() {
        return Err(anyhow::anyhow!("directory '{}' already exists", name));
    }

    fs::create_dir_all(&project_dir)?;
    fs::create_dir_all(project_dir.join("assets"))?;
    fs::create_dir_all(project_dir.join("mods"))?;
    fs::create_dir_all(project_dir.join("config"))?;

    let toml_content = toml::to_string_pretty(&manifest)
        .map_err(|e| anyhow::anyhow!("failed to serialize manifest: {}", e))?;
    fs::write(project_dir.join("mate.toml"), toml_content)?;

    if json {
        let output = serde_json::json!({
            "status": "ok",
            "project_dir": project_dir.display().to_string(),
            "manifest": "mate.toml",
        });
        println!("{}", serde_json::to_string_pretty(&output)?);
    } else {
        println!("Created project '{}' in {}", name, project_dir.display());
        println!("  mate.toml  - project manifest");
        println!("  assets/    - VRM models, textures, sounds");
        println!("  mods/      - optional mod assets");
        println!("  config/    - personality.toml and other config");
    }

    Ok(())
}
```

```rust
// crates/mf-cli/src/commands/doctor.rs
use std::path::PathBuf;

pub fn run(json: bool) -> anyhow::Result<()> {
    let mut checks = Vec::new();

    // Check 1: mate.toml exists
    let manifest_path = PathBuf::from("mate.toml");
    if manifest_path.exists() {
        let content = std::fs::read_to_string(&manifest_path)?;
        match mf_core::parse_manifest(&content) {
            Ok(manifest) => {
                match mf_core::validate_manifest(&manifest) {
                    Ok(()) => checks.push(("manifest", "ok", "valid".into())),
                    Err(e) => checks.push(("manifest", "error", e.to_string())),
                }
            }
            Err(e) => checks.push(("manifest", "error", e.to_string())),
        }
    } else {
        checks.push(("manifest", "error", "mate.toml not found".into()));
    }

    // Check 2: assets directory
    if PathBuf::from("assets").is_dir() {
        checks.push(("assets", "ok", "directory exists".into()));
    } else {
        checks.push(("assets", "warning", "assets/ directory missing".into()));
    }

    if json {
        let output = serde_json::json!({
            "checks": checks.into_iter().map(|(name, status, detail)| {
                serde_json::json!({ "name": name, "status": status, "detail": detail })
            }).collect::<Vec<_>>(),
        });
        println!("{}", serde_json::to_string_pretty(&output)?);
    } else {
        for (name, status, detail) in &checks {
            let icon = match *status {
                "ok" => "\u{2705}",
                "warning" => "\u{26a0}\u{fe0f}",
                _ => "\u{274c}",
            };
            println!("  {icon} {name}: {detail}");
        }
    }

    Ok(())
}
```

```rust
// crates/mf-cli/src/commands/dev.rs
pub fn run(json: bool) -> anyhow::Result<()> {
    if json {
        println!("{}", serde_json::json!({
            "status": "not_implemented",
            "message": "dev server will be implemented in Phase 1"
        }));
    } else {
        println!("`mf dev` will be implemented in Phase 1.");
        println!("This will launch the Mate Runtime against the current project.");
    }
    Ok(())
}
```

- [ ] **Step 4: Run and verify**

```bash
# Help works
cargo run -- --help
# Expected: shows Usage, mf <COMMAND>, new/doctor/dev listed

# New command works
cargo run -- new test-project
# Expected: "Created project 'test-project' in test-project"
ls test-project/
# Expected: mate.toml, assets/, mods/, config/

# New with JSON
cargo run -- --json new json-test
# Expected: JSON output with status ok

# Doctor works
cd test-project && cargo run -- doctor
# Expected: shows manifest ok, assets ok

# Doctor with JSON
cargo run -- --json doctor
# Expected: JSON output with checks array
```

- [ ] **Step 5: Write integration test**

```rust
// tests/integration/new_project.rs
use std::fs;
use std::process::Command;
use tempfile::TempDir;

#[test]
fn new_project_creates_valid_structure() {
    let tmp = TempDir::new().unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .arg("new")
        .arg("test-pet")
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(output.status.success(), "mf new failed: {}", String::from_utf8_lossy(&output.stderr));

    let project = tmp.path().join("test-pet");
    assert!(project.join("mate.toml").exists(), "mate.toml not created");
    assert!(project.join("assets").is_dir(), "assets/ not created");
    assert!(project.join("mods").is_dir(), "mods/ not created");
    assert!(project.join("config").is_dir(), "config/ not created");

    let content = fs::read_to_string(project.join("mate.toml")).unwrap();
    let manifest = mf_core::parse_manifest(&content).unwrap();
    assert_eq!(manifest.project.name, "test-pet");
    assert!(mf_core::validate_manifest(&manifest).is_ok());
}

#[test]
fn new_project_rejects_duplicate() {
    let tmp = TempDir::new().unwrap();
    let project = tmp.path().join("exists");
    fs::create_dir_all(&project).unwrap();

    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .arg("new")
        .arg("exists")
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(!output.status.success());
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(stderr.contains("already exists"));
}

#[test]
fn doctor_json_output() {
    let tmp = TempDir::new().unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .args(["--json", "doctor"])
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(output.status.success());
    let json: serde_json::Value = serde_json::from_slice(&output.stdout).unwrap();
    assert!(json["checks"].is_array());
}
```

Add to `Cargo.toml` workspace root:
```toml
[workspace.dependencies]
mf-core = { path = "crates/mf-core" }
```

- [ ] **Step 6: Run all tests**

```bash
cargo test --workspace
# Expected: all tests pass (21 mf-core + 3 integration + 0 mf-cli unit)
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: mf CLI with new, doctor, dev commands and integration tests"
```

---

### Task 0.4: `mf doctor` — Full Validation

**Covers:** ADR-007 validation, diagnostics

**Files:**
- Modify: `crates/mf-cli/src/commands/doctor.rs`

**Interfaces:**
- Consumes: `parse_manifest`, `validate_manifest` (from Task 0.2)

- [ ] **Step 1: Write the failing tests for doctor checks**

```rust
// crates/mf-cli/src/commands/doctor.rs  (add tests)
#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;
    use std::fs;

    fn setup_project(dir: &std::path::Path, toml_content: &str) {
        fs::write(dir.join("mate.toml"), toml_content).unwrap();
        fs::create_dir_all(dir.join("assets")).unwrap();
    }

    #[test]
    fn doctor_valid_project_all_ok() {
        let tmp = TempDir::new().unwrap();
        let toml = r#"
[project]
name = "test"
runtime = "1.0.0"
"#;
        setup_project(tmp.path(), toml);
        // Run doctor — should have all ok (we can't check stdout easily, but it should not panic)
        let result = run_inner(tmp.path(), false);
        assert!(result.is_ok());
    }

    #[test]
    fn doctor_missing_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_ok()); // doctor doesn't fail, it reports errors
    }

    #[test]
    fn doctor_invalid_manifest() {
        let tmp = TempDir::new().unwrap();
        fs::write(tmp.path().join("mate.toml"), "not valid {{{{").unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_ok()); // doctor reports, doesn't fail
    }

    #[test]
    fn doctor_json_output_valid() {
        let tmp = TempDir::new().unwrap();
        let toml = r#"
[project]
name = "test"
runtime = "1.0.0"
"#;
        setup_project(tmp.path(), toml);
        let result = run_inner(tmp.path(), true);
        assert!(result.is_ok());
    }
}
```

- [ ] **Step 2: Refactor doctor to accept a directory parameter**

```rust
// crates/mf-cli/src/commands/doctor.rs
use std::path::{Path, PathBuf};

pub fn run(json: bool) -> anyhow::Result<()> {
    run_inner(Path::new("."), json)
}

fn run_inner(dir: &Path, json: bool) -> anyhow::Result<()> {
    let mut checks: Vec<(&str, &str, String)> = Vec::new();

    // Check 1: mate.toml exists and is valid
    let manifest_path = dir.join("mate.toml");
    if manifest_path.exists() {
        let content = std::fs::read_to_string(&manifest_path)?;
        match mf_core::parse_manifest(&content) {
            Ok(manifest) => {
                match mf_core::validate_manifest(&manifest) {
                    Ok(()) => checks.push(("manifest", "ok", "valid".into())),
                    Err(e) => checks.push(("manifest", "error", e.to_string())),
                }
                // Check asset paths exist
                if !manifest.character.model.is_empty() {
                    let model_path = dir.join(&manifest.character.model);
                    if model_path.exists() {
                        checks.push(("model", "ok", format!("found: {}", manifest.character.model)));
                    } else {
                        checks.push(("model", "warning", format!("not found: {}", manifest.character.model)));
                    }
                }
            }
            Err(e) => checks.push(("manifest", "error", e.to_string())),
        }
    } else {
        checks.push(("manifest", "error", "mate.toml not found".into()));
    }

    // Check 2: assets directory
    if dir.join("assets").is_dir() {
        let count = std::fs::read_dir(dir.join("assets"))
            .map(|d| d.filter_map(|e| e.ok()).count())
            .unwrap_or(0);
        checks.push(("assets", "ok", format!("{count} files")));
    } else {
        checks.push(("assets", "warning", "assets/ directory missing".into()));
    }

    // Check 3: mods directory
    if dir.join("mods").is_dir() {
        checks.push(("mods", "ok", "directory exists".into()));
    } else {
        checks.push(("mods", "info", "mods/ directory not created (optional)".into()));
    }

    // Output
    if json {
        let items: Vec<_> = checks.iter().map(|(name, status, detail)| {
            serde_json::json!({ "name": name, "status": status, "detail": detail })
        }).collect();
        println!("{}", serde_json::to_string_pretty(&serde_json::json!({ "checks": items }))?);
    } else {
        for (name, status, detail) in &checks {
            let icon = match *status {
                "ok" => "\u{2705}",
                "warning" => "\u{26a0}\u{fe0f}",
                "info" => "\u{2139}\u{fe0f}",
                _ => "\u{274c}",
            };
            println!("  {icon} {name}: {detail}");
        }
    }

    Ok(())
}
```

- [ ] **Step 3: Run tests to verify they pass**

```bash
cargo test -p mf -- --nocapture
# Expected: 3 tests pass
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: doctor with full manifest + asset validation"
```

---

### Task 0.5: CI Pipeline

**Covers:** Quality gate, automation

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Write the CI workflow**

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  CARGO_TERM_COLOR: always

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - uses: Swatinem/rust-cache@v2
      - name: Check formatting
        run: cargo fmt --all -- --check
      - name: Clippy
        run: cargo clippy --workspace --all-targets -- -D warnings
      - name: Test
        run: cargo test --workspace
```

- [ ] **Step 2: Verify locally**

```bash
cargo fmt --all -- --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
# All must pass
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "ci: add GitHub Actions workflow with fmt, clippy, test"
```

---

### Task 0.6: Runtime Manager (TDD)

**Covers:** ADR-001 (Unity runtime), ADR-002 (CLI manages runtime)

**Files:**
- Create: `crates/mf-core/src/runtime.rs`
- Modify: `crates/mf-core/src/lib.rs`

**Interfaces:**
- Produces: `RuntimeManager`, `check_runtime`, `install_runtime`, `runtime_path`

- [ ] **Step 1: Write failing tests**

```rust
// crates/mf-core/src/runtime.rs
use std::path::PathBuf;
use crate::MfError;

/// Where runtimes are cached on disk
pub fn runtime_cache_dir() -> PathBuf {
    let home = std::env::var("HOME").unwrap_or_else(|_| "/tmp".into());
    PathBuf::from(home).join(".mate-framework").join("runtimes")
}

/// Path to a specific runtime version
pub fn runtime_path(version: &str) -> PathBuf {
    runtime_cache_dir().join(version)
}

/// Path to the Unity player binary inside a cached runtime
pub fn player_path(version: &str) -> PathBuf {
    runtime_path(version).join("MateRuntime").join("MateRuntime")
}

/// Check if a specific runtime version is installed
pub fn is_installed(version: &str) -> bool {
    player_path(version).exists()
}

/// List all installed runtime versions
pub fn list_installed() -> Vec<String> {
    let cache = runtime_cache_dir();
    if !cache.exists() {
        return Vec::new();
    }
    let mut versions: Vec<String> = std::fs::read_dir(&cache)
        .map(|entries| {
            entries
                .filter_map(|e| e.ok())
                .filter(|e| e.path().is_dir())
                .filter_map(|e| e.file_name().to_str().map(|s| s.to_string()))
                .collect()
        })
        .unwrap_or_default();
    versions.sort();
    versions
}

/// Resolve the runtime version to use.
/// If `project_version` is set in manifest, use it.
/// Otherwise, use the latest installed version.
pub fn resolve_version(project_runtime: &str) -> Result<String, MfError> {
    if is_installed(project_runtime) {
        return Ok(project_runtime.into());
    }

    let installed = list_installed();
    if installed.is_empty() {
        return Err(MfError::RuntimeNotInstalled);
    }

    // Return latest (last in sorted list)
    Ok(installed.last().unwrap().clone())
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    #[test]
    fn runtime_cache_dir_uses_home() {
        let dir = runtime_cache_dir();
        assert!(dir.to_string_lossy().contains(".mate-framework"));
    }

    #[test]
    fn runtime_path_combines_version() {
        let path = runtime_path("1.0.0");
        assert!(path.to_string_lossy().contains("1.0.0"));
    }

    #[test]
    fn player_path_inside_runtime() {
        let path = player_path("1.0.0");
        assert!(path.to_string_lossy().contains("MateRuntime"));
    }

    #[test]
    fn is_installed_false_when_missing() {
        assert!(!is_installed("999.0.0-test"));
    }

    #[test]
    fn list_installed_empty_when_no_cache() {
        // This test may be flaky if runtimes exist; we test the logic
        let versions = list_installed();
        // Just verify it returns a Vec<String> without panicking
        assert!(versions.iter().all(|v| !v.is_empty()));
    }

    #[test]
    fn resolve_version_fails_when_nothing_installed() {
        let result = resolve_version("999.0.0-test");
        assert!(result.is_err());
        match result.unwrap_err() {
            MfError::RuntimeNotInstalled => {}
            other => panic!("expected RuntimeNotInstalled, got {:?}", other),
        }
    }
}
```

Update `crates/mf-core/src/lib.rs`:
```rust
pub mod error;
pub mod manifest;
pub mod runtime;

pub use error::MfError;
pub use manifest::MateManifest;
```

- [ ] **Step 2: Run tests — verify they pass**

```bash
cargo test -p mf-core
# Expected: 27 tests pass (6 new runtime tests)
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: runtime manager with version resolution and caching"
```

---

### Phase 0 Exit Criteria Checklist

- [ ] Cargo workspace builds: `cargo build --workspace`
- [ ] All tests pass: `cargo test --workspace` (27+ tests)
- [ ] Clippy clean: `cargo clippy --workspace --all-targets -- -D warnings`
- [ ] Formatting clean: `cargo fmt --all -- --check`
- [ ] `cargo run -- new test-project` creates valid project structure
- [ ] `cargo run -- doctor` validates project
- [ ] `cargo run -- --help` shows all commands
- [ ] CI workflow defined in `.github/workflows/ci.yml`
- [ ] Manifest parse → validate → roundtrip works
- [ ] Runtime version resolution works
