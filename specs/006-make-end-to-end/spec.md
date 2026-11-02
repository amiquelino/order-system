# Feature Specification: Reliable, Diagnosable End-to-End Tests in CI

**Feature Branch**: `e2e-diagnostics-and-ci-hygiene` (PR #10)

**Created**: 2026-11-02

**Status**: Implemented, merged in PR #10 (`a6abaa3`, 2026-11-02); post-merge `main` CI green. This spec was written after the
implementation, to record the decisions behind it. US1 scenario 4 was verified
on 2026-11-02 in run 41465842762, a deliberately failing throwaway commit with
unit price `-5`, which the form's own validation blocks:

- The failure message named the page (`'http://localhost:40585/'`) and its text.
- The browser console output appeared under "Standard Output Messages". No
  `POST` response was logged, which correctly shows the click never submitted.
- The `playwright-trace` artifact was uploaded: 1.4 MB, 7-day retention, a
  valid trace with 251 screenshots.

The throwaway branch was deleted afterwards. US3 was verified by PR #10's
passing CI, which had no deprecation or ASPIRE010 warnings and an 8 s npm
install.

**Input**: User description: "Make end-to-end test failures diagnosable, fix the create-order hydration flake, and modernize CI (Node 24 actions/runtime, npm ci, trace artifact)"

## Baseline (CI review, 2026-11-02)

- **Reliability**: the end-to-end job failed **1 of the 29** most recent CI runs.
  The failing run was a docs-only commit (`005-mark-complete`, push run); the
  PR run of the same commit passed. After "Create order" was clicked, the
  browser never reached the order status page within 45 s.
- **Diagnosability**: the failure log contained only a timeout and a stack
  trace. The browser output that the test already collected (console
  messages, page errors, failed requests) never appeared in any CI log,
  passing or failing: **0** lines across the runs checked. No screenshot,
  page snapshot or recording of the run was kept. The run's own record
  could not show whether the click did nothing or the API rejected the
  order.
- **CI maintenance signals**:
  - Every run warned that the CI's building-block actions target a
    deprecated runtime (Node.js 20).
  - The frontend itself ran on Node.js 20, which reached end of life in
    April 2026.
  - The frontend dependency install in the end-to-end job used a
    non-lockfile-exact install, and had grown from ~5 s to 30–68 s with
    identical inputs.
  - A known-harmless Aspire build warning (ASPIRE010) was repeated 3× per
    run.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A failed end-to-end run explains itself (Priority: P1)

As a maintainer looking at a failed CI run, I want the run itself to show what
the browser saw and did. That means its console output, its failed or error
network responses, what was on screen at the moment of failure, and a
replayable recording. Then I can tell *why* it failed without re-running it or
reproducing it locally.

**Why this priority**: The test exercises the whole distributed system, takes
minutes, and needs Docker, Node and a browser to reproduce. A flake that
leaves no evidence can't be fixed, only retried. Every later fix, including
US2, depends on being able to see what went wrong.

**Independent Test**: Push a commit that deliberately makes the end-to-end
test fail. The failed job's log and attached artifacts must be enough to
identify the cause without running anything locally.

**Acceptance Scenarios**:

1. **Given** the end-to-end test fails, **When** the maintainer opens the
   job log, **Then** the browser's console messages, page errors, failed
   requests, and any order-submission or error responses from the run are
   printed alongside the failure.
2. **Given** the test fails because the app never navigated to the order
   status page, **When** the maintainer reads the failure message, **Then**
   it states the page the browser was still on and the text shown on it,
   including any error message the app displayed.
3. **Given** the end-to-end job fails, **When** the run completes, **Then** a
   recording of the browser session (screenshots, page snapshots, network
   activity, console) is attached to the run as a downloadable artifact,
   and the docs say how to open it.
4. **Given** a deliberately broken commit (for example, one whose order is
   blocked by the form's own validation), **When** CI runs it, **Then**
   scenarios 1–3 are all observed on that run.
5. **Given** the end-to-end job passes, **When** the run completes, **Then**
   no recording artifact is uploaded, and the passing log isn't flooded.

---

### User Story 2 - The create-order step doesn't race the page loading (Priority: P2)

As a maintainer, I want the end-to-end test to interact with the create-order
form only once the form is actually ready to handle the submission. Then a
slow page load can't turn into a false failure.

**Why this priority**: This is the most likely cause of the Baseline failure.
In the frontend's development mode, the page's HTML is visible before its
interactive behavior is attached. A submit that lands in that window reloads
the page instead of creating the order, and the test then times out waiting
for navigation. It's P2 because without US1 there's no evidence to confirm
it. US2 removes the most likely cause; US1 makes any remaining cause visible.

**Independent Test**: The end-to-end test passes on CI with no increase in
its usual duration. Across subsequent runs, the "never navigated to the
status page after submitting" failure doesn't recur. If it does recur, US1's
diagnostics show a different cause.

**Acceptance Scenarios**:

1. **Given** the create-order page has been served but isn't yet interactive,
   **When** the test is ready to fill in the order, **Then** it waits until
   the form is interactive before typing or submitting.
2. **Given** the form becomes interactive within the normal load time,
   **When** the test runs, **Then** the added wait doesn't noticeably
   lengthen the test.
3. **Given** the form never becomes interactive within a generous bound,
   **When** the test runs, **Then** it fails with a message saying the page
   never became interactive, rather than a misleading navigation timeout.

---

### User Story 3 - CI runs on supported runtimes, quietly and deterministically (Priority: P3)

As a maintainer, I want CI to use supported runtimes and to install the
frontend's dependencies exactly as locked, with no recurring warnings that
don't call for action. Then real warnings stand out and runs aren't slowed or
changed by the install step.

**Why this priority**: None of these causes failures today, but each one
erodes the signal. Deprecation warnings on every run train people to ignore
warnings. An end-of-life runtime stops getting fixes. A non-exact install can
silently change what's tested and was the slowest step after the test itself.
It's P3 because it's maintenance, not a defect.

**Independent Test**: A CI run on the branch shows no runtime-deprecation
warnings and no ASPIRE010 warnings, the frontend runs on a supported Node.js
release, the dependency install finishes in seconds and doesn't modify the
lockfile, and all CI jobs pass.

**Acceptance Scenarios**:

1. **Given** any CI run, **When** it completes, **Then** it shows no warnings
   about actions running on a deprecated runtime.
2. **Given** the frontend is built, linted and run in CI, **When** the jobs
   run, **Then** they use a currently supported Node.js LTS release.
3. **Given** the end-to-end and frontend jobs install frontend dependencies,
   **When** they run, **Then** they install exactly the locked versions, fail
   if the lockfile and manifest disagree, and skip the redundant
   per-install advisory lookup. Advisories are already covered by feature
   005's Dependabot alerts and Dependency review.
4. **Given** the AppHost builds, **When** CI runs, **Then** the known-harmless
   ASPIRE010 warning no longer appears. The reason is recorded next to the
   suppression.
5. **Given** these changes, **When** all CI jobs run, **Then** they all pass,
   including Dependency audit and Dependency review from feature 005.

---

### Edge Cases

- **The test fails before a page opens** (for example, the distributed app
  never starts). The failure must still be reported normally. A missing
  recording must not cause a second, confusing failure, and the artifact
  step must tolerate there being nothing to upload.
- **Browser events arrive after the test has finished** (for example,
  console output during teardown). They may be dropped, but they must never
  crash the run or turn a pass into a failure.
- **Recordings hold run data.** These are synthetic orders with fake product
  names and generated customer IDs, so no real personal data exists. Keep
  retention short anyway.
- **An action's new major version changes behavior.** Every job must still
  pass on the PR before merge (US3 scenario 5).
- **The runner image changes under us.** GitHub's `ubuntu-latest` is moving
  to a new Ubuntu release. That's out of scope here (see Assumptions), but if
  it breaks the browser install, US1's diagnostics should make it obvious.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When the end-to-end test fails, the CI job log MUST include the
  browser's console messages, page errors, failed requests, the
  order-submission response, and any error (4xx/5xx) responses observed
  during the test.
- **FR-002**: When the test times out waiting for the order status page, the
  failure message MUST include the browser's current page address and the
  visible text of the page.
- **FR-003**: Every end-to-end run MUST produce a recording of the browser
  session, with screenshots, page snapshots, network activity and console.
- **FR-004**: CI MUST attach that recording to the run as a downloadable
  artifact **only when the end-to-end job fails**, kept for a limited period
  (7 days).
- **FR-005**: The project documentation MUST say where the recording is
  produced and how to open it.
- **FR-006**: The test MUST wait until the create-order form is interactive
  before entering data or submitting. The wait MUST be bounded, and its
  failure MUST be distinguishable from a navigation failure.
- **FR-007**: The waits added by FR-006 MUST NOT measurably lengthen a
  passing run. Duration stays within the Baseline's observed range.
- **FR-008**: Late browser events MUST NOT crash the test or change its
  outcome.
- **FR-009**: All CI building-block actions MUST run on a non-deprecated
  runtime, so no deprecation warnings appear.
- **FR-010**: The frontend MUST run on a currently supported Node.js LTS
  release in every CI job that uses it.
- **FR-011**: CI MUST install frontend dependencies exactly as locked,
  without modifying the lockfile and without the per-install advisory
  lookup.
- **FR-012**: The ASPIRE010 build warning MUST NOT appear in CI or local
  builds. The suppression MUST state why the warning doesn't apply.
- **FR-013**: The application's behavior MUST NOT change. Only test code,
  CI configuration, build-warning configuration and docs are in scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a deliberately failing run, a maintainer can identify the
  cause from the CI run alone: the log plus the attached recording, with
  **0** local reproductions needed.
- **SC-002**: The share of failed runs whose log contains browser output
  goes from **0%** (Baseline) to **100%**.
- **SC-003**: The "never reached the order status page" failure doesn't
  recur over at least the next **20** CI runs of the end-to-end job. If it
  does, the run's diagnostics show its cause.
- **SC-004**: Passing end-to-end runs keep the test step within the Baseline
  range of roughly **70–130 s**.
- **SC-005**: CI runs show **0** runtime-deprecation warnings and **0**
  ASPIRE010 warnings, down from 1 and 3 per run in the Baseline.
- **SC-006**: The frontend dependency install in the end-to-end job takes
  **≤ 15 s**, down from 30–68 s.
- **SC-007**: **100%** of CI jobs pass on the change before merge.

## Assumptions

- **One spec for one PR.** The diagnostics (US1), the flake fix (US2) and the
  CI maintenance (US3) were reviewed together and ship as one PR (#10), at
  the maintainer's request. They're separate user stories so each can be
  verified on its own.
- **The development-mode frontend stays.** Running the end-to-end test
  against a production build of the frontend would remove the class of
  timing problems behind US2. It's a bigger change to how the AppHost runs
  the frontend, so it's out of scope and a candidate for its own spec.
- **Ubuntu runner migration is out of scope.** No change for now. US1's
  diagnostics are the safety net if it breaks something.
- **Diagnostics stay in the CI run.** Recordings are kept only as CI
  artifacts, with no external reporting service (Constitution Principle IV).
- **Node.js 24** is the supported LTS release chosen for FR-010. Node 22 is
  in maintenance; Node 24 is the active LTS.
- **The stale "Turbopack" comment in the test fixture** (the frontend's dev
  server uses webpack) is a known nit and isn't part of this spec.
