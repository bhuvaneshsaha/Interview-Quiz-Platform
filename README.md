# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular SPA/PWA. Slices **1–3** are in this branch: **host**, **Access** (JWT + Identity + permissions), **Openings**, **Catalog** quiz authoring and **templates**, **Search** saved filters, and the **web client**. Question bank is **slice 4** (Catalog, copy-on-include, ADR 0007) — codes are seeded, no bank API or UI. Candidate magic-link, AI drafts, and Entra ID are **not** in this slice.

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

Development auto-applies EF migrations (`AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, `SearchDbContext`), seeds the permission catalog, Development Identity users/roles, sample openings plus default field keys (`Client`, `Project`, `Role`), one sample quiz (every v1 question type) under the backend opening, and publishes that quiz as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`. **Never seeded in Production.**

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

# Catalog schema (quizzes / questions / templates)
dotnet ef database update \
  --project src/Modules/Catalog/InterviewQuiz.Catalog.Infrastructure \
  --startup-project src/InterviewQuiz.Host \
  --context CatalogDbContext

# Search schema (saved filters)
dotnet ef database update \
  --project src/Modules/Search/InterviewQuiz.Search \
  --startup-project src/InterviewQuiz.Host \
  --context SearchDbContext

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

| Email | Password | Sample bundle | Slice 3 notes |
|-------|----------|----------------|---------------|
| `recruiter.dev@example.com` | `Dev.Recruiter!1` | Dev Recruiter | `templates.read` + `filters.write` + `filters.share`; **no** `quizzes.write` (cannot clone) |
| `author.dev@example.com` | `Dev.Author!1` | Dev Template author | `templates.write` + `quizzes.write` (publish and clone); `filters.write`; **no** `questions.*` |
| `reviewer.dev@example.com` | `Dev.Reviewer!1` | Dev Reviewer | No template/filter write |
| `admin.dev@example.com` | `Dev.Admin!1` | Dev Admin | Role editor can assign `questions.*`; they are not on this seed bundle |

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

List query: `page`, `pageSize` (capped at 100), `owner`, `experienceMinYears`, `experienceMaxYears`, `startDateFrom`, `startDateTo`, `expectedCloseDateFrom`, `expectedCloseDateTo`, `tags` (JSON object), or `criteria` (full JSON — same shape Search persists for `target: "openings"`).

Concurrency: `row_version` integer token on Opening (incremented on update). PUT requires `rowVersion`. Extra tag keys are allowed on an opening even when they are not in the admin field catalog.

In-process: `IOpeningLookup.GetOpeningAsync(id)` for Catalog and Delivery. Other modules must not read the `openings` schema.

## Catalog API (slices 2–3 — quizzes and templates)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/quizzes` | `quizzes.read` |
| `POST` | `/api/quizzes` | `quizzes.write` |
| `GET` | `/api/quizzes/{id}` | `quizzes.read` **or** `quizzes.write` |
| `PUT` | `/api/quizzes/{id}` | `quizzes.write` |
| `POST` | `/api/quizzes/{id}/publish-template` | `templates.write` |
| `GET` | `/api/templates` | `templates.read` |
| `GET` | `/api/templates/{id}` | `templates.read` |
| `GET` | `/api/templates/{id}/versions` | `templates.read` |
| `GET` | `/api/templates/{id}/versions/{versionId}` | `templates.read` |
| `POST` | `/api/templates/{id}/versions/{versionId}/clone` | `quizzes.write` **and** `templates.read` |

Quiz list query: `openingId`, `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags` (JSON object), `criteria` (full JSON — same shape Search persists for `target: "quizzes"`), `page`, `pageSize` (capped at 100). JSON is camelCase. Eight question `type` values: `multipleChoiceSingle`, `multipleChoiceMulti`, `trueFalse`, `shortText`, `longText`, `dragDropSharedBank`, `dragDropPerSlot`, `ordering`. Scoring: `auto` / `aiAssist` / `humanOnly`. `creditMode` (`partial` / `allOrNothing`) is required only for `multipleChoiceMulti` and `ordering`. PUT replaces the full question list and requires `rowVersion`. Create may send an empty `questions` array. Unknown `openingId` returns 400 `"Opening does not exist."` (Catalog calls `IOpeningLookup` in-process; it does not read the `openings` schema).

A quiz belongs to **exactly one** opening. Reuse across openings is via **template** (publish / clone). Code question types are rejected. `QuizResponse` includes `originTemplateId` and `sourceTemplateVersionId` (not client-writable on create/update). Questions include optional `sourceQuestionId` (persisted as sent; not validated against a bank).

Template list query: `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags`, `criteria` (full JSON for `target: "templates"`), `page`, `pageSize`. List/get template returns latest-version **metadata** (no question bodies). Version list is paged (`page` / `pageSize`). Version get returns full `questions`. Clone body: `openingId` (required), `title` (optional). Clone 201 `Location: /api/quizzes/{newId}`. Publish 201 `Location: /api/templates/{templateId}` with `templateId`, `versionId`, `versionNumber`, `createdNewTemplate`.

Concurrency: integer `row_version` on Quiz (incremented on update), same pattern as Opening. Publish also increments the quiz `rowVersion`.

In-process: `IQuizSnapshotReader.GetSnapshotAsync(quizId)` returns an immutable DTO of questions, keys, and scoring for Delivery to copy at assign time. `IQuestionBankReader` is reserved for slice 4 and is **not** registered.

Not in this slice: question-bank REST/UI, AI drafts, assignments, magic-link.

## Search API (slice 3 — saved filters)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/filters` | authenticated (visible filters only) |
| `GET` | `/api/filters/{id}` | authenticated (404 if not visible) |
| `POST` | `/api/filters` | `filters.write` |
| `PUT` | `/api/filters/{id}` | `filters.write` (owner only) |
| `DELETE` | `/api/filters/{id}` | `filters.write` (owner only; hard delete) |
| `POST` | `/api/filters/{id}/share` | `filters.share` (owner only) |
| `POST` | `/api/filters/{id}/unshare` | `filters.share` (owner only) |

List query: optional `target` (`openings` / `quizzes` / `templates`), `page`, `pageSize`. `criteria` JSON must match that target’s list DTO (`OpeningListCriteria` / `QuizListCriteria` / `TemplateListCriteria`); extra keys are rejected. Default `shareMode` on create is `private`. Share body: `shareMode` (`publicInsideCompany` or `specificUsers`) and `userIds` when specific. Unshare sets `private` and clears `sharedWithUserIds`. There is no `filters.read` code. Applying unsaved criteria on a list endpoint needs only the matching `*.read` (quiz get-by-id still allows `quizzes.write`).

## Web client (Angular SPA / PWA)

Workspace: `src/interview-quiz-web`. Official CLI (`ng new`, `ng generate`, `ng add @angular/pwa`). Slice 1: sign-in, openings, permission-aware admin. Slice 2: quiz list and authoring editor. Slice 3: template library, clone/publish, saved list filters.

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
npm install
ng serve
```

`ng serve` uses `proxy.conf.json` so the browser talks same-origin to `/api` and `/health`, forwarded to `http://localhost:5147`. Run the API first (`dotnet run --project src/InterviewQuiz.Host`), then the SPA at `http://localhost:4200`.

Employee routes used in slices 2–3 (permission on the route, same codes as the API):

| Path | Permission | Screen |
|------|------------|--------|
| `/quizzes` | `quizzes.read` | Quiz list (opening, keyword, experience, saved filters) |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` **or** `quizzes.write` | View / edit quiz; `templates.write` can publish as template |
| `/templates` | `templates.read` | Template list (keyword, experience, tags, saved filters) |
| `/templates/:id` | `templates.read` | Template detail, versions, read-only questions |

Shell nav and home link to Quizzes when the user has `quizzes.read`, and to Templates when `templates.read`. Create is hidden without `quizzes.write`. Recruiter (`templates.read`, no `quizzes.write`) can browse templates but not clone. Author (`quizzes.write` + `templates.read` + `templates.write`) can clone a version into a quiz and publish. Full employee route table: `docs/architecture.md` §6. Client details: `src/interview-quiz-web/README.md`. Shared UI primitives: [`docs/components/README.md`](docs/components/README.md).

### Exercise slices 2–3 locally (Development-only)

Dummy users and passwords are in the table under **Sign in locally** above — do not copy them elsewhere. Use:

- `author.dev@example.com` (Dev Template author) for **write**: list/create/edit quizzes (`quizzes.write`), publish as template (`templates.write`), clone (`quizzes.write` + `templates.read`), save filters (`filters.write`).
- `recruiter.dev@example.com` (Dev Recruiter) for **read-only quizzes and templates**: list and open; quiz form disabled (`quizzes.read` without `quizzes.write`). Can save and **share** filters (`filters.write` + `filters.share`). Cannot clone.

Development seed includes one sample quiz with every v1 question type under the backend opening, published as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`. **Never seeded in Production.** There is no question-bank UI.

```bash
ng build
ng test --watch=false
```

`ng test` runs **Vitest** (Angular unit/component tests, including quiz list/editor, templates, and SavedFilters).

### Storybook

OSS Storybook 10 (`@storybook/angular-vite`, Angular 21 zoneless application builder) galleries shared primitives only (PageStatus, OfflineBanner, HasPermission, SavedFilters). No Chromatic. Playbook markdown remains the source of API tables: `docs/components/README.md`.

```bash
cd src/interview-quiz-web
npm run storybook
```

Opens **http://localhost:6006**. Static build: `npm run build-storybook`.

PWA: installable manifest + service worker that caches the **hashed app shell only**. No `/api` data groups, no IndexedDB outbox. Offline UI is a banner. Token storage, dummy users, and correlation id are documented in `src/interview-quiz-web/README.md`.

## Tests

```bash
dotnet test InterviewQuiz.slnx
```

Unit tests always run (JWT validation, `HasPermission`, login success/failure with fakes, Catalog question/quiz/template rules, Search filter rules). WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents). When the database is present, those tests authenticate with JWT (not `Authorization: Test`) and include Catalog quiz/template API coverage (`CatalogApiTests`) and saved-filter API coverage (`SearchApiTests`).

Angular (Vitest):

```bash
cd src/interview-quiz-web
ng test --watch=false
```

## Solution layout

- `src/InterviewQuiz.Host` — composition root
- `src/InterviewQuiz.Kernel` — clock, pagination, tags, permission code constants
- `src/Modules/Access` — Identity user store, JWT issue/refresh/revoke, permission catalog, roles as permission sets (`access` schema)
- `src/Modules/Openings` — Domain / Application / Infrastructure (`openings` schema)
- `src/Modules/Catalog` — Domain / Application / Infrastructure (`catalog` schema; quiz authoring, templates)
- `src/Modules/Search` — saved filters (`search` schema)
- `src/Modules/Delivery|Evaluation` — empty composition stubs (assignments / review later)
- `src/interview-quiz-web` — Angular SPA + installable PWA (slices 1–3: openings, quizzes, templates, saved filters)
- `deploy/local/compose.yaml` — local PostgreSQL 16
