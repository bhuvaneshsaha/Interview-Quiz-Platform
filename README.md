# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular SPA/PWA. Slices **1–5** are in this branch, plus live sessions, **Reporting v1**, and **human review** of unsettled attempts: **host**, **Access** (JWT + Identity + permissions + assignment-scoped candidate magic-link JWTs), **Openings**, **Catalog**, **templates**, **question bank**, **Search** saved filters, **Delivery** assignments/snapshots, **Evaluation** async attempts/auto-score/review, and the **web client** (employee assignments + candidate `/attempt`). Live start and pause shipped in [v0.1.0](CHANGELOG.md). Reporting v1 shipped as v0.2.0. Human review shipped as v0.3.0. AI suggestion drafts for review and AI authoring drafts are in this branch when configured. Entra ID is not in this pass. Invite email is: Development writes a local sink (or Mailpit), and SMTP settings come from configuration. What slice 5 does and does not run: [docs/slice-5-mvp.md](docs/slice-5-mvp.md).

## Releases

Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). After a change merges to `main`, tag that merge commit with an annotated tag `vMAJOR.MINOR.PATCH` and push the tag. Put the notes for that version in [CHANGELOG.md](CHANGELOG.md) on the same commit, so the tag points at the entry it publishes.

- **MINOR** for a feature that keeps existing contracts. [v0.1.0](https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/releases/tag/v0.1.0) is the first release (Sprints 1–4, the open points from those sprints, and live quiz sessions).
- **PATCH** for a fix. [v0.1.1](https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/releases/tag/v0.1.1) returns slot questions to dropdowns.
- **MAJOR** when a released contract changes in a breaking way.

Pushing a `v*` tag runs [`.github/workflows/release.yml`](.github/workflows/release.yml). The workflow reads the matching changelog section and opens a GitHub Release. If that release already exists, the workflow leaves it alone. `v0.1.0` and `v0.1.1` were tagged before this workflow was on `main`, so those two releases were created by hand.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (this repo targets `net10.0`)
- Docker Compose **or** a local PostgreSQL 16 instance
- `dotnet-ef` for migrations (`dotnet tool install --global dotnet-ef`)
- Node.js LTS + Angular CLI 21 (for the web client)

Docker is used to run PostgreSQL locally, and optionally Mailpit when you want to read invite email in a browser. The app itself runs with `dotnet run`.

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

