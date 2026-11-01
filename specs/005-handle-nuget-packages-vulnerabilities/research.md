# Research: Handle NuGet Package Vulnerabilities

No `NEEDS CLARIFICATION` markers were left in Technical Context. Each entry
below is still a real decision with a rejected alternative, so it's recorded.
Version data was checked against nuget.org on 2026-11-01.

## Decision 1: Upgrade parent packages instead of overriding transitive versions

**Decision**: Fix every Baseline finding by bumping the **direct** dependency
that pulls in the vulnerable package. Don't add direct `PackageReference`
overrides for MessagePack or SSH.NET.

| Finding | Direct dependency (current → target) | Transitive result |
|---|---|---|
| SSH.NET 2025.1.0 (2 × High) | `Testcontainers.Kafka` / `.PostgreSql` / `.RabbitMq` 4.13.0 → **4.14.0** | SSH.NET **2026.0.0** (patched) |
| MessagePack 2.5.302 in NotificationService | `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.10 → **10.0.14** | MessagePack **2.5.303** (patched) |
| MessagePack 2.5.302 in AppHost / EndToEndTests | `Aspire.AppHost.Sdk` + `Aspire.Hosting.*` + `Aspire.Hosting.Testing` 13.4.6 → **13.6.4** | MessagePack **2.5.307** (patched). From 13.6.2 on, Aspire.Hosting references MessagePack directly, which lifts it above StreamJsonRpc 2.25.29's floor of 2.5.302 |

**Rationale**: Every finding has an upstream release that already depends on
the patched version. Upgrading the parent keeps the version that the
publisher tested together. A transitive override, by contrast, is a pin that
someone has to remember to remove later. FR-008 asks for documented rationale
for each override; with no overrides, that's satisfied by this table.

- **Testcontainers 4.14.0** is the *smallest* bump that fixes SSH.NET. 4.15/4.16
  exist, but taking them adds unrelated change surface.
- **SignalR.StackExchangeRedis 10.0.14** is a patch-level bump within the same
  .NET 10 servicing line.
- **Aspire 13.6.4**: 13.6.2 is the minimum fixing version. 13.6.4 is the
  latest patch in that same minor line, and patch releases only carry fixes.
  The `Aspire.AppHost.Sdk` version in `OrderSystem.AppHost.csproj` must move
  in lockstep with the `Aspire.Hosting.*` packages.

**Alternatives considered**:
- *Pin `MessagePack` 2.5.303+ and `SSH.NET` 2026.0.0 as direct references in
  the affected projects*: rejected. It works, but it adds references that no
  project's code uses. It has to be removed by hand once parents catch up, and
  it hides the real upgrade from future readers. Keep it as the fallback only
  if a parent upgrade breaks a test tier (see Decision 5).
- *Jump everything to latest (Testcontainers 4.16, Aspire 13.6.4,
  all Microsoft.Extensions.* to 10.0.14)*: rejected. It isn't required by any
  FR, and it widens the blast radius of a security fix. Unrelated
  `Microsoft.Extensions.*` 10.0.10 references stay where they are, because the
  audit doesn't flag them.

## Decision 2: Use NuGet's built-in audit as the gate, with audit warnings promoted to errors

**Decision**: Add one repo-root `Directory.Build.props` that sets:

- `NuGetAudit` = `true`
- `NuGetAuditMode` = `all` (include transitive packages)
- `NuGetAuditLevel` = `low` (report every severity)
- `WarningsAsErrors` += `NU1901;NU1902;NU1903;NU1904` (low / moderate / high /
  critical vulnerability warnings)

**Rationale**: The .NET 10 SDK already performs this audit during every
`restore` against the GitHub Advisory Database via nuget.org. That's exactly
how the Baseline was found. Today the findings are warnings that block
nothing. Promoting them to errors meets these requirements:

- FR-004 / FR-006: restore fails in CI and locally, with the same rule and the
  same file.
- FR-005: the NU190x message already names the project, package, version and
  advisory URL.
- Spec Assumption: the source of truth is the standard tooling.

It also follows Constitution Principle VI: this is the toolchain's own feature,
and no new service is involved. Setting `NuGetAuditMode`/`NuGetAuditLevel`
explicitly, rather than relying on SDK defaults, makes the "any severity,
including transitive" policy visible in the repo, and keeps it in place if a
future SDK changes the defaults.

**Alternatives considered**:
- *Set these properties in each of the 13 `.csproj` files*: rejected. It
  duplicates the setting 13 times, and a new project could forget it.
  `Directory.Build.props` is MSBuild's standard mechanism for repo-wide
  settings and applies automatically to future projects.
- *`dotnet list package --vulnerable` in a CI step, parsed for output*:
  rejected. It exits 0 even when it finds vulnerabilities, so it needs fragile
  text parsing. It also only runs in CI, which fails FR-006.
- *Dependabot / GitHub code scanning / Snyk / OWASP dependency-check*:
  rejected for this feature. These are a second advisory pipeline or an
  update bot, which is out of scope per the spec's Assumptions and
  Constitution Principle IV. Dependabot alerts may exist on the repo already.
  They're complementary, but they don't block a build.
- *Global `TreatWarningsAsErrors=true`*: rejected. It would promote every
  compiler warning in the solution too, which is unrelated scope.

## Decision 3: Accept a specific advisory with `NuGetAuditSuppress`, plus an adjacent justification comment

**Decision**: FR-007's escape hatch is NuGet's built-in
`<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-…" />` item,
placed in `Directory.Build.props` with a mandatory XML comment directly above
it. The comment states the justification, the date, and the condition for
removing it. **No suppressions are added by this feature**, because every
Baseline finding has a fix. The plan only documents the mechanism, in
`Directory.Build.props` comments and in `quickstart.md`.

