# Slice 5 MVP boundary

What slice 5 executes. Live run shipped afterwards in [ADR 0010](adr/0010-live-quiz-sessions.md). The rows below describe the slice 5 boundary; the live rows note what slice 6 changed.

## Async execute only

Slice 5 runs **async** assignments. A recruiter creates an assignment, copies an invite URL, and the candidate opens `/attempt?token=`, answers, and submits. Auto-scored items produce totals. The recruiter reads those totals, then opens the attempt for each question’s stem, the candidate’s answer, and points. That screen does not include answer keys.

There is no live start, pause, or monitor. There is no human or AI review, no Entra login, and no AI authoring drafts. Invite email is implemented behind `IInviteMailer` (Development sink or SMTP). See [ADR 0009](adr/0009-invite-expiry-email-and-template-assign.md).

## Easy to misread

| What you can see | What it means in slice 5 |
|------------------|--------------------------|
| Live on the create form | Slice 5 left the choice disabled. Slice 6 enables it, requires a duration (1–480), and runs the session from the assignment. Create still omits `inviteUrl`. |
| SMTP / email | Async issue and resend send mail. Development defaults to a file sink. Mailpit or real SMTP is configuration (`InviteMail__Mode`). Tests do not open SMTP. The recruiter can still copy `inviteUrl`. Ticketing is out of scope. |
| Result `pendingReview` | At least one item is unsettled (`aiAssist` or `humanOnly`). Auto totals are not a final decision. A reviewer with `attempts.review` scores those items. An `aiAssist` item can also store an auditable suggestion draft; confirming it still uses the review score. |
| `sessions.live.run` | Seeded on Dev Recruiter. **Active in slice 6** for start, pause, resume, and end. |
| `attempts.review` | Seeded on Dev Reviewer. **Active** for human scoring. It does not call an LLM. |

## Invite and session

The invite link works until it expires, until the recruiter revokes it, or until the recruiter issues a new link. Issuing a new link stops the previous one. Revoke and extend do not issue a replacement token. The link is not burned on first open, and opening it does not start the timer. After submit, the same link still opens the submitted summary when it is still active; it does not start another attempt. A new invite cannot be issued once the assignment is submitted. An expired or revoked link shows a clear page and cannot be consumed. Expiry does not cut off a candidate who already started: they may finish. Revoke does: the in-progress attempt is submitted and the candidate sees that the quiz has ended. Protocol: [ADR 0008](adr/0008-magic-link-reusable-until-submit.md) and [ADR 0009](adr/0009-invite-expiry-email-and-template-assign.md).

The candidate access token is a JWT (`Jwt__CandidateAccessTokenMinutes`, default 60 minutes). There is no candidate refresh token. If that session expires before submit, the candidate opens the **same** invite link again and receives a new access token. Quiz time is the attempt `dueAtUtc`, not the JWT lifetime. Protocol: [ADR 0008](adr/0008-magic-link-reusable-until-submit.md).

## Results

Employee results use `attempts.read` only. They are auto points, and a final score once every item is settled. Unsettled items are **awaiting human review**, not a fail and not zero points. Answer keys are not on that screen. `pendingReview` means a reviewer still has unsettled `humanOnly` or `aiAssist` items. `completed` means every item is settled, either because they were all auto-scored or because review finished.

`sessions.live.run` and `attempts.review` are catalog codes. Both now have a workflow. Operator note: [permissions.md](permissions.md).