The same Compose file also starts **Mailpit** (SMTP `1025`, web UI `http://localhost:8025`). Development does not send through it unless you opt in (see [Invite email](#invite-email)).

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

There is no Compose file at the repo root, so `-f deploy/local/compose.yaml` is required. `-d` leaves Postgres and Mailpit in the background. Database `interviewquiz`, user `interviewquiz`, password `interviewquiz_dev_only`, port `5432` — the same values as [Local PostgreSQL](#local-postgresql). Mailpit SMTP is `localhost:1025` and the mailbox UI is `http://localhost:8025`. Development still uses the file sink until you set `InviteMail__Mode` to `Smtp` (see [Invite email](#invite-email)).

### 2. API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/InterviewQuiz.Host
```

Set `ASPNETCORE_ENVIRONMENT` in that same PowerShell session before `dotnet run`. The `http` launch profile also sets it and listens on `http://localhost:5147` (`src/InterviewQuiz.Host/Properties/launchSettings.json`).

In Development the host applies EF migrations on startup for `AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, `DeliveryDbContext`, `EvaluationDbContext`, and `SearchDbContext` (`Program.cs`), then seeds Development-only data. The manual `dotnet ef database update` steps under [Migrate and run](#migrate-and-run) are optional for this path.

ASP.NET Core Data Protection keys are written to PostgreSQL (`access.data_protection_keys`) by that same migration. Stopping this PowerShell `dotnet run` and starting it again, or redeploying the API against this Compose database, still unprotects invite tokens, so **Resend** keeps working. There is no key file to copy and no extra environment variable. Production uses the same table in the production database. Every API instance must share that database and the application name `InterviewQuiz`. If the table is lost, existing links must be re-issued; new links still work.

On first run, ERR log lines about `__EFMigrationsHistory` are harmless, including lines that name the `delivery` or `evaluation` schemas. The host queries that table before it exists, then creates it and applies migrations for `access`, `openings`, `catalog`, `delivery`, `evaluation`, and `search`. Startup continues.

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

Playwright end-to-end tests do not need PostgreSQL or the API. From `src\interview-quiz-web`:

```powershell
npx playwright install chromium
npm run e2e
```

Do not pass `--with-deps` on Windows. That flag installs Linux system packages. Full steps are in `src/interview-quiz-web/README.md`.

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
| `Jwt__CandidateAccessTokenMinutes` | Candidate magic-link access token lifetime (default `60`, range 1–180). No candidate refresh token |
| `PublicBaseUrl` | Public origin for recruiter-copied invite URLs (no trailing slash; not a secret). Access `IMagicLinkService.BuildInviteUrl` produces `{PublicBaseUrl}/attempt?token=` |
| `Invite__DefaultLifetimeHours` | Default magic-link lifetime when the recruiter omits `expiresAt`. Default `168`. `0` means no time limit until someone sets one |
| `InviteMail__Mode` | `Sink` (default) or `Smtp`. Testing always uses an in-memory mailer and never opens SMTP |
| `InviteMail__FromAddress` | From address on invite email |
| `InviteMail__FromDisplayName` | Display name on invite email |
| `InviteMail__Subject` | Subject line |
| `InviteMail__SinkDirectory` | Folder for sink `.eml` files. Empty uses `src/InterviewQuiz.Host/App_Data/invite-mail` |
| `InviteMail__Smtp__Host` | SMTP host. Mailpit on this Compose file is `localhost` |
| `InviteMail__Smtp__Port` | SMTP port. Mailpit is `1025` |
| `InviteMail__Smtp__UseSsl` | `true` or `false`. Mailpit local SMTP is `false` |
| `InviteMail__Smtp__Username` | Optional SMTP username. Leave empty for Mailpit |
| `InviteMail__Smtp__Password` | Optional SMTP password. Do not commit a real password |
| `Bootstrap__SuperAdminEmail` | Optional mailbox for the first Super Admin. Empty seeds the role and no user. No password is stored in git |
| `Bootstrap__ResetSuperAdminSetup` | `true` re-issues the one-time password token on startup while the user still must set a password |
| `Ai__Provider` | `None` (default), `Fake`, or `OpenAiCompatible`. Development and Testing use `Fake`. Review suggestions and authoring drafts stay off unless this is `Fake`, or `OpenAiCompatible` with a base URL, model, and API key |
| `Ai__BaseUrl` | OpenAI-compatible origin, for example `https://llm.example/v1`. Not a secret. Required only for `OpenAiCompatible` |
| `Ai__Model` | Model name sent to that endpoint |
| `Ai__ApiKey` | Bearer token for that endpoint. Set this in the environment. Do not commit a real key |
| `Ai__TimeoutSeconds` | HTTP timeout for a suggestion or an authoring draft. Default `30`, capped at 120 |

Production must set `Jwt__SigningKey` (environment, OS-protected file, or Vault). The Development signing key in `appsettings.Development.json` is **local-only** and not for Production.

## Migrate and run

Development auto-applies EF migrations (`AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, `DeliveryDbContext`, `EvaluationDbContext`, `SearchDbContext`), seeds the permission catalog, Development Identity users/roles, sample openings plus default field keys (`Client`, `Project`, `Role`), one sample quiz (every v1 question type) under the backend opening, publishes that quiz as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`, three live **bank questions**, and one sample **async assignment** `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001` (`candidate.dev@example.com`, 30 minutes, attempt limit 1). Invite raw tokens are **not** seeded; issue at runtime. **Never seeded in Production.**

Because that startup path applies the migrations, the manual `dotnet ef database update` commands below are optional when the host runs in Development. On first run, ERR log lines about `__EFMigrationsHistory` are harmless, including lines for the `delivery` and `evaluation` schemas. The host queries that table before it exists, then creates it in `access`, `openings`, `catalog`, `delivery`, `evaluation`, and `search` and continues. PowerShell for the same flow: [Windows (PowerShell) quick start](#windows-powershell-quick-start).

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

# Delivery schema (assignments + snapshots)
dotnet ef database update \
  --project src/Modules/Delivery/InterviewQuiz.Delivery.Infrastructure \
  --startup-project src/InterviewQuiz.Host \
  --context DeliveryDbContext

# Evaluation schema (attempts + answers)
dotnet ef database update \
  --project src/Modules/Evaluation/InterviewQuiz.Evaluation.Infrastructure \
  --startup-project src/InterviewQuiz.Host \
  --context EvaluationDbContext

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

Candidates enter with a **magic-link**: `POST /api/auth/magic-link/consume` with `{ "token": "<opaque>" }` returns a short-lived assignment-scoped JWT (`candidate.attempt.participate` + `assignment_id`). The invite is reusable until the assignment is submitted or the recruiter rotates it. No candidate refresh token. Recruiter invite HTTP (`POST /api/assignments/{id}/invite`) is Delivery; Access exposes `IMagicLinkService.IssueAsync` (raw token, hashed in `access.magic_link_invites`) and `BuildInviteUrl`. Candidates have **no password** — do not invent one.

Not in this slice: Entra ID. Invite SMTP is configuration; see [Invite email](#invite-email).

### Sign in locally (Development dummy users)

These accounts exist only when the host runs in Development (or tests seed them). Do not use them in Production.

| Email | Password | Sample bundle | Slice 4–5 notes |
|-------|----------|----------------|---------------|
| `recruiter.dev@example.com` | `Dev.Recruiter!1` | Dev Recruiter | `templates.read` + `filters.write` + `filters.share`; **`assignments.write`** + `assignments.read` + `attempts.read` + **`reports.read`**; **no** `quizzes.write` (cannot clone); **no** `questions.*` |
| `author.dev@example.com` | `Dev.Author!1` | Dev Template author | `templates.write` + `quizzes.write` + `questions.read` + `questions.write`; `filters.write`; **`reports.read`**; **typically no** `assignments.*` (no Assignments nav) |
| `reviewer.dev@example.com` | `Dev.Reviewer!1` | Dev Reviewer | `assignments.read` + `attempts.read` + `attempts.review` + **`reports.read`**; no template/filter/bank write |
| `admin.dev@example.com` | `Dev.Admin!1` | Dev Admin | **`reports.read`**. Role editor can assign `questions.*` / `assignments.*`; they are not on this seed bundle |

There is **no** dummy candidate password. `candidate.dev@example.com` is magic-link only (seed assignment `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001`). Dummy passwords stay in this table only.

```bash
curl -s http://localhost:5147/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin.dev@example.com","password":"Dev.Admin!1"}'
```

Use `Authorization: Bearer {accessToken}` on subsequent requests. Call `POST /api/auth/refresh` with the refresh token to rotate; the new access token re-reads permissions from the database (role changes take effect on the next tokens). Call `POST /api/auth/logout` with the refresh token to revoke it.

Angular should keep the employee access token **in memory** and the refresh token in **sessionStorage** (not `localStorage`, Cache Storage, IndexedDB, or the service worker). Candidate `/attempt` keeps its access token in-memory on `CandidateSession` only (no refresh; not `TokenStore`). See `src/interview-quiz-web/README.md`. Service workers must not cache tokens, `/me`, login, refresh, assignments, attempts, or magic-link URLs.

Do **not** use `[Authorize(Roles = ...)]`. Roles are operator-composed permission sets; API and UI check permission codes only.

## Access API

| Method | Path | Permission |
|--------|------|------------|
| `POST` | `/api/auth/login` | anonymous |
| `POST` | `/api/auth/refresh` | anonymous (valid refresh token) |
| `POST` | `/api/auth/logout` | anonymous (refresh token body) |
| `POST` | `/api/auth/magic-link/consume` | anonymous (opaque invite) |
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
| `POST` | `/api/ai-drafts` | `ai.draft.use` |
| `GET` | `/api/ai-drafts/{id}` | `ai.draft.use` (creator only; other callers get 404) |
| `POST` | `/api/ai-drafts/{id}/accept` | `ai.draft.use` **and** `quizzes.write` |

Quiz list query: `openingId`, `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags` (JSON object), `criteria` (full JSON — same shape Search persists for `target: "quizzes"`), `page`, `pageSize` (capped at 100). JSON is camelCase. Eight question `type` values: `multipleChoiceSingle`, `multipleChoiceMulti`, `trueFalse`, `shortText`, `longText`, `dragDropSharedBank`, `dragDropPerSlot`, `ordering`. Scoring: `auto` / `aiAssist` / `humanOnly`. `creditMode` (`partial` / `allOrNothing`) is required only for `multipleChoiceMulti` and `ordering`. PUT replaces the full question list and requires `rowVersion`. Create may send an empty `questions` array. Unknown `openingId` returns 400 `"Opening does not exist."` (Catalog calls `IOpeningLookup` in-process; it does not read the `openings` schema).

Template list query: `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags`, `criteria` (full JSON for `target: "templates"`), `page`, `pageSize`. List/get template returns latest-version **metadata** (no question bodies). Version list is paged (`page` / `pageSize`). Version get returns full `questions`. Clone body: `openingId` (required), `title` (optional). Clone 201 `Location: /api/quizzes/{newId}`. Publish 201 `Location: /api/templates/{templateId}` with `templateId`, `versionId`, `versionNumber`, `createdNewTemplate`.

A quiz belongs to **exactly one** opening. Reuse across openings is via **template** (publish / clone). Code question types are rejected. `QuizResponse` includes `originTemplateId` and `sourceTemplateVersionId` (not client-writable on create/update). Questions include optional `sourceQuestionId` (bank provenance; include sets it; quiz CRUD still persists it as sent).

Question-bank list query: `keyword` (title **or** stem), `type` (camelCase), `experienceMinYears` / `experienceMaxYears`, `tags` (JSON object), `criteria` (`BankQuestionListCriteria` JSON), `archived` (default `false`; `true` returns only archived and requires `questions.write` else 403), `page`, `pageSize`. `BankQuestionResponse` has no `sortOrder`. Create/update body: `title`, `tags`, `expectedExperienceYears` (0–80), `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`. Update requires `rowVersion`. Client `sourceQuestionId` is ignored. No hard delete — archive / unarchive. Unknown GET is 404. Archive already archived / unarchive when live is 400. The Angular bank list has **no** SavedFilters (`FilterTarget` has no `questions`).

Include body `IncludeQuestionsRequest`: `questionIds` (required, unique, non-empty), `insertAt` (optional 0-based index into the current quiz list; default append; clamped 0..count), `rowVersion` (quiz concurrency). Missing bank id → 400 `"Question does not exist."` Archived bank item → 400 `"Archived question cannot be included."` Copies type/stem/scoring/credit/points/body, **new quiz question ids**, `sourceQuestionId` = bank item id. Returns the updated `QuizResponse` (200).

Concurrency: integer `row_version` on Quiz and BankQuestion (incremented on update), same pattern as Opening. Publish also increments the quiz `rowVersion`. Include increments the quiz `rowVersion`.

In-process: `IQuizSnapshotReader.GetSnapshotAsync(quizId)` returns an immutable DTO of questions, keys, and scoring for Delivery to copy at assign time. `IQuestionBankReader.GetByIdAsync(id)` is registered and used **only** by include (not quiz create/update/publish/clone). GetById returns archived rows so include can reject them.

AI authoring draft (`POST /api/ai-drafts`): body `openingId` (required), optional `resumeText` (paste, max 8000), optional `rulesText` (max 4000), and optional `templateId` plus `templateVersionId` (both or neither). The response is a draft. It does not create a quiz or an assignment. `POST /api/ai-drafts/{id}/accept` body is `title`, `description`, `expectedExperienceYears`, and `questions`. The opening id stays the draft’s opening. Accept is **400** `Edit the draft before saving it as a quiz.` when the question list still matches the generated questions. A second accept is **409** `This draft was already saved as a quiz.` A caller who did not create the draft gets **404**. Unconfigured AI is **400** `AI drafts are not configured.` and writes no row. Development uses the same deterministic fake as review suggestions (`Ai:Provider` = `Fake`). Angular: `/ai-drafts/new` for `ai.draft.use`. Save as quiz is shown only with `quizzes.write`. There is no assign action on this screen. `GET|POST|PUT /api/ai-rule-sets` is not implemented.

## Delivery API (slice 5 — assignments)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/assignments` | `assignments.read` |
| `GET` | `/api/assignments/{id}` | `assignments.read` |
| `POST` | `/api/assignments` | `assignments.write` |
| `POST` | `/api/assignments/from-template` | `assignments.write` and `templates.read` |
| `POST` | `/api/assignments/{id}/invite` | `assignments.write` |
| `POST` | `/api/assignments/{id}/invite/resend` | `assignments.write` |
| `POST` | `/api/assignments/{id}/invite/revoke` | `assignments.write` |
| `POST` | `/api/assignments/{id}/invite/extend` | `assignments.write` |

List query: `openingId`, `keyword` (candidate email or snapshot title), `page`, `pageSize`. No `criteria` JSON. Create body: `openingId`, `quizId`, `candidateEmail`, `mode` (`async` \| `live`), `timing.overallDurationMinutes` (required 1–480 for async), `attemptLimit` (optional, default 1), `inviteExpiresAt` (optional). POST create 201 `Location: /api/assignments/{id}`. Async create includes `inviteUrl` once (`{PublicBaseUrl}/attempt?token=`) and sends the invite email. GET never returns `inviteUrl`. It does return `inviteState` (`none` \| `active` \| `expired` \| `revoked`), `inviteExpiresAt`, and `inviteEmailStatus` (`none` \| `sent` \| `failed` \| `resent`). Live create omits `inviteUrl` and does not send email. No PUT/DELETE.

`POST /api/assignments/from-template` clones the template version into the opening (latest version when `templateVersionId` is omitted), then creates the assignment and snapshot from that new quiz. It does **not** require `quizzes.write`. Body: `openingId`, `templateId`, optional `templateVersionId`, `candidateEmail`, `mode`, `timing`, `attemptLimit`, optional `quizTitle`, optional `inviteExpiresAt`.

`POST .../invite` still rotates the link (the previous token stops working) and emails the new URL. Optional body `{ "expiresAt": "<utc>" }`. `POST .../invite/revoke` revokes the current link and does not issue a replacement. `POST .../invite/extend` body `{ "expiresAt": "<utc>" | null }` changes expiry on the same link (`null` clears the time limit). `POST .../invite/resend` emails the current link again and does not rotate it. A link issued before this change has no recoverable token; resend then asks for a re-issue.

## Invite email

Sending goes through `IInviteMailer`. The raw token is in the email body only. It is not written to logs. A data-protected copy is stored so resend can rebuild the same URL; consume still looks up the hash.

| `InviteMail__Mode` | What happens |
|--------------------|----------------|
| `Sink` (Development default) | Writes `App_Data/invite-mail/*.eml` under the host project and logs the assignment id plus file name |
| `Smtp` | Sends with `System.Net.Mail` using `InviteMail__Smtp__*` |

PowerShell, same session as `dotnet run`, to use Mailpit from `deploy/local/compose.yaml`:

```powershell
$env:InviteMail__Mode = "Smtp"
$env:InviteMail__Smtp__Host = "localhost"
$env:InviteMail__Smtp__Port = "1025"
$env:InviteMail__Smtp__UseSsl = "false"
dotnet run --project src/InterviewQuiz.Host
```

Bash equivalent:

```bash
export InviteMail__Mode=Smtp
export InviteMail__Smtp__Host=localhost
export InviteMail__Smtp__Port=1025
export InviteMail__Smtp__UseSsl=false
dotnet run --project src/InterviewQuiz.Host
```

Open `http://localhost:8025` to read the message. Tests never use this path: the Testing environment registers `RecordingInviteMailer`, which keeps messages in memory and does not open a socket.

`Invite__DefaultLifetimeHours` defaults to `168`. Set it to `0` when new invites should stay valid until a recruiter sets `expiresAt`, revokes them, or rotates them. An expired or revoked link cannot be consumed. After submit, an invite that is still active can be consumed to view the summary (ADR 0008). Expiry and revoke are additional stops (ADR 0009).

Expiry does not cut off a candidate who already started. Their JWT can still save and submit until the attempt is submitted or the quiz timer (`dueAtUtc`) runs out. Opening the link again shows the expiry page. Revoke is different: it ends the in-progress attempt immediately, scores the answers saved so far, and the candidate page says the quiz has ended. They cannot keep answering.

Invite delivery is email only. Ticketing is out of scope.

The resend token is protected with ASP.NET Core Data Protection. Keys are persisted in PostgreSQL table `access.data_protection_keys` (application name `InterviewQuiz`), in Development and in production. A restart or a new instance that uses the same database can still resend the current link. Do not point instances at different databases if they must resend each other's links. There is no filesystem key ring to mount. On Windows, the PowerShell `dotnet run` in [Windows (PowerShell) quick start](#windows-powershell-quick-start) uses the Compose database for those keys; restarting that session does not invalidate resend.

Development seed assignment `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001` (opening `3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001`, quiz `4b8d2e21-7c3e-4d0b-8f22-1a9d3c7b2001`, `candidate.dev@example.com`). To copy an invite URL after `dotnet run` (Development):

```bash
TOKEN=$(curl -s http://localhost:5147/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"recruiter.dev@example.com","password":"Dev.Recruiter!1"}' \
  | python -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])')
curl -s http://localhost:5147/api/assignments/7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001/invite \
  -H "Authorization: Bearer $TOKEN" -X POST
```

## Evaluation API (slice 5 — async attempts)

| Method | Path | Permission |
|--------|------|------------|
| `POST` | `/api/assignments/{id}/attempts` | `candidate.attempt.participate` + `assignment_id` claim |
| `GET` | `/api/assignments/{id}/attempts/preview` | `candidate.attempt.participate` + `assignment_id` claim |
| `GET` | `/api/assignments/{id}/attempts/current` | `candidate.attempt.participate` + `assignment_id` claim |
| `PUT` | `/api/assignments/{id}/attempts/current/answers` | `candidate.attempt.participate` + `assignment_id` claim |
| `POST` | `/api/assignments/{id}/attempts/current/submit` | `candidate.attempt.participate` + `assignment_id` claim |
| `GET` | `/api/assignments/{id}/attempts` | `attempts.read` |
| `GET` | `/api/attempts/{id}` | `attempts.read` |
| `GET` | `/api/attempts/{id}/review` | `attempts.review` |
| `POST` | `/api/attempts/{id}/items/{itemId}/review` | `attempts.review` |
| `POST` | `/api/attempts/{id}/items/{itemId}/suggestion` | `attempts.review` |

Candidate mismatch on `assignment_id` is **403**. Live assignments reject start with 400 `Assignment is live; start is not available.` Auto-score uses snapshot keys only (no LLM). Employee results omit keys. `attempts.read` does not return answer keys or review comments. A reviewer with `attempts.review` opens `/assignments/{assignmentId}/attempts/{attemptId}/review`, scores each `humanOnly` or `aiAssist` item from 0 through that item’s points, and may leave a comment of up to 2000 characters. Auto items cannot be re-scored. When every item is settled, the attempt stores `finalPointsAwarded` and the assignment moves from `pendingReview` to `completed`. Dev Reviewer (`reviewer.dev@example.com` / `Dev.Reviewer!1`) has this permission in Development. For an `aiAssist` item the same permission can request a suggestion. The draft (points, feedback, actor, model, time) stays after the reviewer saves a score, including when they edit the points. `humanOnly` items have no suggestion. Development uses a deterministic fake (empty answer scores 0; any other answer scores half the item points). Set `Ai__Provider` to `OpenAiCompatible` plus `Ai__BaseUrl`, `Ai__Model`, and `Ai__ApiKey` to call an operator endpoint. If those are missing, suggestions are off and scoring still works. The suggestion is not on `GET /api/attempts/{id}`.

Still later: Entra. AI authoring drafts are in [Catalog API](#catalog-api-slices-24--quizzes-templates-question-bank). Invite email is in [Invite email](#invite-email).

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

Workspace: `src/interview-quiz-web`. Official CLI (`ng new`, `ng generate`, `ng add @angular/pwa`). Slice 1: sign-in, openings, permission-aware admin. Slice 2: quiz list and authoring editor. Slice 3: template library, clone/publish, saved list filters. Slice 4: question bank list/editor and include-from-quiz. Slice 5: employee assignments + candidate `/attempt` magic-link.

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
npm install
ng serve
```

`ng serve` uses `proxy.conf.json` so the browser talks same-origin to `/api` and `/health`, forwarded to `http://localhost:5147`. Run the API first (`dotnet run --project src/InterviewQuiz.Host`), then the SPA at `http://localhost:4200`. On Windows, `npx ng serve` after `npm install` is in [Windows (PowerShell) quick start](#windows-powershell-quick-start).

Employee routes used in slices 2–5 (permission on the route, same codes as the API):

| Path | Permission | Screen |
|------|------------|--------|
| `/quizzes` | `quizzes.read` | Quiz list (opening, keyword, experience, saved filters) |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` **or** `quizzes.write` | View / edit quiz; `templates.write` can publish as template; include from bank when `quizzes.write` **and** `questions.read` on an existing saved quiz |
| `/templates` | `templates.read` | Template list (keyword, experience, tags, saved filters) |
| `/templates/:id` | `templates.read` | Template detail, version history (author, date, change note). Metadata edit when `templates.write` |
| `/templates/:id/edit` | `templates.write` | Edit the latest version. Save creates the next version |
| `/questions` | `questions.read` | Question bank list (keyword, type, experience, tags; archived-only toggle only with `questions.write`). **No** SavedFilters |
| `/questions/new` | `questions.write` | Create bank question |
| `/questions/:id` | `questions.read` **or** `questions.write` | View / edit bank question; archive/unarchive with write |
| `/assignments` | `assignments.read` | Assignment list (opening, quiz id, keyword, pager). **No** SavedFilters |
| `/assignments/new` | `assignments.write` | Create assignment (opening, quiz for that opening, email, async/live, duration, attempt limit) |
| `/assignments/:id` | `assignments.read` | Assignment detail, join or invite link (`assignments.write`), **Open live session** when the mode is live, basic results if `attempts.read` |
| `/assignments/:id/live` | `assignments.read` **or** `attempts.read` **or** `sessions.live.run` | Live progress. Start, pause, resume, and end require `sessions.live.run` |
| `/reports` | `reports.read` | Reporting v1 hub |
| `/ai-drafts/new` | `ai.draft.use` | Generate an AI quiz draft. Save as quiz requires `quizzes.write` and a question edit. Nothing is assigned |
| `/reports/:reportKey` | `reports.read` | Funnel, scores, time, questions, candidates, or pending review |

Candidate (outside the employee shell): `/attempt?token=` — `POST /api/auth/magic-link/consume`, then `GET .../attempts/preview`. A welcome page (quiz, opening, duration, question count, rules) calls start only from **Start quiz**, which sets `dueAtUtc`. An in-progress or submitted attempt skips welcome. Isolated in-memory `CandidateSession` (not employee `TokenStore`). No candidate password. The attempt shows a muted type badge and hides points. Ordering uses pointer drag plus Move up and Move down. Shared-bank and per-slot questions use a dropdown for each slot and say “Match each slot”. Shuffle questions and shuffle multiple-choice options are quiz settings; both stay off until an author turns them on. Recruiter attempt detail labels each answer Correct, Wrong, Partly correct, or Pending review.

Shell nav and home link to Quizzes when the user has `quizzes.read`, to **AI draft** when `ai.draft.use`, to Templates when `templates.read`, to **Question bank** when `questions.read`, to **Assignments** when `assignments.read`, and to **Reporting** when `reports.read`. Create assignment is hidden without `assignments.write`. Recruiter (`templates.read`, **`assignments.write`**, no `quizzes.write`, **no** `questions.*`) can assign and copy invites, browse templates but not clone, and does not see bank nav or include. Author (`quizzes.write` + `templates.read` + `templates.write` + `questions.read` + `questions.write`) can clone, publish, author bank items, and include from the bank; the author seed typically has **no** `assignments.*`. Live assignments require a duration (1–480). Create does not return an invite URL. `assignments.write` issues a join link from the assignment. The host runs the session at `/assignments/:id/live` (`sessions.live.run`). The candidate waits in a lobby until the host starts. Decisions: [ADR 0010](docs/adr/0010-live-quiz-sessions.md). Full employee route table: `docs/architecture.md` §6. Client details: `src/interview-quiz-web/README.md`. Shared UI primitives: [`docs/components/README.md`](docs/components/README.md).

### Exercise slices 2–5 locally (Development-only)

Dummy users and passwords are in the table under **Sign in locally** above — do not copy them elsewhere. Use:

- `author.dev@example.com` (Dev Template author) for **write**: list/create/edit quizzes (`quizzes.write`), publish as template (`templates.write`), clone (`quizzes.write` + `templates.read`), question bank (`questions.read` + `questions.write`), include from an existing quiz (`quizzes.write` + `questions.read`), save filters (`filters.write`). Typically **cannot** assign.
- `recruiter.dev@example.com` (Dev Recruiter) for **assignments** (`assignments.write`): list/create, copy an async invite or a live join link (`POST /api/assignments/{id}/invite`), run a live session (`sessions.live.run`), basic results (`attempts.read`). Also **read-only quizzes and templates**: list and open; quiz form disabled (`quizzes.read` without `quizzes.write`). Can save and **share** filters (`filters.write` + `filters.share`). Cannot clone. **No** `questions.*` — no bank nav, no include panel.

Candidates: open `{PublicBaseUrl}/attempt?token=` from the email or from the link the recruiter copied. An async link shows instructions first; the timer starts on **Start quiz**. A live link waits until the interviewer starts. **No** candidate password. After submit, the same link shows the submitted summary while the invite is still active. If the candidate sign-in session expires during a live quiz, opening the same link continues the session. An expired or revoked link shows its own page and does not open the quiz.

Development seed includes one sample quiz with every v1 question type under the backend opening, published as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`, three live bank questions (`6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4001` MC single, `…4002` true/false, `…4003` short text), and one sample **async assignment** `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001` (`candidate.dev@example.com`). Invite raw tokens are **not** seeded. **Never seeded in Production.**

```bash
ng build
ng test --watch=false
```

`ng test` runs **Vitest** (Angular unit/component tests, including quiz list/editor, templates, SavedFilters, question bank list/editor/include, assignment list/form, and candidate `/attempt`). The client has component specs for those screens.

### Storybook

OSS Storybook 10 (`@storybook/angular-vite`, Angular 21 zoneless application builder) galleries shared primitives only (PageStatus, OfflineBanner, HasPermission, SavedFilters). No Chromatic. Playbook markdown remains the source of API tables: `docs/components/README.md`.

```bash
cd src/interview-quiz-web
npm run storybook
```

Opens **http://localhost:6006**. Static build: `npm run build-storybook`.

PWA: installable manifest + service worker that caches the **hashed app shell only**. No `/api` data groups, no IndexedDB outbox. Offline UI is a banner. Token storage, dummy users, and correlation id are documented in `src/interview-quiz-web/README.md`.

## Reporting v1

`reports.read` opens company-wide reports. The host composes them from Delivery and Evaluation public readers. There is no reporting schema. Pass rate is 70%. When an attempt has a final score, pass is `finalPointsAwarded / totalPointsAvailable`. Until that score exists, pass stays `autoPointsAwarded / autoPointsAvailable`, and unsettled human or AI points are not treated as zero. The score histogram and candidate rank stay on auto points. The default window is assignments created in the last 90 days. CSV export is UTF-8 with no answer keys and refuses more than 10,000 rows. Discrimination (top quartile minus bottom quartile) is hidden when fewer than 30 settled auto responses exist. Live time subtracts `TotalPausedSeconds` once. Async time does not. Each report has its own saved-filter target. There is no template-origin filter and no `assignments` saved-filter target. Every Development employee account and Super Admin can open Reporting. A candidate session cannot. Notes for the v0.2.0 release are under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md).

## Tests

```bash
dotnet test InterviewQuiz.slnx
```

Unit tests always run (JWT validation, `HasPermission`, login success/failure with fakes, Catalog question/quiz/template rules, Search filter rules, Delivery assignment rules, Evaluation auto-score). WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents). The variable has to be present in the same shell session as `dotnet test`. Integration tests use the `Testing` environment, so `appsettings.Development.json` is not a substitute. Example matching `deploy/local/compose.yaml`:

```powershell
$env:ConnectionStrings__InterviewQuiz = "Host=localhost;Port=5432;Database=interviewquiz;Username=interviewquiz;Password=interviewquiz_dev_only"
dotnet test InterviewQuiz.slnx
```

When the database is present, those tests authenticate with JWT (not `Authorization: Test`) and include Catalog quiz/template API coverage (`CatalogApiTests`, `TemplateEditApiTests`), question-bank API coverage (`QuestionBankApiTests`), saved-filter API coverage (`SearchApiTests`), assignment API coverage (`AssignmentsApiTests`), opening summary coverage (`OpeningSummaryApiTests`), Super Admin coverage (`SuperAdminApiTests`), magic-link consume (`MagicLinkApiTests`), attempt API coverage (`AttemptsApiTests`), and Reporting v1 coverage (`ReportingApiTests`). That coverage includes metadata versions that reuse question rows, author display names, revoke ending an in-progress attempt, expiry leaving that attempt runnable, and invite resend after a new host reads the Postgres data-protection keys. With PostgreSQL set, `dotnet test` reports **260 passed** (unit plus host integration). `npm test` reports **127 passed**. Playwright `npm run e2e` reports **24 passed**.

Angular (Vitest):

```bash
cd src/interview-quiz-web
ng test --watch=false
```

Playwright covers the candidate welcome, hidden type and points, ordering drag-and-drop (including the keyboard alternative), recruiter attempt detail, template metadata reuse plus slot editing, revoke ending an in-progress attempt, and a mobile project at 375×812 (`mobile-layout.spec.ts`: assignment list, attempt questions, openings, quizzes, templates, question bank). It mocks `/api`, so the database is not required. On Windows, install Chromium with `npx playwright install chromium` and do not pass `--with-deps`. Full steps are in `src/interview-quiz-web/README.md`.

```bash
cd src/interview-quiz-web
npm run e2e
```

## Solution layout

- `src/InterviewQuiz.Host` — composition root
- `src/InterviewQuiz.Kernel` — clock, pagination, tags, permission code constants
- `src/Modules/Access` — Identity user store, JWT issue/refresh/revoke, permission catalog, roles as permission sets (`access` schema)
- `src/Modules/Openings` — Domain / Application / Infrastructure (`openings` schema)
- `src/Modules/Catalog` — Domain / Application / Infrastructure (`catalog` schema; quiz authoring, templates, question bank)
- `src/Modules/Search` — saved filters (`search` schema)
- `src/Modules/Delivery` — assignments + snapshots (`delivery` schema)
- `src/Modules/Evaluation` — async attempts + auto-score (`evaluation` schema)
- `src/interview-quiz-web` — Angular SPA + installable PWA (slices 1–5: openings, quizzes, templates, question bank, saved filters, assignments, candidate `/attempt`)
- `deploy/local/compose.yaml` — local PostgreSQL 16
