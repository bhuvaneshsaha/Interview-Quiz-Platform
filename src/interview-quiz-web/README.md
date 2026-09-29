# Interview Quiz web client

Angular SPA + installable PWA for the Interview Quiz Platform (slice 1: sign-in, openings, permission-aware admin).

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

Open `http://localhost:4200`. Log in with a Development dummy user from the root README, for example `admin.dev@example.com` / `Dev.Admin!1`.

Run API and SPA together:

1. Start PostgreSQL and the host (`dotnet run --project src/InterviewQuiz.Host`).
2. In another terminal, `ng serve` from this folder.

Do not put JWT signing keys, connection strings, or dummy passwords into environment files that ship in the client bundle. Dummy users exist only in API Development seed.

## Build and tests

```bash
ng build
ng test --watch=false
```

Production build registers the Angular service worker. Dev `ng serve` does not (service worker `enabled: !isDevMode()`).

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

UI hide/show uses `hasPermission(code)` and `*hasPermission="'openings.write'"` — never role names. Route `data.permission` is a permission code. Role editor (`roles.manage`) lists catalog checkboxes and excludes `candidate.attempt.participate` / `includeInEmployeeRoleEditor === false`.

## Layout

- `src/app/core` — auth, interceptors, permission helpers, typed API wrappers
- `src/app/features` — login, openings, roles, users
- `src/app/shared` — page status (loading / empty / error)
- `src/app/layout` — authenticated shell
