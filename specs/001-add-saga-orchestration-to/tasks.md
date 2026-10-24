---

description: "Task list for Saga Orchestration for Order Fulfillment"
---

# Tasks: Saga Orchestration for Order Fulfillment

**Input**: Design documents from `specs/001-add-saga-orchestration-to/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md (all present; no contracts/ — this feature adds no new external interface)

**Tests**: Included. Not explicitly requested in the spec, but matches this
codebase's established practice (every handler change in this project ships
with matching test coverage at the layer it touches — Constitution
Principle III) and plan.md's Project Structure already names the specific
new/changed test files.

**Organization**: Tasks are grouped by user story per spec.md's priorities.
Both stories route through the same new orchestrator, so most of the new
code is genuinely shared and sits in Phase 2 (Foundational) rather than
being splittable per-story; the per-story phases are the handler wiring and
the tests proving each story's specific acceptance scenarios.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2) — omitted
  for Setup/Foundational/Polish tasks, per the template's convention
- File paths are real `OrderSystem.*` project paths from plan.md's Project
  Structure section

## Phase 1: Setup

No new NuGet packages and no new Aspire resource are needed (plan.md
Technical Context) — this feature only adds files to three existing
projects.

- [X] T001 Create the `OrderSystem.Application/Sagas/` and
  `OrderSystem.Application/Inventory/` folders (the new ports, entity, and
  orchestrator from Phase 2 live here)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The new entity, ports, orchestrator, and adapter that both user
stories depend on. Neither story's handler wiring (Phase 3/4) can be written
correctly until these exist.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create `OrderFulfillmentSagaState` in
  `OrderSystem.Application/Sagas/OrderFulfillmentSagaState.cs` per
  data-model.md: `OrderId`, `IsStockReserved`, `UpdatedAt`; static
  `Start(OrderId)`; `MarkStockReserved()`; `ReleaseStockIfReserved()`
  returning `bool` and performing a safe no-op (not a throw) when nothing is
  reserved (FR-006, FR-009)
- [X] T003 [P] Create `IOrderFulfillmentSagaRepository` port in
  `OrderSystem.Application/Sagas/IOrderFulfillmentSagaRepository.cs`:
  `GetByOrderIdAsync` (nullable return, not an exception, when no row
  exists), `AddAsync`, `UpdateAsync` — shaped like the existing
  `IOrderRepository`
- [X] T004 [P] Create `IInventoryService` port in
  `OrderSystem.Application/Inventory/IInventoryService.cs`:
  `TryReserveAsync(Order, CancellationToken)` returning `bool`,
  `ReleaseAsync(OrderId, CancellationToken)` — shaped like the existing
  `IPaymentGateway`
- [X] T005 [P] Create `SimulatedInventoryService` in
  `OrderSystem.Application/Inventory/SimulatedInventoryService.cs`:
  `TryReserveAsync` always returns `true`; `ReleaseAsync` is a no-op (it
  holds no state of its own — see research.md Decision 2)
- [X] T006 Create `OrderFulfillmentSagaOrchestrator` in
  `OrderSystem.Application/Sagas/OrderFulfillmentSagaOrchestrator.cs`
  (depends on T002–T005): `StartAsync(Order, CancellationToken)` — reserve
  via `IInventoryService`, persist the saga state, then (if reserved)
  `BeginPaymentProcessing`/charge attempt 0/`ConfirmPayment`-or-`FailPayment`
  and schedule a retry via the existing `IPaymentRetryQueue` on failure,
  publishing `OrderStatusChangedEvent` on every status change (Constitution
  Principle V); `ContinueAsync(Order, int attempt, CancellationToken)` —
  charge the given attempt, confirm/fail, and on failure either schedule the
  next retry or, if attempts are exhausted, call `CompensateAsync` then
  `Order.Cancel()` and publish both `OrderCancelledEvent` and
  `OrderStatusChangedEvent`; `CompensateAsync(OrderId, CancellationToken)` —
  load the saga state (a missing row is a no-op, FR-009), call
  `ReleaseStockIfReserved()`, and only call `IInventoryService.ReleaseAsync`
  when that returned `true` (FR-006's idempotency), then persist the updated
  saga state
- [X] T007 [P] Create `PostgresOrderFulfillmentSagaRepository` in
  `OrderSystem.Adapters.Postgres/PostgresOrderFulfillmentSagaRepository.cs`
  implementing `IOrderFulfillmentSagaRepository`
- [X] T008 Add the `OrderFulfillmentSagaState` mapping to
  `OrderSystem.Adapters.Postgres/OrderDbContext.cs`: new
  `DbSet<OrderFulfillmentSagaState>`, table `order_fulfillment_sagas`,
  `OrderId` converted to its `Guid` value with `ValueGeneratedNever()` (depends on T002)
- [X] T009 Generate the EF Core migration (`dotnet ef migrations add
  AddOrderFulfillmentSaga --project OrderSystem.Adapters.Postgres`) (depends
  on T008)
- [X] T010 Register `PostgresOrderFulfillmentSagaRepository` for
  `IOrderFulfillmentSagaRepository` in
  `OrderSystem.Adapters.Postgres/ServiceCollectionExtensions.cs` (depends on
  T007, T009)
- [X] T011 In `OrderSystem.Api/Program.cs`: register `IInventoryService` →
  `SimulatedInventoryService` (singleton, matching `IPaymentGateway`'s
  registration) and `OrderFulfillmentSagaOrchestrator` (scoped) (depends on
  T005, T006, T010)
- [X] T012 [P] Create `FakeInventoryService` in
  `OrderSystem.Application.Tests/Fakes/FakeInventoryService.cs`
  (in-memory, controllable reserve/release outcome for tests)
- [X] T013 [P] Create `FakeOrderFulfillmentSagaRepository` in
  `OrderSystem.Application.Tests/Fakes/FakeOrderFulfillmentSagaRepository.cs`

**Checkpoint**: Orchestrator and its ports exist and compile; nothing wires
to it yet.

---

## Phase 3: User Story 1 - Permanently-failed orders resolve instead of getting stuck (Priority: P1) 🎯 MVP

**Goal**: An order whose payment is always declined ends up `Cancelled`
automatically, with its reservation released, after exhausting retries —
instead of staying in `PaymentFailed` forever (today's bug).

**Independent Test**: quickstart.md Scenario A — a `$5000` order (always
declines per `SimulatedPaymentGateway`) settles at `Cancelled` with no
manual step.

### Implementation for User Story 1

- [X] T014 [US1] Change `CreateOrderCommandHandler` in
  `OrderSystem.Application/Commands/CreateOrderCommandHandler.cs` to build
  and persist the `Order`, then delegate the rest to
  `OrderFulfillmentSagaOrchestrator.StartAsync` (depends on T006, T011)
- [X] T015 [US1] Change `RetryPaymentCommandHandler` in
  `OrderSystem.Application/Commands/RetryPaymentCommandHandler.cs` to load
  the `Order` and guard its status as today, then delegate to
  `OrderFulfillmentSagaOrchestrator.ContinueAsync` (depends on T006)

### Tests for User Story 1

- [X] T016 [P] [US1] Rewrite
  `OrderSystem.Application.Tests/Commands/CreateOrderCommandHandlerTests.cs`
  for the orchestrator-backed flow, using `FakeInventoryService` and
  `FakeOrderFulfillmentSagaRepository`
- [X] T017 [P] [US1] Rewrite
  `OrderSystem.Application.Tests/Commands/RetryPaymentCommandHandlerTests.cs`,
  adding a new test for the exhausted-retries compensation path (releases
  stock, cancels the order, publishes `OrderCancelledEvent` then
  `OrderStatusChangedEvent`) using an amount `> 1000` so every attempt
  declines
- [X] T018 [US1] Create
  `OrderSystem.Application.Tests/Sagas/OrderFulfillmentSagaOrchestratorTests.cs`
  covering `StartAsync` (reservation succeeds → charge → confirm, and
  charge → fail → retry scheduled) and `ContinueAsync`'s exhausted-retries
  compensation branch
- [X] T019 [US1] Create
  `OrderSystem.Adapters.IntegrationTests/Postgres/PostgresOrderFulfillmentSagaRepositoryTests.cs`
  (Testcontainers) verifying a real round-trip: add, read back, update
  `IsStockReserved`, and that `UpdatedAt` advances

**Checkpoint**: User Story 1 fully functional and independently testable —
quickstart.md Scenario A passes.

---

## Phase 4: User Story 2 - Manual cancellation also releases what was held for the order (Priority: P2)

**Goal**: Cancelling an order directly releases anything reserved for it,
the same as the automatic exhausted-retry path does.

**Independent Test**: quickstart.md Scenario B — a `$500` order cancelled
manually while still `PaymentFailed` and awaiting its retry ends up
`Cancelled` with its reservation released.

### Implementation for User Story 2

- [X] T020 [US2] Change `CancelOrderCommandHandler` in
  `OrderSystem.Application/Commands/CancelOrderCommandHandler.cs` to call
  `OrderFulfillmentSagaOrchestrator.CompensateAsync` alongside its existing
  `Order.Cancel()` + `OrderCancelledEvent`/`OrderStatusChangedEvent`
  publishes (depends on T006, T011)

### Tests for User Story 2

- [X] T021 [P] [US2] Rewrite
  `OrderSystem.Application.Tests/Commands/CancelOrderCommandHandlerTests.cs`:
  a test for cancelling an order with reserved stock (releases it,
  Scenario B), a test confirming a `Confirmed` order still rejects
  cancellation (existing behavior, FR-008, Scenario C), and a test for
  cancelling an order with no saga row at all (FR-009 — safe no-op, no
  exception)
- [X] T022 [US2] Extend
  `OrderSystem.Application.Tests/Sagas/OrderFulfillmentSagaOrchestratorTests.cs`
  with `CompensateAsync`-specific cases: calling it twice for the same
  order calls `IInventoryService.ReleaseAsync` exactly once (FR-006), and
  calling it for an order whose reservation was never made is a no-op

**Checkpoint**: Both user stories independently functional.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T023 Run `dotnet test OrderSystem.slnx` and confirm
  `OrderSystem.Domain.Tests`, `OrderSystem.Application.Tests`, and
  `OrderSystem.Adapters.IntegrationTests` all pass — confirmed: `Domain.Tests`
  21/21, `Application.Tests` 36/36, `Adapters.IntegrationTests` 9/9
  (including the new `PostgresOrderFulfillmentSagaRepositoryTests`, run
  against a real Postgres via Testcontainers once Docker Desktop was started).
- [X] T024 Run `OrderSystem.EndToEndTests` and confirm the existing `$500`
  decline-then-retry-succeeds scenario still passes unchanged (regression
  check — quickstart.md's "Regression check" section) — confirmed, 1/1,
  after starting Docker Desktop and resolving a host-level `libasound.so.2`
  GLIBC mismatch (Homebrew's build needed a newer glibc than this Ubuntu
  22.04 host has; fetched the matching Ubuntu `libasound2` package instead
  via `apt-get download` + `dpkg-deb -x`, no root needed). Unrelated to this
  feature's code.
- [X] T025 [P] Update `README.md`: the Automated tests section (new
  Postgres integration test, the rewritten Application.Tests description)
  and the Payment retry flow section (a permanently-failed order now
  resolves to `Cancelled` instead of staying stuck in `PaymentFailed`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. Blocks both user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational only — not on User
  Story 1 (it touches `CancelOrderCommandHandler`, a different handler from
  `CreateOrderCommandHandler`/`RetryPaymentCommandHandler`), so it can
  proceed in parallel with Phase 3 once Phase 2 is done.
- **Polish (Phase 5)**: Depends on both user stories being complete.

### Within Phase 2

T002, T003, T004, T005 have no dependencies on each other — all `[P]`. T006
depends on all four. T007 (`[P]`) only depends on T003's interface shape,
not on T006. T008 depends on T002. T009 depends on T008. T010 depends on
T007 and T009. T011 depends on T005, T006, T010. T012 and T013 (`[P]`) only
depend on T003/T004's interface shapes.

### Within Each User Story

Implementation task(s) before their tests, since the tests exercise the
real orchestrator/handler code rather than driving it with TDD red-green —
consistent with how this codebase's existing Application.Tests are written.

---

## Parallel Example: Phase 2

```bash
# T002-T005 together (four independent new files):
Task: "Create OrderFulfillmentSagaState in OrderSystem.Application/Sagas/OrderFulfillmentSagaState.cs"
Task: "Create IOrderFulfillmentSagaRepository in OrderSystem.Application/Sagas/IOrderFulfillmentSagaRepository.cs"
Task: "Create IInventoryService in OrderSystem.Application/Inventory/IInventoryService.cs"
Task: "Create SimulatedInventoryService in OrderSystem.Application/Inventory/SimulatedInventoryService.cs"

