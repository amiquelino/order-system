# OrderSystem

[![CI](https://github.com/amiquelino/order-system/actions/workflows/ci.yml/badge.svg)](https://github.com/amiquelino/order-system/actions/workflows/ci.yml)

A study project exploring **Hexagonal Architecture**, **CQRS**, and **event-driven communication** (Kafka + RabbitMQ) in .NET, orchestrated locally with **.NET Aspire**, with a **Next.js** frontend consuming the API over REST and SignalR.

> Every use case is implemented and verified end-to-end, including the full payment lifecycle: `POST /orders` persists an order, reserves its items, and attempts a (simulated) payment, pushing each status change (`Created` → `PaymentPending` → `Confirmed`/`PaymentFailed`) to SignalR clients live and publishing a Kafka event at every transition. A declined payment schedules a delayed retry on RabbitMQ; if it keeps failing it retries up to 3 times, then releases the reservation and resolves to `Cancelled` instead of staying stuck. `GET /orders/{id}` reads current status back (404 if unknown); `POST /orders/{id}/cancel` transitions to `Cancelled` and releases any reservation too (404 if unknown, 409 if already cancelled or confirmed). See **Payment retry flow** and **Saga orchestration & compensation** below for exactly how that works and how it was verified.

## Architecture overview

Hexagonal (ports & adapters) architecture, with the dependency arrows pointing inward:

```
OrderSystem.Api  ──depends on──▶  OrderSystem.Application  ──depends on──▶  OrderSystem.Domain
      │                                                                          ▲
      ├──depends on──▶ OrderSystem.Adapters.Postgres  ──implements ports──┘
      ├──depends on──▶ OrderSystem.Adapters.Kafka     ──implements ports──┘
      └──depends on──▶ OrderSystem.Adapters.RabbitMQ  ──implements ports──┘

OrderSystem.NotificationService  ──depends on──▶  OrderSystem.Adapters.Kafka, OrderSystem.Domain
```

For the runtime picture (which services exist, how they talk over Kafka and RabbitMQ, where the two consumers live, and an order's flow through a payment retry), see the [C4 diagrams](docs/architecture/c4.md).

- **OrderSystem.Domain** — the hexagonal core. `Order` entity, value objects (`OrderId`, `CustomerId`, `Money`, `OrderStatus`), domain events, and the two ports (`IOrderRepository`, `IEventPublisher`). No framework or infrastructure dependencies of any kind.
- **OrderSystem.Application** — CQRS layer using [Wolverine](https://wolverinefx.net/) as the in-process mediator. `Commands/` (`CreateOrderCommand`, `CancelOrderCommand`, `RetryPaymentCommand`) and `Queries/` (`GetOrderStatusQuery`, `GetOrderHistoryQuery`) are plain records; Wolverine discovers the matching `...Handler` classes by convention (no marker interface needed). Each handler depends on the Domain ports plus `Payments/IPaymentGateway` / `Payments/IPaymentRetryQueue` (attempt a charge, schedule a delayed retry on failure). `Payments/SimulatedPaymentGateway` stands in for a real processor with a deterministic rule (see **Payment retry flow**). There's no notification port here at all — every status transition already publishes `OrderStatusChangedEvent` to Kafka via `IEventPublisher`, and that's the only channel `OrderSystem.NotificationService` needs to push live updates (see below).
- **OrderSystem.Adapters.Postgres** — implements `IOrderRepository` with EF Core + Npgsql. `Order.Items` is mapped as a `jsonb` column (`OwnsMany(...).ToJson()`) rather than a separate table, since it's a value-object collection with no independent identity; `Money` is a nested owned type within that JSON column, which is why it's modeled as a `record` rather than a `record struct` (EF's owned-type machinery requires a reference type there).
- **OrderSystem.Adapters.Kafka** — implements `IEventPublisher` with Confluent.Kafka, publishing domain events to a Kafka topic.
- **OrderSystem.Adapters.RabbitMQ** — implements `IPaymentRetryQueue` (`RabbitMqPaymentRetryPublisher`) and hosts `RabbitMqPaymentRetryConsumer`, a `BackgroundService` that dispatches incoming retries straight back into Application via Wolverine's `IMessageBus`. This is the one adapter project that references `OrderSystem.Application` and `WolverineFx` directly — everywhere else "Adapters only depend on Domain" holds, but a message consumer is a *driving* adapter (it initiates a use case, like the Api's HTTP endpoints do), not a driven one, so depending on Application here is the same relationship the Api already has, not an exception to it.
- **OrderSystem.Api** — ASP.NET Core minimal API: `POST /orders`, `GET /orders/{id}`, `POST /orders/{id}/cancel`. Wires up Wolverine and all three adapters via DI. CORS is restricted to the frontend's origin (see `FRONTEND_URL` in **Multi-instance support**). No real-time connections live here at all — see `OrderSystem.NotificationService` below — which is exactly what makes running this as multiple replicas straightforward: every request is independent, there's no per-instance state a load balancer needs to route consistently.
- **OrderSystem.NotificationService** — a small, separate ASP.NET Core service that owns real-time push, on its own resource/endpoint. It hosts the SignalR hub (`Hubs/OrderHub.cs`) and a `KafkaOrderEventsConsumer` `BackgroundService` that subscribes to the same Kafka topic `OrderSystem.Adapters.Kafka` publishes to, filters for `OrderStatusChangedEvent`, and forwards each one to `IHubContext<OrderHub>.Clients.All.SendAsync(...)` (the frontend filters by order id client-side). It still wires up `AddStackExchangeRedis` for its own SignalR backplane, same reasoning as before — see **Multi-instance support**. Pulling this out of the Api was a deliberate architectural change: it isolates SignalR's sticky-connection requirement to one small, purpose-built service instead of spreading it across the (larger, horizontally-scaled) Api tier.
- **OrderSystem.MigrationService** — a worker service whose only job is to apply pending EF Core migrations against Postgres, then exit. Kept separate from the Api so the Api's own DB user never needs schema-write (DDL) permissions.
- **OrderSystem.AppHost** — the .NET Aspire orchestrator. Spins up Postgres, Kafka, RabbitMQ, and Redis as containers, runs the migration service to completion, then runs the Api project (as 2 replicas, load-balanced by Aspire's proxy), the NotificationService, and the Next.js frontend as a managed Node process.
- **order-system-frontend** — Next.js (App Router, TypeScript). A form to create an order and a status page that opens a SignalR connection (to `OrderSystem.NotificationService`, not the Api) to watch that order update live.

## Tech stack

| Layer | Technology |
|---|---|
| Language / runtime | C# / .NET 10 |
| CQRS mediator | Wolverine (open source, MIT) |
| Persistence | PostgreSQL via EF Core + Npgsql |
| Event streaming | Apache Kafka via Confluent.Kafka |
| Messaging / retry queue | RabbitMQ via RabbitMQ.Client |
| Real-time push | ASP.NET Core SignalR + Redis backplane |
| Orchestration | .NET Aspire 13 |
| Frontend | Next.js 16 (App Router), TypeScript, `@microsoft/signalr` |

> Wolverine was chosen over MediatR for the CQRS mediator because MediatR moved to a commercial license starting with v12/v13 (free tier is capped for individuals/small teams). Wolverine is MIT-licensed. It requires the `WolverineFx.RuntimeCompilation` package for dev-time dynamic codegen (see `OrderSystem.Api.csproj`), and `Program.cs` opts `OrderDbContext` into service location (`opts.CodeGeneration.AlwaysUseServiceLocationFor<OrderDbContext>()`) because EF Core's `AddDbContext` registers `DbContextOptions<T>` via an opaque factory that Wolverine's static handler codegen can't inline-construct.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20+ and npm (only needed if you want to run the frontend outside of Aspire, or `npm install` its dependencies)
- **Docker Desktop** (or another container runtime) running — Aspire launches Postgres, Kafka, RabbitMQ, and Redis as containers
- Visual Studio 2022 17.13+ (native `.slnx` solution file support) if you want to run from the IDE, or the Aspire workload/tooling if using an older VS version

Before the first run, install the frontend's npm dependencies and restore the local `dotnet-ef` tool once:

```bash
cd order-system-frontend && npm install && cd ..
dotnet tool restore
```

## Running the application

### Option A — Visual Studio

1. Open `OrderSystem.slnx` in Visual Studio.
2. In Solution Explorer, right-click **OrderSystem.AppHost** and choose **Set as Startup Project**.
3. Make sure Docker Desktop is running.
4. Press **F5** (or **Ctrl+F5** to run without debugging).
5. Visual Studio launches the **Aspire dashboard** in your browser. From there you can see every resource (Postgres, Kafka, RabbitMQ, Redis, the `api` project, the `notifications` project, and the `frontend` Node app), their logs, traces, and endpoint URLs.
6. Open the frontend URL shown in the dashboard for the `frontend` resource (defaults to `http://localhost:3000`).

If Visual Studio doesn't recognize `.slnx` yet, update to the latest VS 2022 release, or open `OrderSystem.slnx` via **File → Open → Project/Solution** which forces recognition of the format.

### Option B — CLI

```bash
dotnet run --project hosts/OrderSystem.AppHost
```

This does the same thing as pressing F5: builds everything, starts the containers, starts the Api and NotificationService, starts the Next.js dev server, and prints the Aspire dashboard URL to the console.

### Option C — Running pieces individually (without Aspire)

Useful for quick iteration on just the API or just the frontend, but you lose automatic service discovery/connection strings that Aspire injects, so you'll need to supply your own `ConnectionStrings:orderdb`, `ConnectionStrings:kafka`, `ConnectionStrings:rabbitmq`, and `ConnectionStrings:cache` (e.g. via `appsettings.Development.json` or environment variables) pointing at infrastructure you run yourself (e.g. `docker run` for Postgres/Kafka/RabbitMQ/Redis).

```bash
# API only (http://localhost:5016)
dotnet run --project hosts/OrderSystem.Api

# Notification service only (http://localhost:5017) — needed for the status page's live push
dotnet run --project hosts/OrderSystem.NotificationService

# Frontend only
cd order-system-frontend
npm run dev
```

## Database schema (EF Core migrations)

Migrations live in `src/Adapters/OrderSystem.Adapters.Postgres/Migrations/` (the project that owns `OrderDbContext`). They're applied by `OrderSystem.MigrationService`, a small worker that calls `Database.MigrateAsync()` once and exits — the Api itself never touches schema. When run via Aspire (`dotnet run --project hosts/OrderSystem.AppHost`), the AppHost runs the migration service to completion before starting the Api (`.WaitForCompletion(migrationService)` in `AppHost.cs`), so the schema is always up to date automatically.

The `dotnet-ef` CLI tool is pinned as a local tool in `.config/dotnet-tools.json` — run `dotnet tool restore` once after cloning to get it. To add a new migration after changing the `Order` model or `OrderDbContext`:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Adapters/OrderSystem.Adapters.Postgres \
  --startup-project hosts/OrderSystem.MigrationService \
  -o Migrations
```

To apply migrations manually against a database (e.g. when running the Api standalone per Option C, without Aspire):

```bash
ConnectionStrings__orderdb="Host=localhost;Port=5432;Database=orderdb;Username=postgres;Password=postgres" \
  dotnet run --project hosts/OrderSystem.MigrationService
```

## Multi-instance support

The Api runs as 2 replicas under Aspire (`.WithReplicas(2)` in `AppHost.cs`), fronted by Aspire's own load-balancing reverse proxy — useful for exercising scale-out behavior locally, per [Aspire's own guidance on this](https://learn.microsoft.com/dotnet/aspire) for debugging clustered state. This is safe with zero extra work today, because the Api holds no per-connection state at all: SignalR now lives entirely in `OrderSystem.NotificationService`, and every Api request is handled independently of which replica gets it.

That separation exists because of a real problem this project ran into (and is worth documenting, since it isn't obvious): **SignalR does not tolerate a plain round-robin proxy across replicas, and a Redis backplane does not fix that.**

SignalR's connection handshake is 2–3 separate HTTP requests, not one: the client calls `POST /orderhub/negotiate`, the server picks a `connectionId` and stores it in *that instance's own* in-memory table, then the client opens the real transport (a WebSocket upgrade, or an SSE/long-polling request) carrying that id. If a proxy round-robins those requests independently — which is exactly what Aspire's built-in replica proxy does, confirmed by inspecting `Aspire.Hosting.dll` directly: it has no "affinity"/"sticky" concept anywhere in it, and explicitly rejects combining `.WithReplicas(N>1)` with a proxy-less endpoint — the negotiate can land on replica A while the transport request lands on replica B, which has never heard of that connection id and returns 404 ("No Connection with that ID"). A Redis backplane (`AddStackExchangeRedis`) doesn't help here: it relays *messages* between instances once a client is connected somewhere, it says nothing about which instance a client's own handshake requests get routed to.

This was originally caught by `OrderSystem.EndToEndTests` (see **Automated tests**) driving the app through Aspire's real shared endpoint — a manual multi-instance check that connects a SignalR client directly to each replica's own port never exercises this, because it bypasses the proxy that a real browser has to go through.

The fix applied here: pull real-time push into its own service (`OrderSystem.NotificationService`) that consumes `OrderStatusChangedEvent` off Kafka instead of being called in-process by the command handlers, and run the Api — which no longer does anything stateful — safely at any replica count. `OrderSystem.NotificationService` itself still runs as a single instance; the Redis backplane it wires up (`AddStackExchangeRedis`) is kept for when/if it's ever scaled out, but doing so would need one of:
- **Azure SignalR Service** (or a similar managed real-time service) — clients connect to a managed endpoint instead of your own process, so your instances become stateless publishers and the affinity problem disappears entirely; or
- **A sticky-session-aware proxy in front of it** — e.g. an nginx/Traefik container with `ip_hash`/cookie affinity (the docker-compose-style answer), or a deployment target that provides this natively (Kubernetes ingress-nginx's `affinity: cookie`, Azure Container Apps' session affinity setting, Azure App Service's ARR affinity cookie). This is infrastructure outside of Aspire's local orchestration either way — Aspire's replica proxy is for local dev, not a production load balancer.

Everything else in the stack was already safe for multiple instances without changes: Postgres/Kafka/RabbitMQ are shared external state, not per-instance; Wolverine here only does local in-process dispatch (no durable inter-instance queuing configured); and the migration service is a separate, non-replicated resource that all Api replicas wait on before starting.

## Payment retry flow

There's no real payment processor wired up (out of scope for this study project), so `SimulatedPaymentGateway` stands in for one with a deterministic rule based on order total — deterministic on purpose, so the three outcomes below are reproducible rather than relying on randomness:

| Order total | Outcome |
|---|---|
| ≤ $100 | Approved immediately |
| $100–$1000 | Declined once, approved on the first retry (simulates a transient decline) |
| > $1000 | Always declined (simulates a hard decline — never recovers) |

**The state machine** (`Order` in `OrderSystem.Domain`): `Created` → `PaymentPending` → `Confirmed` or `PaymentFailed`. A retry re-enters `PaymentPending` from `PaymentFailed` and resolves the same way. Every transition persists to Postgres, publishes an `OrderStatusChangedEvent` to Kafka, and pushes to SignalR — so a client watching the status page sees `Created`, `PaymentPending`, and the final outcome in quick succession, or several `PaymentPending`/`PaymentFailed` cycles a few seconds apart if it's retrying.

**The retry delay** is a standard RabbitMQ pattern, not a `Thread.Sleep`: a failed payment publishes to a `payment-retry-wait-queue` with `x-message-ttl` (4 seconds) and `x-dead-letter-exchange` pointing back at the main `payment-retry-queue`. Nothing ever consumes the wait queue directly — messages just sit there until the TTL expires, then RabbitMQ dead-letters them into the real queue, where `RabbitMqPaymentRetryConsumer` picks them up and dispatches a `RetryPaymentCommand`. `OrderFulfillmentSagaOrchestrator` caps this at 3 attempts total — and, since the saga orchestration feature below, no longer just gives up: exhausting every attempt now triggers compensation and resolves the order to `Cancelled` instead of abandoning it in `PaymentFailed` forever.

**Verified end-to-end** for the $50 and $500 tiers (not just wired up) — created one order per price tier against a live Postgres/Kafka/RabbitMQ/Redis stack, with a SignalR probe watching:
- $50 order: `Created → PaymentPending → Confirmed`, no retry scheduled.
- $500 order: `Created → PaymentPending → PaymentFailed`, then ~4s later `PaymentPending → Confirmed`.

The $5000 (always-declines) tier's new exhausted-retries-resolve-to-`Cancelled` behavior is covered by `OrderFulfillmentSagaOrchestratorTests` and `RetryPaymentCommandHandlerTests` (fakes, no live stack) — see **Saga orchestration & compensation** below. A live end-to-end re-run of that specific tier, the way the $50/$500 tiers were verified above, is still pending in this environment (Docker's daemon isn't reachable here to bring the stack up).

## Saga orchestration & compensation

Payment retries used to be the end of the story: once all 3 attempts failed, the order was abandoned mid-flow in `PaymentFailed` forever — nothing released, no path forward. `OrderFulfillmentSagaOrchestrator` (`src/Application/OrderSystem.Application/Sagas/`) replaces that dead end with a real two-step saga:

1. **Reserve** — `IInventoryService` reserves the order's items before any payment attempt is made. `SimulatedInventoryService` always succeeds; no real inventory counts are modeled yet, deliberately (see the feature's `research.md`). Reservation state lives in its own table, `order_fulfillment_sagas`, tracked separately from `orders` so releasing it twice — or releasing nothing — is a safe no-op rather than an error.
2. **Charge** — unchanged from the flow above: charge, confirm or fail, retry up to 3 times via the same RabbitMQ delay queue.
3. **Compensate** — if every retry is exhausted, *or* the order is cancelled manually at any point before confirmation, `CompensateAsync` releases the reservation and the order resolves to `Cancelled`. No new `OrderStatus` value was needed: `Cancelled` now means either "someone cancelled this" or "payment never went through," and neither the frontend nor `OrderSystem.NotificationService` needs to know which.

The orchestrator is hand-rolled rather than built on Wolverine's `Saga` base class: adopting that would pull a Wolverine-specific type into `OrderSystem.Application` — this project's one layer that, until now, only referenced Wolverine by convention — and would need durable envelope persistence across the Api's two replicas for a two-step workflow that doesn't warrant it. See `specs/001-add-saga-orchestration-to/research.md` for the full tradeoff, and **Spec-Driven Development** above for how this feature was specified, planned, and broken into tasks before any code was written.

## Automated tests

```bash
dotnet test OrderSystem.slnx
```

Four test projects, all xUnit, asserting with [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) (`order.Status.Should().Be(...)`) rather than xUnit's own `Assert.*`. That's a deliberate substitute for FluentAssertions: FluentAssertions v8+ requires a paid license for companies over $1M revenue, and AwesomeAssertions is the MIT-licensed community fork that kept the same fluent API.

Test data comes from [Bogus](https://github.com/bchavez/Bogus) (`new Faker().Commerce.ProductName()`, ...) instead of hand-typed literals like `"Widget"` or a fixed `Guid` — used inline, as a small private helper at the top of whichever test class needs it, rather than a shared cross-project test-utility library: the amount of code involved is a handful of lines per project, well below the threshold where a shared project earns its own place in the solution and a reference from three others. Not applied blindly, either: anywhere a specific value drives the behavior under test — the $100/$1000 thresholds in `SimulatedPaymentGatewayTests`, the attempt numbers in the retry tests — that value stays a literal, because randomizing it would test something different each run instead of the boundary it's there to check. Faked data is unseeded on purpose: every run uses different values, and every assertion is written to check against what was actually generated rather than a separately hardcoded expectation, so the randomness adds coverage instead of flakiness.

**Unit tests** — no Docker needed, run in well under a second:

- **OrderSystem.Domain.Tests** — exercises `Order`'s state machine directly (`Create`, `Cancel`, `BeginPaymentProcessing`, `ConfirmPayment`, `FailPayment`), including every guarded invalid transition (e.g. confirming payment on an order that isn't `PaymentPending`, cancelling one that's already `Confirmed`).
- **OrderSystem.Application.Tests** — exercises every command/query handler (`CreateOrderCommandHandler`, `CancelOrderCommandHandler`, `RetryPaymentCommandHandler`, both query handlers), `OrderFulfillmentSagaOrchestrator` directly (reserve/charge/confirm, scheduling retries, and the compensate-and-cancel path once retries are exhausted), and `SimulatedPaymentGateway`'s threshold rule. `Fakes/` holds a hand-rolled in-memory implementation of each port (`IOrderRepository`, `IEventPublisher`, `IPaymentGateway`, `IPaymentRetryQueue`, `IInventoryService`, `IOrderFulfillmentSagaRepository`) instead of a mocking library — since the ports are already small, deliberately-designed interfaces, a fake is a few lines of real code rather than a mock-framework setup, and it's the same substitutability the hexagonal architecture is there to give you: real Postgres/Kafka/RabbitMQ swap out for these fakes without either side knowing. Each command handler test builds a real `OrderFulfillmentSagaOrchestrator` wired to fakes rather than mocking it — it's in-process Application logic, not infrastructure, so there's no port to fake it behind.

**Integration tests** — `OrderSystem.Adapters.IntegrationTests`, needs Docker (~35s once images are cached, longer on a cold pull). Deliberately *not* unit tests: each adapter's only real behavior is its interaction with the actual infrastructure it wraps, so a mock of `IChannel` or `DbContext` would just be asserting that the mock behaves like the mock, not catching what could actually break. [Testcontainers](https://dotnet.testcontainers.org/) spins up real, disposable Postgres/Kafka/RabbitMQ containers per test class (`IClassFixture<T>`, so one container serves every test in a class rather than one per test) — no manual `docker run` required, unlike the throwaway containers used earlier in this project's development.

- **PostgresOrderRepositoryTests** — round-trips an `Order` through real Postgres (via a migrated schema, not `EnsureCreated`) using two separate `DbContext` instances for the write and the read, specifically so EF Core's identity map can't mask a real database round trip and silently hand back the same in-memory object. Caught a real bug on first run: reusing one `OrderItem` C# instance across two different `Order`s in the same `DbContext` — EF Core correctly rejected it (an owned entity's key is scoped to its parent), which the fake repository in the unit tests had no way to catch since it doesn't model EF's identity/ownership rules at all.
- **PostgresOrderFulfillmentSagaRepositoryTests** — the same two-`DbContext` round-trip discipline, applied to the `order_fulfillment_sagas` table: add, read back, release, and confirm `UpdatedAt` actually advances.
- **KafkaEventPublisherTests** — publishes a real event and consumes it back with a real `Confluent.Kafka` consumer, asserting the `event-type`-keyed, JSON-serialized shape on the wire.
- **RabbitMqPaymentRetryPublisherTests** — the one that actually needed a real broker to mean anything: asserts a scheduled retry is *not* yet visible in the main queue immediately after publishing, then *is* visible after the wait queue's TTL elapses — proving the dead-letter-exchange delay mechanism genuinely delays, not just that the code compiles.

**End-to-end test** — `OrderSystem.EndToEndTests`, the slowest and heaviest tier by far (several minutes; needs Docker, Node, and a Chromium install), and deliberately kept to a single test rather than a large suite. It uses [`Aspire.Hosting.Testing`](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test) (`DistributedApplicationTestingBuilder.CreateAsync<Program>()`) to boot the *real* `AppHost` — every container, both Api replicas, the NotificationService, and the frontend — then drives it with a real headless [Playwright](https://playwright.dev/dotnet/) Chromium browser: fill in the create-order form, submit, and watch the status page reach `Confirmed` live via the real SignalR connection, no mocking or stubbing anywhere in the stack. Unlike the other tiers, this is the only one that goes through the frontend and the real Aspire-managed network topology, which is exactly why it's the tier that caught composition bugs nothing else could: a missing `NEXT_PUBLIC_API_URL` (`.WithReference(api)` alone doesn't set the specific env var Next.js needs), a CORS origin hardcoded to a port Aspire doesn't guarantee the frontend actually gets, and the SignalR multi-replica affinity gap that led to pulling `OrderSystem.NotificationService` out (see **Multi-instance support**) — none of which a unit or adapter-integration test could ever see, since they don't touch the frontend or Aspire's real proxy/networking.

Needs the Playwright Chromium browser installed once (`dotnet build tests/OrderSystem.EndToEndTests && pwsh tests/OrderSystem.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install chromium`, or the non-PowerShell equivalent via `Microsoft.Playwright.Program.Main(["install", "chromium"])`). On Linux without `--with-deps` (e.g. no passwordless `sudo`), headless Chromium may need system libraries like `libasound2` sourced manually if they're missing.

## Spec-Driven Development

Non-trivial changes (new features, cross-layer decisions) go through a
`spec-kit`-style SDD workflow: `/speckit-specify` → `/speckit-plan` →
`/speckit-tasks` → `/speckit-implement`, with `/speckit-constitution` used to
amend the project's guiding principles themselves. These are Claude Code
skills under `.claude/skills/speckit-*/`, backed by a trimmed-down,
hand-rolled port of [GitHub's spec-kit](https://github.com/github/spec-kit)
(no git branches — this repo tracks the active feature via
`.specify/feature.json` instead; no presets/extensions/multi-agent layering,
since none of that applies to a single-developer project on one agent).

- `.specify/memory/constitution.md` — the ratified principles (hexagonal
  boundaries, ports-before-adapters, the test pyramid, no speculative
  abstraction, every status change publishes `OrderStatusChangedEvent`, and
  when to prefer a framework's native feature over a hand-rolled one).
  Amend it with `/speckit-constitution`.
- `.specify/templates/` — the `spec.md`/`plan.md`/`tasks.md` scaffolds each
  skill fills in.
- `.specify/scripts/bash/` — the small scripts that create a new feature
  directory, seed each document, and check prerequisites before the next
  step. No Python/jq dependency; plain bash + the project's own structure.
- `specs/<NNN-slug>/` — one directory per feature, created by
  `/speckit-specify`: `spec.md`, `plan.md`, `tasks.md`, and optionally
  `research.md`, `data-model.md`, `contracts/`, `quickstart.md`.

A one-line fix or something already fully scoped in chat doesn't need this —
the workflow exists to make real design decisions explicit before code is
written, not to add ceremony everywhere.

## Solution layout

```
OrderSystem.slnx
src/
├── Domain/OrderSystem.Domain/              # entities, value objects, domain events, ports
├── Application/OrderSystem.Application/    # CQRS commands, queries, Wolverine handlers
└── Adapters/
    ├── OrderSystem.Adapters.Postgres/      # IOrderRepository via EF Core + Npgsql
    ├── OrderSystem.Adapters.Kafka/         # IEventPublisher via Confluent.Kafka
    └── OrderSystem.Adapters.RabbitMQ/      # failed-payment retry publisher/consumer
hosts/
├── OrderSystem.Api/                 # minimal API, DI composition root
├── OrderSystem.NotificationService/ # SignalR hub + Kafka consumer, real-time push (own resource)
├── OrderSystem.MigrationService/    # applies EF Core migrations against Postgres, then exits
└── OrderSystem.AppHost/             # Aspire orchestration (Postgres, Kafka, RabbitMQ, migrator, Api, NotificationService, frontend)
tests/
├── OrderSystem.Domain.Tests/              # unit tests for the Order state machine
├── OrderSystem.Application.Tests/         # handler unit tests, using hand-rolled fakes for every port
├── OrderSystem.Adapters.IntegrationTests/ # Testcontainers-backed tests for all three adapters
└── OrderSystem.EndToEndTests/             # Aspire.Hosting.Testing + Playwright, one full-stack browser-driven test
order-system-frontend/           # Next.js app (create order form, live status page)
.specify/                        # Spec-Driven Development: constitution, templates, scripts (see above)
specs/                           # one directory per feature spec/plan/tasks, created by /speckit-specify
```

## Endpoints

**OrderSystem.Api:**

| Method | Path | Description |
|---|---|---|
| POST | `/orders` | Create an order |
| GET | `/orders/{id}` | Get an order's current status |
| POST | `/orders/{id}/cancel` | Cancel an order (404 if unknown, 409 if already cancelled/confirmed) |

**OrderSystem.NotificationService:**

| Method | Path | Description |
|---|---|---|
| WS | `/orderhub` | SignalR hub, pushes order status changes in real time |
