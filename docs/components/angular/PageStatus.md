---
name: PageStatus
framework: angular
status: stable
source: src/interview-quiz-web/src/app/shared/page-status/page-status.component.ts
export: app-page-status
---

# PageStatus

## Purpose
Shows a single-sentence loading, empty, or error state for list and save screens, and projects the page content when none of those apply.

## When to use
- Feature lists and forms that load from the API
- Save paths that need a consistent error line (including a correlation id in development)

## When not to use
- Inline field validation (use the field’s `aria-describedby` error)
- Toast/snackbar stacks — this is a page-level status region

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `loading` | `boolean` | `false` | no | Shows the loading sentence |
| `empty` | `boolean` | `false` | no | Shows the empty sentence when not loading/error |
| `error` | `string \| null` | `null` | no | Error sentence; takes priority over empty |
| `correlationId` | `string \| null` | `null` | no | Shown after the error in development builds |
| `loadingMessage` | `string` | `Loading.` | no | Loading copy |
| `emptyMessage` | `string` | `Nothing to show yet.` | no | Empty copy |

### Outputs / events
None

### Content / slots
Default `ng-content` is shown when not loading, not empty, and no error.

### Configuration
Standalone component. No extra providers.

## Variants and states
- Loading: `loading=true`
- Error: `error` set (role=alert)
- Empty: `empty=true` and no error/loading
- Content: none of the above

## Usage

```html
<app-page-status
  [loading]="loading()"
  [error]="error()"
  [empty]="items().length === 0"
  emptyMessage="No openings match this filter."
>
  <table><!-- rows --></table>
</app-page-status>
```

Permission-gated actions stay in the projected content, for example `*hasPermission="'openings.write'"` on a create button.

## Accessibility
Error uses `role="alert"`. Loading uses `role="status"`. Callers still need a page `h1`.

## Dependencies
None beyond Angular.

## Do
- Pass one sentence for each state
- Map `ApiError` with `PageStatus.fromError`

## Don’t
- Put passwords, tokens, or response bodies in `error`

## Common mistakes
- Setting `empty=true` while `loading=true` — loading wins; still set empty only from the loaded result.

## Related
- [HasPermission](HasPermission.md)
- [OfflineBanner](OfflineBanner.md)
- Storybook: `Shared/PageStatus` (`npm run storybook` in `src/interview-quiz-web`, port 6006)

## Source of truth
`PageStatus` in `page-status.component.ts` (`loading`, `empty`, `error`, `correlationId`, `loadingMessage`, `emptyMessage` inputs).
