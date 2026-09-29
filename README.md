# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular SPA/PWA. Slice 1 delivers the **host**, **Access** (JWT + Identity + permissions), **Openings**, and the **web client** (sign-in, openings, permission-aware admin). Candidate magic-link and Entra ID are **not** in this slice.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (this repo targets `net10.0`)
- Docker Compose **or** a local PostgreSQL 16 instance
- `dotnet-ef` for migrations (`dotnet tool install --global dotnet-ef`)
- Node.js LTS + Angular CLI 21 (for the web client)

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
| `Jwt__SigningKey` | HMAC-SHA256 signing key (required, ≥ 32 characters). **Never** use the Development key in Production |
| `Jwt__Issuer` | JWT issuer (defaults to `InterviewQuiz` in appsettings) |
| `Jwt__Audience` | JWT audience (defaults to `InterviewQuiz` in appsettings) |
| `Jwt__AccessTokenMinutes` | Access token lifetime (default `15`) |
| `Jwt__RefreshTokenDays` | Refresh token lifetime (default `14`) |

Production must set `Jwt__SigningKey` (environment, OS-protected file, or Vault). The Development signing key in `appsettings.Development.json` is **local-only** and not for Production.

## Migrate and run

Development auto-applies EF migrations, seeds the permission catalog, Development Identity users/roles, and sample openings plus default field keys (`Client`, `Project`, `Role`). **Never seeded in Production.**

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH

# Access schema (Identity, roles, refresh tokens, permission catalog)
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

- Document: `http://localhost:5147/openapi/v1.json` (Development) — bearer security scheme
- Swagger UI: `http://localhost:5147/swagger` (Development)

### Health

- Liveness (process): `GET /health/live`
- Readiness (PostgreSQL): `GET /health/ready`

Correlation: send `traceparent` and/or `X-Correlation-ID`. The host generates a correlation id if missing and echoes `X-Correlation-ID`.

## Authentication (JWT bearer)

Employee entry is **email + password → JWT**. The API default scheme is JwtBearer in every environment. Identity is the **user store only** (no cookie auth as the API mechanism). Refresh tokens are stored **hashed** in PostgreSQL (`access.refresh_tokens`), rotated on refresh, and revoked on logout. Access tokens carry flattened **permission codes** (claim type `permission`), not role names.

There is **no self-registration**. Admins provision users (`users.manage`).

Not in this slice: candidate magic-link, Entra ID.

### Sign in locally (Development dummy users)

These accounts exist only when the host runs in Development (or tests seed them). Do not use them in Production.

| Email | Password | Sample bundle |
|-------|----------|----------------|
| `recruiter.dev@example.com` | `Dev.Recruiter!1` | Dev Recruiter |
| `author.dev@example.com` | `Dev.Author!1` | Dev Template author |
| `reviewer.dev@example.com` | `Dev.Reviewer!1` | Dev Reviewer |
| `admin.dev@example.com` | `Dev.Admin!1` | Dev Admin |

```bash
curl -s http://localhost:5147/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin.dev@example.com","password":"Dev.Admin!1"}'
```

Use `Authorization: Bearer {accessToken}` on subsequent requests. Call `POST /api/auth/refresh` with the refresh token to rotate; the new access token re-reads permissions from the database (role changes take effect on the next tokens). Call `POST /api/auth/logout` with the refresh token to revoke it.

Angular should keep the access token **in memory** and the refresh token in **sessionStorage** (not `localStorage`, Cache Storage, IndexedDB, or the service worker). See `src/interview-quiz-web/README.md`. Service workers must not cache tokens, `/me`, login, or refresh.

Do **not** use `[Authorize(Roles = ...)]`. Roles are operator-composed permission sets; API and UI check permission codes only.

## Access API

| Method | Path | Permission |
|--------|------|------------|
| `POST` | `/api/auth/login` | anonymous |
| `POST` | `/api/auth/refresh` | anonymous (valid refresh token) |
| `POST` | `/api/auth/logout` | anonymous (refresh token body) |
| `GET` | `/api/me` | authenticated |
| `GET` | `/api/me/permissions` | authenticated |
| `GET` | `/api/permissions` | `roles.manage` |
| `GET\|POST\|PUT\|DELETE` | `/api/roles` | `roles.manage` |
| `POST\|DELETE` | `/api/roles/{id}/users` | `roles.manage` |
| `GET\|POST\|PUT` | `/api/users` | `users.manage` |

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

## Web client (Angular SPA / PWA)

Workspace: `src/interview-quiz-web`. Official CLI (`ng new`, `ng generate`, `ng add @angular/pwa`).

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
npm install
ng serve
```

`ng serve` uses `proxy.conf.json` so the browser talks same-origin to `/api` and `/health`, forwarded to `http://localhost:5147`. Run the API first (`dotnet run --project src/InterviewQuiz.Host`), then the SPA at `http://localhost:4200`.

```bash
ng build
ng test --watch=false
```

PWA: installable manifest + service worker that caches the **hashed app shell only**. No `/api` data groups, no IndexedDB outbox. Offline UI is a banner. Token storage, dummy users, and correlation id are documented in `src/interview-quiz-web/README.md`.

Shared UI primitives: `docs/components/README.md`.

## Tests

```bash
dotnet test InterviewQuiz.slnx
```

Unit tests always run (JWT validation, `HasPermission`, login success/failure with fakes). WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents). When the database is present, those tests authenticate with JWT (not `Authorization: Test`).

## Solution layout

- `src/InterviewQuiz.Host` — composition root
- `src/InterviewQuiz.Kernel` — clock, pagination, tags, permission code constants
- `src/Modules/Access` — Identity user store, JWT issue/refresh/revoke, permission catalog, roles as permission sets (`access` schema)
- `src/Modules/Openings` — Domain / Application / Infrastructure (`openings` schema)
- `src/Modules/Catalog|Delivery|Evaluation|Search` — empty composition stubs
- `src/interview-quiz-web` — Angular SPA + installable PWA (slice 1)
- `deploy/local/compose.yaml` — local PostgreSQL 16
