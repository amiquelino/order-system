# Data Model: Upgrade .NET to 11

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## No entity, state machine or schema change

This feature changes the runtime and dependency versions only (FR-011). It
doesn't change:

- **`Order` and the `OrderStatus` state machine** (`OrderSystem.Domain`):
  no change to fields, invariants or transitions. Domain.Tests (21) passed
  unchanged on `net11.0` in the spike.
- **`OrderStatusChangedEvent` and the other message contracts**: no change
  to shape or serialization contract. SignalR moves to 11.0.0, so the E2E
  live-status check in CI confirms that the payload the frontend receives
  hasn't changed.
- **The PostgreSQL schema / EF Core model**: EF Core stays on the 10.x
  line (research Decision 2). The spike ran
  `dotnet ef migrations has-pending-model-changes` (startup project
  `OrderSystem.MigrationService`) on the .NET 11 runtime, and it reported
  *"No changes have been made to the model since the last migration."* No
  migration is added.

## The one record this feature produces: the dependency exception

FR-006 / SC-006 require every dependency that isn't on its .NET 11 line to be
recorded with a reason and an exit condition. There's exactly one, recorded in
[research.md](./research.md) (Decision 2) and in the project file it applies to:

| Field | Value |
|---|---|
| Packages | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Microsoft.EntityFrameworkCore.Design, dotnet-ef (tool), Npgsql.EntityFrameworkCore.PostgreSQL |
| Held at | EF Core / Relational / Design / dotnet-ef 10.0.15; Npgsql provider 10.0.3 |
| Reason | The provider has no stable 11.x (only 11.0.0-rc.3). Provider 10.0.3 requires EF Core `[10.0.4, 11.0.0)` |
| Exit condition | Npgsql.EntityFrameworkCore.PostgreSQL ships stable 11.0.0. Then move all four to 11.x together |

Dependencies that release independently of .NET (Wolverine, Confluent.Kafka,
RabbitMQ.Client, Testcontainers, test tooling) and Aspire (13.6.5) aren't
exceptions. They don't have a ".NET 11 line", and the spike confirmed they
work on .NET 11 (FR-004).
