---
name: speckit-plan
description: Generate the technical implementation plan (tech stack, Constitution Check, data model, contracts, quickstart) for the active feature spec. Use when the user invokes /speckit-plan, or is ready to choose the architecture/tech approach for an already-specified feature.
---

## Steps

1. Run, from the repo root:

   ```bash
   bash .specify/scripts/bash/setup-plan.sh
   ```

   Parse `FEATURE_SPEC`, `IMPL_PLAN`, and `FEATURE_DIR` from its output.
   The script seeds `plan.md` from the template if it doesn't already exist.

2. Read `FEATURE_SPEC` and `.specify/memory/constitution.md`. Read
   `IMPL_PLAN` (the freshly-copied template) to see the sections to fill.

3. If the user's message included technical direction (tech stack,
   architecture, specific services/projects to touch), use it. Otherwise
   infer the simplest approach consistent with how this codebase already
   solves similar problems — look at the relevant existing projects
   (`OrderSystem.Domain`, `OrderSystem.Application`, the `Adapters.*`
   projects, `OrderSystem.Api`, `OrderSystem.NotificationService`) before
   proposing new structure.

4. Fill in `plan.md`:
   - **Technical Context**: language/version, primary dependencies, storage,
     testing, target platform, scope — mark anything truly unresolved as
     `NEEDS CLARIFICATION` rather than guessing at something that changes
     the design.
   - **Constitution Check**: go through each principle in the constitution
     and state whether this design is consistent with it. For anything in
     tension (most often Principle I - Hexagonal Boundaries, Principle IV -
     No Speculative Abstraction, or Principle VI - framework vs. hand-rolled),
     either change the design to resolve it, or carry it into Complexity
     Tracking as an explicit, justified exception. Don't leave an
     unresolved, unacknowledged tension.
   - **Project Structure**: replace the placeholder tree with the actual
     projects/folders this feature will touch, using real OrderSystem
     project names — not the generic example.
   - **Complexity Tracking**: only if Constitution Check found violations.

5. Phase 0 — write `research.md` in `FEATURE_DIR`: resolve every
   `NEEDS CLARIFICATION` from Technical Context with a Decision /
   Rationale / Alternatives-considered entry each.

6. Phase 1 — from the (now unknown-free) spec and research:
   - `data-model.md`: entities, fields, relationships, validation rules,
     state transitions — matching the style of the existing `Order` entity
     and its `OrderStatus` state machine where relevant.
   - `contracts/`: only if this feature exposes a new interface (an HTTP
     endpoint, a new Kafka/RabbitMQ message contract, a new port). Skip
     this directory entirely if there's nothing to document.
   - `quickstart.md`: a runnable validation guide (prerequisites, commands,
     expected outcome) proving the feature works end-to-end. No
     implementation code, no full test suites — that belongs in tasks.md
     and the implementation itself.

7. Re-check the Constitution Check section against the Phase 1 design; update
   it if the detailed design changed anything.

8. Report to the user: the plan path and every artifact generated, and
   whether it's ready for `/speckit-tasks`.
