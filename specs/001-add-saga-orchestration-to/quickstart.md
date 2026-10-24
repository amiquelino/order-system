# Quickstart: Saga Orchestration for Order Fulfillment

Validates all three functional paths this feature adds, using
`SimulatedPaymentGateway`'s existing, unchanged thresholds (`<=100` →
always approved, `<=1000` → approved from the first retry onward, `>1000` →
always declines) to pick order totals deterministically.

## Prerequisites

- `dotnet run --project OrderSystem.AppHost` (or any option from the
  README's "Running the application" section) — Postgres, Kafka, RabbitMQ,
  the Api (2 replicas), and NotificationService all up.
- Migrations applied (automatic via `OrderSystem.MigrationService` on
  startup — includes the new `order_fulfillment_sagas` table once this
  feature's migration exists).
- `API_URL` = the Api's resolved endpoint (from the Aspire dashboard, or the
  fixed port if running standalone per the README's Option C).

## Scenario A — Exhausted retries resolve to Cancelled (User Story 1, P1)

An order whose total is always declined (`> 1000`) should end up
`Cancelled` with nothing left reserved, automatically, with no manual step.

```bash
curl -s -X POST "$API_URL/orders" -H 'Content-Type: application/json' -d '{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "items": [{ "productName": "Quickstart Always-Declines Item", "quantity": 1, "unitPrice": 5000, "currency": "USD" }]
}'
# -> 201 Created, { "orderId": "<id>" }

# Poll every few seconds — 3 retries at the existing RabbitMQ delay means
# this settles within roughly the same window the README documents for the
# $5000 case today (3 retries, ~4s apart).
curl -s "$API_URL/orders/<id>"
```

**Expected outcome**: after the last retry, `status` becomes `"Cancelled"`
(today, before this feature, it stays `"PaymentFailed"` forever — that
difference is the thing being validated). Confirm nothing is left reserved
via the `OrderFulfillmentSagaOrchestratorTests`/
`PostgresOrderFulfillmentSagaRepositoryTests` assertions (`IsStockReserved`
is `false`) — not reachable through the public API, since reservation state
is internal bookkeeping, not an HTTP-exposed fact.

## Scenario B — Manual cancellation releases reserved stock (User Story 2, P2)

An order whose total declines once then would succeed on retry
(`<= 1000` and `> 100`) gives a real window — between the first decline and
the retry firing — to cancel manually while stock is still held.

```bash
curl -s -X POST "$API_URL/orders" -H 'Content-Type: application/json' -d '{
  "customerId": "22222222-2222-2222-2222-222222222222",
  "items": [{ "productName": "Quickstart Retry-Then-Succeeds Item", "quantity": 1, "unitPrice": 500, "currency": "USD" }]
}'
# -> 201 Created, { "orderId": "<id>" }

# Immediately (before the ~4s retry delay elapses):
curl -s -X POST "$API_URL/orders/<id>/cancel"
```

**Expected outcome**: `204 No Content`, and a subsequent `GET
/orders/<id>` shows `"status": "Cancelled"`. The scheduled retry, when it
later fires, has no further effect (Edge Case in spec.md — cancellation
already moved the order out of this feature's flow).

## Scenario C — Confirmed orders still reject cancellation (existing behavior, FR-008)

```bash
curl -s -X POST "$API_URL/orders" -H 'Content-Type: application/json' -d '{
  "customerId": "33333333-3333-3333-3333-333333333333",
  "items": [{ "productName": "Quickstart Always-Approved Item", "quantity": 1, "unitPrice": 50, "currency": "USD" }]
}'
# -> 201 Created, { "orderId": "<id>" }, confirmed synchronously (<= 100 always approves)

curl -s -X POST "$API_URL/orders/<id>/cancel"
```

**Expected outcome**: `409 Conflict` — unchanged from today. Confirms this
feature didn't loosen the existing guard in `Order.Cancel()`.

## Idempotent release (FR-006) — not a manual API scenario

`Order.Cancel()` already rejects a second direct cancel at the aggregate
level (`InvalidOperationException`), so there's no two-step curl sequence
that exercises double-release through the public API. This property is
validated directly: call the orchestrator's compensation path twice for the
same order in `OrderFulfillmentSagaOrchestratorTests` and
`PostgresOrderFulfillmentSagaRepositoryTests`, asserting the second call is
a no-op (no exception, `IsStockReserved` stays `false`,
`IInventoryService.ReleaseAsync` is not called a second time).

## Regression check

The existing `OrderSystem.EndToEndTests` scenario (a `$500` order:
`PaymentFailed` then live-confirms via SignalR on retry) must still pass
unchanged — this feature must not alter the happy/transient-failure path's
timing or outcome.
