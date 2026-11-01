# Quickstart: Validating NuGet Vulnerability Handling

Run everything from the repo root on the
`005-handle-nuget-packages-vulnerabilities` branch after implementation.

## Prerequisites

- .NET 10 SDK (`dotnet --version` → `10.0.x`)
- Network access to `api.nuget.org` (the audit queries its vulnerability feed)
- Docker running (for the integration tier)
- Node 20 + `npm install` in `order-system-frontend/` (for the end-to-end tier,
  see README "Prerequisites")

## 1. Solution-wide audit is clean (US1, SC-001)

```bash
dotnet restore OrderSystem.slnx --force
dotnet list OrderSystem.slnx package --vulnerable --include-transitive
```

**Expected**: `restore` succeeds with no `NU1901`–`NU1904` output, and every
project reports *"has no vulnerable packages given the current sources"*.

Confirm that the Baseline packages resolved to patched versions:

```bash
dotnet nuget why tests/OrderSystem.Adapters.IntegrationTests SSH.NET      # → 2026.0.0
dotnet nuget why hosts/OrderSystem.NotificationService MessagePack        # → ≥ 2.5.303
dotnet nuget why hosts/OrderSystem.AppHost MessagePack                    # → ≥ 2.5.303
dotnet nuget why tests/OrderSystem.EndToEndTests MessagePack              # → ≥ 2.5.303
```

## 2. Nothing broke (US1, FR-003, SC-002)

```bash
dotnet test tests/OrderSystem.Domain.Tests
dotnet test tests/OrderSystem.Application.Tests
dotnet test tests/OrderSystem.Adapters.IntegrationTests
dotnet build tests/OrderSystem.EndToEndTests && dotnet test tests/OrderSystem.EndToEndTests
```

**Expected**: All pass, with no tests skipped or removed. Then push and confirm
that all **five** CI jobs are green, including the new **Dependency audit**
job.

Manual golden path (US1 scenario 3): `dotnet run --project hosts/OrderSystem.AppHost`,
place an order in the frontend, and watch its status update live.

## 3. The gate fails on a vulnerable package (US2, SC-003)

On a throwaway branch, add a known-vulnerable reference to any project. For
example, put a historical vulnerable version in
`tests/OrderSystem.Domain.Tests/OrderSystem.Domain.Tests.csproj`:

```xml
<PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
```

```bash
dotnet restore tests/OrderSystem.Domain.Tests --force
```

**Expected**: restore **fails** with `error NU1903` (high severity). The
error names the project, `Newtonsoft.Json`, `12.0.1`, and the advisory URL
(FR-005). Push the branch: the **Dependency audit** CI job fails (FR-004).
Running the same command locally fails the same way (FR-006).

Revert the change and confirm that `dotnet restore OrderSystem.slnx --force`
succeeds again (SC-004).

## 4. Accepting a single advisory works and stays scoped (FR-007)

Still on the throwaway branch with the vulnerable reference in place, add this
to `Directory.Build.props` inside an `<ItemGroup>`, using the advisory URL
printed in step 3:

```xml
<!-- Accepted 2026-11-01: <justification>. Remove when <condition>. -->
<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-5crp-9r3c-p9vr" />
```

**Expected**: restore succeeds, and only that advisory is silenced. Next,
reference a *different* vulnerable package as well. Restore fails again for
the second package, which shows that the suppression doesn't widen. Discard
the throwaway branch afterwards.

## 5. Unreachable advisory feed is visible (spec edge case)

This is optional. With network access to nuget.org blocked and a warm local
package cache, `dotnet restore --force` emits a `NU1900` **warning**. The
restore doesn't silently report "no vulnerabilities" (research Decision 5).

## 6. Dependabot notifications and the critical PR check (US3, SC-005)

Repository settings are enabled:

```bash
gh api repos/amiquelino/order-system/vulnerability-alerts -i | head -1       # → HTTP/2.0 204
gh api repos/amiquelino/order-system/automated-security-fixes                  # → "enabled": true
```

**Expected**: Under the repository's **Security → Dependabot**, alerts are on,
and **Insights → Dependency graph → Dependabot** lists the `nuget`, `npm` and
`github-actions` configurations from `.github/dependabot.yml` with no config
errors. Dependabot opens no routine version-update PRs (US3 scenario 5).

**Critical fails.** On a throwaway branch, add a dependency with a known
**critical** advisory to `order-system-frontend/package.json`, for example a
historical `"minimist": "1.2.5"`, which has a critical prototype-pollution
advisory, then run `npm install` to update the lockfile. Open a PR to `main`.

**Expected**: the **Dependency review** check fails and names the package and
the critical advisory (FR-011). Close the PR without merging and delete the
branch.

**Below critical passes.** Do the same with an npm package that has only a
moderate or high advisory. **Expected**: Dependency review passes, and lists
the advisory in its summary (US3 scenario 4).

**The NuGet gate is unaffected.** Re-run §3. A *high* NuGet advisory still
fails `Dependency audit` even though `Dependency review` would pass it
(FR-012).
