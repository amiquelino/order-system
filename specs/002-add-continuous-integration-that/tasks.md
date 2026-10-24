---

description: "Task list for Continuous Integration"
---

# Tasks: Continuous Integration

**Input**: Design documents from `specs/002-add-continuous-integration-that/`

**Prerequisites**: plan.md, spec.md, research.md, quickstart.md (all
present; no data-model.md or contracts/ — this feature has no business
entities and exposes no new interface, per plan.md)

**Tests**: Not applicable in the usual sense — this feature's "tests" are
the three quickstart scenarios (push, open a PR, observe the result), since
there's no application code to unit-test. They're tracked as their own
tasks below rather than as `*.Tests` project files.

**Organization**: Tasks are grouped by user story per spec.md's priorities.
Nearly all of the real work is the single new workflow file
(`.github/workflows/ci.yml`), authored once in Phase 2 — by design
(plan.md's Structure Decision), that one file already satisfies all three
user stories simultaneously (per-tier jobs + both triggers). The per-story
phases below are therefore validation against quickstart.md, not separate
implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies). Tasks
  T003–T006 all edit the same single `ci.yml` file and are therefore
  **not** marked `[P]` even though the jobs they add are logically
  independent of each other.
- **[Story]**: Which user story this task validates (US1, US2, US3) —
  omitted for Setup/Foundational/Polish tasks, per convention.

## Phase 1: Setup

No new account, package, or dependency is needed (plan.md Technical
Context) — this feature only adds one file to the repository.

- [X] T001 Create the `.github/workflows/` directory

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one workflow file every user story depends on.

**⚠️ CRITICAL**: No validation work (Phase 3–5) can happen until this file
exists and is pushed.

- [X] T002 In `.github/workflows/ci.yml`, author the workflow skeleton:
  `name: CI`, triggers on `push` (any branch) and `pull_request` (base
  `main`) — satisfies FR-001 and is what makes User Story 2's PR-visible
  status possible with no further configuration
- [X] T003 In `.github/workflows/ci.yml`, add the `domain-and-application-tests`
  job: checkout, `actions/setup-dotnet` (.NET 10), then
  `dotnet test OrderSystem.Domain.Tests` and
  `dotnet test OrderSystem.Application.Tests` — no Docker needed (depends
  on T002)
- [X] T004 In `.github/workflows/ci.yml`, add the `adapter-integration-tests`
  job: checkout, `actions/setup-dotnet`, `dotnet test
  OrderSystem.Adapters.IntegrationTests` — Docker ships pre-installed and
  running on `ubuntu-latest`, so no Docker setup step is needed
  (research.md Decision 3) (depends on T002)
- [X] T005 In `.github/workflows/ci.yml`, add the `end-to-end-tests` job:
  checkout, `actions/setup-dotnet`, `actions/setup-node`, build
  `OrderSystem.EndToEndTests`, install the Playwright Chromium browser with
  `--with-deps` (research.md Decision 4 — real `sudo`/`apt` access on this
  runner, unlike the local dev workaround), then
  `dotnet test OrderSystem.EndToEndTests` (depends on T002)
- [X] T006 In `.github/workflows/ci.yml`, add the `frontend-checks` job:
  checkout, `actions/setup-node`, `npm ci`, `npm run lint`, `npm run build`,
  all with working directory `order-system-frontend` (depends on T002)

**Checkpoint**: Push this file to a branch and confirm all four jobs appear
and run (they may still be failing or passing at this point — that's what
Phase 3 actually validates).

---

## Phase 3: User Story 1 - Every push is automatically built and tested (Priority: P1) 🎯 MVP

**Goal**: Confirm pushing code triggers the full suite automatically, with
no manual step, and reports pass/fail without requiring local reproduction.

**Independent Test**: quickstart.md Scenario A.

- [ ] T007 [US1] Run quickstart.md Scenario A (push a clean commit on a
  scratch branch) and confirm: the run starts automatically with no manual
  trigger (SC-001), all four jobs from Phase 2 report success (SC-002),
  and total wall-clock time from push to every job finishing is under 15
  minutes (SC-003)

