# Implementation Plan: Handle NuGet Package Vulnerabilities

**Branch**: `005-handle-nuget-packages-vulnerabilities` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-handle-nuget-packages-vulnerabilities/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

All three Baseline advisories are transitive, and each one already has an
upstream release of its *parent* package that depends on the patched version.
Remediation (P1) is therefore a set of parent-package version bumps, with no
transitive pins:

- Testcontainers 4.13.0 → 4.14.0 moves SSH.NET to 2026.0.0.
- SignalR.StackExchangeRedis 10.0.10 → 10.0.14 moves MessagePack to 2.5.303.
- Aspire 13.4.6 → 13.6.4 moves MessagePack to 2.5.307.

Recurrence prevention (P2) uses NuGet's built-in restore-time audit. A new
repo-root `Directory.Build.props` enables it for all severities and transitive
packages, and promotes NU1901–NU1904 to errors. A new `dependency-audit` CI
job restores the whole solution so every project is audited on every
push and PR. The FR-007 escape hatch is the SDK's own per-advisory
`NuGetAuditSuppress` item, documented but unused.

Notifications (P3) use GitHub's native features. Vulnerability alerts and
Dependabot security updates are enabled on the repository. A
`.github/dependabot.yml` covers NuGet, npm (frontend) and GitHub Actions, with
routine version PRs turned off. A PR-only `dependency-review` CI job fails on
**critical** advisories in any ecosystem. This deliberately sits alongside the
stricter NuGet gate rather than replacing it, because GitHub's dependency
graph can't see transitive NuGet packages. See [research.md](./research.md)
(Decisions 1–7).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.302 locally; CI uses `10.0.x`)

**Primary Dependencies**: NuGet restore audit (built into the SDK); GitHub Dependabot (alerts + security updates); `actions/dependency-review-action@v4`; packages being bumped: Testcontainers.{Kafka,PostgreSql,RabbitMq}, Microsoft.AspNetCore.SignalR.StackExchangeRedis, Aspire.AppHost.Sdk + Aspire.Hosting.{JavaScript,Kafka,PostgreSQL,RabbitMQ,Redis,Testing}

**Storage**: N/A

**Testing**: Existing suites only: xUnit + AwesomeAssertions (Domain, Application), Testcontainers (Adapters.IntegrationTests), Aspire.Hosting.Testing + Playwright (EndToEndTests). No new tests: the "test" for P2 is the gate itself, validated by the quickstart's deliberate-vulnerable-package check.

**Target Platform**: Build/CI configuration (GitHub Actions `ubuntu-latest`, plus local dev builds)

**Project Type**: Build configuration + dependency version changes. No new projects, ports, adapters or handlers.

**Performance Goals**: The new CI job adds ≤ ~1 minute (a solution restore); it runs in parallel with existing jobs.

**Constraints**: No application code or behavior change (FR-003). No change to `Domain`/`Application` package references. No new third-party scanning service. Every existing CI job must stay green.

**Scale/Scope**: 1 new file (`Directory.Build.props`); 4 `.csproj` files edited (AppHost, including its `Aspire.AppHost.Sdk` version; NotificationService; EndToEndTests; Adapters.IntegrationTests); 1 workflow edited (`ci.yml`: 2 new jobs); 1 new `.github/dependabot.yml`; 2 repository security settings enabled; README note.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable**: ✅ Consistent. `OrderSystem.Domain` and
  `OrderSystem.Application` package references are untouched. The bumped
  packages live in hosts and test projects. `Directory.Build.props` adds only
  NuGet restore properties, not package references, so Domain still has zero
  dependencies.
- **II. Ports Are Discovered From the Core, Not the Adapter**: ✅ Not affected. No ports
  are added or changed.
- **III. The Test Pyramid Mirrors the Architecture**: ✅ Consistent. This feature changes
  dependency versions under every tier, so validation is "every existing tier
  still passes" (FR-003 / SC-002). That's exactly what the per-tier CI jobs
  prove. No tier is skipped. The new gate is a build rule, not runtime
  behavior, so it's validated by the quickstart's deliberate-failure check
  rather than an xUnit test. The same applies to US3's PR check
  (quickstart §6).
- **IV. No Speculative Abstraction**: ✅ Consistent, with one judgment call.
  `Directory.Build.props` is new, but it serves an existing need: three
  findings already slipped in unnoticed. It holds only the audit settings. It
  doesn't take this chance to centralize other properties (TargetFramework,
  Nullable) or adopt Central Package Management. The FR-007 suppression
  mechanism is documented, not used. It's the SDK's built-in item, so no
  custom code exists for a case the project doesn't have. Dependabot (US3)
  is configured for **security updates only** (`open-pull-requests-limit: 0`),
  and adds no routine version-bump churn that no requirement asked for.
