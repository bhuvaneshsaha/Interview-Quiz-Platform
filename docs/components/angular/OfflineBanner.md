---
name: OfflineBanner
framework: angular
status: stable
source: src/interview-quiz-web/src/app/core/pwa/offline-banner/offline-banner.component.ts
export: app-offline-banner
---

# OfflineBanner

## Purpose
Tells the user they are offline. Slice 1 is an installable, online-first PWA with no mutation outbox.

## When to use
- Once in the authenticated app shell
- Any full-page employee surface that cannot work without the API

## When not to use
- As a substitute for disabling individual buttons while offline (the banner is the product rule for v1)
- Candidate attempt timing UI in later slices still must not queue answers offline

## Public API

### Inputs / props
None. Reads `OnlineStatus.online`.

### Outputs / events
None

### Content / slots
None

### Configuration
Standalone. Requires `OnlineStatus` (`providedIn: 'root'`).

## Variants and states
Visible only when `navigator.onLine` is false (and `offline` window events).

## Usage

```html
<app-offline-banner />
```

Do not gate this on a role name. Offline affects every signed-in employee.

## Accessibility
Uses `role="status"` so assistive tech hears the message without stealing focus.

## Dependencies
`OnlineStatus` (`window` `online`/`offline` events).

## Do
- Keep the copy explicit: openings and authoring need a network

## Don’t
- Cache `/api` to hide this banner

## Common mistakes
- Showing a queued-outbox indicator — v1 has no outbox.

## Related
- [PageStatus](PageStatus.md)

## Source of truth
`OfflineBanner` template in `offline-banner.component.html`.
