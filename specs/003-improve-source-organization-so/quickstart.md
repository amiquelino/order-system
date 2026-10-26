# Quickstart: Improve Source Organization

Validates both user stories and FR-002's zero-regression requirement,
executed through a real branch and pull request (research.md Decision 3)
rather than directly against `main`.

## Prerequisites

- A clean working tree on `main`, up to date with `origin/main`.
- The CI pipeline from the previous feature already merged and green on
  `main` (it is).

## Steps

```bash
git checkout -b improve-source-organization
```

1. Perform every move from `data-model.md`'s move table with `git mv`
   (preserves rename history), e.g.:

   ```bash
   mkdir -p src/Domain src/Application src/Adapters hosts tests
   git mv OrderSystem.Domain src/Domain/OrderSystem.Domain
   git mv OrderSystem.Application src/Application/OrderSystem.Application
   git mv OrderSystem.Adapters.Postgres src/Adapters/OrderSystem.Adapters.Postgres
   git mv OrderSystem.Adapters.Kafka src/Adapters/OrderSystem.Adapters.Kafka
   git mv OrderSystem.Adapters.RabbitMQ src/Adapters/OrderSystem.Adapters.RabbitMQ
   git mv OrderSystem.Api hosts/OrderSystem.Api
   git mv OrderSystem.AppHost hosts/OrderSystem.AppHost
   git mv OrderSystem.MigrationService hosts/OrderSystem.MigrationService
   git mv OrderSystem.NotificationService hosts/OrderSystem.NotificationService
   git mv OrderSystem.Domain.Tests tests/OrderSystem.Domain.Tests
   git mv OrderSystem.Application.Tests tests/OrderSystem.Application.Tests
   git mv OrderSystem.Adapters.IntegrationTests tests/OrderSystem.Adapters.IntegrationTests
   git mv OrderSystem.EndToEndTests tests/OrderSystem.EndToEndTests
   ```

2. Apply every `<ProjectReference>` edit from data-model.md's second
   table to the 11 `.csproj` files that have one.

3. Apply the 13 `<Project Path>` edits to `OrderSystem.slnx`.

4. Apply the 5 path edits to `.github/workflows/ci.yml`.

5. Apply every README.md path edit from data-model.md's last table.

6. Build everything locally before pushing:

   ```bash
   dotnet build OrderSystem.slnx
   ```

   **Expected outcome**: builds clean, same as before the move (FR-002).
   If it doesn't, the error names which project's reference is still
   wrong — fix it against data-model.md's table and rebuild.

7. Run the full local test suite (Domain.Tests + Application.Tests need
   nothing extra; Adapters.IntegrationTests + EndToEndTests need Docker
   running):

   ```bash
   dotnet test tests/OrderSystem.Domain.Tests
   dotnet test tests/OrderSystem.Application.Tests
   dotnet test tests/OrderSystem.Adapters.IntegrationTests
   dotnet test tests/OrderSystem.EndToEndTests
   ```

   **Expected outcome**: same pass/fail results as on `main` before this
   change — this feature changes nothing about behavior (FR-002).

8. Push and open a pull request:

   ```bash
   git add -A
   git commit -m "Reorganize source into src/, hosts/, tests/ by hexagonal layer"
   git push -u origin improve-source-organization
   gh pr create --fill --base main
   ```

9. Watch the PR's checks (`gh pr checks <number>`) until all four CI jobs
   finish.

   **Expected outcome**: all four jobs pass — this is the real-world test
   of SC-002 (full CI pipeline, zero regressions) and, since it's exactly
   the same pipeline User Story 2 of the CI feature validated, also a
   second, independent proof that feature still works after a change this
   disruptive to file layout.

10. Browse the PR's **Files changed** tab (or the repo's own GitHub file
    browser on the branch) and confirm projects now appear grouped under
    `src/`, `hosts/`, and `tests/` rather than as one flat list — User
    Story 1 and User Story 2's actual acceptance check.

11. Merge once green.

## Regression check

Steps 6–9 above *are* the regression check (FR-002/SC-002) — there is no
separate suite to run, since this feature's only job is making sure the
existing one still passes after everything it depends on moved.