**Rationale**: The suppression is keyed by advisory URL, so it silences only
that advisory. Other advisories against the same package, and all other
packages, are still reported. That's FR-007's scoping requirement. It sits in
the same reviewed file as the gate itself, which satisfies "visible in the
repository".

**Alternatives considered**:
- *`NoWarn` NU1902 etc. per project or per `PackageReference`*: rejected. It
  suppresses by severity code, not by advisory, so it would hide unrelated
  future advisories. That breaks FR-007.
- *A separate allow-list file plus custom tooling*: rejected under Principle
  IV. The SDK already has the feature.

## Decision 4: A dedicated CI job that restores the whole solution

**Decision**: Add a fifth job, `dependency-audit`, to
`.github/workflows/ci.yml`. It runs `dotnet restore OrderSystem.slnx`.

**Rationale**: The gate in `Directory.Build.props` already fires in every
existing job when that job restores its projects. But each job restores only
part of the graph. The unit-test job never restores AppHost, for example. A
solution-wide restore in one named job guarantees that every project is
audited on every push and PR, which covers Scenario 2.3 (new advisories
against unchanged code). It also gives the GitHub run summary a job whose name
says "this failed because of a vulnerable package". That matches feature 002's
research Decision 2: one job per concern, so a failure is identifiable from
the summary alone. Restore takes seconds, so the job adds negligible time.

**Alternatives considered**:
- *Rely only on the existing jobs*: rejected. Coverage would depend on
  which projects happen to be referenced by which test project. An audit
  failure would also show up as, say, "Unit tests failed", which misleads.
- *A scheduled (cron) audit job*: rejected per the spec's Assumptions. The
  next push or PR run is enough for now.

## Decision 5: NU1900 (advisory source unreachable) stays a warning

**Decision**: Don't promote `NU1900` to an error.

**Rationale**: The spec's edge case requires an unreachable advisory feed to
be *visible*, not silently treated as clean. It leaves blocking up to the plan.
NuGet already emits NU1900 as a warning in the restore output. Making it an
error would make every build fail during a nuget.org vulnerability-feed
outage, even though restore itself (which needs nuget.org anyway) succeeded.
The cost is that one CI run during an outage could pass without an audit. The
next run audits again, so the gap is temporary and visible.

**Alternatives considered**:
- *Promote NU1900 to an error*: rejected for now. It couples build
  availability to a secondary feed. Easy to revisit if outages turn out to
  hide real findings.

