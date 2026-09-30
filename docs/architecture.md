# Interview Quiz Platform — Architecture

Status: v1 architecture record — slices **1–4 implemented**; **slice 5** (assignments / delivery) is next.  
Platform skill: `enterprise-architecture-onprem.md`  
Client shape: **Angular SPA + installable PWA, online-first** (no Ionic / Capacitor)  
AuthN (given, not chosen here): **JWT bearer**; ASP.NET Core Identity as the user store; candidate magic-link; Entra ID later as external login. Auth owns issuance, storage, and protocol.  
AuthZ: permission-based capability catalog (see `docs/permissions.md`). Never `[Authorize(Roles = ...)]`.

---

## 1. Goals and constraints

Internal openings-first interview quiz platform. Replaces Microsoft Forms packs. Sits beside hiring process; **not** an ATS, **not** a public career portal.

**Users:** recruiters, specialist template authors (Dev / QA / HR / Finance), admins, candidates.

**v1 goals:** searchable quizzes tied to openings; specialist-owned templates; a Catalog-owned **question bank** (reusable items, copy-on-include); one quiz model for live and async; AI as draft-only (last slice); tags and saved filters instead of org hierarchy.

**Locked hosting:** on-premises VMs, reverse proxy, self-hosted data. No Azure or AWS services as defaults. No commercial libraries, paid IdPs, paid APM, or paid sync.

**Stack:** ASP.NET Core Modular Monolith, EF Core, PostgreSQL, Angular. REST. Simple DDD. One company (not multi-tenant SaaS).

---

## 2. Assumptions (explicit)

Master defaults are **accepted**; Architecture does not override them:

| Topic | Assumption |
|--------|------------|
| Template edits | New version; existing assignments keep the old version. Working copy is always a **quiz**. Slice 3 has no in-place template editor — clone → edit quiz → publish creates the next version. |
| Assignment | Stores a snapshot of questions, keys, and scoring rules. |
| Quiz reuse | A quiz belongs to **one** opening. Reuse across openings is via **template**. This overrides Brief §6.2 “linked to one or more openings” in favor of Brief §16. |
| Question bank | Catalog-owned **item** library. Same types/scoring/credit as quiz questions. **Copy-on-include** (new quiz question id; `sourceQuestionId` provenance). Not a new bounded context. Distinct from templates (whole-quiz reuse) and from the drag-drop `dragDropSharedBank` question type. ADR 0007. Bank CRUD + Angular UI is **slice 4** (implemented); groundwork is in slice 3. |
| Template lineage | First publish of a quiz with no origin creates a template. Later publish from **that quiz** or from a quiz **cloned from that template** creates a **new version** of the same lineage. No always-new-template. No fork-to-new-lineage in slice 3. |
| Ordering partial credit | Default formula: **adjacent-pair**. Exact implementation is Evaluation when scoring lands (slice 5 auto-score / slice 7 review). |
| Retention | Configurable archive (default 5 years). **No hard delete.** Separate restore rules for resumes, attempts, quiz content. Restore is an admin permission (split codes below). |
| Company AI rules | Versioned JSON: global layer + required fields per question type. Concrete schema deferred to the AI slice. |
| Async attempts | One attempt unless the assignment override allows another. |
| Tenancy | Single internal company. |
| AI runtime | Last slice (now **slice 8**). Operator-configured **OpenAI-compatible HTTP** endpoint (self-hosted or otherwise). Not Azure OpenAI. First slices have **no LLM**. |
| AuthN | JWT + Identity store + magic-link + Entra-later is a **given**. Architecture does not design Cookie vs JWT. |
| CQRS | **No.** Application services in each module. Never MediatR. See ADR 0005. |
| Data | One PostgreSQL database, module-owned schemas, until a split is justified. See ADR 0003. |
| Files | Resumes and other binaries on local / NAS disk; paths in PostgreSQL. Not a cloud object store. |
| Live progress | REST start / pause / poll in v1. SignalR is optional later (OSS only); not required. |
| Network | SPA and API same-site behind one reverse proxy (simplifies cookies if Auth ever needs them; JWT still works). Split origins need an explicit CORS allowlist — not the default. |
| CI | Self-hosted runners. Exact product (GitHub Actions, GitLab, Jenkins, Azure DevOps Server) is **unknown** — DevOps confirms what ops already run. |

Open questions that remain product/Auth-owned are listed in §11.

---

## 3. Context diagram

```mermaid
flowchart LR
  subgraph people [People]
    Recruiter
    Author[Template author]
    Admin
    Candidate
  end

  subgraph edge [On-prem edge]
    RP[Reverse proxy<br/>TLS termination]
  end

  subgraph host [App VM]
    SPA[Angular SPA / PWA<br/>hashed assets + index.html]
    API[ASP.NET Core<br/>Kestrel Modular Monolith]
  end

  subgraph data [On-prem data]
    PG[(PostgreSQL)]
    Files[File store<br/>local or NAS]
  end

  subgraph obs [Observability]
    OTel[OTel Collector]
    Logs[Log files / OSS stack]
  end

  subgraph later [Later — not first slices]
    Entra[Entra ID<br/>employee external login]
    LLM[OpenAI-compatible HTTP]
  end

  Recruiter --> RP
  Author --> RP
  Admin --> RP
  Candidate --> RP
  RP --> SPA
  RP --> API
  SPA -->|same origin /api| API
  API --> PG
  API --> Files
  API --> OTel
  API --> Logs
  SPA -.->|errors + correlation id<br/>no PII / tokens| API
  API -.->|slice 8| LLM
  API -.->|employees later| Entra
```

