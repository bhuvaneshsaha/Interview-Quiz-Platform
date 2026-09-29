# ADR 0001 — Hosting on-premises

## Context

The Interview Quiz Platform is an internal company system (openings, quizzes, candidate attempts, resumes). Hosting was chosen by the product owner: **on-premises** (VMs, reverse proxy, self-hosted data). Public cloud is not the default. The quality bar is open-source or built-in only — no commercial PaaS, paid APM, or cloud KMS as defaults.

Architecture must map a Modular Monolith (ASP.NET Core + Angular) onto that estate without pulling Azure or AWS services.

## Decision

Deploy a **single Modular Monolith** process:

- **Kestrel** behind a **reverse proxy** (nginx, IIS, or whatever the datacenter already runs) that terminates **TLS**.
- Serve the Angular SPA/PWA **same-site** from that proxy; forward `/api` and `/health` to Kestrel.
- **PostgreSQL** on-prem (see ADR 0003). Resume files on **local disk or NAS**.
- Secrets via **environment variables**, OS-protected files, or **HashiCorp Vault (OSS)** — not cloud KMS.
- Telemetry: app emits **OpenTelemetry** + structured logs to an **on-prem OTel Collector** and files / existing OSS stack — not a paid APM, not Azure Monitor, not CloudWatch.
- Environments: **Dev**, **Test**, **Prod**. Local Dev uses **Docker Compose PostgreSQL**.
- CI/CD: **self-hosted runners**; DevOps picks the system ops already has.
- Reverse proxy **must not** long-cache `index.html` or `ngsw.json` (PWA). Hashed static assets may be cached.

**Explicitly out of scope as defaults:** Azure App Service / Azure SQL / Azure OpenAI / Azure Monitor, AWS equivalents, commercial appliances, Ionic native hosting.

Slice 7 AI uses an operator-configured OpenAI-compatible **HTTP URL**, which may be self-hosted. That is not an Azure/AWS hosting choice.

## Consequences

- Ops owns TLS certificates, backup of PostgreSQL and the file store, patch windows, and capacity of one host first. Split hosts only if a later constraint requires it.
- CORS stays simple while SPA and API share origin. A split origin needs an allowlist — not assumed.
- DevOps implements Compose, proxy headers, health-gated deploys, and collector exporters. Architecture does not write pipeline YAML.
- Entra ID later is an **external login** from this host, not a move to cloud hosting.
- HA/load-balancing can be added at the proxy later; v1 does not invent availability numbers.
