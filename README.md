# Interview Quiz Platform

ASP.NET Core Modular Monolith + Angular SPA/PWA. Slices **1–5** are in this branch: **host**, **Access** (JWT + Identity + permissions + assignment-scoped candidate magic-link JWTs), **Openings**, **Catalog**, **templates**, **question bank**, **Search** saved filters, **Delivery** assignments/snapshots, **Evaluation** async attempts/auto-score, and the **web client** (employee assignments + candidate `/attempt`). Live start/pause, human review, AI drafts, Entra ID, and SMTP are **not** in this pass. What slice 5 does and does not run: [docs/slice-5-mvp.md](docs/slice-5-mvp.md).

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
| `Jwt__CandidateAccessTokenMinutes` | Candidate magic-link access token lifetime (default `60`, range 1–180). No candidate refresh token |
| `PublicBaseUrl` | Public origin for recruiter-copied invite URLs (no trailing slash; not a secret). Access `IMagicLinkService.BuildInviteUrl` produces `{PublicBaseUrl}/attempt?token=` |

Production must set `Jwt__SigningKey` (environment, OS-protected file, or Vault). The Development signing key in `appsettings.Development.json` is **local-only** and not for Production.

## Migrate and run

Development auto-applies EF migrations (`AccessDbContext`, `OpeningsDbContext`, `CatalogDbContext`, `DeliveryDbContext`, `EvaluationDbContext`, `SearchDbContext`), seeds the permission catalog, Development Identity users/roles, sample openings plus default field keys (`Client`, `Project`, `Role`), one sample quiz (every v1 question type) under the backend opening, publishes that quiz as template `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`, three live **bank questions**, and one sample **async assignment** `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001` (`candidate.dev@example.com`, 30 minutes, attempt limit 1). Invite raw tokens are **not** seeded; issue at runtime. **Never seeded in Production.**

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

Not in this slice: Entra ID, SMTP.

### Sign in locally (Development dummy users)

These accounts exist only when the host runs in Development (or tests seed them). Do not use them in Production.

| Email | Password | Sample bundle | Slice 4–5 notes |
|-------|----------|----------------|---------------|
| `recruiter.dev@example.com` | `Dev.Recruiter!1` | Dev Recruiter | `templates.read` + `filters.write` + `filters.share`; **`assignments.write`** + `assignments.read` + `attempts.read`; **no** `quizzes.write` (cannot clone); **no** `questions.*` |
| `author.dev@example.com` | `Dev.Author!1` | Dev Template author | `templates.write` + `quizzes.write` + `questions.read` + `questions.write`; `filters.write`; **typically no** `assignments.*` (no Assignments nav) |
| `reviewer.dev@example.com` | `Dev.Reviewer!1` | Dev Reviewer | `assignments.read` + `attempts.read`; no template/filter/bank write |
| `admin.dev@example.com` | `Dev.Admin!1` | Dev Admin | Role editor can assign `questions.*` / `assignments.*`; they are not on this seed bundle |

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

Quiz list query: `openingId`, `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags` (JSON object), `criteria` (full JSON — same shape Search persists for `target: "quizzes"`), `page`, `pageSize` (capped at 100). JSON is camelCase. Eight question `type` values: `multipleChoiceSingle`, `multipleChoiceMulti`, `trueFalse`, `shortText`, `longText`, `dragDropSharedBank`, `dragDropPerSlot`, `ordering`. Scoring: `auto` / `aiAssist` / `humanOnly`. `creditMode` (`partial` / `allOrNothing`) is required only for `multipleChoiceMulti` and `ordering`. PUT replaces the full question list and requires `rowVersion`. Create may send an empty `questions` array. Unknown `openingId` returns 400 `"Opening does not exist."` (Catalog calls `IOpeningLookup` in-process; it does not read the `openings` schema).

Template list query: `keyword`, `experienceMinYears`, `experienceMaxYears`, `tags`, `criteria` (full JSON for `target: "templates"`), `page`, `pageSize`. List/get template returns latest-version **metadata** (no question bodies). Version list is paged (`page` / `pageSize`). Version get returns full `questions`. Clone body: `openingId` (required), `title` (optional). Clone 201 `Location: /api/quizzes/{newId}`. Publish 201 `Location: /api/templates/{templateId}` with `templateId`, `versionId`, `versionNumber`, `createdNewTemplate`.

