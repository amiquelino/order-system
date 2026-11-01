---

description: "Task list for Handle NuGet Package Vulnerabilities"
---

# Tasks: Handle NuGet Package Vulnerabilities

**Input**: Design documents from `specs/005-handle-nuget-packages-vulnerabilities/`

**Prerequisites**: plan.md, spec.md, research.md, quickstart.md. There's no
data-model.md or contracts/: the feature has no entities and no new interface,
per plan.md.

**Tests**: No new `*.Tests` code. This feature changes dependency versions and
build configuration only, not application behavior. Its tests are:

- **US1**: every *existing* tier must stay green (FR-003). Per Constitution
  Principle III, each tier runs, because the bumped packages sit under
  different tiers: Testcontainers under the integration tier, Aspire and
  SignalR/Redis under the end-to-end tier.
- **US2**: the quickstart's deliberate-failure checks.

Each of these is tracked as its own task below.

**Organization**: Grouped by user story in spec priority order (US1 P1, US2 P2, US3 P3).

- **Ordering constraint**: US1 must land before US2's gate is switched on.
  `Directory.Build.props` (T012) turns NU1901–NU1904 into errors, so adding it
  while the Baseline findings still exist would break every restore.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1, US2). Omitted for Setup and Polish tasks.

---

## Phase 1: Setup

No new tools, packages, or projects are needed. The .NET 10 SDK's built-in
audit is all the tooling required (research Decision 2).

- [X] T001 Re-run the Baseline audit and confirm it still matches spec.md's Baseline table (3 advisories: MessagePack 2.5.302, SSH.NET 2025.1.0 × 2). Run `dotnet restore OrderSystem.slnx --force` and `dotnet list OrderSystem.slnx package --vulnerable --include-transitive`. If new advisories have appeared since 2026-11-01, add them to the Baseline before continuing.

---

## Phase 2: Foundational (Blocking Prerequisites)

None. No shared infrastructure blocks either story. The only cross-story
dependency is the ordering constraint above (US1 before T012).

---

## Phase 3: User Story 1 - Remove the currently known vulnerabilities (Priority: P1) 🎯 MVP

**Goal**: A solution-wide audit, including transitive packages, reports zero
known vulnerabilities, and all existing test tiers still pass (FR-001,
FR-002, FR-003).

**Independent Test**: quickstart.md §1 (clean audit plus `dotnet nuget why`
checks) and §2 (all tiers green).

### Implementation for User Story 1

Upgrade the parent packages, not transitive pins (research Decision 1):

