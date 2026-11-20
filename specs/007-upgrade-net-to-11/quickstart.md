# Quickstart: Validate the .NET 11 Upgrade

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Run from the repo root on the `007-upgrade-net-to-11` branch after
implementation. Each step names the requirement or success criterion it
proves.

## Prerequisites

- **.NET 11 SDK** (11.0.100 or a later 11.0.1xx feature band).
- Docker running (Testcontainers and the Aspire AppHost need it).
- Node.js 24 with frontend dependencies installed
  (`cd order-system-frontend && npm ci`).

## 1. The SDK pin is enforced (FR-002, US1 scenario 4)

```bash
dotnet --version            # expect 11.0.1xx, selected by global.json
```

On a machine that has only the .NET 10 SDK, `dotnet build OrderSystem.slnx`
must fail right away with:

```text
A compatible .NET SDK was not found.
Requested SDK version: 11.0.100
```

## 2. Every project targets .NET 11 (FR-001, SC-001)

```bash
grep -rhoE '<TargetFramework>[^<]+' --include=*.csproj . | sort | uniq -c
```

Expected: exactly one line, `13 <TargetFramework>net11.0`. No `net10.0`
anywhere.

## 3. Builds with the audit gate clean, no warnings (FR-007, SC-003)

```bash
dotnet build OrderSystem.slnx
```

Expected: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`. The NuGet audit
runs during restore with NU1901–NU1904 as errors, so a clean build means the
audit is clean. Also confirm no suppressions were added:

```bash
git diff main -- Directory.Build.props   # expect no NuGetAuditSuppress / NoWarn additions
```

## 4. No prerelease packages, and the exception is recorded (FR-005, FR-006, SC-004, SC-006)

```bash
grep -rhoE 'Version="[^"]+"' --include=*.csproj . .config/dotnet-tools.json | grep -iE 'alpha|beta|preview|rc' \
  && echo "PRERELEASE FOUND" || echo "no prerelease packages"
grep -n "Npgsql" src/Adapters/OrderSystem.Adapters.Postgres/*.csproj
```

Expected: `no prerelease packages`. The EF Core / Npgsql references sit
next to a comment that states the exit condition (research Decision 2).

## 5. Every test layer passes (FR-008, SC-002)

```bash
dotnet test tests/OrderSystem.Domain.Tests               # baseline: 21 passed
dotnet test tests/OrderSystem.Application.Tests          # baseline: 36 passed
dotnet test tests/OrderSystem.Adapters.IntegrationTests  # count ≥ main's
dotnet test tests/OrderSystem.EndToEndTests              # count ≥ main's
```

Expected: 0 failed, 0 skipped. Each count at least matches the same run on
`main`.

## 6. EF model hasn't drifted (FR-011)

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes \
  --project src/Adapters/OrderSystem.Adapters.Postgres \
  --startup-project hosts/OrderSystem.MigrationService
```

Expected: `No changes have been made to the model since the last migration.`

## 7. Golden path (FR-011, US1 scenario 3, SC-005)

```bash
dotnet run --project hosts/OrderSystem.AppHost
```

In the frontend, place an order. Expected: it reaches its final status, the
live status page updates without a refresh (SignalR 11 + Redis backplane),
and the notification arrives, the same as on .NET 10.

## 8. CI and docs (FR-009, FR-010)

```bash
grep -nE 'dotnet-version|global-json-file|net10\.0' .github/workflows/ci.yml
grep -nE '\.NET 1[01]' README.md
```

Expected: CI uses only `global-json-file: global.json` (no `dotnet-version`,
no `net10.0`), and the README says .NET 11. On the PR, every CI job is
green.