A quiz belongs to **exactly one** opening. Reuse across openings is via **template** (publish / clone). Code question types are rejected. `QuizResponse` includes `originTemplateId` and `sourceTemplateVersionId` (not client-writable on create/update). Questions include optional `sourceQuestionId` (bank provenance; include sets it; quiz CRUD still persists it as sent).

Question-bank list query: `keyword` (title **or** stem), `type` (camelCase), `experienceMinYears` / `experienceMaxYears`, `tags` (JSON object), `criteria` (`BankQuestionListCriteria` JSON), `archived` (default `false`; `true` returns only archived and requires `questions.write` else 403), `page`, `pageSize`. `BankQuestionResponse` has no `sortOrder`. Create/update body: `title`, `tags`, `expectedExperienceYears` (0–80), `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`. Update requires `rowVersion`. Client `sourceQuestionId` is ignored. No hard delete — archive / unarchive. Unknown GET is 404. Archive already archived / unarchive when live is 400. The Angular bank list has **no** SavedFilters (`FilterTarget` has no `questions`).

Include body `IncludeQuestionsRequest`: `questionIds` (required, unique, non-empty), `insertAt` (optional 0-based index into the current quiz list; default append; clamped 0..count), `rowVersion` (quiz concurrency). Missing bank id → 400 `"Question does not exist."` Archived bank item → 400 `"Archived question cannot be included."` Copies type/stem/scoring/credit/points/body, **new quiz question ids**, `sourceQuestionId` = bank item id. Returns the updated `QuizResponse` (200).

Concurrency: integer `row_version` on Quiz and BankQuestion (incremented on update), same pattern as Opening. Publish also increments the quiz `rowVersion`. Include increments the quiz `rowVersion`.

In-process: `IQuizSnapshotReader.GetSnapshotAsync(quizId)` returns an immutable DTO of questions, keys, and scoring for Delivery to copy at assign time. `IQuestionBankReader.GetByIdAsync(id)` is registered and used **only** by include (not quiz create/update/publish/clone). GetById returns archived rows so include can reject them.

## Delivery API (slice 5 — assignments)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/assignments` | `assignments.read` |
| `GET` | `/api/assignments/{id}` | `assignments.read` |
| `POST` | `/api/assignments` | `assignments.write` |
| `POST` | `/api/assignments/{id}/invite` | `assignments.write` |

List query: `openingId`, `keyword` (candidate email or snapshot title), `page`, `pageSize`. No `criteria` JSON. Create body: `openingId`, `quizId`, `candidateEmail`, `mode` (`async` \| `live`), `timing.overallDurationMinutes` (required 1–480 for async), `attemptLimit` (optional, default 1). POST create 201 `Location: /api/assignments/{id}`. Async create includes `inviteUrl` once (`{PublicBaseUrl}/attempt?token=`). GET never returns `inviteUrl`. Live create omits `inviteUrl`. No PUT/DELETE.

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
| `GET` | `/api/assignments/{id}/attempts/current` | `candidate.attempt.participate` + `assignment_id` claim |
| `PUT` | `/api/assignments/{id}/attempts/current/answers` | `candidate.attempt.participate` + `assignment_id` claim |
| `POST` | `/api/assignments/{id}/attempts/current/submit` | `candidate.attempt.participate` + `assignment_id` claim |
| `GET` | `/api/assignments/{id}/attempts` | `attempts.read` |
| `GET` | `/api/attempts/{id}` | `attempts.read` |

Candidate mismatch on `assignment_id` is **403**. Live assignments reject start with 400 `Assignment is live; start is not available.` Auto-score uses snapshot keys only (no LLM). Employee results omit keys. No `GET /api/attempts` without an assignment. No `attempts.review` endpoints.

Not in this slice: live start/pause REST, human review, AI drafts, Entra, SMTP.

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

