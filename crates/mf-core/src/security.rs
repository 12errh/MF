use std::path::Path;

use crate::MfError;

/// Validate that a path does not escape the project directory.
/// Both paths are canonicalized; the target must resolve inside the project.
pub fn validate_path(project_dir: &Path, target: &Path) -> Result<(), MfError> {
    let canonical_project = project_dir
        .canonicalize()
        .map_err(|e| MfError::Io(format!("cannot resolve project dir: {}", e)))?;
    let canonical_target = target
        .canonicalize()
        .map_err(|e| MfError::Io(format!("cannot resolve target path: {}", e)))?;

    if !canonical_target.starts_with(&canonical_project) {
        return Err(MfError::SecurityViolation(format!(
            "path {} escapes project directory",
            target.display()
        )));
    }
    Ok(())
}

/// Validate that a URL uses HTTPS.
pub fn validate_url(url: &str) -> Result<(), MfError> {
    if !url.starts_with("https://") {
        return Err(MfError::SecurityViolation(format!(
            "URL must use HTTPS: {}",
            url
        )));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;
    use tempfile::TempDir;

    #[test]
    fn validate_path_within_project() {
        let tmp = TempDir::new().unwrap();
        let file = tmp.path().join("assets").join("avatar.vrm");
        std::fs::create_dir_all(file.parent().unwrap()).unwrap();
        std::fs::write(&file, b"vrm").unwrap();
        assert!(validate_path(tmp.path(), &file).is_ok());
    }

    #[test]
    fn validate_path_escapes_project() {
        let tmp = TempDir::new().unwrap();
        let outside = PathBuf::from("/etc/passwd");
        assert!(validate_path(tmp.path(), &outside).is_err());
    }

    #[test]
    fn validate_url_https() {
        assert!(validate_url("https://example.com").is_ok());
    }

    #[test]
    fn validate_url_http_rejected() {
        assert!(validate_url("http://example.com").is_err());
    }
}
