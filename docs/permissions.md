# Permission catalog

Status: Living catalog. Access seeds these codes; API and Angular check them. Slice 2 quiz authoring is live (`quizzes.read` / `quizzes.write` on `/api/quizzes` and `/quizzes` routes). Codes for later slices (templates, **question bank**, AI, assignments, attempts, filters) are catalogued; slice 3 exposes templates + filters and **seeds** `questions.read` / `questions.write` without bank REST. **Operators compose roles** from these codes; employees do not invent codes. API and UI check **permissions only** — never role names (`Recruiter`, `Admin`, etc.).

Deny by default: unauthenticated → 401; authenticated without the code → 403.

`GET /api/me` and `GET /api/me/permissions` require an authenticated principal only (no extra catalog permission).

---

## Model

- **Permission** — code + display name + module. Seeded from this catalog.
- **Role** — named set of permissions, CRUD in-app (`roles.manage`).
- **Assignment** — users receive one or more roles; effective set is the union.
- **Resource rules** (in addition to a permission): e.g. candidate JWT may only access one assignment; opening handlers do not bypass `openings.write` via a role name.

Candidate access is **not** a row in the employee role editor. Magic-link JWTs are scoped to an assignment (Auth). Do not add `isCandidate` checks.

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
| `questions.read` | View question bank | List and open company **bank items** (slice 4). Seed the code in slice 3; no bank API yet |
| `questions.write` | Create and edit question bank | Author bank items (slice 4). Seed the code in slice 3; **do not** grant on Dev Template author until the bank ships |
| `ai.rules.manage` | Manage company AI rule sets | Versioned JSON rule sets (global + per-question-type fields) |
| `ai.draft.use` | Use AI draft | Generate a draft from resume + rules; human must edit before assign |

A Dev author can hold `templates.write` / `quizzes.write` **without** `openings.write` or `ai.rules.manage`. The Development seed bundle also includes `openings.read` so the author can pick an opening in the quiz editor without needing `openings.write`.

`questions.read` / `questions.write` are **stable catalog codes** (module `catalog`), same seeding path as `templates.*`. Slice 3 Access must insert the permission rows so the role editor can assign them; quiz/template endpoints do **not** check `questions.*`. Slice 4 include-into-quiz will require `quizzes.write` plus `questions.read`. Do not authorize by role name when the bank ships — add the codes to the Dev Template author **seed bundle** only.

These codes are **not** the drag-drop question type `dragDropSharedBank` (items inside one question).

---

## Delivery

| Code | Display name | Capability |
|------|----------------|------------|
| `assignments.read` | View assignments | See assignment status and configuration (not a substitute for review) |
| `assignments.write` | Assign quizzes | Bind snapshot to candidate + opening; live or async; timing/attempt rules |
| `sessions.live.run` | Run live sessions | Start, pause, and monitor a live attempt |

Recruiters typically get assign + live run without `ai.rules.manage` or `quizzes.write`.

---

## Evaluation

| Code | Display name | Capability |
|------|----------------|------------|
| `attempts.read` | View attempts and results | Scores/outcomes for permitted openings |
| `attempts.review` | Review attempts | Score written items (human or confirm AI-assist); finalise result |

AI-assist **suggestion** in slice 6 still requires `attempts.review` to confirm. Generating authoring drafts is `ai.draft.use`, not this code.

---

## Search (saved filters)

| Code | Display name | Capability |
|------|----------------|------------|
| `filters.write` | Manage own saved filters | Create/edit/delete personal saved filters |
| `filters.share` | Share saved filters | Share public-inside-company or with specific people |

Applying unsaved criteria on `GET /api/openings`, `GET /api/quizzes`, or `GET /api/templates` needs only the matching `*.read` permission (quiz get-by-id still allows `quizzes.write`). Saved-filter CRUD is §4.6 / architecture §15.5.

---

## Candidate session (not a composable employee role)

Auth issues a JWT bound to an assignment (and attempt). The API authorizes with **resource scope** plus a narrow session capability, for example:

| Code | Display name | Notes |
|------|----------------|--------|
| `candidate.attempt.participate` | Take and submit own attempt | Only on the bound assignment. Not shown as a checkable box on the employee role editor (or shown read-only / excluded from seed roles). |

Do not implement as `[Authorize(Roles = "Candidate")]`.

---

## Suggested Development seed bundles (not product roles)

Names below are **sample Identity role rows** for local seed only. Production operators create their own bundles. Code and UI must not branch on these names.

| Seed name | Permission codes (union) |
|-----------|---------------------------|
| Dev Recruiter | `openings.read`, `openings.write`, `quizzes.read`, `templates.read`, `assignments.read`, `assignments.write`, `sessions.live.run`, `attempts.read`, `filters.write`, `filters.share` |
| Dev Template author | `openings.read`, `quizzes.read`, `quizzes.write`, `templates.read`, `templates.write`, `ai.draft.use`, `filters.write` — **add** `questions.read` and `questions.write` when slice 4 (question bank) ships, not in the slice 3 seed |
| Dev Reviewer | `openings.read`, `assignments.read`, `attempts.read`, `attempts.review` |
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
| Manage saved filters | `filters.write` + `filters.share` |
| Restore from archive (Master default) | `archive.restore.resumes` / `.attempts` / `.catalog` |

Auth implements policies, claims, and `/me/permissions`. Angular uses the same codes in guards and `*hasPermission`.
