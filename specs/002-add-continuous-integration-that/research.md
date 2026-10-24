# Research: Continuous Integration

No `NEEDS CLARIFICATION` markers were left in Technical Context. Recorded
here anyway, since each of these is a real decision with a rejected
alternative, not a default.

## Decision 1: GitHub Actions as the CI platform

**Decision**: GitHub Actions, triggered natively on `push` and
`pull_request`.

**Rationale**: The repository already lives on GitHub
(`github.com/amiquelino/order-system`). GitHub Actions needs no new
account, no secret-sharing setup between a third-party service and the
repo, and is free with unlimited minutes for public repositories. This is
Constitution Principle VI applied directly: prefer what's already there
over adding an external dependency that buys nothing extra here.

**Alternatives considered**:
- *A third-party CI SaaS (CircleCI, Travis CI, etc.)* — rejected: would
  need its own account and a GitHub-to-third-party auth/webhook connection
  for identical functionality GitHub Actions already provides natively.
- *Azure Pipelines* — rejected for the same reason; also a second
  Microsoft-adjacent surface to maintain alongside Aspire/.NET for no
  stated benefit.

## Decision 2: One job per test tier, not one job with sequential steps

**Decision**: `.github/workflows/ci.yml` defines separate jobs —
`domain-and-application-tests`, `adapter-integration-tests`,
`end-to-end-tests`, `frontend-checks` — running in parallel, each doing its
own checkout and restore.

**Rationale**: GitHub's run-summary UI reports each *job's* pass/fail
independently and by name; steps within a single job are only reported as
one pass/fail for the whole job. FR-005 and SC-004 both require identifying
which tier failed from the summary alone, without opening a log — that's a
job-level guarantee, not a step-level one. Running the jobs in parallel
(rather than making the heavier ones depend on the fast ones finishing
first) is what makes SC-003's 15-minute budget realistic: the slowest tier,
`end-to-end-tests`, already takes several minutes on its own per this
project's own documentation; chaining it after the others would only add
their time on top for no benefit, since this is a public repo with free,
unlimited Actions minutes — there's no cost reason to fail fast and save
runner time here.

**Alternatives considered**:
- *One job, one step per tier, running sequentially* — rejected: a failed
  step still only shows as "job failed" in the run summary; identifying
  which step requires opening that job's log, which is exactly what FR-005
  says not to require. Sequential execution also stacks every tier's
  duration into one wall-clock total, working against SC-003.
- *Fail-fast: only run integration/E2E jobs if the unit-test job succeeds*
  — rejected: would save runner time, which matters when minutes are
  metered, but this repo's Actions minutes aren't (public repo, free tier).
  Trading SC-003's speed for a savings that doesn't apply here isn't worth
  the added pipeline complexity (job dependencies, conditional `needs:`).

## Decision 3: Hosted `ubuntu-latest` runners, not self-hosted

**Decision**: Every job runs on GitHub's hosted `ubuntu-latest` runner.

**Rationale**: FR-006 requires the run to provision everything it needs
automatically, with "no dependency on something being pre-installed on a
persistent machine." A self-hosted runner is, by definition, a persistent
machine someone has to keep configured — exactly what FR-006 rules out.
Hosted runners also come with Docker already running (Testcontainers needs
nothing extra) and real `sudo`/`apt` access, unlike the local WSL
development environment used earlier in this project, where a Homebrew/glibc
version mismatch needed a one-off manual workaround (see this repo's commit
history) — that workaround was specific to that one machine's unusual
software mix and isn't expected to recur on a clean, disposable runner.

**Alternatives considered**:
- *A self-hosted runner* — rejected per FR-006 above. Would also reintroduce
  exactly the kind of host-specific library drift (Homebrew vs. system
  glibc) this project already hit once locally — a clean, disposable hosted
  runner avoids that class of problem entirely rather than needing it
  debugged again.

## Decision 4: Playwright's browser install uses `--with-deps` on the runner

**Decision**: The `end-to-end-tests` job installs Chromium via
`--with-deps` (either `Microsoft.Playwright.Program.Main(["install",
"--with-deps", "chromium"])` or the generated `playwright.sh`, per the
approach `README.md`'s "End-to-end test" section already documents for
local development).

**Rationale**: `--with-deps` runs `apt-get install` for every OS library
Chromium needs (including `libasound2`), which requires real root — exactly
what a hosted Actions runner has and this project's local WSL dev
environment didn't (no passwordless `sudo`, per `README.md`'s existing
caveat). On the hosted runner, this should just work without the manual
`.deb`-extraction workaround this session needed locally.

**Alternatives considered**:
- *Reuse the local workaround (extract a matching `.deb` into a scratch
  directory, point `LD_LIBRARY_PATH` at it)* — rejected: that workaround
  existed only to route around a Homebrew-vs-system-glibc conflict unique
  to one local machine. A hosted runner has no Homebrew and no such
  conflict; carrying the workaround forward would be solving a problem the
  runner doesn't have.
