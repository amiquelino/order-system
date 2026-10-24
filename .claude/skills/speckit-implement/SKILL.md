---
name: speckit-implement
description: Execute the tasks in the active feature's tasks.md in dependency order, marking each one complete as it's done. Use when the user invokes /speckit-implement, or tasks.md exists and is ready to be built.
---

## Steps

1. Run, from the repo root:

   ```bash
   bash .specify/scripts/bash/check-prerequisites.sh --require-tasks --include-tasks
   ```

   It errors if `plan.md` or `tasks.md` is missing — if so, tell the user
   which skill to run first (`/speckit-plan` or `/speckit-tasks`).

2. Read, from the feature directory printed above: `tasks.md` (required),
   `plan.md` (required), and whichever of `data-model.md`, `contracts/`,
   `research.md`, `quickstart.md` exist. Read
   `.specify/memory/constitution.md` too — every change you make here still
   has to honor it (hexagonal boundaries, ports before adapters, the test
   pyramid, no speculative abstraction, every status change publishing
   `OrderStatusChangedEvent`, and the framework-vs-hand-rolled tradeoff
   recorded in the plan).

3. Execute `tasks.md` phase by phase, in order: Setup → Foundational → each
   user story phase in priority order → Polish. Within a phase, run tasks
   marked `[P]` together (different files, no dependency between them);
   run unmarked tasks in sequence. If tests are present for a phase, write
   and confirm they fail before writing the implementation they cover.

4. After each task completes, edit `tasks.md` to flip it from `- [ ]` to
   `- [X]` — do this as you go, not in one pass at the end, so progress is
   visible if the run is interrupted.

5. If a non-parallel task fails: stop, report the failure with enough
   context to debug it, and don't continue past it. If a `[P]` task fails
   while others in its group succeed, finish the rest of that group and
   report the failure alongside them.

6. After each user-story phase, do a quick sanity check that the story's
   "Independent Test" (from `spec.md`) is actually satisfiable before
   moving to the next phase.

7. When all tasks are done (or the user asked you to stop at a specific
   phase), report: what was built, what's left unmarked and why (if
   anything), and whether existing test suites were run and passed.
