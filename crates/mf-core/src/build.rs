use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

use crate::MfError;

/// Build metadata recorded for reproducibility.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct BuildManifest {
    pub runtime_version: String,
    pub project_name: String,
    pub project_version: String,
    pub built_at: String,
    pub assets: Vec<String>,
}

impl BuildManifest {
    pub fn new(runtime_version: &str, project_name: &str, project_version: &str) -> Self {
        Self {
            runtime_version: runtime_version.to_string(),
            project_name: project_name.to_string(),
            project_version: project_version.to_string(),
            built_at: chrono::Utc::now().to_rfc3339(),
            assets: Vec::new(),
        }
    }

    pub fn add_asset(&mut self, path: String) {
        self.assets.push(path);
    }

    pub fn write(&self, dir: &Path) -> Result<(), MfError> {
        let json = serde_json::to_string_pretty(self)
            .map_err(|e| MfError::Io(format!("failed to serialize build manifest: {e}")))?;
        std::fs::write(dir.join("build-manifest.json"), json)
            .map_err(|e| MfError::Io(format!("failed to write build manifest: {e}")))
    }

    pub fn read(dir: &Path) -> Result<Self, MfError> {
        let json = std::fs::read_to_string(dir.join("build-manifest.json"))
            .map_err(|e| MfError::Io(format!("failed to read build manifest: {e}")))?;
        serde_json::from_str(&json).map_err(|e| MfError::Io(format!("invalid build manifest: {e}")))
    }
}

/// Result of a project build.
#[derive(Debug)]
pub struct BuildResult {
    pub output_dir: PathBuf,
    pub files_copied: usize,
    pub manifest: String,
}

/// Build a project — validate and package raw assets (no Unity Editor needed).
pub fn build_project(project_dir: &Path, output_dir: &Path) -> Result<BuildResult, MfError> {
    // 1. Load and validate manifest
    let manifest_path = project_dir.join("mate.toml");
    if !manifest_path.exists() {
        return Err(MfError::ManifestNotFound {
            path: manifest_path.display().to_string(),
        });
    }

    let content =
        std::fs::read_to_string(&manifest_path).map_err(|e| MfError::Io(e.to_string()))?;
    let manifest = crate::parse_manifest(&content)?;
    crate::validate_manifest(&manifest)?;

    // 2. Create output directory
    std::fs::create_dir_all(output_dir).map_err(|e| MfError::Io(e.to_string()))?;

    // 3. Copy manifest
    std::fs::create_dir_all(output_dir.join("config")).map_err(|e| MfError::Io(e.to_string()))?;
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

/// Result of packaging a built project into an archive.
#[derive(Debug)]
pub struct PackageResult {
    pub archive_path: PathBuf,
    pub archive_size: u64,
    pub includes_runtime: bool,
    pub manifest: BuildManifest,
}

/// Build a project, write a build manifest, then archive the build dir.
pub fn package_project(project_dir: &Path, output_dir: &Path) -> Result<PackageResult, MfError> {
    // 1. Build first
    let build_result = build_project(project_dir, output_dir)?;

    // 2. Load the project manifest to read runtime + version
    let content = std::fs::read_to_string(project_dir.join("mate.toml"))
        .map_err(|e| MfError::Io(format!("failed to read mate.toml: {e}")))?;
    let manifest = crate::parse_manifest(&content)?;

    // 3. Build manifest with runtime version + project version
    let mut build_manifest = BuildManifest::new(
        &manifest.project.runtime,
        &build_result.manifest,
        &manifest.project.version,
    );

    // 4. Scan copied assets
    scan_assets(output_dir, &mut build_manifest)?;

    // 5. Write build manifest
    build_manifest.write(output_dir)?;

    // 6. Create archive. Stage it in a temp dir first — writing the archive
    //    inside the dir being tar'd makes tar fail ("file changed as we read it").
    let archive_name = format!("{}.tar.gz", build_result.manifest);
    let archive_path = output_dir.join(&archive_name);

    let staging = std::env::temp_dir().join(format!(
        "mf-package-{}-{}-{}",
        build_result.manifest,
        std::process::id(),
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0)
    ));
    std::fs::create_dir_all(&staging).map_err(|e| MfError::Io(format!("mkdir staging: {e}")))?;
    let staged_archive = staging.join(&archive_name);

    let status = std::process::Command::new("tar")
        .args(["-czf", &staged_archive.to_string_lossy()])
        .args(["-C", &output_dir.to_string_lossy()])
        .arg(".")
        .status()
        .map_err(|e| MfError::Io(format!("tar failed: {e}")))?;

    if !status.success() {
        let _ = std::fs::remove_dir_all(&staging);
        return Err(MfError::Io(format!(
            "tar exited with code {}",
            status.code().unwrap_or(-1)
        )));
    }

    std::fs::copy(&staged_archive, &archive_path)
        .map_err(|e| MfError::Io(format!("failed to move archive: {e}")))?;
    let _ = std::fs::remove_dir_all(&staging);

    let archive_size = std::fs::metadata(&archive_path)
        .map(|m| m.len())
        .unwrap_or(0);

    Ok(PackageResult {
        archive_path,
        archive_size,
        includes_runtime: false,
        manifest: build_manifest,
    })
}

