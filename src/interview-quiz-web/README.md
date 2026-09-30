# Interview Quiz web client

Angular SPA + installable PWA for the Interview Quiz Platform (slice 1: sign-in, openings, permission-aware admin; slice 2: quiz list and authoring editor; slice 3: templates, clone/publish, saved list filters; slice 4: question bank list/editor and include-from-quiz; slice 5: assignments, invite copy, candidate `/attempt` magic-link).

## Prerequisites

- Node.js LTS (this workspace used Node 22)
- Angular CLI 21 (`npm i -g @angular/cli` or `$HOME/.npm-global/bin/ng`)
- The API running at `http://localhost:5147` (see the repository root README)

## Install

```bash
cd src/interview-quiz-web
npm install
```

This folder includes `.npmrc` with `legacy-peer-deps=true` so `vitest` peer resolution succeeds on npm 10.

## Serve (dev proxy)

`ng serve` proxies `/api` and `/health` to the host so the SPA is same-origin:

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
ng serve
```

Open `http://localhost:4200`. Log in with a Development dummy user from the repository root README.

Run API and SPA together:

1. Start PostgreSQL and the host (`dotnet run --project src/InterviewQuiz.Host`).
2. In another terminal, `ng serve` from this folder.

Do not put JWT signing keys, connection strings, or dummy passwords into environment files that ship in the client bundle. Dummy users exist only in API Development seed.

## Routes (slices 2–5)

