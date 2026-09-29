Interview Quiz Platform — Product Brief (v1)

Status: Draft for alignment
Audience: Product, engineering, recruitment, and template-authoring teams
Based on: Current Microsoft Forms–based interview quiz process and agreed v1 decisions

---

1. Problem

Today, Development/QA build interview question sets in Microsoft Forms and hand them to Recruitment. In the first interview phase, recruiters share the relevant quiz (for example a .NET quiz), ask the candidate to share their screen, and have them take it live.

That process breaks down in several ways:

* The quiz set is small and the same pack is reused across many candidates.
* Recruitment often does not know what is inside the quiz.
* Question types are limited by Forms.
* Content is hard to maintain, version, or specialise per opening or candidate.
* There is no clean link from a quiz to an opening, client, project, role, or experience bar.
* There is no good path to reuse, template, filter, or AI-assist while keeping humans in control.

2. Vision

Build an internal openings-centric interview quiz platform where anyone with the right permission can:

* Create and maintain openings with flexible tracking fields.
* Build quizzes manually or from an AI draft (resume + company rules), then review before use.
* Turn strong quizzes into templates owned across teams (Dev, QA, HR, Finance, etc.).
* Search templates by experience and tags, then assign to a candidate.
* Run the assignment as live (proctored / screen-share style) or async (timed link).
* Track attempts, auto-score where possible, and review written answers with per-question scoring modes.

3. Goals (v1)

1. Replace ad-hoc Forms packs with searchable, maintainable quizzes tied to openings.
2. Let Recruitment assign the right quiz without owning deep technical content.
3. Let specialist teams author and maintain templates with richer question types.
4. Support both live and async delivery from day one using one quiz model.
5. Use AI only as a draft assistant—humans always edit before assign.
6. Avoid rigid org hierarchy: use tags / key–value fields and saved filters instead.

4. Non-goals (v1)

* Full coding IDE / online judge (no code question type in v1).
* Fully automatic assign with no human edit of AI output.
* Replacing the whole ATS / hiring system (this sits beside openings + interview process).
* Advanced proctoring hardware (webcam AI, lockdown browsers) beyond practical live/async controls.
* Candidate career portal or public self-serve job applications.
* ATS / HRIS integration (long-term vision only; not a v1 requirement).

---

5. Primary users & access model

Prefer fine-grained permissions over fixed job-title roles.
Named groups (Recruiter, Tech author, Admin, etc.) are only bundles of permissions, not hard-coded product roles.

Suggested permission capabilities

* Manage admin defaults — Default dynamic fields/tags, company AI rule sets
* Manage users & permission groups — Grant/revoke capability bundles
* Create / edit openings — Title, JD, owners, dates, headcount, handlers, tags
* Create / edit quizzes — Manual authoring, structure, scoring mode per question
* Create / edit templates — Publish quiz as template; maintain library
* Use AI draft — Generate draft from resume + rules
* Assign quizzes — Attach quiz to candidate + opening; choose live or async
* Run live sessions — Start/pause/monitor live attempt
* Review attempts — Score written answers; finalise result
* Manage saved filters — Personal filters; share public or with specific people

A Dev engineer can author templates without opening access. A recruiter can assign and run sessions without editing company AI rules.

---

6. Core domain model

6.1 Opening

The anchor object for hiring work.

Core fields

* Title
* Job description
* Owner (e.g. PM)
* Start date
* Expected close date
* Numbers required (headcount)
* Expected experience level
* Handlers / recruiters working the opening

Dynamic fields (tags as key–value pairs)

* Admin defines default keys for new openings (commonly: Client, Project, Role; extensible).
* Users fill values per opening; additional keys allowed where permitted.
* Hierarchy is intentionally loose—filtering and saved views replace a rigid tree.

6.2 Quiz

A concrete question set that can be assigned.

* Title, description, experience target, tags
* Linked to one or more openings (or created in opening context)
* Questions with type + scoring mode
* Can be converted to a template for reuse