Request path: browser → reverse proxy (TLS) → Kestrel. The proxy serves the Angular app and forwards `/api` and `/health` to the monolith. PostgreSQL and the file store are internal. The OTel Collector is the only telemetry backend named here.

---

## 4. Bounded contexts (module map)

One ASP.NET Core host. In-process modules with **public application contracts**; other modules do not read another module’s tables. Shared kernel stays small: identifiers, pagination, tag key–value value object, permission code constants, clock. **No** domain entities in the kernel. Aggregates only where invariants need them (quiz graph, assignment + snapshot, attempt scoring completeness, role as a permission set).

```mermaid
flowchart TB
  subgraph host [Modular Monolith]
    Access
    Openings
    Catalog
    Delivery
    Evaluation
    Search
    Kernel[Shared kernel — small]
  end

  Access --> Kernel
  Openings --> Kernel
  Catalog --> Kernel
  Delivery --> Kernel
  Evaluation --> Kernel
  Search --> Kernel

  Catalog -->|Opening must exist| Openings
  Delivery -->|snapshot via public API| Catalog
  Delivery -->|opening + candidate binding| Openings
  Evaluation -->|assignment + snapshot| Delivery
  Search -->|stores filter definitions only| Openings
  Search --> Catalog
```

### 4.1 Access

**Language:** user, permission, role (operator-composed bundle), JWT, magic-link, later Entra link.

**Owns:** employee and candidate user records (ASP.NET Core Identity as store), permission catalog seed, roles as permission sets, role assignment, JWT issue / revoke (Auth implements), candidate magic-link issuance/consumption (Auth implements), later Entra as Identity external login. After Entra login the **same app still issues JWTs**.

**Does not own:** Cookie vs JWT choice (already given). Opening/quiz business rules.

**Public contracts (REST, Auth/.NET fill details):**

- `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` (revoke)
- Candidate magic-link start/consume (paths owned by Auth)
- `GET /api/me`, `GET /api/me/permissions`
- `GET /api/permissions` — catalog for the role editor (`roles.manage`)
- `GET|POST|PUT|DELETE /api/roles`, assign/unassign users
- `GET|POST|PUT /api/users` (`users.manage`)
- Later: Entra challenge / callback, then app JWT

**In-process:** resolve current user; effective permission set; assignment-scoped candidate principal (resource check, not a role-name check).

**Permissions defined here:** `users.manage`, `roles.manage`, archive restore codes (admin).

### 4.2 Openings

**Language:** opening, handler, dynamic field definition, tag (key–value).

**Owns:** openings (title, JD, owner, dates, headcount, experience, handlers), admin default field keys, per-opening values, extra keys where permitted. No rigid org tree.

**Does not own:** quizzes, assignments, filters UI storage.

**Public contracts:**

- `GET|POST|PUT /api/openings`
- `GET /api/openings/{id}`
- `GET|PUT /api/opening-field-definitions` (`openings.fields.manage`)

**In-process:** `GetOpening(id)` for Catalog (quiz must reference one opening) and Delivery.

**Permissions:** `openings.read`, `openings.write`, `openings.fields.manage`.

### 4.3 Catalog

**Language:** quiz, template, template version, template lineage, question, question bank (item library), bank item, `sourceQuestionId` (provenance), question type, scoring mode (auto / AI-assist / human-only), credit mode (partial / all-or-nothing), company AI rule set.

**Owns:** quizzes (working copy under **one** opening), templates and versions, **question bank items** (slice 4 table; same `Question` shape as quiz questions), question types listed in Brief §7 (no code questions in v1), per-question scoring and credit modes, company AI rule sets as versioned JSON. AI **draft jobs** in slice 8 (resume + opening + optional template + rules → draft quiz; human edit gate before assign).

**Does not own:** assignment lifecycle, attempts, live session control, saved-filter storage (Search).

**Public contracts:**

Slice 2 **implemented** (REST, camelCase JSON): `GET|POST /api/quizzes`, `GET|PUT /api/quizzes/{id}`. Permissions: list `quizzes.read`; create/update `quizzes.write`; get by id `quizzes.read` **or** `quizzes.write`. Question `type` / `scoringMode` / `creditMode` are strings (`multipleChoiceSingle`, `auto`, `partial`, …). `creditMode` is required only for `multipleChoiceMulti` and `ordering`. A quiz belongs to **exactly one** opening. Code question types are rejected.

Slice 3 **implemented** (paths, DTOs, permissions in §15): `POST /api/quizzes/{id}/publish-template`; `GET /api/templates`, `GET /api/templates/{id}`, versions list/get, clone; quiz list criteria expanded so Search-stored filters execute here (`openingId`, `keyword`, experience range, `tags`, `criteria` JSON — §15.3).

