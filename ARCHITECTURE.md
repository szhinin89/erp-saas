# Architecture — ERP SaaS (entrada raíz)

> **Documento canónico:** [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)  
> **Reglas bloqueantes PR:** [`docs/ARCHITECTURE-RULES.md`](docs/ARCHITECTURE-RULES.md) · [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md)

## Vista rápida

| Capa | Ubicación | Responsabilidad |
|------|-----------|-----------------|
| API | `backend/src/ERP.API` | HTTP, auth, DTOs |
| Application | `backend/src/ERP.Application` | CQRS, validación, orquestación |
| Domain | `backend/src/ERP.Domain` | Entidades, invariantes, interfaces |
| Infrastructure | `backend/src/ERP.Infrastructure` | EF, repos, servicios técnicos |
| Frontend | `frontend/src/modules/*` | UI modular, ZH Form, i18n |

## Scopes

- **Platform (SuperAdmin):** `/api/platform/*`, `/superadmin/*`, `/companies`
- **Tenant ERP:** datos filtrados por `SubscriberId` + empresa operativa

## Decisiones

Índice ADR: [`docs/decisions/`](docs/decisions/)

## Diagramas

[`docs/diagrams/`](docs/diagrams/)
