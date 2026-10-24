---
name: speckit-specify
description: Create a new feature specification under specs/<NNN-slug>/spec.md from a natural-language feature description, focused on WHAT and WHY, not implementation. Use when the user invokes /speckit-specify, or describes a feature they want specified before planning or implementation begins.
---

## Steps

1. The feature description is whatever the user just gave you (or the text
   after `/speckit-specify`). If it's empty, ask for it before continuing.

2. Run, from the repo root:

   ```bash
   bash .specify/scripts/bash/create-new-feature.sh "<feature description>"
   ```

   Parse `FEATURE_DIR` and `SPEC_FILE` from its output. The script has
   already created the directory and seeded `spec.md` from the template —
   don't create another feature directory yourself.

3. Read the seeded `SPEC_FILE` to see the required structure, and read
   `.specify/memory/constitution.md` if it exists, for project principles
   that bear on scope (e.g. Principle IV, No Speculative Abstraction — don't
   let the spec balloon past what was actually asked for).

4. Fill in the template, replacing every bracketed placeholder with concrete
   content derived from the feature description:
   - Extract actors, actions, data, and constraints from the description.
   - Prioritize user stories (P1 = MVP-critical) so each is independently
     testable on its own.
   - Write functional requirements that are each independently testable.
   - Write success criteria that are measurable and technology-agnostic —
     no framework or implementation detail.
   - For anything genuinely ambiguous where no reasonable default exists and
     the choice materially changes scope, security, or UX: mark it
     `[NEEDS CLARIFICATION: specific question]` — but cap this at 3 markers
     total, prioritized scope > security/privacy > UX > technical detail.
     For everything else, make an informed, documented assumption instead
     (record it in the Assumptions section) rather than asking.

5. Write the filled-in spec back to `SPEC_FILE`, preserving section order and
   headings.

6. If any `[NEEDS CLARIFICATION]` markers remain, present them to the user
   as a short numbered list with your own suggested answer for each, and
   wait for their response before leaving the spec in that state. Otherwise,
   skip straight to reporting.

7. Report to the user: the feature directory, the spec file path, and
   whether it's ready for `/speckit-plan` or still has open questions.

## Guidelines

- Focus on WHAT users need and WHY — no tech stack, APIs, or code structure.
  Implementation detail belongs in `/speckit-plan`, not here.
- Remove sections from the template that genuinely don't apply; don't leave
  them as "N/A".
- One feature per invocation — if the description bundles two unrelated
  features, specify the first and tell the user to run this again for the
  second.