Slice 4 **implemented** (REST, camelCase JSON): table `catalog.bank_questions`; `GET|POST /api/questions`, `GET|PUT /api/questions/{id}`, archive/unarchive, `POST /api/quizzes/{quizId}/include-questions`; registered `IQuestionBankReader`; Dev Template author seed includes `questions.read` / `questions.write`. Angular: `/questions`, `/questions/new`, `/questions/:id`; quiz editor include panel.

Later (not this pass):

- `GET|POST|PUT /api/ai-rule-sets` (`ai.rules.manage`) — schema in AI slice
- Slice 8: `POST /api/ai-drafts` (`ai.draft.use`); draft is never auto-assigned

**In-process:**

- `IQuizSnapshotReader.GetSnapshotAsync(quizId)` → immutable DTO of questions, keys, scoring rules for Delivery to persist on assign (wired; Delivery does not call it yet). Snapshot questions include `sourceQuestionId`.
- `IOpeningLookup.GetOpeningAsync(id)` — already used; clone and quiz write still require the opening to exist.
- Slice 3: template version read for clone (Catalog-internal; other modules still must not read `catalog` tables).
- `IQuestionBankReader.GetByIdAsync(id)` — registered; **include only** (not quiz create/update/publish/clone). GetById may return archived items so include can reject them.

**Permissions:** `quizzes.read`, `quizzes.write`, `templates.read`, `templates.write`, `questions.read`, `questions.write` (granted on Dev Template author Development seed), `ai.rules.manage`, `ai.draft.use`.

**Versioning:** template publish → new version. Quizzes are mutable working copies; freeze is the **assignment snapshot**, not a full quiz history in v1. Bank item edits (slice 4) mutate the library row only; copies already in quizzes/templates/snapshots stay as they were.

**Three reuse mechanisms (do not conflate):**

| Mechanism | What is reused | How |
|-----------|----------------|-----|
| Template | Whole quiz (metadata + questions) | Publish / clone; new question ids; lineage via `originTemplateId` |
| Question bank | One **item** (type, stem, scoring, credit, body) | Copy-on-include; new question id; `sourceQuestionId` → bank item |
| `dragDropSharedBank` | Tokens **inside one question** | Question-type body; not a company library |

### 4.4 Delivery

**Language:** assignment, candidate contact, mode (live | async), timing rules, attempt policy, snapshot, live session.

**Owns:** binding of a quiz snapshot to a candidate under an opening; live vs async as a **property of the assignment**; overall and per-section timing; attempt limit (default one async attempt); live start / pause / monitor; magic-link target association (token protocol is Auth). Snapshot rows/JSON at assign time.

**Does not own:** scoring, review workflow, template library, question bank.

**Public contracts:**

- `GET|POST /api/assignments`, `GET /api/assignments/{id}`
- `POST /api/assignments/{id}/live/start|pause`
- `GET /api/assignments/{id}/live/progress`
- Candidate-facing assignment/attempt session (Auth-scoped JWT) — exact paths Auth + .NET

**In-process:** `GetAssignmentWithSnapshot(id)` for Evaluation.

**Permissions:** `assignments.read`, `assignments.write`, `sessions.live.run`.

### 4.5 Evaluation

**Language:** attempt, answer, auto-score, AI-assist draft, human review, final result.

**Owns:** attempts (answers, timestamps, duration); immediate auto-score; written items until every non-auto item is settled; AI-assist as **auditable drafts** (suggestion + human confirmation or edit + actor + timestamps); final score/outcome. Archive/restore of attempts metadata (files via file store).

**Does not own:** assignment creation, quiz authoring.

**Public contracts:**

- `GET /api/attempts`, `GET /api/attempts/{id}`
- Candidate submit (scoped)
- `POST /api/attempts/{id}/items/{itemId}/review` (`attempts.review`)
- Slice 7: AI-assist suggestion endpoint (still human-confirm)

**Permissions:** `attempts.read`, `attempts.review`.

### 4.6 Search (saved filters)

**Language:** saved filter, target (`openings` / `quizzes` / `templates`), personal vs shared, share mode (`private` / `publicInsideCompany` / `specificUsers`).

**Owns:** stored filter definitions (target, criteria JSON, owner, share mode, share list). **Does not execute** domain queries — Openings and Catalog list endpoints accept the **same criteria shape** Search persists. Search does not read `openings` or `catalog` tables.

**Public contracts:** slice 3 **implemented** (§15.5).

- `GET|POST /api/filters`
- `GET|PUT|DELETE /api/filters/{id}`
- `POST /api/filters/{id}/share`, `POST /api/filters/{id}/unshare`

**Permissions:** `filters.write` (mutate **own** filters); `filters.share` (share/unshare **own** filters, plus ownership resource check). `GET` is authenticated and returns filters the caller can see (owned, public-inside-company, or listed in `sharedWithUserIds`). There is **no** `filters.read` code. Applying criteria on a list endpoint requires only the matching `*.read` (or quiz get’s read-or-write) permission — the client sends `criteria` JSON; list endpoints do **not** join `filterId` into Search.

Persistence is schema `search` (`SearchDbContext`, table `filters`). Search does not read `openings` or `catalog` tables.

---

## 5. Data

