# ADR 0007 — Question bank is Catalog-owned; copy-on-include

## Context

v1 already has three reuse mechanisms that are easy to confuse:

1. **Quiz** — mutable working copy under **one** opening. Questions are owned rows (`catalog.questions`) with Guid id, type, stem, scoring, credit, JSONB body.
2. **Template** — whole-quiz reuse across openings (Brief §16). Edit → new version; assignments keep the old version. Assignment freeze is a **snapshot copy** (`IQuizSnapshotReader` / `QuizSnapshotDto`), not a live join.
3. **Drag-and-drop shared answer bank** — a **question type** (`dragDropSharedBank`): items live *inside one question* and are dragged into slots. It is not a company item library.

Authors also need a **company question bank**: reusable *items* (full questions) that can be included into many quizzes. The current system must already support that bank when the slice is built (extension points now; no bank CRUD/UI in this pass).

Two obvious alternatives:

- **Live-join** bank items by id at attempt time (quiz rows hold only `sourceQuestionId`).
- A **new bounded context** for the bank, with its own question types and scoring.

Both fight existing freeze rules and would duplicate Catalog invariants.

## Decision

1. **The question bank is Catalog-owned.** Same question types, scoring modes, and credit modes as quiz questions (`Question` shape: `id`, `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`). Not a new bounded context. Slice 4 persists bank items in Catalog (separate table from quiz-owned `catalog.questions`); slice 3 does not create that table.

2. **Copy-on-include, never live-join at attempt time.** Including a bank item into a quiz copies type, stem, scoring, credit, and body. The quiz question gets a **new** id. Nullable `sourceQuestionId` on the copy points at the bank item. Later bank edits do **not** change existing quizzes, template versions, or assignment snapshots. Same copy rule as templates and assignment snapshots.

3. **Provenance is optional and opaque until slice 4.** `sourceQuestionId` is a nullable Guid on quiz questions, template-version questions, snapshot DTOs, and quiz API request/response. Slice 3 persists it when the client sends it and does **not** validate it against a bank (no bank rows yet, no fake data). Slice 4 include will set it after a real `IQuestionBankReader` lookup.

4. **Template publish copies questions (new ids) and preserves `sourceQuestionId`** so provenance survives publish and clone.

5. **Delivery still snapshots the quiz graph.** Attempt time never joins live quiz tables or live bank tables. `sourceQuestionId` on a snapshot is provenance only.

6. **Permissions:** stable codes `questions.read` and `questions.write` (see `docs/permissions.md`). Seed in the Access catalog when .NET lands slice 3 groundwork. Dev Template author receives `questions.write` when the **bank ships** (slice 4), not in the slice 3 seed bundle. API and UI check permission codes, never role names.

7. **Slice order:** insert **slice 4 Question bank** (authoring library + include-into-quiz). Shift former 4–7 to **5–8**. Slice 3 stays templates + saved filters and proceeds now. No bank REST or UI in slice 3.

8. **In-process `IQuestionBankReader`** is reserved on Catalog for later include/validation. Slice 3 defines the interface and does not call it from quiz CRUD. No stub that returns sample bank items.

## Consequences

- .NET adds `sourceQuestionId` on the existing `Question` shape and DTOs in the slice 3 pass, so slice 4 does not rewrite quiz/template/snapshot contracts.
- Bank CRUD (`GET|POST|PUT /api/questions`, include-into-quiz) waits for slice 4. Operators can grant `questions.*` in the role editor once codes are seeded; endpoints will not exist until slice 4.
- Drag-and-drop **shared answer bank** stays a question-type body. Do not reuse that name for the company library in APIs or UI copy.
- Forking a quiz to a **new** template lineage (instead of versioning the origin template) is **not** in slice 3. See `docs/architecture.md` §15.1.
- No CQRS, no MediatR, no commercial libraries, no Ionic, no Azure/AWS defaults (ADRs 0001–0006 unchanged).
