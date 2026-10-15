# Permission catalog

Status: Living catalog. Access seeds these codes; API and Angular check them. **Live:** slice 1 Access/Openings, slice 2 quiz authoring, slice 3 templates and saved filters, slice 4 question bank, slice 5 Delivery assignments (`assignments.read` / `assignments.write` on `/api/assignments`; Angular `/assignments`, `/assignments/new`, `/assignments/:id`) and Evaluation attempts (`attempts.read` on `/api/assignments/{id}/attempts` and `/api/attempts/{id}`; `candidate.attempt.participate` on candidate attempt routes + `POST /api/auth/magic-link/consume`; Angular `/attempt?token=`), slice 6 live sessions (`sessions.live.run` on start/pause/resume/end; progress also allows `attempts.read`; Angular `/assignments/:id/live`), Reporting v1 (`reports.read` on `/api/reports`; Angular `/reports` and `/reports/:reportKey`), human review (`attempts.review`), and AI authoring drafts (`ai.draft.use` on `/api/ai-drafts` and Angular `/ai-drafts/new`). Later: company AI rule sets (`ai.rules.manage`). **Operators compose roles** from these codes; employees do not invent codes. API and UI check **permissions only** — never role names (`Recruiter`, `Admin`, etc.).

Deny by default: unauthenticated → 401; authenticated without the code → 403.

`GET /api/me` and `GET /api/me/permissions` require an authenticated principal only (no extra catalog permission).

---

## Model

- **Permission** — code + display name + module. Seeded from this catalog.
- **Role** — named set of permissions, CRUD in-app (`roles.manage`).
- **Assignment** — users receive one or more roles; effective set is the union.
- **Resource rules** (in addition to a permission): e.g. candidate JWT may only access one assignment; opening handlers do not bypass `openings.write` via a role name.

Candidate access is **not** a row in the employee role editor. Magic-link JWTs are scoped to an assignment (Auth; ADR 0008). Do not add `isCandidate` checks.

---

## Access

| Code | Display name | Capability |
|------|----------------|------------|
| `users.manage` | Manage users | Create, disable, and assign roles to users |
| `roles.manage` | Manage permission groups | CRUD roles as permission bundles; read the permission catalog for the editor |
| `archive.restore.resumes` | Restore archived resumes | Restore resume files/metadata from archive (admin) |
| `archive.restore.attempts` | Restore archived attempts | Restore attempt/result records from archive (admin) |
| `archive.restore.catalog` | Restore archived quiz content | Restore quizzes/templates/**bank items**/rule sets from archive (admin) |

`GET /api/permissions` is gated by `roles.manage` (role editor). Split restore codes so operators can grant resume restore without quiz-content restore (Brief §16 / Master default).

---

## Openings

| Code | Display name | Capability |
|------|----------------|------------|
| `openings.read` | View openings | List and open openings (tags, fields, handlers) |
| `openings.write` | Create and edit openings | Core fields, dynamic values, extra keys where allowed, handlers |
| `openings.fields.manage` | Manage opening field defaults | Admin default dynamic field keys/tags for new openings |

Split from Brief “Manage admin defaults” so field defaults are not bundled with AI rules.

---

## Catalog

| Code | Display name | Capability |
|------|----------------|------------|
| `quizzes.read` | View quizzes | Open quizzes the user is allowed to see (still one opening per quiz) |
| `quizzes.write` | Create and edit quizzes | Manual authoring, structure, scoring/credit modes; **clone** a template version into an opening |
| `templates.read` | View templates | Search/list template library; required with `quizzes.write` to clone |
| `templates.write` | Create and edit templates | Publish quiz as template (new version on the quiz’s lineage — see architecture §15.1) |
| `questions.read` | View question bank | List and open company **bank items** (`GET /api/questions`; Angular `/questions`, `/questions/:id`) |
| `questions.write` | Create and edit question bank | Author bank items; archive/unarchive; list archived (`archived=true`); Angular `/questions/new` and editor write |
| `ai.rules.manage` | Manage company AI rule sets | Versioned JSON rule sets (global + per-question-type fields). Not the authoring-draft path. Drafts take optional rules text instead |
| `ai.draft.use` | Use AI draft | `POST /api/ai-drafts` and `GET /api/ai-drafts/{id}` (creator only). Generate a draft from an opening, optional pasted resume, optional rules text, and an optional template version. Accept (`POST /api/ai-drafts/{id}/accept`) also requires `quizzes.write` and a question edit, then creates a quiz. It does not assign |

A Dev author can hold `templates.write` / `quizzes.write` **without** `openings.write` or `ai.rules.manage`. The Development seed bundle also includes `openings.read` so the author can pick an opening in the quiz editor without needing `openings.write`.

