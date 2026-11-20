# Feature Specification: Upgrade .NET to 11

**Feature Branch**: `007-upgrade-net-to-11`

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: "Upgrade .net to 11"

## Baseline (2026-11-18)

- All 13 .NET projects in the solution target .NET 10. CI installs the
  .NET 10 SDK. The README lists .NET 10 as a prerequisite.
- .NET 11.0.0 is generally available. It is a Standard Term Support (STS)
  release, and its end of support is 2028-11-14. .NET 10 is the current Long
  Term Support (LTS) release, and its end of support is also 2028-11-14. Moving
  to 11 does not shorten or lengthen how long the solution's runtime is
  supported. What changes is that the next upgrade (to .NET 12) moves earlier
  than it would on the LTS track, and the solution gets 11's runtime and library
  improvements now.
- The solution's dependencies are at different levels of .NET 11 readiness.
  Microsoft's framework-aligned packages (ASP.NET Core, EF Core,
  Microsoft.Extensions.*) have stable 11.0.0 releases. The PostgreSQL EF Core
  provider only has a release candidate of its 11 line so far. Wolverine,
  Aspire, Confluent.Kafka, RabbitMQ.Client, Testcontainers and the test
  tooling release on their own schedules, independent of .NET versions.
- `main` CI is currently red because of new advisories against
  Aspire 13.6.4 and Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.14,
  not because of anything this feature changes (see Assumptions).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The whole solution builds, tests, and runs on .NET 11 (Priority: P1)

As a maintainer, I want every .NET project in the solution (services,
adapters, hosts and test projects) to build and run on .NET 11. That way the
solution is on the current .NET release, and the version is the same
everywhere: on developer machines, in CI, and in the running system.

**Why this priority**: This is the feature. Any other story is only useful
once the solution actually runs on the new runtime.

**Independent Test**: Build the solution with only the .NET 11 SDK
installed and run every existing CI job. All of them pass. Start the system
through the Aspire AppHost and complete the golden path.

**Acceptance Scenarios**:

1. **Given** the upgraded solution, **When** every project's target runtime
   is inspected, **Then** every .NET project targets .NET 11 and none still
   targets .NET 10.
2. **Given** the upgraded solution, **When** all existing CI jobs run (Domain
   + Application unit tests, adapter integration tests, end-to-end tests,
   dependency audit, frontend checks), **Then** they all pass, with no test
   removed, skipped or weakened to get there.
3. **Given** the upgraded solution running locally through the Aspire
   AppHost, **When** a user places an order, **Then** the order reaches its
   final status, the live status page updates, and the notification arrives,
   as they did on .NET 10.
4. **Given** a developer machine or CI runner with only the .NET 10 SDK,
   **When** a build is attempted, **Then** it fails immediately with an
   error that names the required SDK version, instead of failing later with
   unrelated errors.

---

### User Story 2 - Dependencies move to their .NET 11 line where a stable release exists (Priority: P2)

As a maintainer, I want the packages that ship in step with .NET to move to
their .NET 11 versions, so that the framework and its companion libraries
match. Running .NET 11 with 10.x framework libraries would mean two framework
versions in the same process. I also want every other dependency checked for
.NET 11 compatibility instead of assumed compatible.

**Why this priority**: The runtime upgrade (US1) can technically succeed
with 10.x companion packages, but the result is a mixed stack that is harder
to reason about. It comes second because it depends on US1 and on which
dependencies are ready.

**Independent Test**: List every package the solution references. Each
framework-aligned package either is on a stable 11.x version or has a
recorded reason it is not, with the condition that will let it move. The
vulnerability audit reports zero findings.

**Acceptance Scenarios**:

1. **Given** a framework-aligned package with a stable 11.x release,
   **When** the upgrade is complete, **Then** the solution references that
   11.x release.
2. **Given** a dependency with no stable .NET 11-compatible release yet,
   **When** the upgrade is complete, **Then** the solution stays on that
   dependency's latest stable version, provided it works on .NET 11. The
   exception is recorded with the condition that ends it (for example,
   "move when the provider ships 11.0.0").
3. **Given** the upgraded package set, **When** the solution-wide NuGet
   vulnerability audit runs (including transitive packages), **Then** it
   reports zero findings, and no new advisory suppressions were added for
   this upgrade.
4. **Given** the upgraded package set, **When** the referenced versions are
   inspected, **Then** none of them is a prerelease (alpha, beta, preview or
   release candidate).

---

### User Story 3 - Contributors are told the new version (Priority: P3)

As a contributor setting up the repo or reading the project docs, I want
the stated prerequisites and CI configuration to say .NET 11, so that I
install the right SDK the first time.

**Why this priority**: It's needed for the upgrade to be complete, but it's
small and depends on US1.

**Independent Test**: Follow the README prerequisites on a clean machine.
The listed SDK builds and runs the solution.

**Acceptance Scenarios**:

1. **Given** the upgrade is merged, **When** a contributor reads the README
   prerequisites, **Then** they list .NET 11.
2. **Given** the upgrade is merged, **When** CI provisions its toolchain,
   **Then** it installs the .NET 11 SDK and nothing in CI still pins
   .NET 10.

