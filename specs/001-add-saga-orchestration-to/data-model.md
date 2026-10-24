# Data Model: Saga Orchestration for Order Fulfillment

## Order (existing — unchanged)

No field, status value, or transition changes. Referenced here only to show
how the new entity relates to it.

- `Status` values stay `Created`, `PaymentPending`, `PaymentFailed`,
  `Confirmed`, `Cancelled`. `Cancelled` now also represents the terminal
  outcome of exhausted payment retries (FR-004), in addition to direct
  cancellation — no new value needed since both are, to a customer, "this
  order did not go through."
- `Order.Cancel()` already throws from `Confirmed` and is already valid
  from `Created`, `PaymentPending`, and `PaymentFailed` — exactly the states
  this feature needs it callable from (FR-003/FR-004/FR-005, and the
  existing-behavior guard in FR-008). No change to `Order.cs`.

## OrderFulfillmentSagaState (new)

Tracks exactly one fact the orchestrator needs that `Order` doesn't carry:
whether stock is currently held for this order. Lives in
`OrderSystem.Application/Sagas/`.

| Field | Type | Notes |
|---|---|---|
| `OrderId` | `OrderId` (existing value object) | Primary key; 1:1 with `Order.Id`. Not a foreign-key-enforced join in the business sense — this table can briefly exist for an order row that's still being created in the same unit of work, and conceptually, the two could live in different stores entirely. |
| `IsStockReserved` | `bool` | `true` once reservation succeeds; `false` after release. Starts `false`. |
| `UpdatedAt` | `DateTimeOffset` | Set on every transition, for observability (Adapters.IntegrationTests can assert it moved). |

**Construction / transitions** (mirrors `Order`'s private-constructor,
named-method style):

- `OrderFulfillmentSagaState.Start(OrderId orderId)` — creates the row with
  `IsStockReserved = false`.
- `MarkStockReserved()` — sets `IsStockReserved = true`. Called once, right
  after a successful `IInventoryService.TryReserveAsync`.
- `ReleaseStockIfReserved()` — if `IsStockReserved` is `false`, does nothing
  and returns `false` (FR-006/FR-009: releasing twice, or releasing nothing,
  is a safe no-op, not an error). If `true`, sets it to `false`, updates
  `UpdatedAt`, and returns `true` (the caller uses this to decide whether
  `IInventoryService.ReleaseAsync` actually needs calling).

Unlike `Order`'s guarded transitions (which throw on an invalid call),
`ReleaseStockIfReserved()` is deliberately idempotent-by-return-value rather
than throw-on-repeat — FR-006 requires exactly that, since both the
exhausted-retry path and a manual cancellation could plausibly reach it for
the same order.

**Validation rules**: none beyond the type system — this entity has no
business invariants of its own; it is bookkeeping, not a domain concept with
rules a customer or the business cares about.

## IOrderFulfillmentSagaRepository (new port)

`OrderSystem.Application/Sagas/`, shaped like the existing
`IOrderRepository`:

```csharp
public interface IOrderFulfillmentSagaRepository
{
    Task<OrderFulfillmentSagaState?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task AddAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrderFulfillmentSagaState state, CancellationToken cancellationToken = default);
}
```

`GetByOrderIdAsync` returning `null` is the expected, non-error outcome for
"this order never got a saga row" (FR-009) — never translated into an
exception by any caller.

## IInventoryService (new port)

`OrderSystem.Application/Inventory/`, shaped like the existing
`IPaymentGateway`:

```csharp
public interface IInventoryService
{
    Task<bool> TryReserveAsync(Order order, CancellationToken cancellationToken = default);
    Task ReleaseAsync(OrderId orderId, CancellationToken cancellationToken = default);
}
```

`SimulatedInventoryService` (Application): `TryReserveAsync` always returns
`true`; `ReleaseAsync` is a no-op — it holds no state of its own, since
"was this order's stock already released" is `OrderFulfillmentSagaState`'s
job, not the inventory service's (see research.md Decision 2).

## Postgres mapping

New table `order_fulfillment_sagas`:

| Column | Type | Notes |
|---|---|---|
| `order_id` | `uuid` | Primary key, same value as `orders.id`. |
| `is_stock_reserved` | `boolean` | |
| `updated_at` | `timestamptz` | |

Mapped in `OrderDbContext` the same way `orders` is: a conversion from
`OrderId` to its `Guid` value, `ValueGeneratedNever()` since the id is
assigned by the orchestrator, not the database. Added via a new EF Core
migration (`dotnet ef migrations add AddOrderFulfillmentSaga --project
OrderSystem.Adapters.Postgres`), applied the same way the existing
`InitialCreate` migration is — through `OrderSystem.MigrationService` at
startup, not manually.
