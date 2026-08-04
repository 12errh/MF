use mf_core::{
    ProjectWatcher, RuntimeProcess, WatcherEvent, build_launch_config, parse_manifest, player_path,
    resolve_version, validate_manifest,
};
use std::path::Path;
use std::thread;
use std::time::{Duration, Instant};

pub fn run(json: bool) -> anyhow::Result<()> {
    run_inner(Path::new("."), json)
}

fn run_inner(dir: &Path, json: bool) -> anyhow::Result<()> {
    // 1. Load and validate manifest
    let manifest_path = dir.join("mate.toml");
    if !manifest_path.exists() {
        return Err(anyhow::anyhow!(
            "mate.toml not found. Run `mf new <name>` first."
        ));
    }

    let content = std::fs::read_to_string(&manifest_path)?;
    let manifest = parse_manifest(&content).map_err(|e| anyhow::anyhow!("{}", e))?;
    validate_manifest(&manifest).map_err(|e| anyhow::anyhow!("{}", e))?;

    // 2. Resolve runtime
    let version =
        resolve_version(&manifest.project.runtime).map_err(|e| anyhow::anyhow!("{}", e))?;
    let player = player_path(&version);

    if !player.exists() {
        return Err(anyhow::anyhow!(
            "Unity player not found at {}. Run `mf runtime install {}`",
            player.display(),
            version
        ));
    }

    if !json {
        println!(
            "Starting '{}' with runtime v{}...",
            manifest.project.name, version
        );
        println!("Player: {}", player.display());
        println!("Project: {}", dir.display());
        println!("Press Ctrl+C to stop.\n");
    }

    // 3. Start file watcher
    let watcher = ProjectWatcher::new(dir)
        .map_err(|e| anyhow::anyhow!("failed to start file watcher: {e}"))?;

    // 4. Main loop: start -> watch -> restart on crash/change.
    // The restart counter resets after a stable run, so only rapid
    // consecutive restarts (a crash/edit loop) trip the limit.
    let mut restart_count = 0u32;
    let mut last_restart_at = Instant::now();
    const MAX_RESTARTS: u32 = 10;
    const RESTART_WINDOW: Duration = Duration::from_secs(5);

    loop {
        let config = build_launch_config(player.clone(), dir.to_path_buf());
        let mut process = match RuntimeProcess::spawn(&config) {
            Ok(p) => {
                if json {
                    println!(
                        "{}",
                        serde_json::json!({
                            "event": "started",
                            "pid": p.pid,
                            "runtime": version,
                        })
                    );
                } else {
                    println!("[mf] Runtime started (PID: {})", p.pid);
                }
                p
            }
            Err(e) => return Err(anyhow::anyhow!("failed to start runtime: {e}")),
        };

        // Wait for process exit or file change
        let exit_code = wait_for_event_or_exit(&mut process, &watcher);

        // Kill process if still running
        let _ = process.kill();

        // Reset the counter if the previous run was stable for longer than
        // RESTART_WINDOW, so only rapid consecutive restarts count.
        if last_restart_at.elapsed() > RESTART_WINDOW {
            restart_count = 0;
        }

        match exit_code {
            DevExitReason::FileChanged(path) => {
                restart_count += 1;
                last_restart_at = Instant::now();
                if restart_count > MAX_RESTARTS {
                    return Err(anyhow::anyhow!(
                        "too many restarts ({}) — something is wrong. Stopping.",
                        MAX_RESTARTS
                    ));
                }
                if json {
                    println!(
                        "{}",
                        serde_json::json!({
                            "event": "restarting",
                            "reason": "file_changed",
                            "path": path,
                            "restart_count": restart_count,
                        })
                    );
                } else {
                    println!(
                        "[mf] Change detected: {} (restart #{})",
                        path, restart_count
                    );
                }
                thread::sleep(Duration::from_millis(500)); // debounce
            }
            DevExitReason::ProcessCrashed(code) => {
                restart_count += 1;
                last_restart_at = Instant::now();
                if restart_count > MAX_RESTARTS {
                    return Err(anyhow::anyhow!(
                        "runtime crashed too many times ({}). Last exit code: {}",
                        MAX_RESTARTS,
                        code
                    ));
                }
                if json {
                    println!(
                        "{}",
                        serde_json::json!({
                            "event": "restarting",
                            "reason": "crash",
                            "exit_code": code,
                            "restart_count": restart_count,
                        })
                    );
                } else {
                    println!(
                        "[mf] Runtime crashed (exit: {}). Restarting... (#{})",
                        code, restart_count
                    );
                }
                thread::sleep(Duration::from_secs(1));
            }
        }
    }
}

enum DevExitReason {
    FileChanged(String),
    ProcessCrashed(i32),
}

/// Wait until the process exits OR a relevant file change occurs.
fn wait_for_event_or_exit(process: &mut RuntimeProcess, watcher: &ProjectWatcher) -> DevExitReason {
    let check_interval = Duration::from_millis(100);

    loop {
        // Check for file change
        if let Some(event) = watcher.poll() {
            match event {
                WatcherEvent::ConfigChanged(path) | WatcherEvent::AssetChanged(path) => {
                    return DevExitReason::FileChanged(path);
                }
                WatcherEvent::ModChanged(path) => {
                    return DevExitReason::FileChanged(path);
                }
                WatcherEvent::Unknown(_) => {}
            }
        }

        // Check if process is still running
        if !process.is_running() {
            let code = process.wait().unwrap_or(-1);
            return DevExitReason::ProcessCrashed(code);
        }

        thread::sleep(check_interval);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use tempfile::TempDir;

    #[test]
    fn dev_fails_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_err());
        assert!(
            result
                .unwrap_err()
                .to_string()
                .contains("mate.toml not found")
        );
    }

    #[test]
    fn dev_fails_with_invalid_manifest() {
        let tmp = TempDir::new().unwrap();
        fs::write(tmp.path().join("mate.toml"), "not valid {{{{").unwrap();
        let result = run_inner(tmp.path(), false);
        assert!(result.is_err());
    }

    #[test]
    fn dev_json_output_without_manifest() {
        let tmp = TempDir::new().unwrap();
        let result = run_inner(tmp.path(), true);
        assert!(result.is_err());
    }
}