| Store | Use |
|--------|-----|
| **PostgreSQL** (one database) | All module tables. Schemas: `access`, `openings`, `catalog`, `delivery`, `evaluation`, `search`. JSONB for dynamic fields, question graphs, snapshots, filter criteria, AI rule JSON. Slice 3 **has** `catalog.templates` / `catalog.template_versions` (owned questions with `SourceQuestionId`) and `search.filters`. Slice 4 **has** `catalog.bank_questions` (no FK from `catalog.questions.source_question_id`). |
| **Local / NAS files** | Resume uploads and future attachments. DB holds path + content type + owning module id. |
| Not used in v1 | Redis as a default, document DB, cloud blobs, message bus. |

EF Core migrations, expand/contract. Dummy seed in Development only.

**Retention:** status/archived-at (or equivalent) per policy family — resumes, attempts, quiz/template/bank content. Configurable years (default 5). Restore requires the matching `archive.restore.*` permission. `archive.restore.catalog` covers quizzes, templates, bank items, and rule sets. No silent hard delete.

---

## 6. Client shape

**Angular SPA + installable PWA.** Online-first. **No Ionic, no Capacitor, no Nx** unless a later dedicated decision.

| Capability | v1 |
|------------|----|
| Web app manifest + `@angular/service-worker` | Yes — installable on supporting browsers |
| Cache hashed app shell | Yes |
| Cache authenticated APIs, tokens, `/me`, login/refresh, quiz payloads, answers | **No** |
| IndexedDB outbox for attempts | **No** |
| Offline authoring / taking a quiz | **No** — show “you are offline” |
| Timed live/async, scoring, magic-link as offline mutations | **No** (integrity, anti-cheat, resume PII) |

Details: `docs/adr/0004-pwa-online-first.md`. Angular implements; this ADR is the product rule.

Candidate and employee UIs may share the SPA with different routes; Auth owns branded entry routing.

**Implemented employee routes** (Angular `app.routes.ts`; permission on the route, same codes as the API):

| Path | Permission | Screen |
|------|------------|--------|
| `/login` | guest (unauthenticated) | Sign in |
| `/` | authenticated | Home |
| `/openings` | `openings.read` | Opening list |
| `/openings/new` | `openings.write` | Create opening |
| `/openings/:id` | `openings.read` | View / edit opening |
| `/quizzes` | `quizzes.read` | Quiz list |
| `/quizzes/new` | `quizzes.write` | Create quiz |
| `/quizzes/:id` | `quizzes.read` **or** `quizzes.write` | View / edit quiz |
| `/templates` | `templates.read` | Template library list/search |
| `/templates/:id` | `templates.read` | Template detail + versions |
| `/questions` | `questions.read` | Question bank list |
| `/questions/new` | `questions.write` | Create bank question |
| `/questions/:id` | `questions.read` **or** `questions.write` | View / edit bank question |
| `/opening-fields` | `openings.fields.manage` | Opening field defaults |
| `/roles`, `/roles/new`, `/roles/:id` | `roles.manage` | Role list / editor |
| `/users`, `/users/new`, `/users/:id` | `users.manage` | User list / form |

Nav and home link to Quizzes when the user has `quizzes.read`. Create is hidden without `quizzes.write`. Recruiter (read only) sees a disabled form; author (write) gets the full editor. Templates nav when `templates.read`. Clone-into-opening is hidden unless the user has **both** `quizzes.write` and `templates.read`. Publish-as-template is hidden without `templates.write`. Question bank nav (shell + home **Question bank**) when `questions.read`. Include from question bank on the quiz editor requires `quizzes.write` **and** `questions.read` on an existing saved quiz (hidden on create-new). Recruiter without `questions.*` does not see bank nav or include. Saved filters live on list screens (openings / quizzes / templates), not a separate admin module and **not** on the question bank list (no `FilterTarget = questions`). Feature screens keep list/editor state in component signals. Session is `AuthService` (`currentUser`, `sessionReady`); permissions are `PermissionService`. There is no global entity store.

