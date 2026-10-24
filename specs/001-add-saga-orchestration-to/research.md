# Research: Saga Orchestration for Order Fulfillment

No `NEEDS CLARIFICATION` markers were left in Technical Context — every open
question was resolved during the architecture discussion that preceded this
spec. Recorded here anyway, since each one is a real decision with a
rejected alternative, not a default.

## Decision 1: Hand-rolled orchestrator vs. Wolverine's `Saga` base class

**Decision**: A plain C# `OrderFulfillmentSagaOrchestrator` class in
`OrderSystem.Application`, with its own persisted state and its own
repository port — no base class, no Wolverine-specific type.

**Rationale**: `OrderSystem.Application` currently references `WolverineFx`
only by package/convention — no handler imports a Wolverine namespace or
inherits a Wolverine type; discovery is by method-signature convention
(`Handle(Command, CancellationToken)`). Subclassing Wolverine's `Saga` would
be the first actual Wolverine type inside the hexagonal core, breaking
Principle I's "framework-free except by convention" property this codebase
has maintained so far. Wolverine's saga persistence also needs durable
envelope/message storage (an EF Core or Postgresql persistence package, plus
outbox-style delivery guarantees) to be correct across the Api's two
replicas — substantial new infrastructure for one thin, two-step workflow.

**Alternatives considered**:
- *Wolverine `Saga`* — rejected per above: framework leakage into
  Application, plus durable-storage infrastructure disproportionate to the
  problem.
- *MassTransit state-machine saga* — rejected without deep investigation: it
  would introduce a second competing messaging/mediator framework alongside
  Wolverine, for a single in-process workflow that doesn't need one.

## Decision 2: Where reservation-held state lives

**Decision**: A new `OrderFulfillmentSagaState` record, persisted in its own
table (`order_fulfillment_sagas`), related to `Order` by `OrderId` but not
part of the `Order` entity or its EF mapping.

**Rationale**: Tracking "is this order's stock currently reserved" is purely
an orchestration bookkeeping concern (needed so release can be idempotent
per FR-006/FR-009) — it is not a customer-visible fact about the order
itself, and `Order`'s own state machine (`Order.cs`) and status values
already fully support everything the business-visible side of this feature
needs with zero changes. Keeping it separate means `Order`'s schema, its
existing invariants, and every piece of code that already depends on its
shape (the frontend, `NotificationService`, the repository, every existing
test) stays untouched.

**Alternatives considered**:
- *Add an `IsStockReserved` column directly to `orders`* — rejected: grows
  the core aggregate's schema for a fact that only the orchestrator itself
  ever reads, and ties an orchestration-lifecycle concern to the aggregate's
  own migration history.

## Decision 3: Reservation always succeeds (no capacity modeling)

**Decision**: `SimulatedInventoryService.TryReserveAsync` always returns
`true`. No stock-count table, no per-SKU quantity tracking.

**Rationale**: The spec's own Assumptions section scopes this out
explicitly, and Constitution Principle IV forbids building for a scenario
("what if we run out of stock") the project doesn't have a concrete,
already-known requirement for yet. `IInventoryService.TryReserveAsync`'s
signature still returns `bool` (not `void`) so a future adapter that does
model capacity is a drop-in replacement with no caller changes — the port's
shape doesn't foreclose the future, only today's implementation does.

**Alternatives considered**:
- *A real in-memory or Postgres-backed stock-count table now* — rejected:
  nothing in the spec calls for it, and it would be exactly the kind of
  speculative generality Principle IV exists to prevent.
