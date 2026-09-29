# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular (SPA/PWA later). Slice 1 delivers the **host** and the **Openings** module. **Login is not implemented yet** — Auth owns JWT bearer, ASP.NET Core Identity, magic-link, and Entra later.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (this repo targets `net10.0`)
- Docker Compose **or** a local PostgreSQL 16 instance
- `dotnet-ef` for migrations (`dotnet tool install --global dotnet-ef`)

Docker is used only to run PostgreSQL locally. The app itself runs with `dotnet run`.

## Local PostgreSQL

From the repo root:

```bash
docker compose -f deploy/local/compose.yaml up -d
```

Compose (local development only — not production secrets):

- database: `interviewquiz`
- user: `interviewquiz`
- password: `interviewquiz_dev_only`
- port: `5432`

Point the API at it with the **name** `ConnectionStrings__InterviewQuiz` (never commit production values):

```bash
export ConnectionStrings__InterviewQuiz="Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only"
```

`src/InterviewQuiz.Host/appsettings.Development.json` already uses the Compose credentials so `dotnet run` in Development works without exporting the variable. Other environments must set the environment variable (or an OS-protected file / Vault). Do not put production passwords in source.

## Environment variable names

| Name | Purpose |
|------|---------|
| `ConnectionStrings__InterviewQuiz` | PostgreSQL connection (required outside Development appsettings) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Test` / `Production` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional. When set, traces and metrics are exported via OTLP |
| `OTEL_SERVICE_NAME` | Optional. Defaults to `interviewquiz-api` |
| `FileStorage__Root` | Later (resumes). Not used in slice 1 |
| `Jwt__*` | Later (Auth). Not used yet |

## Migrate and run

Development auto-applies EF migrations and seeds sample openings plus default field keys (`Client`, `Project`, `Role`). **Never seeded in Production.**

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH

# Access schema (permission catalog)
dotnet ef database update \
  --project src/Modules/Access/InterviewQuiz.Access \
  --startup-project src/InterviewQuiz.Host \
  --context AccessDbContext

# Openings schema
dotnet ef database update \
  --project src/Modules/Openings/InterviewQuiz.Openings.Infrastructure \
  --startup-project src/InterviewQuiz.Host \
  --context OpeningsDbContext

dotnet run --project src/InterviewQuiz.Host
```

HTTP profile: `http://localhost:5147` (see `Properties/launchSettings.json`).

### OpenAPI

- Document: `http://localhost:5147/openapi/v1.json` (Development)
- Swagger UI: `http://localhost:5147/swagger` (Development)

### Health

- Liveness (process): `GET /health/live`
- Readiness (PostgreSQL): `GET /health/ready`

Correlation: send `traceparent` and/or `X-Correlation-ID`. The host generates a correlation id if missing and echoes `X-Correlation-ID`.

## Temporary Development authentication

Until Auth wires JWT:

```http
Authorization: Test recruiter@example.com
```

Enabled only in `Development` and `Testing`. Mutating and read opening endpoints also have `[HasPermission("openings.write")]` (etc.). The current handler **fails closed for anonymous** and **allows any authenticated identity**. Auth will replace this with permission claims. Production currently uses a fail-closed placeholder scheme so the host cannot be called as authenticated until JWT is wired.

Do **not** use `[Authorize(Roles = ...)]`. Role/user tables are not in this slice.

## Openings API (slice 1)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/openings` | `openings.read` |
| `POST` | `/api/openings` | `openings.write` |
| `PUT` | `/api/openings/{id}` | `openings.write` |
| `PUT` | `/api/openings` (id in body) | `openings.write` |
| `GET` | `/api/openings/{id}` | `openings.read` |
| `GET` | `/api/opening-field-definitions` | `openings.fields.manage` |
| `PUT` | `/api/opening-field-definitions` | `openings.fields.manage` |

List query: `page`, `pageSize` (capped at 100), `owner`, `experienceMinYears`, `experienceMaxYears`, `startDateFrom`, `startDateTo`, `expectedCloseDateFrom`, `expectedCloseDateTo`, `tags` (JSON object), or `criteria` (full JSON — same shape Search will persist later).

Concurrency: `row_version` integer token on Opening (incremented on update). PUT requires `rowVersion`. Extra tag keys are allowed on an opening even when they are not in the admin field catalog.

In-process: `IOpeningLookup.GetOpeningAsync(id)` for Catalog and Delivery. Other modules must not read the `openings` schema.

## Tests

```bash
dotnet test InterviewQuiz.slnx
```

Unit tests always run. WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents).

## Solution layout

- `src/InterviewQuiz.Host` — composition root
- `src/InterviewQuiz.Kernel` — clock, pagination, tags, permission code constants
- `src/Modules/Access` — permission catalog table (`access` schema); Auth will add Identity/JWT
- `src/Modules/Openings` — Domain / Application / Infrastructure (`openings` schema)
- `src/Modules/Catalog|Delivery|Evaluation|Search` — empty composition stubs
- `deploy/local/compose.yaml` — local PostgreSQL 16
