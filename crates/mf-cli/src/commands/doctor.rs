use std::path::Path;

use mf_core::MfError;

pub fn run(json: bool) -> anyhow::Result<()> {
    if json {
        let payload = run_doctor_json(Path::new("."))?;
        println!("{payload}");
    } else {
        let report = run_doctor(Path::new("."))?;
        println!("{report}");
    }
    Ok(())
}

/// Run all checks and return a human-readable report.
pub fn run_doctor(dir: &Path) -> Result<String, MfError> {
    let checks = collect_checks(dir);
    let mut out = String::new();
    for (name, status, detail) in &checks {
        let icon = match *status {
            "ok" => "\u{2705}",
            "warning" => "\u{26a0}\u{fe0f}",
            "info" => "\u{2139}\u{fe0f}",
            _ => "\u{274c}",
        };
        out.push_str(&format!("  {icon} {name}: {detail}\n"));
    }
    Ok(out)
}

/// Run all checks and return the JSON payload (a `{ "checks": [...] }` object).
pub fn run_doctor_json(dir: &Path) -> Result<String, MfError> {
    let checks = collect_checks(dir);
    let items: Vec<_> = checks
        .iter()
        .map(|(name, status, detail)| {
            serde_json::json!({ "name": name, "status": status, "detail": detail })
        })
        .collect();
    serde_json::to_string_pretty(&serde_json::json!({ "checks": items }))
        .map_err(|e| MfError::Io(format!("failed to serialize doctor output: {e}")))
}

fn collect_checks(dir: &Path) -> Vec<(&'static str, &'static str, String)> {
    let mut checks: Vec<(&'static str, &'static str, String)> = Vec::new();

    // Check 1: mate.toml exists and is valid
    let manifest_path = dir.join("mate.toml");
    if manifest_path.exists() {
        let content = std::fs::read_to_string(&manifest_path).unwrap_or_default();
        match mf_core::parse_manifest(&content) {
            Ok(manifest) => {
                match mf_core::validate_manifest(&manifest) {
                    Ok(()) => checks.push(("manifest", "ok", "valid".into())),
                    Err(e) => checks.push(("manifest", "error", e.to_string())),
                }
                // Check asset paths exist
                if !manifest.character.model.is_empty() {
                    let model_path = dir.join(&manifest.character.model);
                    if model_path.exists() {
                        checks.push((
                            "model",
                            "ok",
                            format!("found: {}", manifest.character.model),
                        ));
                    } else {
                        checks.push((
                            "model",
                            "warning",
                            format!("not found: {}", manifest.character.model),
                        ));
                    }
                }
            }
            Err(e) => checks.push(("manifest", "error", e.to_string())),
        }
    } else {
        checks.push(("manifest", "error", "mate.toml not found".into()));
    }

    // Check 2: assets directory
    if dir.join("assets").is_dir() {
        let count = std::fs::read_dir(dir.join("assets"))
            .map(|d| d.filter_map(|e| e.ok()).count())
            .unwrap_or(0);
        checks.push(("assets", "ok", format!("{count} files")));
    } else {
        checks.push(("assets", "warning", "assets/ directory missing".into()));
    }

    // Check 3: mods directory
    if dir.join("mods").is_dir() {
        checks.push(("mods", "ok", "directory exists".into()));
    } else {
        checks.push((
            "mods",
            "info",
            "mods/ directory not created (optional)".into(),
        ));
    }

    // Check 4: runtime
    let installed = mf_core::list_installed();
    let project_runtime =
        mf_core::parse_manifest(&std::fs::read_to_string(manifest_path).unwrap_or_default())
            .map(|m| m.project.runtime)
            .unwrap_or_default();
    checks.push(check_runtime(&installed, &project_runtime));

    // Check 5: display server
    let session = std::env::var("XDG_SESSION_TYPE").unwrap_or_default();
    let desktop = std::env::var("XDG_CURRENT_DESKTOP").unwrap_or_default();
    if session.is_empty() {
        checks.push((
            "display_server",
            "warning",
            "XDG_SESSION_TYPE not set. Are you running X11 or Wayland?".into(),
        ));
    } else {
        checks.push((
            "display_server",
            "ok",
            format!("session: {session} ({desktop})"),
        ));
    }

    // Check 6: permissions (can we write to the project dir?)
    let probe = dir.join(format!(".mf-write-probe-{}", std::process::id()));
    let writable = std::fs::write(&probe, b"probe").is_ok();
    let _ = std::fs::remove_file(&probe);
    if writable {
        checks.push(("permissions", "ok", "project dir is writable".into()));
    } else {
        checks.push((
            "permissions",
            "warning",
            format!("cannot write to {}", dir.display()),
        ));
    }

    checks
}

