use serde::{Deserialize, Serialize};

use crate::MfError;

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

fn default_idle_count() -> i32 {
    10
}
fn default_dance_count() -> i32 {
    5
}
fn default_idle_switch() -> f32 {
    10.0
}
fn default_idle_transition() -> f32 {
    1.0
}
fn default_dance_switch() -> f32 {
    15.0
}
fn default_dance_transition() -> f32 {
    2.0
}

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

fn default_ai_provider() -> String {
    "ollama".into()
}
fn default_ai_model() -> String {
    "phi3:mini".into()
}
fn default_context_length() -> i32 {
    4096
}

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

fn default_tray_tooltip() -> String {
    "My Mate".into()
}

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

fn default_mods_path() -> String {
    "mods/".into()
}

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

fn default_fps_limit() -> i32 {
    90
}
fn default_graphics_quality() -> i32 {
    1
}

// ---- Parse & Validate ----

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
            reason: format!(
                "character.scale must be 0.0 < scale <= 10.0, got {}",
                manifest.character.scale
            ),
        });
    }
    if manifest.performance.fps_limit < 10 || manifest.performance.fps_limit > 240 {
        return Err(MfError::ManifestInvalid {
            reason: format!(
                "performance.fps_limit must be 10..=240, got {}",
                manifest.performance.fps_limit
            ),
        });
    }
    Ok(())
}

pub fn default_manifest(name: &str) -> MateManifest {
    MateManifest {
        project: ProjectConfig {
            name: name.into(),
            version: default_version(),
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
        assert_eq!(
            manifest.project.description,
            "A Mate Framework project: hello"
        );
    }
}
