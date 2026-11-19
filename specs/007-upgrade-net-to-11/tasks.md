---

description: "Task list for Upgrade .NET to 11"
---

# Tasks: Upgrade .NET to 11

**Input**: Design documents from `specs/007-upgrade-net-to-11/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
quickstart.md. There's no `contracts/`: the feature exposes no new interface.

**Tests**: No new `*.Tests` code. The feature changes runtime and dependency
versions only (FR-011), and the spec asks for no new tests. Its proof is
that every *existing* tier passes on .NET 11 (FR-008). Per Constitution
Principle III, each tier is its own validation task, because the upgrade can
break each one differently:

- **Domain and Application**: BCL behavior.
- **Adapters.IntegrationTests**: EF Core 10 + Npgsql, Kafka and RabbitMQ
  clients on the new runtime.
- **EndToEndTests**: Aspire, SignalR 11 with the Redis backplane, and
  Wolverine runtime codegen.

If a regression only shows up at the E2E tier, the spec's edge case adds a
test at the layer where it could recur.

**Organization**: Grouped by user story in spec priority order (US1 P1,
US2 P2, US3 P3).

- **Ordering constraint**: Phase 2 has to leave the build green before
  US1 starts. NU1901–NU1904 are errors (spec 005), and `main` currently fails
  restore on the Aspire 13.6.4 and SignalR 10.0.14 advisories. Without
  Phase 2, every US1 build failure would be mixed up with those.
- **CI belongs to US1, not US3.** Once projects target `net11.0`, a CI
  runner with the .NET 10 SDK can't build them, so US1's independent test
  ("every CI job passes") needs CI on .NET 11. US3 is documentation only.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1–US3). Omitted for Setup, Foundational and Polish tasks.

---

## Phase 1: Setup

**Purpose**: Toolchain and a recorded baseline to compare against.

- [X] T001 Install the .NET 11 SDK (11.0.100 or a later 11.0.1xx) on the dev machine and confirm with `dotnet --list-sdks`. Keep the .NET 10 SDK too, for the FR-002 negative check in T008 — *Done 2026-11-19: SDK 11.0.100 used from a session-local install; .NET 10.0.302 still present.*
- [X] T002 Record the baseline test counts per tier from the latest green `main` CI run (or a local `dotnet test` per project on `main`). Note them for SC-002 in the PR description: Domain.Tests (21), Application.Tests (36), Adapters.IntegrationTests (n), EndToEndTests (n) — *Baseline (CI run 42130608907, `main` @ ee99873): Domain 21, Application 36, Adapters.IntegrationTests 9, EndToEndTests 1.*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Get a green build to start from. Without it, upgrade failures and unrelated advisory failures can't be told apart (spec Assumptions; plan "Sequencing note").

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Check whether the .NET 10 security fix (Aspire 13.6.5 + Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.15) has merged to `main`. If so, `git rebase origin/main` and skip T004–T005 — *Not merged (PR #16 still open); T004–T005 applied here.*
- [X] T004 [P] *(only if T003 found no fix)* Bump `Aspire.AppHost.Sdk/13.6.4` → `13.6.5` and `Aspire.Hosting.{JavaScript,Kafka,PostgreSQL,RabbitMQ,Redis}` 13.6.4 → 13.6.5 in `hosts/OrderSystem.AppHost/OrderSystem.AppHost.csproj`, and `Aspire.Hosting.Testing` 13.6.4 → 13.6.5 in `tests/OrderSystem.EndToEndTests/OrderSystem.EndToEndTests.csproj` (research Decision 3: not 17.0)
- [X] T005 [P] *(only if T003 found no fix)* Bump `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.14 → 10.0.15 in `hosts/OrderSystem.NotificationService/OrderSystem.NotificationService.csproj`. This is the GHSA-wr8v-9x5q-ggcm fix while still on net10.0. T018 takes it to 11.0.0
- [X] T006 `dotnet build OrderSystem.slnx` on the .NET 10 SDK succeeds with the audit gate on (0 errors). Commit as "Fix Aspire/SignalR advisories (prerequisite for 007)" if T004–T005 ran

