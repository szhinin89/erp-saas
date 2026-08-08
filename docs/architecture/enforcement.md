# Enforcement — validación, docs sync, CI

---

## NO DUPLICAR REGLAS

> **Política anti-drift obligatoria.**

| ✅ Correcto | ❌ Incorrecto |
|------------|---------------|
| Regla completa **una vez** en `docs/architecture/*` | Misma regla repetida en `CLAUDE.md` + `backend/CLAUDE.md` + `frontend/CLAUDE.md` + `docs/architecture/*` |
| `backend/CLAUDE.md`/`frontend/CLAUDE.md` con enlace + hint ≤5 líneas | Copiar 300 líneas al `CLAUDE.md` de capa |
| Editar canónico → actualizar enlaces | Editar solo `CLAUDE.md` y olvidar canónico |

Al añadir regla nueva: elegir **un** archivo canónico en `docs/architecture/` → enlazar desde `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md`.

Ver [Compatibilidad multi-agente](#compatibilidad-multi-agente) más abajo en este documento.

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

Detalle backend/frontend: [BACKEND-RULES.md](./backend.md), [FRONTEND-RULES.md](./frontend.md).

---

## Catálogo PR bloqueante

Reglas B-xx / F-xx con severidad **BLOQUEANTE**: [PR-RULES-CATALOG.md](./pr-rules-catalog.md)

Entrada raíz PR: `docs/ARCHITECTURE-RULES.md` (adaptador).

---

## Roles y acceso UI — contrato congelado

Fuente única de roles (`SecurityRoles` backend / `isAdminRole()` frontend) y modelo de
gating de páginas (server-driven menu + `isAdminRole`, deny-by-default): ver
[SECURITY.md — Security & Access Contract V1 — LOCKED](./security.md#security--access-contract-v1--locked)
y regla [SEC-01b / PR-18](./pr-rules-catalog.md#sec-01b--roles-fuente-única-securityroles--isadminrole).

---

## Sincronización docs de avance

**Al completar funcionalidad**, actualizar documentación de avance:

| Documento | Acción |
|-----------|--------|
| `PROGRESS.html` | Marcar ítem, `#last-updated`, badge |
| `STATUS.md` | Resumen, tablas módulos, pendientes MVP, fecha |
| `docs/ROADMAP.md` | Si cambian prioridades/fases |
| `README.md` | Si cambia alcance, rutas, endpoints, permisos |

Estados módulo: `✅` completo, `🟡` parcial, `⏳` pendiente, `🚧` en progreso.

Fuente operativa consolidada: **`STATUS.md`**.

Cursor hint: `.cursor/rules/docs-progress-status-sync.mdc` (glob `STATUS.md`).

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

Fuente: [`tools/architecture/README.md`](../../tools/architecture/README.md)

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
| [ADR-001](../decisions/ADR-001-modular-monolith.md) | Modular monolith |
| [ADR-002](../decisions/ADR-002-no-erp-shared.md) | Sin ERP.Shared |
| [ADR-003](../decisions/ADR-003-pages-wrapper-only.md) | Pages wrapper |
| [ADR-004](../decisions/ADR-004-clean-architecture-enforcement.md) | Clean Architecture + checks |
| [ADR-005](../decisions/ADR-005-multi-tenant-query-filters.md) | Multi-tenant filters |
| [ADR-006](../decisions/ADR-006-multi-agent-governance.md) | Governance multi-agente |
| [ADR-007](../decisions/ADR-007-domain-events-foundation.md) | Domain events foundation |
| [ADR-008](../decisions/ADR-008-outbox-pattern-foundation.md) | Outbox pattern foundation |
| [ADR-009](../decisions/ADR-009-ai-layer-separation.md) | Separación capa IA |

Índice: [`docs/decisions/README.md`](../decisions/README.md). **`docs/architecture/*`** = reglas; **ADRs** = por qué.

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

Ramas y CI: ver [architecture.md § CI y ramas](./architecture.md#ci-y-ramas) (cuerpo normativo único — no duplicado aquí).

---

# Compatibilidad multi-agente

Arquitectura para que **Cursor**, **Claude** y futuros agentes lean la misma verdad sin drift documental.

## Fuente canónica

**`docs/architecture/*`** es la única fuente donde viven las reglas completas (sustituye al antiguo `AI-RULES/*`, ver [`docs/decisions/archive-ai-rules/README.md`](../decisions/archive-ai-rules/README.md)).

Ningún agente debe inventar convenciones fuera de estos archivos sin confirmación explícita del usuario.

## Cómo consume cada agente

| Agente | Punto de entrada | Qué lee en la práctica |
|--------|------------------|------------------------|
| **Cursor** | `.cursor/rules/*.mdc` (`alwaysApply`, `globs`) | Adaptadores livianos → enlaces a `docs/architecture/*` |
| **Claude Code** | `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md` | Onboarding + índice → enlaces a `docs/architecture/*` |
| **Humanos / PR** | `CONTEXT.md` | Índice → [pr-rules-catalog.md](./pr-rules-catalog.md) |
| **Futuros agentes** | [`docs/architecture/README.md`](./README.md) | Mismo índice canónico |

## Adaptadores (obligatorio mantener)

Los adaptadores **no duplican** reglas extensas. Solo:

- Enlaces al canónico
- Hints operativos mínimos (globs Cursor, checklist de 3–5 ítems)
- Referencias cruzadas entre áreas

| Adaptador | Propósito |
|-----------|-----------|
| `CLAUDE.md` | Entrada Claude; jerarquía documental, precedencia, resumen de reglas globales |
| `backend/CLAUDE.md` | Entrada backend; complementa `CLAUDE.md`, enlaza a `docs/architecture/*` |
| `frontend/CLAUDE.md` | Entrada frontend; complementa `CLAUDE.md`, enlaza a `docs/architecture/*` |
| `erp-unified-rules.mdc` | Regla transversal Cursor (`alwaysApply: true`) |
| `rules-consolidated-map.mdc` | Mapa de precedencia Cursor |
| `backend-*.mdc`, `frontend-*.mdc` | Scope por glob en Cursor |

## Executable Enforcement

Las reglas críticas de frontend **no dependen únicamente** de prompts IA.

**Capa oficial:** `tools/architecture/*.mjs` (Node.js ESM, sin deps pesadas).

| Comando | Uso |
|---------|-----|
| `npm run architecture:check` | CI + pre-merge (desde `frontend/`) |
| `node tools/architecture/run-all.mjs` | Mismo runner desde raíz |
| `npm run architecture:report` | Artefacto JSON para agentes/CI |

Si un agente (Cursor, Claude, futuro) sugiere código que viola un check, **el CI fallará** aunque el agente no haya leído el adaptador.

Detalle por check: [§ Guardrails automatizados](#guardrails-automatizados-node--frontend--backend) más arriba · [`tools/architecture/README.md`](../../tools/architecture/README.md)

## CI Authority

Si entran en conflicto **prompts de IA**, **documentación** o **código sugerido** con el resultado de CI:

1. **Scripts ejecutables** (`tools/architecture/*.mjs`, guardrails PowerShell, tests) tienen **prioridad absoluta**.
2. **`docs/architecture/*`** prevalece sobre adaptadores (`.mdc`, `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`) en conflictos documentales.
3. **ADRs** (`docs/decisions/`) explican el *por qué*; no anulan un check que falla en CI sin ADR + cambio de config.
4. Los agentes deben **corregir el código** o **proponer cambio en config/ADR**, no ignorar el fallo del pipeline.

Rationale histórico: [`docs/decisions/ADR-006-multi-agent-governance.md`](../decisions/ADR-006-multi-agent-governance.md).

## Integrar un agente nuevo

1. Leer este documento y [`docs/architecture/README.md`](./README.md).
2. Crear un adaptador mínimo (≤80 líneas) que enlace a `docs/architecture/*`.
3. **No** copiar cuerpos completos de reglas al adaptador.
4. Registrar el adaptador en [`docs/architecture/README.md`](./README.md) y `CONTEXT.md`.

---

# Jerarquía de documentación y precedencia

Orden de autoridad cuando varias fuentes aplican al mismo cambio.

## Capas (de mayor a menor prioridad normativa)

| # | Capa | Ubicación | Rol |
|---|------|-----------|-----|
| 1 | **Scripts ejecutables + CI** | `tools/architecture/*.mjs`, guardrails PS | Bloquean merge si fallan |
| 2 | **Seguridad / multi-tenant** | [security.md](./security.md) | Innegociable |
| 3 | **Catálogo PR bloqueante** | [pr-rules-catalog.md](./pr-rules-catalog.md) | B-xx / F-xx |
| 4 | **Reglas canónicas por área** | [architecture.md](./architecture.md), [backend.md](./backend.md), [frontend.md](./frontend.md), este documento | Implementación diaria |
| 5 | **Stack y herramientas** | [stack.md](./stack.md) → `docs/DEVELOPMENT.md#stack-oficial` | Solo herramientas aprobadas |
| 6 | **Adaptadores de agente** | `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `.cursor/rules/*.mdc` | Índice + hints Cursor |
| 7 | **Contexto descriptivo** | `CONTEXT.md`, `docs/ARCHITECTURE.md`, `STATUS.md` | Estado, diagramas |
| 8 | **ADRs (rationale)** | [`docs/decisions/`](../decisions/README.md) | **Por qué** se decidió (no enforcement) |
| 9 | **Docs feature-specific** | `docs/*` | Detalle de dominio |

### ADRs vs docs/architecture/*

| Fuente | Responde | Ejemplo |
|--------|----------|---------|
| **ADRs** (`docs/decisions/ADR-*.md`) | *¿Por qué esta decisión?* | Modular monolith, no ERP.Shared |
| **`docs/architecture/*`** | *¿Qué hacer / qué prohíbe CI?* | PR-6 pages wrapper, B-layering |
| **`tools/architecture/`** | *¿Cumple el repo ahora?* | Score, violations, annotations |

## Resolución de conflictos

1. **Resultado de CI/scripts** prevalece sobre sugerencias de agentes IA.
2. **Seguridad / tenant / billing** prevalece sobre conveniencia o velocidad.
3. Entre reglas canónicas: la regla **más específica por área** gana.
4. Entre `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md` y `docs/architecture/*`: **`docs/architecture/*` prevalece** si hay contradicción documental.
5. `.cursor/rules/*.mdc` con `globs` aplican sobre reglas generales **solo en su alcance**.

## Qué NO es fuente de verdad

- Comentarios sueltos en código
- Preferencias personales en PRs
- Diagramas desactualizados fuera de `docs/architecture/` o ADRs vigentes
- Copias duplicadas de reglas en `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md` o `.mdc`

## Flujo recomendado al implementar

Ver tabla en [architecture.md § Flujo jerárquico](./architecture.md#flujo-jerárquico-implementar-una-feature).
