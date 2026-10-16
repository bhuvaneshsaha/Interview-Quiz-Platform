# UX review — 16 October 2026

Walked the Development seed in a browser with Playwright (desktop 1280×900 and phone 375×812). API on `http://localhost:5147`, SPA on `http://127.0.0.1:4200`. Accounts: recruiter, author, reviewer, and admin. Created an opening, a bank question, a quiz, an async assignment (submitted), and a live assignment (host started after the candidate joined the lobby).

Screenshots live next to this file in `docs/ux-review-2026-10/`.

## Counts

| Severity | New findings | Already tracked |
|----------|----------------|-----------------|
| High | 2 | 0 |
| Medium | 17 | 8 |
| Low | 2 | 0 |

New issues: #73–#93. Not fixed in this pass: #89 (the users API does not say who is a candidate, and Access role seeding is in flight).

## How this was judged

QA: something fails, lies, or does a different thing from the label. First-time reader: a hiring manager or interviewer who does not know GUIDs, snapshots, stems, or permission codes.

## Already tracked (no new issue)

| Screen | What we saw | Issue |
|--------|-------------|-------|
| Quiz preview | No way back to the editor. Esc does nothing. | #64 |
| Candidate score | After submit: "Auto-scored 2 of 13 automatic points… Result: Incomplete." | #70 |
| Pending review | Status with no explanation. Review is a text link, not a next step. | #71 |
| Reviewer and Reporting | Reviewer is sent Home from `/reports`. | #61 |
| Admin vs Super Admin | Role list has no built-in Admin description. | #62 |
| Filters | One tag key/value; the filter block is always open; clearing a field was not re-tested here. | #72, #67, #66 |
| Create quiz | Include-from-bank is a known gap on the unsaved create page. | #68 |
| Help | No info icons on scoring, shuffle, or live vs async. | #69 |
| Slot questions | Candidate question 6 was dropdowns only ("Match each slot"). The dual drag-and-dropdown bug is already filed. | #65, #45 |

## High

### 1. An active invite is replaced by "Issue invite link" — #73

- **Screen:** Assignment detail after create, once the email is already sent.
- **Persona:** First-time recruiter and QA.
- **What happened:** Invite is Active and the email is Sent, but the primary button says **Issue invite link**. The page does not keep the URL, so the click skips the re-issue confirmation and rotates the token. The link in the email stops working.
- **Screenshot:** [31-assignment-detail.png](ux-review-2026-10/31-assignment-detail.png)
- **Fix:** If an invite is already active or expired, label the button **Re-issue invite link** and always confirm before rotating it.

### 2. Saving an opening with missing fields shows nothing — #74

- **Screen:** Create opening.
- **Persona:** First-time hiring manager and QA.
- **What happened:** Title, owner, and start date are required. **Save opening** with those empty does not move, and no message appears.
- **Screenshot:** [12-opening-create-invalid.png](ux-review-2026-10/12-opening-create-invalid.png)
- **Fix:** Show an error under each invalid field and focus the first one.

## Medium

### 3. Sign-in tells candidates to open `/attempt` — #75

- **Screen:** Sign in.
- **Persona:** A candidate who lands on the staff page.
- **What happened:** "Candidates use an invite link (`/attempt`), not this form." A wrong password correctly says "Invalid email or password."
- **Screenshot:** [01-login.png](ux-review-2026-10/01-login.png)
- **Fix:** Say that candidates should open the link in their email.

### 4. Home and the menu use different names — #76

- **Screen:** Home for an admin.
- **Persona:** First-time admin.
- **What happened:** The menu says **Field defaults** and **Roles**. Home says **Opening field defaults** and **Roles (permission groups)**. The empty-permission sentence says "slice 1–5 screens".
- **Screenshot:** [72-home-admin.png](ux-review-2026-10/72-home-admin.png)
- **Fix:** Use **Opening fields** and **Roles** in both places. Replace the slice sentence with a plain "ask an administrator" line.

### 5. Question editor jargon and a raw choice id — #77