- [X] T002 [P] [US1] In `tests/OrderSystem.Adapters.IntegrationTests/OrderSystem.Adapters.IntegrationTests.csproj`, bump `Testcontainers.Kafka`, `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq` 4.13.0 → 4.14.0. This moves SSH.NET to 2026.0.0 and fixes GHSA-mggc-4xg6-vcxf and GHSA-q939-rpr3-3284.
- [X] T003 [P] [US1] In `hosts/OrderSystem.NotificationService/OrderSystem.NotificationService.csproj`, bump `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.10 → 10.0.14. This moves MessagePack to 2.5.303 and fixes GHSA-qhrr-8q5h-9q3h.
- [X] T004 [P] [US1] In `hosts/OrderSystem.AppHost/OrderSystem.AppHost.csproj`, bump the `Aspire.AppHost.Sdk/13.4.6` SDK attribute **and** `Aspire.Hosting.{JavaScript,Kafka,PostgreSQL,RabbitMQ,Redis}` 13.4.6 → 13.6.4 in lockstep. This moves MessagePack to 2.5.307.
- [X] T005 [P] [US1] In `tests/OrderSystem.EndToEndTests/OrderSystem.EndToEndTests.csproj`, bump `Aspire.Hosting.Testing` 13.4.6 → 13.6.4 to match T004.

### Validation for User Story 1

- [X] T006 [US1] Run quickstart.md §1. `dotnet restore OrderSystem.slnx --force` should produce no NU1901–NU1904 output, `dotnet list … --vulnerable --include-transitive` should report no vulnerable packages for all 13 projects, and the `dotnet nuget why` checks should show SSH.NET 2026.0.0 and MessagePack ≥ 2.5.303 everywhere. Depends on T002–T005.
- [X] T007 [P] [US1] Domain + Application tier: `dotnet test tests/OrderSystem.Domain.Tests` and `dotnet test tests/OrderSystem.Application.Tests` pass. This is a sanity check, since neither project's packages changed. Depends on T006.
  - *2026-11-01:* Domain 21/21 and Application 36/36 passed. The full solution also builds with 0 errors; Aspire 13.6 adds one informational ASPIRE010 warning, because the SDK now defaults `AspireUseCliBundle=false`.
- [X] T008 [P] [US1] Adapter integration tier (Docker running): `dotnet test tests/OrderSystem.Adapters.IntegrationTests` passes on Testcontainers 4.14.0. Depends on T006.
  - *2026-11-01, CI on PR #4:* Adapter integration tests passed on Testcontainers 4.14.0 (not run locally, since Docker was unavailable).
- [X] T009 [P] [US1] End-to-end tier: `dotnet build tests/OrderSystem.EndToEndTests`, install Playwright Chromium if needed, then `dotnet test tests/OrderSystem.EndToEndTests` passes on Aspire 13.6.4 and SignalR.StackExchangeRedis 10.0.14. This is the regression guard for Principle V's live status updates. Depends on T006.
  - *2026-11-01, CI on PR #4:* End-to-end tests passed on Aspire 13.6.4 and SignalR.StackExchangeRedis 10.0.14 (~3m15s).
- [X] T010 [US1] **Conditional, only if T008 or T009 fails because of a bump** that isn't a trivial API fix: apply research Decision 6's fallback to *that finding only*. Revert its parent bump, and add a direct `MessagePack` (≥ 2.5.303) or `SSH.NET` (2026.0.0) `PackageReference` to the affected `.csproj`. Put an XML comment above it linking the GHSA and stating "remove once <parent> ≥ <version>". Re-run T006 and the failed tier. Record the outcome in research.md Decision 6 (FR-008). If T008 and T009 both passed, mark this task done with "not needed".
  - *Not needed:* T008 and T009 both passed, so no fallback pins were added.
- [ ] T011 [US1] Manual golden path (spec US1 scenario 3, quickstart.md §2): `dotnet run --project hosts/OrderSystem.AppHost`, place an order in the frontend, and watch its status update live through NotificationService.
  - *Open:* not run manually (no Docker locally). The automated End-to-end test (T009) drives the same golden path (create order → live `Confirmed` status over SignalR) and passed in CI.

**Checkpoint**: The audit is clean and all tiers are green. US1 is shippable on its own as the MVP.

---

## Phase 4: User Story 2 - Prevent vulnerable packages from silently returning (Priority: P2)

**Goal**: Restore fails locally and in CI whenever any project resolves a
package with a known advisory. The failure message names the project, package,
version and advisory. Accepting a specific advisory is possible but tightly
scoped (FR-004–FR-007).

**Independent Test**: quickstart.md §3 (a deliberately vulnerable reference
fails the build) and §4 (one suppression silences only its advisory).

### Implementation for User Story 2

- [X] T012 [US2] Create the repo-root `Directory.Build.props` (research Decision 2). In one `<PropertyGroup>`, set `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=low`, and `WarningsAsErrors=$(WarningsAsErrors);NU1901;NU1902;NU1903;NU1904`. Append to the property rather than overwrite it, so per-project values survive. Leave NU1900 as a warning (research Decision 5). Add a header comment explaining the policy and pointing to this spec. Depends on T006, because the gate can only be turned on against a clean graph.
- [X] T013 [US2] In `Directory.Build.props`, add a commented-out example `<ItemGroup>` showing the FR-007 escape hatch: `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-…" />`, preceded by the required justification comment format ("Accepted <date>: <why not exploitable / no fix>. Remove when <condition>."). Add a note that suppressions are per-advisory only, and that `NoWarn` on NU190x is not allowed (research Decision 3). Add no live suppressions. Same file as T012, so it runs sequentially.
- [X] T014 [P] [US2] In `.github/workflows/ci.yml`, add a fifth job `dependency-audit` (name: `Dependency audit`) on `ubuntu-latest`: `actions/checkout@v4`, `actions/setup-dotnet@v4` with `dotnet-version: "10.0.x"`, then `dotnet restore OrderSystem.slnx` (research Decision 4). It has no `needs:`, so it runs in parallel with the other four jobs. It can be done in parallel with T012/T013 because it's a different file.