**Checkpoint**: The build is green on .NET 10. Upgrade work can begin.

---

## Phase 3: User Story 1 - The whole solution builds, tests, and runs on .NET 11 (Priority: P1) 🎯 MVP

**Goal**: All 13 projects target `net11.0`. The SDK is pinned. CI builds on .NET 11 and every tier passes.

**Independent Test**: With only the .NET 11 SDK, build and run every CI job, and all pass. Start the AppHost and complete the golden path (quickstart §1–3, §5, §7).

### Implementation for User Story 1

- [ ] T007 [US1] Create `global.json` at the repo root: `{ "sdk": { "version": "11.0.100", "rollForward": "latestFeature" } }` (research Decision 1; FR-002)
- [ ] T008 [US1] Verify FR-002: with a .NET 10-only SDK, `dotnet build OrderSystem.slnx` fails immediately with "A compatible .NET SDK was not found. Requested SDK version: 11.0.100" (quickstart §1). One way: run the 10.0.x SDK's `dotnet` binary directly
- [ ] T009 [P] [US1] Change `<TargetFramework>net10.0</TargetFramework>` → `net11.0` in the core and adapter projects: `src/Domain/OrderSystem.Domain/OrderSystem.Domain.csproj`, `src/Application/OrderSystem.Application/OrderSystem.Application.csproj`, `src/Adapters/OrderSystem.Adapters.Postgres/OrderSystem.Adapters.Postgres.csproj`, `src/Adapters/OrderSystem.Adapters.Kafka/OrderSystem.Adapters.Kafka.csproj`, `src/Adapters/OrderSystem.Adapters.RabbitMQ/OrderSystem.Adapters.RabbitMQ.csproj`. Domain and Application change only their TFM: no package references added (FR-012)
- [ ] T010 [P] [US1] Change `net10.0` → `net11.0` in the hosts: `hosts/OrderSystem.Api/OrderSystem.Api.csproj`, `hosts/OrderSystem.NotificationService/OrderSystem.NotificationService.csproj`, `hosts/OrderSystem.MigrationService/OrderSystem.MigrationService.csproj`, `hosts/OrderSystem.AppHost/OrderSystem.AppHost.csproj`
- [ ] T011 [P] [US1] Change `net10.0` → `net11.0` in the test projects: `tests/OrderSystem.Domain.Tests/OrderSystem.Domain.Tests.csproj`, `tests/OrderSystem.Application.Tests/OrderSystem.Application.Tests.csproj`, `tests/OrderSystem.Adapters.IntegrationTests/OrderSystem.Adapters.IntegrationTests.csproj`, `tests/OrderSystem.EndToEndTests/OrderSystem.EndToEndTests.csproj`
- [ ] T012 [US1] Fix the net11.0-only NU1510 warnings (research Decision 4) by removing the `Microsoft.Extensions.DependencyInjection.Abstractions` PackageReference from `src/Adapters/OrderSystem.Adapters.Kafka/OrderSystem.Adapters.Kafka.csproj` and the `Microsoft.Extensions.Hosting.Abstractions` PackageReference from `src/Adapters/OrderSystem.Adapters.RabbitMQ/OrderSystem.Adapters.RabbitMQ.csproj`. Then `dotnet build OrderSystem.slnx` shows `0 Warning(s)`, `0 Error(s)` (quickstart §3)
- [ ] T013 [US1] In `.github/workflows/ci.yml`, replace `dotnet-version: "10.0.x"` with `global-json-file: global.json` in all 4 `actions/setup-dotnet@v6` steps (unit, integration, end-to-end, dependency-audit jobs). Change both hard-coded Playwright paths `bin/Debug/net10.0/` → `bin/Debug/net11.0/` (the `playwright.ps1 install` step and the `playwright-traces` artifact path). FR-009
- [ ] T014 [P] [US1] Validate the unit tiers: `dotnet test tests/OrderSystem.Domain.Tests` and `dotnet test tests/OrderSystem.Application.Tests` both report `(net11.0)`, 0 failed, 0 skipped, counts ≥ T002 baseline
- [ ] T015 [P] [US1] Validate the adapter tier (needs Docker): `dotnet test tests/OrderSystem.Adapters.IntegrationTests`, 0 failed, 0 skipped, count ≥ baseline. This is the first real-database run of EF Core 10 + Npgsql on the .NET 11 runtime (research "Risks")
- [ ] T016 [US1] Validate the end-to-end tier (needs Docker): `dotnet test tests/OrderSystem.EndToEndTests`, 0 failed, 0 skipped, count ≥ baseline. This is the first run of Aspire 13.6.5 orchestrating net11.0 hosts and of Wolverine runtime codegen at host startup (research "Risks")
- [ ] T017 [US1] Golden path by hand (quickstart §7): `dotnet run --project hosts/OrderSystem.AppHost`, place an order in the frontend, and confirm the final status, a live status update without a refresh, and the notification

