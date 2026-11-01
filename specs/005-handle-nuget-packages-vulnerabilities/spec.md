# Feature Specification: Handle NuGet Package Vulnerabilities

**Feature Branch**: `005-handle-nuget-packages-vulnerabilities`

**Created**: 2026-11-01

**Status**: Implemented (merged in PR #4, 2026-11-01)

**Input**: User description: "Handle nuget packages vulnerabilities"

## Baseline (audit run 2026-11-01)

A solution-wide NuGet vulnerability audit, including transitive dependencies,
currently reports the following. All of them are **transitive**. No project
references a vulnerable package directly, so none of them was a deliberate
choice, and nothing stopped them from arriving.

| Package | Resolved | Severity | Advisory | First patched | Affected projects | Pulled in via |
|---|---|---|---|---|---|---|
| MessagePack | 2.5.302 | Moderate | GHSA-qhrr-8q5h-9q3h (nested arrays can exceed buffer length limits) | 2.5.303 | OrderSystem.NotificationService, OrderSystem.AppHost, OrderSystem.EndToEndTests | NotificationService: Microsoft.AspNetCore.SignalR.StackExchangeRedis. AppHost / EndToEndTests: Aspire.Hosting.* → StreamJsonRpc |
| SSH.NET | 2025.1.0 | High | GHSA-mggc-4xg6-vcxf (ScpClient server-side RCE via default SCP path handling) | 2026.0.0 | OrderSystem.Adapters.IntegrationTests | Testcontainers (Kafka, PostgreSql, RabbitMq) 4.13.0 |
| SSH.NET | 2025.1.0 | High | GHSA-q939-rpr3-3284 (ScpClient recursive download allows arbitrary file write) | 2026.0.0 | OrderSystem.Adapters.IntegrationTests | Testcontainers (Kafka, PostgreSql, RabbitMq) 4.13.0 |

Each finding has a published patched version, so the remediation stories below
assume upgrades are possible.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Remove the currently known vulnerabilities (Priority: P1)

As a maintainer, I want every NuGet package the solution resolves to be free of
known vulnerabilities. Then the deployed services (NotificationService in
particular, which ships MessagePack in production) and the CI runners that
execute the integration tests aren't exposed to the advisories listed in the
Baseline.

**Why this priority**: This is the actual exposure that exists today.
NotificationService is a running service, and SSH.NET's high-severity
advisories affect code that runs on CI infrastructure. Nothing else in this
feature matters if the known findings stay unresolved.

**Independent Test**: Run a solution-wide vulnerability audit, including
transitive dependencies. It reports zero findings, and the full existing test
suite still passes.

**Acceptance Scenarios**:

1. **Given** the Baseline findings, **When** remediation is complete and a
   solution-wide audit (including transitive packages) is run, **Then** it
   reports no known vulnerabilities for any project.
2. **Given** remediation has changed resolved package versions, **When** all
   existing CI jobs run (Domain + Application unit tests, adapter integration
   tests, end-to-end tests, frontend checks), **Then** they all pass with no
   new failures.
3. **Given** remediation is complete, **When** the system runs locally through
   the Aspire AppHost, **Then** the golden path still works: placing an order,
   seeing its live status update, and receiving notifications.

---

### User Story 2 - Prevent vulnerable packages from silently returning (Priority: P2)

As a maintainer, I want the build to fail when a known-vulnerable NuGet package
enters the dependency graph, whether directly or transitively. Then a future
dependency bump, or a new advisory against a package we already use, can't
slip in unnoticed the way the Baseline findings did.

**Why this priority**: The Baseline findings show a real recurrence risk. All
three arrived transitively, and audit warnings existed but blocked nothing.
Without a gate, User Story 1 is a one-time cleanup that will decay. It's P2
because it protects the P1 outcome but doesn't by itself remove any current
exposure.

**Independent Test**: On a throwaway branch, deliberately reference a package
version with a known advisory. CI fails with a message that names the package
and advisory. Remove it, and CI passes again.

**Acceptance Scenarios**:

1. **Given** a clean, vulnerability-free solution, **When** a change introduces
   a package (directly or transitively) with a known advisory, **Then** the CI
   build fails and the failure output identifies the package, its version, and
   the advisory.
2. **Given** a clean, vulnerability-free solution, **When** a change introduces
   no vulnerable packages, **Then** the gate passes and adds no noticeable
   friction to the normal workflow.
3. **Given** a new advisory is published against a package already in use,
   **When** CI next runs against an unchanged commit, **Then** the gate
   surfaces it as a failure rather than letting it pass silently.
4. **Given** a developer builds locally, **When** the solution contains a
   vulnerable package, **Then** the same condition is visible locally as a
   failure, not only in CI.

---

### User Story 3 - Get notified of vulnerabilities and block critical ones on pull requests (Priority: P3)

As a maintainer, I want GitHub to alert me when a dependency of the
repository has a known vulnerability, and to open a fix PR when one exists.
This covers the .NET packages, the frontend's npm packages and the CI
workflow's actions. I also want a pull request that introduces a **critical**
vulnerability, in any of those ecosystems, to fail a visible check before
merge.

**Why this priority**: US2's build gate only sees NuGet, and only acts when
someone pushes. Notifications reach the maintainer even when nobody is
pushing. They also cover the npm frontend and the GitHub Actions used by CI,
which nothing in US1/US2 watches. It's P3 because it adds reach and
convenience on top of the gate rather than removing current exposure.

**Independent Test**: Confirm that vulnerability alerts and automated
security fixes are enabled on the repository. Open a PR that adds a
dependency with a known critical advisory: the dependency-review check fails
and names the advisory. A PR with no new vulnerable dependencies passes it.

**Acceptance Scenarios**:

1. **Given** a known advisory affects a NuGet, npm or GitHub Actions
   dependency on `main`, **When** GitHub's advisory data is updated, **Then**
   a vulnerability alert appears for the repository and the maintainer is
   notified.
2. **Given** an alert has a patched version available, **When** Dependabot
   processes it, **Then** it opens a pull request that upgrades the
   dependency, and that PR runs the full CI pipeline.
3. **Given** a pull request adds or upgrades a dependency to a version with a
   **critical** advisory, **When** CI runs on the PR, **Then** the
   dependency-review check fails and identifies the package and advisory.
4. **Given** a pull request introduces only low/moderate/high advisories in
   npm or Actions dependencies, **When** CI runs, **Then** the
   dependency-review check passes, but the advisories are still shown in its
   summary. NuGet findings at any severity are still blocked by US2's gate.
5. **Given** Dependabot is configured, **When** no advisory applies, **Then**
   it opens no routine version-bump PRs. This story is about security
   notifications, not general dependency churn.

---

### Edge Cases

- **A patched version forces a breaking upgrade of a parent package.** For
  example, SSH.NET 2026.0.0 may need a newer Testcontainers, or MessagePack
  2.5.303 may conflict with what Aspire/StreamJsonRpc or the SignalR Redis
  backplane pins. The plan must choose between upgrading the parent package and
  overriding the transitive version directly, and record why. Either way,
  acceptance scenario 1.2 (all CI jobs pass) still applies.
- **No patched version exists for a future finding.** The gate must allow a
  specific, named advisory to be explicitly accepted. Each acceptance records a
  written justification (why it isn't exploitable here, or why there's no
  alternative) where reviewers can see it. Accepting one advisory must not
  silence other advisories or other packages. No such case exists today, so
  this is a narrow escape hatch, not a workflow.
- **The vulnerability source is unreachable** during a build (for example, a
  network outage reaching the advisory feed). The gate shouldn't silently pass
  as "no vulnerabilities". The failure mode should be visible, even if the
  plan decides it shouldn't block the build.
- **Test-only and dev-time projects** (Adapters.IntegrationTests,
  EndToEndTests, AppHost) are covered by the same rule as shipping projects.
  They run on CI infrastructure and developer machines, so "it's only a test
  dependency" isn't grounds for exclusion.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every project in the solution MUST resolve only NuGet packages
  with no known vulnerabilities, of any severity, including transitive
  packages.
- **FR-002**: The specific Baseline findings (MessagePack < 2.5.303; SSH.NET <
  2026.0.0) MUST no longer be resolved by any project.
- **FR-003**: Remediation MUST NOT change application behavior. All existing
  automated tests at every layer (Domain, Application, Adapter integration,
  End-to-end, frontend checks) MUST continue to pass.
- **FR-004**: The CI pipeline MUST fail when any project resolves a NuGet
  package with a known vulnerability, direct or transitive.
- **FR-005**: The failure output MUST identify the affected project, package,
  resolved version, and advisory, so a maintainer can act without re-running
  diagnostics.
- **FR-006**: The same vulnerability check MUST apply to local builds, so
  developers see the problem before pushing.
- **FR-007**: It MUST be possible to accept a specific advisory explicitly, with
  a recorded justification that's visible in the repository. An acceptance
  MUST apply only to the named advisory and MUST NOT suppress any other
  findings.
- **FR-008**: The remediation approach for each Baseline finding (upgrade the
  parent package vs. override the transitive version) MUST be documented with
  its rationale, so a future maintainer knows why each override exists and
  when it can be removed.
- **FR-009**: The repository MUST have vulnerability alerts enabled, so the
  maintainer is notified of advisories affecting NuGet, npm
  (`order-system-frontend`) and GitHub Actions dependencies.
- **FR-010**: Dependabot MUST open security-fix pull requests for alerts that
  have a patched version, in all three ecosystems. It MUST NOT open routine
  (non-security) version-update PRs.
- **FR-011**: Every pull request to `main` MUST run a dependency-review check
  that fails when the PR introduces a dependency with a **critical** advisory,
  in any ecosystem. The failure output MUST name the package and advisory.
- **FR-012**: The dependency-review check MUST NOT weaken US2's gate. NuGet
  advisories of any severity still fail restore, independently of FR-011's
  critical threshold.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A solution-wide vulnerability audit, including transitive
  dependencies, reports **0** known vulnerabilities, down from the Baseline's
  3 advisories across 2 packages in 4 projects.
- **SC-002**: **100%** of existing CI jobs pass after remediation, with no
  tests skipped or removed to get there.
- **SC-003**: Deliberately introducing a package with a known advisory causes
  CI to fail on **100%** of attempts, and the failure message names the
  offending package and advisory.
- **SC-004**: A clean change (no vulnerable packages) passes the gate with no
  added manual steps for the developer.
- **SC-005**: A pull request that introduces a critical-severity dependency
  fails the dependency-review check on **100%** of attempts. Vulnerability
  alerts and Dependabot security updates show as enabled in the repository's
  security settings, for all three ecosystems.

## Assumptions

- **Scope.** Remediation (US1) and the build-failing gate (US2) cover NuGet
  only. Notifications and the critical-only PR check (US3) also cover npm
  (`order-system-frontend`) and GitHub Actions, as the maintainer asked
  (2026-11-01). Fixing existing npm advisories, or failing on npm advisories
  below critical, is out of scope. That would need its own spec.
- **Two thresholds, on purpose.** NuGet fails at *any* severity (US2), while
  the cross-ecosystem PR check fails only at *critical* (US3). The PR check
  adds coverage for npm and Actions without making frontend work fail on
  moderate advisories, and the stricter NuGet rule stays in force.
- **Repository security settings** (vulnerability alerts, Dependabot
  security updates, dependency graph) are changed by a repository admin. The
  repository is public, so the dependency graph and dependency review are
  available at no cost.
- **Severity threshold is "any".** Every severity the audit reports (low,
  moderate, high, critical) fails the gate. This is the simplest rule, and the
  current solution can meet it. A higher threshold can be adopted later if
  noise becomes a real problem.
- **The source of truth for "known vulnerability"** is the advisory data the
  standard NuGet tooling already consults (the GitHub Advisory Database via
  nuget.org). No additional scanning service is introduced.
- **No broader dependency-management restructuring.** Adopting centralized
  package version management, routine (non-security) dependency-update PRs,
  or scheduled scans is not required by this feature (Constitution Principle IV). The plan
  may adopt a mechanism only if it's the simplest way to satisfy FR-001–FR-008.
- **The current CI workflow is the place to enforce the gate.** It runs on push
  and on pull requests to `main`. Scenario 2.3 (new advisories against
  unchanged code) is satisfied on the next CI run, and no separate scheduled
  job is required.
- **Patched versions are compatible enough.** Remediation is expected to be
  version bumps or transitive overrides with no application code changes. If
  an upgrade turns out to require code changes, they're in scope only insofar
  as they keep FR-003 satisfied.
