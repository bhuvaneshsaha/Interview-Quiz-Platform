# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.8.0] - 2026-10-16

### Changed

- A page you cannot open explains that on Home. Opening counts link to assignments once, quiz and template pages link to the related item instead of showing an id, and the assignment list picks a quiz by name. Saved filters say “these filters” and let a recruiter pick a person. An empty question bank or template list offers Clear filters, and View and Review stay separate (#78, #79, #81, #84, #85, #91, #92, #93).
- Staff sign-in, home, and navigation use the same plain names (Opening fields, Roles) and no longer mention a candidate URL. Question and quiz editors say question text, choices, wrong answer, and how a question is scored. Assignment and attempt pages say version instead of snapshot or revision, and a live join link no longer claims it is emailed. Reports describe results in everyday language (#75, #76, #77, #80, #87, #88, #90).

### Fixed

- Replacing an invite that is already active or expired asks first, and the button says Re-issue. Archiving a question, disabling a user, and deleting a role ask first too (#73, #82).
- Create opening, create user, and create role say what is missing instead of staying silent. An AI draft with no opening says to select one, and a sample draft no longer shows the word fake (#74, #83, #86).

## [0.7.0] - 2026-10-16

### Changed

- Every employee role can view Reporting. The built-in Admin role and the Development seeds for Template author, Recruiter, and Reviewer include `reports.read`, and a migration grants that code to employee roles already stored. A role that holds `candidate.attempt.participate` is left unchanged. Candidates still receive 403 from the reports API, and the app hides the Reporting link and sends `/reports` home. `GET /api/permissions` lists `reports.read` as “View reports” in the Evaluation group, and the role editor shows that checkbox so an admin can assign or remove it (#61).
- Admin is a built-in role in every environment, with a stable id. It starts with every employee permission, those permissions can be edited, and it cannot be deleted or used to assign or revoke Super Admin. It never has `candidate.attempt.participate`. Existing Dev Admin holders map onto this role, so there is one Admin role. Super Admin stays the locked system role: it holds every employee permission, only a Super Admin can assign or revoke it, and the last active holder is protected. The role editor shows a System or Built-in badge and each role’s description (#62).

## [0.6.2] - 2026-10-16

### Fixed

- A full page load of a guarded route waits for one shared session restore before the auth or permission guard decides. The access token stays in memory, so a reload used to check permissions while that set was still empty, and routes that run both guards together (quiz and template preview) sent the user Home. `/reports` now stays put for someone who has `reports.read`, including a Super Admin, and a user without that code still lands on Home. A bootstrapped Super Admin access token and `GET /api/me` include `reports.read` even when that person already had an account; startup sync writes the code back onto the Super Admin role when an existing database is missing the row.

## [0.6.1] - 2026-10-16

### Fixed

- Opening Owner and Handlers pickers list only active employees. `GET /api/users/assignable` excludes candidate identities created when a magic link is consumed, users with no employee role, users who cannot sign in with a password, and disabled users. Creating or updating an opening with one of those ids returns 400 and names Owner or Handlers.

## [0.6.0] - 2026-10-16

### Added

- The candidate attempt shows the question type as a muted badge and keeps Answered N of M while the quiz is in progress. Shared-bank and per-slot questions say “Match each slot” and still use a dropdown for each slot (#12).
- A quiz can shuffle questions, and can shuffle multiple-choice options. Both settings default off and are copied into the assignment snapshot. Each attempt stores a seed and the resolved order, so refresh, resume, and a live session keep that order. Scoring, review, and recruiter detail stay in the saved order. Ordering items are not shuffled again. Cloning a template starts with both settings off (#46).
- Recruiter attempt detail marks each question Correct (light green), Wrong (light red), Partly correct (light amber), or Pending review (neutral card, amber icon). Each mark is an icon plus a text label. Candidate pages do not show those labels (#47).

## [0.5.0] - 2026-10-16

### Added

- Authors with `ai.draft.use` can generate a quiz draft from an opening, an optional pasted resume, optional rules text, and an optional template version (`POST /api/ai-drafts`). Saving it (`POST /api/ai-drafts/{id}/accept`) also requires `quizzes.write` and a change to the questions. The result is a quiz on that opening. Nothing is assigned. The same `Ai:Provider` switch as review suggestions is used, including the deterministic fake. Company rule-set CRUD is not included (#26).

## [0.4.0] - 2026-10-16

### Added

- Reviewers with `attempts.review` can request an auditable AI suggestion for an `aiAssist` item (`POST /api/attempts/{id}/items/{itemId}/suggestion`). The draft stores the suggested points, feedback, actor, model, and time, and it stays after the reviewer confirms or edits the score. Development and tests use a deterministic fake. An OpenAI-compatible endpoint is used only when `Ai__BaseUrl`, `Ai__Model`, and `Ai__ApiKey` are set; otherwise suggestions are off and human scoring still works. No API key is stored in git (#22).

## [0.3.0] - 2026-10-15

### Added

- Reviewers with `attempts.review` score `humanOnly` and `aiAssist` items on `GET/POST /api/attempts/{id}/review`. The last unsettled item moves the assignment from `pendingReview` to `completed` and stores a final score. Answer keys stay on the review payload. Reporting pass/fail uses that final score when it exists, and the auto-score ratio until then. AI suggestion drafts are not included (#21).

## [0.2.0] - 2026-10-15

### Added

- Reporting v1 (`reports.read`): company-wide funnel, score distribution and 70% auto-score pass rate, time taken, per-question difficulty and discrimination, candidate comparison, and pending-review age. Chip filters and one saved-filter target per report. CSV export is UTF-8, has no answer keys, and stops at 10,000 rows. Dev Recruiter and Super Admin can open it; Dev Reviewer cannot (#51).

## [0.1.1] - 2026-10-15

### Fixed

- Shared-bank and per-slot questions on the candidate attempt page and in candidate preview use dropdowns again. Those screens no longer show drag handles or drop zones for slots. Ordering questions still use drag-and-drop, with Move up and Move down (#48).
- Submitted answers are unchanged: shared-bank slots stay `{ slotId, itemId }`, per-slot answers stay `{ slotId, optionId }`, and ordering stays `{ itemIds }`. Scoring is unchanged.
- Choosing the same shared-bank item in a second dropdown no longer clears it from the first slot. Each dropdown writes its own slot.
- The quiz editor, question editor, and template editor keep their slot fields. Those fields are how authors define the question.

## [0.1.0] - 2026-10-15

### Added

- Candidates see a welcome page before the timer starts. Only Start quiz creates the attempt and sets `dueAtUtc`. The candidate payload omits per-question points, scoring mode, and credit mode. Ordering questions use drag-and-drop with Move up and Move down. Recruiter attempt detail shows snapshot text. Playwright covers these flows (#40).
- Quizzes stay editable after assignment. Each snapshot keeps the quiz version it was copied from, and save asks for confirmation when assignments still use an earlier snapshot. Assigned copies stay frozen. The attempt shows one question at a time, with Previous/Next, a numbered sidebar, mark for review, and a submit confirmation. Recruiters can preview a saved quiz, an unsaved draft, or a template version without creating an assignment, attempt, or invite. Opening Owner and Handlers are user ids, and `GET /api/users/assignable` searches staff who can read openings (#41).
- A system Super Admin role holds every permission except `candidate.attempt.participate` and cannot be renamed, deleted, or reduced. The first holder comes from `Bootstrap__SuperAdminEmail`. Opening detail lists quizzes and Assigned / Appeared / Submitted counts. Template metadata and content saves create version N+1 and leave earlier versions, clones, and snapshots unchanged. Assignment statuses are `notStarted`, `inProgress`, `pendingReview`, and `completed`. Ordering uses positional adjacent-pair scoring. Recruiter lists use a card layout at 375px (#42).
- Invites expire (`Invite:DefaultLifetimeHours`, default 168 hours). Recruiters can revoke, extend, resend the same link, or re-issue a new one. Invite email goes through `IInviteMailer` (Development file sink, optional Mailpit or SMTP). `POST /api/assignments/from-template` clones a template version into the opening and snapshots that quiz (#43).
- A template metadata edit creates version N+1 that reuses the previous version’s question rows. Content edits still copy questions. The template editor edits shared-bank and per-slot questions with the same controls as the quiz editor, and version history shows the author’s display name. ASP.NET Data Protection keys are stored in PostgreSQL so invite resend survives a process restart (#44).
- Hosts start, pause, resume, and end a live assignment over REST. The candidate waits in a lobby until the host starts. Pause freezes remaining time on the server (15 minutes per pause, 30 minutes total). Host and candidate poll for progress. Start waits until the candidate has joined the lobby in the last 30 seconds. Every host action is audited (#49).
- Windows PowerShell local quick start for Compose, the API, Angular, integration tests, and Playwright (#38).
- Break-glass Super Admin steps in `docs/permissions.md`, and adjacent-pair scoring described for open questions and the quiz and template editors (#42).
- Data Protection key storage documented for Development and production, including the Windows quick start (#44).

### Fixed

- A second attempt start returns the same attempt. The clock is truncated to microseconds so `startedAtUtc` and `dueAtUtc` match PostgreSQL `timestamptz` (#40).
- Opening create and update reject unknown Owner and Handler user ids. Unmatched legacy email text is left in place (#41).
- An expired invite returns “This invite has expired.” and a revoked invite returns “This invite has been revoked.” The attempt page shows that state and does not start the quiz (#43).
- Revoke ends an in-progress attempt. A JWT already issued can still save and submit after simple expiry until submit or `dueAtUtc` (#44).

[Unreleased]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.8.0...HEAD
[0.8.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.6.2...v0.7.0
[0.6.2]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.6.1...v0.6.2
[0.6.1]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/releases/tag/v0.1.0
