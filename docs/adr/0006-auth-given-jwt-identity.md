# ADR 0006 — AuthN given: JWT, Identity store, magic-link, Entra later

## Context

Architecture does **not** choose Cookie vs JWT vs OIDC. The product owner already locked authentication for this system. Open questions remain about branded entry routing and protocol details; those belong to Auth, not a hosting or module-boundary decision.

Authorization is permission-based (capability bundles). Role **names** are never checked in API or UI.

## Decision

**Record as given** (Auth implements; this ADR does not design token format, storage, or magic-link message shape):

1. **API authN mechanism:** JWT **bearer**. Not cookie auth as the API mechanism.
2. **User store (first):** users in the **app database** via **ASP.NET Core Identity** (store only). Identity is not a second product database.
3. **Candidates:** **magic-link**. Candidate sessions are **assignment-scoped** (resource-based), not an employee role name such as “Candidate”.
4. **Employees later:** **Entra ID** as Identity **external / third-party** login using Microsoft Identity libraries. After Entra login, the **same app still issues JWTs**. No paid IdP.
5. **AuthZ:** permission catalog + operator-composed roles. Policies such as `HasPermission("openings.write")`. Never `[Authorize(Roles = ...)]`. Claims carry **permission codes** (refresh on role change — Auth’s issue).
6. Branded entry that routes employee vs candidate is **Auth + product** (`requirements/Open questions.md`). Architecture only requires both populations to end as JWTs understood by the same API.

Permission codes: `docs/permissions.md`.

## Consequences

- Auth specialist uses the JWT skill + permission-based access + Microsoft Identity **authorization** (policies). Microsoft Identity **authentication** applies when Entra is wired, not in slice 1 by default.
- .NET modules assume `[Authorize]` + permission policy placeholders until Auth wires them. Angular does not check role names and does not invent a parallel authZ model.
- PWA/service worker must not cache login, refresh, tokens, or `/me` (ADR 0004).
- Security threat-models magic-link theft, JWT storage, and Entra callback — not this ADR.
- Adding Entra must not introduce a cloud hosting dependency (ADR 0001); it is an outbound IdP call from the on-prem host.