`questions.read` / `questions.write` are **stable catalog codes** (module `catalog`). Access seeds the permission rows so the role editor can assign them (`GET /api/permissions`). Quiz/template endpoints do **not** check `questions.*` except include-into-quiz, which requires `quizzes.write` plus `questions.read` (API and the quiz editor include panel). Unarchive is library undelete, **not** `archive.restore.catalog`. Do not authorize by role name — the Dev Template author Development seed bundle includes `questions.read` and `questions.write`. Recruiter seed has **no** `questions.*`. The Angular `permission-codes.ts` helper lists `QuestionsRead` / `QuestionsWrite`. Shell and Home nav **Question bank** behind `questions.read`. Bank list has no SavedFilters (`FilterTarget` has no `questions`).

These codes are **not** the drag-drop question type `dragDropSharedBank` (items inside one question).

---

## Delivery

| Code | Display name | Capability |
|------|----------------|------------|
| `assignments.read` | View assignments | List/get assignment status and configuration (`GET /api/assignments`, `GET /api/assignments/{id}`). Not a substitute for review. Angular `/assignments`, `/assignments/:id`. **No** SavedFilters on the list. |
| `assignments.write` | Assign quizzes | `POST /api/assignments`; `POST /api/assignments/from-template` (also `templates.read`); `POST /api/assignments/{id}/invite`, `.../resend`, `.../revoke`, `.../extend`. Bind snapshot to candidate + opening; live or async; timing/attempt rules. Angular `/assignments/new`. |
| `sessions.live.run` | Run live sessions | Start, pause, resume, and end `POST /api/assignments/{id}/live/*`. Dev Recruiter seed includes it. Progress `GET .../live/progress` also allows `attempts.read`. |

Recruiters typically get assign + live run without `ai.rules.manage` or `quizzes.write`. **Dev Recruiter seed already includes** `assignments.read` / `assignments.write` / `sessions.live.run`. **Dev Template author seed does not** — authors cannot assign unless an operator grants the codes. Candidate JWTs never include `assignments.write`.

Slice 5 does **not** add saved-filter `target: assignments`.

---

## Evaluation

| Code | Display name | Capability |
|------|----------------|------------|
| `attempts.read` | View attempts and results | `GET /api/assignments/{id}/attempts`, `GET /api/attempts/{id}` — scores/outcomes. **Not** review. Angular assignment detail results panel. Dev Recruiter seed has this. |
| `reports.read` | View reports | Reporting v1. Company-wide `GET /api/reports/*` and CSV export. Angular `/reports` and `/reports/:reportKey`. Dev Recruiter seed includes it. Super Admin receives it from `PermissionCodes.All`. Dev Reviewer does **not**. |
| `attempts.review` | Review attempts | Score `humanOnly` and `aiAssist` items and finalise a result. `GET /api/attempts/{id}/review`, `POST /api/attempts/{id}/items/{itemId}/review`, and `POST /api/attempts/{id}/items/{itemId}/suggestion`. Angular `/assignments/:assignmentId/attempts/:attemptId/review`. Dev Reviewer seed includes it. `attempts.review` does not grant `reports.read`. Answer keys and AI drafts are on this payload only. |

Generating authoring drafts is `ai.draft.use`, not this code. A review suggestion uses `attempts.review`. Auto-score does not use `attempts.review`.

### Seeded permissions

`sessions.live.run` is active for live host actions ([ADR 0010](adr/0010-live-quiz-sessions.md)). `attempts.review` is active for human scoring and for AI suggestion drafts on `aiAssist` items.

| Code | Seeded on | Status |
|------|-----------|--------|
| `sessions.live.run` | Dev Recruiter | Active. Start, pause, resume, end. |
| `attempts.review` | Dev Reviewer | Active. Human scores for `humanOnly` and `aiAssist` items, including the answer key and AI suggestion drafts. |

See [slice 5 MVP boundary](slice-5-mvp.md) for what slice 5 itself did not run.

---

## Search (saved filters)

| Code | Display name | Capability |
|------|----------------|------------|
| `filters.write` | Manage own saved filters | Create/edit/delete personal saved filters |
| `filters.share` | Share saved filters | Share public-inside-company or with specific people |

Applying unsaved criteria on `GET /api/openings`, `GET /api/quizzes`, or `GET /api/templates` needs only the matching `*.read` permission (quiz get-by-id still allows `quizzes.write`). Saved-filter CRUD is **live** on `/api/filters` (architecture §15.5). Angular list screens host `app-saved-filters` (playbook: `docs/components/angular/SavedFilters.md`). **Slice 5 does not add** `target: assignments` (architecture §16.10). Reporting v1 adds one target per report key: `reportsFunnel`, `reportsScores`, `reportsTime`, `reportsQuestions`, `reportsCandidates`, `reportsPendingReview`. Saving a field that report does not allow returns 400. There is still no `assignments` target.

---

## Candidate session (not a composable employee role)