- **Screen:** Create bank question (same words on the quiz and template editors).
- **Persona:** First-time author.
- **What happened:** The text is **Stem**. Matching questions are a **Bank** of **Bank item**s with a **Distractor** checkbox. The correct-choice menu shows `item-e00c0ae60df2` when the choice is blank. Ordering asks for **Correct index (0 is first)**. Types include **Drag and drop (shared bank)**. Scoring says **AI assist** and **Human only**. **Credit mode** is unexplained. Empty save does say "Stem is required."
- **Screenshots:** [54-question-invalid.png](ux-review-2026-10/54-question-invalid.png), [55-question-shared-bank-labels.png](ux-review-2026-10/55-question-shared-bank-labels.png), [55b-question-ordering-labels.png](ux-review-2026-10/55b-question-ordering-labels.png)
- **Fix:** Plain labels, "Choice 1" instead of an id, and a short note that 0 is the first place.

### 6. Quiz and template pages show raw ids — #78

- **Screen:** Quiz editor and template detail.
- **Persona:** First-time author or recruiter.
- **What happened:** "Origin template:" and "Origin quiz" are GUIDs.
- **Screenshots:** [22-quiz-readonly.png](ux-review-2026-10/22-quiz-readonly.png), [25-template-detail.png](ux-review-2026-10/25-template-detail.png)
- **Fix:** Link **View template** and **View original quiz**.

### 7. Assignment list asks for a quiz id — #79

- **Screen:** Assignments.
- **Persona:** First-time recruiter.
- **What happened:** The filter is **Quiz id**. The column says "Quiz revision 1". Keyword help says "snapshot title".
- **Screenshot:** [26-assignments-list.png](ux-review-2026-10/26-assignments-list.png)
- **Fix:** A quiz dropdown by title, and call the column **Version**.

### 8. Assignment page says snapshot and revision — #80

- **Screen:** Assignment detail (same sentence on attempt detail).
- **Persona:** First-time recruiter.
- **What happened:** **Snapshot** and "Quiz revision 1. Results are not comparable across revisions."
- **Screenshot:** [31-assignment-detail.png](ux-review-2026-10/31-assignment-detail.png)
- **Fix:** Use the quiz name and "Version 1. This candidate keeps this version if the quiz is edited later."

### 9. Assigned, Appeared, and Submitted open the same list — #81

- **Screen:** Opening detail.
- **Persona:** First-time recruiter and QA.
- **What happened:** Three links, one destination. **Appeared 0** and **Submitted 0** still show the assigned candidate. "Appeared" is unclear.
- **Screenshot:** [10-opening-detail.png](ux-review-2026-10/10-opening-detail.png)
- **Fix:** Show the numbers as text (Assigned, Started, Submitted) and one **View assignments** link.

### 10. Archive, disable, and delete role do not ask — #82

- **Screen:** Bank question, user, role editor.
- **Persona:** First-time author or admin, and QA.
- **What happened:** **Archive** archives immediately. Disable user and Delete role are one click. Revoke invite already confirms.
- **Screenshot:** [56b-archive-no-confirm.png](ux-review-2026-10/56b-archive-no-confirm.png)
- **Fix:** The same confirm dialog as revoke.

### 11. Create user and create role fail quietly — #83

- **Screen:** `/users/new`, `/roles/new`.
- **Persona:** First-time admin and QA.
- **What happened:** Empty user, bad email, short password, and empty role name all stay on the page with no message.
- **Screenshots:** [75-user-invalid.png](ux-review-2026-10/75-user-invalid.png), [79-role-invalid.png](ux-review-2026-10/79-role-invalid.png)
- **Fix:** Field errors: valid email, at least 8 characters, name required.

### 12. Saved filters say "criteria" and ask for user ids — #84

- **Screen:** Saved filters on openings (same block on quizzes, templates, and reports).
- **Persona:** First-time recruiter.
- **What happened:** "Save the criteria currently on this list." Specific people asks for comma-separated user ids and says recruiters typically paste them.
- **Screenshot:** [18-openings-saved-filter.png](ux-review-2026-10/18-openings-saved-filter.png)
- **Fix:** Say "these filters". Pick people by name or email.

### 13. Empty filters do not say what to do next — #85

- **Screen:** Question bank and templates after a filter with no rows.
- **Persona:** First-time author.
- **What happened:** "No questions match this filter." and "No templates match this filter." No clear action. Templates never say a template is published from a quiz.
- **Screenshots:** [52-question-empty.png](ux-review-2026-10/52-question-empty.png), [66-templates-empty.png](ux-review-2026-10/66-templates-empty.png)
- **Fix:** **Clear filters**, and on templates one line about publishing a quiz.

