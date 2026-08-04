use std::path::{Path, PathBuf};

use semver::Version;

use crate::MfError;

/// Where runtimes are cached on disk.
pub fn runtime_cache_dir() -> PathBuf {
    let home = std::env::var("HOME").unwrap_or_else(|_| "/tmp".into());
    PathBuf::from(home).join(".mate-framework").join("runtimes")
}

/// Path to a specific runtime version.
pub fn runtime_path(version: &str) -> PathBuf {
    runtime_cache_dir().join(version)
}

/// Path to the Unity player binary inside a cached runtime.
pub fn player_path(version: &str) -> PathBuf {
    runtime_path(version)
        .join("MateRuntime")
        .join("MateRuntime")
}

/// Check if a specific runtime version is installed.
pub fn is_installed(version: &str) -> bool {
    is_installed_in(&runtime_cache_dir(), version)
}

/// List all installed runtime versions, sorted ascending by semver.
pub fn list_installed() -> Vec<String> {
    list_installed_in(&runtime_cache_dir())
}

/// Resolve the runtime version to use.
/// If `project_runtime` is installed, use it; otherwise use the newest
/// installed version.
pub fn resolve_version(project_runtime: &str) -> Result<String, MfError> {
    resolve_version_in(&runtime_cache_dir(), project_runtime)
}

fn is_installed_in(cache: &Path, version: &str) -> bool {
    cache
        .join(version)
        .join("MateRuntime")
        .join("MateRuntime")
        .exists()
}

fn list_installed_in(cache: &Path) -> Vec<String> {
    if !cache.exists() {
        return Vec::new();
    }
    let mut versions: Vec<String> = std::fs::read_dir(cache)
        .map(|entries| {
            entries
                .filter_map(|e| e.ok())
                .filter(|e| e.path().is_dir())
                .filter_map(|e| e.file_name().to_str().map(|s| s.to_string()))
                .collect()
        })
        .unwrap_or_default();
    versions.sort_by(|a, b| {
        let va = Version::parse(a).unwrap_or_else(|_| Version::new(0, 0, 0));
        let vb = Version::parse(b).unwrap_or_else(|_| Version::new(0, 0, 0));
        va.cmp(&vb)
    });
    versions
}

fn resolve_version_in(cache: &Path, project_runtime: &str) -> Result<String, MfError> {
    if is_installed_in(cache, project_runtime) {
        return Ok(project_runtime.into());
    }
    list_installed_in(cache)
        .last()
        .cloned()
        .ok_or(MfError::RuntimeNotInstalled)
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    fn install_version(cache: &Path, version: &str) {
        let player = cache.join(version).join("MateRuntime").join("MateRuntime");
        std::fs::create_dir_all(player.parent().unwrap()).unwrap();
        std::fs::write(&player, b"").unwrap();
    }

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
        let tmp = TempDir::new().unwrap();
        assert!(!is_installed_in(tmp.path(), "999.0.0-test"));
    }

    #[test]
    fn list_installed_empty_when_no_cache() {
        let tmp = TempDir::new().unwrap();
        assert!(list_installed_in(tmp.path()).is_empty());
    }

    #[test]
    fn resolve_version_fails_when_nothing_installed() {
        let tmp = TempDir::new().unwrap();
        assert_eq!(
            resolve_version_in(tmp.path(), "999.0.0-test"),
            Err(MfError::RuntimeNotInstalled)
        );
    }

    #[test]
    fn resolve_version_returns_requested_when_installed() {
        let tmp = TempDir::new().unwrap();
        install_version(tmp.path(), "1.0.0");
        assert_eq!(resolve_version_in(tmp.path(), "1.0.0"), Ok("1.0.0".into()));
    }

    #[test]
    fn resolve_version_picks_newest_semver() {
        let tmp = TempDir::new().unwrap();
        for version in ["1.9.0", "1.10.0", "1.0.0"] {
            install_version(tmp.path(), version);
        }
        let result = resolve_version_in(tmp.path(), "missing");
        assert_eq!(result, Ok("1.10.0".into()));
    }
}
