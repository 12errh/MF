# Risk Register: Mate Framework

## Risk Matrix

| ID | Risk | Probability | Impact | Severity | Phase | Mitigation |
|----|------|------------|--------|----------|-------|-----------|
| R01 | ~~License incompatibility~~ | ~~HIGH~~ | ~~CRITICAL~~ | ~~P0~~ | ~~Phase 0~~ | **RESOLVED**: Framework is open-source + non-commercial, compatible with MateEngine Pro License v2.0 |
| R02 | Unity runtime too large for distribution (>500MB) | MEDIUM | HIGH | P1 | Phase 3 | Strip unused Unity modules, use build profiles, compress runtime |
| R03 | X11 P/Invoke code breaks during migration | MEDIUM | HIGH | P1 | Phase 2 | Wrap existing code in adapters, test against existing behaviors |
| R04 | Performance regression vs original application | MEDIUM | HIGH | P1 | Phase 9 | Profile every phase, establish baselines before migration |
| R05 | Hyprland/KWin integration regressions | MEDIUM | MEDIUM | P2 | Phase 2 | Dedicated integration tests for each compositor |
| R06 | Unity version lock prevents security updates | LOW | MEDIUM | P3 | Phase 0 | Design runtime as swappable component, track Unity LTS |
| R07 | VRM loading regression (format compatibility) | LOW | HIGH | P2 | Phase 3 | Test with community VRM models, maintain UniVRM version |
| R08 | Audio monitoring breaks on PulseAudio upgrades | LOW | MEDIUM | P3 | Phase 4 | Pin PulseAudio version requirements, test multiple versions |
| R09 | Scope creep delays v1.0 release | HIGH | HIGH | P1 | All | Strict phase gates, P0 features only for v1.0 |
| R10 | Developer adoption below expectations | MEDIUM | MEDIUM | P2 | Phase 10 | Demo project, community outreach, clear documentation |
| R11 | Single developer bus factor | HIGH | CRITICAL | P0 | All | Document everything, automated CI, clear architecture |
| R12 | Unity license changes (runtime pricing) | LOW | CRITICAL | P2 | Phase 0 | Monitor Unity terms, maintain Godot migration path |
| R13 | Third-party dependency abandoned | LOW | MEDIUM | P3 | All | Pin versions, maintain forks for critical deps |
| R14 | AI model licensing restrictions | MEDIUM | MEDIUM | P2 | Phase 6 | Use open-licensed models only (Apache 2.0, MIT) |
| R15 | Incompatible with newer Linux distributions | LOW | HIGH | P2 | Phase 9 | Test on Ubuntu, Fedora, Arch; use portable native libs |

## Detailed Risk Analysis

### R01: ~~License Incompatibility~~ — RESOLVED

**Current state:** MateEngine Pro License v2.0 is copyleft + non-commercial + source-disclosure. The Mate Framework is also open-source and non-commercial, making the licenses compatible. Framework must also be released under copyleft + same license terms. Source disclosure required for derivative works.

### R02: Unity Runtime Size

**Current estimate:** Unity 6 player for Linux x64 is ~150-200MB.

**Mitigation:**
- Strip unused Unity modules (AI, terrain, networking)
- Use build profiles to exclude unused packages
- Compress runtime archive (tar.gz)
- Consider IL2CPP for smaller binary (but lose dynamic loading)

### R09: Scope Creep

**Current risk:** The 38-section task document is very comprehensive.

**Mitigation:**
- Phase gates: no phase starts until previous phase passes all exit criteria
- P0 features only for v1.0 (12 core features)
- P1-P3 features deferred to future releases
- Weekly scope review

### R11: Bus Factor

**Current state:** Single developer.

**Mitigation:**
- Complete architecture documentation (this document set)
- Automated CI/CD
- Clear module boundaries
- Open-source the framework (after license resolution)
