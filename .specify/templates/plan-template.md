# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., C# / .NET 10 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., Wolverine, EF Core, Confluent.Kafka or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, Kafka topic, RabbitMQ queue or N/A]

**Testing**: [e.g., xUnit + AwesomeAssertions, Testcontainers, Playwright or NEEDS CLARIFICATION]

**Target Platform**: [e.g., .NET Aspire distributed app, Linux container or NEEDS CLARIFICATION]

**Project Type**: [e.g., new adapter project, new port + handler in an existing project, new Aspire resource or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., no new framework type inside Domain/Application or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., number of new projects/ports/adapters touched]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

[For each principle in `.specify/memory/constitution.md`, state whether this
design is consistent with it. For any principle in tension with the design
(most commonly Principle I - Hexagonal Boundaries, Principle IV - No
Speculative Abstraction, or Principle VI - framework vs. hand-rolled), either
resolve the tension in the design or carry it into Complexity Tracking below
as an explicit, justified exception.]

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output (/speckit-plan)
├── data-model.md        # Phase 1 output (/speckit-plan)
├── quickstart.md        # Phase 1 output (/speckit-plan)
├── contracts/           # Phase 1 output (/speckit-plan)
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

### Source Code (repository root)

<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature within the existing OrderSystem solution. Name the actual
  projects/folders touched (e.g. OrderSystem.Application/Sagas/,
  OrderSystem.Adapters.Postgres/, OrderSystem.NotificationService/). Delete
  this comment and the bracketed example once filled in.
-->

```text
OrderSystem.Domain/
└── [new/changed files]

OrderSystem.Application/
└── [new/changed files]

OrderSystem.Adapters.<Name>/
└── [new/changed files]

OrderSystem.<Host-or-Service>/
└── [new/changed files]

OrderSystem.Domain.Tests/ | OrderSystem.Application.Tests/ | OrderSystem.Adapters.IntegrationTests/ | OrderSystem.EndToEndTests/
└── [new/changed test files — match the layer being tested, per Constitution Principle III]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., Wolverine Saga base class in Application] | [current need] | [why a hand-rolled orchestrator was insufficient] |
