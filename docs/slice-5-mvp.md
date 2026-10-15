# Slice 5 MVP boundary

What this branch executes. Later slices are named so a stored live row or a seeded permission is not treated as a working feature.

## Async execute only

Slice 5 runs **async** assignments. A recruiter creates an assignment, copies an invite URL, and the candidate opens `/attempt?token=`, answers, and submits. Auto-scored items produce totals. The recruiter reads those totals, then opens the attempt for each question’s stem, the candidate’s answer, and points. That screen does not include answer keys.

There is no live start, pause, or monitor. There is no human or AI review, no Entra login, and no AI authoring drafts. Invite email is implemented behind `IInviteMailer` (Development sink or SMTP). See [ADR 0009](adr/0009-invite-expiry-email-and-template-assign.md).

## Easy to misread

| What you can see | What it means in slice 5 |
|------------------|--------------------------|
| Live on the create form | The choice is disabled (Coming soon). A live row can still exist from the API, but it has no invite and the candidate cannot start. Live run is slice 6. |
| SMTP / email | Async issue and resend send mail. Development defaults to a file sink. Mailpit or real SMTP is configuration (`InviteMail__Mode`). Tests do not open SMTP. The recruiter can still copy `inviteUrl`. |
| Result `pendingReview` | At least one item is unsettled (`aiAssist` or `humanOnly`). Auto totals are not a final decision. Human or AI-assist review is slice 7. |
| `sessions.live.run` | Seeded on Dev Recruiter. **Inactive until slice 6.** |
| `attempts.review` | Seeded on Dev Reviewer. **Inactive until slice 7.** |

## Invite and session

The invite link works until it expires, until the recruiter revokes it, or until the recruiter issues a new link. Issuing a new link stops the previous one. Revoke and extend do not issue a replacement token. The link is not burned on first open, and opening it does not start the timer. After submit, the same link still opens the submitted summary when it is still active; it does not start another attempt. A new invite cannot be issued once the assignment is submitted. An expired or revoked link shows a clear page and cannot be consumed. Protocol: [ADR 0008](adr/0008-magic-link-reusable-until-submit.md) and [ADR 0009](adr/0009-invite-expiry-email-and-template-assign.md).

The candidate access token is a JWT (`Jwt__CandidateAccessTokenMinutes`, default 60 minutes). There is no candidate refresh token. If that session expires before submit, the candidate opens the **same** invite link again and receives a new access token. Quiz time is the attempt `dueAtUtc`, not the JWT lifetime. Protocol: [ADR 0008](adr/0008-magic-link-reusable-until-submit.md).

## Results

Employee results use `attempts.read` only. They are auto points plus per-question status. Unsettled items are **awaiting human review**, not a fail and not zero points. `completed` means every item auto-scored. `pendingReview` means slice 7 still has to review.

`sessions.live.run` and `attempts.review` stay in the catalog and in Development seeds so later slices do not invent new codes. Granting them now does not unlock a workflow. Operator note: [permissions.md](permissions.md).