### Validation for User Story 2

- [X] T015 [US2] With the gate on, confirm a clean graph passes (SC-004). `dotnet restore OrderSystem.slnx --force` and `dotnet build OrderSystem.slnx` should both succeed with no new errors. This also confirms that `WarningsAsErrors` didn't promote any unrelated existing warnings. Depends on T012–T013.
- [X] T016 [US2] Run quickstart.md §3 on a throwaway branch. Add `Newtonsoft.Json` 12.0.1 to `tests/OrderSystem.Domain.Tests/OrderSystem.Domain.Tests.csproj`; `dotnet restore` should **fail** with `error NU1903` naming the project, package, version and advisory URL (FR-005, FR-006). Push the branch and confirm the `Dependency audit` CI job fails (FR-004, SC-003). Depends on T014, T015.
  - *2026-11-01, local part verified:* restore failed with `error NU1903: Warning As Error: Package 'Newtonsoft.Json' 12.0.1 … GHSA-5crp-9r3c-p9vr`. The temporary change was reverted. The CI part still needs a pushed throwaway branch.
  - *2026-11-01, CI part:* throwaway draft PR #5 failed **Dependency audit** with `error NU1903 … Newtonsoft.Json 12.0.1 … GHSA-5crp-9r3c-p9vr`. (Unit tests also failed, as expected, because they restore the same project.) The PR was closed and the branch deleted.
- [X] T017 [US2] Run quickstart.md §4 on the same throwaway branch. Add a `NuGetAuditSuppress` for that one advisory and confirm restore passes. Add a second, different vulnerable package and confirm restore fails again for it only (FR-007). Delete the throwaway branch locally and on the remote. Depends on T016.
  - *2026-11-01, verified locally:* with GHSA-5crp-9r3c-p9vr suppressed, restore passed. Adding `System.Text.Json` 8.0.4 then failed restore with `NU1903 … GHSA-8g4q-xg66-9fp4` only. Temporary edits were reverted. The remaining step is cleaning up the throwaway branch, which depends on T016's CI part.
  - *Cleanup done:* throwaway branches deleted locally and on the remote.

**Checkpoint**: The gate is live locally and in CI, it's proven to fail on a vulnerable package and pass on a clean graph, and suppression is proven to be scoped.

---

## Phase 5: User Story 3 - Get notified of vulnerabilities and block critical ones on pull requests (Priority: P3)

**Goal**: Turn on GitHub vulnerability alerts and Dependabot security-fix PRs
for NuGet, npm and GitHub Actions, and add a PR check that fails on
**critical** advisories in any ecosystem. US2's NuGet gate stays
unchanged (FR-009–FR-012, research Decision 7).

**Independent Test**: quickstart.md §6.

### Implementation for User Story 3

- [X] T018 [P] [US3] Create `.github/dependabot.yml` (`version: 2`) with three `updates` entries, each `schedule.interval: weekly` and `open-pull-requests-limit: 0`, so security updates only and no routine version PRs (FR-010):
  - `package-ecosystem: nuget` with `directories: ["/src/**", "/hosts/**", "/tests/**"]`
  - `package-ecosystem: npm` with `directory: "/order-system-frontend"`
  - `package-ecosystem: github-actions` with `directory: "/"`

  Add a header comment explaining why the limit is 0, and pointing to research Decision 7.
