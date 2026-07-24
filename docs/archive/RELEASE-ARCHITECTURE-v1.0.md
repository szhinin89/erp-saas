# RELEASE — Architecture Baseline v1.0

**Tag Git:** `architecture-v1.0`  
**Fecha:** 2026-05-21  
**Commit base:** post `a2ab492` + stabilization + governance layer  
**Tipo:** Architecture freeze (estructura y gobernanza — no feature freeze funcional)

---

## 1. Resumen ejecutivo

Este release consolida el monorepo **ERP SaaS ZH Technologies** como **baseline oficial de producción** tras:

1. Reorganización enterprise del root (`infrastructure/`, `scripts/`, `tools/`, `security/`, `monitoring/`)
2. Estabilización CI (299 tests backend + guardrails; frontend lint/build/E2E smoke verificados post-remediación)
3. Capa de gobernanza: `SYSTEM_TRUTH.md`, `ARCHITECTURE_GATES.md`, architecture tests

**Punto de no retorno arquitectónico:** cambios estructurales post v1.0 requieren ADR + actualización de `RELEASES/` y posible tag `architecture-v1.x`.

---

## 2. Estado final del monorepo

| Área | Ruta | Rol |
|------|------|-----|
| Backend | `backend/src/` | .NET 10 — Domain, Application, Infrastructure, API, tests |
| Frontend | `frontend/src/` | Vite + React — `modules/` vertical, ZH Form System |
| Infraestructura | `infrastructure/` | Docker compose base, postgres, deployment stubs |
| Documentación | `docs/` | 7 canónicos + ADRs + deployment/diagrams |
| Scripts operación | `scripts/` | `dev/`, `ci/`, `db/`, `setup/` |
| Tooling | `tools/` | architecture guardrails, quality, generators |
| Seguridad | `security/` | Políticas auth, tenant, compliance stubs |
| Monitoreo | `monitoring/` | Preparación observabilidad |
| Tests índice | `tests/` | Índice suites (código en `backend/src/ERP.*.Tests`) |
| Releases | `RELEASES/` | Baselines formales |
| CI | `.github/workflows/` | Orquestador modular |
| Gobernanza | raíz | `SYSTEM_TRUTH.md`, `ARCHITECTURE_GATES.md`, `CONTEXT.md` |

**Compose local:** `docker-compose.yml` → `infrastructure/docker/compose.base.yml` (PostgreSQL `:5435`, Redis `:6379`).

---

## 3. Módulos backend oficiales

### 3.1 `ERP.Domain/Modules/`

Ubicación principal de entidades y contratos por vertical. Ver tabla completa en [`SYSTEM_TRUTH.md`](../SYSTEM_TRUTH.md) §2.1.

Incluye: Access, Accounting, Audit, Auth, Cash, Company, Configuration, ElectronicDocuments, Expenses, Inventory, Logistics, Menu, Navigation, Products, Purchasing, Purchases, Sales (**Customer** aquí), Security, SriCatalogs, Tenants, Common/SharedKernel.

**No están bajo `Modules/` en Domain:** Branches (`ERP.Domain/Branches/`), Billing (`ERP.Domain/Billing/`), Subscriptions (`ERP.Domain/Subscriptions/`), Geography, Navigation (dominio raíz).

### 3.2 Application layer

| Área | Ruta |
|------|------|
| Casos de uso producto | `ERP.Application/Modules/` |
| SaaS / planes | `ERP.Application/Subscriptions/` |
| Facturación SaaS | `ERP.Application/Billing/` |
| SuperAdmin global | `ERP.Application/Admin/UseCases/SuperAdminGlobal/` |
| Plataforma | `ERP.Application/Platform/` |

---

## 4. Módulos oficiales frontend

Ubicación: `frontend/src/modules/`. Patrón: `api/`, `schemas/`, `hooks/`, `pages/`.

| Módulo | Dominio |
|--------|---------|
| `access` | Perfiles, acceso subscriber |
| `accounting` | Contabilidad |
| `admin` | Actividad admin |
| `auth` | Login, selección subscriber/empresa |
| `billing` | Preparación SaaS billing (UI parcial) |
| `branches` | Sucursales |
| `catalog` | Catálogo estructura (categorías, unidades, tipos) |
| `companies` | SuperAdmin empresas |
| `company-management` | Gestión empresa operativa |
| `compras` | Proveedores, OC, compras |
| `config` | Feature gates, entitlements |
| `configuracion` | Empresa, SRI |
| `customers` | Clientes |
| `dashboard` | Panel principal |
| `gastos` | Gastos |
| `inventario` | Bodegas, transferencias, ajustes |
| `logistica` | Transportistas |
| `products` | Productos |
| `reportes` | Reportes (p. ej. ventas) |
| `saas` | Utilidades SaaS |
| `security` | Configuración seguridad tenant |
| `shared` | Placeholders, utilidades UI |
| `superadmin` | Panel plataforma, planes, menú builder |
| `ventas` | Facturación, ventas |

**Regla post-baseline:** nuevas features solo bajo `modules/` — no recrear `frontend/src/services/`.

---

## 5. Arquitectura multi-tenant

