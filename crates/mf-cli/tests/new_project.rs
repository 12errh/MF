use std::fs;
use std::process::Command;
use tempfile::TempDir;

#[test]
fn new_project_creates_valid_structure() {
    let tmp = TempDir::new().unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .arg("new")
        .arg("test-pet")
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(
        output.status.success(),
        "mf new failed: {}",
        String::from_utf8_lossy(&output.stderr)
    );

    let project = tmp.path().join("test-pet");
    assert!(project.join("mate.toml").exists(), "mate.toml not created");
    assert!(project.join("assets").is_dir(), "assets/ not created");
    assert!(project.join("mods").is_dir(), "mods/ not created");
    assert!(project.join("config").is_dir(), "config/ not created");

    let content = fs::read_to_string(project.join("mate.toml")).unwrap();
    let manifest = mf_core::parse_manifest(&content).unwrap();
    assert_eq!(manifest.project.name, "test-pet");
    assert!(mf_core::validate_manifest(&manifest).is_ok());
}

#[test]
fn new_project_rejects_duplicate() {
    let tmp = TempDir::new().unwrap();
    let project = tmp.path().join("exists");
    fs::create_dir_all(&project).unwrap();

    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .arg("new")
        .arg("exists")
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(!output.status.success());
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(stderr.contains("already exists"));
}

#[test]
fn doctor_json_output() {
    let tmp = TempDir::new().unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_mf"))
        .args(["--json", "doctor"])
        .current_dir(&tmp)
        .output()
        .expect("failed to run mf");

    assert!(output.status.success());
    let json: serde_json::Value = serde_json::from_slice(&output.stdout).unwrap();
    assert!(json["checks"].is_array());
}
