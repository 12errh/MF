use std::path::{Path, PathBuf};

use semver::Version;

use crate::MfError;

/// Where runtimes are cached on disk.
pub fn runtime_cache_dir() -> PathBuf {
    let home = std::env::var("HOME").unwrap_or_else(|_| "/tmp".into());
    PathBuf::from(home).join(".mate-framework").join("runtimes")
}

/// A specific runtime version identified by its semantic version string.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RuntimeVersion(pub String);

/// Installation state of a runtime version on disk.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum InstallStatus {
    NotInstalled,
    Downloading,
    Installed,
    Incomplete,
}

impl RuntimeVersion {
    /// Directory where this version's files live in the cache.
    pub fn cache_dir(&self) -> PathBuf {
        runtime_cache_dir().join(&self.0)
    }

    /// Installation state based on the presence of the player binary.
    pub fn status(&self) -> InstallStatus {
        let dir = self.cache_dir();
        if !dir.exists() {
            return InstallStatus::NotInstalled;
        }
        if dir.join("MateRuntime").join("MateRuntime").exists() {
            InstallStatus::Installed
        } else {
            InstallStatus::Incomplete
        }
    }

    /// Whether the version string is a valid semantic version.
    pub fn is_valid(&self) -> bool {
        Version::parse(&self.0).is_ok()
    }

    /// Download URL for the runtime tarball on GitHub Releases.
    pub fn download_url(&self) -> String {
        format!(
            "https://github.com/mate-framework/mate-runtime/releases/download/v{}/MateRuntime-linux-x64.tar.gz",
            self.0
        )
    }

    /// Remove this version's directory from the cache. No-op if not installed.
    pub fn remove(&self) -> Result<(), MfError> {
        let dir = self.cache_dir();
        if dir.exists() {
            std::fs::remove_dir_all(&dir)
                .map_err(|e| MfError::Io(format!("failed to remove runtime {}: {}", self.0, e)))?;
        }
        Ok(())
    }
}

/// Ensure a runtime version is available, installing it if needed.
/// Actual download is deferred until releases are published; this validates
/// the version and returns its cache directory.
pub fn install_runtime(version: &str) -> Result<PathBuf, MfError> {
    let rv = RuntimeVersion(version.to_string());

    if rv.status() == InstallStatus::Installed {
        return Ok(rv.cache_dir());
    }

    if !rv.is_valid() {
        return Err(MfError::InvalidVersion(version.to_string()));
    }

    Ok(rv.cache_dir())
}

/// Remove an installed runtime version.
pub fn remove_runtime(version: &str) -> Result<(), MfError> {
    RuntimeVersion(version.to_string()).remove()
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

    // ---- RuntimeVersion / InstallStatus / install / remove ----

    #[test]
    fn version_status_not_installed_for_missing() {
        let v = RuntimeVersion("99.99.99".into());
        assert_eq!(v.status(), InstallStatus::NotInstalled);
    }

    #[test]
    fn version_cache_dir_path() {
        let v = RuntimeVersion("1.0.0".into());
        let dir = v.cache_dir();
        assert!(dir.to_string_lossy().contains("1.0.0"));
        assert!(dir.to_string_lossy().contains(".mate-framework"));
    }

    #[test]
    fn version_download_url() {
        let v = RuntimeVersion("1.0.0".into());
        let url = v.download_url();
        assert!(url.contains("github.com"));
        assert!(url.contains("v1.0.0"));
        assert!(url.contains("MateRuntime-linux-x64.tar.gz"));
    }

    #[test]
    fn version_remove_nonexistent_is_ok() {
        let v = RuntimeVersion("99.99.99".into());
        assert!(v.remove().is_ok());
    }

    #[test]
    fn version_invalid_semver_is_not_valid() {
        assert!(!RuntimeVersion("not-a-version".into()).is_valid());
        assert!(RuntimeVersion("1.2.3".into()).is_valid());
    }

    #[test]
    fn install_runtime_invalid_version_returns_error() {
        let result = install_runtime("not-a-version");
        match result {
            Err(MfError::InvalidVersion(v)) => assert_eq!(v, "not-a-version"),
            other => panic!("expected InvalidVersion, got {other:?}"),
        }
    }

    #[test]
    fn remove_runtime_nonexistent_is_ok() {
        assert!(remove_runtime("99.99.99").is_ok());
    }
}
