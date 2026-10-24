# Feature Specification: Continuous Integration

**Feature Directory**: `specs/002-add-continuous-integration-that/`

**Created**: 2026-10-24

**Status**: Draft

**Input**: User description: "Create CI and improve source organization" — split into two features per the one-feature-per-spec rule; this spec covers CI only. "Improve source organization" needs its own `/speckit-specify` run once its scope is defined.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every push is automatically built and tested (Priority: P1)

As the maintainer, whenever I push code to any branch, I want it
automatically built and run through every existing automated test tier, so
I find out immediately if something's broken instead of discovering it
later or having to remember to run the suite myself.

**Why this priority**: This is the entire point of CI — without it, nothing
else in this feature has anything to report on. It's also independently
useful on its own, before any pull-request-specific behavior exists.

**Independent Test**: Push a commit that breaks a test in any tier (e.g., a
failing assertion in `OrderSystem.Domain.Tests`) and verify a run starts
automatically and reports failure, with no manual step taken to trigger it.

**Acceptance Scenarios**:

1. **Given** a pushed commit where every test passes, **When** the run
   completes, **Then** it reports success.
2. **Given** a pushed commit that breaks a test in any tier, **When** the
   run completes, **Then** it reports failure, and the failure is visible
   without needing to reproduce it locally first.

---

### User Story 2 - Pull requests show pass/fail status directly (Priority: P2)

As the maintainer, when I open a pull request, I want the same checks to
run and their result to show directly on the pull request, so I can tell
whether a change is safe to merge without leaving the pull request page.

**Why this priority**: Extends User Story 1 to the point where the
decision actually gets made — reviewing a change before merging it. Not
P1 because the underlying build-and-test behavior (US1) has to exist first;
this is about where that result is surfaced.

**Independent Test**: Open a pull request against a branch with a known
test failure and verify the failing status is visible on the pull request
itself, not only in a separate log a reviewer has to go find.

**Acceptance Scenarios**:

1. **Given** an open pull request, **When** its latest commit's checks
   finish, **Then** the pull request displays that result (success or
   failure) directly.
2. **Given** a pull request whose checks are still running, **When** someone
   views the pull request, **Then** it shows the in-progress state rather
   than an absent or stale one.

---

### User Story 3 - A failure points at which tier broke (Priority: P3)

As the maintainer, when a run fails, I want to be able to tell which test
tier (unit, adapter-integration, or end-to-end) or which build step failed
directly from the run's summary, so I don't have to read through one
undifferentiated log to find out.

**Why this priority**: A quality-of-life improvement on top of US1/US2 —
the run already reports pass/fail without this; this makes a failure
actually actionable at a glance, which matters once there's more than one
test tier to sift through (there are currently four).

**Independent Test**: Break a test in exactly one tier (e.g., only
`OrderSystem.Adapters.IntegrationTests`) and verify the run's summary
identifies that tier specifically, distinct from the others, which still
report success.

**Acceptance Scenarios**:

1. **Given** a run where exactly one test tier fails, **When** viewing the
   run's summary, **Then** the failing tier is distinguishable from the
   passing ones without opening every step's full log.

---

### Edge Cases

- What happens if a push touches only the frontend, or only the .NET side?
  The run executes the full suite regardless of which files changed — see
  Assumptions.
- What happens if a test tier that depends on external services (database,
  message broker, browser) can't provision what it needs on the run
  environment? The run reports failure for that tier rather than silently
  skipping it — a tier that can't run is not the same as a tier that passed.
- What happens to a run already in progress when its branch receives a new
  push? Out of scope to specify precisely here (e.g., whether the stale run
  is superseded or left to finish) — left to whichever platform implements
  this, as it's a standard behavior most CI platforms already provide.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST automatically start a build-and-test run whenever
  new code is pushed to any branch or proposed via a pull request, with no
  manual step required to trigger it.
- **FR-002**: The run MUST build the entire repository — every .NET project
  and the frontend — and report failure if the build itself fails, before
  attempting to run any tests.
- **FR-003**: The run MUST execute every automated test tier that exists in
  the repository (not a subset), consistent with this project's standing
  practice of only trusting behavior that's actually been verified.
- **FR-004**: The run's result MUST be visible directly on the pushed
  commit, and, when the push is part of a pull request, directly on the
  pull request itself — without requiring anyone to open the run's logs to
  learn the outcome.
- **FR-005**: When a run fails, its summary MUST make it possible to
  identify which specific test tier or build step failed, without
  re-running anything locally first.
- **FR-006**: The run MUST provision everything the test suite needs
  (database, message broker, browser, etc.) automatically as part of the
  run itself — no manual setup step, and no dependency on something being
  pre-installed on a persistent machine.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of pushes and pull requests trigger a run automatically
  — zero requiring a manual step to start.
- **SC-002**: 100% of the repository's existing automated test tiers
  (currently: `OrderSystem.Domain.Tests`, `OrderSystem.Application.Tests`,
  `OrderSystem.Adapters.IntegrationTests`, `OrderSystem.EndToEndTests`, and
  the frontend's own checks) execute as part of every run — none silently
  skipped.
- **SC-003**: A contributor can learn whether a change is safe to merge
  within 15 minutes of pushing, without leaving the pull request page.
- **SC-004**: When exactly one test tier fails, a contributor can identify
  which one directly from the run's summary view, without opening a full
  log.

## Assumptions

- This feature covers the automated run reporting pass/fail status; it
  does not include enabling branch-protection rules that block merging on
  a failing status. That's a one-click repository setting rather than
  pipeline-authoring work, and can be turned on separately once this run
  exists to gate on.
- Every run executes the full test suite regardless of which files
  changed — no path-based filtering to skip, say, .NET tests on a
  frontend-only change. Nothing in this project's history suggests the
  pipeline is slow enough yet to justify that complexity (Constitution
  Principle IV).
- The run reports status through the CI platform's own native UI (commit
  and pull-request status checks). No additional notification channel
  (email, chat) is in scope.
- Whatever provisions Docker/Node/browser dependencies for the test suite
  on the CI platform is expected to do so cleanly on a standard, disposable
  run environment. The host-specific library mismatch this project's local
  development environment needed a one-off workaround for (documented in
  this repository's commit history) was specific to that one machine's
  software mix, not expected to recur on a clean CI runner.
- This feature targets the repository's existing single `main` branch and
  its pull requests; multi-branch or multi-environment pipeline strategies
  (staging, release channels) are out of scope since none exist yet.
