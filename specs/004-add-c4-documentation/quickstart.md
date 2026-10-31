# Quickstart: Validate the C4 Documentation

Run after `docs/architecture/c4.md` is written and README is linked. No
containers or app run are needed.

## Prerequisites

- Node.js 20+ (already required by the repo for the frontend)
- The repository checked out on the working branch

## 1. Every diagram parses and renders

Extract each fenced `mermaid` block into its own file in a scratch
directory (not the repo), then render each:

```bash
npx -y @mermaid-js/mermaid-cli -i <scratch>/view-1.mmd -o <scratch>/view-1.svg
```

**Expected**: exit code 0 for all five views, and each SVG opens showing
every element with its label un-clipped. If a label is clipped, apply the
mitigation in research.md Decision 3.

## 2. Container view matches the AppHost (FR-003, SC-002)

Compare the container view to the `Add*` calls in
`hosts/OrderSystem.AppHost/AppHost.cs`.

**Expected**: each of `postgres`/`orderdb`, `kafka`, `rabbitmq`, `cache`,
`migrationservice`, `api`, `notifications`, `frontend` appears exactly once;
nothing else does (the PgWeb, Kafka UI, and RabbitMQ management helpers are
mentioned in a note, not drawn).

## 3. Component views match the code (FR-004)

- Api view contains the saga orchestrator, the Postgres, Kafka, and RabbitMQ
  publisher adapters, and the RabbitMQ retry consumer.
- NotificationService view contains the Kafka consumer and `OrderHub`.

**Expected**: `grep -r "class .*Consumer" src hosts` finds exactly the two
consumers the views show, and each is placed in the process that hosts it.

## 4. Relationships are labelled and consistent with README (FR-005, FR-010)

**Expected**: no arrow without a label; read README's "Architecture
overview", "Payment retry flow", and "Saga orchestration & compensation"
sections and find nothing the views contradict.

## 5. Discoverability and GitHub rendering (FR-007, FR-008, SC-003, SC-004)

1. README's "Architecture overview" links to `docs/architecture/c4.md`.
2. Push the branch and open `docs/architecture/c4.md` on GitHub.

**Expected**: the file is reachable from the front page in at most 2 clicks,
and every diagram renders as a picture, not a code block. If a block shows
as raw text on GitHub but rendered locally, treat it as a GitHub-specific
Mermaid version difference and simplify that block.

## 6. Spot-check for a newcomer (SC-001, SC-005)

Ask someone unfamiliar with the project to list the deployable pieces and
brokers from the container view, and to point at the saga orchestrator and
both consumers in the component views.

**Expected**: all answered correctly in under 5 minutes.
