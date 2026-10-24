# OrderSystem Constitution

## Core Principles

### I. Hexagonal Boundaries Are Inviolable

`OrderSystem.Domain` has zero dependencies on frameworks, infrastructure, or other
projects — no EF Core, no Wolverine, no Confluent.Kafka, nothing that isn't plain
C#. `OrderSystem.Application` depends only on `OrderSystem.Domain` and defines
every capability it needs from the outside world as a port (an interface) rather
than reaching for a concrete technology. Adapters (`OrderSystem.Adapters.*`,
`OrderSystem.Api`, `OrderSystem.NotificationService`) implement those ports and
are the only projects allowed to depend on infrastructure SDKs.

A change that makes Domain or Application reference a package whose job is
talking to a database, message broker, or network is a constitution violation,
not a style nit — it is exactly the coupling this project exists to study how to
avoid.

### II. Ports Are Discovered From the Core, Not the Adapter

A new capability starts as an interface shaped by what the Domain or Application
layer needs (e.g. `IPaymentGateway`, `IOrderRepository`), never as a thin wrapper
generated from a specific adapter's SDK shape. If a port's method signatures
leak a specific technology's vocabulary (a Kafka `ConsumeResult`, an EF
`DbContext`), the port was designed backwards.

### III. The Test Pyramid Mirrors the Architecture

Each layer gets the kind of test that matches what it can break:

- **Domain.Tests** — pure, no mocks, no fakes beyond what the Domain itself
  defines. Tests the state machine and invariants in isolation.
- **Application.Tests** — one fake per port, no real infrastructure. Tests
  orchestration logic (what calls what, in what order, on which outcome).
- **Adapters.IntegrationTests** — real infrastructure via Testcontainers. Tests
  that an adapter actually satisfies its port's contract against the real
  technology.
- **EndToEndTests** — the full Aspire distributed application driven through a
  real browser (Playwright). Used sparingly, for golden-path and
  previously-broken-in-production-shaped scenarios only — not a substitute for
  the layers below it.

A bug fix or new feature that only adds tests at one layer when the defect could
recur at another is incomplete.

### IV. No Speculative Abstraction

Don't add a port, a configuration flag, an `OrderStatus` value, or an extension
point for a scenario the project doesn't have yet. Three similar lines beat a
premature interface. If a "future-proofing" change can't point to a concrete,
already-known upcoming requirement, cut it. (This project is a study of
patterns under real constraints, not a framework — speculative generality
defeats the point.)

### V. Every State Transition Announces Itself

Any command handler that changes `Order.Status` MUST publish
`OrderStatusChangedEvent` on the same transaction boundary as the status change.
`OrderSystem.NotificationService` — and any future subscriber — depends on this
event being the single, reliable signal for "the order's status changed,
go tell someone." A handler that mutates status without publishing it is a
silent regression: the live status page simply stops updating, with no
exception anywhere to point at.

### VI. Match the Project's Existing Idiom Before Reaching for a Framework's

When a framework already in use (Wolverine, EF Core, Aspire) offers a
built-in feature that would solve a problem (e.g. Wolverine's `Saga` base
class, EF Core's change tracking for process state), prefer it only when
adopting it doesn't leak a new framework type into `Domain` or `Application`
(Principle I) or pull in infrastructure (durable envelope storage, an outbox)
disproportionate to the problem being solved. When a hand-rolled solution
consistent with the project's existing ports-and-adapters shape is simpler and
equally correct, prefer it, and record the tradeoff explicitly — this project's
value is in seeing both paths reasoned about, not in defaulting to whichever
is less code today.

## Workflow

This repo uses Spec-Driven Development for anything bigger than a small,
obviously-correct change: `/speckit-specify` → `/speckit-plan` →
`/speckit-tasks` → `/speckit-implement`, with `/speckit-constitution` used to
amend this document itself. Specs live under `specs/<NNN-slug>/`. A typo fix,
a one-line config change, or a change already fully scoped in chat does not
need a spec — use judgment; the workflow exists to make non-trivial design
decisions explicit and reviewable before code is written, not to add
ceremony to everything.

Every plan produced by `/speckit-plan` MUST include a Constitution Check that
names which principles above are in tension with the proposed design (if any)
and either resolves the tension or documents it as an explicit, justified
exception in the plan's Complexity Tracking section.

## Governance

This constitution supersedes ad hoc conventions when the two disagree. Amend
it with `/speckit-constitution`, passing the principle(s) to add, remove, or
reword — amendments are a normal part of the project learning what it actually
needs, not a heavyweight process.

**Versioning**: semantic versioning against this document.
- **MAJOR** — a principle is removed or redefined in a backward-incompatible
  way.
- **MINOR** — a new principle or materially expanded section is added.
- **PATCH** — wording, clarification, or typo fixes with no semantic change.

**Version**: 1.0.0 | **Ratified**: 2026-10-24 | **Last Amended**: 2026-10-24
