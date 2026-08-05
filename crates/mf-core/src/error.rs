use thiserror::Error;

/// The unified error type for all fallible operations in `mf-core`.
#[derive(Error, Debug, Clone, PartialEq, Eq)]
pub enum MfError {
    #[error("manifest not found at {path}. Run `mf new <name>` to create a project")]
    ManifestNotFound { path: String },

    #[error("invalid manifest: {reason}")]
    ManifestInvalid { reason: String },

    #[error("runtime not installed, run `mf runtime install`")]
    RuntimeNotInstalled,

    #[error("invalid runtime version: {0}. Expected a semantic version like 1.0.0")]
    InvalidVersion(String),

    #[error("Unity player not found for runtime v{version}. Run `mf runtime install {version}`")]
    RuntimeMissing { version: String },

    #[error("display server not detected. Are you running X11 or Wayland? Set XDG_SESSION_TYPE")]
    NoDisplayServer,

    #[error("VRM model not found at {path}. Check your [character] model setting in mate.toml")]
    ModelNotFound { path: String },

    #[error("Ollama not running. Start it with `ollama serve` or disable AI in mate.toml")]
    OllamaNotRunning,

    #[error("security violation: {0}")]
    SecurityViolation(String),

    #[error("download failed for {url}: HTTP {status}")]
    DownloadFailed { url: String, status: u16 },

    #[error("Unity player crashed with exit code {code}")]
    UnityCrashed { code: i32 },

    #[error("I/O error: {0}")]
    Io(String),

    #[error("template error: {0}")]
    Template(String),
}

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
            "manifest not found at /tmp/test/mate.toml. Run `mf new <name>` to create a project"
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

    #[test]
    fn error_no_display_server_has_suggestion() {
        let msg = MfError::NoDisplayServer.to_string();
        assert!(msg.contains("X11") || msg.contains("Wayland"));
    }

    #[test]
    fn error_runtime_missing_has_version_and_fix() {
        let msg = MfError::RuntimeMissing {
            version: "1.0.0".into(),
        }
        .to_string();
        assert!(msg.contains("1.0.0"));
        assert!(msg.contains("mf runtime install"));
    }

    #[test]
    fn error_model_not_found_has_path() {
        let msg = MfError::ModelNotFound {
            path: "/tmp/avatar.vrm".into(),
        }
        .to_string();
        assert!(msg.contains("/tmp/avatar.vrm"));
    }

    #[test]
    fn error_ollama_not_running_has_hint() {
        let msg = MfError::OllamaNotRunning.to_string();
        assert!(msg.contains("ollama serve"));
    }

    #[test]
    fn error_invalid_version_has_guidance() {
        let msg = MfError::InvalidVersion("abc".into()).to_string();
        assert!(msg.contains("abc"));
    }

    #[test]
    fn error_security_violation_has_reason() {
        let msg = MfError::SecurityViolation("path escapes project".into()).to_string();
        assert!(msg.contains("path escapes project"));
    }

    #[test]
    fn error_download_failed_has_url_and_status() {
        let msg = MfError::DownloadFailed {
            url: "https://example.com/x.tar.gz".into(),
            status: 404,
        }
        .to_string();
        assert!(msg.contains("https://example.com/x.tar.gz"));
        assert!(msg.contains("404"));
    }
}
