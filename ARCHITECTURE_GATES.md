# Architecture Gates — ERP SaaS (obligatorio)

**Fuente de verdad:** [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md)

Este archivo define **puertas de arquitectura** (gates) para PR, CI, revisiones humanas y agentes (Cursor / assistants). Incumplir una regla **prohibida** bloquea merge salvo ADR + entrada en `tools/architecture/architecture-grandfather.json`.

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
| G-P11 | **No herramientas fuera del stack** (`docs/HERRAMIENTAS-ERP-SAAS.md`) | Stack allowlist CI |
| G-P12 | **No AutoMapper** — mapeos manuales en handlers | Review |

---

## Reglas obligatorias

| ID | Regla | Referencia |
|----|-------|------------|
| G-O01 | Módulo backend = **vertical slice** por capas: Domain → Application → Infrastructure → API | `docs/ARCHITECTURE.md` |
| G-O02 | Todo endpoint mutante/lectura persistida pasa por **Application** (Command/Query + Validator) | CQRS ADR-002 |
| G-O03 | Toda feature frontend nueva vive bajo **`frontend/src/modules/{dominio}/`** (api, schemas, hooks, pages) | ADR-006 |
| G-O04 | Validación **4 capas** para datos persistidos (Zod, FluentValidation, Domain, EF config) | `CLAUDE.md` |
| G-O05 | Multi-tenant: **`TenantId`** en queries; empresa operativa vía **`CompanyId`** cuando aplique | ADR-004 |
| G-O06 | Integraciones explícitas (SRI, Redis, Hangfire) vía Infrastructure + interfaces en Application | Review |
| G-O07 | i18n: claves nuevas en **`es.json`**, **`en.json`**, **`qu.json`** | Reglas frontend |
| G-O08 | Módulo/pantalla comercializable: **`SaasFeatureDefinition`** + planes antes de cerrar | Reglas SaaS |
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
| Frontend | lint, build, chunk, Playwright | `frontend-ci.yml` |

**Pre-PR local (mínimo):**

```powershell
./scripts/ci/verify-stack-allowlist.ps1
dotnet test backend/src/ERP.slnx -c Release
cd frontend; npm run lint; npm run build
./tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk
```

---

## Excepciones (legacy tolerado)

Solo vía **`tools/architecture/architecture-grandfather.json`** con justificación en ADR. Objetivo: **vaciar** el grandfather, no ampliarlo.

---

## Uso en code review

Checklist PR (además de [`CONTRIBUTING.md`](CONTRIBUTING.md)):

1. ¿Respeta gates G-P* y G-O*?
2. ¿Añadió excepción al grandfather sin ADR? → rechazar
3. ¿Cambió estructura raíz del monorepo? → requiere actualización `docs/ARCHITECTURE.md` + release note
4. ¿Nuevo módulo SaaS? → planes confirmados

---

## Uso en agentes AI

Antes de implementar: leer **`docs/ARCHITECTURE.md`** + **`CLAUDE.md`**.  
No proponer herramientas fuera del stack.  
No crear paths paralelos (`scripts/sql/`, duplicar `services/`).  
Tras feature: actualizar `STATUS.md` y `PROGRESS.html` si aplica.
