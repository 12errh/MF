# ADR-013: Build Pipeline & Extensibility Model

## Status

Accepted

## Context

The current docs have three gaps that threaten the "no Unity Editor" developer promise:

1. **`mf build` spec requires Unity Editor** — TRD.md defines `mf build` as "Unity batch mode build" and "Build AssetBundles for project content." `BuildPipeline.BuildAssetBundles` is an Editor-only API. This means shipping a real app requires Unity installed, contradicting ADR-002's "no Unity dependency" and the PRD's "developers never interact with Unity."

2. **No defined mechanism for custom logic** — ADR-006 says "developer scripts (if any) are loaded dynamically via reflection" but never specifies how those get compiled. If a dev wants behavior beyond TOML config (custom movement, LLM tool-calling), the only implied path is a Unity project.

3. **Hot reload expectations unclear** — Every doc correctly states "C# hot reload not feasible in v1" but doesn't clarify what happens if custom code exists.

## Decision

### 1. `mf build` does NOT require Unity Editor

The framework's loading pipeline already supports raw asset files at runtime:
- `VRMLoader.cs` loads `.vrm` files directly via UniVRM (no AssetBundle compilation needed)
- `PulseAudioManager` loads sounds at runtime
- `AvatarAnimatorController` loads `.anim` clips at runtime

Therefore `mf build` performs **packaging only**, not compilation:

```
mf build workflow:
  1. Validate mate.toml (required fields, path existence, value ranges)
  2. Resolve runtime version (download if not cached)
  3. Copy prebuilt Unity player + Mate assemblies from runtime cache
  4. Copy raw project assets (VRM, animations, sounds, textures) as-is
  5. Copy config files (mate.toml, personality.toml, etc.)
  6. Write manifest with asset paths and runtime version
  7. Output: build/<project-name>-linux-x64/ (ready to run)
```

**No `BuildPipeline` calls. No AssetBundle compilation. No Unity Editor.**

The developer's machine needs only:
- Rust toolchain (for `mf` CLI)
- .NET SDK (optional, only if writing custom plugins in v2)

Unity is only needed by the **framework team** to build the prebuilt runtime, which is distributed as a release artifact.

### 2. v1 has NO code extensibility — config + assets only

The extensibility model is explicitly tiered:

| Version | Extensibility | Mechanism |
|---------|--------------|-----------|
| v1.0 | Config only | `mate.toml` sections, `personality.toml`, runtime settings |
| v1.x | Asset mods | `mods/` directory with sounds, animations, textures (ADR-010) |
| v2.0 | Code plugins | `dotnet build` compiled DLLs loaded via reflection |

**v1 scope is deliberately narrow.** A developer who wants custom behavior beyond what TOML configures:
- Custom idle/dance animations → supply `.anim` files via mods (v1.x)
- Custom sound effects → supply `.wav` files via mods (v1.x)
- Custom AI personality → edit `personality.toml` (v1.0)
- Custom movement logic → NOT SUPPORTED in v1 (v2.0 plugin system)

This is a feature, not a limitation. It keeps the "no thousands of lines" promise. The 20+ avatar handler MonoBehaviours in the reference codebase are what make it hard to understand — the framework deliberately absorbs that complexity.

### 3. Hot reload scope is explicit

| Change type | Reload behavior | Restart required |
|-------------|----------------|-----------------|
| `mate.toml` edits | Live reload (file watcher) | No |
| `personality.toml` edits | Live reload | No |
| Asset swap (VRM, sounds) | Model reload on next file change | No |
| New mod added to `mods/` | Loaded on next file change | No |
| Custom plugin DLL (v2) | Not supported | Yes, full restart |
| Runtime settings change | Live reload | No |

## Rationale

1. **Zero Unity dependency for developers** — This is the core promise. Removing AssetBundle compilation from `mf build` eliminates the last reason a developer would need Unity installed.

2. **VRM loading already works without AssetBundles** — `VRMLoader.LoadVRM()` uses UniVRM to parse `.vrm` files directly. The AssetBundle path in the reference code is for `.me` format (the app's own format), not the framework's format.

3. **Config + assets covers 90% of use cases** — The reference codebase's 30+ avatar handlers are mostly config-driven (enable/disable, threshold values, timing). The framework exposes these as TOML knobs.

4. **Code extensibility deferred to v2** — The plugin system (ADR-010 v2 design) already exists as a concept. Deferring it avoids scope creep (R09) while the core framework stabilizes.

5. **Simpler distribution** — Raw assets are smaller than AssetBundles (no Unity-specific headers). `mf build` output is just files + a zip.

## Consequences

### Updated TRD.md changes
- `mf build` is now: validate → copy runtime → copy assets → copy config → zip
- No Unity batch mode build step
- No AssetBundle compilation
- Build output is a directory (or archive) ready to run

### Updated IMPLEMENTATION_PLAN.md changes
- Phase 7 (Build & Package) simplifies significantly
- Remove "Unity batch mode build integration" task
- Remove "AssetBundle compilation for project content" task
- Add "Raw asset packaging" task

### Updated ADR-006 changes
- Remove "developer scripts (if any) are loaded dynamically via reflection" (deferred to v2)
- Clarify that v1 runtime loads only framework assemblies, not developer code

### Updated PRD.md changes
- P3 "Hot reload" already correct (config only)
- Add note: v1 extensibility is config + asset mods only

## Alternatives Considered

1. **Ship Unity Editor as part of runtime** — Rejected: defeats the "no Unity" purpose, 5GB+ install
2. **Use Unity headless build in CI only** — Partially acceptable for framework team's runtime builds, but still requires Unity on the build machine, not the developer's
3. **Custom AssetBundle builder in Rust** — Rejected: Unity AssetBundle format is internal and version-locked. Not feasible.
4. **Allow C# scripting in v1** — Rejected: scope creep. The reference codebase's complexity comes from 30+ MonoBehaviours with implicit dependencies. v1 absorbs that complexity into the runtime. Code extensibility is v2.
