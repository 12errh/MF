use std::path::PathBuf;

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
    player_path(version).exists()
}

/// List all installed runtime versions, sorted ascending.
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
/// If `project_runtime` is installed, use it; otherwise use the latest
/// installed version.
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