# T012-T013 together, any time after T003/T004:
Task: "Create FakeInventoryService in OrderSystem.Application.Tests/Fakes/FakeInventoryService.cs"
Task: "Create FakeOrderFulfillmentSagaRepository in OrderSystem.Application.Tests/Fakes/FakeOrderFulfillmentSagaRepository.cs"
```

## Parallel Example: User Stories

```bash
# After Phase 2 completes, Phase 3 and Phase 4 can proceed together —
# different handlers, different test files, no shared new code between them.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (trivial) + Phase 2 (the real foundational work).
2. Complete Phase 3 (User Story 1).
3. **STOP and VALIDATE**: run quickstart.md Scenario A against a running
   `AppHost`.
4. Phase 4 (User Story 2) and Phase 5 (Polish) can follow once the MVP is
   confirmed, or be done in the same pass — they're small.

### Incremental Delivery

1. Setup + Foundational → orchestrator exists but nothing calls it yet.
2. User Story 1 → the actual motivating bug (stuck `PaymentFailed` orders)
   is fixed; validate with quickstart.md Scenario A.
3. User Story 2 → manual cancellation becomes consistent with the automatic
   path; validate with quickstart.md Scenarios B and C.
4. Polish → full suite + E2E regression check, README.

---

## Notes

- `[P]` tasks touch different files with no dependency between them.
- `[USn]` maps a task to its user story for traceability; Setup,
  Foundational, and Polish tasks carry no story label by convention.
- No task touches `OrderSystem.Domain`, `OrderSystem.Domain.Tests`,
  `OrderSystem.NotificationService`, `order-system-frontend`, or
  `OrderSystem.EndToEndTests` (other than running the existing E2E test as a
  regression check in T024) — per plan.md's Project Structure, this feature
  doesn't touch any of them.
- Mark each task `[X]` as it's completed, not in one pass at the end.