### 14. AI draft generate is silent, then says "Model fake" — #86

- **Screen:** AI draft.
- **Persona:** First-time author.
- **What happened:** **Generate draft** with no opening does nothing. A successful Development draft says "Model fake."
- **Screenshots:** [64-ai-invalid.png](ux-review-2026-10/64-ai-invalid.png), [65-ai-generated.png](ux-review-2026-10/65-ai-generated.png)
- **Fix:** "Select an opening." Do not show the word fake.

### 15. Live session copy is wrong — #87

- **Screen:** Live host page and live assignment.
- **Persona:** First-time interviewer.
- **What happened:** "No, last seen not seen yet". The hint says re-issue emails the new link. Live links are not emailed.
- **Screenshots:** [43-live-host-waiting.png](ux-review-2026-10/43-live-host-waiting.png), [41-live-detail.png](ux-review-2026-10/41-live-detail.png)
- **Fix:** "Not connected yet." On live, say re-issue replaces the join link and does not send email.

### 16. Reporting is written for developers — #88

- **Screen:** Reporting hub and report pages.
- **Persona:** First-time recruiter.
- **What happened:** "Reporting v1 is company-wide for anyone with reports.read." Descriptions mention funnel mechanics, unsettled points, and discrimination. Each report says "Do not mix cohorts across quiz versions or snapshots." The tables themselves load.
- **Screenshot:** [48-reports-hub.png](ux-review-2026-10/48-reports-hub.png)
- **Fix:** Plain introductions. "Don't combine results from different quiz versions."

### 17. Users list mixes candidates with staff — #89

- **Screen:** Users.
- **Persona:** First-time admin and QA.
- **What happened:** `candidate.audit@example.com` and `live.audit@example.com` appear as Active with **Roles assigned** 0, beside employees. The column is a count, not names. The list API only returns id, email, disabled, and role ids, so the screen cannot tell a candidate from a staff member with no role.
- **Screenshot:** [73-users.png](ux-review-2026-10/73-users.png)
- **Fix:** A candidate flag on the users API, then hide them by default and show role names. Not changed here, because that flag belongs in Access while role seeding is in progress.

### 18. Question bank says Live for an available question — #90

- **Screen:** Question bank.
- **Persona:** First-time author.
- **What happened:** Status is **Live** or **Archived**. Live already means a proctored session.
- **Screenshot:** [51-question-bank.png](ux-review-2026-10/51-question-bank.png)
- **Fix:** **Available**.

### 19. A blocked page sends you Home with no explanation — #91

- **Screen:** Author opening `/assignments` (reviewer opening `/reports` does the same).
- **Persona:** First-time author.
- **What happened:** Home looks normal. Nothing says the page was blocked.
- **Screenshot:** [66b-author-no-assignments.png](ux-review-2026-10/66b-author-no-assignments.png)
- **Fix:** "You don't have access to that page."

## Low

### 20. Template version says "Showing" — #92

- **Screen:** Template detail, Questions view.
- **Persona:** First-time author.
- **What happened:** The selected action is **Showing**, jammed against **Preview as candidate**.
- **Screenshot:** [25-template-detail.png](ux-review-2026-10/25-template-detail.png)
- **Fix:** **Showing questions**, with space between the actions.

### 21. View and Review run together — #93

- **Screen:** Assignment results, reviewer.
- **Persona:** First-time reviewer. The missing Pending review next step stays on #71.
- **What happened:** The cell reads ViewReview.
- **Screenshot:** [69-reviewer-assignment.png](ux-review-2026-10/69-reviewer-assignment.png)
- **Fix:** A gap between the two links.

## What already works

Sign-in rejects a bad password in plain language. Recruiter, author, reviewer, and admin each see a different menu, and it wraps on a phone ([81-admin-mobile.png](ux-review-2026-10/81-admin-mobile.png)). Openings, quizzes, and assignments have empty states when the list itself is empty. The candidate welcome explains the timer before **Start quiz**. Submit asks for confirmation. The live lobby tells the candidate to stay on the page. Quiz and question forms (other than openings, users, roles, and AI draft) do show field errors. Saved-filter save, apply, and the share-mode names are understandable once the id field is ignored.
