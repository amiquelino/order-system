---
name: speckit-constitution
description: Create or update the project constitution at .specify/memory/constitution.md from principles the user provides or that are inferred from the existing codebase. Use when the user invokes /speckit-constitution, or asks to establish, review, or amend OrderSystem's guiding principles/constitution.
---

## Scope Guard

This skill's work is limited to `.specify/memory/constitution.md`. If the
user's input also contains a feature request, a bug fix, or any other
non-governance intent, do not act on it here — note it back to them as a
suggested follow-up (`/speckit-specify`, typically) without executing it.
Never create, modify, or delete application source files from this skill.

## Steps

1. Read the current `.specify/memory/constitution.md`.
2. Classify the user's input: which parts are new/changed/removed principles
   or governance rules, and which parts are something else (defer those per
   the Scope Guard above).
3. Draft the updated constitution, preserving the existing structure (Core
   Principles, Workflow, Governance) and existing principles that aren't
   being changed. Each principle needs: a short name, a clear MUST/SHOULD
   rule, and a rationale if it isn't obvious from the rule itself.
4. Decide the version bump per the Governance section's own rules (MAJOR:
   principle removed/redefined; MINOR: principle added; PATCH: wording only).
   Update "Last Amended" to today's date; leave "Ratified" untouched.
5. Write the file back in full (overwrite).
6. Report to the user: the new version and why, and a one-line note of any
   deferred non-governance intent along with which skill would handle it.

Keep the tone consistent with the rest of the document: direct, specific,
and grounded in what this codebase actually does — not generic
software-engineering platitudes.
