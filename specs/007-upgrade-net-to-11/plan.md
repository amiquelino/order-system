# Implementation Plan: Upgrade .NET to 11

**Branch**: `007-upgrade-net-to-11` | **Date**: 2026-11-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-upgrade-net-to-11/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

Retarget all 13 .NET projects from `net10.0` to `net11.0`. Pin the SDK with a
repo-root `global.json` (11.0.100, `latestFeature`), and have CI read it
instead of hard-coding `10.0.x`. Then move dependencies according to a
readiness matrix checked in a spike:

- SignalR.StackExchangeRedis and Microsoft.Extensions.Hosting → **11.0.0**.
- Two `Microsoft.Extensions.*` references that are now pruned on net11.0
  (NU1510) are **removed**.
- EF Core, `.Design` and `dotnet-ef` stay on the **10.x line (→ 10.0.15)**,
  because the Npgsql provider has no stable 11. This is the one recorded
  exception.
- Aspire → **13.6.5**. This is the advisory fix, not the 17.0 major version.
- Wolverine and everything else stay where they are.

A spike with only the .NET 11 SDK installed showed the solution builds with
the audit gate on and 0 warnings, the Domain and Application tests pass
(57/57), EF tooling runs with no model drift, and an old SDK is rejected
with a clear message. The container-backed layers (integration, E2E) are
checked by CI on the PR. See [research.md](./research.md) (Decisions 1–6
and Risks).

## Technical Context

**Language/Version**: C# / .NET 10 → **.NET 11** (SDK 11.0.100, runtime 11.0.0, STS, end of support 2028-11-14)

**Primary Dependencies**: Packages changing: Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.14 → 11.0.0; Microsoft.Extensions.Hosting 10.0.10 → 11.0.0; Microsoft.EntityFrameworkCore{,.Design} 10.0.4 → 10.0.15; dotnet-ef 10.0.10 → 10.0.15; Aspire.AppHost.Sdk + Aspire.Hosting.* 13.6.4 → 13.6.5. Removed: Microsoft.Extensions.DependencyInjection.Abstractions (Adapters.Kafka), Microsoft.Extensions.Hosting.Abstractions (Adapters.RabbitMQ). Unchanged: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, WolverineFx 6.21.0, Confluent.Kafka, RabbitMQ.Client, Testcontainers, test tooling

**Storage**: PostgreSQL through EF Core 10 + Npgsql 10. No schema or migration change (spike: no pending model changes)

**Testing**: Existing suites only. xUnit + AwesomeAssertions (Domain, Application), Testcontainers (Adapters.IntegrationTests), Aspire.Hosting.Testing + Playwright (EndToEndTests). No new tests: the upgrade adds no behavior, so the proof is every existing layer passing on the new runtime (FR-008)

**Target Platform**: .NET Aspire distributed app on .NET 11; CI on GitHub Actions `ubuntu-latest`

**Project Type**: Runtime and dependency version change across existing projects plus build configuration. No new projects, ports, adapters or handlers

**Performance Goals**: None set. The feature makes no performance claims, and existing E2E timeouts stay as they are

**Constraints**: Stable packages only (FR-005). Audit gate unchanged and clean (FR-007). No tests removed, skipped or loosened (FR-008). No behavior change (FR-011). No package references added to Domain or Application (FR-012)

**Scale/Scope**: 13 `.csproj` files (TFM), 6 of them with package changes; 1 new `global.json`; `.config/dotnet-tools.json`; `.github/workflows/ci.yml` (4 setup-dotnet steps and 2 hard-coded `net10.0` Playwright paths); README (prerequisites and stack table)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable**: ✅ Consistent. Domain and
  Application still have no package references. They change only their
  TFM. The package changes are all in adapters, hosts and tests, and two of
  them remove references (FR-012).
- **II. Ports Are Discovered From the Core, Not the Adapter**: ✅ Not
  affected. No ports added or changed.
- **III. The Test Pyramid Mirrors the Architecture**: ✅ Consistent. A
  runtime upgrade can break each layer in its own way: Domain and
  Application through BCL behavior; adapters through EF/Npgsql, Kafka and
  RabbitMQ clients on a new runtime; the full system through Aspire, SignalR
  and Wolverine codegen. That's why validation is every tier passing, not a
  sample. The spike ran the two tiers it could; CI runs all four. If CI finds a
  regression only at the E2E level, the spec's edge case requires a test at the
  layer where it could come back.