6.3 Template

A reusable quiz owned/maintained by specialist teams.

* Searchable by experience, tags, team, keyword
* Recruiter (or permitted user) clones/instantiates into a quiz for an opening/candidate
* Supports specialised variants: generic template vs candidate-specific quiz (manual or AI-drafted)

6.4 Assignment

Binds a quiz to a candidate under an opening.

* Candidate identity/contact
* Opening + quiz
* Mode: live | async
* Timing / attempt rules
* Status: not started → in progress → submitted → pending review → completed

Mode is a property of the assignment, not a separate product. Scoring, tags, openings, and reports stay shared.

6.5 Attempt & result

* Answers, timestamps, duration
* Auto-scored items settled immediately
* Written items follow per-question scoring mode
* Attempt stays pending review until every non-auto item is settled
* Final score / outcome visible to permitted users

6.6 Saved filters

* Filter openings/quizzes by tags, experience, owner, dates, handlers, etc.
* Save personal filters
* Share as public or with specific people

---

7. Question types (v1)

* Multiple choice — single answer — Auto-score
* Multiple choice — multi-select — Auto-score. Credit mode per question: Partial credit or All-or-nothing (company default available)
* True / false — Auto-score
* Short text / fill-in — Optional auto when acceptable answers are keyed; otherwise AI-assist or human
* Long text / written scenario — AI-assist or human-only (not auto by default)
* Drag and drop — shared answer bank — Auto-score. One bank of items (plus author-chosen distractors) dragged into slots in the stem
* Drag and drop — per-slot options — Auto-score. Each slot has its own option set (plus author-chosen distractors)
* Ordering / sequence — Auto-score. Candidate reorders a list. Credit mode per question: Partial credit or All-or-nothing (same modes as multi-select; default partial formula TBD — recommended adjacent-pair)

Mobile: aim to support drag interactions on mobile in v1 (tap-to-place fallback if native drag is costly). Defer only if remaining effort is still large.

Out of v1: code / coding problems.

---

8. Scoring model

Each question declares a scoring mode:

1. Auto — objective items; keyed short answers where configured
2. AI-assist — system suggests score/feedback; human confirms
3. Human-only — reviewer marks without AI suggestion

MCQ, true/false, both drag-and-drop types, and ordering default to auto. Long text defaults to human-only or AI-assist (author chooses). Short text chooses based on whether acceptable answers are defined.

Credit modes (multi-select and ordering)

Per question (company default available):

* Partial credit — award points for correctly selected / correctly placed parts
* All-or-nothing — award points only when the complete answer is correct

AI-assist audit trail

AI-assist scores are stored as auditable drafts: the AI suggestion, the human confirmation (or edit), actor, and timestamps are retained for review history — not discarded suggestions-only.

---

9. Delivery modes

Live (screen-share / proctored style)

* Interviewer / recruiter starts a live session while the candidate is present (existing interview habit: candidate screen-share, recruiter watches)
* Timing can be set on live assignments too (overall duration and/or per-section limits)—same timing controls as async, not live-only freeform
* Recruiter’s primary job in live mode is to watch what the candidate is doing; optional start/pause/progress controls support that, not replace the timer
* Full result after submit (and after review if needed)

Async (timed link)

* Candidate receives a timed link
* One attempt (or controlled retries by policy)
* Clear time box
* Recruiter opens result without watching live

Both modes share quiz content, openings, tags, reporting, and assignment timing / attempt rules. Timing is a property of the assignment, available for live and async.

---

10. AI draft generation

v1 policy: suggest only — human must review and edit before assign.

Inputs

* Optional resume (upload/paste)
* Opening context (JD, experience, tags)
* Optional template as base
* Company rule set — global layer (topics to cover/avoid, difficulty, question mix, compliance wording, banned content, language) plus required fields per question type so system prompts stay consistent

Output

* Draft quiz (questions, types, suggested scoring modes, answer keys where applicable)
* Human edits in the quiz editor
* Only then: assign (live or async)

