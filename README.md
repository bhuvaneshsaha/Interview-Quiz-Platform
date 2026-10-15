# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular SPA/PWA. Slices **1–4** are in this branch: **host**, **Access** (JWT + Identity + permissions), **Openings**, **Catalog** quiz authoring, **templates**, **question bank** (copy-on-include, ADR 0007) **API and Angular UI**, **Search** saved filters, and the **web client**. Candidate magic-link, AI drafts, and Entra ID are **not** in this slice.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (this repo targets `net10.0`)
- Docker Compose **or** a local PostgreSQL 16 instance
- `dotnet-ef` for migrations (`dotnet tool install --global dotnet-ef`)
- Node.js LTS + Angular CLI 21 (for the web client)

Docker is used only to run PostgreSQL locally. The app itself runs with `dotnet run`.

Windows (PowerShell): [Windows (PowerShell) quick start](#windows-powershell-quick-start). The bash commands in the sections below stay as they are.

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

PowerShell (same value, same session as the process that needs it):

```powershell
$env:ConnectionStrings__InterviewQuiz = "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only"
```

`src/InterviewQuiz.Host/appsettings.Development.json` already uses the Compose credentials so `dotnet run` in Development works without setting the variable. Other environments must set the environment variable (or an OS-protected file / Vault). Do not put production passwords in source. Integration tests run as `Testing`, which does not load that file — set `ConnectionStrings__InterviewQuiz` before `dotnet test` (see the Windows quick start).

## Windows (PowerShell) quick start

From the repo root in PowerShell, with Docker Desktop running. This is the same local stack as the bash sections: Compose file `deploy/local/compose.yaml`, host `src/InterviewQuiz.Host`, Angular workspace `src/interview-quiz-web`.

### 1. PostgreSQL

```powershell
docker compose -f deploy/local/compose.yaml up -d
```

There is no Compose file at the repo root, so `-f deploy/local/compose.yaml` is required. `-d` leaves Postgres in the background. Database `interviewquiz`, user `interviewquiz`, password `interviewquiz_dev_only`, port `5432` — the same values as [Local PostgreSQL](#local-postgresql).

### 2. API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/InterviewQuiz.Host
```

Set `ASPNETCORE_ENVIRONMENT` in that same PowerShell session before `dotnet run`. The `http` launch profile also sets it and listens on `http://localhost:5147` (`src/InterviewQuiz.Host/Properties/launchSettings.json`).

In Development the host applies EF migrations on startup for `AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, and `SearchDbContext` (`Program.cs`), then seeds Development-only data. The manual `dotnet ef database update` steps under [Migrate and run](#migrate-and-run) are optional for this path.

On first run, ERR log lines about `__EFMigrationsHistory` are harmless, including lines that name the `delivery` or `evaluation` schemas. Delivery and Evaluation are composition stubs in this slice and do not apply migrations, so a missing history table there is expected. The same missing-table ERR can appear for `access`, `openings`, `catalog`, and `search` before the host creates the table; startup continues and applies those migrations.

`appsettings.Development.json` already contains the Compose connection string, so this `dotnet run` does not need `ConnectionStrings__InterviewQuiz`.

### 3. Angular

In a second PowerShell window, from the repo root, with the API still running:

```powershell
cd src/interview-quiz-web
npm install
npx ng serve
```

`npx ng serve` uses the Angular CLI from this workspace (`@angular/cli` in `devDependencies`). Open `http://localhost:4200`. `proxy.conf.json` forwards `/api` and `/health` to `http://localhost:5147`.

### Tests in the same session

Integration tests skip unless `ConnectionStrings__InterviewQuiz` is set. The `Testing` environment does not read `appsettings.Development.json`, so the Development file is not enough. Set the variable in the same PowerShell session that runs `dotnet test`, using the Compose value:

```powershell
$env:ConnectionStrings__InterviewQuiz = "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only"
dotnet test InterviewQuiz.slnx
```

A new window does not keep `$env:` values. Set the variable again before `dotnet test`.

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

Development auto-applies EF migrations (`AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, `SearchDbContext`), seeds the permission catalog, Development Identity users/roles, sample openings plus default field keys (`Client`, `Project`, `Role`), one sample quiz (every v1 question type) under the backend opening, publishes that quiz as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`, and three live **bank questions** (`6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001` MC single, `…4002` true/false, `…4003` short text). **Never seeded in Production.**

Because that startup path applies the migrations, the manual `dotnet ef database update` commands below are optional when the host runs in Development. On first run, ERR log lines about `__EFMigrationsHistory` are harmless, including lines for the `delivery` and `evaluation` schemas (composition stubs; not migrated in this slice). The host still creates the history table in `access`, `openings`, `catalog`, and `search` and continues. PowerShell for the same flow: [Windows (PowerShell) quick start](#windows-powershell-quick-start).

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

# Catalog schema (quizzes / questions / templates / bank_questions)
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

| Email | Password | Sample bundle | Slice 4 notes |
|-------|----------|----------------|---------------|
| `recruiter.dev@example.com` | `Dev.Recruiter!1` | Dev Recruiter | `templates.read` + `filters.write` + `filters.share`; **no** `quizzes.write` (cannot clone); **no** `questions.*` |
| `author.dev@example.com` | `Dev.Author!1` | Dev Template author | `templates.write` + `quizzes.write` + `questions.read` + `questions.write`; `filters.write` |
| `reviewer.dev@example.com` | `Dev.Reviewer!1` | Dev Reviewer | No template/filter/bank write |
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

## Catalog API (slices 2–4 — quizzes, templates, question bank)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/quizzes` | `quizzes.read` |
| `POST` | `/api/quizzes` | `quizzes.write` |
| `GET` | `/api/quizzes/{id}` | `quizzes.read` **or** `quizzes.write` |
| `PUT` | `/api/quizzes/{id}` | `quizzes.write` |
| `POST` | `/api/quizzes/{quizId}/include-questions` | `quizzes.write` **and** `questions.read` |
| `POST` | `/api/quizzes/{id}/publish-template` | `templates.write` |
| `GET` | `/api/templates` | `templates.read` |
| `GET` | `/api/templates/{id}` | `templates.read` |
| `GET` | `/api/templates/{id}/versions` | `templates.read` |
| `GET` | `/api/templates/{id}/versions/{versionId}` | `templates.read` |
| `POST` | `/api/templates/{id}/versions/{versionId}/clone` | `quizzes.write` **and** `templates.read` |
| `GET` | `/api/questions` | `questions.read` |
| `POST` | `/api/questions` | `questions.write` |
| `GET` | `/api/questions/{id}` | `questions.read` **or** `questions.write` |
| `PUT` | `/api/questions/{id}` | `questions.write` |
| `POST` | `/api/questions/{id}/archive` | `questions.write` |
| `POST` | `/api/questions/{id}/unarchive` | `questions.write` |

Quiz list query: `openingId`, `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags` (JSON object), `criteria` (full JSON — same shape Search persists for `target: "quizzes"`), `page`, `pageSize` (capped at 100). JSON is camelCase. Eight question `type` values: `multipleChoiceSingle`, `multipleChoiceMulti`, `trueFalse`, `shortText`, `longText`, `dragDropSharedBank`, `dragDropPerSlot`, `ordering`. Scoring: `auto` / `aiAssist` / `humanOnly`. `creditMode` (`partial` / `allOrNothing`) is required only for `multipleChoiceMulti` and `ordering`. PUT replaces the full question list and requires `rowVersion`. Create may send an empty `questions` array. Unknown `openingId` returns 400 `"Opening does not exist."` (Catalog calls `IOpeningLookup` in-process; it does not read the `openings` schema).

Template list query: `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags`, `criteria` (full JSON for `target: "templates"`), `page`, `pageSize`. List/get template returns latest-version **metadata** (no question bodies). Version list is paged (`page` / `pageSize`). Version get returns full `questions`. Clone body: `openingId` (required), `title` (optional). Clone 201 `Location: /api/quizzes/{newId}`. Publish 201 `Location: /api/templates/{templateId}` with `templateId`, `versionId`, `versionNumber`, `createdNewTemplate`.

A quiz belongs to **exactly one** opening. Reuse across openings is via **template** (publish / clone). Code question types are rejected. `QuizResponse` includes `originTemplateId` and `sourceTemplateVersionId` (not client-writable on create/update). Questions include optional `sourceQuestionId` (bank provenance; include sets it; quiz CRUD still persists it as sent).

Question-bank list query: `keyword` (title **or** stem), `type` (camelCase), `experienceMinYears` / `experienceMaxYears`, `tags` (JSON object), `criteria` (`BankQuestionListCriteria` JSON), `archived` (default `false`; `true` returns only archived and requires `questions.write` else 403), `page`, `pageSize`. `BankQuestionResponse` has no `sortOrder`. Create/update body: `title`, `tags`, `expectedExperienceYears` (0–80), `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`. Update requires `rowVersion`. Client `sourceQuestionId` is ignored. No hard delete — archive / unarchive. Unknown GET is 404. Archive already archived / unarchive when live is 400. The Angular bank list has **no** SavedFilters (`FilterTarget` has no `questions`).

Include body `IncludeQuestionsRequest`: `questionIds` (required, unique, non-empty), `insertAt` (optional 0-based index into the current quiz list; default append; clamped 0..count), `rowVersion` (quiz concurrency). Missing bank id → 400 `"Question does not exist."` Archived bank item → 400 `"Archived question cannot be included."` Copies type/stem/scoring/credit/points/body, **new quiz question ids**, `sourceQuestionId` = bank item id. Returns the updated `QuizResponse` (200).

Concurrency: integer `row_version` on Quiz and BankQuestion (incremented on update), same pattern as Opening. Publish also increments the quiz `rowVersion`. Include increments the quiz `rowVersion`.

In-process: `IQuizSnapshotReader.GetSnapshotAsync(quizId)` returns an immutable DTO of questions, keys, and scoring for Delivery to copy at assign time. `IQuestionBankReader.GetByIdAsync(id)` is registered and used **only** by include (not quiz create/update/publish/clone). GetById returns archived rows so include can reject them.

Not in this slice: AI drafts, assignments, magic-link.

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

Workspace: `src/interview-quiz-web`. Official CLI (`ng new`, `ng generate`, `ng add @angular/pwa`). Slice 1: sign-in, openings, permission-aware admin. Slice 2: quiz list and authoring editor. Slice 3: template library, clone/publish, saved list filters. Slice 4: question bank list/editor and include-from-quiz.

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
npm install
ng serve
```

`ng serve` uses `proxy.conf.json` so the browser talks same-origin to `/api` and `/health`, forwarded to `http://localhost:5147`. Run the API first (`dotnet run --project src/InterviewQuiz.Host`), then the SPA at `http://localhost:4200`. On Windows, `npx ng serve` after `npm install` is in [Windows (PowerShell) quick start](#windows-powershell-quick-start).

Employee routes used in slices 2–4 (permission on the route, same codes as the API):

| Path | Permission | Screen |
|------|------------|--------|
| `/quizzes` | `quizzes.read` | Quiz list (opening, keyword, experience, saved filters) |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` **or** `quizzes.write` | View / edit quiz; `templates.write` can publish as template; include from bank when `quizzes.write` **and** `questions.read` on an existing saved quiz |
| `/templates` | `templates.read` | Template list (keyword, experience, tags, saved filters) |
| `/templates/:id` | `templates.read` | Template detail, versions, read-only questions |
| `/questions` | `questions.read` | Question bank list (keyword, type, experience, tags; archived-only toggle only with `questions.write`). **No** SavedFilters |
| `/questions/new` | `questions.write` | Create bank question |
| `/questions/:id` | `questions.read` **or** `questions.write` | View / edit bank question; archive/unarchive with write |

Shell nav and home link to Quizzes when the user has `quizzes.read`, to Templates when `templates.read`, and to **Question bank** when `questions.read`. Create is hidden without `quizzes.write`. Recruiter (`templates.read`, no `quizzes.write`, **no** `questions.*`) can browse templates but not clone, and does not see bank nav or include. Author (`quizzes.write` + `templates.read` + `templates.write` + `questions.read` + `questions.write`) can clone, publish, author bank items, and include from the bank. Full employee route table: `docs/architecture.md` §6. Client details: `src/interview-quiz-web/README.md`. Shared UI primitives: [`docs/components/README.md`](docs/components/README.md).

### Exercise slices 2–4 locally (Development-only)

Dummy users and passwords are in the table under **Sign in locally** above — do not copy them elsewhere. Use:

- `author.dev@example.com` (Dev Template author) for **write**: list/create/edit quizzes (`quizzes.write`), publish as template (`templates.write`), clone (`quizzes.write` + `templates.read`), question bank (`questions.read` + `questions.write`), include from an existing quiz (`quizzes.write` + `questions.read`), save filters (`filters.write`).
- `recruiter.dev@example.com` (Dev Recruiter) for **read-only quizzes and templates**: list and open; quiz form disabled (`quizzes.read` without `quizzes.write`). Can save and **share** filters (`filters.write` + `filters.share`). Cannot clone. **No** `questions.*` — no bank nav, no include panel.

Development seed includes one sample quiz with every v1 question type under the backend opening, published as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`, and three live bank questions (`6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001` MC single, `…4002` true/false, `…4003` short text). **Never seeded in Production.**

```bash
ng build
ng test --watch=false
```

`ng test` runs **Vitest** (Angular unit/component tests, including quiz list/editor, templates, SavedFilters, and question bank list/editor/include). The client has component specs for those screens.

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

Unit tests always run (JWT validation, `HasPermission`, login success/failure with fakes, Catalog question/quiz/template rules, Search filter rules). WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents). The variable has to be present in the same shell session as `dotnet test`. Integration tests use the `Testing` environment, so `appsettings.Development.json` is not a substitute. Example matching `deploy/local/compose.yaml`:

```powershell
$env:ConnectionStrings__InterviewQuiz = "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only"
dotnet test InterviewQuiz.slnx
```

When the database is present, those tests authenticate with JWT (not `Authorization: Test`) and include Catalog quiz/template API coverage (`CatalogApiTests`), question-bank API coverage (`QuestionBankApiTests`), and saved-filter API coverage (`SearchApiTests`).

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
- `src/Modules/Catalog` — Domain / Application / Infrastructure (`catalog` schema; quiz authoring, templates, question bank)
- `src/Modules/Search` — saved filters (`search` schema)
- `src/Modules/Delivery|Evaluation` — empty composition stubs (assignments / review later)
- `src/interview-quiz-web` — Angular SPA + installable PWA (slices 1–4: openings, quizzes, templates, question bank, saved filters)
- `deploy/local/compose.yaml` — local PostgreSQL 16