- **IV. No Speculative Abstraction**: ✅ Consistent. This was the main
  temptation in this feature, and the plan resists it:
  - The TFM isn't centralized into `Directory.Build.props`.
  - Central Package Management isn't adopted.
  - Aspire isn't jumped to 17 and Wolverine isn't moved to 6.64, since
    neither is needed for .NET 11.
  - No new C# or library features are used (research Decisions 3, 5, 6).

  `global.json` is new, but it meets an actual requirement (FR-002) with
  the SDK's own mechanism, and removes the duplicated `10.0.x` in CI rather
  than adding configuration.
- **V. Every State Transition Announces Itself**: ✅ Not affected. No
  handler code changes. The E2E golden path (live status updates) confirms
  that `OrderStatusChangedEvent` still reaches the NotificationService over
  SignalR 11.
- **VI. Match the Project's Existing Idiom Before Reaching for a
  Framework's**: ✅ Consistent. Version handling follows spec 005's rule,
  which this repo already uses: change the parent package, add no
  transitive pins and no suppressions. The SDK pin uses the SDK's own
  `global.json`, the most common way to do it, not a custom MSBuild check.

**Post-design re-check (after Phase 1)**: No change. Phase 1 added no
entities, contracts or code, only the validation guide. No violations, so
Complexity Tracking is omitted.

## Project Structure

### Documentation (this feature)

```text
specs/007-upgrade-net-to-11/
├── spec.md              # /speckit-specify output
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0: spike results, readiness matrix, Decisions 1–6
├── data-model.md        # Phase 1: no entity or schema change (and why that is verified)
├── quickstart.md        # Phase 1: validation guide
└── tasks.md             # Phase 2 (/speckit-tasks; NOT created by /speckit-plan)
```

No `contracts/`: the feature exposes no new HTTP endpoint, message
contract or port.

### Source Code (repository root)

```text
global.json                                   # NEW: SDK 11.0.100, rollForward latestFeature (Decision 1)
.config/dotnet-tools.json                     # dotnet-ef 10.0.10 → 10.0.15 (Decision 2)
.github/workflows/ci.yml                      # setup-dotnet: global-json-file (×4); Playwright paths net10.0 → net11.0 (×2)
README.md                                     # Prerequisites: .NET 11 SDK; stack table: .NET 11

src/
├── Domain/OrderSystem.Domain/                # TFM only
├── Application/OrderSystem.Application/      # TFM only
└── Adapters/
    ├── OrderSystem.Adapters.Postgres/        # TFM; EF Core + .Design 10.0.4 → 10.0.15; Npgsql unchanged;
    │                                         #   comment recording the exception + exit condition (FR-006)
    ├── OrderSystem.Adapters.Kafka/           # TFM; remove M.E.DependencyInjection.Abstractions (Decision 4)
    └── OrderSystem.Adapters.RabbitMQ/        # TFM; remove M.E.Hosting.Abstractions (Decision 4)

hosts/
├── OrderSystem.Api/                          # TFM only
├── OrderSystem.NotificationService/          # TFM; SignalR.StackExchangeRedis 10.0.14 → 11.0.0
├── OrderSystem.MigrationService/             # TFM; EF .Design → 10.0.15; M.E.Hosting 10.0.10 → 11.0.0
└── OrderSystem.AppHost/                      # TFM; Aspire.AppHost.Sdk + Aspire.Hosting.* 13.6.4 → 13.6.5

tests/
├── OrderSystem.Domain.Tests/                 # TFM only
├── OrderSystem.Application.Tests/            # TFM only
├── OrderSystem.Adapters.IntegrationTests/    # TFM only
└── OrderSystem.EndToEndTests/                # TFM; Aspire.Hosting.Testing 13.6.4 → 13.6.5
```

**Structure Decision**: Edit the existing projects in place. The tree
shows all 13 projects in the solution (paths checked against the repo) with
the change each one gets. Nothing is
added except `global.json`. The frontend (`order-system-frontend/`) isn't
touched.

**Sequencing note**: If the .NET 10 security fix (Aspire 13.6.5 +
SignalR 10.0.15) merges to `main` first, as the spec assumes, rebase onto
it. The Aspire row above is then already done, and SignalR goes
10.0.15 → 11.0.0. If it hasn't merged, this branch makes the same changes,
so `main` ends up green either way.
