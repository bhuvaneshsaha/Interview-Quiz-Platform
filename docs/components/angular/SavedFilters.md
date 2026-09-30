---
name: SavedFilters
framework: angular
status: stable
source: src/interview-quiz-web/src/app/shared/saved-filters/saved-filters.component.ts
export: app-saved-filters
---

# SavedFilters

## Purpose
Reusable control for list screens that apply, save, share, and delete Search saved filters for a single target (`openings`, `quizzes`, or `templates`).

## When to use
- Opening, quiz, and template list screens that already collect list criteria
- Any future list whose API criteria match a Search `FilterTarget`

## When not to use
- A standalone “filter admin” module — keep this on the list page
- Inline field validation or PageStatus for the parent list’s HTTP load

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `target` | `'openings' \| 'quizzes' \| 'templates'` | — | yes | Search filter target; used on `GET /api/filters` and save |
| `criteria` | `ListCriteria` | `{}` | no | Current list criteria persisted when the user saves |

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|
| `apply` | `ListCriteria` | Mapped criteria from the selected visible filter |

### Content / slots
None

### Configuration
Standalone. Injects `FiltersApi`, `AccessApi`, `AuthService`, and `PermissionService`. Actions use permission codes (`filters.write`, `filters.share`, `users.manage` for the optional user picker) — never role names.

## Variants and states
- Loading saved filters: status sentence while `GET /api/filters` is in flight
- Error: alert with optional development correlation id
- Apply-only: no save/share/delete without `filters.write` / `filters.share`
- Owned filter: share and delete when the signed-in user is `ownerUserId`

## Usage

```html
<app-saved-filters
  [target]="'quizzes'"
  [criteria]="criteriaFromForm()"
  (apply)="applySaved($event)"
/>
```

```ts
applySaved(raw: ListCriteria): void {
  const criteria = quizCriteriaFromUnknown(raw);
  // patch list fields, then reload
}
```

Save is gated with `*hasPermission="'filters.write'"`. Share uses `filters.share`. Recruiter accounts without `users.manage` paste user ids into a labeled text field.

## Accessibility
Section heading is `h2` (the list page keeps a single `h1`). Inputs have labels. Errors use `role="alert"`; success uses `role="status"`. Delete is a two-step confirm, not `window.prompt`.

## Dependencies
`FiltersApi`, `AccessApi` (only called with `users.manage`), `AuthService.currentUser`, `HasPermission`, `PageStatus.fromError`. Design tokens from `src/styles.css`.

## Do
- Pass compact criteria that match the target’s list DTO
- Hide clone/publish elsewhere with the same permission codes the API uses

## Don’t
- Check role names such as Recruiter or Author
- Use `window.prompt` for the filter name

## Common mistakes
- Binding `*hasPermission` with an array for clone (that is any-of). Clone needs both `quizzes.write` and `templates.read` in the feature component.
- Saving extra JSON keys — Search rejects unmapped criteria fields.

## Related
- [PageStatus](PageStatus.md)
- [HasPermission](HasPermission.md)
- Storybook: `Shared/SavedFilters` (`npm run storybook` in `src/interview-quiz-web`, port 6006)

## Source of truth
`SavedFilters` in `saved-filters.component.ts` (`target`, `criteria` inputs; `apply` output). Mapping helpers: `saved-filter.criteria.ts`.
