# Component catalog

Reusable UI primitives for the Angular SPA. Feature screens (login, openings, quizzes, templates, questions, assignments, candidate `/attempt`, role editor) are not listed here — those live under `src/interview-quiz-web/src/app/features/`. Slice 5 added no new shared primitive (assignment list reuses PageStatus / HasPermission; no SavedFilters).

**Playbook markdown is the source of API tables** (inputs, outputs, usage). Angular owns those pages; do not invent props here. OSS Storybook is the isolated gallery for the same primitives, not a second API contract.

| Name | Framework | Status | Purpose | Page |
|------|-----------|--------|---------|------|
| PageStatus | angular | stable | Loading / empty / error sentences for API pages | [angular/PageStatus.md](angular/PageStatus.md) |
| OfflineBanner | angular | stable | Online-first PWA “you are offline” banner | [angular/OfflineBanner.md](angular/OfflineBanner.md) |
| HasPermission | angular | stable | Show/hide by permission code | [angular/HasPermission.md](angular/HasPermission.md) |
| SavedFilters | angular | stable | Apply / save / share / delete list saved filters | [angular/SavedFilters.md](angular/SavedFilters.md) |

## Storybook (OSS gallery)

Storybook 10 + `@storybook/angular-vite` (Angular 21 / zoneless / application builder). Stories live next to the source (`*.stories.ts`): `Shared/PageStatus`, `Shared/OfflineBanner`, `Shared/HasPermission`, `Shared/SavedFilters`. Chromatic is not used.

From `src/interview-quiz-web`:

```bash
npm run storybook
```

Opens **http://localhost:6006**. Static build: `npm run build-storybook` (output `storybook-static/`). Details: `src/interview-quiz-web/README.md`.
