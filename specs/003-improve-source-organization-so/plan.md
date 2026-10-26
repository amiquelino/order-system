# Implementation Plan: Improve Source Organization

**Branch**: `specs/003-improve-source-organization-so/` (no git branches used for feature identity in this repo — though this feature's *execution* does use a real git branch + PR, see research.md Decision 3) | **Date**: 2026-10-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-improve-source-organization-so/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

Physically relocate all 13 `.NET` project directories into `src/`
(Domain, Application, Adapters) and `hosts/` (Api, AppHost,
MigrationService, NotificationService), with the 4 test projects into
`tests/`. `order-system-frontend/`, `OrderSystem.slnx`, `README.md`,
`.github/`, `.specify/`, and `specs/` all stay at the repository root.
Every `<ProjectReference>` (26 of them, across 11 `.csproj` files),
`OrderSystem.slnx`'s 13 project paths, `.github/workflows/ci.yml`'s 5
path-bearing lines, and every project path documented in `README.md` get
updated to match. No project, dependency, namespace, or behavior changes —
this is pure relocation, verified by the existing build and full test
suite (including the CI pipeline added in the previous feature) staying
green throughout.

## Technical Context

**Language/Version**: C# / .NET 10 (unchanged). XML edits to `.csproj`
files and `OrderSystem.slnx`; YAML edits to `.github/workflows/ci.yml`;
Markdown edits to `README.md`.

**Primary Dependencies**: None new.

**Storage**: N/A.

**Testing**: The existing four test projects plus the CI pipeline
(previous feature) are this feature's own verification mechanism — there
is nothing new to test, only confirmation that nothing broke (FR-002).

**Target Platform**: Unchanged.

**Project Type**: Directory restructuring of the 13 existing `.NET`
projects. Zero new projects, zero removed projects.

**Performance Goals**: N/A.

**Constraints**: Zero build or test regressions (FR-002, SC-002). Zero
namespace changes, zero dependency changes, zero behavior changes (spec
Assumptions) — every change in this feature is either a file move or a
path-string edit pointing at where that file now lives.

**Scale/Scope**: 13 project directories moved. 26 `<ProjectReference>`
edits across 11 `.csproj` files (full mapping in data-model.md). 13 path
rewrites in `OrderSystem.slnx`. 5 path-bearing lines in
`.github/workflows/ci.yml`. Several sections of `README.md` (Running the
application, Database schema, Automated tests, Solution layout).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable** — Directly reinforced, not
  just preserved: this feature's entire purpose is making the boundaries
  the Domain/Application/Adapters/Hosts split already enforces in code
  *visible* in the folder structure too (spec.md Problem statement).
- **II. Ports Are Discovered From the Core** — Not applicable, no port
  changes.
- **III. The Test Pyramid Mirrors the Architecture** — Reinforced: the new
  `tests/` directory groups all four tiers together as a visible
  counterpart to README's existing "Automated tests" description (User
  Story 2).
- **IV. No Speculative Abstraction** — Shaped the design directly: no
  further subdivision beyond the four groups the spec actually asked for
  (e.g. no per-adapter sub-folders, no renaming projects or namespaces,
  no restructuring `order-system-frontend/` internally). Exactly what
  FR-001–FR-004 call for, nothing more.
- **V. Every State Transition Announces Itself** — Not applicable, no
  application code is touched.
- **VI. Match the Project's Existing Idiom Before Reaching for a
  Framework's** — Not applicable, no framework choice involved.

No violations. Complexity Tracking is intentionally omitted below.

## Project Structure

### Documentation (this feature)

```text
specs/003-improve-source-organization-so/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output — the full move + reference mapping
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

No `contracts/`: no new HTTP/message interface is introduced.
`data-model.md` is repurposed here — not business entities, but the exact
old-path → new-path and every `<ProjectReference>`'s new relative value,
since getting even one of those 26 wrong breaks the build (research.md
Decision 1 explains why this is computed once, up front, rather than
during implementation).

### Source Code (repository root)

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
├── OrderSystem.AppHost/
├── OrderSystem.MigrationService/
└── OrderSystem.NotificationService/

tests/
├── OrderSystem.Domain.Tests/
├── OrderSystem.Application.Tests/
├── OrderSystem.Adapters.IntegrationTests/
└── OrderSystem.EndToEndTests/

order-system-frontend/     # unchanged, unmoved
OrderSystem.slnx           # unchanged location; every <Project Path> inside updated
README.md                  # changed — every documented project path
.github/workflows/ci.yml   # changed — 5 path-bearing lines
.gitignore                 # unchanged — its **/OrderSystem.AppHost/bin/ pattern
                            # already matches at any depth, including hosts/OrderSystem.AppHost/bin/
```

Not touched: any project's own source files (no namespace changes, per
spec Assumptions), `order-system-frontend/`, `.specify/`, `specs/`,
`.claude/`, `.config/`.

**Structure Decision**: Matches spec.md FR-001 exactly — `src/` split by
hexagonal layer (`Domain`, `Application`, `Adapters`), `hosts/` for every
entry-point/service project, `tests/` for all four test tiers together.

## Complexity Tracking

*No violations — table intentionally omitted.*
