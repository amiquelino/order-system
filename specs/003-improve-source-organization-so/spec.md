# Feature Specification: Improve Source Organization

**Feature Directory**: `specs/003-improve-source-organization-so/`

**Created**: 2026-10-24

**Status**: Draft

**Input**: User description: "improve source organization" — the second half of an earlier combined request ("Create CI and improve source organization"), split off per the one-feature-per-spec rule and specified separately now that CI is done.

## Problem

The repository currently has 13 .NET project directories plus the frontend,
`specs/`, `.specify/`, `.github/`, and `.claude/` all sitting flat at the
repository root (18+ top-level entries). The hexagonal architecture this
project is explicitly built to demonstrate — Domain, Application, Adapters,
Hosts, and a matching test pyramid — is documented at length in README's
"Architecture overview" and "Solution layout" sections, but nothing in the
actual folder structure reflects it: `OrderSystem.Domain`,
`OrderSystem.Adapters.Postgres`, `OrderSystem.Api`, and
`OrderSystem.Domain.Tests` all sit as siblings with no visual grouping.
Anyone browsing the repo (on GitHub, in a terminal, or in an IDE) has to
read the prose to learn which layer each project belongs to.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The architecture is visible in the structure itself (Priority: P1)

As someone browsing this repository — a visitor on GitHub, a contributor
in a terminal, or a reviewer opening the solution in an IDE — I want the
hexagonal layers (core domain/application, adapters, host
projects/services, tests) to be grouped and visually distinguishable in
the project structure itself, so I can tell which layer a project belongs
to at a glance, without first reading README prose.

**Why this priority**: This is the entire point of the feature — without
it, there's nothing to validate. It's also the project's own stated value
(Constitution Principle I, "Hexagonal Boundaries Are Inviolable") made
visible rather than only enforced in code.

**Independent Test**: Open the repository (via whichever means — GitHub's
file browser, `ls`/`tree` in a terminal, or an IDE's solution/project
explorer) and verify a viewer can identify which group (core, adapters,
hosts, or tests) each project belongs to from the grouping alone, without
opening `README.md`.

**Acceptance Scenarios**:

1. **Given** the repository's top-level structure, **When** someone lists
   or browses it, **Then** projects are grouped by architectural layer
   rather than appearing as one flat list of 13+ siblings.
2. **Given** an existing cross-project reference (e.g.
   `OrderSystem.Api` depending on `OrderSystem.Adapters.Postgres`), **When**
   the reorganization is complete, **Then** the reference still resolves
   correctly and the solution still builds.

---

### User Story 2 - Test projects are grouped distinctly from what they test (Priority: P2)

As a contributor, I want test projects grouped together (or clearly paired
with the project they test) rather than interleaved anonymously among
source projects, so the project structure reflects the same test-pyramid
distinction (unit / adapter-integration / end-to-end) that README's
"Automated tests" section already documents.

**Why this priority**: A refinement of User Story 1 specifically for test
projects, which currently make up more than a third of the repository's
top-level entries (`Domain.Tests`, `Application.Tests`,
`Adapters.IntegrationTests`, `EndToEndTests`).

**Independent Test**: Browse the reorganized structure and verify test
projects are identifiable as a distinct group from the source projects
they exercise.

**Acceptance Scenarios**:

1. **Given** the reorganized structure, **When** someone looks for "all the
   tests," **Then** they're grouped together (or otherwise clearly
   distinguished) rather than requiring the README's project-by-project
   description to identify them.

---

### Edge Cases

- What happens to every existing cross-project reference
  (`<ProjectReference>` in every `.csproj`, `OrderSystem.slnx` itself,
  Aspire's `OrderSystem.AppHost.csproj` references to every project it
  orchestrates) when projects physically move? Every one of them MUST be
  updated to the new relative path and MUST still resolve — this is the
  real risk Option A accepts in exchange for structure that's visible
  everywhere, not just in an IDE.
