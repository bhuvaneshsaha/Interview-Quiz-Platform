# ADR 0008 — Candidate magic-link: opaque invite, reusable until first submit

Status: **Implemented**. Decision unchanged. Slice 5 is in the code: hashed `access.magic_link_invites`, `POST /api/auth/magic-link/consume`, `IMagicLinkService.IssueAsync` + `BuildInviteUrl`, assignment-scoped candidate JWT (`candidate.attempt.participate` + `assignment_id`), no candidate refresh. Env names: `PublicBaseUrl`, `Jwt__CandidateAccessTokenMinutes`. Angular `/attempt?token=` uses in-memory `CandidateSession` isolated from employee `TokenStore`.

## Context

Slice 5 delivers async assignments. Candidates enter with a **magic-link**, not employee password login. ADR 0006 already records JWT bearer, Identity as the user store, assignment-scoped candidate sessions, and permission-based authZ. It left protocol details to Auth.

Two non-obvious choices block Auth/.NET/Angular if left open:

1. **What sits in the URL** — a session JWT, or an invite that is exchanged for a JWT?
2. **How many times the link works** — one-time on first GET/open, one-time on first consume, or reusable until the attempt is submitted?

Email scanners and Safe-Links-style prefetch often **GET** a URL once. A timed async quiz also needs browser refresh and a recruiter who lost the copied URL. Putting a live access token in the query string leaks it to proxies, referrer logs, and the service worker’s URL cache surface.

No SMTP provider is in v1. Development must still let a recruiter copy a URL. No commercial IdP or mail product.

## Decision

1. **The URL carries an opaque invite, not the session JWT.**  
   Recruiter issue (`POST /api/assignments/{id}/invite`, and the async create response) returns `inviteUrl` **once per issue**. The token in the query string is a high-entropy opaque value. Access stores only a **hash** (same idea as `access.refresh_tokens`). `POST /api/auth/magic-link/consume` exchanges it for a **JWT bearer** resource-scoped to that assignment (and `attempt_id` when an attempt exists). APIs authorize the JWT, never the invite string.

2. **The invite is reusable until first submit** (or explicit rotate).  
   Consume does **not** burn the invite. The candidate may consume again (new access token) until the bound assignment has a **submitted** attempt, or the recruiter issues a new invite (previous hashes stop working). Submitted, revoked, or live-mode assignments reject consume. This is **not** a one-time link.

3. **Consume is POST**, body `{ "token": "<opaque>" }`, `[AllowAnonymous]`. Do not consume on GET of the landing page (prefetch would mint sessions).

4. **Candidate JWT permission set is exactly** `candidate.attempt.participate`.  
   Claims include existing `sub` / email / `permission`, plus `assignment_id` (required) and `attempt_id` (when an attempt exists). **Never** union employee role permissions, even if the email matches an employee Identity user. Employee password login is a separate token. `candidate.attempt.participate` stays out of the employee role editor (`IncludeInEmployeeRoleEditor: false`).

5. **No candidate refresh token in slice 5.** Re-consume the still-valid invite. Lifetime: `Jwt__CandidateAccessTokenMinutes` (default 60, same 1–180 cap family as `Jwt__AccessTokenMinutes`). Quiz timing is enforced on the attempt (`dueAtUtc`), not by the JWT expiry.

6. **No SMTP in slice 5.** Issue responses return `inviteUrl` for the recruiter to copy. `PublicBaseUrl` builds the origin. No secrets in git. Rotate with a new `POST .../invite` if the URL leaked.

Landing path (Angular): `{PublicBaseUrl}/attempt?token=...` — see `docs/architecture.md` §16.3.

## Consequences

- Auth implemented hashed invites, consume, and candidate JWT claims. Delivery owns whether the assignment is still invitable (async, not submitted). Evaluation owns attempt identity for `attempt_id`.
- Security threat-models invite theft and prefetch; this ADR chooses POST consume + hashed storage + rotate-to-revoke so Architecture does not invent a mail vendor.
- Angular keeps the **candidate** access token apart from the employee session so a logged-in recruiter opening the invite cannot send `assignments.write` to candidate APIs, and the candidate token cannot call employee assignment write.
- Service worker: still **no** `/api` data groups (ADR 0004). Invite consume, assignment, and attempt URLs are never cached.
- One-time-on-open magic-links would be a new ADR (and a product decision). Slice 5 does not do that.
