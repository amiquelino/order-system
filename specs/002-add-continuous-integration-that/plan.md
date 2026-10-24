# Implementation Plan: Continuous Integration

**Branch**: `specs/002-add-continuous-integration-that/` (no git branches used for feature identity in this repo) | **Date**: 2026-10-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-add-continuous-integration-that/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

One new GitHub Actions workflow, `.github/workflows/ci.yml`, triggered on
every push and pull request. It runs the existing test pyramid — unit
(`Domain.Tests` + `Application.Tests`), adapter-integration
(`Adapters.IntegrationTests`, needs Docker), end-to-end
(`EndToEndTests`, needs Docker + Node + a headless browser), and the
frontend's own lint + build — as separate parallel jobs on hosted
`ubuntu-latest` runners, so GitHub's own PR/commit status UI shows each
tier's pass/fail distinctly with no extra tooling. A one-line status badge
is added to `README.md`. No application source code changes.

## Technical Context

**Language/Version**: YAML (GitHub Actions workflow syntax); runs the
existing .NET 10 SDK and Node.js toolchains — no new language introduced.

**Primary Dependencies**: `actions/checkout`, `actions/setup-dotnet`,
`actions/setup-node` (all first-party GitHub Actions). Docker ships
pre-installed and running on `ubuntu-latest` runners — no Docker-specific
action needed for Testcontainers to work. No new NuGet or npm packages.

**Storage**: N/A.

**Testing**: Runs the repository's existing four test projects and the
frontend's existing `npm run lint` / `npm run build` scripts verbatim. This
feature adds no new tests of its own — it's the thing that runs them.

**Target Platform**: GitHub-hosted `ubuntu-latest` runners (Linux) — matches
this project's existing primary development/deployment target.

**Project Type**: A single new infrastructure-as-config file
(`.github/workflows/ci.yml`) plus a one-line `README.md` addition. The
first non-C#/non-TypeScript artifact in the repository.

**Performance Goals**: Full pipeline (all jobs, running in parallel)
completes within SC-003's 15-minute budget.

**Constraints**: No new paid account or external service (Constitution
Principle VI — prefer what's already there: the repo already lives on
GitHub). No self-hosted runner (FR-006 rules out anything needing a
persistently pre-configured machine). Must work within GitHub Actions'
free, unlimited-minutes tier for public repositories.

**Scale/Scope**: One new file, ~5 jobs in it. Zero changes to any existing
`OrderSystem.*` project or `order-system-frontend` source file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable** — Not applicable. This feature
  touches no project's dependencies or layering; it only runs what already
  exists.
- **II. Ports Are Discovered From the Core** — Not applicable, no new port.
- **III. The Test Pyramid Mirrors the Architecture** — Directly reinforced:
  this feature's entire purpose is making sure every existing tier actually
  runs, every time, instead of relying on someone remembering to run it
  locally (FR-003). The per-tier job split (see research.md Decision 2) is
  what makes the pyramid's structure visible in the CI UI, not just in the
  repo layout.
- **IV. No Speculative Abstraction** — Shaped the design directly: no
  multi-OS/.NET-version build matrix, no path-based filtering to skip tiers
  on partial changes, no deployment or publishing stage — none of that was
  asked for (spec.md Assumptions). One workflow file, exactly the jobs
  FR-001–FR-006 call for.
- **V. Every State Transition Announces Itself** — Not applicable, no
  domain/application code is touched.
- **VI. Match the Project's Existing Idiom Before Reaching for a
  Framework's** — Directly applied: GitHub Actions is the zero-new-dependency
  choice because the repository already lives on GitHub, versus adopting a
  third-party CI SaaS that would need its own account and secret-sharing
  setup for no added benefit here.

No violations. Complexity Tracking is intentionally omitted below.

## Project Structure

### Documentation (this feature)

```text
specs/002-add-continuous-integration-that/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

No `data-model.md` and no `contracts/`: this feature has no business
entities and exposes no new HTTP/message interface — the only "shape" it
introduces is the workflow's own job structure, which is documented in
research.md (Decision 2) instead of forced into either template.

### Source Code (repository root)

```text
.github/
└── workflows/
    └── ci.yml            # new — triggers: push (any branch), pull_request (into main)
                           # jobs: domain-and-application-tests (no Docker),
                           #       adapter-integration-tests (needs Docker),
                           #       end-to-end-tests (needs Docker + Node + browser),
                           #       frontend-checks (npm run lint, npm run build)

README.md                  # changed — one-line CI status badge near the top
```

Not touched: every existing `OrderSystem.*` project, `order-system-frontend`,
`.specify/`, and every existing `specs/*` feature directory.

**Structure Decision**: A single workflow file with multiple jobs, not
multiple workflow files — GitHub's UI shows jobs from one workflow exactly
as distinctly as jobs from separate workflows (satisfying FR-005/SC-004
either way), so splitting into several files would only add duplication
(each file re-declaring its own `on:` trigger) with no benefit.

## Complexity Tracking

*No violations — table intentionally omitted.*
