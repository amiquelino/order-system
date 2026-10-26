---

description: "Task list for Improve Source Organization"
---

# Tasks: Improve Source Organization

**Input**: Design documents from `specs/003-improve-source-organization-so/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
quickstart.md (all present; no contracts/ — no new interface)

**Tests**: Not applicable in the usual sense — there is no new application
code. This feature's "tests" are confirming the existing four test tiers
and the CI pipeline still pass after everything they depend on moved
(FR-002/SC-002), tracked below as verification tasks rather than new
`*.Tests` files.

**Organization**: Per research.md Decision 2, there is no partial state
where the solution stays buildable — the 13 moves and their reference
updates are one indivisible unit of work (Phase 2). As with the CI
feature, the two user-story phases below are validation against the
single change Phase 2 makes, not separate implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies).
- **[Story]**: Which user story this task validates (US1, US2) — omitted
  for Setup/Foundational/Polish tasks.

## Phase 1: Setup

- [X] T001 Create branch `improve-source-organization` from `main`;
  scaffold the target directories: `src/Domain`, `src/Application`,
  `src/Adapters`, `hosts`, `tests` (quickstart.md step 1)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one atomic move everything else depends on.

**⚠️ CRITICAL**: Per research.md Decision 2, the solution will not build
between T002 and T005 — there is no safe place to stop partway through
this phase.

- [X] T002 `git mv` all 13 project directories per data-model.md's move
  table (depends on T001)
- [X] T003 [P] Apply all 24 `<ProjectReference>` edits across the 12
  `.csproj` files that have one, per data-model.md's reference table
  (depends on T002) — corrected count; plan.md/data-model.md originally
  said 26/11, actual is 24/12
- [X] T004 [P] Apply all 13 `<Project Path>` edits in `OrderSystem.slnx`,
  per data-model.md (depends on T002)
- [X] T005 Run `dotnet build OrderSystem.slnx` and fix any remaining
  reference mismatch against data-model.md until it builds clean — this
  is FR-002's first gate, before touching CI config or docs (depends on
  T003, T004)
- [X] T006 [P] Apply the 5 path edits in `.github/workflows/ci.yml`, per
  data-model.md (depends on T005)
- [X] T007 [P] Apply every path edit in `README.md` (Running the
  application, Database schema, Automated tests, Solution layout), per
  data-model.md (depends on T005) — a follow-up sweep found one inline
  prose path data-model.md's table missed (`OrderSystem.Application/Sagas/`
  in the "Saga orchestration" section); fixed too.
- [X] T008 Run the full local test suite —
  `dotnet test tests/OrderSystem.Domain.Tests`,
  `dotnet test tests/OrderSystem.Application.Tests`,
  `dotnet test tests/OrderSystem.Adapters.IntegrationTests` (needs
  Docker), `dotnet test tests/OrderSystem.EndToEndTests` (needs Docker) —
  and confirm the same pass/fail results as on `main` before this change
  (depends on T005) — Domain.Tests 21/21, Application.Tests 36/36,
  Adapters.IntegrationTests 9/9, all identical to pre-move. EndToEndTests
  **failed first** (`TimeoutException` waiting for the frontend resource)
  — a real regression this gate was supposed to catch: `AppHost.cs`'s
  `AddJavaScriptApp("frontend", "../order-system-frontend")` is a
  string-literal relative path, not a `<ProjectReference>`, so the
  planning-phase audit missed it. Fixed to `"../../order-system-frontend"`
  (AppHost moved one level deeper); re-ran, 1/1 pass. Documented in
  data-model.md for the record.

**Checkpoint**: The move is complete and verified locally. Nothing is
pushed yet.

---

## Phase 3: User Story 1 - The architecture is visible in the structure itself (Priority: P1) 🎯 MVP

**Goal**: Confirm the new grouping is actually visible to someone
browsing the repo, not just correct on disk.

**Independent Test**: quickstart.md steps 8 and 10.

- [X] T009 [US1] Push the branch and open a pull request (quickstart.md
  step 8); browse its file tree (PR "Files changed" tab, or the branch's
  own GitHub file browser) and confirm projects appear grouped under
  `src/`, `hosts/`, and `tests/` rather than as one flat list of 13+
  siblings (SC-001, SC-003)

**Checkpoint**: User Story 1 confirmed on the pushed branch.

---

## Phase 4: User Story 2 - Test projects are grouped distinctly (Priority: P2)

**Goal**: Confirm the four test projects read as one distinct group,
separate from the source projects they test.

**Independent Test**: Same browse as T009, looking specifically at
`tests/`.

- [X] T010 [US2] Within the same pull request's file tree, confirm all
  four test projects (`OrderSystem.Domain.Tests`,
  `OrderSystem.Application.Tests`, `OrderSystem.Adapters.IntegrationTests`,
  `OrderSystem.EndToEndTests`) sit together under `tests/`, distinguishable
  from the source projects in `src/`/`hosts/` without consulting
  `README.md`

**Checkpoint**: Both user stories confirmed on the same pull request.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T011 Watch the pull request's CI checks (`gh pr checks <number>`)
  until all four jobs finish; confirm all four pass — the authoritative,
  hosted-runner proof of FR-002/SC-002, and incidentally a second
  independent confirmation that the CI feature itself still works after a
  change this disruptive to file layout (depends on T009) — confirmed:
  PR #2, all 4 jobs pass on both the push and pull_request runs
  (`end-to-end-tests` ~2m30-2m38s, the rest well under a minute each).
- [X] T012 Merge the pull request once T011 is green — merged by the user
  (merging without human review is outside what this agent does
  autonomously); PR #2 merged 2026-10-26.
- [X] T013 Delete the local and remote `improve-source-organization`
  branch after merging (depends on T012) — both deleted.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. Internally linear per
  research.md Decision 2 — T002 → {T003, T004} → T005 (gate) →
  {T006, T007} → T008 (gate).
- **User Story 1 (Phase 3)**: Depends on all of Phase 2 (needs a pushed
  branch with the completed move).
- **User Story 2 (Phase 4)**: Depends on Phase 2 the same way Phase 3
  does; in practice reuses Phase 3's same pull request rather than a
  second push.
- **Polish (Phase 5)**: T011 depends on T009 (needs the PR open to watch
  its checks). T012 depends on T011. T013 depends on T012.

### Within Phase 2

T002 must complete before T003 or T004 can be applied (the files have to
exist at their new paths first). T003 and T004 touch different files
(`.csproj`s vs. `.slnx`) and don't depend on each other. T005 is a gate —
nothing after it should be attempted until it passes. T006 and T007 touch
different files and don't depend on each other, but both only make sense
once T005 confirms the move itself is correct. T008 is the second gate.

---

## Parallel Example

```bash
# After T002 (all moves done):
Task: "Apply all 26 <ProjectReference> edits across the 11 .csproj files"
Task: "Apply all 13 <Project Path> edits in OrderSystem.slnx"

# After T005 (local build confirmed):
Task: "Apply the 5 path edits in .github/workflows/ci.yml"
Task: "Apply every path edit in README.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (branch + scaffold) + Phase 2 (the one real move,
   verified locally).
2. Complete Phase 3 (push, open PR, confirm the grouping is visible).
3. **STOP and VALIDATE**: this alone already delivers the core value.
4. Phase 4 and Phase 5 follow immediately — neither needs new work beyond
   what Phase 2/3 already produced.

### Incremental Delivery

1. Setup + Foundational → the move exists, builds, and passes tests
   locally.
2. User Story 1 → confirmed visible on the pushed branch/PR.
3. User Story 2 → confirmed on the same PR (test grouping specifically).
4. Polish → CI green on the hosted runner, merge, clean up.

---

## Notes

- No task introduces new application behavior — every task either moves a
  file, edits a path string to match where that file now lives, or
  verifies nothing broke.
- Mark each task `[X]` as it's completed, not in one pass at the end.
