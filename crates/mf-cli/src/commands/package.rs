use std::path::Path;

pub fn run(json: bool) -> anyhow::Result<()> {
    let result = mf_core::package_project(Path::new("."), Path::new("build"))
        .map_err(|e| anyhow::anyhow!("{}", e))?;

    if json {
        println!(
            "{}",
            serde_json::json!({
                "status": "ok",
                "archive": result.archive_path.display().to_string(),
                "size": result.archive_size,
                "includes_runtime": result.includes_runtime,
                "assets": result.manifest.assets.len(),
            })
        );
    } else {
        println!("Package created: {}", result.archive_path.display());
        println!(
            "  size: {} bytes, {} assets, runtime v{}",
            result.archive_size,
            result.manifest.assets.len(),
            result.manifest.runtime_version
        );
    }

    Ok(())
}
