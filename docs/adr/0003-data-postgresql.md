# ADR 0003 — PostgreSQL as the system database

## Context

Modules need a relational store (users, openings, assignments, attempts, permissions) and flexible documents (dynamic opening fields, question graphs, assignment snapshots, saved-filter criteria, versioned AI rule JSON). The quality bar prefers open-source. Hosting is on-premises. EF Core supports both SQL Server and PostgreSQL.

SQL Server would be justified if the company already standardized on it for this estate and forbade PostgreSQL. That constraint is **not documented**.

## Decision

Use **PostgreSQL** as the single v1 database:

- One database, **module-owned schemas** (`access`, `openings`, `catalog`, `delivery`, `evaluation`, `search`) until a split is justified.
- **JSONB** for dynamic fields, questions, snapshots, filters, and AI rule documents. Relational columns for keys, statuses, FKs, and queryable tags/experience where listings need them.
- EF Core migrations; expand/contract. Development seed only in Dev.
- File binaries (resumes) **off-database** (local/NAS); PostgreSQL stores metadata and paths.
- Local development: **Docker Compose** PostgreSQL.
- No default Redis, Elasticsearch, or extra document database.

## Consequences

- Ops runs and backs up PostgreSQL on-prem (or Compose in Dev). DevOps documents connection env names, not secrets.
- JSONB fits openings-without-hierarchy and snapshot-on-assign without a second product database.
- If a future mandate requires SQL Server, that is a new ADR and a migration — not the v1 default.
- Search/saved filters are rows + JSON criteria executed by the owning list APIs, not a separate search engine in v1.
