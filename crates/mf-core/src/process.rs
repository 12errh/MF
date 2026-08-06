use std::path::PathBuf;
use std::process::Stdio;

use crate::MfError;

/// Configuration for launching the Unity runtime.
#[derive(Debug, Clone)]
pub struct RuntimeLaunchConfig {
    pub player_path: PathBuf,
    pub project_dir: PathBuf,
    pub project_args: Vec<String>,
}

/// Handle to a running Unity player process.
#[derive(Debug)]
pub struct RuntimeProcess {
    child: std::process::Child,
    pub pid: u32,
}

impl RuntimeProcess {
    /// Spawn the Unity player with the given config.
    pub fn spawn(config: &RuntimeLaunchConfig) -> Result<Self, MfError> {
        if !config.player_path.exists() {
            return Err(MfError::RuntimeNotInstalled);
        }

        let mut cmd = std::process::Command::new(&config.player_path);
        cmd.arg("--projectPath").arg(&config.project_dir);
        cmd.args(&config.project_args);
        // A transparent window requires the player to open on a 32-bit ARGB
        // visual; SDL_VIDEO_X11_VISUALID must be set before the window is
        // created. Detect it from the running X server (glxinfo first, then
        // xdpyinfo) unless the caller already chose one.
        apply_argb_visual(&mut cmd);
        cmd.stdin(Stdio::null());
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::piped());

        let child = cmd
            .spawn()
            .map_err(|e| MfError::Io(format!("failed to spawn Unity player: {e}")))?;
        let pid = child.id();

        Ok(Self { child, pid })
    }

    /// Wait for the process to exit, returning its exit code.
    pub fn wait(&mut self) -> Result<i32, MfError> {
        self.child
            .wait()
            .map(|status| status.code().unwrap_or(-1))
            .map_err(|e| MfError::Io(format!("failed to wait on process: {e}")))
    }

    /// Kill the process.
    pub fn kill(&mut self) -> Result<(), MfError> {
        self.child
            .kill()
            .map_err(|e| MfError::Io(format!("failed to kill process: {e}")))
    }

    /// Check if the process is still running.
    pub fn is_running(&mut self) -> bool {
        matches!(self.child.try_wait(), Ok(None))
    }
}

/// Launch config from a player path and project directory.
pub fn build_launch_config(player_path: PathBuf, project_dir: PathBuf) -> RuntimeLaunchConfig {
    RuntimeLaunchConfig {
        player_path,
        project_dir,
        project_args: Vec::new(),
    }
}

/// If SDL_VIDEO_X11_VISUALID is not already set, detect a 32-bit ARGB visual
/// from the running X server and set it on the child command's environment.
/// Unity reads this before creating its window, which is required for a
/// transparent background.
fn apply_argb_visual(cmd: &mut std::process::Command) {
    if std::env::var("SDL_VIDEO_X11_VISUALID").is_ok() {
        return;
    }
    if let Some(visual) = detect_argb_visual() {
        cmd.env("SDL_VIDEO_X11_VISUALID", visual);
    }
}

/// Find a 32-bit ARGB visual id. Tries glxinfo first (the reference's
/// launch.sh does the same), then falls back to xdpyinfo's 32-plane visuals.
/// The id is returned with its 0x prefix, matching what SDL expects.
fn detect_argb_visual() -> Option<String> {
    if let Ok(out) = std::process::Command::new("glxinfo").output() {
        let text = String::from_utf8_lossy(&out.stdout);
        if let Some(line) = text.lines().find(|l| l.contains("32 tc") && l.contains("8  8  8  8"))
            && let Some(id) = line.split_whitespace().next()
        {
            return Some(id.to_string());
        }
    }
    if let Ok(out) = std::process::Command::new("xdpyinfo").output() {
        let text = String::from_utf8_lossy(&out.stdout);
        // xdpyinfo lists visuals by depth; find the "depth 32" block's visual id.
        let lines: Vec<&str> = text.lines().collect();
        for (i, line) in lines.iter().enumerate() {
            if line.contains("depth 32") {
                for next in &lines[i..] {
                    if next.contains("visual id")
                        && let Some(id) = next.split_whitespace().nth(2)
                    {
                        return Some(id.to_string());
                    }
                    if next.contains("depth ") && !next.contains("depth 32") {
                        break;
                    }
                }
            }
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn spawn_fails_when_player_missing() {
        let config = RuntimeLaunchConfig {
            player_path: PathBuf::from("/nonexistent/MateRuntime"),
            project_dir: PathBuf::from("/tmp/test"),
            project_args: vec![],
        };
        let result = RuntimeProcess::spawn(&config);
        match result {
            Err(MfError::RuntimeNotInstalled) => {}
            other => panic!("expected RuntimeNotInstalled, got {other:?}"),
        }
    }

    #[test]
    fn build_launch_config_sets_paths() {
        let config =
            build_launch_config(PathBuf::from("/runtime/player"), PathBuf::from("/project"));
        assert_eq!(config.player_path, PathBuf::from("/runtime/player"));
        assert_eq!(config.project_dir, PathBuf::from("/project"));
        assert!(config.project_args.is_empty());
    }

    #[test]
    fn spawn_and_kill_real_process() {
        let tmp = tempfile::TempDir::new().unwrap();
        let script = write_player_script(tmp.path(), "#!/bin/sh\nsleep 30\n");
        let config = RuntimeLaunchConfig {
            player_path: script,
            project_dir: tmp.path().join("project"),
            project_args: vec![],
        };
        let mut process = RuntimeProcess::spawn(&config).expect("spawn script");
        assert!(process.is_running());
        process.kill().expect("kill script");
        // Reap the child so the test leaves no zombie process.
        let _ = process.wait();
    }

    #[test]
    fn wait_returns_exit_code() {
        let tmp = tempfile::TempDir::new().unwrap();
        let script = write_player_script(tmp.path(), "#!/bin/sh\nexit 3\n");
        let config = RuntimeLaunchConfig {
            player_path: script,
            project_dir: tmp.path().join("project"),
            project_args: vec![],
        };
        let mut process = RuntimeProcess::spawn(&config).expect("spawn script");
        assert_eq!(process.wait().expect("wait"), 3);
    }

    #[cfg(unix)]
    fn write_player_script(dir: &std::path::Path, body: &str) -> PathBuf {
        use std::os::unix::fs::PermissionsExt;
        let script = dir.join("fake-player");
        std::fs::write(&script, body).unwrap();
        std::fs::set_permissions(&script, std::fs::Permissions::from_mode(0o755)).unwrap();
        script
    }
}
