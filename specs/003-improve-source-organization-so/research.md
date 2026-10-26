# Research: Improve Source Organization

No `NEEDS CLARIFICATION` markers were left in Technical Context — the
spec's own clarification process already resolved the one open question
(physical move vs. IDE-only grouping). Three further decisions below,
each with a rejected alternative.

## Decision 1: Compute the full move + reference mapping once, up front

**Decision**: Every project's old path, new path, and every
`<ProjectReference>`'s new relative value is computed in `data-model.md`
during planning, not derived ad hoc during implementation.

**Rationale**: There are 26 `<ProjectReference>` entries across 11
`.csproj` files. Relative-path arithmetic (`..\..\`) is exactly the kind
of mechanical work where a single miscount silently breaks a build that
only surfaces as an obscure MSBuild "project not found" error — far
cheaper to get right once, during planning, with the whole dependency
graph in view, than to re-derive it file-by-file during implementation
and catch mistakes only when the build fails.

**Alternatives considered**:
- *Derive each path individually while editing each file* — rejected:
  no net benefit, and each edit happens with less context (only that one
  file's relative depth in mind) than the full graph gives this once.

## Decision 2: One atomic move, not an incremental migration

**Decision**: All 13 directory moves and all corresponding reference
edits happen together, verified once at the end by a full solution build
and test run — not project-by-project with the solution left in a
partially-moved, intentionally-broken-in-between state.

**Rationale**: A `<ProjectReference>` is a hard dependency: the instant
one side of it moves, every reference to it from an unmoved project
breaks, and every reference *from* it to another not-yet-moved project
also needs updating. There's no ordering of the 13 moves that keeps the
solution buildable partway through — Domain is referenced by nearly
everything, so moving it first breaks everything else immediately, and
moving it last leaves everything else broken until then instead. Given
that, there's no partial-migration benefit to chase; the only real
checkpoint that matters is the end state.

**Alternatives considered**:
- *Move leaf-first (tests, then hosts, then adapters, then
  Domain/Application last)* — rejected: still leaves the solution
  unbuildable at every intermediate commit (an unmoved project can't
  resolve a reference to an already-moved one, or vice versa), for no
  actual benefit — there's no "the build still half-works" checkpoint
  available either way, so there's nothing to gain by sequencing it.

## Decision 3: Execute through a real branch and pull request, gated by CI

**Decision**: Unlike the previous two features (committed directly to
`main`), this one's actual move happens on a branch, opened as a pull
request, and only merged once the CI pipeline (added in the previous
feature) reports all four jobs green on it.

**Rationale**: This is the highest-blast-radius change made to this
repository so far — every build-relevant file is touched at once. The CI
pipeline exists specifically to answer "is this safe to merge" before
merging (that feature's own User Story 2) — not using it here, on the one
change that most needs it, would be the one case where skipping that
safety net actually matters. A broken push straight to `main` would leave
the repository's primary branch unbuildable until fixed; a broken PR
leaves `main` untouched the whole time.

**Alternatives considered**:
- *Push directly to `main`, matching how the Saga Orchestration and CI
  features were committed* — rejected specifically because of scale: both
  of those features only *added* files (new ports, a new workflow); this
  one *moves* every existing build-relevant file simultaneously, which is
  qualitatively riskier and is exactly the scenario a pull-request-plus-CI
  gate exists for.
