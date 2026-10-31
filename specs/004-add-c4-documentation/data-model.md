# Data Model: Add C4 Documentation

There is no runtime data. The "model" is the inventory of elements and
relationships the diagrams must show. This is the checklist the document is
authored from and reviewed against, so FR-003 (nothing missing, nothing
invented) is mechanically checkable.

## Views

| # | View | Type | Subject | Spec |
|---|---|---|---|---|
| 1 | System context | `C4Context` | OrderSystem | US1 |
| 2 | Containers | `C4Container` | OrderSystem | US1 |
| 3 | Api components | `C4Component` | Api | US2 |
| 4 | NotificationService components | `C4Component` | NotificationService | US2 |
| 5 | Order flow with retry | `C4Dynamic` | Containers | US3 |

## Elements

### Level 1

| Element | Kind | Note |
|---|---|---|
| Customer | Person | Creates orders, watches status |
| OrderSystem | System | |
| Payment gateway | External system | Simulated; lives inside the Api (FR-006) |
| Inventory service | External system | Simulated; lives inside the Api (FR-006) |

### Level 2 (must equal the AppHost resources, plus the person)

| Element | AppHost resource | Kind | Source of truth |
|---|---|---|---|
| Frontend | `frontend` | Container | `order-system-frontend/` |
| Api | `api` (2 replicas) | Container | `hosts/OrderSystem.Api/` |
| NotificationService | `notifications` | Container | `hosts/OrderSystem.NotificationService/` |
| MigrationService | `migrationservice` | Container | `hosts/OrderSystem.MigrationService/` |
| PostgreSQL | `postgres` / `orderdb` | Database | AppHost |
| Kafka | `kafka` | Queue (topic `order-events`) | AppHost |
| RabbitMQ | `rabbitmq` | Queue (wait + retry) | AppHost |
| Redis | `cache` | Store (SignalR backplane) | AppHost |

Dev-only UIs the AppHost attaches (`WithPgWeb`, `WithKafkaUI`,
`WithManagementPlugin`) are tooling, not part of the system's design, and
are left off the diagram with a one-line note.

### Level 3: Api

| Component | Real location | Hexagonal role |
|---|---|---|
| Minimal API endpoints | `OrderSystem.Api/Program.cs` | Driving adapter |
| Wolverine message bus | `IMessageBus` | Mediator |
| Command and query handlers | `OrderSystem.Application/Commands`, `/Queries` | Application |
| Order fulfillment saga orchestrator | `OrderSystem.Application/Sagas` | Application |
| Simulated payment gateway and inventory | `OrderSystem.Application/Payments`, `/Inventory` | Application (stand-ins for external systems) |
| Domain (Order, events, ports) | `OrderSystem.Domain` | Core |
| Postgres adapter | `OrderSystem.Adapters.Postgres` | Driven adapter |
| Kafka adapter (event publisher) | `OrderSystem.Adapters.Kafka` | Driven adapter |
| RabbitMQ retry publisher | `OrderSystem.Adapters.RabbitMQ` | Driven adapter |
| RabbitMQ retry consumer | `OrderSystem.Adapters.RabbitMQ` | Driving adapter (FR-004) |

### Level 3: NotificationService

| Component | Real location |
|---|---|
| Kafka order-events consumer (`group notification-service`) | `Consumers/KafkaOrderEventsConsumer.cs` (FR-004) |
| Order hub (SignalR) | `Hubs/OrderHub.cs` |

## Relationships that must appear (container level)

| From | To | Label (purpose / style) |
|---|---|---|
| Customer | Frontend | Uses (HTTPS) |
| Frontend | Api | Create, read, cancel orders (REST) |
| Frontend | NotificationService | Receives live status (SignalR) |
| Api | PostgreSQL | Reads and writes orders and saga state |
| Api | Kafka | Publishes order events |
| Api | RabbitMQ | Schedules delayed payment retry |
| RabbitMQ | Api | Delivers retry after delay (consumer) |
| Kafka | NotificationService | Streams order events (consumer) |
| NotificationService | Redis | SignalR backplane |
| MigrationService | PostgreSQL | Applies schema, then exits |

## Rules

- A container or component appears **once** per view. Replicas are stated in
  the label, never drawn twice.
- Every relationship has a label (FR-005).
- A dependency arrow between layers points inward (adapter to core). The
  RabbitMQ retry consumer's reference to Application is the one documented
  exception and is labelled as a driving adapter.
- The dynamic view's steps are numbered and must include: persistence,
  event publication for each status change, retry scheduling, retry
  delivery, final outcome, notification push (FR-002).

## Drawing notes (recorded during implementation)

- The two RabbitMQ rows above (Api to RabbitMQ, RabbitMQ to Api) are drawn as
  **one two-way arrow** in the container view: two opposite arrows between
  the same pair of boxes stacked their labels on top of each other.
- Views 2 to 4 are styled flowcharts and view 5 is a sequence diagram, not
  Mermaid `C4Container` / `C4Component` / `C4Dynamic` blocks; only view 1 uses
  `C4Context`. See research.md Decision 3 (fallback) and the captions in
  `docs/architecture/c4.md`.
- Retry count: the saga allows the first charge plus 3 retries (4 charges
  at most), so the views say "3 retries" rather than "3 attempts".