/// Recursively record every file under `dir` (relative paths) into the manifest.
fn scan_assets(dir: &Path, manifest: &mut BuildManifest) -> Result<(), MfError> {
    if !dir.is_dir() {
        return Ok(());
    }
    for entry in std::fs::read_dir(dir).map_err(|e| MfError::Io(e.to_string()))? {
        let entry = entry.map_err(|e| MfError::Io(e.to_string()))?;
        let path = entry.path();
        if path.is_dir() {
            scan_assets(&path, manifest)?;
        } else {
            let rel = path.strip_prefix(dir).unwrap_or(&path);
            manifest.add_asset(rel.display().to_string());
        }
    }
    Ok(())
}

fn copy_dir_recursive(src: &Path, dst: &Path) -> Result<usize, MfError> {
    let mut count = 0;
    std::fs::create_dir_all(dst).map_err(|e| MfError::Io(e.to_string()))?;

    for entry in std::fs::read_dir(src).map_err(|e| MfError::Io(e.to_string()))? {
        let entry = entry.map_err(|e| MfError::Io(e.to_string()))?;
        let src_path = entry.path();
        let dst_path = dst.join(entry.file_name());

        if src_path.is_dir() {
            count += copy_dir_recursive(&src_path, &dst_path)?;
        } else {
            std::fs::copy(&src_path, &dst_path).map_err(|e| MfError::Io(e.to_string()))?;
            count += 1;
        }
    }
    Ok(count)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use tempfile::TempDir;

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
        fs::write(
            dir.join("config/personality.toml"),
            "[personality]\nname = \"Luna\"\n",
        )
        .unwrap();
    }

    #[test]
    fn build_fails_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let out = TempDir::new().unwrap();
        let result = build_project(tmp.path(), out.path());
        match result {
            Err(MfError::ManifestNotFound { .. }) => {}
            other => panic!("expected ManifestNotFound, got {other:?}"),
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

    // ---- BuildManifest ----

    #[test]
    fn build_manifest_new_sets_fields() {
        let manifest = BuildManifest::new("1.0.0", "test-project", "0.1.0");
        assert_eq!(manifest.runtime_version, "1.0.0");
        assert_eq!(manifest.project_name, "test-project");
        assert_eq!(manifest.project_version, "0.1.0");
        assert!(manifest.built_at.contains('T'));
    }

    #[test]
    fn build_manifest_add_asset() {
        let mut manifest = BuildManifest::new("1.0.0", "test", "0.1.0");
        manifest.add_asset("assets/avatar.vrm".to_string());
        manifest.add_asset("assets/sounds/drag.wav".to_string());
        assert_eq!(manifest.assets.len(), 2);
    }

    #[test]
    fn build_manifest_write_and_read_roundtrip() {
        let tmp = TempDir::new().unwrap();
        let mut manifest = BuildManifest::new("1.0.0", "roundtrip", "0.2.0");
        manifest.add_asset("assets/test.vrm".to_string());
        manifest.write(tmp.path()).unwrap();

        let loaded = BuildManifest::read(tmp.path()).unwrap();
        assert_eq!(loaded.runtime_version, "1.0.0");
        assert_eq!(loaded.project_name, "roundtrip");
        assert_eq!(loaded.assets.len(), 1);
        assert_eq!(loaded.assets[0], "assets/test.vrm");
    }

    #[test]
    fn build_manifest_read_fails_for_missing_file() {
        let tmp = TempDir::new().unwrap();
        assert!(BuildManifest::read(tmp.path()).is_err());
    }

    #[test]
    fn build_manifest_valid_json() {
        let tmp = TempDir::new().unwrap();
        let manifest = BuildManifest::new("1.0.0", "json-test", "0.1.0");
        manifest.write(tmp.path()).unwrap();

        let content = fs::read_to_string(tmp.path().join("build-manifest.json")).unwrap();
        assert!(content.contains("runtime_version"));
        assert!(content.contains("1.0.0"));
        assert!(serde_json::from_str::<serde_json::Value>(&content).is_ok());
    }

    // ---- package_project ----

    fn setup_package_project(dir: &Path) {
        fs::write(
            dir.join("mate.toml"),
            r#"
[project]
name = "pkg-test"
version = "0.3.0"
runtime = "1.0.0"
"#,
        )
        .unwrap();
        fs::create_dir_all(dir.join("assets")).unwrap();
        fs::write(dir.join("assets/avatar.vrm"), b"fake-vrm").unwrap();
    }

    #[test]
    fn package_creates_archive() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        setup_package_project(project.path());

        let result = package_project(project.path(), output.path()).unwrap();
        assert!(result.archive_path.exists());
        assert!(result.archive_size > 0);
        assert!(result.archive_path.to_string_lossy().ends_with(".tar.gz"));
    }

    #[test]
    fn package_includes_build_manifest() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        setup_package_project(project.path());

        let result = package_project(project.path(), output.path()).unwrap();
        assert!(output.path().join("build-manifest.json").exists());
        assert_eq!(result.manifest.runtime_version, "1.0.0");
        assert_eq!(result.manifest.project_version, "0.3.0");
        assert!(!result.manifest.assets.is_empty());
    }

    #[test]
    fn package_without_assets_works() {
        let project = TempDir::new().unwrap();
        let output = TempDir::new().unwrap();
        fs::write(
            project.path().join("mate.toml"),
            r#"
[project]
name = "no-assets"
runtime = "1.0.0"
"#,
        )
        .unwrap();

        let result = package_project(project.path(), output.path());
        assert!(result.is_ok());
    }
}