**Checkpoint**: User Story 1 confirmed working on its own, independent of
any pull request.

---

## Phase 4: User Story 2 - Pull requests show pass/fail status directly (Priority: P2)

**Goal**: Confirm a pull request surfaces the run's result directly on the
PR page — no new job or code beyond Phase 2's `pull_request` trigger.

**Independent Test**: quickstart.md Scenario B (the PR-visibility half).

- [ ] T008 [US2] Run quickstart.md Scenario B (open a pull request) and
  confirm: the PR shows an in-progress state while jobs are running, and
  the final result directly on the PR page once they finish — without
  needing to leave the PR to learn it

**Checkpoint**: Both user stories 1 and 2 confirmed working together.

---

## Phase 5: User Story 3 - A failure points at which tier broke (Priority: P3)

**Goal**: Confirm a single broken tier is identifiable from the run
summary alone — no new job or code beyond Phase 2's per-tier job split.

**Independent Test**: quickstart.md Scenario B (the failure-identification
half — reuses the same pull request as T008, with exactly one tier
deliberately broken).

- [ ] T009 [US3] Within the same quickstart.md Scenario B pull request,
  confirm the run's summary shows exactly the one job matching the broken
  tier as failed, visually distinct from the other three jobs, which still
  report success — without opening that job's log (SC-004)

**Checkpoint**: All three user stories confirmed independently.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T010 [P] Add a CI status badge near the top of `README.md`
  (`https://github.com/amiquelino/order-system/actions/workflows/ci.yml/badge.svg`)
- [ ] T011 Clean up the quickstart scratch branches and pull request
  created during T007–T009 (quickstart.md "Cleanup" section)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. T003–T006 each depend on
  T002 (the skeleton) but not on each other — they're independent
  additions to the same file, edited one at a time since they share it.
- **User Story 1 (Phase 3)**: Depends on Foundational (needs all of
  T002–T006 pushed to validate).
- **User Story 2 (Phase 4)**: Depends on Foundational only, same as Phase
  3 — not on Phase 3's result, though doing T007 first before opening a PR
  is the natural order.
- **User Story 3 (Phase 5)**: Depends on Foundational, and in practice
  reuses Phase 4's same pull request (T008 and T009 are two checks against
  one PR, not two separate pushes).
- **Polish (Phase 6)**: T010 depends only on T002 (the file needs to exist
  for its badge URL to resolve) and can happen any time after — it doesn't
  need T003–T009. T011 depends on T007–T009 having run (there's nothing to
  clean up before then).

### Within Phase 2

T002 first. T003, T004, T005, T006 each depend only on T002, but all edit
`ci.yml`, so they're done one at a time in this list's order rather than
truly in parallel.

---

## Parallel Example

```bash
# T010 can run any time after T002, alongside Phase 2-5 work on ci.yml —
# different file, no dependency on the jobs themselves:
Task: "Add a CI status badge near the top of README.md"
```

There is no other genuine parallel opportunity in this feature: T003–T006
share one file, and T007–T009 are sequential validations of the same
pushed artifact.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (trivial) + Phase 2 (the one real file).
2. Complete Phase 3 (push a clean commit, confirm it all works).
3. **STOP and VALIDATE**: this alone already delivers the core value —
   automatic build-and-test on every push.
4. Phases 4, 5, and 6 are all small and can follow immediately — none of
   them need new code.

### Incremental Delivery

1. Setup + Foundational → `ci.yml` exists with all four jobs.
2. User Story 1 → confirmed via a clean scratch push.
3. User Story 2 + User Story 3 → confirmed together via one scratch pull
   request (clean half for US2, a deliberately-broken tier for US3).
4. Polish → badge, scratch-branch cleanup.

---

## Notes

- No task touches any existing `OrderSystem.*` project or
  `order-system-frontend` source file — this feature is entirely new
  infrastructure-as-config plus a one-line README addition, per plan.md.
- Mark each task `[X]` as it's completed, not in one pass at the end.
