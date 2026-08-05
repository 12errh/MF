---
feature: phase2-runtime-core
status: delivered
updated: 2026-08-05
branch: phase2-runtime-core
commits: e0dde69..2ccb018
---

# Phase 2: Runtime Core

## Report

**What was built** — A new tracked `runtime/` .NET solution (`Mate.Core` class library + `Mate.Core.Tests` NUnit project, net8.0) implementing the pure-.NET core of the Unity runtime foundation: the `Result<T>`/`Result` error pattern and `WindowInfo`/`MonitorInfo`/`Rectangle`/`ChatMessage` records; a typed `SimpleEventBus` with subscribe/unsubscribe/publish/clear; the `MateContext` DI container (factory + singleton + dispose); `PlatformDetector` with XDG env-var detection returning an `IPlatformCapabilities` implementation; the `IWindowService` and `IPlatformCapabilities` interfaces; and `FileConfiguration` (a JSON `settings.json` store via Newtonsoft.Json). The Unity-bound `LinuxX11Backend` adapter was deferred per the environment decision.

**Verification** — `dotnet build runtime/Mate.Core.sln` PASS (0 warnings, 0 errors); `dotnet test runtime/Mate.Core.sln` PASS (33 tests: 5 model + 6 event bus + 7 context + 10 platform + 5 config).

**Journey log** —
- The plan's `IWindowService` references a non-generic `Result`, but the plan only defined `Result<T>`; added a non-generic `Result` for void operations.
- `System.Numerics.Rectangle` does not exist in .NET; the plan's `MonitorInfo` test referenced it. Used the plan's own `Rectangle` record instead.
- The plan's `MateContextTests` passed an instance to `Register<T>(Func<T>)`; fixed the tests to use factory lambdas.
- The NUnit.Analyzers style package generated 21 warnings on the plan's classic `Assert.AreEqual` style; removed the analyzer to keep the plan's test style with a clean build. Nullable was disabled in both csproj to match the plan's Unity-style C#.
- Review found `SimpleEventBus` mutating the handler list during publish (throws on unsubscribe-in-handler) and vacuous `PlatformDetectorTests`; fixed with a publish snapshot and meaningful env-var tests, plus added `Clear`/`IsRegistered`/`Reload` coverage (23 → 33 tests).

## [S1] Problem

The Mate Framework needs a C# runtime foundation so the Unity player can be structured without god-object singletons. Phase 2 builds the core: a `Result<T>` error pattern, data models, a typed event bus, a service container (`MateContext`) replacing singletons, platform detection, the `IWindowService`/`IPlatformCapabilities` interfaces, and file-based configuration.

**Environment constraint:** This workspace has no Unity Editor and no C# toolchain. The plan's Unity Test Runner verification cannot run here. Per user decision, the pure-.NET core of Phase 2 is implemented in a new tracked `runtime/` directory and verified with the .NET SDK + NUnit via `dotnet test`. The Unity-bound `LinuxX11Backend` adapter (which wraps the existing `WindowManager.cs` monolith) is deferred until a Unity environment is available.

## [S2] Design

- New tracked `runtime/` directory (not inside the gitignored `refrence/` Unity project) containing a .NET solution:
  - `Mate.Core` — class library (net8.0) with namespaces `Mate.Core`, `Mate.Core.Models`, `Mate.Core.Interfaces`, `Mate.Platform`.
  - `Mate.Core.Tests` — NUnit test project (net8.0).
- Components (all pure .NET Standard-compatible, no UnityEngine dependency):
  - `Models/MateError.cs`: `Result<T>` (Value/IsSuccess/Error, `Ok`/`Fail`, implicit conversion) and a non-generic `Result` for void operations.
  - `Models/WindowInfo.cs`: records `WindowInfo`, `MonitorInfo`, `Rectangle` (using `System.Numerics.Vector2` and a custom `Rectangle` record — `System.Numerics.Rectangle` does not exist).
  - `Models/ChatMessage.cs`: record `ChatMessage(Role, Content)`.
  - `Core/IEventBus.cs` + `Core/SimpleEventBus.cs`: typed `Subscribe<T>`/`Unsubscribe`/`Publish<T>`/`Clear` with a `SubscriptionToken`.
  - `Core/MateContext.cs`: DI container with `Register<T>(factory)`, `RegisterSingleton<T>(instance)`, `Resolve<T>`, `IsRegistered<T>`, `Dispose` (disposes `IDisposable` singletons).
  - `Core/IConfiguration.cs` + `Core/FileConfiguration.cs`: JSON settings store (`settings.json`) via Newtonsoft.Json, with `GetFloat/GetInt/GetString/GetBool/Set/Save/Reload`.
  - `Interfaces/IPlatformCapabilities.cs`: capability query interface.
  - `Interfaces/IWindowService.cs`: async window-management interface (`Task<Result<...>>`), pure .NET.
  - `Platform/PlatformDetector.cs`: XDG env-var detection (`XDG_CURRENT_DESKTOP`, `XDG_SESSION_TYPE`, `HYPRLAND_INSTANCE_SIGNATURE`) returning an `IPlatformCapabilities` implementation.
- No `FindFirstObjectByType`, no `Singleton<T>` in new code. All public APIs documented.
- Verification: `dotnet test` runs 33 NUnit tests (5 model + 6 event bus + 7 context + 10 platform + 5 config).

## [S3] Out of Scope

- `LinuxX11Backend` adapter wrapping `WindowManager.cs` and `LinuxHyprlandBackend` — deferred until a Unity environment is available (Unity-bound, cannot compile/test here).
- Unity Test Runner / Unity Editor integration.
- The existing C# monoliths in `refrence/`.
- Committing `refrence/` or planning docs.

## Tasks

- [x] T1: Scaffold `runtime/` .NET solution (Mate.Core lib + Mate.Core.Tests NUnit) — acceptance: `dotnet build` succeeds (covers: S2)
- [x] T2: Result pattern + models (Result<T>, Result, WindowInfo, MonitorInfo, ChatMessage) — acceptance: `dotnet test` passes 5 model tests (covers: S2; depends: T1)
- [x] T3: Typed event bus (IEventBus, SimpleEventBus) — acceptance: `dotnet test` passes 6 event-bus tests (covers: S2; depends: T1)
- [x] T4: MateContext service container — acceptance: `dotnet test` passes 7 context tests (covers: S2; depends: T1)
- [x] T5: PlatformDetector + IPlatformCapabilities — acceptance: `dotnet test` passes 10 platform tests (covers: S2; depends: T1)
- [x] T6: IWindowService interface — acceptance: project compiles with the interface (covers: S2; depends: T1)
- [x] T7: FileConfiguration — acceptance: `dotnet test` passes 5 config tests (covers: S2; depends: T1)