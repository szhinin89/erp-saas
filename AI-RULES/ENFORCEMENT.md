# Enforcement — validación, docs sync, CI

---

## NO DUPLICAR REGLAS

> **Política anti-drift obligatoria.**

| ✅ Correcto | ❌ Incorrecto |
|------------|---------------|
| Regla completa **una vez** en `AI-RULES/*` | Misma regla en `CLAUDE.md` + `.mdc` + `AI-RULES/` |
| Adaptador con enlace + hint ≤5 líneas | Copiar 300 líneas a cada adaptador |
| Editar canónico → actualizar enlaces | Editar solo adaptador y olvidar canónico |

Al añadir regla nueva: elegir **un** archivo canónico → enlazar desde adaptadores.

Ver [AGENT-COMPATIBILITY.md](./AGENT-COMPATIBILITY.md).

---

## Validación en 4 capas (datos persistidos)

Toda validación que afecte datos guardados se refleja en **4 capas**. Prohibido validar solo en frontend.

| Capa | Herramienta | Ubicación |
|------|-------------|-----------|
| 1 Frontend | Zod + react-hook-form | `frontend/src/schemas/{modulo}/`; `zodResolver`; error por campo |
| 2 Application | FluentValidation + MediatR | `[Nombre]Validator`; `ValidationBehavior` |
| 3 Domain | Guard clauses + factories | `Entidad.Create(...)`; `DomainException` |
| 4 BD | EF Core configuration | `IsRequired`, `HasMaxLength`; índices únicos con `TenantId` |

### Convención de errores

| Capa | Formato |
|------|---------|
| Frontend | Mensajes en español junto al campo |
| Application | `ValidationException` → **422** |
| Domain | `DomainException` vía middleware |
| API | `ApiResponse<T>` (envelope `code/severity/message{user,dev}/data/meta`, `code` es la fuente única de verdad vía `MessageCatalog`; `meta.correlationId` viene siempre de `RequestCorrelationMiddleware`); `Result<T>` fallido → **400** — contrato **LOCKED**, ver BACKEND-RULES.md §"API Response Contract V1 — LOCKED" |

Detalle backend/frontend: [BACKEND-RULES.md](./BACKEND-RULES.md), [FRONTEND-RULES.md](./FRONTEND-RULES.md).

---

## Catálogo PR bloqueante

Reglas B-xx / F-xx con severidad **BLOQUEANTE**: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md)

Entrada raíz PR: `docs/ARCHITECTURE-RULES.md` (adaptador).

---

## Roles y acceso UI — contrato congelado

