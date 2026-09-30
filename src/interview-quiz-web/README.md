# Interview Quiz web client

Angular SPA + installable PWA for the Interview Quiz Platform (slice 1: sign-in, openings, permission-aware admin; slice 2: quiz list and authoring editor; slice 3: templates, clone/publish, saved list filters). Question-bank screens are **slice 4** (not in this client).

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

## Routes (slice 2–3)

| Path | Permission | Screen |
|------|------------|--------|
| `/quizzes` | `quizzes.read` | Quiz list (opening, keyword, experience, saved filters) |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` or `quizzes.write` | View / edit quiz; `templates.write` can publish as template |
| `/templates` | `templates.read` | Template list (keyword, experience, tags, saved filters) |
| `/templates/:id` | `templates.read` | Template detail, versions, read-only questions |

Create quiz is hidden without `quizzes.write`. Recruiter (`templates.read`, no `quizzes.write`) can browse templates but not clone. Author (`quizzes.write` + `templates.read`) can clone a version into a quiz (opening picker needs `openings.read`, otherwise an opening id field). After clone the client navigates to `/quizzes/{id}`. Publish as template is hidden without `templates.write` and is not shown on create-new.

Saved filters live on the openings, quizzes, and templates list screens (`GET /api/filters`). Saving requires `filters.write`; sharing requires `filters.share` and ownership. Recruiter can share but typically lacks `users.manage`, so share-with is a user-id text field.

Exercise with the Development dummy users in the repository root README (passwords stay in that table only): `author.dev@example.com` has `templates.write` + `quizzes.write` (publish and clone) and `filters.write`; `recruiter.dev@example.com` has `templates.read` + `filters.share` and **no** `quizzes.write`, so no clone. Other employee routes (openings, field defaults, roles, users) are listed in `docs/architecture.md` §6. The Development seed template id is `5c9e3f32-8d4f-4e1c-9a33-2b0e4d8c3001`. Host Development auto-migrates `SearchDbContext` with Catalog; see the root README for `dotnet ef` commands.

## Build and tests

```bash
ng build
ng test --watch=false
```

Production build registers the Angular service worker. Dev `ng serve` does not (service worker `enabled: !isDevMode()`).

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
- **No** `dataGroups` for `/api`. Login, refresh, `/me`, tokens, and quiz payloads are not cached.
- No IndexedDB and no mutation outbox. Offline UX is a banner: “You are offline…”
- Reverse proxy must always revalidate `index.html` and `ngsw.json`; hashed assets may be long-cached.

## Token storage

| Secret | Where |
|--------|--------|
| Access token | In-memory signal only |
| Refresh token | `sessionStorage` key `iq.refreshToken` |
| Not used | `localStorage`, Cache Storage, IndexedDB, service worker |

On 401 the auth interceptor tries refresh **once**, then signs out. Logout clears memory and `sessionStorage` and calls `POST /api/auth/logout`.

**Residual XSS risk:** script running on this origin can read `sessionStorage` and in-memory tokens. Mitigations are CSP (Security/DevOps), short access-token lifetime, and never putting tokens in the service worker. This is documented, not eliminated.

Requests send `Authorization: Bearer {accessToken}` and `X-Correlation-ID` (generated UUID if the caller did not set one). HTTP failure logs in development include status + correlation id, not bodies.

## Permissions

UI hide/show uses `hasPermission(code)` and `*hasPermission="'quizzes.write'"` — never role names. Route `data.permission` is a permission code or an any-of list. Role editor (`roles.manage`) lists catalog checkboxes and excludes `candidate.attempt.participate` / `includeInEmployeeRoleEditor === false`.

Quiz authoring checks `quizzes.read` / `quizzes.write` only. Opening picker uses `GET /api/openings` when the user has `openings.read`; otherwise the editor falls back to a UUID field for `openingId`. Publish as template uses `templates.write`. Clone uses both `quizzes.write` and `templates.read` (any-of `*hasPermission` is not used for clone). Saved filters use `filters.write` / `filters.share`.

Mapper round-trips optional `sourceQuestionId` on save. `originTemplateId` and `sourceTemplateVersionId` are read-only on `QuizResponse` and are never sent on create/update.

## Layout

- `src/app/core` — auth, interceptors, permission helpers, typed API wrappers
- `src/app/features` — login, openings, quizzes, templates, roles, users
- `src/app/shared` — page status (loading / empty / error), saved filters
- `src/app/layout` — authenticated shell

List and editor screens keep criteria and form state in component signals. Session: `AuthService` signals. Permissions: `PermissionService`. No global entity store.
