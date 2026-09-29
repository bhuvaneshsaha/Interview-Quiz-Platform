---
name: HasPermission
framework: angular
status: stable
source: src/interview-quiz-web/src/app/core/permissions/has-permission.directive.ts
export: "[hasPermission]"
---

# HasPermission

## Purpose
Structural directive that shows content only when the signed-in user has a permission code (or any of a list). UI hide is UX; the API still enforces the same codes.

## When to use
- Nav links and primary actions the API would 403 without the code
- Role editor is still a permission check (`roles.manage`), never `*ngIf="isAdmin"`

## When not to use
- Route protection — use `permissionGuard` with `data.permission`
- Resource-scoped rules (assignment-bound candidate JWTs) — those are API resource checks

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `hasPermission` | `string \| readonly string[]` | — | yes | Permission code, or a list (any-of) |

### Outputs / events
None

### Content / slots
Structural: the host template is created when allowed and destroyed when not.

### Configuration
Standalone directive. Reads `PermissionService`. Storybook stories provide a mock `PermissionService` with `set(codes)` so granted vs denied do not depend on login.

## Variants and states
Shown when `PermissionService.hasPermission` / `hasAny` is true for the bound code(s).

## Usage

```html
<a routerLink="/openings" *hasPermission="'openings.read'">Openings</a>
<button type="submit" *hasPermission="'openings.write'">Save opening</button>
```

In TypeScript:

```ts
permissions.hasPermission(PermissionCodes.OpeningsWrite);
```

Never bind role names such as `Admin` or `Dev Recruiter`.

## Accessibility
Hidden actions are not in the accessibility tree. Do not leave a disabled control that implies the user should request a role name.

## Dependencies
`PermissionService` (codes from `GET /api/me`).

## Do
- Use catalog codes from `docs/permissions.md`
- Keep `*hasPermission` in sync with the endpoint’s `[HasPermission]`

## Don’t
- Check `role === 'Admin'`

## Common mistakes
- Forgetting to import `HasPermission` on the standalone component — the binding is then just an unknown attribute.

## Related
- [PageStatus](PageStatus.md)
- `docs/permissions.md`
- Storybook: `Shared/HasPermission` (`npm run storybook` in `src/interview-quiz-web`, port 6006)

## Source of truth
`HasPermission.hasPermission` input in `has-permission.directive.ts`.
