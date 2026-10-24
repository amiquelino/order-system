# Implementation Plan: Saga Orchestration for Order Fulfillment

**Branch**: `specs/001-add-saga-orchestration-to/` (no git branches in this repo — feature identity is this directory) | **Date**: 2026-10-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-add-saga-orchestration-to/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

Add a stock-reservation step before payment and a compensation path for when
payment permanently fails, orchestrated by a new, hand-rolled
`OrderFulfillmentSagaOrchestrator` in `OrderSystem.Application`. No new
`OrderStatus` values, no new top-level project, no new Aspire resource — the
orchestrator absorbs logic currently duplicated across
`CreateOrderCommandHandler` and `RetryPaymentCommandHandler`, adds a new
`IInventoryService` port (simulated, always succeeds) and a new persisted
`OrderFulfillmentSagaState` (tracked separately from `Order`, via a new
`IOrderFulfillmentSagaRepository` port + Postgres adapter) so that releasing
reserved stock is idempotent regardless of which path triggers it.

## Technical Context

**Language/Version**: C# / .NET 10 (unchanged)

**Primary Dependencies**: Wolverine (existing handler discovery — no new
Wolverine features used), EF Core + Npgsql (existing — one new entity/table).
No new NuGet packages.

**Storage**: PostgreSQL (existing `orderdb` resource) — one new table,
`order_fulfillment_sagas`.

**Testing**: xUnit + AwesomeAssertions + Bogus (existing). Testcontainers for
the one new Postgres repository test.

**Target Platform**: .NET Aspire distributed app (existing `AppHost` — no new
resource; reuses the existing `postgres` resource).

**Project Type**: New ports and a new orchestrator inside the existing
`OrderSystem.Application` project; a new adapter class + EF migration inside
the existing `OrderSystem.Adapters.Postgres` project; DI registration changes
in `OrderSystem.Api`. No new top-level project.

**Performance Goals**: None specified; existing retry timing (RabbitMQ
TTL/DLX delay, 3 attempts) is unchanged by this feature.

**Constraints**: No new framework type inside `OrderSystem.Domain` or
`OrderSystem.Application` (Constitution Principle I). No new `OrderStatus`
values (spec Assumption). `POST /orders` keeps attempting payment
synchronously on the first attempt (spec Assumption, out of scope to change).

**Scale/Scope**: Touches 3 existing projects
(`OrderSystem.Application`, `OrderSystem.Adapters.Postgres`,
`OrderSystem.Api`) plus their matching test projects. Zero new top-level
projects. Zero changes to `OrderSystem.Domain`, the frontend, or
`OrderSystem.NotificationService`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable** — Consistent. `OrderSystem.Domain`
  needs zero changes: `Order.Cancel()`, `BeginPaymentProcessing()`,
  `ConfirmPayment()`, and `FailPayment()` already support everything this
  feature needs. The new ports (`IInventoryService`,
  `IOrderFulfillmentSagaRepository`) are defined and consumed only in
  `OrderSystem.Application`; only `OrderSystem.Adapters.Postgres` depends on
  EF Core for the new repository.
- **II. Ports Are Discovered From the Core** — Consistent. Both new ports are
  shaped by what the orchestrator needs (`TryReserveAsync`/`ReleaseAsync`;
  `GetByOrderIdAsync`/`AddAsync`/`UpdateAsync`), matching the existing
  `IPaymentGateway`/`IOrderRepository` shapes rather than any adapter SDK.
- **III. The Test Pyramid Mirrors the Architecture** — Consistent, with one
  deliberate scope decision: no new `OrderSystem.EndToEndTests` scenario is
  added for the exhausted-retry → Cancelled path. That path is 3 real
  RabbitMQ-delayed retries (~12s+) and its orchestration logic is already
  fully exercised at the Application.Tests (fakes) and
  Adapters.IntegrationTests (real Postgres) layers; per Principle III,
  EndToEndTests stays reserved for golden-path/previously-broken-in-prod
  scenarios, and the existing $500 decline-then-retry-succeeds test already
  covers that tier for this workflow. The existing E2E test must still pass
  unchanged.
