# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/bhuvaneshsaha/Interview-Quiz-Platform/releases/tag/v0.1.0
