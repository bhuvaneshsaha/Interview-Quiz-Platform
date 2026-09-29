# ADR 0002 — Modular Monolith

## Context

The product is enterprise-internal and expected to grow (authoring, templates, live/async delivery, review, later AI). Several bounded contexts must stay extractable without starting as a microservice fleet. The team is .NET + Angular; REST is the agreed API style. Simple DDD is required: ubiquitous language, modules as contexts, aggregates only when invariants need them.

## Decision

Ship **one ASP.NET Core host** with in-process modules:

Access, Openings, Catalog, Delivery, Evaluation, Search.

- Each module owns its tables (PostgreSQL schemas) and REST surface.
- Other modules call a **small public application API**, never foreign tables.
- Shared kernel is identifiers, tag key–value, pagination, permission code constants, clock — **not** a dumping ground for entities.
- Integration is **synchronous in-process** and **REST to the SPA**. No message bus, saga, or domain-event storm in v1.
- Delivery **snapshots** quiz content at assign time via Catalog’s public API so attempts do not join live authoring tables.
- Skip repositories-for-everything, specification-pattern defaults, and mediator libraries.

Module map and contracts: `docs/architecture.md`.

## Consequences

- .NET scaffolds one host, one database, module folders/projects with clear boundaries — cheaper to operate on-prem than many services.
- A module can become a separate process later if an invariant or scale reason appears; v1 does not split.
- Angular talks only to HTTP contracts; it does not know module internals.
- Live and async stay one assignment model inside Delivery (Brief §9), not two products.
