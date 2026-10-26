# Move + Reference Mapping: Improve Source Organization

Not business entities — this feature's actual "data" is the move mapping
itself (research.md Decision 1). Every value below is pre-computed so
`/speckit-tasks` and `/speckit-implement` can apply it directly rather
than re-deriving relative paths during implementation.

## Directory moves (13 total)

| Project | Old path | New path |
|---|---|---|
| `OrderSystem.Domain` | `OrderSystem.Domain/` | `src/Domain/OrderSystem.Domain/` |
| `OrderSystem.Application` | `OrderSystem.Application/` | `src/Application/OrderSystem.Application/` |
| `OrderSystem.Adapters.Postgres` | `OrderSystem.Adapters.Postgres/` | `src/Adapters/OrderSystem.Adapters.Postgres/` |
| `OrderSystem.Adapters.Kafka` | `OrderSystem.Adapters.Kafka/` | `src/Adapters/OrderSystem.Adapters.Kafka/` |
| `OrderSystem.Adapters.RabbitMQ` | `OrderSystem.Adapters.RabbitMQ/` | `src/Adapters/OrderSystem.Adapters.RabbitMQ/` |
| `OrderSystem.Api` | `OrderSystem.Api/` | `hosts/OrderSystem.Api/` |
| `OrderSystem.AppHost` | `OrderSystem.AppHost/` | `hosts/OrderSystem.AppHost/` |
| `OrderSystem.MigrationService` | `OrderSystem.MigrationService/` | `hosts/OrderSystem.MigrationService/` |
| `OrderSystem.NotificationService` | `OrderSystem.NotificationService/` | `hosts/OrderSystem.NotificationService/` |
| `OrderSystem.Domain.Tests` | `OrderSystem.Domain.Tests/` | `tests/OrderSystem.Domain.Tests/` |
| `OrderSystem.Application.Tests` | `OrderSystem.Application.Tests/` | `tests/OrderSystem.Application.Tests/` |
| `OrderSystem.Adapters.IntegrationTests` | `OrderSystem.Adapters.IntegrationTests/` | `tests/OrderSystem.Adapters.IntegrationTests/` |
| `OrderSystem.EndToEndTests` | `OrderSystem.EndToEndTests/` | `tests/OrderSystem.EndToEndTests/` |

Unmoved: `order-system-frontend/`, `OrderSystem.slnx`, `README.md`,
`.github/`, `.specify/`, `specs/`, `.claude/`, `.config/`, `.gitignore`.

## `<ProjectReference>` edits (26 total, across 11 `.csproj` files)

Every `Include` value below replaces that file's current one exactly —
computed from the move table above, not re-derived per file.

| Referencing project (new location) | Referenced project | New `Include` value |
|---|---|---|
| `tests/OrderSystem.Adapters.IntegrationTests/` | Domain | `..\..\src\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| ″ | Application | `..\..\src\Application\OrderSystem.Application\OrderSystem.Application.csproj` |
| ″ | Adapters.Postgres | `..\..\src\Adapters\OrderSystem.Adapters.Postgres\OrderSystem.Adapters.Postgres.csproj` |
| ″ | Adapters.Kafka | `..\..\src\Adapters\OrderSystem.Adapters.Kafka\OrderSystem.Adapters.Kafka.csproj` |
| ″ | Adapters.RabbitMQ | `..\..\src\Adapters\OrderSystem.Adapters.RabbitMQ\OrderSystem.Adapters.RabbitMQ.csproj` |
| `hosts/OrderSystem.Api/` | Application | `..\..\src\Application\OrderSystem.Application\OrderSystem.Application.csproj` |
| ″ | Adapters.Postgres | `..\..\src\Adapters\OrderSystem.Adapters.Postgres\OrderSystem.Adapters.Postgres.csproj` |
| ″ | Adapters.Kafka | `..\..\src\Adapters\OrderSystem.Adapters.Kafka\OrderSystem.Adapters.Kafka.csproj` |
| ″ | Adapters.RabbitMQ | `..\..\src\Adapters\OrderSystem.Adapters.RabbitMQ\OrderSystem.Adapters.RabbitMQ.csproj` |
| `src/Adapters/OrderSystem.Adapters.Kafka/` | Domain | `..\..\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| `hosts/OrderSystem.AppHost/` | Api | `..\OrderSystem.Api\OrderSystem.Api.csproj` |
| ″ | MigrationService | `..\OrderSystem.MigrationService\OrderSystem.MigrationService.csproj` |
| ″ | NotificationService | `..\OrderSystem.NotificationService\OrderSystem.NotificationService.csproj` |
| `src/Adapters/OrderSystem.Adapters.Postgres/` | Domain | `..\..\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| ″ | Application | `..\..\Application\OrderSystem.Application\OrderSystem.Application.csproj` |
| `src/Application/OrderSystem.Application/` | Domain | `..\..\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| `src/Adapters/OrderSystem.Adapters.RabbitMQ/` | Domain | `..\..\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| ″ | Application | `..\..\Application\OrderSystem.Application\OrderSystem.Application.csproj` |
| `tests/OrderSystem.Application.Tests/` | Application | `..\..\src\Application\OrderSystem.Application\OrderSystem.Application.csproj` |
| `tests/OrderSystem.EndToEndTests/` | AppHost | `..\..\hosts\OrderSystem.AppHost\OrderSystem.AppHost.csproj` |
| `hosts/OrderSystem.MigrationService/` | Adapters.Postgres | `..\..\src\Adapters\OrderSystem.Adapters.Postgres\OrderSystem.Adapters.Postgres.csproj` |
| `hosts/OrderSystem.NotificationService/` | Domain | `..\..\src\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |
| ″ | Adapters.Kafka | `..\..\src\Adapters\OrderSystem.Adapters.Kafka\OrderSystem.Adapters.Kafka.csproj` |
| `tests/OrderSystem.Domain.Tests/` | Domain | `..\..\src\Domain\OrderSystem.Domain\OrderSystem.Domain.csproj` |

