# Research: Reliable, Diagnosable End-to-End Tests in CI

No `NEEDS CLARIFICATION` markers were left in Technical Context. These are the
real decisions behind PR #10, each with the alternative that was rejected.
The plan was written after the implementation, but every entry below was
decided before the corresponding code was written.

## Decision 1: Browser output through `ITestOutputHelper`, not `Console`

**Decision**: Inject `ITestOutputHelper` into `CreateOrderE2ETests` and write
all browser events through a `Log()` helper: console messages, page errors,
failed requests, every `POST` response, and every response with status ≥ 400.

**Rationale**: xUnit v2 doesn't capture `Console.WriteLine`. Across the CI runs
inspected (Baseline), the existing `[browser:…]` lines appeared **0** times.
`dotnet test` prints `ITestOutputHelper` output under "Standard Output
Messages" for failed tests, so it shows exactly when needed and stays out of
passing logs (spec US1 scenario 5). Logging every `POST` lets the log tell
"the click never submitted" (no POST) apart from "the API rejected it"
(POST → 4xx/5xx).

**Alternatives considered**:
- *Raise `dotnet test` console verbosity*: rejected. It still doesn't capture
  `Console` under xUnit v2, and it would make passing logs noisy.
- *Migrate to xUnit v3*: rejected. That's a framework migration for the whole
  test suite to fix one test's logging (Principle IV).

## Decision 2: Playwright tracing on every run, uploaded only on failure

**Decision**: Create a browser *context* and start `Tracing` (screenshots,
snapshots, sources) in `InitializeAsync`. Stop it and save to
`bin/Debug/net10.0/playwright-traces/CreateOrderE2ETests.zip` in
`DisposeAsync`. In CI, upload that directory with
`actions/upload-artifact@v7` under `if: failure()`, `retention-days: 7`,
`if-no-files-found: ignore`.

**Rationale**: A trace covers screenshots, DOM snapshots, network and console
in one file that can be replayed in the browser (trace.playwright.dev). That
gives the most evidence per failure (FR-003). Saving it on every run avoids
having to detect test failure inside xUnit v2, which has no clean hook for
it. The cost is a ~1.4 MB local file. Uploading only on failure keeps the
artifact store clean (FR-004). `if-no-files-found: ignore` covers the case
where the fixture fails before any page exists (spec edge case).

**Alternatives considered**:
- *Screenshot only on failure*: rejected. A single image can't show the
  sequence (submit → reload) behind the observed flake.
- *Video recording*: rejected. It's heavier, and it has no network or
  console data.
- *Trace only on failure, via a custom xUnit attribute or `BeforeAfterTestAttribute`*:
  rejected. It's more machinery than the saving is worth (Principle IV).

## Decision 3: Wait for React hydration via the `__reactProps$` marker

**Decision**: Before filling the form, use `page.WaitForFunctionAsync` to wait
until the `<form>` element has an own property starting with `__reactProps`.
React attaches this to every host element it has hydrated. The wait is
bounded at 60 s, and its own `TimeoutException` message says the form never
became interactive (FR-006).

**Rationale**: The frontend runs under `next dev`. HTML arrives before the
client bundle loads and React hydrates. A click in that window triggers the
browser's native form submit, which reloads `/`, so `router.push` never runs.
That fits the Baseline failure exactly: a navigation timeout with no error.
The marker is the direct signal of "handlers attached". It needs **no change
to the frontend** (FR-013) and costs nothing when hydration is already done
(FR-007).

**Alternatives considered**:
- *A fixed delay*: rejected. It's either too short (flaky) or too long
  (wasted time), and it hides the real condition.
- *`WaitUntil = NetworkIdle`*: rejected. The dev server's HMR connection makes
  "network idle" unreliable as a hydration signal.
- *Add a `data-hydrated` attribute in the frontend*: rejected. It changes app
  code for a test, and the marker already exists. If React ever renames the
  key, the wait times out with its own clear message, which is visible and
  easy to fix.
- *Run the frontend as a production build*: deferred (spec Assumptions). It's
  the real fix for this whole class of problem, but it changes how the
  AppHost runs the frontend.

## Decision 4: Navigation timeout reports the URL and visible page text

**Decision**: Wrap `WaitForURLAsync("**/status/*")` in a `catch
(TimeoutException)` that rethrows with `page.Url` and
`page.Locator("main").InnerTextAsync()`.

**Rationale**: On any `POST /orders` error, the create page stays on `/` and
renders the message inside `<main>`. Including that text turns "timeout" into
"Request failed with status 500", or shows that the form was simply still
there (FR-002). This was verified in run 41465842762.

## Decision 5: Late browser events are swallowed in `Log()`

**Decision**: `Log()` catches `InvalidOperationException` from
`ITestOutputHelper.WriteLine`.

**Rationale**: Playwright raises events on its own threads. Console output
during teardown can arrive after xUnit has closed the test's output, and
xUnit then throws. That must never crash the run or flip its result (FR-008).
Losing those lines is acceptable.

## Decision 6: Latest Node 24-based majors of every action; Node 24 for the frontend

**Decision**:
- `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`,
  `actions/dependency-review-action@v5`, `actions/upload-artifact@v7`.
- `node-version: "24"` in both jobs that use Node.

**Rationale**: Every run carried "Node.js 20 is deprecated … forced to run on
Node.js 24" for the v4 actions. All the chosen majors declare
`using: node24`. Their release notes list nothing breaking for
GitHub-hosted runners beyond a minimum runner version (2.327.1), which
hosted runners already meet. Node 20 reached end of life in April 2026.
Node 24 is the active LTS, and Next.js 16 supports it (FR-010).

**Alternatives considered**:
- *The lowest Node 24-based majors (checkout v5 etc.)*: rejected. They'd need
  bumping again soon, and the newer majors add no breaking changes for us.
- *Node 22*: rejected. It's in maintenance LTS, and 24 is the active line.

## Decision 7: `npm ci --no-audit --no-fund` in both Node jobs

**Decision**: Replace `npm install` in the end-to-end job with
`npm ci --no-audit --no-fund`, and add the same flags to frontend-checks.

**Rationale**:
- `npm ci` installs exactly the lockfile, fails on a manifest/lockfile
  mismatch, and never rewrites the lockfile (FR-011).
- The step had grown from ~5 s to 30–68 s with identical inputs (same cache
  key, same 365 packages). The per-install audit is a network call that
  duplicates feature 005's Dependabot and Dependency review.
- Result: 8–9 s on PR #10 (SC-006).

## Decision 8: Demote ASPIRE010 to a message in the AppHost project

**Decision**: Add `<MSBuildWarningsAsMessages>$(MSBuildWarningsAsMessages);ASPIRE010</MSBuildWarningsAsMessages>`
to `OrderSystem.AppHost.csproj`, with a comment explaining why.

**Rationale**: ASPIRE010 comes from an MSBuild `<Warning>` task in
`Aspire.Hosting.AppHost.targets`, so `NoWarn` doesn't apply.
`MSBuildWarningsAsMessages` is MSBuild's mechanism for this. The setting the
warning complains about (`AspireUseCliBundle=false`) is the SDK default and
matches how the project has always run (`dotnet run` and
`Aspire.Hosting.Testing`, without the Aspire CLI). Verified locally: AppHost
builds with 0 warnings (FR-012).

**Alternatives considered**:
- *Set `AspireUseCliBundle=true`*: rejected. It switches how the dashboard
  and DCP are resolved, which changes runtime behavior to silence a warning
  (FR-013).
- *Put it in `Directory.Build.props`*: rejected. It only applies to the one
  AppHost project.