| Path | Permission | Screen |
|------|------------|--------|
| `/quizzes` | `quizzes.read` | Quiz list (opening, keyword, experience, saved filters) |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` or `quizzes.write` | View / edit quiz; `templates.write` can publish as template; include from bank when `quizzes.write` **and** `questions.read` on an existing saved quiz |
| `/templates` | `templates.read` | Template list (keyword, experience, tags, saved filters) |
| `/templates/:id` | `templates.read` | Template detail, versions, read-only questions |
| `/questions` | `questions.read` | Question bank list (keyword, type, experience, tags; archived-only toggle only with `questions.write`). **No** SavedFilters |
| `/questions/new` | `questions.write` | Create bank question |
| `/questions/:id` | `questions.read` or `questions.write` | View / edit bank question; archive/unarchive with write; read-only without write |
| `/assignments` | `assignments.read` | Assignment list (opening, keyword, pager). **No** SavedFilters |
| `/assignments/new` | `assignments.write` | Create assignment (opening, quiz for that opening, email, async/live, duration, attempt limit). Async 201 shows `inviteUrl` (`data-testid="invite-url"`). Live has no invite copy (slice 6) |
| `/assignments/:id` | `assignments.read` | Assignment detail, issue invite (`assignments.write`, async not submitted), results table if `attempts.read` |
| `/attempt?token=` | none (outside employee shell) | Candidate magic-link: POST consume, start/save/submit. Isolated candidate access token |

Create quiz is hidden without `quizzes.write`. Recruiter (`templates.read`, no `quizzes.write`, **no** `questions.*`) can browse templates but not clone, and does not see bank nav or include. Author (`quizzes.write` + `templates.read`) can clone a version into a quiz (opening picker needs `openings.read`, otherwise an opening id field). After clone the client navigates to `/quizzes/{id}`. Publish as template is hidden without `templates.write` and is not shown on create-new.

Question bank nav (shell + home **Question bank**) requires `questions.read`. Create is hidden without `questions.write`. Include from question bank appears on the quiz editor only when `quizzes.write` **and** `questions.read` **and** the quiz is already saved (hidden on create-new). Copies go through `POST /api/quizzes/{quizId}/include-questions`; quiz questions with `sourceQuestionId` show the hint “From question bank”. Archive/unarchive is hidden on create-new.

Assignments nav (shell + home **Assignments**) requires `assignments.read`. Create is hidden without `assignments.write`. Recruiter seed has assign + results; a typical template author has no `assignments.*` and does not see the nav. List query params are `openingId`, `keyword`, `page`, `pageSize` (not a `criteria` JSON blob). Live mode may be created but has no invite URL; candidate start is slice 6.

Candidate `/attempt` is **not** behind `authGuard`. Consume is `POST /api/auth/magic-link/consume` `{ token }` (never GET). The candidate access token is in-memory on `CandidateSession` only — never `TokenStore`, never `iq.refreshToken`, never the service worker. Employee logout is not required. After submit the candidate session is cleared.

Saved filters live on the openings, quizzes, and templates list screens (`GET /api/filters`). Saving requires `filters.write`; sharing requires `filters.share` and ownership. Recruiter can share but typically lacks `users.manage`, so share-with is a user-id text field. The bank list and assignment list do not use SavedFilters (`FilterTarget` has no `questions` or `assignments`).

Exercise with the Development dummy users in the repository root README (passwords stay in that table only): `author.dev@example.com` has `templates.write` + `quizzes.write` + `questions.read` + `questions.write` (publish, clone, bank, include) and `filters.write`, and **typically no** `assignments.*`; `recruiter.dev@example.com` has **`assignments.write`** + `assignments.read` + `attempts.read` + `templates.read` + `filters.share` and **no** `quizzes.write` / **no** `questions.*`, so can assign and copy invites, but no clone, no bank nav, no include. Candidates use magic-link only (`/attempt?token=`) — **no** candidate password. Other employee routes (openings, field defaults, roles, users) are listed in `docs/architecture.md` §6. The Development seed template id is `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`. Seed assignment id is `7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001` (`candidate.dev@example.com`). Seed bank question ids are in the repository root README. Host Development auto-migrates `DeliveryDbContext` and `EvaluationDbContext` with Catalog and Search; see the root README for `dotnet ef` commands.

## Build and tests

```bash
ng build
ng test --watch=false
```

Production build registers the Angular service worker. Dev `ng serve` does not (service worker `enabled: !isDevMode()`).

The client has component specs for quiz list/editor, templates, SavedFilters, question bank list/editor/include, assignment list/form, and candidate `/attempt`. Specialists reported `npm test` **74 passed** — not re-counted in this docs pass.

## Storybook

OSS Storybook 10 (`@storybook/angular-vite`, Angular 21 / zoneless / application builder) documents shared primitives. Feature screens are not stories. Playbook markdown in `docs/components/` remains the source of API tables. There is no Chromatic.

```bash
export PATH=$HOME/.npm-global/bin:$PATH
cd src/interview-quiz-web
npm run storybook
```

Opens **http://localhost:6006**. Stories:

- `Shared/PageStatus` — Loading, Empty, Error (correlation id), Content
- `Shared/OfflineBanner` — Online vs Offline (`OnlineStatus` mocked)
- `Shared/HasPermission` — granted vs denied (`PermissionService` mocked)
- `Shared/SavedFilters` — apply-only vs write/share (`FiltersApi` mocked)

Static build:

```bash
npm run build-storybook
```

Output is `storybook-static/`. Catalog pages: `docs/components/`.

## PWA (ADR 0004)

- Installable: `manifest.webmanifest` with `display: standalone`, theme color, icons.
- Service worker caches **hashed app shell** only (`ngsw-config.json` asset groups).
- **No** `dataGroups` for `/api`. Login, refresh, `/me`, tokens, quiz payloads, assignments, attempts, and magic-link URLs are not cached.
- No IndexedDB and no mutation outbox. Offline UX is a banner: “You are offline…”
- Reverse proxy must always revalidate `index.html` and `ngsw.json`; hashed assets may be long-cached.

## Token storage

| Secret | Where |
|--------|--------|
| Employee access token | In-memory `TokenStore` signal only |
| Employee refresh token | `sessionStorage` key `iq.refreshToken` |
| Candidate access token | In-memory `CandidateSession` only (no refresh; `/attempt` re-consumes the invite token) |
| Not used | `localStorage`, Cache Storage, IndexedDB, service worker |

On 401 the auth interceptor tries refresh **once**, then signs out. Logout clears memory and `sessionStorage` and calls `POST /api/auth/logout`.

**Residual XSS risk:** script running on this origin can read `sessionStorage` and in-memory tokens. Mitigations are CSP (Security/DevOps), short access-token lifetime, and never putting tokens in the service worker. This is documented, not eliminated.

Requests send `Authorization: Bearer {accessToken}` and `X-Correlation-ID` (generated UUID if the caller did not set one). HTTP failure logs in development include status + correlation id, not bodies.

## Permissions

UI hide/show uses `hasPermission(code)` and `*hasPermission="'quizzes.write'"` — never role names. Route `data.permission` is a permission code or an any-of list. Role editor (`roles.manage`) lists catalog checkboxes and excludes `candidate.attempt.participate` / `includeInEmployeeRoleEditor === false`.

Quiz authoring checks `quizzes.read` / `quizzes.write` only. Opening picker uses `GET /api/openings` when the user has `openings.read`; otherwise the editor falls back to a UUID field for `openingId`. Publish as template uses `templates.write`. Clone uses both `quizzes.write` and `templates.read` (any-of `*hasPermission` is not used for clone). Saved filters use `filters.write` / `filters.share`. Question bank uses `questions.read` / `questions.write` (`PermissionCodes.QuestionsRead` / `QuestionsWrite`). Include from question bank on the quiz editor requires both `quizzes.write` and `questions.read` on an existing saved quiz (hidden on create-new). Recruiter without `questions.*` does not see bank nav or include. Assignments use `assignments.read` / `assignments.write`; results use `attempts.read`. Candidate attempt APIs use `candidate.attempt.participate` on the isolated candidate JWT.

Mapper round-trips optional `sourceQuestionId` on save. Include sets it via the include API. `originTemplateId` and `sourceTemplateVersionId` are read-only on `QuizResponse` and are never sent on create/update.

## Layout

- `src/app/core` — auth, interceptors, permission helpers, typed API wrappers
- `src/app/features` — login, openings, quizzes, templates, questions, assignments, attempt, roles, users
- `src/app/shared` — page status (loading / empty / error), saved filters
- `src/app/layout` — authenticated employee shell (`/attempt` is outside this shell)

List and editor screens keep criteria and form state in component signals. Session: `AuthService` signals. Permissions: `PermissionService`. No global entity store.
