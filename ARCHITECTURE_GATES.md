# Architecture Gates — ERP SaaS (obligatorio)

**Jerarquía:** [`CLAUDE.md`](CLAUDE.md#jerarquía-documental). Reglas completas: [catálogo PR](docs/architecture/pr-rules-catalog.md) y [enforcement](docs/architecture/enforcement.md).

Este archivo es un índice de gates para PR, CI, revisiones humanas y agentes. Las tablas son recordatorios; no redefinen reglas ni autorizan excepciones generales. La especificación y las excepciones admitidas pertenecen a las fuentes canónicas enlazadas.

---

## Reglas prohibidas

| ID | Regla | Enforcement |
|----|-------|-------------|
| G-P01 | **No `DbContext` / `ErpDbContext` en Controllers** | `ERP.Architecture.Tests` · grep guardrails |
| G-P02 | **No lógica de negocio en Controllers** (solo HTTP + MediatR + `ApiResultExtensions`) | Review + guardrails |
| G-P03 | **No imports cruzados entre módulos Application** (comunicación vía contratos de dominio / MediatR / orquestación explícita) | NetArchTest + review |
| G-P04 | **No `services/` globales nuevos en frontend** — features en `frontend/src/modules/` | `check-architecture-guardrails.ps1` |
| G-P05 | **No schemas Zod duplicados** — un schema por entidad en `frontend/src/schemas/` o `modules/*/schemas/` | Review + guardrails |
| G-P06 | **No referencia directa Application → Infrastructure** | `LayerDependencyTests` |
| G-P07 | **No paths legacy** (`scripts/sql/`, `scripts/dev-restart.ps1` raíz, etc.) | `verify-stack-allowlist.ps1` |
| G-P08 | **No entidades de dominio en contratos API** | Review + `ARCHITECTURE-RULES` |
| G-P09 | **No DELETE físico** en entidades de negocio (soft delete) | Review + dominio |
| G-P10 | **No UUID de tenant en URL compartible** | Regla `saas-navigation-no-sensitive-url` |
| G-P11 | **No herramientas fuera del stack** ([stack oficial](docs/DEVELOPMENT.md#stack-oficial)) | Stack allowlist CI |
| G-P12 | **No AutoMapper** — mapeos manuales en handlers | Review |

---

## Reglas obligatorias

| ID | Regla | Referencia |
|----|-------|------------|
| G-O01 | Módulo backend = **vertical slice** por capas, con dependencias Clean Architecture | [Arquitectura](docs/architecture/architecture.md) |
| G-O02 | Todo endpoint mutante/lectura persistida pasa por **Application** (Command/Query + Validator) | [ADR-013](docs/decisions/ADR-013-cqrs-mediatr.md) |
| G-O03 | Toda feature frontend nueva vive bajo **`frontend/src/modules/{dominio}/`** (api, schemas, hooks, pages) | [ADR-016](docs/decisions/ADR-016-frontend-modularization.md) |
| G-O04 | Validación **4 capas** para datos persistidos (Zod, FluentValidation, Domain, EF config) | `CLAUDE.md` |
| G-O05 | Multi-tenant: **`TenantId`** en queries; empresa operativa vía **`CompanyId`** cuando aplique | [Seguridad](docs/architecture/security.md) |
| G-O06 | Integraciones explícitas (SRI, Redis, Hangfire) vía Infrastructure + interfaces en Application | Review |
| G-O07 | i18n: claves nuevas en **`es.json`** y **`en.json`** | Reglas frontend |
| G-O08 | Planes y capacidades comerciales pertenecen a una futura Platform externa; no son requisito de cierre de módulos ERP | [Frontera ERP ↔ Platform](docs/architecture/architecture.md#reglas-de-arquitectura-que-no-se-rompen) |
| G-O09 | Controllers usan **`ApiResultExtensions`** (`ToOkOrBadRequest`, etc.) | `backend-api-contracts` |
| G-O10 | Entidades de dominio instanciadas solo vía **`Create(...)`** factories | Domain rules |

---

## Enforcement (CI y local)

| Capa | Herramienta | Comando / workflow |
|------|-------------|-------------------|
| Stack | `scripts/ci/verify-stack-allowlist.ps1` | `.github/workflows/architecture.yml` |
| Backend capas | `ERP.Architecture.Tests` (NetArchTest) | `dotnet test backend/src/ERP.slnx` |
| Controllers sin DbContext | `ApiControllerGuardrailTests` | idem |
| Handler size | `tools/quality/check-handler-size.ps1` | `architecture.yml` |
| Patrones repo | `tools/architecture/check-architecture-guardrails.ps1` | `architecture.yml` + `frontend-ci.yml` |
| Identity legacy | `tools/architecture/check-identity-guardrails.ps1` | `architecture.yml`, `security.yml` |
| Backend tests | `dotnet test` | `backend-ci.yml` |
| Frontend | lint, build y guardrails (sin E2E) | `frontend-ci.yml` |
| E2E manual | PostgreSQL, API y Playwright | `e2e.yml` → `e2e-manual.yml` |

**Validación local:** aplicar los checks según el tipo de cambio en
[enforcement](docs/architecture/enforcement.md), incluido el gate oficial
`npm run architecture:check` desde `frontend/`. Este índice no mantiene otra lista
de comandos mínimos. Para trabajo documental: referencias y `git diff --check`.

---

## Excepciones (legacy tolerado)

Consultar [excepciones e integridad del grandfather](docs/architecture/enforcement.md#guardrails-automatizados). Una entrada solo es válida para la regla y el checker que la consumen; no exime del ratchet ni de las reglas globales.

---

## Uso en code review

Checklist PR (además de [`CONTRIBUTING.md`](CONTRIBUTING.md)):

1. ¿Respeta gates G-P* y G-O*?
2. ¿Añadió excepción al grandfather sin ADR? → rechazar
3. ¿Cambió estructura raíz del monorepo? → requiere actualización `docs/ARCHITECTURE.md` + release note
4. ¿Respeta la frontera ERP ↔ Platform?

---

## Uso en agentes AI

Antes de implementar: leer **`docs/ARCHITECTURE.md`** + **`CLAUDE.md`**.  
No proponer herramientas fuera del stack.  
No crear paths paralelos (`scripts/sql/`, duplicar `services/`).  
Avance y cierres: [sincronización documental](docs/architecture/enforcement.md#sincronización-docs-de-avance) y [flujo por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).