`ng serve` uses `proxy.conf.json` so the browser talks same-origin to `/api` and `/health`, forwarded to `http://localhost:5147`. Run the API first (`dotnet run --project src/InterviewQuiz.Host`), then the SPA at `http://localhost:4200`.

Employee routes used in slices 2–5 (permission on the route, same codes as the API):

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
| `/assignments` | `assignments.read` | Assignment list (opening, keyword, pager). **No** SavedFilters |
| `/assignments/new` | `assignments.write` | Create assignment (opening, quiz for that opening, email, async/live, duration, attempt limit) |
| `/assignments/:id` | `assignments.read` | Assignment detail, copy invite (`assignments.write`), basic results if `attempts.read` |

Candidate (outside the employee shell): `/attempt?token=` — `POST /api/auth/magic-link/consume`, then start/save/submit. Isolated in-memory `CandidateSession` (not employee `TokenStore`). No candidate password.

Shell nav and home link to Quizzes when the user has `quizzes.read`, to Templates when `templates.read`, to **Question bank** when `questions.read`, and to **Assignments** when `assignments.read`. Create assignment is hidden without `assignments.write`. Recruiter (`templates.read`, **`assignments.write`**, no `quizzes.write`, **no** `questions.*`) can assign and copy invites, browse templates but not clone, and does not see bank nav or include. Author (`quizzes.write` + `templates.read` + `templates.write` + `questions.read` + `questions.write`) can clone, publish, author bank items, and include from the bank; the author seed typically has **no** `assignments.*`. Live mode may be stored on create; invite/attempt for live is **not** executed (slice 6). Full employee route table: `docs/architecture.md` §6. Client details: `src/interview-quiz-web/README.md`. Shared UI primitives: [`docs/components/README.md`](docs/components/README.md).

### Exercise slices 2–5 locally (Development-only)

Dummy users and passwords are in the table under **Sign in locally** above — do not copy them elsewhere. Use:

- `author.dev@example.com` (Dev Template author) for **write**: list/create/edit quizzes (`quizzes.write`), publish as template (`templates.write`), clone (`quizzes.write` + `templates.read`), question bank (`questions.read` + `questions.write`), include from an existing quiz (`quizzes.write` + `questions.read`), save filters (`filters.write`). Typically **cannot** assign.
- `recruiter.dev@example.com` (Dev Recruiter) for **assignments** (`assignments.write`): list/create, copy invite (`POST /api/assignments/{id}/invite`), basic results (`attempts.read`). Also **read-only quizzes and templates**: list and open; quiz form disabled (`quizzes.read` without `quizzes.write`). Can save and **share** filters (`filters.write` + `filters.share`). Cannot clone. **No** `questions.*` — no bank nav, no include panel.

Candidates: open `{PublicBaseUrl}/attempt?token=` after the recruiter copies `inviteUrl`. **No** candidate password.

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

## Tests

```bash
dotnet test InterviewQuiz.slnx
```

Unit tests always run (JWT validation, `HasPermission`, login success/failure with fakes, Catalog question/quiz/template rules, Search filter rules, Delivery assignment rules, Evaluation auto-score). WebApplicationFactory tests **skip** unless `ConnectionStrings__InterviewQuiz` is set (no Testcontainers; Docker may be unavailable in CI agents). When the database is present, those tests authenticate with JWT (not `Authorization: Test`) and include Catalog quiz/template API coverage (`CatalogApiTests`), question-bank API coverage (`QuestionBankApiTests`), saved-filter API coverage (`SearchApiTests`), assignment API coverage (`AssignmentsApiTests`), magic-link consume (`MagicLinkApiTests`), and attempt API coverage (`AttemptsApiTests`). Specialists reported `dotnet test` **173 passed** with the database set, and `npm test` **74 passed** — not re-counted in this docs pass.

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
- `src/Modules/Delivery` — assignments + snapshots (`delivery` schema)
- `src/Modules/Evaluation` — async attempts + auto-score (`evaluation` schema)
- `src/interview-quiz-web` — Angular SPA + installable PWA (slices 1–5: openings, quizzes, templates, question bank, saved filters, assignments, candidate `/attempt`)
- `deploy/local/compose.yaml` — local PostgreSQL 16
