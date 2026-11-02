# Quickstart: Validating Reliable, Diagnosable E2E Tests

## Prerequisites

- Push access to the repository (the checks run in GitHub Actions)
- `gh` CLI, authenticated
- Optional, for local runs: Docker, Node 24, .NET 10 SDK, and Playwright
  Chromium (see README "Automated tests")

## 1. Passing run: US2, US3 (SC-004 to SC-007)

Push the branch, or open or update its PR, and wait for CI.

**Expected**:
- All jobs pass: Unit tests, Adapter integration tests, End-to-end tests,
  Dependency audit, Frontend checks, and Dependency review on PRs.
- The end-to-end test step stays within ~70–130 s.
- The run has **no** `playwright-trace` artifact, since it only uploads on
  failure.

Check the warnings and the install time:

```bash
RUN=$(gh run list --branch <branch> --limit 1 --json databaseId --jq '.[0].databaseId')
for J in $(gh api repos/amiquelino/order-system/actions/runs/$RUN/jobs --jq '.jobs[].id'); do
  gh api repos/amiquelino/order-system/check-runs/$J/annotations --jq '.[] | "\(.annotation_level): \(.message[0:100])"'
done
```

**Expected**: no "Node.js 20 is deprecated" annotation and no ASPIRE010
annotation. An informational notice about the Ubuntu runner migration is fine.
In the end-to-end job, "Install frontend dependencies" takes ≤ 15 s.

## 2. Deliberate failure: US1 (SC-001, SC-002)

On a throwaway branch off the feature branch, change the unit price the test
enters in `tests/OrderSystem.EndToEndTests/CreateOrderE2ETests.cs` from `"500"`
to `"-5"`. The form's `min={0}` validation then blocks the submit, with no app
change. Push the branch; CI runs on push.

**Expected**:
- The **End-to-end tests** job fails, and every other job passes.
- The failure reads `Never navigated to /status/* after submitting the order;
  still on 'http://localhost:<port>/'. Page content: Create Order …`.
- "Standard Output Messages" shows the `[browser:…]` console lines and the
  `Playwright trace saved to …` line. No `[browser:response] POST …` line
  appears, because the click never submitted.
- The run has a `playwright-trace` artifact with 7-day retention:

```bash
gh run download <run-id> -n playwright-trace -D ./trace
```

  It contains `CreateOrderE2ETests.zip`. Open it at https://trace.playwright.dev
  and step through: filling the form, the click, and the page staying on `/`.

Then delete the throwaway branch:

```bash
git push origin --delete <throwaway-branch>
```

*Verified 2026-11-02 in run 41465842762, as recorded in spec.md Status.*

## 3. Ongoing: SC-003

Over the next ≥ 20 end-to-end runs on any branch, the "never reached the order
status page" failure shouldn't recur. If any end-to-end run fails, its log and
trace should show the cause without a local reproduction.
