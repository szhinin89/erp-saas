# Platform.Contracts

This project contains **only contracts** — interfaces, DTOs, records, and enums — for a
**future** Platform/SaaS layer that will consume the ERP's public integration API
(`/api/integration/v1/*`). No implementation exists yet.

## Boundary rules (ADR-ERP-002)

Per ADR-ERP-002 (see `ERP_CORE_FREEZE.md` at the repo root): *"ERP never depends on
Platform; Platform may consume ERP APIs only"*.

- `ERP.*` projects must **never** reference `Platform.Contracts`.
- `Platform.Contracts` must **never** reference any `ERP.*` project.

## Contents

- `IIntegrationEvent` / `Events/*` — placeholder integration event contracts for a future
  event bus integration. These mirror `ERP.Domain.Common.IIntegrationEvent`, which is the
  ERP-side marker applied to exportable domain events (e.g. `ItemCreatedEvent`,
  `InvoiceCreatedEvent`, `InvoiceAuthorizedEvent`).
- `Webhooks/*` — generic webhook envelope and event type enum for delivering integration
  events to Platform.
- `Integration/*` — `IErpPublicApiClient` contract and DTOs mirroring the ERP's public
  integration API endpoints for tenant/company provisioning and status management.
