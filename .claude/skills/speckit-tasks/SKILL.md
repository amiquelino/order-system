---
name: speckit-tasks
description: Break an implementation plan into an actionable, dependency-ordered tasks.md, organized by user story. Use when the user invokes /speckit-tasks, or the plan is ready to be broken into concrete implementation tasks.
---

## Steps

1. Run, from the repo root:

   ```bash
   bash .specify/scripts/bash/setup-tasks.sh
   ```

   Parse `FEATURE_DIR`, `TASKS_TEMPLATE`, and the `AVAILABLE_DOCS` checklist
   from its output. It errors if `plan.md` or `spec.md` is missing — if so,
   tell the user to run `/speckit-plan` or `/speckit-specify` first.

2. Read, from `FEATURE_DIR`:
   - **Required**: `plan.md` (tech stack, project structure), `spec.md`
     (user stories with priorities).
   - **Whichever of these exist**: `data-model.md`, `contracts/`,
     `research.md`, `quickstart.md`.
   - `.specify/memory/constitution.md`, for Principle III (the test pyramid
     must drive which test tasks go in which project).

3. Load `TASKS_TEMPLATE` for the required structure, then generate
   `tasks.md`:
   - Phase 1: Setup. Phase 2: Foundational (blocking prerequisites for every
     user story). Phase 3+: one phase per user story, in priority order
     (P1, P2, …). Final phase: polish/cross-cutting.
   - Map entities from `data-model.md` and interfaces from `contracts/` to
     the user story that needs them.
   - Only include test tasks if the spec explicitly asked for tests or the
     user requested TDD — and when you do, put each test in the project
     matching its layer per Constitution Principle III (Domain.Tests for
     entity logic, Application.Tests with a fake per port for handlers,
     Adapters.IntegrationTests for a new adapter, EndToEndTests only for a
     genuinely new golden-path scenario).
   - Every task follows `- [ ] T0NN [P?] [USn?] Description with exact file
     path`, using the real OrderSystem project paths from `plan.md`'s
     Project Structure section (never the template's generic `src/model.py`
     style placeholders).

4. Write the completed `tasks.md` to `FEATURE_DIR`.

5. Report to the user: total task count, count per user story, which tasks
   are parallelizable, and the suggested MVP scope (usually just User
   Story 1) — and that it's ready for `/speckit-implement`.
