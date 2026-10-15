# ADR 0010 — Live quiz sessions

Status: **Implemented**. Slice 6. Hosting, JWT, and the Angular client are unchanged from ADRs 0001–0009.

## Context

An interviewer runs a call outside this product and needs to start, pause, and end the assigned quiz while watching progress. The assignment, snapshot, attempt, scoring, and results model stay the same as async. Only who controls the clock changes.

## Decision

1. **REST polling.** The host polls `GET /api/assignments/{id}/live/progress` every 3 seconds. The candidate polls every 2 seconds so a pause is visible within 3 seconds. Errors back off from 1 second to 30 seconds with jitter and show "Reconnecting…" after two failures. No SignalR in v1.
2. **The host sees progress only.** Counts, question index, flagged count, and remaining time. Answer content stays on the results screen after submit.
3. **Start requires a lobby join.** The candidate must have consumed the link and been seen in the last 30 seconds. Otherwise start is 409 `Candidate has not joined.`
4. **Pauses freeze the server clock.** `DueAtUtc` moves forward by the paused seconds on resume. One pause is capped at 15 minutes and the total at 30 minutes. Past either cap, the next read auto-submits with `endReason=pauseLimit`.
5. **Mark for review is stored** on `attempt_answers.flagged`. The host sees the flagged count.
6. **Live duration is required**, 1–480 minutes. A legacy null duration cannot start (`Set a duration before starting.`).
7. **Only `assignments.write` issues the join link.** `sessions.live.run` starts, pauses, resumes, and ends. `attempts.read` can read progress.
8. **One candidate per assignment.**
9. **An expired candidate JWT re-consumes the same magic link** and continues. There is no candidate refresh token.

Host actions are appended to `delivery.live_session_events`. Candidate `POST /attempts` stays 400 (`Waiting for the host to start.`). The host start creates the attempt.

## Consequences

- Magic-link consume and invite issue work for live assignments. Rotate and revoke are unchanged.
- The candidate lobby has no Start quiz button. Refresh during a running or paused session returns to the questions.
- While paused, answer and flag writes are 409 `Session is paused.`