- [X] T019 [US3] In `.github/workflows/ci.yml`, add a job `dependency-review` (name: `Dependency review`) on `ubuntu-latest` with `if: github.event_name == 'pull_request'`. Steps: `actions/checkout@v4`, then `actions/dependency-review-action@v4` with `fail-on-severity: critical` (FR-011). Don't set `needs:`. Add a comment noting that it sees only direct NuGet references, so the transitive NuGet gate is the `dependency-audit` job (FR-012). Same file as T014, so run it after T014.
- [X] T020 [US3] **Repo-admin action, needs the maintainer's go-ahead because it changes GitHub repository settings.** Enable vulnerability alerts and automated security fixes: `gh api -X PUT repos/amiquelino/order-system/vulnerability-alerts` then `gh api -X PUT repos/amiquelino/order-system/automated-security-fixes`. Confirm with the two `gh api` GET checks in quickstart.md §6 (FR-009, FR-010). This is independent of every file task, so it can be done any time.
  - *2026-11-01:* both `PUT`s returned 204; `vulnerability-alerts` GET → 204, `automated-security-fixes` → `{"enabled":true,"paused":false}`.

### Validation for User Story 3

- [X] T021 [US3] After T018 is pushed to `main` (Dependabot reads its config from the default branch), confirm that **Insights → Dependency graph → Dependabot** shows the nuget, npm and github-actions configs with no errors, and that no routine version-update PRs are opened (US3 scenario 5). Depends on T018, T020, and the feature being merged.
  - *2026-11-01, initially pending:* `security_and_analysis.dependabot_security_updates` = enabled, and 36 alerts are open (all npm, frontend; 5 critical in `next` 16.2.10, patched in 16.3.3–16.4.1). 30 minutes after the merge there was still no Dependabot run or PR, and no "Dependabot Updates" workflow was registered yet. Confirm the config in Insights → Dependency graph → Dependabot, and confirm a security-fix PR for `next` appears.
  - *2026-11-02, done:* the "Dependabot Updates" workflow registered about 2h after the merge. Dependabot opened security-fix PRs #7 (sharp + next) and #8 (next 16.2.10 → 16.4.1), both citing GHSA-673q-mjm6-r5xr and GHSA-2464-fhr4-w9xw. All CI checks are green on both. There were no routine version-update PRs (scenario 5). NuGet and github-actions have no open alerts, so they produced no PRs; their config entries can only be eyeballed in Insights → Dependency graph → Dependabot.
- [X] T022 [US3] Run quickstart.md §6 "Critical fails". On a throwaway branch, add `"minimist": "1.2.5"` (critical advisory GHSA-xvch-5gv4-984h) to `order-system-frontend/package.json`, run `npm install`, and open a PR to `main`. Confirm that **Dependency review** fails and names the package and advisory (FR-011, SC-005). Close the PR and delete the branch. Depends on T019.
  - *2026-11-01:* throwaway draft PR #6 failed **Dependency review**: "minimist@1.2.5 – Prototype Pollution in minimist (critical severity) ↪ GHSA-xvch-5gv4-984h". The PR was closed and the branch deleted.
- [X] T023 [US3] Run quickstart.md §6 "Below critical passes" and "The NuGet gate is unaffected". Confirm that an npm package with only a moderate or high advisory passes Dependency review but appears in its summary. Re-run T016's NuGet case and confirm that the high NuGet advisory still fails `Dependency audit` (FR-012). Depends on T022, T016.
  - *2026-11-01, PR #5:* **Dependency review** passed ("did not detect any vulnerable packages with severity level critical or higher") with lodash 4.17.20 (high) and Newtonsoft.Json 12.0.1 (high) added, while **Dependency audit** failed on the same Newtonsoft.Json. Both thresholds are confirmed independent (FR-012).

**Checkpoint**: Alerts and security-fix PRs are on for three ecosystems, critical advisories block PRs, and the NuGet gate is still strict.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T024 [P] In `README.md`, add a short "Dependency vulnerabilities" section covering:
  - Restore fails on any known NuGet advisory.
  - PRs fail on any **critical** advisory in NuGet, npm or GitHub Actions.
  - Dependabot alerts and security-fix PRs are on; routine version PRs are not.
  - To fix a NuGet advisory, upgrade the parent package first. Use a direct pin only as a fallback, with a removal comment.
  - To accept an advisory when no fix exists, add a `NuGetAuditSuppress` plus justification in `Directory.Build.props`.