- **V. Every State Transition Announces Itself**: ✅ Not affected. No handler
  changes. The end-to-end tier, which observes live status updates through
  NotificationService/SignalR, is the regression guard for the SignalR Redis
  bump.
- **VI. Match the Project's Existing Idiom**: ✅ Consistent. It uses the
  toolchain's own audit (`NuGetAudit`, `NuGetAuditSuppress`) rather than an
  external scanner, and the existing GitHub Actions workflow with its
  one-job-per-concern convention (feature 002, research Decision 2). Tradeoff
  recorded in research Decisions 2 and 4: an external scanner could add
  dashboards and auto-PRs, but it can't block a local build, and it adds a
  service to maintain. Dependabot and dependency-review (US3) are GitHub's
  own features, on the platform the repo already uses, so no third-party
  service is added (research Decision 7).

**Result**: No violations. Complexity Tracking omitted.

*Post-Phase 1 re-check*: The Phase 1 design (quickstart; no data model or
contracts, see below) introduced nothing new. Still no violations.

*Re-check after adding US3 (2026-11-01)*: Still no violations. The two
severity thresholds (any for NuGet, critical cross-ecosystem) are a
deliberate layering recorded in the spec's Assumptions and research
Decision 7, not a contradiction.

## Project Structure

### Documentation (this feature)

```text
specs/005-handle-nuget-packages-vulnerabilities/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: seven decisions (upgrade strategy, gate, suppression, CI job, NU1900, fallback, Dependabot + dependency review)
├── quickstart.md        # Phase 1: audit, gate-fails, gate-passes, suppression, Dependabot/dependency-review checks
└── tasks.md             # Phase 2 (/speckit-tasks; not created here)
```

There's no `data-model.md`, because the feature introduces no entities,
state, or persistence. There's no `contracts/`, because it exposes no new
endpoint, message, or port. Both are omitted, as feature 002 (CI) did.

### Source Code (repository root)

```text
Directory.Build.props                                   # NEW: NuGetAudit on, mode=all, level=low,
                                                        #      WarningsAsErrors NU1901–NU1904,
                                                        #      commented NuGetAuditSuppress how-to

.github/workflows/ci.yml                                # EDIT: add `dependency-audit` job
                                                        #       (dotnet restore OrderSystem.slnx) and
                                                        #       PR-only `dependency-review` job
                                                        #       (fail-on-severity: critical)
.github/dependabot.yml                                  # NEW: nuget + npm (order-system-frontend) +
                                                        #      github-actions; security updates only
                                                        #      (open-pull-requests-limit: 0)
(GitHub repo settings)                                  # ENABLE: vulnerability alerts, automated
                                                        #         security fixes (repo admin)

hosts/OrderSystem.AppHost/OrderSystem.AppHost.csproj    # EDIT: Aspire.AppHost.Sdk 13.4.6 → 13.6.4;
                                                        #       Aspire.Hosting.{JavaScript,Kafka,PostgreSQL,
                                                        #       RabbitMQ,Redis} 13.4.6 → 13.6.4
hosts/OrderSystem.NotificationService/
  OrderSystem.NotificationService.csproj                # EDIT: SignalR.StackExchangeRedis 10.0.10 → 10.0.14

tests/OrderSystem.EndToEndTests/
  OrderSystem.EndToEndTests.csproj                      # EDIT: Aspire.Hosting.Testing 13.4.6 → 13.6.4
tests/OrderSystem.Adapters.IntegrationTests/
  OrderSystem.Adapters.IntegrationTests.csproj          # EDIT: Testcontainers.{Kafka,PostgreSql,RabbitMq}
                                                        #       4.13.0 → 4.14.0

README.md                                               # EDIT: short "Dependency vulnerabilities" note:
                                                        #       build fails on advisories; how to fix / accept

src/Domain/**, src/Application/**, src/Adapters/**,
hosts/OrderSystem.Api, hosts/OrderSystem.MigrationService  # UNCHANGED
```

**Structure Decision**: No new projects. The only new file is a repo-root
`Directory.Build.props`. MSBuild picks it up automatically for all 13 projects
under `src/`, `hosts/`, and `tests/`, including future ones, so the gate can't be forgotten on a new project.
`order-system-frontend/` isn't an MSBuild project and is unaffected, which
matches the spec's NuGet-only scope. Version bumps are confined to the four
projects named in the Baseline. If the Aspire or Testcontainers bump breaks
its tier, research Decision 6 defines the per-finding fallback (direct pin
with a removal comment).
