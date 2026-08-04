use thiserror::Error;

/// The unified error type for all fallible operations in `mf-core`.
#[derive(Error, Debug, Clone, PartialEq, Eq)]
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