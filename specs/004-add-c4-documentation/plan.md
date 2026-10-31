# Implementation Plan: Add C4 Documentation

**Branch**: `main` (no feature branch created) | **Date**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-add-c4-documentation/spec.md`

## Summary

Add one Markdown document, `docs/architecture/c4.md`, holding C4 diagrams
(context, container, Api components, NotificationService components, and
one dynamic order flow) written as Mermaid C4 blocks so GitHub renders them
with no tooling. Link it from README's "Architecture overview" and add a
maintenance note listing which views to revisit when the system changes.
No source code, project, package, or CI change.

## Technical Context

**Language/Version**: Markdown + Mermaid (C4 diagram types). No C#/.NET change.

**Primary Dependencies**: None added to the repo. For local validation only:
`@mermaid-js/mermaid-cli` run on demand through `npx` (not committed, not in
`package.json`).

**Storage**: N/A

**Testing**: No automated test layer applies (no executable code). Validation
is the manual checklist in [quickstart.md](quickstart.md): every diagram
parses and renders, and the container view is cross-checked against
`hosts/OrderSystem.AppHost/AppHost.cs`.

**Target Platform**: GitHub's Markdown renderer (primary); any
Mermaid-capable viewer such as the VS Code preview (secondary).

**Project Type**: Documentation only; one new file plus a README edit.

**Performance Goals**: N/A

**Constraints**: Diagrams must be plain text in the repo (reviewable in
diffs); must render on GitHub unaided; must not contradict README or the
code; must not draw anything the AppHost does not define.

**Scale/Scope**: 1 new file (~5 diagrams, ~8 containers, ~10 Api
components, 2 NotificationService components), 1 README edit.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Hexagonal Boundaries Are Inviolable | Consistent | No code touched. The component view *depicts* the boundaries (adapters depend inward) and must not draw an arrow that violates them. The one deliberate exception already in README (the RabbitMQ adapter references Application as a driving adapter) is drawn as it is. |
| II. Ports Are Discovered From the Core | Consistent | No port added. Port names (`IOrderRepository`, `IEventPublisher`, `IPaymentRetryQueue`) appear in diagram labels as they exist. |
| III. The Test Pyramid Mirrors the Architecture | Not applicable | No behavior changes, so there is no layer to test. Verification is the manual render-and-cross-check checklist; adding a docs linter to CI would be speculative (see IV). |
| IV. No Speculative Abstraction | Consistent | One file, no generator, no DSL toolchain, no CI job. Automated drift detection is explicitly out of scope in the spec. |
| V. Every State Transition Announces Itself | Not applicable | No handler or status change. The dynamic view shows existing event publication; it must keep showing a status event at each transition. |
| VI. Match the Project's Existing Idiom | Consistent | The repo documents architecture in Markdown with fenced diagrams (README). Staying in Markdown matches that; a modelling tool (Structurizr, PlantUML) would add a new toolchain. Tradeoff recorded in research.md Decision 1. |

No violations, so Complexity Tracking is omitted.

## Project Structure

### Documentation (this feature)

```text
specs/004-add-c4-documentation/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output (/speckit-plan)
├── data-model.md        # Phase 1 output: element and relationship inventory
├── quickstart.md        # Phase 1 output: validation checklist
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

No `contracts/` directory: the feature exposes no endpoint, message, or port.

### Source Code (repository root)

```text
docs/
└── architecture/
    └── c4.md            # NEW: all C4 views + maintenance note

README.md                # EDIT: link from "Architecture overview"; shorten
                         #       prose only where a view now says the same
                         #       thing, never contradict it
```

No change under `src/`, `hosts/`, `tests/`, `.github/`, or the frontend.

**Structure Decision**: A single file under a new top-level `docs/`
directory. The repo has no `docs/` yet; `specs/` is for per-feature
decision records and `README.md` is the front page, so neither is the right
home for long-lived architecture reference. `docs/architecture/` leaves room
for sibling documents without committing to any now. One file (not one per
view) keeps the views cross-referenceable and keeps drift checks to a single
diff.
