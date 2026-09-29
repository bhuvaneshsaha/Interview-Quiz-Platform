# ADR 0005 — No CQRS (and never MediatR)

## Context

The CQRS skill applies when reads and writes genuinely diverge (different models, permissions, or scale) or the team wants explicit command/query types. It forbids MediatR and similar mediators. This product has CRUD-style modules plus list/filter endpoints. Saved filters store criteria JSON; the owning module still runs the query. Assignment snapshots are a write-time copy, not a separate read store.

v1 scale and team size do not show a read/write split that would pay for a dispatcher layer.

## Decision

**Do not use CQRS** in v1.

- Each module exposes **application services** (or equivalent use-case classes) called from API endpoints.
- No `ICommand` / `IQuery` dispatcher, no MediatR, Wolverine, MassTransit mediator, or pipeline DSL.
- No event sourcing, domain-event bus, or server outbox.
- List/search stays SQL/EF queries in Openings and Catalog, optionally parameterized by Search’s saved criteria.

Revisit only if a measured split appears (for example a reporting database). That would be a new ADR — still without MediatR if CQRS were introduced.

## Consequences

- .NET keeps a straightforward path: controller → application service → EF. Newcomers find a use case by service name.
- Testing targets services and HTTP; no mediator unit-test tax.
- Permission checks stay on the endpoint/service, not on a generic `Handle`.