**Checkpoint**: The solution is on .NET 11, and every tier passes locally. Push the branch and confirm all CI jobs are green on the .NET 11 SDK.

---

## Phase 4: User Story 2 - Dependencies move to their .NET 11 line where a stable release exists (Priority: P2)

**Goal**: Framework-aligned packages on stable 11.x. The one held-back group recorded with its exit condition. No prereleases. Audit clean.

**Independent Test**: Every framework-aligned package is either on stable 11.x or carries a recorded reason and exit condition. 0 prerelease references. The audit is clean (quickstart §3, §4).

### Implementation for User Story 2

- [ ] T018 [P] [US2] Bump `Microsoft.AspNetCore.SignalR.StackExchangeRedis` → `11.0.0` in `hosts/OrderSystem.NotificationService/OrderSystem.NotificationService.csproj` (FR-003)
- [ ] T019 [US2] In `hosts/OrderSystem.MigrationService/OrderSystem.MigrationService.csproj`, bump `Microsoft.Extensions.Hosting` 10.0.10 → `11.0.0` (FR-003) and `Microsoft.EntityFrameworkCore.Design` 10.0.4 → `10.0.15` (research Decision 2)
- [ ] T020 [US2] In `src/Adapters/OrderSystem.Adapters.Postgres/OrderSystem.Adapters.Postgres.csproj`:
  - Bump `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Design` 10.0.4 → `10.0.15`.
  - Leave `Npgsql.EntityFrameworkCore.PostgreSQL` at 10.0.3.
  - Above these references, add a comment recording the FR-006 exception: the reason (the provider has only 11.0.0-rc.3, and 10.0.3 requires EF Core `[10.0.4, 11.0.0)`), the exit condition (move EF Core, .Design, dotnet-ef and the provider to 11.x together once Npgsql.EntityFrameworkCore.PostgreSQL ships stable 11.0.0), and a link to `specs/007-upgrade-net-to-11/research.md` Decision 2
- [ ] T021 [P] [US2] Bump the `dotnet-ef` local tool 10.0.10 → `10.0.15` in `.config/dotnet-tools.json` so it matches EF Core (research Decision 2). Then `dotnet tool restore`
- [ ] T022 [US2] EF model hasn't drifted (quickstart §6): `dotnet ef migrations has-pending-model-changes --project src/Adapters/OrderSystem.Adapters.Postgres --startup-project hosts/OrderSystem.MigrationService` reports "No changes have been made to the model since the last migration."
- [ ] T023 [US2] Audit and prerelease checks (quickstart §3–4):
  - `dotnet build OrderSystem.slnx` gives 0 warnings and 0 errors with the gate on.
  - The quickstart §4 grep finds no prerelease version.
  - `git diff main -- Directory.Build.props` adds no `NuGetAuditSuppress` or `NoWarn` (FR-005, FR-007, SC-003, SC-004)
- [ ] T024 [US2] Re-run T014–T016 (all four tiers) after the package bumps. SignalR 11 and EF 10.0.15 sit under the E2E and adapter tiers respectively

**Checkpoint**: The dependency set is final. CI is green.

---

