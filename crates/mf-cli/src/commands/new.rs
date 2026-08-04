use mf_core::{default_manifest, validate_manifest};
use std::fs;
use std::path::PathBuf;

pub fn run(name: &str, json: bool) -> anyhow::Result<()> {
    validate_project_name(name)?;

    let manifest = default_manifest(name);
    validate_manifest(&manifest).map_err(|e| anyhow::anyhow!("{}", e))?;

    let project_dir = PathBuf::from(name);

    if project_dir.exists() {
        return Err(anyhow::anyhow!("directory '{}' already exists", name));
    }

    fs::create_dir_all(&project_dir)?;
    fs::create_dir_all(project_dir.join("assets"))?;
    fs::create_dir_all(project_dir.join("mods"))?;
    fs::create_dir_all(project_dir.join("config"))?;

    let toml_content = toml::to_string_pretty(&manifest)
        .map_err(|e| anyhow::anyhow!("failed to serialize manifest: {}", e))?;
    fs::write(project_dir.join("mate.toml"), toml_content)?;

    if json {
        let output = serde_json::json!({
            "status": "ok",
            "project_dir": project_dir.display().to_string(),
            "manifest": "mate.toml",
        });
        println!("{}", serde_json::to_string_pretty(&output)?);
    } else {
        println!("Created project '{}' in {}", name, project_dir.display());
        println!("  mate.toml  - project manifest");
        println!("  assets/    - VRM models, textures, sounds");
        println!("  mods/      - optional mod assets");
        println!("  config/    - personality.toml and other config");
    }

    Ok(())
}

fn validate_project_name(name: &str) -> anyhow::Result<()> {
    if name.is_empty() || name == "." || name == ".." || name.contains('/') || name.contains('\\') {
        return Err(anyhow::anyhow!("invalid project name: '{}'", name));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_path_traversal() {
        assert!(validate_project_name("../escape").is_err());
        assert!(validate_project_name("a/b").is_err());
        assert!(validate_project_name("..").is_err());
        assert!(validate_project_name(".").is_err());
        assert!(validate_project_name("").is_err());
    }

    #[test]
    fn accepts_simple_name() {
        assert!(validate_project_name("my-mate").is_ok());
    }
}