- [X] T025 Push `005-handle-nuget-packages-vulnerabilities`, open the PR to `main`, and confirm all **six** CI jobs are green: Unit tests, Adapter integration tests, End-to-end tests, Frontend checks, Dependency audit, Dependency review. Dependency review runs only on the PR, not on the push (SC-002). Depends on T011, T017, T019, T024.
  - *2026-11-01:* PR #4 is green on all six jobs. Dependency review ran on the `pull_request` event and was skipped on `push`, as designed.
- [X] T026 Final full quickstart.md run-through (§1–§4, §6), then mark spec.md **Status** as Implemented. Run T021 after the merge.
  - *2026-11-01, on `main` after merging PR #4:* §1 `dotnet restore --force` gave 0 NU19xx errors, and `--vulnerable --include-transitive` reported 13/13 projects clean. Post-merge `main` CI succeeded. §3/§4 were verified locally and on PR #5; §6 on PRs #5/#6 (see T016, T017, T022, T023). Spec status set to Implemented.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: none.
- **US1 (T002–T011)**: after T001.
- **US2 (T012–T017)**: T012 needs T006 (clean audit). T014 can start any time,
  but CI only goes green on it once US1 is pushed.
- **US3 (T018–T023)**: independent of US1/US2 except that T019 edits the same
  `ci.yml` as T014 (run it after), and T023 reuses T016. T020 is a repo-settings
  change that needs the maintainer's go-ahead. T021 can only run after the merge
  to `main`.
- **Polish (T024–T026)**: T024 can be done any time. T025 and T026 come after all stories.

### Within Stories

- T002–T005 → T006 → {T007, T008, T009} → T010 (conditional) → T011
- T012 → T013 → T015 → T016 → T017, with T014 alongside T012/T013
- T018 ∥ T020; T014 → T019 → T022 → T023; T021 after the merge

### Parallel Opportunities

- **T002, T003, T004, T005**: four different `.csproj` files.
- **T007, T008, T009**: independent test tiers. T009 is by far the slowest.
- **T014** alongside **T012/T013**: `ci.yml` vs `Directory.Build.props`.
- **T018** (`dependabot.yml`) and **T020** (repo settings): independent of every other file.
- **T024**: README, independent of everything.

---

## Parallel Example: User Story 1

```bash
# Version bumps: four different files
Task: "T002 Testcontainers 4.13.0 → 4.14.0 in tests/OrderSystem.Adapters.IntegrationTests/…csproj"
Task: "T003 SignalR.StackExchangeRedis 10.0.10 → 10.0.14 in hosts/OrderSystem.NotificationService/…csproj"
Task: "T004 Aspire SDK + Aspire.Hosting.* 13.4.6 → 13.6.4 in hosts/OrderSystem.AppHost/…csproj"
Task: "T005 Aspire.Hosting.Testing 13.4.6 → 13.6.4 in tests/OrderSystem.EndToEndTests/…csproj"

# After T006, run the tiers together
Task: "T007 Domain + Application tests"
Task: "T008 Adapter integration tests"
Task: "T009 End-to-end tests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001: confirm the Baseline.
2. T002–T011: bump the parent packages, verify the audit is clean and all tiers are green.
3. **STOP and VALIDATE**: The current exposure is gone, and US1 can merge on its own.

### Incremental Delivery

1. US1 → clean audit (MVP; removes the real exposure today).
2. US2 → gate plus CI job (keeps it clean).
3. US3 → Dependabot alerts/security PRs for NuGet + npm + Actions, and a critical-only PR check.
4. Polish → README, six green CI jobs, quickstart sign-off.

---

## Notes

- Highest-risk task: **T004/T009**, the Aspire minor-version jump. T010 is the pre-agreed fallback, so don't improvise a different one.
- Never turn on the gate (T012) before the audit is clean (T006). Every restore would fail.
- Don't use `NoWarn` for NU1901–NU1904 anywhere. It suppresses by severity, not by advisory, and would violate FR-007.
- T020 changes GitHub repository settings. Confirm with the maintainer before running it.
- Commit after each logical group: US1 bumps, US2 gate + CI, US3 Dependabot + dependency review, README.
