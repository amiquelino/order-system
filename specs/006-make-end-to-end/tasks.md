---

description: "Task list for Reliable, Diagnosable End-to-End Tests in CI"
---

# Tasks: Reliable, Diagnosable End-to-End Tests in CI

**Input**: Design documents from `specs/006-make-end-to-end/`

**Prerequisites**: plan.md, spec.md, research.md, quickstart.md. There's no
data-model.md or contracts/; see plan.md.

**Status**: Generated after the implementation in PR #10. Tasks that were
already done are marked `[X]` with the commit they landed in. The open tasks
are what's actually left.

**Tests**: No new test methods. This feature changes the end-to-end tier
itself (Constitution Principle III: the flake is only observable there). It's
validated by:

- a passing CI run (US2, US3)
- one deliberately failing throwaway commit (US1)

Each validation is its own task below.

**Organization**: Grouped by user story in spec priority order. US1 and US2
both edit `CreateOrderE2ETests.cs`, so their implementation tasks run in
sequence even though the stories are independent.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3. Omitted for Setup and Polish tasks.

---

## Phase 1: Setup

- [X] T001 Review CI history for the end-to-end job: durations, step timings, failures, annotations. Record the Baseline in spec.md: 1 failure in 29 runs, 0 browser-output lines, Node 20 deprecation, ASPIRE010 ×3, npm install 30–68 s. *(Review, 2026-11-02)*
- [X] T002 Create branch `e2e-diagnostics-and-ci-hygiene` from `main`.

---

## Phase 2: Foundational (Blocking Prerequisites)

None. The stories share a file (`CreateOrderE2ETests.cs`), but no shared
infrastructure.

---

## Phase 3: User Story 1 - A failed end-to-end run explains itself (Priority: P1) 🎯 MVP

**Goal**: A failed run's log and artifact are enough to diagnose it (FR-001–FR-005, FR-008).

**Independent Test**: quickstart.md §2. A deliberately failing commit produces the message, output and trace.

### Implementation for User Story 1

- [X] T003 [US1] In `tests/OrderSystem.EndToEndTests/CreateOrderE2ETests.cs`, inject `ITestOutputHelper`. Add a `Log()` helper that swallows `InvalidOperationException` from late events (research Decisions 1, 5). Route console, page-error and request-failed events through it, and log every `POST` response and every response with status ≥ 400. *(d8bcac6)*
- [X] T004 [US1] In the same file, create a browser context in `InitializeAsync` and start `Tracing` (screenshots, snapshots, sources). In `DisposeAsync`, stop it and save to `bin/Debug/net10.0/playwright-traces/CreateOrderE2ETests.zip`, then log the path (research Decision 2). *(d8bcac6)*
- [X] T005 [US1] In the same file, wrap `WaitForURLAsync("**/status/*")` so a timeout rethrows with `page.Url` and the `<main>` inner text (research Decision 4). *(d8bcac6)*
- [X] T006 [P] [US1] In `.github/workflows/ci.yml`, add an "Upload Playwright trace" step to the end-to-end job: `if: failure()`, `actions/upload-artifact@v7`, path `tests/OrderSystem.EndToEndTests/bin/Debug/net10.0/playwright-traces/`, `retention-days: 7`, `if-no-files-found: ignore`. *(2dda308)*
- [X] T007 [P] [US1] In `README.md`, add a paragraph to the end-to-end section on where the trace is written, the `playwright-trace` artifact, how to open it, and where browser output appears. *(d8bcac6)*

### Validation for User Story 1

- [X] T008 [US1] Run quickstart.md §2. On a throwaway branch, change the unit price to `-5` and push. *Verified in run 41465842762:*
  - The end-to-end job failed and all other jobs passed.
  - The message included `still on 'http://localhost:40585/'` and the page text.
  - The `[browser:…]` lines and the trace path appeared under "Standard Output Messages", with no `POST` logged.
  - The `playwright-trace` artifact was 1.4 MB, kept 7 days, and held a valid trace with 251 screenshots.

  The throwaway branch was deleted afterwards, locally and on the remote.

**Checkpoint**: Failures are diagnosable from the CI run alone (SC-001, SC-002).

---

## Phase 4: User Story 2 - The create-order step doesn't race the page loading (Priority: P2)

**Goal**: The test only interacts with the form once React has hydrated it (FR-006, FR-007).

**Independent Test**: quickstart.md §1 (passing run, normal duration) and §3 (no recurrence).

### Implementation for User Story 2

