# Research: Upgrade .NET to 11

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-11-19

## Method

Every decision below comes from a spike, not from release notes alone. A
throwaway worktree of `007-upgrade-net-to-11` (at `5009e85`) was retargeted
to `net11.0` and built with the **.NET 11.0.100 SDK** (runtime 11.0.0,
released 2026-11-10). Only that SDK was installed, with no .NET 10 runtime
alongside, so nothing could quietly fall back to .NET 10.

| Spike check | Result |
|---|---|
| `dotnet build OrderSystem.slnx` (NuGet audit gate on: NU1901–NU1904 as errors) | ✅ Build succeeded, 0 errors. 4 × NU1510 warnings, then 0 warnings after Decision 4 |
| `dotnet test` Domain.Tests | ✅ 21/21 passed (`net11.0`) |
| `dotnet test` Application.Tests | ✅ 36/36 passed (`net11.0`) |
| `dotnet tool run dotnet-ef` (tool 10.0.10) on the .NET 11 runtime only | ✅ Runs |
| `dotnet ef migrations has-pending-model-changes` (startup project: MigrationService) | ✅ "No changes have been made to the model since the last migration." |
| Old SDK (10.0.302) against a `global.json` pinning 11.0.100 | ✅ Fails immediately: "A compatible .NET SDK was not found. Requested SDK version: 11.0.100 … Install the [11.0.100] .NET SDK" |
| Adapters.IntegrationTests, EndToEndTests | ⚠️ Not run: no Docker on the spike machine. Covered by CI on the PR (see Risks) |

## Package readiness matrix (nuget.org, 2026-11-19)

| Package (current) | Stable 11.x? | Constraint found | Decision |
|---|---|---|---|
| Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.14 | ✅ 11.0.0 | Framework-aligned. Also fixes GHSA-wr8v-9x5q-ggcm (<10.0.15 / <11.0.0) | → **11.0.0** |
| Microsoft.Extensions.Hosting 10.0.10 (MigrationService) | ✅ 11.0.0 | — | → **11.0.0** |
| Microsoft.Extensions.DependencyInjection.Abstractions 10.0.10 (Adapters.Kafka) | ✅ 11.0.0 | NU1510 on net11.0: the package is now pruned (comes with the framework) | **Remove the reference** (Decision 4) |
| Microsoft.Extensions.Hosting.Abstractions 10.0.10 (Adapters.RabbitMQ) | ✅ 11.0.0 | NU1510 on net11.0, same as above | **Remove the reference** (Decision 4) |
| Microsoft.EntityFrameworkCore / .Design 10.0.4 | ✅ 11.0.0 | Blocked by the Npgsql provider (Decision 2) | Stay on 10.x → **10.0.15** (latest 10.x patch) |
| Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 | ❌ 11.0.0-rc.3 only | 10.0.3 requires EF Core `[10.0.4, 11.0.0)` | Stay on **10.0.3** (latest stable) |
| dotnet-ef local tool 10.0.10 | ✅ 11.0.0 | Must match the EF Core major version | → **10.0.15**, to match EF Core |
| Aspire.AppHost.Sdk + Aspire.Hosting.* 13.6.4 | 17.0.0 (first with a `net11.0` asset) | 13.6.4 has critical/high advisories. 13.6.5 builds and runs on net11.0 through its net10.0 asset | → **13.6.5** (Decision 3) |
| WolverineFx / .RuntimeCompilation 6.21.0 | n/a (7.0 is alpha) | 6.x net10.0 asset depends on Microsoft.Extensions.* `10.0.0` with no upper bound | Stay on **6.21.0** (Decision 5) |
| Confluent.Kafka 2.15.0, RabbitMQ.Client 7.2.1 | n/a | netstandard / net8+ assets. Built clean | No change |
| Testcontainers.* 4.14.0, Microsoft.Playwright 1.61.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4, AwesomeAssertions 9.5.0, Bogus 35.6.5 | n/a | Independent of the .NET version. Built and ran clean | No change |

## Decisions

### Decision 1 — Pin the SDK with a repo-root `global.json`, and have CI read it

- **Decision**: Add `global.json` with `"sdk": { "version": "11.0.100",
  "rollForward": "latestFeature" }`. In all four CI jobs, replace
  `dotnet-version: "10.0.x"` with `global-json-file: global.json` on
  `actions/setup-dotnet`.
- **Rationale**: FR-002 needs an older SDK to fail immediately with a
  message that names the required version. The spike shows `global.json`
  does exactly that, with no custom check. CI reading the same file
  keeps one source of truth, so FR-009 can't drift from the local
  requirement. `latestFeature` accepts any 11.0.1xx-or-later feature band,
  so contributors don't need the exact patch.
- **Alternatives considered**: A custom MSBuild `<Error>` target
  that checks `$(NETCoreSdkVersion)`. It's more code for something the SDK
  already does (Principle IV). Leaving the SDK unpinned (the status quo) fails
  FR-002: an older SDK only finds out per project, after evaluation has
  started, that it can't target `net11.0`, instead of failing once, right
  away, with the required version. `rollForward: latestMajor` would let a future
  .NET 12 SDK build silently, which defeats the purpose of the pin.

### Decision 2 — EF Core stays on 10.x, because the Npgsql provider has no stable 11

