Interview Quiz Platform — Overview

Status: Draft for alignment
Audience: Product, engineering, recruitment, and template-authoring teams

Pitch

An openings-first interview quiz system: templates, a question bank of reusable items, and tags for reuse, live or async assignment, and AI that drafts—never publishes—while permissions stay fine-grained.

Problem

Today, Development/QA build interview question sets in Microsoft Forms and hand them to Recruitment. In the first interview phase, recruiters share the relevant quiz (for example a .NET quiz), ask the candidate to share their screen, and have them take it live.

That process breaks down because:

* The quiz set is small and the same pack is reused across many candidates.
* Recruitment often does not know what is inside the quiz.
* Question types are limited by Forms.
* Content is hard to maintain, version, or specialise per opening or candidate.
* There is no clean link from a quiz to an opening, client, project, role, or experience bar.
* There is no good path to reuse, template, filter, or AI-assist while keeping humans in control.

Vision

Build an internal openings-centric interview quiz platform where anyone with the right permission can:

* Create and maintain openings with flexible tracking fields.
* Build quizzes manually or from an AI draft (resume + company rules), then review before use.
* Turn strong quizzes into templates owned across teams (Dev, QA, HR, Finance, etc.).
* Maintain a question bank of reusable items and copy them into quizzes (not a live join; distinct from templates and from the drag-drop shared answer bank question type).
* Search templates by experience and tags, then assign to a candidate.
* Run the assignment as live (proctored / screen-share style) or async (timed link).
* Track attempts, auto-score where possible, and review written answers with per-question scoring modes.

Goals (v1)

1. Replace ad-hoc Forms packs with searchable, maintainable quizzes tied to openings.
2. Let Recruitment assign the right quiz without owning deep technical content.
3. Let specialist teams author and maintain templates **and a question bank of reusable items**, with richer question types.
4. Support both live and async delivery from day one using one quiz model.
5. Use AI only as a draft assistant—humans always edit before assign.
6. Avoid rigid org hierarchy: use tags / key–value fields and saved filters instead.

Non-goals (v1)

* Full coding IDE / online judge (no code question type in v1).
* Fully automatic assign with no human edit of AI output.
* Replacing the whole ATS / hiring system.
* Advanced proctoring hardware beyond practical live/async controls.
* Candidate career portal or public self-serve job applications.