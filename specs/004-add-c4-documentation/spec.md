# Feature Specification: Add C4 Documentation

**Feature Directory**: `specs/004-add-c4-documentation/`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "add c4 documentation"

## Problem

The architecture of OrderSystem is currently described only as prose and
ASCII sketches in README's "Architecture overview". That text explains the
hexagonal layering well, but it does not show the thing a newcomer most
wants to see first: which runtime pieces exist (Api, NotificationService,
MigrationService, frontend, and the four infrastructure services), how they
talk to each other, and how an order flows through them. In particular, the
two message consumers (the Kafka one that feeds live notifications and the
RabbitMQ one that drives payment retries) and the fact that the Api
produces *and* consumes its own retry queue are easy to miss in prose.

C4 is a standard, tool-neutral way to present exactly this: progressively
more detailed views (context, containers, components) of one system.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the whole system and its runtime pieces at a glance (Priority: P1)

As someone new to the repository, I want a system-context view and a
container view, so I can learn who uses the system, which deployable pieces
exist, and what each one is responsible for, without reading source code.

**Why this priority**: These two views answer the "what is this and how is
it put together" question and are useful on their own even if nothing else
is delivered.

**Independent Test**: Hand the two views to someone who has not seen the
project and ask them to name every deployable piece, the data store, both
message brokers, and which piece pushes live updates to the user. They can
answer from the diagrams alone.

**Acceptance Scenarios**:

1. **Given** the documentation, **When** a reader opens the context view,
   **Then** they see the customer, the OrderSystem as a single box, and the
   external capabilities it relies on (payment, inventory) with a note that
   they are simulated.
2. **Given** the documentation, **When** a reader opens the container view,
   **Then** every runtime piece in the Aspire app host appears exactly once
   (frontend, Api, NotificationService, MigrationService, Postgres, Kafka,
   RabbitMQ, Redis), with each relationship labelled with its purpose and
   communication style (request/response, event, queue, push).
3. **Given** the container view, **When** a reader traces the payment-retry
   loop, **Then** they can see that the Api both publishes to and consumes
   from the retry queue.

---

### User Story 2 - Understand the internals of the two services that matter (Priority: P2)

As a contributor, I want a component view of the Api and of the
NotificationService, so I can see where the hexagonal boundaries fall, which
component is the saga, and where each consumer lives.

**Why this priority**: It is the level contributors work at, but it only
makes sense after the P1 views exist.

**Independent Test**: A contributor can point to the component that
orchestrates the order saga, the two consumers, and the adapter for each
piece of infrastructure, using only the component views.

**Acceptance Scenarios**:

1. **Given** the Api component view, **When** a reader looks for the saga
   orchestrator, the Postgres/Kafka/RabbitMQ adapters, and the retry
   consumer, **Then** each is present and its dependency direction matches
   the layering described in README (adapters depend inward on the core).
2. **Given** the NotificationService component view, **When** a reader
   follows an event from Kafka to the browser, **Then** the path is
   consumer, then hub, then client, with the Redis backplane shown.

---

### User Story 3 - Follow one order end to end (Priority: P3)

As a reader, I want a dynamic view of a representative order flow (create,
payment declined, delayed retry, final outcome, live notification), so I can
see the *sequence* the static views cannot show.

**Why this priority**: Valuable for explanation, but the static views stand
on their own.

**Independent Test**: A reader can state, from the diagram only, which
broker carries the retry delay and which carries the status events.

**Acceptance Scenarios**:

1. **Given** the dynamic view, **When** a reader follows the numbered steps,
   **Then** they pass through persistence, event publication, retry
   scheduling, retry delivery, and the notification push in order.

---

### User Story 4 - The documentation is discoverable and stays honest (Priority: P2)

As a maintainer, I want the C4 documentation linked from README and
rendered directly on GitHub, and I want a clear way to notice when it has
drifted from the system, so it does not silently rot.

