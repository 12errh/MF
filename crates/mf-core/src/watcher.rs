use notify::{Event, EventKind, RecommendedWatcher, RecursiveMode, Watcher};
use std::path::Path;
use std::sync::mpsc;

use crate::MfError;

/// A classified file-system event from the project watcher.
#[derive(Debug, Clone, PartialEq)]
pub enum WatcherEvent {
    /// mate.toml or personality.toml changed.
    ConfigChanged(String),
    /// VRM, animation, or sound file changed.
    AssetChanged(String),
    /// Mod directory changed.
    ModChanged(String),
    /// Any other file changed.
    Unknown(String),
}

/// Watches a project directory recursively for changes.
pub struct ProjectWatcher {
    _watcher: RecommendedWatcher,
    rx: mpsc::Receiver<WatcherEvent>,
}

impl ProjectWatcher {
    /// Create a new watcher for the given project directory.
    pub fn new(project_dir: &Path) -> Result<Self, MfError> {
        let (tx, rx) = mpsc::channel();

        let mut watcher = RecommendedWatcher::new(
            move |result: Result<Event, notify::Error>| {
                if let Ok(event) = result {
                    let event = match event.kind {
                        EventKind::Create(_) | EventKind::Modify(_) | EventKind::Remove(_) => {
                            let path_str = event
                                .paths
                                .first()
                                .map(|p| p.display().to_string())
                                .unwrap_or_default();

                            if path_str.ends_with("mate.toml")
                                || path_str.ends_with("personality.toml")
                            {
                                WatcherEvent::ConfigChanged(path_str)
                            } else if path_str.ends_with(".vrm")
                                || path_str.ends_with(".anim")
                                || path_str.ends_with(".wav")
                                || path_str.ends_with(".mp3")
                            {
                                WatcherEvent::AssetChanged(path_str)
                            } else if path_str.contains("mods/") {
                                WatcherEvent::ModChanged(path_str)
                            } else {
                                WatcherEvent::Unknown(path_str)
                            }
                        }
                        _ => return,
                    };
                    let _ = tx.send(event);
                }
            },
            notify::Config::default(),
        )
        .map_err(|e| MfError::Io(format!("failed to create file watcher: {e}")))?;

        watcher
            .watch(project_dir, RecursiveMode::Recursive)
            .map_err(|e| MfError::Io(format!("failed to watch directory: {e}")))?;

        Ok(Self {
            _watcher: watcher,
            rx,
        })
    }

    /// Check for events without blocking.
    pub fn poll(&self) -> Option<WatcherEvent> {
        self.rx.try_recv().ok()
    }

    /// Block until an event arrives or the timeout elapses.
    pub fn wait_event(&self, timeout_ms: u64) -> Option<WatcherEvent> {
        self.rx
            .recv_timeout(std::time::Duration::from_millis(timeout_ms))
            .ok()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use tempfile::TempDir;

    #[test]
    fn watcher_detects_toml_change() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();

        fs::write(tmp.path().join("mate.toml"), "[project]\nname = \"test\"\n").unwrap();

        let event = watcher.wait_event(2000);
        assert!(event.is_some(), "should detect mate.toml change");
        match event.unwrap() {
            WatcherEvent::ConfigChanged(path) => {
                assert!(path.contains("mate.toml"));
            }
            other => panic!("expected ConfigChanged, got {other:?}"),
        }
    }

    #[test]
    fn watcher_detects_vrm_change() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();

        fs::write(tmp.path().join("avatar.vrm"), b"fake-vrm").unwrap();

        let event = watcher.wait_event(2000);
        assert!(event.is_some());
        match event.unwrap() {
            WatcherEvent::AssetChanged(path) => {
                assert!(path.contains("avatar.vrm"));
            }
            other => panic!("expected AssetChanged, got {other:?}"),
        }
    }

    #[test]
    fn watcher_poll_returns_none_when_nothing() {
        let tmp = TempDir::new().unwrap();
        let watcher = ProjectWatcher::new(tmp.path()).unwrap();
        assert!(watcher.poll().is_none());
    }
}
