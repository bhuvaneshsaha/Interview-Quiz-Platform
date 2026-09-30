# ADR 0007 — Question bank is Catalog-owned; copy-on-include

Status: **Implemented** (API + Angular UI). Decision unchanged. Slice 4 is in the code: `catalog.bank_questions`, `/api/questions`, include-into-quiz, registered `IQuestionBankReader`, `questions.read` / `questions.write` on Dev Template author, Angular `/questions` (list/editor) and the quiz editor include panel.

## Context

v1 already has three reuse mechanisms that are easy to confuse:

1. **Quiz** — mutable working copy under **one** opening. Questions are owned rows (`catalog.questions`) with Guid id, type, stem, scoring, credit, JSONB body.
2. **Template** — whole-quiz reuse across openings (Brief §16). Edit → new version; assignments keep the old version. Assignment freeze is a **snapshot copy** (`IQuizSnapshotReader` / `QuizSnapshotDto`), not a live join.
3. **Drag-and-drop shared answer bank** — a **question type** (`dragDropSharedBank`): items live *inside one question* and are dragged into slots. It is not a company item library.

Authors also need a **company question bank**: reusable *items* (full questions) that can be included into many quizzes. Slice 3 shipped extension points (`sourceQuestionId`, reserved `IQuestionBankReader`, seeded `questions.*` codes) so slice 4 could add bank CRUD and UI without rewriting quiz/template/snapshot contracts. Slice 3 did not ship bank REST or UI.

Two obvious alternatives:

- **Live-join** bank items by id at attempt time (quiz rows hold only `sourceQuestionId`).
- A **new bounded context** for the bank, with its own question types and scoring.

Both fight existing freeze rules and would duplicate Catalog invariants.

## Decision

1. **The question bank is Catalog-owned.** Same question types, scoring modes, and credit modes as quiz questions (`Question` shape: `id`, `type`, `stem`, `scoringMode`, `creditMode`, `points`, `body`). Not a new bounded context. Slice 3 did not create the table; slice 4 persists bank items in Catalog as `catalog.bank_questions` (separate from quiz-owned `catalog.questions`). No FK from `catalog.questions.source_question_id`.

2. **Copy-on-include, never live-join at attempt time.** Including a bank item into a quiz copies type, stem, scoring, credit, and body. The quiz question gets a **new** id. Nullable `sourceQuestionId` on the copy points at the bank item. Later bank edits do **not** change existing quizzes, template versions, or assignment snapshots. Same copy rule as templates and assignment snapshots.

3. **Provenance.** `sourceQuestionId` is a nullable Guid on quiz questions, template-version questions, snapshot DTOs, and quiz API request/response. Slice 3 persisted it when the client sent it and did **not** validate it against a bank. Slice 4 include sets it after a real `IQuestionBankReader` lookup. The Angular quiz editor shows the hint “From question bank” when `sourceQuestionId` is set.

4. **Template publish copies questions (new ids) and preserves `sourceQuestionId`** so provenance survives publish and clone.

5. **Delivery still snapshots the quiz graph.** Attempt time never joins live quiz tables or live bank tables. `sourceQuestionId` on a snapshot is provenance only.

6. **Permissions:** stable codes `questions.read` and `questions.write` (see `docs/permissions.md`). Access seeded the catalog rows in slice 3. Slice 4 granted them on the Dev Template author Development seed (not Recruiter). API and UI check permission codes, never role names.

7. **Slice order:** insert **slice 4 Question bank** (authoring library + include-into-quiz). Shift former 4–7 to **5–8**. Slice 3 stayed templates + saved filters and shipped first. No bank REST or UI in slice 3; that was a slice 3 constraint, not a remaining gap.

8. **In-process `IQuestionBankReader`** is Catalog-internal. Slice 3 defined the interface and did not call it from quiz CRUD. Slice 4 registers it and uses it **only** for include (not quiz create/update/publish/clone). No stub that returns sample bank items.

## Consequences

- .NET added `sourceQuestionId` on the existing `Question` shape and DTOs in the slice 3 pass, so slice 4 did not rewrite quiz/template/snapshot contracts.
- Slice 4 shipped bank CRUD (`GET|POST|PUT /api/questions`, archive/unarchive, include-into-quiz) and Angular bank UI (`/questions`, `/questions/new`, `/questions/:id`) plus include-from-quiz. Operators grant `questions.*` in the role editor; Dev Template author seed includes them. Recruiter seed has no `questions.*`.
- Drag-and-drop **shared answer bank** stays a question-type body. Do not reuse that name for the company library in APIs or UI copy.
- Forking a quiz to a **new** template lineage (instead of versioning the origin template) is **not** in slice 3. See `docs/architecture.md` §15.1.
- No CQRS, no MediatR, no commercial libraries, no Ionic, no Azure/AWS defaults (ADRs 0001–0006 unchanged).
