use std::path::{Path, PathBuf};

use crate::MfError;

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
}
