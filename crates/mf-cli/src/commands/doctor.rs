use std::path::PathBuf;

pub fn run(json: bool) -> anyhow::Result<()> {
    let mut checks = Vec::new();

    // Check 1: mate.toml exists
    let manifest_path = PathBuf::from("mate.toml");
    if manifest_path.exists() {
        let content = std::fs::read_to_string(&manifest_path)?;
        match mf_core::parse_manifest(&content) {
            Ok(manifest) => {
                match mf_core::validate_manifest(&manifest) {
                    Ok(()) => checks.push(("manifest", "ok", "valid".into())),
                    Err(e) => checks.push(("manifest", "error", e.to_string())),
                }
            }
            Err(e) => checks.push(("manifest", "error", e.to_string())),
        }
    } else {
        checks.push(("manifest", "error", "mate.toml not found".into()));
    }

    // Check 2: assets directory
    if PathBuf::from("assets").is_dir() {
        checks.push(("assets", "ok", "directory exists".into()));
    } else {
        checks.push(("assets", "warning", "assets/ directory missing".into()));
    }

    if json {
        let output = serde_json::json!({
            "checks": checks.into_iter().map(|(name, status, detail)| {
                serde_json::json!({ "name": name, "status": status, "detail": detail })
            }).collect::<Vec<_>>(),
        });
        println!("{}", serde_json::to_string_pretty(&output)?);
    } else {
        for (name, status, detail) in &checks {
            let icon = match *status {
                "ok" => "\u{2705}",
                "warning" => "\u{26a0}\u{fe0f}",
                _ => "\u{274c}",
            };
            println!("  {icon} {name}: {detail}");
        }
    }

    Ok(())
}