Shared UI primitives: [`docs/components/README.md`](components/README.md). Playbook markdown pages are the source of API tables. OSS Storybook 10 (`@storybook/angular-vite`, Angular 21 zoneless application builder) is the isolated gallery for those primitives (`npm run storybook` in `src/interview-quiz-web` → http://localhost:6006). No Chromatic.

---

## 7. Hosting (on-premises)

Skill used: **`enterprise-architecture-onprem.md`**. Azure/AWS skills were not used.

| Concern | Choice |
|---------|--------|
| Compute | Single Modular Monolith on Kestrel, **behind a reverse proxy** (nginx, IIS, or equivalent already in the datacenter). Containers optional if ops already run Docker — not required to ship. |
| TLS | Terminated at the reverse proxy. HTTP from proxy to Kestrel on the internal network. |
| SPA | Same-site: proxy serves static Angular output and `/api`. |
| PWA cache | Proxy **must not** aggressively cache `index.html` or `ngsw.json` (always revalidate). Hashed assets may be long-cached. |
| Secrets | Environment variables, OS-protected files, or HashiCorp Vault (OSS). **Not** cloud KMS. |
| Observability | App **emits** OpenTelemetry traces/metrics + structured logs. **Goes to** an on-prem OTel Collector and files / existing OSS stack. No paid APM. |
| Health | `GET /health/live` (process), `GET /health/ready` (PostgreSQL + file store). CD waits on ready. |
| Correlation | Accept `traceparent` and/or `X-Correlation-ID`; generate if missing; echo to clients. Angular logs status + correlation id, not bodies with PII. |
| Environments | **Dev** (local seed, Docker Compose PostgreSQL), **Test**, **Prod**. No cloud environment names. |
| Dev data | Docker Compose PostgreSQL for local development. |
| CI/CD | Self-hosted runners; DevOps matches this platform. |
| AI (slice 8) | Configured base URL + secret via env/Vault. Operator’s OpenAI-compatible endpoint. |
| Entra (later) | App calls Entra as external IdP; still issues app JWTs. No paid IdP product. |

Suggested env **names** (values never in git): `ConnectionStrings__InterviewQuiz`, `FileStorage__Root`, `Jwt__*` (Auth), `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, `Retention__Years`, later `Ai__BaseUrl` / `Ai__ApiKey`.

---

## 8. Observability — emit vs destination

| Emit (app) | Destination (on-prem) |
|------------|------------------------|
| Structured logs (templates, no secrets) | Files and/or collector |
| OTel traces named after use cases (assign, submit, score, live start, templates.publish, templates.clone, filters.write) | OTel Collector |
| Metrics: request duration, error rate, dependency duration | OTel Collector |
| Health live/ready | Reverse proxy / CI |
| Angular HTTP failures: status + correlation id | API log ingest or collector; **no tokens, no resume text, no answers** |

Intentionally light in v1: no PWA outbox metrics (no outbox). Do not trace raw quiz payloads or answer bodies. Client OTel JS exporter is optional and not required.

---

## 9. Integration style

- **REST** between Angular and the monolith.
- **In-process** module calls for snapshot, opening existence, assignment fetch, later bank read.
- **No** integration event bus, sagas, or domain-event storm in v1.
- Cross-module rule: Delivery **copies** a Catalog snapshot at assign time; it does not join live quiz tables **or live bank tables** at attempt time. Template publish and bank include are the same copy rule.

---

## 10. Risks

- **Integrity of timed attempts** if anyone caches or replays quiz payloads — mitigated by online-first PWA rules and no API cache.
- **Resume PII** on disk and in logs — file store ACLs, no PII in telemetry, retention/restore permissions.
- **Magic-link and JWT theft** — Auth + Security; Architecture only records that candidate tokens must be assignment-scoped.
- **Single host capacity / patch windows** — one monolith first; backup and migration outage are ops realities, not invented SLAs.
- **AI endpoint reachability and prompt leakage** (slice 8) — operator-owned HTTP; never default to a cloud LLM.
- **Entra later** must not fork a second authZ model — same permission catalog, same app JWTs.
- **Filter/tag inconsistency** is a product risk (Brief goal 6), not solved by a rigid hierarchy in software.

No numeric SLAs or KPIs are claimed; Brief §13 is qualitative until a Forms baseline exists.

---

## 11. Open questions (owners)

| Question | Owner |
|----------|--------|
| One branded auth entry that routes employee vs candidate (magic-link / password) — exact UX and routes | **Auth** (product still open; working direction in `requirements/Open questions.md`) |
| JWT storage, refresh, magic-link protocol | **Auth** |
| Exact restore UX per policy family (already: separate permissions, no hard delete) | **Master / product**; Access + owning modules implement |
| Concrete company AI JSON schema | **Catalog (.NET)** in slice 8 |
| Adjacent-pair scoring edge cases (ties, duplicate items) | **Evaluation (.NET)** when scoring lands |
| Reverse proxy product, DMZ vs internal zones, Vault vs env | **DevOps** + ops |
| Which self-hosted CI already exists | **DevOps** |
| Threat model of live “watch the screen” vs app-side controls | **Security** (not Architecture) |
| Fork-to-new-template (publish a cloned quiz as a **new** lineage instead of versioning the origin) | **Product / Master** — out of slice 3; default is stay in lineage (§15.1) |
| Recruiter instantiate-from-template (clone requires `quizzes.write`; Dev Recruiter seed does not have it) | **Product / Master** — slice 3 clone is an authoring action; assignment (slice 5) binds an existing quiz |

---

## 12. First slices (Brief §14, with bank inserted)

Vertical slices. Each slice is not done until the collaboration skill DoD holds (permission on API + UI, migrations, tests, correlation id, no secrets, contract updated).

| Slice | Status | What | Modules |
|-------|--------|------|---------|
| **1 Foundations** | Implemented | Users, permission catalog + role editor, openings + dynamic fields/tags, Angular shell / PWA | Access, Openings, Angular |
| **2 Authoring** | Implemented | Quiz authoring API + Angular list/editor; eight v1 question types; scoring and credit modes | Catalog, Angular |
| **3 Templates and search** | Implemented | Publish template, versions, list/search, clone into opening, saved/shared filters; **question-bank groundwork** (`sourceQuestionId`, reserved `IQuestionBankReader`, `questions.*` seeded) | Catalog, Search, Access, Angular |
| **4 Question bank** | Implemented | Authoring library + include-into-quiz (copy-on-include); Angular list/editor and quiz include panel; `questions.read` / `questions.write` on Dev Template author seed | Catalog, Angular |
| **5 Assignments** | Later | Candidate assignment, snapshot, async timed link, basic results | Delivery, Evaluation (submit + auto-score), Access (magic-link) |
| **6 Live mode** | Later | Start/pause/monitor on the **same** assignment model | Delivery |
| **7 Review** | Later | Human + AI-assist marking, auditable drafts, finalise | Evaluation |
| **8 AI draft** | Later | Resume + opening + rules → draft → forced human edit | Catalog (+ file store, operator LLM HTTP) |

No LLM in slices 1–6. Slice 7 AI-assist may call the same operator endpoint when configured; if unset, human-only review still works. Slice 8 is the first **authoring** LLM path.

---

## 13. ADRs

| ADR | Topic |
|-----|--------|
| [0001](adr/0001-hosting-on-premises.md) | On-premises hosting; Azure/AWS out of scope |
| [0002](adr/0002-modular-monolith.md) | Modular Monolith, REST, simple DDD |
| [0003](adr/0003-data-postgresql.md) | PostgreSQL + JSONB; one DB until split |
| [0004](adr/0004-pwa-online-first.md) | Installable PWA, online-first, shell cache only |
| [0005](adr/0005-no-cqrs.md) | No CQRS, no MediatR |
| [0006](adr/0006-auth-given-jwt-identity.md) | JWT + Identity + magic-link + Entra-later as given |
| [0007](adr/0007-question-bank-copy-on-include.md) | Question bank in Catalog; copy-on-include; `sourceQuestionId` |

Permission catalog: [`docs/permissions.md`](permissions.md).

---

## 14. Delivery status

Slices **1** (foundations), **2** (Catalog quiz authoring), **3** (templates, saved filters, question-bank groundwork), and **4** (question bank API + Angular UI, copy-on-include) are implemented. Hosting and auth ADRs (0001, 0006) are unchanged. ADR 0007 records the bank.

Still later: production runbook, self-hosted CI (DevOps), candidate magic-link, assignments, live mode, review, AI draft. No LLM in slices 1–6.

---

## 15. Slice 3 contracts (implemented)

Locked contract for Catalog/Search. Status: **in code**. Do not treat this section as a backlog. Slice 4 bank APIs are in §15.7.

REST, camelCase JSON, permission-based. Page defaults match `PageRequest` (page 1, pageSize 20, cap 100). No CQRS, no MediatR. Catalog and Search application services; never a mediator.

All question payloads reuse one shape (existing fields + groundwork):

`QuestionRequest` / `QuestionResponse` / `QuizSnapshotQuestionDto` / template-version questions:

| Field | Notes |
|-------|--------|
| `id` | Guid. New on create if omitted/empty; **new** on publish copy and clone copy |
| `type` | Same eight strings as slice 2 |
| `stem` | Required |
| `scoringMode` | `auto` / `aiAssist` / `humanOnly` |
| `creditMode` | Required only for `multipleChoiceMulti` and `ordering` |
| `points` | ≥ 1 |
| `body` | JSON; same validators as slice 2 |
| `sourceQuestionId` | **Nullable Guid.** Provenance of a bank include. Slice 3 **persists as sent**; does not validate against a bank. Slice 4 include sets it. Omitted/null = authored in this quiz (or provenance dropped). Stem edits do **not** auto-clear it |

`sortOrder` is list order on write (same as slice 2). Response includes `sortOrder`.

### 15.1 Template lineage (publish identity)

**One template lineage per origin, not always-new-template.**

Quiz carries server-owned `originTemplateId` (nullable Guid) on `QuizResponse`. **Not** client-writable on `CreateQuizRequest` / `UpdateQuizRequest` (ignore if sent).

| Situation | Publish does |
|-----------|----------------|
| Quiz has `originTemplateId == null` and no template exists with `originQuizId == quiz.id` | **Create** template `T`, version **1**. Set `T.originQuizId = quiz.id`. Set `quiz.originTemplateId = T.id`. |
| Quiz has `originTemplateId` set | **New version** of that template. |
| Quiz has `originTemplateId == null` but a template already has `originQuizId == quiz.id` (first publish already happened; quiz row should have been updated — recovery) | **New version** of that template; set `quiz.originTemplateId`. |
| Quiz was **cloned** from a template version | Clone already set `originTemplateId`. Publish → **new version** of that same template. |

Concurrent publish: unique `(templateId, versionNumber)`; loser retries. Publishing updates `quiz.originTemplateId` and increments quiz `rowVersion`.

**Not in slice 3:** in-place `PUT /api/templates/{id}`; fork-to-new-lineage flag; bank include API.

Template tables (Catalog schema `catalog`): `templates` (lineage: `id`, `originQuizId`, timestamps) and `template_versions` (version number, metadata snapshot from the quiz at publish, `publishedFromQuizId`, `publishedAtUtc`, `publishedByUserId`, owned questions with the same `Question` shape including `sourceQuestionId`). List/search uses **latest version** metadata (title, description, experience, tags).

### 15.2 Publish, list, get, clone

| Method | Path | Permission | Result |
|--------|------|------------|--------|
| `POST` | `/api/quizzes/{id}/publish-template` | `templates.write` | `PublishTemplateResponse` 201 |
| `GET` | `/api/templates` | `templates.read` | `PagedResult<TemplateSummaryResponse>` |
| `GET` | `/api/templates/{id}` | `templates.read` | `TemplateResponse` (lineage + latest version **metadata**, no full question bodies) |
| `GET` | `/api/templates/{id}/versions` | `templates.read` | `PagedResult<TemplateVersionSummaryResponse>` or a full list if short — prefer paged with same `page`/`pageSize` |
| `GET` | `/api/templates/{id}/versions/{versionId}` | `templates.read` | `TemplateVersionResponse` (full `questions`) |
| `POST` | `/api/templates/{id}/versions/{versionId}/clone` | `quizzes.write` **and** `templates.read` | `QuizResponse` 201, `Location: /api/quizzes/{newId}` |

Unknown template/version, or `versionId` not under `{id}` → 404. Unknown `openingId` on clone → 400 `"Opening does not exist."` via `IOpeningLookup` (Catalog does not read the `openings` schema).

**`PublishTemplateResponse`:** `templateId`, `versionId`, `versionNumber` (int, 1-based), `createdNewTemplate` (bool).

**`TemplateSummaryResponse`:** `id`, `title`, `description`, `expectedExperienceYears`, `tags`, `latestVersionId`, `latestVersionNumber`, `originQuizId`, `updatedAtUtc`.

**`TemplateResponse`:** summary fields plus `createdAtUtc`. Client loads questions from the version endpoint.

**`TemplateVersionSummaryResponse`:** `id`, `templateId`, `versionNumber`, `title`, `publishedFromQuizId`, `publishedAtUtc`, `questionCount`.

**`TemplateVersionResponse`:** summary plus `description`, `expectedExperienceYears`, `tags`, `questions` (`QuestionResponse[]` with `sourceQuestionId`).

**Clone body `CloneTemplateVersionRequest`:** `openingId` (required Guid), `title` (optional string; default = version title). Copies description, `expectedExperienceYears`, tags from the version. New quiz id. **New question ids.** **Preserve `sourceQuestionId`.** Set `originTemplateId` to the template lineage id. Store `sourceTemplateVersionId` on the quiz if useful for audit (optional nullable Guid on `QuizResponse`; not used to choose the publish target).

Publish **copies** questions from the quiz: new ids, preserve `sourceQuestionId`, snapshot title/description/experience/tags onto the version.

Activity names: `templates.publish`, `templates.list`, `templates.get`, `templates.versions`, `templates.clone`. Structured logs with template id / version number; no question bodies.

### 15.3 Template and quiz list criteria

Search persists this JSON; Catalog executes it. Field names match Openings where they overlap (`experienceMinYears`, `experienceMaxYears`, `tags`).

**`TemplateListCriteria`** (also bindable from query string; `criteria` JSON wins like openings):

| Field | Match |
|-------|--------|
| `keyword` | Case-insensitive substring on **title** (product “keyword/title”) |
| `experienceMinYears` / `experienceMaxYears` | Inclusive range on `expectedExperienceYears` |
| `tags` | All listed key/value pairs must match (same as openings) |

Query also: `page`, `pageSize`, `criteria` (full JSON).

**`QuizListCriteria`** — implemented (expanded from slice 2 `openingId`-only):

| Field | Match |
|-------|--------|
| `openingId` | Exact (existing) |
| `keyword` | Case-insensitive substring on **title** |
| `experienceMinYears` / `experienceMaxYears` | Inclusive range on `expectedExperienceYears` |
| `tags` | All listed key/value pairs must match |

Query: existing `openingId`, `page`, `pageSize`, plus `keyword`, experience range, `tags` JSON, and `criteria` (full `QuizListCriteria` JSON). Saved filters with `target: "quizzes"` must round-trip this shape.

**`OpeningListCriteria`** — already implemented; do not change field names. Saved filters with `target: "openings"` store that JSON as-is.

### 15.4 Quiz response additions (slice 3)

`QuizResponse` adds `originTemplateId` and `sourceTemplateVersionId` (nullable Guids). Questions add `sourceQuestionId`. `IQuizSnapshotReader` / `QuizSnapshotQuestionDto` add `sourceQuestionId`. EF: nullable uuid column on `catalog.questions` and template-version owned questions; **no FK** to bank items. Slice 4 added `catalog.bank_questions` and still does **not** add an FK from `catalog.questions.source_question_id`.

### 15.5 Saved filters (Search)

Schema `search`. Search stores; Catalog/Openings execute.

| Method | Path | Permission | Notes |
|--------|------|------------|--------|
| `GET` | `/api/filters` | authenticated | Visible filters only. Query: optional `target` (`openings` / `quizzes` / `templates`), `page`, `pageSize` |
| `GET` | `/api/filters/{id}` | authenticated | 404 if not visible |
| `POST` | `/api/filters` | `filters.write` | Creates **owned** filter; default `shareMode: private` |
| `PUT` | `/api/filters/{id}` | `filters.write` | **Owner only.** Name, target, criteria. Does not share |
| `DELETE` | `/api/filters/{id}` | `filters.write` | **Owner only.** Archive or delete row; no silent hard-delete of audit if you already have archive patterns — a hard delete of a personal filter is acceptable in v1 (not retention-policy content) |
| `POST` | `/api/filters/{id}/share` | `filters.share` | **Owner only.** Body sets share mode |
| `POST` | `/api/filters/{id}/unshare` | `filters.share` | **Owner only.** Sets `private`, clears `sharedWithUserIds` |

**`FilterResponse` / `CreateFilterRequest` / `UpdateFilterRequest`:**

| Field | Type | Notes |
|-------|------|--------|
| `id` | Guid | |
| `name` | string | Required on write |
| `target` | `openings` / `quizzes` / `templates` | Required |
| `criteria` | object | **Must deserialize** to `OpeningListCriteria` / `QuizListCriteria` / `TemplateListCriteria` for that target. 400 if shape does not match. Persist JSONB camelCase |
| `ownerUserId` | string | Current user on create; not client-writable |
| `shareMode` | `private` / `publicInsideCompany` / `specificUsers` | |
| `sharedWithUserIds` | string[] | Required when `specificUsers` (Identity user ids). Empty otherwise |
| `createdAtUtc` / `updatedAtUtc` | | |

**`ShareFilterRequest`:** `shareMode` (`publicInsideCompany` or `specificUsers`), `userIds` (required when specific). `private` is unshare, not this body.

Visibility: owner **or** `publicInsideCompany` **or** (`specificUsers` and current user id in `sharedWithUserIds`). Single company; public means everyone in this app’s user store, not the internet.

Activity names: `filters.list`, `filters.write`, `filters.share`. Do not log criteria JSON if it might include names beyond tags — tags/keyword are fine.

### 15.6 Question-bank groundwork (implemented in slice 3)

Slice 3 shipped no bank REST or UI (slice 4 completed both — §15.7). In Catalog/Access:

1. Nullable `sourceQuestionId` on quiz questions (`Question` + EF `catalog.questions`), template-version questions, `QuestionRequest`, `QuestionResponse`, `QuizSnapshotQuestionDto`.
2. Same `Question` shape reused later by bank items: `id`, `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`, `sourceQuestionId` (bank rows use `sourceQuestionId` null; they **are** the source).
3. Catalog application interface `IQuestionBankReader` with `GetByIdAsync(Guid id, CancellationToken)` → `QuestionBankItemDto` (question shape plus `title`, `tags`, `expectedExperienceYears`, `archivedAtUtc`). Registered in slice 4. Not called from quiz create/update/list/publish/clone.
4. Publish and clone **copy** questions with **new ids** and **preserve** `sourceQuestionId`.
5. Access seeds permission catalog rows `questions.read` / `questions.write`. Slice 4 grants them on the Dev Template author seed bundle.
6. `archive.restore.catalog` covers bank items; unarchive on `/api/questions/{id}/unarchive` is library undelete, not retention restore.

### 15.7 Slice 4 bank (implemented)

Catalog table `catalog.bank_questions` — not owned by a quiz; not the drag-drop body. No FK from `catalog.questions.source_question_id`. Library metadata: `title`, `tags`, `expectedExperienceYears` (0–80), plus the `Question` payload (`type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`). `row_version` concurrency. No hard delete (`ArchivedAtUtc`). Archived items are excluded from the default list and cannot be included into a quiz.

**`BankQuestionResponse`:** `id`, `title`, `tags`, `expectedExperienceYears`, `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`, `archivedAtUtc` (null if live), `rowVersion`, `createdAtUtc`, `updatedAtUtc`. No `sortOrder`.

**Create/Update request:** `title`, `tags`, `expectedExperienceYears`, `type`, `stem`, `scoringMode?`, `creditMode?`, `points`, `body`. Update also `rowVersion`. Client `sourceQuestionId` is ignored.

**`IncludeQuestionsRequest`:** `questionIds` (required, non-empty, unique), `insertAt` (optional 0-based index; default append; clamped 0..count), `rowVersion` (quiz concurrency). Include copies via `IQuestionBankReader`; new quiz question ids; `sourceQuestionId` = bank item id. Returns `QuizResponse` 200.

List query: `page`, `pageSize`, `keyword` (title or stem), `type`, `experienceMinYears` / `experienceMaxYears`, `tags` JSON, `criteria` JSON (`BankQuestionListCriteria`), `archived` bool default false (`true` requires `questions.write`).

Activities: `questions.list`, `questions.write`, `questions.archive`, `quizzes.include-questions`. Logs: ids, not question bodies.

**Angular (implemented):** `/questions` (`questions.read`), `/questions/new` (`questions.write`), `/questions/:id` (`questions.read` or `questions.write`). List: keyword, type, experience, tags; archived-only toggle **only** with `questions.write`. **No** SavedFilters / no `FilterTarget = questions`. Editor: title, tags, expectedExperienceYears, same eight question types as the quiz mapper; archive/unarchive with write (hidden on create-new); read-only without write. Quiz editor **Include from question bank** only when `quizzes.write` **and** `questions.read` **and** the quiz is already saved (hidden on create-new); copy via include API; provenance hint “From question bank” when `sourceQuestionId` is set. Recruiter without `questions.*` does not see bank nav or include. Permission helper lists `QuestionsRead` / `QuestionsWrite`. No new shared playbook primitive.

---

## Handoff

See the specialist return to Master (same contract). Artifacts are the paths in §13 plus this file.
