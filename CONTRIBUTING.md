# Contributing to Mate Framework

Thanks for your interest in Mate Framework! This project is in early
development, and contributions of all kinds are welcome — bug reports,
feature ideas, documentation, and code.

## Code of Conduct

Be respectful and constructive. Harassment, trolling, and personal attacks
are not tolerated.

## How to Contribute

### 1. Report a Bug or Request a Feature

Open an issue on GitHub. Please include:

- What you expected to happen
- What actually happened (include error output)
- Environment: Linux distribution, desktop session (`XDG_SESSION_TYPE`),
  Rust version, `mf --version` if applicable
- Steps to reproduce

### 2. Submit Code

1. **Fork** the repository and create a branch:
   ```bash
   git checkout -b feature/my-change
   ```
2. **Make your change** — keep it focused and minimal.
3. **Add or update tests** — new behavior needs tests; bug fixes need a
   regression test.
4. **Run the checks:**
   ```bash
   cargo test --workspace
   cargo fmt --all -- --check
   cargo clippy --workspace --all-targets -- -D warnings
   ```
5. **Commit** with a clear message following the existing style
   (e.g. `feat: ...`, `fix: ...`).
6. **Push and open a pull request** against `main`.

### Commit Message Style

Use conventional commits:

- `feat:` — a new feature
- `fix:` — a bug fix
- `docs:` — documentation only
- `refactor:` — code change that fixes neither a bug nor adds a feature
- `test:` — adding or correcting tests
- `ci:` — CI configuration changes

## Development Notes

- The repo has three parts: the Rust CLI (`crates/`), the .NET core
  (`runtime/Mate.Core/`), and the Unity runtime (`unity/`). Changes to
  `runtime/Mate.Core` are copied into `unity/Assets/MateFramework/Core/`
  (Unity on Linux cannot import symlinked folders).
- The Unity project is pinned to **Unity 6000.2.6f2**.
- Hot reload deliberately covers config/assets only, never C# code
  (see `docs/architecture/adr/013-build-pipeline-and-extensibility.md`).
- Run Rust tests from the repo root; run .NET tests from `runtime/`.

## Licensing

By contributing, you agree that your contributions are licensed under the
project's dual license — MIT OR Apache-2.0 (see `LICENSE-MIT` and
`LICENSE-APACHE`).
