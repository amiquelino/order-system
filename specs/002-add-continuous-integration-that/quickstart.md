# Quickstart: Continuous Integration

Validates all three user stories using a disposable scratch branch — no
changes to `main` needed.

## Prerequisites

- `.github/workflows/ci.yml` exists on the default branch (GitHub Actions
  only runs workflows that are present on the branch/ref being evaluated,
  or already merged to the base branch for a pull request's checks).
- A clone with push access to `github.com/amiquelino/order-system`.

## Scenario A — A clean push is built, tested, and reports success (User Story 1)

```bash
git checkout -b ci-quickstart-clean
git commit --allow-empty -m "ci quickstart: clean run"
git push -u origin ci-quickstart-clean
```

**Expected outcome**: within a few seconds, a run starts automatically
(visible under the repo's **Actions** tab and as a status indicator on the
commit) with no manual step taken to start it. All jobs
(`domain-and-application-tests`, `adapter-integration-tests`,
`end-to-end-tests`, `frontend-checks`) report success. Total wall-clock
time from push to every job finishing is under 15 minutes (SC-003).

## Scenario B — A pull request shows the result directly, and a single broken tier is identifiable (User Stories 2 and 3)

```bash
git checkout -b ci-quickstart-broken
# Break exactly one tier without touching anything else — e.g. a Domain.Tests assertion:
#   sed -i 's/Should().Be(OrderStatus.Confirmed)/Should().Be(OrderStatus.Cancelled)/' \
#     OrderSystem.Domain.Tests/<some existing passing test file>.cs
git commit -am "ci quickstart: break exactly one tier"
git push -u origin ci-quickstart-broken
gh pr create --fill --base main
```

**Expected outcome**:
- While jobs are running, the pull request shows an in-progress state, not
  an absent or stale one.
- Once finished, the pull request displays the overall result directly —
  no need to leave the PR page to learn it failed.
- The run's summary shows `domain-and-application-tests` (or whichever
  single job maps to the tier that was broken) as the failing job, visually
  distinct from the other jobs, which still report success — without
  opening that job's log.

## Cleanup

```bash
gh pr close ci-quickstart-broken --delete-branch 2>/dev/null
git push origin --delete ci-quickstart-clean
git branch -D ci-quickstart-clean ci-quickstart-broken
```

## Regression check

None — this feature adds no application code, so there's nothing in the
existing four test projects or the frontend's own checks for it to
regress. The scenarios above *are* the regression check: if any existing
test tier or the frontend build breaks, the relevant job in Scenario A
already reports that.