**Why this priority**: Documentation nobody finds, or that no longer
matches reality, is worse than none.

**Independent Test**: From the repository's front page a reader reaches the
diagrams in one click and sees them rendered, not as raw text. Comparing the
container view against the app host's resource list shows no missing or
extra pieces.

**Acceptance Scenarios**:

1. **Given** README, **When** a reader looks at the architecture section,
   **Then** there is a link to the C4 documentation.
2. **Given** the C4 documentation viewed on GitHub, **When** the page loads,
   **Then** all diagrams render without any local tooling.
3. **Given** a future change that adds or removes a runtime piece, **When**
   the contributor reads the documentation's maintenance note, **Then** they
   know which views to update.

---

### Edge Cases

- A reader's viewer does not render diagrams (plain-text editor): each
  diagram must still be understandable from its title and a short caption
  or legend written beside it.
- Simulated collaborators (payment, inventory) live inside the Api, not in
  separate services: the documentation must not imply they are separate
  deployables.
- The Api runs as multiple replicas: the container view must say so without
  drawing duplicate boxes.
- Known architectural gaps (shared database use, no outbox, no dead-letter
  handling) are out of scope as design changes, but the documentation must
  not misrepresent the system as having them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain C4 documentation with a system
  context view, a container view, and component views for the Api and the
  NotificationService.
- **FR-002**: The documentation MUST include one dynamic view showing a
  representative order flow that includes a declined payment and its retry.
- **FR-003**: The container view MUST show exactly the runtime resources the
  Aspire app host defines, with no omissions and no invented services.
- **FR-004**: Both message consumers (live-notification consumer and
  payment-retry consumer) MUST be identifiable in the views, together with
  the process that hosts each.
- **FR-005**: Every relationship in every view MUST carry a label stating its
  purpose, and MUST state the communication style where it is not obvious.
- **FR-006**: The documentation MUST state that payment and inventory are
  simulated and hosted inside the Api.
- **FR-007**: The documentation MUST be linked from README's architecture
  section.
- **FR-008**: All diagrams MUST render on GitHub with no local tooling and
  MUST be stored as text in the repository so they are reviewable in diffs.
- **FR-009**: The documentation MUST include a short maintenance note
  saying which views to revisit when a runtime piece or a service's
  components change.
- **FR-010**: Existing README content MUST remain accurate; where the new
  views duplicate prose, the prose may be shortened to point at them but
  MUST NOT contradict them.

### Key Entities

- **View**: One C4 diagram at a given level (context, container, component,
  dynamic), with a title, a caption, and its elements and relationships.
- **Element**: A person, system, container, or component shown in a view,
  with a name, a technology or responsibility description.
- **Relationship**: A labelled, directed link between two elements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person new to the project can name every deployable piece
  and every message broker, using only the container view, in under 5
  minutes.
- **SC-002**: 100% of the resources defined in the app host appear in the
  container view, and 0 elements appear that are not in the system.
- **SC-003**: From the repository front page, a reader reaches the first
  diagram in at most 2 clicks.
- **SC-004**: All diagrams display rendered (not as raw text) on the
  repository's web interface.
- **SC-005**: A contributor can locate the saga orchestrator and both
  consumers within 2 minutes using only the component views.

## Assumptions

- Audience is developers and reviewers of this study project, not end users
  or operations staff, so deployment and infrastructure-sizing views are out
  of scope.
- The text-based, GitHub-rendered diagram format is acceptable even though
  its C4 support is less polished than a dedicated C4 tool; a dedicated
  modelling tool is out of scope.
- Documentation is written in English, matching README and existing specs.
- The frontend and MigrationService have no meaningful internal structure,
  so they get no component view.
- Capturing the current system as-is is the goal; fixing the architectural
  gaps noted in Edge Cases is separate work.
- Keeping the diagrams in sync is a manual, review-time discipline (the
  maintenance note); automated drift detection is out of scope.
