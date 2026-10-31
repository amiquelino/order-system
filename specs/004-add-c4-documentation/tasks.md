---

description: "Task list for Add C4 Documentation"
---

# Tasks: Add C4 Documentation

**Input**: Design documents from `specs/004-add-c4-documentation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
quickstart.md (all present; no contracts/ — no new interface)

**Tests**: Not applicable — no executable code is added, and Constitution
Principle IV argues against a docs-lint CI job for one file. Verification
is the render-and-cross-check steps from `quickstart.md`, tracked below as
tasks per story.

**Organization**: All five diagrams live in one file, `docs/architecture/c4.md`
(plan.md Structure Decision), so tasks that add a view cannot run in
parallel with each other; none carry `[P]` except where they touch a
different file.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies).
- **[Story]**: Which user story the task belongs to (US1–US4); omitted for
  Setup/Foundational/Polish tasks.
- Scratch files for rendering go in the session scratchpad, never in the repo.

## Phase 1: Setup

**Purpose**: Create the document shell that every view is added to.

- [X] T001 Create `docs/architecture/c4.md` with: title, one-paragraph
      purpose, a contents list naming the five views, and a short legend
      (replicas shown once with a count in the label; payment and inventory
      are simulated and run inside the Api; dev-only UIs from the AppHost
      are not drawn). No diagrams yet.

---

## Phase 2: Foundational

**Purpose**: Fix the facts every view is drawn from, before drawing.

**⚠️ CRITICAL**: No view should be written until this is done.

- [X] T002 Re-read `hosts/OrderSystem.AppHost/AppHost.cs`,
      `hosts/OrderSystem.Api/Program.cs`,
      `hosts/OrderSystem.NotificationService/Program.cs`,
      `src/Adapters/OrderSystem.Adapters.RabbitMQ/ServiceCollectionExtensions.cs`
      and both consumer classes, and confirm every row of `data-model.md`
      (elements, host process, relationships) still matches the code. Fix
      `data-model.md` first if anything has drifted.

**Checkpoint**: The inventory is verified; views can be written from it.

---

## Phase 3: User Story 1 - Whole system at a glance (Priority: P1) 🎯 MVP

**Goal**: A newcomer can name every deployable piece, store, and broker from
two views.

**Independent Test**: Render the two views and answer, from them alone,
which pieces are deployable, where data lives, which two brokers exist, and
what pushes live updates to the user (quickstart.md steps 1, 2, 4).

- [X] T003 [US1] Add the **System context** view (`C4Context`) to
      `docs/architecture/c4.md` per `data-model.md` Level 1: Customer,
      OrderSystem, and the two simulated external capabilities, with a
      caption and labelled relationships (FR-001, FR-005, FR-006).
- [X] T004 [US1] Add the **Container** view (`C4Container`) to
      `docs/architecture/c4.md` per `data-model.md` Level 2: exactly the
      eight AppHost resources plus the Customer, the Api labelled "2
      replicas", and all ten container-level relationships including the
      Api→RabbitMQ→Api retry loop (FR-001, FR-003, FR-004, FR-005).
- [X] T005 [US1] Render both views with `mermaid-cli` via `npx` (quickstart
      step 1) and compare the container view to `AppHost.cs` (step 2). Fix
      clipped labels per research.md Decision 3; if a block is unreadable,
      switch that view to a styled flowchart and say so in its caption.

**Checkpoint**: MVP — the system can be understood from two diagrams.

---

## Phase 4: User Story 2 - Service internals (Priority: P2)

**Goal**: A contributor can find the saga and both consumers, and see where
the hexagonal boundaries fall.

**Independent Test**: Using only the component views, point to the saga
orchestrator, each driven adapter, and each consumer with its host (quickstart
steps 3, 4).

- [X] T006 [US2] Add the **Api components** view (`C4Component`) to
      `docs/architecture/c4.md` per `data-model.md` Level 3 (Api): endpoints,
      message bus, handlers, saga orchestrator, simulated payment/inventory,
      Domain, the three driven adapters, and the RabbitMQ retry consumer
      labelled as a driving adapter. Dependency arrows point inward; the
      consumer→Application link is labelled as the documented exception
      (FR-001, FR-004, FR-005).
- [X] T007 [US2] Add the **NotificationService components** view
      (`C4Component`) to `docs/architecture/c4.md`: Kafka order-events
      consumer (group `notification-service`, forwards only
      `OrderStatusChangedEvent`), `OrderHub`, with Redis backplane and the
      browser push shown (FR-001, FR-004, FR-005).
- [X] T008 [US2] Render both views and run quickstart step 3, including
      `grep -r "class .*Consumer" src hosts` to confirm exactly two
      consumers exist and both are shown in their hosting process.

**Checkpoint**: Contributors can navigate from containers down to
components.

---

## Phase 5: User Story 3 - One order end to end (Priority: P3)

**Goal**: The sequence the static views cannot show.

**Independent Test**: From the diagram alone, state which broker carries the
retry delay and which carries status events (quickstart step 1).

- [X] T009 [US3] Add the **Order flow with retry** view (`C4Dynamic`) to
      `docs/architecture/c4.md` with numbered steps covering persistence,
      event publication on each status change, retry scheduling, retry
      delivery, final outcome (confirmed, or cancelled after compensation),
      and the notification push. Caption states the retry cap (3 attempts)
      matches `OrderFulfillmentSagaOrchestrator` (FR-002).
- [X] T010 [US3] Render the view and confirm it parses and every numbered
      step is visible; verify the step order against
      `src/Application/OrderSystem.Application/Sagas/OrderFulfillmentSagaOrchestrator.cs`.

**Checkpoint**: All five views exist.

---

## Phase 6: User Story 4 - Discoverable and honest (Priority: P2)

**Goal**: Found from the front page, rendered on GitHub, and kept in sync.

**Independent Test**: From the repo front page reach the diagrams in at most
2 clicks, rendered as pictures; the maintenance note says what to update
(quickstart step 5).

- [X] T011 [US4] Add a **Maintaining this document** section at the end of
      `docs/architecture/c4.md`: a table mapping each kind of change (new or
      removed AppHost resource, new consumer or adapter, saga step change,
      new endpoint) to the views to revisit, and a pointer to
      `specs/004-add-c4-documentation/quickstart.md` for the check (FR-009).
- [X] T012 [P] [US4] Edit `README.md`: add a link to
      `docs/architecture/c4.md` in "Architecture overview". Where a
      sentence now duplicates a view, shorten it to point there; do not
      remove content that is not in the views, and do not contradict
      them (FR-007, FR-010).

**Checkpoint**: Documentation is linked and has an upkeep path.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T013 Run the full `quickstart.md` steps 1–4 once end to end over the
      finished file and fix any finding; confirm no arrow lacks a label
      (FR-005) and no element appears twice in a view.
- [X] T014 Confirm `git status` shows only `docs/architecture/c4.md`,
      `README.md`, and files under `specs/004-add-c4-documentation/`
      (plus `.specify/feature.json`); nothing under `src/`, `hosts/`,
      `tests/`, `.github/`, or the frontend changed.
- [ ] T015 **User action, not automatable**: push the branch and confirm
      every diagram renders on GitHub (quickstart step 5), then do the
      newcomer spot-check (step 6). Record the outcome in the final report
      rather than ticking this as done without it.

---

## Dependencies & Execution Order

- T001 → T002 → views. Within the views phase, T003–T004, T006–T007, T009
  and T011 all edit `c4.md`, so they run sequentially in that file; the
  stories are independent in content but not in file access.
- T012 edits a different file and may run any time after T001 (the link
  target must exist), in parallel with the view tasks.
- Each story's render task (T005, T008, T010) follows that story's view
  tasks.
- T013–T015 follow everything else.

### Story independence

US1 delivers value alone (two views). US2 and US3 add detail and do not
change US1. US4 can be delivered right after US1 to make the MVP
discoverable.

## Implementation Strategy

### MVP First

1. T001 (shell), T002 (verify facts)
2. T003–T005 (US1: context + containers, rendered and cross-checked)
3. T012 (README link) so the MVP is reachable
4. **Stop and validate**: a newcomer can name the pieces.

### Incremental Delivery

1. MVP above.
2. US2 (components) → render-check.
3. US3 (dynamic) → render-check.
4. US4 maintenance note, then Polish.

## Notes

- 15 tasks total; the only manual, non-agent step is T015.
- Commit after each phase checkpoint.
- Avoid: drawing anything not in `data-model.md`; adding tooling or CI
  (plan.md Constitution Check, Principle IV).