- What happens to paths that currently assume today's flat layout:
  `.github/workflows/ci.yml` (passes project names like
  `OrderSystem.Domain.Tests` directly to `dotnet test`, which currently
  resolves because that name is also the relative path from the repo
  root), and every command documented in `README.md` (e.g. `dotnet run
  --project OrderSystem.AppHost`)? All MUST be updated to the new paths
  (e.g. `dotnet test tests/OrderSystem.Domain.Tests`) — this is exactly
  what FR-002 requires verifying stays green.
- What happens to the existing `specs/00N-*` feature directories and
  `.specify/` tooling? Out of scope — see Assumptions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST group projects by architectural layer through a
  **physical directory move** (Clarification 1, Option A) — every
  `.csproj` directory actually relocates, so the grouping is visible on
  GitHub, in a terminal, and in every IDE alike, not only inside one that
  renders solution folders. The target grouping, using this project's own
  already-declared layer names (`.specify/memory/constitution.md`
  Principle I):

  ```text
  src/
  ├── Domain/OrderSystem.Domain/
  ├── Application/OrderSystem.Application/
  └── Adapters/
      ├── OrderSystem.Adapters.Postgres/
      ├── OrderSystem.Adapters.Kafka/
      └── OrderSystem.Adapters.RabbitMQ/

  hosts/
  ├── OrderSystem.Api/
  ├── OrderSystem.NotificationService/
  ├── OrderSystem.MigrationService/
  └── OrderSystem.AppHost/

  tests/
  ├── OrderSystem.Domain.Tests/
  ├── OrderSystem.Application.Tests/
  ├── OrderSystem.Adapters.IntegrationTests/
  └── OrderSystem.EndToEndTests/
  ```

  `OrderSystem.slnx`, `README.md`, `.gitignore`, `.github/`, `.specify/`,
  and `specs/` all stay at the repository root (conventional location
  regardless of how projects underneath are grouped). `order-system-frontend/`
  also stays at the root, unmoved — it's a separate Next.js/TypeScript
  consumer application, not one of this project's .NET hexagonal layers,
  and grouping it under `hosts/` would misrepresent it as one.
- **FR-002**: Whichever mechanism FR-001 resolves to, every existing
  cross-project reference and every existing automated check
  (`OrderSystem.slnx` build, all four test-tier CI jobs, the frontend's CI
  checks) MUST continue to pass with zero regressions — this feature
  reorganizes structure, it does not change behavior.
- **FR-003**: The grouping MUST reflect this project's own already-declared
  architecture (the layers named in `.specify/memory/constitution.md`
  Principle I and README's "Architecture overview"), not a generic or
  invented scheme.
- **FR-004**: Test projects MUST be grouped as a distinct set from the
  source projects they test (User Story 2) — satisfied by FR-001's
  dedicated `tests/` directory.

### Key Entities

Not applicable — this feature reorganizes existing structure; it
introduces no new business entity, port, or adapter.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every one of the repository's existing projects is placed in
  exactly one layer group, with zero projects left ungrouped or
  ambiguously placed.
- **SC-002**: The full existing test suite and CI pipeline (all four job
  tiers from the Continuous Integration feature) pass with zero
  regressions after the reorganization — same outcome as before, just
  relocated.
- **SC-003**: A person unfamiliar with this specific repo, given only its
  top-level structure (no README), can correctly guess which group at
  least 80% of the projects belong to.

## Assumptions

- `specs/*` feature directories and the `.specify/` SDD tooling are out of
  scope for this feature. "Source organization" refers to the application
  source code's own project layout, not the spec-authoring tooling
  layered on top of it. (The awkward auto-generated slugs in existing
  `specs/00N-*` directory names are a separate, smaller concern — not
  addressed here.)
- No project's C# namespaces change as part of this feature. C# namespaces
  are independent of folder paths; moving a project's *directory* doesn't
  require renaming anything inside it.
- `order-system-frontend/` stays exactly where it is, unmoved and
  internally unchanged (it already follows Next.js's own conventional
  `app/`/`lib/` layout) — it's a separate consumer application, not one of
  the .NET hexagonal layers this feature organizes (FR-001).
- This feature does not change which projects exist, what they depend on,
  or any build/test behavior — purely where things live and how that's
  presented.
