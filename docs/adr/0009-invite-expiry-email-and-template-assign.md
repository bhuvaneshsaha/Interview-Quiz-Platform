# ADR 0009 — Invite expiry, email delivery, and assign-from-template

Status: **Implemented**. Amends [ADR 0008](0008-magic-link-reusable-until-submit.md) decision 2 and closes architecture §11 “Recruiter instantiate-from-template”.

## Context

ADR 0008 made the opaque invite reusable until the candidate submits, and until a recruiter rotates it. It also said there is no SMTP: the recruiter copies `inviteUrl`. Product follow-up needs three things that ADR 0008 left out:

1. Invites expire, and a recruiter can revoke one without minting a replacement.
2. The candidate is emailed the link, with a record of sent, failed, and resend.
3. A recruiter can assign from a template without `quizzes.write`.

## Decision

### Expiry and revoke versus reusable-until-submit

1. **Reusable until submit still holds for an invite that is active.** Consume does not burn the token. After submit, consume of an **active** invite still returns a candidate JWT so `/attempt?token=` can show the submitted summary. Live mode uses the same consume path ([ADR 0010](0010-live-quiz-sessions.md)). A new invite still cannot be issued once the assignment is submitted.

2. **Expiry and revoke are extra stops. They do not rotate.**
   - `ExpiresAt` is optional on `access.magic_link_invites`.
   - When the recruiter omits `expiresAt`, `Invite:DefaultLifetimeHours` applies (default 168). `0` means no time limit.
   - A past `expiresAt` is rejected at issue and extend time.
   - `POST /api/assignments/{id}/invite/revoke` sets `RevokedAt` on the current row and does not insert a new token.
   - `POST /api/assignments/{id}/invite/extend` updates `ExpiresAt` on the current row. `null` clears the time limit. The raw token stays the same.
   - `POST /api/assignments/{id}/invite` still rotates: the previous row is revoked and a new token is issued.

3. **Consume of an expired token returns** `This invite has expired.` **A revoked token returns** `This invite has been revoked.` Unknown tokens stay `Invite is not valid.` The Angular `/attempt` page shows a dedicated explanation and does not start the quiz.

4. **Expiry does not end an in-progress attempt.** A candidate JWT already issued stays usable for save and submit after `expiresAt` passes. Quiz timing stays on the attempt `dueAtUtc`. Opening the link again re-consumes and shows the expiry page, so a fresh visit cannot start, but the candidate who already started may finish.

   **Revoke does end an in-progress attempt.** `POST /api/assignments/{id}/invite/revoke` still does not mint a replacement. It also submits the in-progress attempt from the answers saved so far. Later candidate calls (`preview`, `current`, save, submit, start) return `This invite has been revoked.` The Angular page clears the questions and says the quiz has ended. A retry of revoke, after the link is already revoked and the assignment is still invitable, still closes an in-progress attempt.

### Email

5. **`IInviteMailer` is the only send path.** `FileSinkInviteMailer` is the Development default (writes `.eml` under `App_Data/invite-mail`, or `InviteMail:SinkDirectory`). `SmtpInviteMailer` uses `InviteMail:Smtp` (`System.Net.Mail`). `deploy/local/compose.yaml` includes Mailpit on SMTP `1025` and UI `8025` for when `InviteMail:Mode` is `Smtp`. The Testing environment registers `RecordingInviteMailer` and never opens a socket.

6. **The raw token is not logged.** Logs carry assignment id, outcome, and sink file name. Stored delivery errors are passed through `InviteSecretRedactor`. The email file or SMTP body contains the URL because that is the message.

7. **Resend does not rotate.** Access stores a data-protected copy of the current token beside the hash. `POST .../invite/resend` unprotects it and emails the same URL. Revoke clears the protected value. Links created before this column existed must be re-issued before they can be emailed again. GET assignment still never returns `inviteUrl`.

8. **Delivery records the latest email attempt** on the assignment: `inviteEmailStatus` `none` | `sent` | `failed` | `resent`, plus attempt count, last attempt time, and last error. The first success is `sent`. A later success is `resent`. A failure is `failed` even if an earlier attempt succeeded. The assignment and invite still succeed when email fails, so the recruiter can copy the URL.

### Assign from a template

9. **Recruiters assign from a template without `quizzes.write`.** `POST /api/assignments/from-template` requires `assignments.write` and `templates.read`. The host clones the chosen template version into the opening (latest version when omitted), then creates the assignment. The snapshot is the normal freeze of that new quiz (new question ids from the clone, preserved on the snapshot). The authoring clone route remains `quizzes.write` plus `templates.read`.

10. **Dev Recruiter is not granted `quizzes.write`.** The seed already has `templates.read` and `assignments.write`, which is enough for from-template.

## Consequences

- Operators set `Invite__DefaultLifetimeHours` and `InviteMail__*` . Production SMTP passwords stay in the environment, not in git.
- Data Protection keys are stored in PostgreSQL (`access.data_protection_keys`, application name `InterviewQuiz`). Resend of an existing link works after the API process restarts or a new instance starts against the same database. Losing that table means those links must be re-issued. There is no separate key-ring path to configure.
- Ticketing is out of scope. Invite delivery is email through `IInviteMailer` only.
- Clone-then-assign uses two module saves. If assignment create fails after the quiz is saved, the cloned quiz remains in the opening.
- Angular assignment create can start from an existing quiz or a template, and the candidate expiry page is specific to expired and revoked links.