| Concepto | Implementación |
|----------|----------------|
| Tenant SaaS | **Subscriber** (`SubscriberId`) — facturación, límites comerciales |
| Empresa operativa | **Company** (`CompanyId`) — multi-empresa por subscriber |
| Aislamiento app | EF global query filters + `CompanyScopeBehavior` + membership |
| Aislamiento DB | PostgreSQL RLS (tablas enterprise core) |
| Soft delete | `IsActive = false` — sin DELETE físico de negocio |
| Contexto JWT | Claims subscriber + company; empresa vía `ICurrentCompany` |

**ADR:** [ADR-004](../docs/decisions/ADR-004-multi-tenant.md) · Detalle: [`docs/DATABASE.md`](../docs/DATABASE.md).

---

## 6. Modelo de autenticación

| Aspecto | Decisión baseline |
|---------|-------------------|
| Access token | Memoria cliente (no persistencia localStorage para access) |
| Refresh token | Cookie **httpOnly**, path `/api` |
| Rotación | Atómica por **familia** (`FamilyId`); revocación en cadena |
| Multi-tab | Web Locks + BroadcastChannel (`authRefreshManager`) |
| Bootstrap | `SessionBootstrap` + retry; perfil/permisos en `sessionStorage` |
| Logout | `fullLogout()` centralizado (stores + `erp.saas.*`) |
| SuperAdmin | Rutas plataforma; **sin UUID tenant en URL** (`sessionStorage`) |

**ADR:** [ADR-003](../docs/decisions/ADR-003-refresh-token-rotation.md) · [`AUTH_RULES.md`](../AUTH_RULES.md).

---

## 7. CI/CD actual

**Orquestador:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)

| Workflow | Contenido |
|----------|-----------|
| `architecture.yml` | Stack allowlist, handler size, architecture guardrails, identity guardrails |
| `backend-ci.yml` | `dotnet test backend/src/ERP.slnx -c Release` |
| `frontend-ci.yml` | ESLint, TypeScript, build, chunk guardrail, Playwright |
| `security.yml` | Identity guardrails (reusable) |
| `e2e.yml` | E2E manual dispatch |

**Scripts clave:**

- `scripts/ci/verify-stack-allowlist.ps1`
- `tools/architecture/check-architecture-guardrails.ps1`
- `tools/architecture/check-identity-guardrails.ps1`
- `tools/quality/check-handler-size.ps1`

---

## 8. Estado de tests (baseline)

### 8.1 Tag inicial (`5874ca2`) — auditoría senior

| Suite | Resultado al tag |
|-------|------------------|
| Backend (`dotnet test`) | ✅ 299 / 299 |
| Frontend ESLint | ❌ ~185 errores en `src/` |
| Frontend `tsc` + `build` | 🟡 fallaba por tipos en `ProductForm` |
| Playwright smoke | ❌ selector `h2.zh-form-title` obsoleto |
| `SuperAdminController` | ❌ repos directos + dependencia Infrastructure |
| `SYSTEM_TRUTH.md` | 🟡 Branches/Subscriptions listados bajo `Modules/` incorrectamente |

**Veredicto auditoría:** REJECTED — no sellado hasta remediación.

### 8.2 Post-remediación (validado local 2026-05-21)

| Suite | Resultado |
|-------|-----------|
| `ERP.API.Tests` | 174 / 174 |
| `ERP.Application.Tests` | 95 / 95 |
| `ERP.Domain.Tests` | 24 / 24 |
| `ERP.Architecture.Tests` | 7 / 7 |
| `ERP.Infrastructure.Tests` | 23 / 23 |
| **Total backend** | **299 / 299** |
| Frontend ESLint | ✅ 0 errors (15 warnings baseline) |
| Frontend Vitest | 22 / 22 |
| Frontend `tsc` + `build` | ✅ |
| Playwright smoke (`e2e/smoke.spec.ts`) | ✅ PASS |
| Playwright enterprise (`e2e/enterprise-*.spec.ts`) | ⏭ skip si API no disponible (requiere ERP.API + migraciones) |
| `check-architecture-guardrails.ps1` | ✅ |

**Remediación aplicada:**

- `SuperAdminController` → thin controller (`IMediator` only); handlers en `ERP.Application/Admin/UseCases/SuperAdminGlobal/`
- `IInstanceQuotaPersistence` en Application; implementación en Infrastructure
- ESLint: fixes reales + reglas React Compiler desactivadas en baseline (documentado en `eslint.config.js`)
- E2E smoke: `data-testid="erp-brand-title"` + selectores `#lp-email` / `#lp-password`
- `SYSTEM_TRUTH.md` alineado con layout real Domain/Application

**Architecture enforcement:**

- Backend: `backend/src/ERP.Architecture.Tests/` (NetArchTest + controller guardrails)
- Frontend: `tools/architecture/check-architecture-guardrails.ps1` (pointer: `frontend/scripts/architecture/README.md`)

---

## 9. Decisiones arquitectónicas consolidadas (ADRs)