(`″` repeats the row above's "Referencing project".)

## `hosts/OrderSystem.AppHost/AppHost.cs` — one more reference, found during implementation

Not caught during planning: `AddJavaScriptApp("frontend", "../order-system-frontend")`
is a string-literal relative path, not a `<ProjectReference>`, so the
audit above — which only covered `.csproj`/`.slnx`/`.yml`/`.md` — missed
it. `OrderSystem.AppHost` moved one level deeper (`OrderSystem.AppHost/` →
`hosts/OrderSystem.AppHost/`), so this needs an extra `../`:
`"../order-system-frontend"` → `"../../order-system-frontend"`. Caught by
`tests/OrderSystem.EndToEndTests` timing out waiting for the frontend
resource to start (T008) — exactly the kind of regression that gate
exists to catch. A repo-wide grep for `"\.\./` across all `.cs` files
confirmed this was the only hardcoded relative path of its kind.

## `OrderSystem.slnx` — every `<Project Path>` updated

Each of the 13 `<Project Path="...">` values becomes its New path from
the move table above (forward slashes, matching the file's existing
style), e.g. `<Project Path="src/Domain/OrderSystem.Domain/OrderSystem.Domain.csproj" />`.

## `.github/workflows/ci.yml` — 5 lines

| Current | New |
|---|---|
| `dotnet test OrderSystem.Domain.Tests` | `dotnet test tests/OrderSystem.Domain.Tests` |
| `dotnet test OrderSystem.Application.Tests` | `dotnet test tests/OrderSystem.Application.Tests` |
| `dotnet test OrderSystem.Adapters.IntegrationTests` | `dotnet test tests/OrderSystem.Adapters.IntegrationTests` |
| `dotnet build OrderSystem.EndToEndTests` | `dotnet build tests/OrderSystem.EndToEndTests` |
| `working-directory: OrderSystem.EndToEndTests` | `working-directory: tests/OrderSystem.EndToEndTests` |
| `dotnet test OrderSystem.EndToEndTests` | `dotnet test tests/OrderSystem.EndToEndTests` |

No change needed to `working-directory: order-system-frontend` (unmoved)
or `.gitignore`'s `**/OrderSystem.AppHost/bin/` /
`**/OrderSystem.AppHost/obj/` patterns (the `**/` prefix already matches
at any depth, including `hosts/OrderSystem.AppHost/bin/`).

## `README.md` — every documented path

| Section | Current | New |
|---|---|---|
| Running the application (Option B) | `dotnet run --project OrderSystem.AppHost` | `dotnet run --project hosts/OrderSystem.AppHost` |
| Running the application (Option C) | `dotnet run --project OrderSystem.Api` | `dotnet run --project hosts/OrderSystem.Api` |
| ″ | `dotnet run --project OrderSystem.NotificationService` | `dotnet run --project hosts/OrderSystem.NotificationService` |
| Database schema | `OrderSystem.Adapters.Postgres/Migrations/` | `src/Adapters/OrderSystem.Adapters.Postgres/Migrations/` |
| ″ | `--project OrderSystem.Adapters.Postgres` | `--project src/Adapters/OrderSystem.Adapters.Postgres` |
| ″ | `--startup-project OrderSystem.MigrationService` | `--startup-project hosts/OrderSystem.MigrationService` |
| ″ | `dotnet run --project OrderSystem.MigrationService` | `dotnet run --project hosts/OrderSystem.MigrationService` |
| Automated tests (Playwright install) | `OrderSystem.EndToEndTests/bin/Debug/net10.0/playwright.ps1` | `tests/OrderSystem.EndToEndTests/bin/Debug/net10.0/playwright.ps1` |
| Solution layout (code block) | all 13 project lines | rewritten to the new `src/`/`hosts/`/`tests/` tree, same inline comments |

No change needed to `dotnet test OrderSystem.slnx` (the `.slnx` file
itself doesn't move) or any prose that names a project without a path
(e.g. "the `OrderSystem.Application` project").