- **Decision**: Keep `Microsoft.EntityFrameworkCore`, `.Design` and the
  `dotnet-ef` tool on the 10.x line, and move them to the latest patch,
  10.0.15. Keep `Npgsql.EntityFrameworkCore.PostgreSQL` at 10.0.3. Record
  this as the feature's one framework-aligned exception (FR-006). **Exit
  condition**: Npgsql.EntityFrameworkCore.PostgreSQL ships a stable 11.0.0.
  Then move EF Core, `.Design`, `dotnet-ef` and the provider to 11.x together.
- **Rationale**: Npgsql 10.0.3 declares EF Core `[10.0.4, 11.0.0)`, so
  EF Core 11 can't resolve against it. The only way to EF Core 11 today is the
  provider's `11.0.0-rc.3`, which FR-005 rules out. The spike shows the 10.x
  stack builds, its tooling runs, and the model snapshot doesn't drift on the
  .NET 11 runtime. The spec's edge case expected exactly this. Moving to
  10.0.15 keeps the line patched and costs nothing. All four are bumped in
  step so the tool and the runtime library match.
- **Alternatives considered**: Adopting the provider's RC (violates
  FR-005). Waiting for the provider (the spec says the upgrade doesn't wait).
  EF Core 11 with an unmatched provider: NuGet resolution fails.

### Decision 3 — Aspire 13.6.4 → 13.6.5, not 17.0.0

- **Decision**: Move `Aspire.AppHost.Sdk` and all six `Aspire.Hosting.*`
  references to **13.6.5**.
- **Rationale**: Leaving them alone isn't an option: 13.6.4 has
  GHSA-4g86-54fm-h9h8 (critical) and GHSA-f7vh-h5g3-7jrh (high), which
  fail the restore gate (FR-007). 13.6.5 is the first patched version. The
  spike shows it builds a `net11.0` AppHost and EndToEndTests through its
  `net10.0` asset. FR-004 allows a major version bump only when needed for
  .NET 11 compatibility, and it isn't needed here. 17.0.0 is a four-major
  jump in the orchestrator with its own breaking changes, and doesn't belong
  in a runtime upgrade.
- **Alternatives considered**: Aspire 17.0.0 (the first with a `net11.0`
  asset). Not required, and adds unrelated risk. Revisit as its own change.
- **Interaction with the open security fix**: the spec assumes the
  .NET 10 security fix (Aspire 13.6.5 + SignalR 10.0.15) lands on `main`
  first. If it does, this decision is already done when this branch
  rebases. If it doesn't, this feature makes the same change and fixes
  `main`'s red build as a side effect. Either way the end state is the same.

### Decision 4 — Remove the two `Microsoft.Extensions.*` references that NU1510 flags as pruned

- **Decision**: Delete `Microsoft.Extensions.DependencyInjection.Abstractions`
  from `OrderSystem.Adapters.Kafka.csproj` and
  `Microsoft.Extensions.Hosting.Abstractions` from
  `OrderSystem.Adapters.RabbitMQ.csproj`.
- **Rationale**: On `net11.0`, the SDK reports both as
  "automatically available and does not need to be referenced explicitly.
  Remove the PackageReference item." The spec's edge case says new warnings are
  fixed, not suppressed. The spike shows the solution builds with 0 warnings
  after removing them. It's less to keep upgraded later, too.
- **Alternatives considered**: Bumping them to 11.0.0 and keeping them
  (still warns). Using `NoWarn` for NU1510 (suppresses instead of fixing).

### Decision 5 — Wolverine stays on 6.21.0

- **Decision**: No change to `WolverineFx` or
  `WolverineFx.RuntimeCompilation`.
- **Rationale**: The only Wolverine line newer than 6.x is 7.0 alpha
  (ruled out by FR-005). 6.21.0's `net10.0` asset needs
  `Microsoft.Extensions.* >= 10.0.0` with no upper bound, so it resolves cleanly
  next to the 11.0.0 framework. The spike build and the Application tests pass.
  Moving to 6.64.2 is a version-update choice unrelated to .NET 11 (FR-004:
  move only when needed for compatibility).
- **Alternatives considered**: 6.64.2 (not required). 7.0.0-alpha.2
  (prerelease).

### Decision 6 — Don't use new .NET 11 or C# features, and don't centralize the TargetFramework

- **Decision**: Change `<TargetFramework>` in each of the 13 project files,
  in place. Don't move it into `Directory.Build.props`, and don't adopt
  Central Package Management.
- **Rationale**: The spec scopes this to the runtime and dependency
  versions only. Spec 005 also deliberately kept `Directory.Build.props`
  limited to the audit settings (its plan's Principle IV note).
  Centralizing the TFM would make future upgrades a one-line change, but no
  next upgrade is scheduled yet, so that's speculative (Principle IV). The
  13-file edit is mechanical and reviewable.
- **Alternatives considered**: `<TargetFramework>` in
  `Directory.Build.props`. Reasonable, but the spec didn't ask for it.
  Propose it separately if the .NET 12 upgrade comes.

## Risks validated only in CI

The spike machine had no Docker, so these run for the first time on the
PR's CI:

- **Wolverine runtime code generation** (`WolverineFx.RuntimeCompilation`
  compiles handlers with Roslyn at startup) runs only when a host starts.
  The integration and end-to-end jobs start the hosts.
- **Npgsql / EF Core 10 against a real PostgreSQL on the .NET 11 runtime**:
  Adapters.IntegrationTests (Testcontainers).
- **Aspire 13.6.5 orchestrating `net11.0` projects, SignalR 11 with the
  Redis backplane, and the golden path**: EndToEndTests (Playwright).

If any of these fail, the decision it touches is reopened. Tests are not
loosened (FR-008).
