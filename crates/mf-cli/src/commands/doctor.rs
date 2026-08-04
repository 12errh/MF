use std::path::{Path, PathBuf};

pub fn run(json: bool) -> anyhow::Result<()> {
    run_inner(Path::new("."), json)
}

fn run_inner(dir: &Path, json: bool) -> anyhow::Result<()> {
    let mut checks: Vec<(&str, &str, String)> = Vec::new();

    // Check 1: mate.toml exists and is valid
    let manifest_path = dir.join("mate.toml");
    if manifest_path.exists() {
        let content = std::fs::read_to_string(&manifest_path)?;
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

    // Output
    if json {
        let items: Vec<_> = checks
            .iter()
            .map(|(name, status, detail)| {
                serde_json::json!({ "name": name, "status": status, "detail": detail })
            })
            .collect();
        println!(
            "{}",
            serde_json::to_string_pretty(&serde_json::json!({ "checks": items }))?
        );
    } else {
        for (name, status, detail) in &checks {
            let icon = match *status {
                "ok" => "\u{2705}",
                "warning" => "\u{26a0}\u{fe0f}",
                "info" => "\u{2139}\u{fe0f}",
                _ => "\u{274c}",
            };
            println!("  {icon} {name}: {detail}");
        }
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;
    use std::fs;

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
        let result = run_inner(tmp.path(), false);
        assert!(result.is_ok());
    }

    #[test]
    fn doctor_missing_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_ok()); // doctor doesn't fail, it reports errors
    }

    #[test]
    fn doctor_invalid_manifest() {
        let tmp = TempDir::new().unwrap();
        fs::write(tmp.path().join("mate.toml"), "not valid {{{{").unwrap();
        let result = run_inner(tmp.path(), false);
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
        let result = run_inner(tmp.path(), true);
        assert!(result.is_ok());
    }
}