use mf_core::{list_installed, runtime_cache_dir, runtime_path};

pub fn run(subcommand: &str, json: bool) -> anyhow::Result<()> {
    match subcommand {
        "list" => run_list(json),
        "status" => run_status(json),
        "install" => {
            if json {
                println!(
                    "{}",
                    serde_json::json!({
                        "status": "not_implemented",
                        "message": "automatic runtime download will be available when releases are published"
                    })
                );
            } else {
                println!(
                    "Automatic runtime download will be available when releases are published."
                );
                println!(
                    "For now, manually place the runtime at: {}",
                    runtime_path("1.0.0").display()
                );
            }
            Ok(())
        }
        _ => Err(anyhow::anyhow!("unknown runtime subcommand: {subcommand}")),
    }
}

fn run_list(json: bool) -> anyhow::Result<()> {
    let versions = list_installed();
    if json {
        println!("{}", serde_json::json!({ "versions": versions }));
    } else if versions.is_empty() {
        println!("No runtimes installed. Run `mf runtime install` to download.");
    } else {
        println!("Installed runtimes:");
        for v in &versions {
            let path = runtime_path(v);
            let player = path.join("MateRuntime").join("MateRuntime");
            let status = if player.exists() { "ok" } else { "incomplete" };
            println!("  v{v} ({status}) — {}", path.display());
        }
    }
    Ok(())
}

fn run_status(json: bool) -> anyhow::Result<()> {
    let versions = list_installed();
    let cache_dir = runtime_cache_dir();

    if json {
        println!(
            "{}",
            serde_json::json!({
                "cache_dir": cache_dir.display().to_string(),
                "count": versions.len(),
                "versions": versions,
            })
        );
    } else {
        println!("Runtime cache: {}", cache_dir.display());
        println!("Installed: {} version(s)", versions.len());
        for v in &versions {
            println!("  v{v}");
        }
    }
    Ok(())
}
