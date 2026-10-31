# Research: Add C4 Documentation

No `NEEDS CLARIFICATION` markers were left in Technical Context. Four
decisions below, each with a rejected alternative.

## Decision 1: Mermaid C4 blocks in Markdown

**Decision**: Write each view as a fenced `mermaid` block using Mermaid's C4
diagram types (`C4Context`, `C4Container`, `C4Component`, `C4Dynamic`).

**Rationale**: GitHub renders Mermaid natively, which satisfies FR-008
(renders with no local tooling, stored as reviewable text) and SC-004. It
matches how the repo already documents things (Markdown with fenced
diagrams) per Constitution Principle VI, and adds no toolchain.

**Alternatives considered**:
- *Structurizr DSL* — rejected: best-in-class C4 modelling, but needs
  Structurizr Lite or CLI to render, and GitHub shows it as raw text.
  Fails FR-008.
- *PlantUML with the C4-PlantUML library* — rejected: GitHub does not render
  PlantUML; it would need a generated-image step committed to the repo,
  which hides the diagram from diffs.
- *Mermaid flowcharts styled to look like C4* — rejected: renders more
  predictably than the C4 types, but is not C4 notation, so it loses the
  shared vocabulary that is the reason to use C4 at all. Kept as the
  fallback if a C4 block proves unreadable (see Decision 3).

## Decision 2: One file, `docs/architecture/c4.md`

**Decision**: All views live in a single Markdown file, ordered by C4 level,
each preceded by a one-paragraph caption and followed by a legend of what is
simulated or replicated.

**Rationale**: The views reference each other (the container view's Api box
is the component view's subject). A single file keeps one diff per
architectural change and one place for the maintenance note. FR-007 and
SC-003 (two clicks from the front page) are met by a README link straight to
the file.

**Alternatives considered**:
- *One file per level* — rejected: five tiny files to keep in sync for a
  system this size, and more links to follow to read the whole story.
- *Inline everything in README* — rejected: README is already long, and the
  spec requires only a link plus prose that does not contradict the views.

## Decision 3: Handle Mermaid C4's layout limits by keeping diagrams small

**Decision**: Cap each view at roughly 10 elements. Put relationship detail
in the label text and the caption, not in extra boxes. Use `UpdateLayoutConfig`
only if a render shows clipped labels. If a block still reads poorly after
that, fall back to styled flowcharts for that view alone and say so in its
caption.

**Rationale**: Mermaid's C4 support is documented as experimental: layout is
declaration-order-driven, long labels can overflow, and there is no manual
positioning. Small diagrams are the reliable mitigation, and they are also
what makes the views legible to a newcomer (SC-001).

**Alternatives considered**:
- *Draw everything in one big container diagram* — rejected: the
  Api component detail would swamp the container view and break the
  progressive-detail idea.
- *Commit rendered SVG/PNG next to the source* — rejected: duplicate
  artifacts drift from the text, and the spec wants text as the source of
  truth.

## Decision 4: Verify by cross-reading the code, render-check locally

**Decision**: Accuracy is checked by comparing the container view to the
resource list in `hosts/OrderSystem.AppHost/AppHost.cs` and the component
views to the `Program.cs` registrations and consumer classes. Syntax and
legibility are checked by rendering each block with `mermaid-cli` through
`npx`, run on demand, and by previewing on a pushed branch in GitHub.

**Rationale**: The spec rules out automated drift detection (Assumptions),
and Constitution Principle IV argues against adding a CI job for a
single-file doc. A one-time documented check at authoring time is enough;
the maintenance note covers later changes.

**Alternatives considered**:
- *A CI step that renders all Mermaid blocks* — rejected: new CI surface and
  a Node dependency for a file that changes rarely; revisit if the docs
  grow.
- *Trust GitHub's preview only* — rejected: it requires a push to find a
  syntax error, which is a slow feedback loop; local rendering is minutes.