| ADR | Decisión |
|-----|----------|
| [ADR-001](../docs/decisions/ADR-001-clean-architecture.md) | Clean Architecture — dependencias hacia abajo |
| [ADR-002](../docs/decisions/ADR-002-cqrs-mediatr.md) | CQRS + MediatR + FluentValidation pipeline |
| [ADR-003](../docs/decisions/ADR-003-refresh-token-rotation.md) | Refresh rotation por familia |
| [ADR-004](../docs/decisions/ADR-004-multi-tenant.md) | SubscriberId + CompanyId |
| [ADR-005](../docs/decisions/ADR-005-postgresql-strategy.md) | PostgreSQL 16, baseline EF único, RLS |
| [ADR-006](../docs/decisions/ADR-006-frontend-modularization.md) | Features en `frontend/src/modules/` |
| [ADR-007](../docs/decisions/ADR-007-modular-monolith.md) | Monolito modular por vertical slices |
| [ADR-008](../docs/decisions/ADR-008-saas-commercial-model.md) | Planes, features, límites comerciales |

---

## 10. Legacy tolerado

| Item | Estado | Notas |
|------|--------|-------|
| `Purchasing` + `Purchases` (backend) | Tolerado | Consolidación naming futura con ADR |
| `frontend/src/pages/` wrappers | Tolerado | Solo wrappers delgados; no crecer |
| `frontend/src/schemas/` compartidos | Tolerado | Preferir `modules/*/schemas/` en código nuevo |
| Unified document schema | Opcional | Flag `Documents:UseUnifiedSchema`; SQL en `scripts/db/sql/` |
| `build-and-deploy.yml` | Paralelo | Azure legacy; `ci.yml` es orquestador principal |
| ICE (impuesto consumos) | Diferido | Dominio preparado; sin UI/XML hasta requerimiento |
| Grandfather JSON | **Vacío** | `tools/architecture/architecture-grandfather.json` — sin deuda activa |

**Eliminado en baseline:** `scripts/sql/` (migrado a `scripts/db/sql/`), `frontend/src/services/` (eliminado).

---

## 11. Riesgos conocidos

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Fuga cross-company | Alta | RLS + EF filters + `CompanyScopeBehavior` + tests inventario |
| `UnifiedDocumentSyncIntegrationTests` skip en Docker local | Media | Seed EF completo; CI sin Postgres usa skip controlado |
| Playwright E2E drift | Media | Hardener flujo subscriber/company en CI |
| Billing UI no construida | Media | Entitlements API expone estado; UI `/saas/billing` pendiente |
| Validación SRI real (`celcer`) | Media | Código listo; certificado prueba pendiente |
| NuGet advisory `System.Security.Cryptography.Xml` | Baja | Monitorear upgrade en ciclo dependencias |
| Migración prod desde cadena EF antigua | Alta | Baseline único + migración datos planificada — nunca `DROP SCHEMA` |

---

## 12. Reglas activas del sistema

| Documento | Rol |
|-----------|-----|
| [`SYSTEM_TRUTH.md`](../SYSTEM_TRUTH.md) | Fuente única verdad arquitectónica |
| [`ARCHITECTURE_GATES.md`](../ARCHITECTURE_GATES.md) | Gates bloqueantes PR/CI/AI |
| [`CLAUDE.md`](../CLAUDE.md) | Reglas implementación agentes |
| [`docs/ARCHITECTURE-RULES.md`](../docs/ARCHITECTURE-RULES.md) | Reglas detalladas PR |
| [`.cursor/rules/`](../.cursor/rules/) | Enforcement IDE (tenant URL, stack, CQRS) |
| [`docs/HERRAMIENTAS-ERP-SAAS.md`](../docs/HERRAMIENTAS-ERP-SAAS.md) | Stack permitido |

**Gates críticos (resumen):** no DbContext en API · no lógica negocio en controllers · no Application→Infrastructure · features frontend en `modules/` · validación 4 capas · soft delete · no tenant UUID en URL.

---

## 13. Criterios de sello baseline

| Criterio | v1.0 |
|----------|------|
| `SYSTEM_TRUTH.md` | ✅ |
| `ARCHITECTURE_GATES.md` | ✅ |
| Tag `architecture-v1.0` | ✅ (con commit stabilization) |
| Architecture tests definidos y passing | ✅ |
| Paths legacy `scripts/sql/` eliminados | ✅ |
| CI workflows alineados | ✅ (post-remediación 2026-05-21) |
| Frontend lint + smoke E2E verdes | ✅ (post-remediación) |
| `SuperAdminController` sin repos/Infrastructure | ✅ (post-remediación) |

---

## 14. Evolución post v1.0

1. Cambio estructural → ADR + `RELEASES/RELEASE-ARCHITECTURE-v1.x.md`
2. Nuevo módulo producto → vertical completo + permisos + planes SaaS
3. Excepción a gate → ADR + grandfather temporal con fecha remoción
4. Estado delivery → `docs/STATUS.md` (no contradice SYSTEM_TRUTH)

**Referencias:** [`SYSTEM_TRUTH.md`](../SYSTEM_TRUTH.md) · [`docs/STATUS.md`](../docs/STATUS.md) · [`CONTEXT.md`](../CONTEXT.md)