No silent auto-assign from AI.

---

11. Key user flows

A. Create opening

1. User creates opening with core fields.
2. Default dynamic fields from admin appear; user fills values; may add extras if allowed.
3. Assign handlers/recruiters.
4. Opening becomes filterable/taggable for quizzes and assignments.

B. Author template / quiz (manual)

1. Permitted author builds questions (types above).
2. Sets scoring mode per question.
3. Tags by experience/client/project/role/etc.
4. Optionally publishes as template for others to search.

C. AI-assisted specialised quiz

1. User selects opening (+ optional template) and provides resume.
2. Applies company rule set.
3. AI returns draft.
4. Human edits.
5. Save as quiz; optionally promote to template later.

D. Assign to candidate

1. Search template or existing quiz (filters / saved views).
2. Instantiate/link under opening.
3. Choose live or async.
4. Configure timing/attempt rules.
5. Share session or send link.

E. Complete & review

1. Candidate submits.
2. Auto items score immediately.
3. Reviewers complete AI-assist / human-only items (AI-assist kept as auditable drafts).
4. Result marked complete for the opening/candidate.

---

12. Admin configuration (v1)

* Default dynamic field keys for openings (e.g. Client, Project, Role)
* Permission groups as capability bundles
* Company AI rule sets (versioned if possible)
* Optional defaults: live/async duration, attempt limits, multi-select / ordering credit-mode default
* Data retention default: 5 years (configurable); after retention, archive (no hard delete / silent loss)

---

13. Success criteria (qualitative)

v1 succeeds when:

* Recruitment can find and assign a quiz for an opening without pinging Dev for the Forms link every time.
* Specialist teams maintain templates instead of one-off Forms.
* The same quiz content supports live and async without duplicate authoring.
* AI drafts speed authoring but never skip human edit.
* Openings remain findable via tags + saved filters despite inconsistent hierarchy.
* Written answers have a clear path to a finalised score.

(Numeric KPIs can be added once baseline Forms usage is measured.)

---

14. Suggested build order

1. Foundations: users, fine-grained permissions, openings + dynamic fields/tags
2. Authoring: quiz editor + question types + per-question scoring modes
3. Templates & search: publish template, filters, saved/shared filters
4. Assignments: candidate assignment, async timed link, basic results
5. Live mode: live session controls on the same assignment model
6. Review: human + AI-assist marking for written answers
7. AI draft: resume + opening + company rules → draft quiz → forced human edit gate

---

15. Open points to decide later

* Exact auth routing: one branded entry that routes employee SSO vs candidate magic link / password (app DB users first; Entra later)
* Retention restore: shared vs separate policies for resumes / attempts / quiz content; who can restore from archive
* Ordering partial-credit formula (recommended default: adjacent-pair)
* Concrete company AI rule schema / versioning details

16. Recommended clarifications

Keep these decisions simple and explicit before development:

* Template versioning: When a template changes, create a new version. Existing assignments keep the old version.
* Quiz snapshot: When a quiz is assigned, save the exact questions, answers, and scoring rules used for that assignment.
* Quiz reuse: A quiz can be used for one opening. To reuse the same quiz for another opening, convert it to a template first.
* Dynamic filters: Keep filters flexible. Users should be able to create, save, and share filters using tags and custom fields.
* Scoring: Keep one common scoring model for all question types: Auto, AI-assist, or Human-only.
* AI: AI creates drafts only. A user must review and approve before a quiz can be assigned.
* Retention: Keep the default 5-year retention, but define separate restore rules for resumes, attempts, and quiz content.
* Authentication: Decide the employee SSO and candidate magic-link flows before implementing assignment security.

These decisions reduce confusion later and make the system easier to build and maintain.

---

17. One-line pitch

An openings-first interview quiz system: templates and tags for reuse, live or async assignment, and AI that drafts—never publishes—while permissions stay fine-grained.