/// Runtime check as a pure function so the guidance branches are testable
/// without depending on the real runtime cache on the machine.
fn check_runtime(
    installed: &[String],
    project_runtime: &str,
) -> (&'static str, &'static str, String) {
    if installed.is_empty() {
        return (
            "runtime",
            "error",
            "no runtimes installed. Run `mf runtime install`".into(),
        );
    }
    if project_runtime.is_empty() || installed.contains(&project_runtime.to_string()) {
        return (
            "runtime",
            "ok",
            format!("{} installed", installed.join(", ")),
        );
    }
    (
        "runtime",
        "warning",
        format!(
            "project needs v{project_runtime}, but only {} installed. Run `mf runtime install {project_runtime}`",
            installed.join(", ")
        ),
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use tempfile::TempDir;

    fn setup_project(dir: &std::path::Path, toml_content: &str) {
        fs::write(dir.join("mate.toml"), toml_content).unwrap();
        fs::create_dir_all(dir.join("assets")).unwrap();
    }

    #[test]
    fn doctor_valid_project_all_ok() {
        let tmp = TempDir::new().unwrap();
        let toml = r#"
[project]
name = "test"
runtime = "1.0.0"
"#;
        setup_project(tmp.path(), toml);
        let result = run_doctor(tmp.path());
        assert!(result.is_ok());
    }

    #[test]
    fn doctor_missing_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_doctor(tmp.path());
        assert!(result.is_ok()); // doctor doesn't fail, it reports errors
    }

    #[test]
    fn doctor_invalid_manifest() {
        let tmp = TempDir::new().unwrap();
        fs::write(tmp.path().join("mate.toml"), "not valid {{{{").unwrap();
        let result = run_doctor(tmp.path());
        assert!(result.is_ok()); // doctor reports, doesn't fail
    }

    #[test]
    fn doctor_json_output_valid() {
        let tmp = TempDir::new().unwrap();
        let toml = r#"
[project]
name = "test"
runtime = "1.0.0"
"#;
        setup_project(tmp.path(), toml);
        let result = run_doctor_json(tmp.path());
        assert!(result.is_ok());
        let json: serde_json::Value = serde_json::from_str(&result.unwrap()).unwrap();
        assert!(json.is_object());
        assert!(json["checks"].is_array());
    }

    #[test]
    fn doctor_json_output_has_all_check_names() {
        let tmp = TempDir::new().unwrap();
        let result = run_doctor_json(tmp.path()).unwrap();
        let json: serde_json::Value = serde_json::from_str(&result).unwrap();
        let names: Vec<String> = json["checks"]
            .as_array()
            .unwrap()
            .iter()
            .map(|c| c["name"].as_str().unwrap().to_string())
            .collect();
        for expected in [
            "manifest",
            "runtime",
            "assets",
            "display_server",
            "permissions",
        ] {
            assert!(names.contains(&expected.to_string()), "missing {expected}");
        }
    }

    #[test]
    fn doctor_runtime_check_guidance_for_empty_install() {
        let (_, status, detail) = check_runtime(&[], "1.0.0");
        assert_eq!(status, "error");
        assert!(detail.contains("mf runtime install"));
    }

    #[test]
    fn doctor_runtime_check_ok_when_project_version_installed() {
        let (_, status, _) = check_runtime(&["1.0.0".to_string()], "1.0.0");
        assert_eq!(status, "ok");
    }

    #[test]
    fn doctor_runtime_check_warns_when_version_mismatch() {
        let (_, status, detail) = check_runtime(&["1.0.0".to_string()], "2.0.0");
        assert_eq!(status, "warning");
        assert!(detail.contains("2.0.0"));
    }
}
