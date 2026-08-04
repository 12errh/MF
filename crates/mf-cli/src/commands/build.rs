use std::path::Path;

pub fn run(output: Option<&str>, json: bool) -> anyhow::Result<()> {
    let project_dir = Path::new(".");
    let output_dir = match output {
        Some(o) => std::path::PathBuf::from(o),
        None => std::path::PathBuf::from("build"),
    };

    if !json {
        println!("Building project...");
    }

    let result =
        mf_core::build_project(project_dir, &output_dir).map_err(|e| anyhow::anyhow!("{}", e))?;

    if json {
        println!(
            "{}",
            serde_json::json!({
                "status": "ok",
                "manifest": result.manifest,
                "output": result.output_dir.display().to_string(),
                "files_copied": result.files_copied,
            })
        );
    } else {
        println!("Build complete!");
        println!("  Project: {}", result.manifest);
        println!("  Output: {}", result.output_dir.display());
        println!("  Files copied: {}", result.files_copied);
    }

    Ok(())
}
