# ADR 0004 — Installable PWA, online-first (no offline mutations)

## Context

The user asked for Angular **with PWA**. The product includes timed live and async attempts, scoring, candidate magic-link auth, and resume PII. The PWA skill defaults to online-first unless Architecture says otherwise. Ionic/Capacitor is **not** in scope (native shell was not requested).

Offline mutation of attempts would mean caching quiz payloads and queuing answers — which conflicts with timing integrity, anti-cheat expectations of a live/async interview, and residual risk of storing answers/PII in IndexedDB.

## Decision

**Installable PWA, online-first.** Architecture **agrees** with the Master recommendation.

| In v1 | Out of v1 |
|-------|-----------|
| Web app manifest; `display: standalone` | Ionic / Capacitor |
| `@angular/service-worker` (`ng add @angular/pwa`) | Workbox unless Angular SW cannot express the plan (not expected) |
| Cache **hashed app shell** only | Cache authenticated APIs |
| Offline UX: installed shell + **“you are offline”** for quiz and authoring | IndexedDB outbox for attempts or authoring |
| | Cache tokens, `/me`, login/refresh, quiz payloads, answers |
| | Sync/delta attempt APIs |

Timed live/async, scoring, and magic-link **must not** work as offline mutations in v1.

DevOps: reverse proxy cache headers — **always revalidate** `index.html` and `ngsw.json`; long-cache hashed assets.

Auth + Security: logout must not leave a path to reuse cached user data; SW must not store tokens.

## Consequences

- Angular owns `ngsw-config.json` data groups: asset groups for hashed output; **no** data groups for `/api`.
- .NET does **not** build PWA sync/outbox endpoints in v1.
- Candidates and authors need network for real work; install still helps “add to home screen” on internal devices.
- If a later slice needs true offline (e.g. authoring on a plane), that is a new ADR and a Security review before IndexedDB holds PII.
- Residual risk: XSS plus a cached shell is still a concern; not storing tokens or quiz JSON in Cache Storage keeps the blast radius to static assets.
