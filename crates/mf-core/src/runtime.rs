use std::path::{Path, PathBuf};

use semver::Version;

use crate::security::validate_url;
use crate::MfError;

/// Where runtimes are cached on disk.
pub fn runtime_cache_dir() -> PathBuf {
    let home = std::env::var("HOME").unwrap_or_else(|_| "/tmp".into());
    PathBuf::from(home).join(".mate-framework").join("runtimes")
}

/// GitHub repo that hosts runtime player releases.
pub const RUNTIME_RELEASE_REPO: &str = "12errh/MF";

/// Filename of the runtime tarball attached to each release.
pub const RUNTIME_ARCHIVE: &str = "MateRuntime-linux-x64.tar.gz";

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
            "https://github.com/{RUNTIME_RELEASE_REPO}/releases/download/v{}/{}",
            self.0, RUNTIME_ARCHIVE
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
/// Downloads the release tarball, extracts it into the cache, and verifies
/// the player binary landed in the expected layout.
pub fn install_runtime(version: &str) -> Result<PathBuf, MfError> {
    let rv = RuntimeVersion(version.to_string());

    if rv.status() == InstallStatus::Installed {
        return Ok(rv.cache_dir());
    }

    if !rv.is_valid() {
        return Err(MfError::InvalidVersion(version.to_string()));
    }

    let url = rv.download_url();
    validate_url(&url)?;

    let cache = rv.cache_dir();
    // Stage the archive next to the cache dir so a failed install leaves no
    // partial version directory behind, and remove it on any failure.
    let archive = cache_dir_tmp(cache.parent().unwrap_or(Path::new("/tmp")), &rv.0);
    let result = download(&url, &archive).and_then(|_| extract(&archive, &cache));
    let _ = std::fs::remove_file(&archive);
    result?;

    if rv.status() != InstallStatus::Installed {
        return Err(MfError::Io(format!(
            "runtime v{version} downloaded but player binary not found at {}",
            player_path(version).display()
        )));
    }

    Ok(cache)
}

/// Download `url` into `dest` with a blocking HTTP client, following redirects.
fn download(url: &str, dest: &Path) -> Result<(), MfError> {
    let response = reqwest::blocking::Client::new()
        .get(url)
        .send()
        .map_err(|e| MfError::Io(format!("failed to download {url}: {e}")))?;

    let status = response.status();
    if !status.is_success() {
        return Err(MfError::DownloadFailed {
            url: url.to_string(),
            status: status.as_u16(),
        });
    }

    // Ensure the cache parent exists (a fresh machine has no ~/.mate-framework).
    if let Some(parent) = dest.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| MfError::Io(format!("failed to create {}: {e}", parent.display())))?;
    }

    let mut file = std::fs::File::create(dest)
        .map_err(|e| MfError::Io(format!("failed to create {}: {e}", dest.display())))?;
    let mut body = response;
    std::io::copy(&mut body, &mut file)
        .map_err(|e| MfError::Io(format!("failed to write {}: {e}", dest.display())))?;
    Ok(())
}

/// Extract a gzip tarball into `dest`, creating it if needed.
fn extract(archive: &Path, dest: &Path) -> Result<(), MfError> {
    std::fs::create_dir_all(dest)
        .map_err(|e| MfError::Io(format!("failed to create {}: {e}", dest.display())))?;

    let file = std::fs::File::open(archive)
        .map_err(|e| MfError::Io(format!("failed to open {}: {e}", archive.display())))?;
    let gz = flate2::read::GzDecoder::new(file);
    let mut tar = tar::Archive::new(gz);
    tar.unpack(dest)
        .map_err(|e| MfError::Io(format!("failed to extract {}: {e}", archive.display())))?;
    Ok(())
}

/// Temp archive path in `dir`, unique per version.
fn cache_dir_tmp(dir: &Path, version: &str) -> PathBuf {
    dir.join(format!(".{version}.{}.tar.gz", std::process::id()))
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
        assert!(url.contains("12errh/MF"));
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
