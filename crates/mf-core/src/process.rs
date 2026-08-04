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