## Phase 5: User Story 3 - Contributors are told the new version (Priority: P3)

**Goal**: The README prerequisites and stack description say .NET 11.

**Independent Test**: Following only the README prerequisites, the listed SDK builds and runs the solution (quickstart §8, SC-005).

### Implementation for User Story 3

- [ ] T025 [P] [US3] In `README.md`:
  - Change the stack table row `C# / .NET 10` → `C# / .NET 11`.
  - Change the prerequisite `[.NET 10 SDK]` → `[.NET 11 SDK]`.
  - Add one sentence saying `global.json` pins the SDK feature band.
  - Change any other current-state `.NET 10` mention. Leave historical references in `specs/001`–`006` alone, since they record past decisions
- [ ] T026 [US3] Verify quickstart §8: `grep -nE 'dotnet-version|net10\.0' .github/workflows/ci.yml` returns nothing, and `grep -nE '\.NET 1[01]' README.md` shows only .NET 11

**Checkpoint**: All user stories are complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T027 Run the full `specs/007-upgrade-net-to-11/quickstart.md` (§1–§8) top to bottom and record the result of each step in the PR description, including the per-tier counts compared with the T002 baseline (SC-002)
- [ ] T028 Update `specs/007-upgrade-net-to-11/spec.md` **Status** to Implemented, with the PR number and merge commit, following specs 005 and 006

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: depends on Setup. **Blocks all stories.**
- **US1 (Phase 3)**: depends on Phase 2.
- **US2 (Phase 4)**: depends on US1. SignalR 11.0.0 and
  Microsoft.Extensions.Hosting 11.0.0 need `net11.0` TFMs to restore.
- **US3 (Phase 5)**: depends on US1 (it documents what US1 pins). It can
  run in parallel with US2.
- **Polish (Phase 6)**: depends on all stories.

### Within User Story 1

- T007 comes before T008, T012 and the validations. With `global.json`
  present, every later command uses the .NET 11 SDK.
- T009, T010 and T011 touch different files and run in parallel. T012
  edits two of T009's files, so it runs after T009.
- T013 (CI) can be written any time after T007, but it only proves itself
  on push, after T009–T012.
- T014 and T015 run in parallel. T016 and T017 need the full build and
  Docker.

### Parallel Opportunities

- Phase 2: T004 ∥ T005 (different csproj).
- US1: T009 ∥ T010 ∥ T011. T014 ∥ T015.
- US2: T018 ∥ T021. T019 and T020 run in sequence after T018, and both
  touch EF `.Design`, so keep their versions in step.
- US3 ∥ US2 once US1 is done.

## Parallel Example: User Story 1

```bash
# Retarget all 13 projects at once (three disjoint file sets):
Task: "T009 TFM net11.0 in src/Domain, src/Application, src/Adapters/*"
Task: "T010 TFM net11.0 in hosts/*"
Task: "T011 TFM net11.0 in tests/*"

# Then validate the two tiers that don't depend on each other:
Task: "T014 dotnet test Domain.Tests + Application.Tests"
Task: "T015 dotnet test Adapters.IntegrationTests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 → Phase 2 (green on .NET 10).
2. Phase 3 (US1): the solution runs on .NET 11 with CI green. This alone
   satisfies the feature's core ask. The remaining 10.x framework packages
   still work, because they resolve on net11.0, as the spike showed.
3. **Stop and validate**: quickstart §1–3, §5, §7.

### Incremental Delivery

1. US1, then push. CI on .NET 11 is the first real check of the
   container-backed tiers (research "Risks").
2. US2: package alignment, then re-run all tiers (T024).
3. US3: README.
4. Polish: full quickstart, then open the PR.

---

## Notes

- 28 tasks. No new test code, per the **Tests** note above.
- If T015 or T016 fails, reopen the research decision it touches. Don't
  loosen or skip tests (FR-008).
- Commit after each phase. Suggested messages: "Retarget solution to .NET 11
  (007 US1)", "Align packages with .NET 11 (007 US2)", "Document .NET 11
  prerequisite (007 US3)".
