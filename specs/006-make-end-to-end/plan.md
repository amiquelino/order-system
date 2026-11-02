# Implementation Plan: Reliable, Diagnosable End-to-End Tests in CI

**Branch**: `e2e-diagnostics-and-ci-hygiene` (PR #10) | **Date**: 2026-11-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-make-end-to-end/spec.md`

**Note**: This template is filled in by the `/speckit-plan` skill; its definition describes the execution workflow. This plan was written **after** the implementation in PR #10, to document the design and its constitution check. It describes the code as merged in that PR.

## Summary

The end-to-end test failed once in 29 CI runs, and the run kept no evidence of
why. This feature does three things:

- **Failures explain themselves (US1).** Browser output goes through xUnit's
  test output so it reaches the CI log. Every run records a Playwright trace,
  and CI uploads it on failure. The navigation timeout now reports the page's
  URL and text.
- **The likely race is removed (US2).** The test waits for React to hydrate
  the create-order form before it interacts with it.
- **CI is cleaned up (US3).** Actions move to Node 24-based majors, the
  frontend runtime moves to Node 24, installs use `npm ci --no-audit
  --no-fund`, and the known-harmless ASPIRE010 warning is demoted.

No application code changes. See [research.md](./research.md) for the eight
decisions.

## Technical Context

**Language/Version**: C# / .NET 10 (test code); YAML (GitHub Actions); Node.js 24 (frontend runtime in CI)

**Primary Dependencies**: Microsoft.Playwright 1.61.0 (tracing, `WaitForFunctionAsync`), xUnit 2.9.3 (`ITestOutputHelper`), Aspire.Hosting.Testing 13.6.4 (unchanged); `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`, `actions/upload-artifact@v7`, `actions/dependency-review-action@v5`

**Storage**: N/A. The trace zip is a local build-output file, plus a CI artifact with 7-day retention.

**Testing**: The end-to-end tier itself. This feature *is* test infrastructure, so it's validated by (a) a passing CI run (US2, US3) and (b) one deliberately failing throwaway commit (US1 scenario 4; quickstart §2).

**Target Platform**: GitHub Actions `ubuntu-latest` and local dev runs of `OrderSystem.EndToEndTests`

**Project Type**: Changes to an existing test project + CI workflow + one build property. No new projects, ports or adapters.

**Performance Goals**: The passing end-to-end test step stays within ~70–130 s (SC-004); the frontend install takes ≤ 15 s (SC-006).

**Constraints**: No change to frontend or backend application code (FR-013). Diagnostics stay inside the CI run, with no external services.

**Scale/Scope**: 1 test class rewritten (`CreateOrderE2ETests.cs`); 1 workflow edited (`ci.yml`, all six jobs); 1 csproj property (`OrderSystem.AppHost`); README paragraph.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Hexagonal Boundaries Are Inviolable**: ✅ Not affected. No change to `Domain`, `Application` or any adapter.
- **II. Ports Are Discovered From the Core, Not the Adapter**: ✅ Not affected. No ports.
- **III. The Test Pyramid Mirrors the Architecture**: ✅ Consistent. The work
  stays inside the end-to-end tier, which is the only tier that can see this
  class of problem (frontend hydration in the real composed system). The fix
  makes that tier more trustworthy rather than adding tests elsewhere. No test
  is added at another layer, because the flake isn't reproducible below
  end-to-end.
- **IV. No Speculative Abstraction**: ✅ Consistent.
  - The one helper (`Log()`) exists because of a concrete threading hazard
    (research Decision 5).
  - The trace is saved unconditionally rather than through a custom xUnit
    failure hook (Decision 2).
  - The hydration wait reuses React's own marker instead of adding a
    test-only attribute to the app (Decision 3).
  - The production-build frontend and the Ubuntu migration were deliberately
    left out (spec Assumptions).
- **V. Every State Transition Announces Itself**: ✅ Strengthened, not
  changed. The test still asserts the live `Confirmed` update and the SignalR
  `WebSocket connected` message. US2 removes a false failure on the path that
  guards this principle.
- **VI. Match the Project's Existing Idiom**: ✅ Consistent. It uses each
  framework's built-in mechanism:
  - xUnit's `ITestOutputHelper`
  - Playwright's own tracing and viewer
  - GitHub's own artifact action
  - MSBuild's `MSBuildWarningsAsMessages`

  No third-party reporting or flake-tracking service is added. The tradeoff
  is recorded in research Decisions 1, 2 and 8.

**Result**: No violations. Complexity Tracking omitted.

*Post-Phase 1 re-check*: Phase 1 produced only a quickstart; there's no data
model and no contracts. Nothing changed, and there are still no violations.

## Project Structure

### Documentation (this feature)

```text
specs/006-make-end-to-end/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: 8 decisions (output helper, tracing, hydration wait, timeout text,
│                        #          late events, action/Node versions, npm ci, ASPIRE010)
├── quickstart.md        # Phase 1: passing-run check, deliberate-failure check, warnings/timing check
└── tasks.md             # Phase 2 (/speckit-tasks)
```

There's no `data-model.md` (no entities or state) and no `contracts/` (no new
endpoint, message or port).

### Source Code (repository root)

```text
tests/OrderSystem.EndToEndTests/
└── CreateOrderE2ETests.cs          # EDIT: ITestOutputHelper + Log(); browser context + tracing
                                    #       (save in DisposeAsync); hydration wait with own timeout
                                    #       message; navigation timeout → URL + <main> text;
                                    #       log POST and ≥400 responses

.github/workflows/ci.yml            # EDIT: checkout v7 / setup-dotnet v6 / setup-node v7 /
                                    #       dependency-review v5 (all jobs); node 24;
                                    #       npm ci --no-audit --no-fund (e2e + frontend-checks);
                                    #       upload-artifact v7 step `if: failure()` in e2e job

hosts/OrderSystem.AppHost/
└── OrderSystem.AppHost.csproj      # EDIT: MSBuildWarningsAsMessages += ASPIRE010, with rationale

README.md                           # EDIT: E2E section, where the trace is and how to open it

order-system-frontend/**, src/**, hosts/OrderSystem.{Api,NotificationService,MigrationService},
tests/OrderSystem.{Domain,Application}.Tests, tests/OrderSystem.Adapters.IntegrationTests,
tests/OrderSystem.EndToEndTests/AppHostFixture.cs     # UNCHANGED
```

**Structure Decision**: All test-side changes are confined to the one
end-to-end test class. `AppHostFixture` is deliberately untouched, including
its stale "Turbopack" comment, which is out of scope per the spec. CI changes
are in the existing single workflow file, and the build-warning change is in
the one project that raises it.
