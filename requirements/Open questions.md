Open questions

Still open

* Candidate / employee identity (exact routing)
  * Working direction: store users in the app database first (Entra ID seat limits). Support magic-link for candidates. Employees use Microsoft / third-party IdP when ready.
  * Still to nail: one branded auth entry that routes by account type (employee SSO vs candidate magic link / password) — not identical flows for both.
* Retention restore details
  * Locked direction: default 5 years (configurable); after retention, archive (do not hard-delete / lose data).
  * Still to nail: whether resumes, attempts, and quiz content share one policy; who can restore from archive.
* Ordering partial-credit formula
  * Locked: same credit modes as multi-select (Partial credit | All-or-nothing), per question, with company default.
  * Still to nail: exact partial formula for sequences (recommended default: adjacent-pair scoring).
* Company AI rule schema (full shape)
  * Locked direction: required fields per question type for consistent system prompts, plus a global layer (topics, difficulty, banned content, language).
  * Still to nail: concrete schema / versioning details.

Decided (moved into Product Brief)

* AI-assist scores are stored as auditable drafts (suggestion + confirmer + timestamps).
* ATS / HRIS integration: not required for v1; long-term management vision only.
* Multi-select (and ordering) credit modes: Partial credit and All-or-nothing; author chooses per question; company default.
* Drag interactions modeled as two question types: Shared answer bank, and Per-slot options.
* Distractor count: decided by the question author.
* Mobile drag-and-drop: aim for v1; use tap-to-place fallback if native drag is too costly; only defer full mobile DnD to v2 if still insufficient.