- **IV. No Speculative Abstraction** — Consistent, and shaped the design
  directly: no real inventory/SKU count table (reservation always succeeds
  per spec Assumption — modeling scarcity is explicitly out of scope), no
  generic reusable "saga engine" or step-enum state machine (one concrete
  orchestrator for this one workflow), no new `OrderStatus` value.
- **V. Every State Transition Announces Itself** — Consistent, but requires
  care in implementation: the orchestrator's compensation path (exhausted
  retries → `Order.Cancel()`) MUST publish both `OrderCancelledEvent` and
  `OrderStatusChangedEvent`, mirroring `CancelOrderCommandHandler`'s existing
  dual-publish — otherwise `OrderSystem.NotificationService` silently stops
  reflecting that order's final state.
- **VI. Match the Project's Existing Idiom Before Reaching for a Framework's**
  — Directly applied, not in tension: a hand-rolled orchestrator was chosen
  over Wolverine's `Saga` base class specifically because the latter would
  leak a Wolverine type into `OrderSystem.Application` and require durable
  envelope/message persistence disproportionate to one thin workflow. See
  research.md for the full tradeoff.

No violations. Complexity Tracking is intentionally omitted below.

## Project Structure

### Documentation (this feature)

```text
specs/001-add-saga-orchestration-to/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

No `contracts/` directory: this feature adds no new HTTP endpoint and no new
Kafka/RabbitMQ message contract. Reservation and release happen synchronously
inside the existing command handlers via the new ports — there is nothing
external to document a contract for.

### Source Code (repository root)

```text
OrderSystem.Application/
├── Sagas/
│   ├── OrderFulfillmentSagaState.cs            # new — the persisted reservation-held flag, separate from Order
│   ├── IOrderFulfillmentSagaRepository.cs      # new port
│   └── OrderFulfillmentSagaOrchestrator.cs     # new — absorbs the reserve/charge/confirm/compensate flow
├── Inventory/
│   ├── IInventoryService.cs                    # new port
│   └── SimulatedInventoryService.cs            # new — always reserves successfully, mirrors SimulatedPaymentGateway
└── Commands/
    ├── CreateOrderCommandHandler.cs            # changed — delegates to the orchestrator's StartAsync
    ├── RetryPaymentCommandHandler.cs           # changed — delegates to the orchestrator's ContinueAsync
    └── CancelOrderCommandHandler.cs            # changed — calls the orchestrator's CompensateAsync before cancelling

OrderSystem.Adapters.Postgres/
├── OrderDbContext.cs                           # changed — new DbSet<OrderFulfillmentSagaState> + mapping
├── PostgresOrderFulfillmentSagaRepository.cs   # new
├── ServiceCollectionExtensions.cs              # changed — registers the new repository
└── Migrations/<timestamp>_AddOrderFulfillmentSaga.cs  # new EF migration

OrderSystem.Api/
└── Program.cs                                  # changed — registers IInventoryService and the orchestrator

OrderSystem.Application.Tests/
├── Sagas/OrderFulfillmentSagaOrchestratorTests.cs   # new
├── Fakes/FakeInventoryService.cs                    # new
├── Fakes/FakeOrderFulfillmentSagaRepository.cs       # new
└── Commands/*.cs                                     # changed — rewritten for the new orchestrator-backed flow

OrderSystem.Adapters.IntegrationTests/
└── Postgres/PostgresOrderFulfillmentSagaRepositoryTests.cs  # new — real Postgres round-trip, Testcontainers
```

Not touched: `OrderSystem.Domain`, `OrderSystem.Domain.Tests`,
`OrderSystem.NotificationService`, `order-system-frontend`,
`OrderSystem.EndToEndTests` (existing test must keep passing unchanged),
`OrderSystem.AppHost` (no new resource).

**Structure Decision**: Everything new lives inside the three existing
projects that already own this workflow (`Application`, `Adapters.Postgres`,
`Api`) rather than a new project, consistent with Principle IV — there's no
case here for the ceremony of a fourth project for two ports, one entity, and
one orchestrator class.

## Complexity Tracking

*No violations — table intentionally omitted.*
