use std::path::Path;

pub fn run(json: bool) -> anyhow::Result<()> {
    // 1. Build first
    let build_dir = Path::new("build");
    let result =
        mf_core::build_project(Path::new("."), build_dir).map_err(|e| anyhow::anyhow!("{}", e))?;

    // 2. Create archive
    let archive_name = format!("{}.tar.gz", result.manifest);

    let status = std::process::Command::new("tar")
        .args(["-czf", &archive_name, "-C", "build", "."])
        .status()
        .map_err(|e| anyhow::anyhow!("failed to run tar: {e}"))?;

    if !status.success() {
        return Err(anyhow::anyhow!(
            "tar failed with exit code {}",
            status.code().unwrap_or(-1)
        ));
    }

    if json {
        println!(
            "{}",
            serde_json::json!({ "status": "ok", "archive": archive_name })
        );
    } else {
        println!("Package created: {archive_name}");
    }

    Ok(())
}