Fuente única de roles (`SecurityRoles` backend / `isAdminRole()` frontend) y modelo de
gating de páginas (server-driven menu + `isAdminRole`, deny-by-default): ver
[SECURITY.md — Security & Access Contract V1 — LOCKED](./SECURITY.md#security--access-contract-v1--locked)
y regla [SEC-01b / PR-18](./PR-RULES-CATALOG.md#sec-01b--roles-fuente-única-securityroles--isadminrole).

---

## Sincronización docs de avance

**Al completar funcionalidad**, actualizar documentación de avance:

| Documento | Acción |
|-----------|--------|
| `PROGRESS.html` | Marcar ítem, `#last-updated`, badge |
| `docs/STATUS.md` | Resumen, tablas módulos, pendientes MVP, fecha |
| `docs/ROADMAP.md` | Si cambian prioridades/fases |
| `README.md` | Si cambia alcance, rutas, endpoints, permisos |

Estados módulo: `✅` completo, `🟡` parcial, `⏳` pendiente, `🚧` en progreso.

Fuente operativa consolidada: **`docs/STATUS.md`**.

Cursor hint: `.cursor/rules/docs-progress-status-sync.mdc` (glob `docs/STATUS.md`).

---

## Tests pre-merge

```powershell
cd backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj
cd frontend && npx tsc --noEmit && npm run build && npm run architecture:check
```

---

## Guardrails automatizados

### Backend / monorepo (PowerShell + .NET)

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Identity guardrails | `tools/architecture/check-identity-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

### Frontend + backend architecture (Node.js — enforcement ejecutable)

| Script | Comando | Qué valida |
|--------|---------|------------|
| Runner | `npm run architecture:check` (desde `frontend/`) | 9 checks + score + JSON report |
| Pages wrapper | `npm run architecture:pages` | `pages/**/*.tsx` ≤15 líneas, sin hooks/api |
| Import boundaries | `npm run architecture:imports` | Imports prohibidos, profundidad relativa |
| Module boundaries | `npm run architecture:modules` | Cross-imports entre módulos |
| CSS prefixes | `npm run architecture:css` | Prefijos por área, clases ambiguas |
| Cross-layer | `npm run architecture:cross-layer` | Pages/stores sin capas prohibidas |
| Backend (4 checks) | `npm run architecture:backend` | Layering csproj, usings, controllers, tenant |
| JSON + score | `npm run architecture:report` | `architecture-report.json` con `architectureScore` |
| GitHub annotations | `node tools/architecture/run-all.mjs --annotate` | `::error file=…::` / `::warning file=…::` |

Fuente: [`tools/architecture/README.md`](../tools/architecture/README.md)

#### Backend checks (Node, heurístico — sin Roslyn)

| Check | Regla | Qué valida |
|-------|-------|------------|
| `backend-layering` | B-layering | Referencias de proyecto/paquetes por capa (.csproj) |
| `backend-clean-architecture` | B-domain / B-application | `using` prohibidos en Domain/Application |
| `backend-controller-thin` | B-controller | Líneas máx., patrones EF/SQL inline (error); líneas > umbral warning |
| `backend-subscriber-rules` | B-subscriber | `IgnoreQueryFilters()` fuera de allowlist; entidades sin `TenantId`/marker |

Umbrales y allowlists: `tools/architecture/config/architecture-rules.json` → `backend`.

#### Architecture score

`calculate-score.mjs` + `config/scoring-rules.json` producen en `architecture-report.json`:

- `architectureScore` (0–100), `status` (`healthy` | `warning` | `critical`)
- `driftRisk` (`low` | `medium` | `high`)
- `modules` — desglose por módulo frontend/backend
- `adrs` — índice de ADRs vigentes

Penalizaciones: violations (−8), warnings (−2), entradas grandfather (−1). Solo afecta reporte/CI; **cero impacto runtime**.

#### PR annotations (GitHub Actions)

`formatters/github-formatter.mjs` convierte violations/warnings en anotaciones:

```
::error file=frontend/src/pages/Foo.tsx,line=12::PR-6 violation: pages wrapper exceeds 15 lines
::warning file=backend/src/ERP.API/Controllers/BarController.cs::B-controller-warn: controller has 200 lines
```

Auto-emite en CI cuando `GITHUB_ACTIONS=true` (integrado en `run-all.mjs`).

#### Checks event-driven + IA (Node.js)

| Script | Qué valida |
|--------|------------|
| `check-domain-events-rules.mjs` | Naming eventos (past tense + `Event` suffix); solo en `ERP.Domain`; no en `ERP.API` |
| `check-ai-layer-boundaries.mjs` | `ERP.Domain`/`ERP.Application` no referencian paquetes IA; `ERP.AI.*` no acceden a DbContext ERP directamente |

---

#### ADRs (rationale, no enforcement)

| ADR | Tema |
|-----|------|
| [ADR-001](../docs/adr/ADR-001-modular-monolith.md) | Modular monolith |
| [ADR-002](../docs/adr/ADR-002-no-erp-shared.md) | Sin ERP.Shared |
| [ADR-003](../docs/adr/ADR-003-pages-wrapper-only.md) | Pages wrapper |
| [ADR-004](../docs/adr/ADR-004-clean-architecture-enforcement.md) | Clean Architecture + checks |
| [ADR-005](../docs/adr/ADR-005-multi-tenant-query-filters.md) | Multi-tenant filters |
| [ADR-006](../docs/adr/ADR-006-multi-agent-governance.md) | Governance multi-agente |
| [ADR-007](../docs/adr/ADR-007-domain-events-foundation.md) | Domain events foundation |
| [ADR-008](../docs/adr/ADR-008-outbox-pattern-foundation.md) | Outbox pattern foundation |
| [ADR-009](../docs/adr/ADR-009-ai-layer-separation.md) | Separación capa IA |

Índice: [`docs/adr/README.md`](../docs/adr/README.md). **AI-RULES** = reglas; **ADRs** = por qué.

**Config:**

| Archivo | Uso |
|---------|-----|
| `tools/architecture/config/architecture-rules.json` | Reglas FE/BE import/module/cross-layer/backend + `exemptions` + `adr.index` |
| `tools/architecture/config/css-prefixes.json` | Mapa path → prefijo CSS |
| `tools/architecture/config/scoring-rules.json` | Pesos del architecture score |
| `tools/architecture/architecture-grandfather.json` | Legacy permitido (`tsxPageWrapperMaxLines15`, …) |

**Extender reglas:** editar JSON de config → `npm run architecture:check`. Documentar en este archivo (tabla) si la regla es normativa nueva.

**Excepciones:** añadir path a `architecture-grandfather.json` o `architecture-rules.json` → `exemptions`. Requiere ADR o nota en PR. No silenciar checks en código.

Grandfather legacy: `tools/architecture/architecture-grandfather.json`

---

## Ramas

| Rama | Uso |
|------|-----|
| `main` | Integración estable |
| `development` | Features diarias |
| `release/*` | Estabilización |
| `hotfix/*` | Correcciones urgentes |
