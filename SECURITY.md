# Security

The Mate Framework CLI and runtime take a defense-in-depth approach. This
document is the security audit checklist for contributors and maintainers.

## Audit Checklist

### Credentials & Secrets

- [ ] No hardcoded credentials, API keys, or tokens anywhere in the codebase.
- [ ] Secrets are read from environment variables or user config at runtime only.
- [ ] `mate.toml` and other project files never contain secrets by default.

### Input Validation

- [ ] Project names are validated before `mf new` creates directories
      (no path traversal: `..`, `/`, `\` rejected).
- [ ] File paths are validated against the project directory before use
      (`mf_core::security::validate_path`) — paths that escape the project
      directory are rejected.
- [ ] URL validation enforces HTTPS only (`mf_core::security::validate_url`);
      plain HTTP endpoints are rejected.

### Runtime & Downloads

- [ ] Runtime artifacts are downloaded from GitHub Releases over HTTPS only.
- [ ] Runtime version strings are validated as semantic versions before any
      filesystem operation.
- [ ] No shell interpolation of untrusted strings into commands — the tar
      step uses argument-passing, never `sh -c` with concatenation.

### AI Endpoints

- [ ] AI provider endpoints are configurable via `mate.toml`; no provider
      credentials are baked into the binary.
- [ ] Ollama is a localhost service by default; remote endpoints are opt-in
      and user-configured.

## Reporting

Found a vulnerability? Do not open a public issue. Contact the maintainers
directly and describe the issue, the affected version, and a reproduction.