- [X] T009 [US2] In `tests/OrderSystem.EndToEndTests/CreateOrderE2ETests.cs`, after `GotoAsync` and before `FillAsync`, add `WaitForFunctionAsync` that waits until the `<form>` has an own property starting with `__reactProps`, bounded at 60 s (research Decision 3). Comment on why. *(d8bcac6)* Depends on T003–T005, which edit the same file.
- [X] T010 [US2] Wrap that wait so a timeout rethrows "The create-order form … never became interactive (React never hydrated it) within 60s", distinct from the navigation timeout (FR-006, US2 scenario 3). *(5b84f5b)*

### Validation for User Story 2

- [X] T011 [US2] Passing CI run on PR #10 (commit 2dda308): End-to-end tests passed, with the test step at 112 s, inside the 70–130 s Baseline range (FR-007, SC-004).
- [ ] T012 [US2] **Ongoing (SC-003)**: over the next ≥ 20 end-to-end runs, confirm the "never navigated to /status/*" failure doesn't recur. If any end-to-end run fails, confirm its log and trace show the cause, and record it here.

**Checkpoint**: The hydration race is closed. Any remaining flake is diagnosable thanks to US1.

---

## Phase 5: User Story 3 - CI runs on supported runtimes, quietly and deterministically (Priority: P3)

**Goal**: No recurring non-actionable warnings, a supported Node release, and lockfile-exact fast installs (FR-009–FR-012).

**Independent Test**: quickstart.md §1, the warnings and install-time part.

### Implementation for User Story 3

- [X] T013 [US3] In `.github/workflows/ci.yml`, bump every job to `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`, and `actions/dependency-review-action@v5` (research Decision 6). *(2dda308)*
- [X] T014 [US3] In `.github/workflows/ci.yml`, set `node-version: "24"` in the end-to-end and frontend-checks jobs (FR-010). *(2dda308)*
- [X] T015 [US3] In `.github/workflows/ci.yml`, change the end-to-end job's `npm install` to `npm ci --no-audit --no-fund` with an explanatory comment, and add the same flags to frontend-checks' `npm ci` (research Decision 7). *(2dda308)* T013–T015 edit the same file, so they run in sequence with T006.
- [X] T016 [P] [US3] In `hosts/OrderSystem.AppHost/OrderSystem.AppHost.csproj`, add `MSBuildWarningsAsMessages += ASPIRE010` with a comment explaining why. Verify that `dotnet build hosts/OrderSystem.AppHost` gives 0 warnings (research Decision 8). *(2dda308)*

### Validation for User Story 3

- [X] T017 [US3] PR #10 CI (commit 2dda308) shows all jobs green and no Node 20 deprecation or ASPIRE010 annotations; only the informational Ubuntu-migration notice remains. Frontend install took 8 s in end-to-end and 9 s in frontend-checks (SC-005, SC-006, SC-007).

**Checkpoint**: CI is quiet, uses supported runtimes, and installs deterministically.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T018 Write the retroactive design docs: `specs/006-make-end-to-end/spec.md`, `plan.md`, `research.md`, `quickstart.md`, `tasks.md`.
- [X] T019 Confirm PR #10 is green on its **latest** commit, which adds the spec and the T010 hydration-timeout message, then merge it (the maintainer's action).
  - *2026-11-02:* PR #10 was green on its latest commit (`5739d51`), and the maintainer merged it as `a6abaa3`.
- [X] T020 After the merge, confirm `main` CI is green, and set spec.md **Status** to note that it's merged.
  - *2026-11-02:* `main` CI on `a6abaa3` was green: Unit, Adapter integration, End-to-end, Frontend checks and Dependency audit all succeeded, and Dependency review was skipped on push as designed. Spec Status updated.

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → all stories.
- **US1 and US2** share `CreateOrderE2ETests.cs`, so they run in order: T003 → T004 → T005 → T009 → T010.
- **ci.yml**: T006, T013, T014 and T015 run in sequence in that one file.
- **Parallel**: T007 (README) and T016 (AppHost csproj) are independent of everything else.
- **Validation**: T008 needs T003–T006. T011 needs T009. T017 needs T013–T016.
- **Polish**: T019 needs everything above. T020 needs the merge. T012 is ongoing after the merge.

## Implementation Strategy

US1 is the MVP. A flake that leaves evidence can be fixed; one that doesn't
can only be retried. US2 then removes the most likely cause, and US3 is
maintenance that rode along in the same PR at the maintainer's request.

## Notes

- Out of scope, and candidates for their own specs:
  - running the end-to-end tests against a production Next.js build (the
    structural fix for US2's class of problem)
  - the Ubuntu runner migration
  - the stale "Turbopack" comment in `AppHostFixture.cs`