Auth issues a JWT bound to an assignment (and attempt when created). The API authorizes with **resource scope** (`assignment_id` claim) plus this catalog code. Paths: `POST /api/auth/magic-link/consume`; candidate `POST /api/assignments/{id}/attempts`, `GET /api/assignments/{id}/attempts/current`, `PUT .../current/answers`, `POST .../current/submit` (architecture §16.3–16.4, ADR 0008). Angular landing: `/attempt?token=` **outside** the employee shell (`CandidateSession` in-memory; not employee `TokenStore`). No candidate password.

| Code | Display name | Notes |
|------|----------------|--------|
| `candidate.attempt.participate` | Take and submit own attempt | Only on the bound assignment. Not shown as a checkable box on the employee role editor (or shown read-only / excluded from seed roles). **Never** present on employee login JWTs. |

Do not implement as `[Authorize(Roles = "Candidate")]`.

---

## Super Admin (system role)

`Super Admin` is a system role, not a sample bundle. Stable id `f8c1a0b2-6d4e-4a11-9c3e-1b7a5d0e0001`, `isSystem=true`. The Access migration inserts it, and startup re-syncs its permission rows in every environment. It is granted every catalog code except `candidate.attempt.participate`. Token issue computes that set from `PermissionCodes.All`, so a new code is included even when the stored rows are stale. Startup writes the same set onto `access.role_permissions` for codes that exist in `access.permissions`.

The API still authorizes with permission codes. The extra rule for assigning or revoking this role checks the stable role id, never the display name. That call also requires `roles.manage`, and the actor must already hold the role. Each assign and revoke writes `access.audit_events` (actor, action, target, time, correlation id).

The role cannot be renamed, deleted, or reduced. The API returns 409 if the last active holder (not disabled) is unassigned or deactivated. The role editor shows it read-only with a warning badge.

### Break-glass

Keep the number of holders minimal. The first holder is not a password in git.

1. Set `Bootstrap__SuperAdminEmail` to a company mailbox (environment or protected config). Leave it empty to seed the role without a user.
2. Start the API. Startup creates the user with no password and logs a one-time setup token once at warning. Treat that log line as a secret. The token is stored only as a SHA-256 hash and expires after 72 hours.
3. `POST /api/auth/bootstrap/set-password` with `{ email, token, newPassword }`. The token is single use. Sign-in before that call returns 409.
4. `Bootstrap__ResetSuperAdminSetup=true` re-issues a token on the next start when the user still must set a password. Turn it off after the new token is used.
5. A second holder is assigned in the role editor by someone who already holds Super Admin. If every holder is locked out, set the bootstrap email to an existing account that already has a password (it is assigned the role and is not forced to reset) or to a new mailbox and repeat the token flow.

---

## Suggested Development seed bundles (not product roles)

Names below are **sample Identity role rows** for local seed only. Production operators create their own bundles. Code and UI must not branch on these names.

| Seed name | Permission codes (union) |
|-----------|---------------------------|
| Dev Recruiter | `openings.read`, `openings.write`, `quizzes.read`, `templates.read`, `assignments.read`, `assignments.write`, `sessions.live.run`, `attempts.read`, `reports.read`, `filters.write`, `filters.share` |
| Dev Template author | `openings.read`, `quizzes.read`, `quizzes.write`, `templates.read`, `templates.write`, `questions.read`, `questions.write`, `ai.draft.use`, `filters.write` |
| Dev Reviewer | `openings.read`, `assignments.read`, `attempts.read`, `attempts.review` (no `reports.read`) |
| Dev Admin | `users.manage`, `roles.manage`, `openings.fields.manage`, `ai.rules.manage`, `archive.restore.resumes`, `archive.restore.attempts`, `archive.restore.catalog`, plus read of openings/catalog as needed to administer |

Never seed production with extra privilege by accident.

---

## Mapping from Brief §5

| Brief capability | Codes |
|------------------|--------|
| Manage admin defaults (fields/tags + AI rules) | `openings.fields.manage` + `ai.rules.manage` |
| Manage users & permission groups | `users.manage` + `roles.manage` |
| Create / edit openings | `openings.write` (with `openings.read`) |
| Create / edit quizzes | `quizzes.write` |
| Create / edit templates | `templates.write` |
| Create / edit question bank | `questions.write` (with `questions.read`) |
| Use AI draft | `ai.draft.use` |
| Assign quizzes | `assignments.write` |
| Run live sessions | `sessions.live.run` |
| Review attempts | `attempts.review` |
| View reports | `reports.read` |
| Manage saved filters | `filters.write` + `filters.share` |
| Restore from archive (Master default) | `archive.restore.resumes` / `.attempts` / `.catalog` |

Auth implements policies, claims, and `/me/permissions`. Angular uses the same codes in guards and `*hasPermission`.