---

### Edge Cases

- **A dependency has no stable .NET 11 release** (for example, the
  PostgreSQL EF Core provider, which is at a release candidate today): the
  solution keeps that dependency's stable 10.x line, as long as it runs on
  .NET 11. If keeping the provider at 10.x means EF Core itself also has to
  stay at 10.x, both stay there together. The exception is recorded (US2,
  scenario 2). The upgrade doesn't wait for every dependency to reach 11.x.
- **A dependency doesn't work on the .NET 11 runtime at all** (for example,
  runtime code generation that breaks on a new runtime): this blocks the
  upgrade. Moving to the dependency's next stable version that supports
  .NET 11 is in scope, even if it's a major version. Replacing the
  dependency with a different library is out of scope and goes back to
  the maintainer.
- **.NET 11 changes behavior** (serialization defaults, hosting, ASP.NET
  Core, EF Core query translation): the existing tests at each layer have to
  catch it. If a regression only shows up in the golden-path run or by hand,
  a test at the layer where it could come back is added (Constitution
  Principle III).
- **New compiler or analyzer warnings in .NET 11**: they are fixed, not
  suppressed across the whole solution. Package vulnerability warnings stay
  errors.
- **A new advisory is published during the upgrade**: it's handled the
  way spec 005 set up. Upgrade the parent package first; suppress only when
  no patched version exists.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every .NET project in the solution MUST target .NET 11.
- **FR-002**: The repository MUST state which .NET SDK it requires, so
  that a build with an older SDK fails right away with a clear message
  (US1, scenario 4).
- **FR-003**: Every framework-aligned package (ASP.NET Core,
  EF Core, Microsoft.Extensions.*) that has a stable 11.x release MUST be
  upgraded to it.
- **FR-004**: Every other dependency MUST be on a stable version that is
  verified to work on .NET 11. Moving to a newer major version of a
  dependency is allowed only when needed for .NET 11 compatibility.
- **FR-005**: The solution MUST NOT reference any prerelease package after
  the upgrade.
- **FR-006**: Every dependency left on its 10.x line MUST be recorded,
  with the reason and the condition for moving it.
- **FR-007**: The NuGet vulnerability gate (spec 005) MUST stay as
  strict as it is now, and the solution-wide audit MUST report zero
  findings.
- **FR-008**: Every existing test in every layer MUST pass. No test may be
  removed, skipped or loosened to make the upgrade pass.
- **FR-009**: CI MUST build and test on the .NET 11 SDK only.
- **FR-010**: Contributor-facing prerequisites (README) MUST state
  .NET 11.
- **FR-011**: The upgrade MUST NOT change the system's observable
  behavior: order lifecycle, status transitions, events, live status updates
  or notifications.
- **FR-012**: The upgrade MUST NOT add any new project dependency that
  breaks Constitution Principle I. For example, a package added for .NET 11
  compatibility must not end up referenced by Domain or Application.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the solution's .NET projects target .NET 11. 0
  target .NET 10.
- **SC-002**: Every CI job passes on the upgrade branch, and the test
  count per layer is the same as or higher than before the upgrade.
- **SC-003**: The solution-wide vulnerability audit reports 0 findings,
  with 0 advisory suppressions added by this feature.
- **SC-004**: 0 prerelease packages are referenced.
- **SC-005**: A contributor who follows only the README prerequisites
  can build the solution and run the golden path on the first try.
- **SC-006**: Every dependency that isn't on its .NET 11 line has a
  recorded reason and a condition for moving it: 100% coverage, no
  undocumented exceptions.

## Assumptions

- **The .NET 10 security fixes land first, separately.** The current red
  `main` (Aspire 13.6.4 and SignalR.StackExchangeRedis 10.0.14 advisories) is
  fixed by its own small change before this feature starts. This feature's
  baseline is a green `main`, so that upgrade regressions aren't mixed up
  with unrelated advisory failures.
- **STS is acceptable.** The maintainer asked for .NET 11 knowing it is an
  STS release. Because 11 and 10 both reach end of support on 2028-11-14,
  this feature doesn't change the support window and adds no
  long-term-support requirement.
- **Stable releases only.** No prerelease packages, even where a release
  candidate exists. When a dependency's 11.x is still prerelease, the
  default is to stay on its latest stable version (FR-004, FR-006), not to
  wait or to adopt the release candidate.
- **The frontend is out of scope.** `order-system-frontend` doesn't run on
  .NET, and nothing in it changes.
- **Using new .NET 11 language or library features is out of scope.**
  This feature changes the runtime and dependency versions only. Refactoring
  code to use new APIs or C# language features comes later, if there's a
  concrete reason (Constitution Principle IV).
- **Deployment targets follow the SDK.** The solution has no Dockerfiles of
  its own. Services run through Aspire, which uses the runtime the projects
  target, so no separate base-image work is expected. If a runtime image
  turns up in the plan, it's updated as part of this feature.
- **Aspire's version is independent of .NET's.** Aspire releases on its
  own schedule. It's upgraded only if the version in use doesn't support
  .NET 11 (FR-004).