## Decision 6: Fallback if a parent upgrade breaks a test tier

**Decision**: If the Aspire 13.4.6 → 13.6.4 bump breaks the AppHost or
end-to-end tier, and the break isn't a trivially fixable API change, fall back
for that finding only. Stay on Aspire 13.4.6 and add a direct
`MessagePack` 2.5.303+ reference to `OrderSystem.AppHost` and
`OrderSystem.EndToEndTests`, with a comment that links GHSA-qhrr-8q5h-9q3h and
states the removal condition ("remove once Aspire ≥ 13.6.2"). Apply the same
pattern to SSH.NET if Testcontainers 4.14.0 breaks the integration tier.

**Rationale**: FR-003 (no behavior change, all tiers green) outranks the
preference in Decision 1. The Aspire jump crosses minor versions and has the
most risk. The other two are patch or minor-patch bumps.

**Alternatives considered**:
- *Suppress the advisory instead*: rejected. A patched version exists, so
  accepting the risk isn't justified (FR-007 is for the no-fix case).

## Decision 7: Dependabot security updates + `dependency-review-action` for US3

*Added 2026-11-01 when the maintainer asked for Dependabot notifications and a
critical-severity CI failure covering NuGet, npm and GitHub Actions.*

**Decision**:

1. **Repository settings**: enable *vulnerability alerts* (Dependabot alerts)
   and *automated security fixes* (Dependabot security updates). This is a
   repo-admin action: `gh api -X PUT repos/amiquelino/order-system/vulnerability-alerts`
   then `gh api -X PUT repos/amiquelino/order-system/automated-security-fixes`.
   Today `GET …/vulnerability-alerts` returns 404, which means alerts are off.
   The dependency graph is on by default for public repos.
2. **`.github/dependabot.yml`** (version 2) with one `updates` entry per
   ecosystem. All three use a `weekly` schedule and
   **`open-pull-requests-limit: 0`**:
   - `nuget`, with `directories: ["/src/**", "/hosts/**", "/tests/**"]`
   - `npm`, with `directory: "/order-system-frontend"`
   - `github-actions`, with `directory: "/"`

   A limit of 0 turns off routine version-update PRs. Security-update PRs
   aren't subject to that limit, so FR-010 is met. The file still tells
   Dependabot where each manifest lives.
3. **New CI job `dependency-review`** in `.github/workflows/ci.yml`. It runs
   only on `pull_request` (`if: github.event_name == 'pull_request'`) and
   uses `actions/checkout@v4` then `actions/dependency-review-action@v4` with
   `fail-on-severity: critical`.

**Rationale**: These are GitHub's native mechanisms for exactly this request.
The repository already lives there (feature 002, research Decision 1), so
nothing new needs hosting (Principle VI). `dependency-review-action` compares
the PR's dependency graph against `main` and fails on newly introduced
advisories at or above the threshold. That's FR-011 across all three
ecosystems.

**Known limitation (why US2's gate stays)**: GitHub's dependency graph reads
NuGet `.csproj` files statically, so it sees **direct** `PackageReference`s
only, not resolved transitive packages. All three Baseline findings were
transitive, so dependency-review would not have caught them; the restore-time
NuGet audit (Decision 2) did. FR-012 keeps the two layers independent:

- dependency-review: wide (three ecosystems), shallow for NuGet, critical only
- NuGet audit: NuGet only, deep (transitive), every severity

Dependabot *alerts* may likewise under-report transitive NuGet findings, for
the same reason.

**Alternatives considered**:
- *Lower the NuGet gate to critical only, to match*: rejected by the
  maintainer on 2026-11-01. Today's moderate and high findings would no
  longer block anything.
- *Allow routine Dependabot version-update PRs*: rejected. FR-010 and US3
  scenario 5 say security only. Weekly bumps across three ecosystems add churn
  that no current requirement asks for (Principle IV).
- *Dependabot alone, with no CI check*: rejected. Dependabot notifies and opens
  PRs, but it can't fail a PR's CI. That needs dependency-review.
- *A third-party scanner (Snyk, OWASP dependency-check)*: rejected, same
  reasons as Decision 2.
