use criterion::{Criterion, black_box, criterion_group, criterion_main};
use std::path::PathBuf;
use std::process::Command;

/// Bench `mf --help` — CLI startup + parse time.
fn bench_mf_help(c: &mut Criterion) {
    c.bench_function("mf_help", |b| {
        b.iter(|| Command::new(binary_path()).arg("--help").output().unwrap())
    });
}

/// Bench `mf new <name>` — project scaffold. Each iteration uses a unique
/// project name so it exercises the happy path (a repeated name would error).
fn bench_mf_new(c: &mut Criterion) {
    let tmp = tempfile::TempDir::new().unwrap();
    c.bench_function("mf_new", |b| {
        let mut i = 0u64;
        b.iter(|| {
            i += 1;
            Command::new(binary_path())
                .args(["new", &format!("bench-{i}")])
                .current_dir(tmp.path())
                .output()
                .unwrap()
        })
    });
}

/// Bench `mf doctor` — diagnostics over a small project.
fn bench_mf_doctor(c: &mut Criterion) {
    let tmp = tempfile::TempDir::new().unwrap();
    std::fs::write(
        tmp.path().join("mate.toml"),
        "[project]\nname = \"bench\"\nruntime = \"1.0.0\"\n",
    )
    .unwrap();
    std::fs::create_dir_all(tmp.path().join("assets")).unwrap();
    c.bench_function("mf_doctor", |b| {
        b.iter(|| {
            Command::new(binary_path())
                .arg("doctor")
                .current_dir(tmp.path())
                .output()
                .unwrap()
        })
    });
}

/// Path to the built `mf` binary (set by cargo at build time for benchmarks).
fn binary_path() -> PathBuf {
    black_box(PathBuf::from(env!("CARGO_BIN_EXE_mf")))
}

criterion_group!(benches, bench_mf_help, bench_mf_new, bench_mf_doctor);
criterion_main!(benches);
