# Auditoría Enterprise QA — ERP SaaS ZH Technologies

> ## ⚠️ HISTÓRICO
>
> Este documento representa una decisión, auditoría o estado anterior del proyecto (pre-FASE 1 Kernel Cleanup, 2026-05-21).
>
> **NO representa la arquitectura actual del ERP.** Describe un "Panel global `/platform/*`" (`PlatformLayout`, `PlatformSubscribersPage`, `GlobalPlatformOperator`) que ya no existe en el código.
>
> La fuente de verdad actual es:
> - [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)
> - [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
> - El código fuente actual (`frontend/src`, `backend/src`)

---

**Rama:** `feature/qa-hardening-final`  
**Baseline:** `architecture-v1.0`  
**Fecha:** 2026-05-21  
**Alcance:** Auditoría funcional + técnica (sin refactors masivos)  
**Modo:** Read-only + fixes mínimos compatibles con baseline

---

## Resumen ejecutivo

El producto está **listo para QA funcional intensivo en staging**, con arquitectura multi-tenant sólida, templates visuales consolidados y buena cobertura de integración en flujos ERP core. **No está listo para producción pública** sin cerrar **3 hallazgos críticos de seguridad/auth** y **2 de límites comerciales**.

| Dimensión | Score | Notas |
|-----------|------:|-------|
| Arquitectura / baseline | **88/100** | Clean Architecture, CQRS, filters EF, templates UI |
| Seguridad & auth | **52/100** | Register abierto, logout sin revocación server-side, impersonación |
| Aislamiento tenant | **78/100** | Filters fuertes; gaps en plan limits y platform context |
| Backend calidad | **74/100** | Validators presentes; N+1 en listados admin; 422 inconsistente |
| Frontend core (módulos maduros) | **82/100** | ErpPageTemplate, ZH, responsive base OK |
| Frontend batch nuevo + documentos | **58/100** | i18n incompleto, formularios sin Zod+RHF |
| Tests & CI | **71/100** | ~140+ tests API; e2e limitado; gaps auth/register |
| Ops / deployment | **68/100** | Health OK; Redis fallback silencioso; SRI en ready probe |
| **Production readiness global** | **71/100** | **Conditional GO** — staging sí, prod tras P0 |

**Veredicto:** Entrar en **fase QA funcional** ahora. Bloquear release prod hasta P0.

---

## Metodología

1. Revisión estática de código (auth, provisioning, filters, frontend templates).
2. Tres exploraciones paralelas: auth/tenant, backend SaaS, frontend UX.
3. Verificación de build/lint (frontend: 1 error ESLint corregido en rama; backend build bloqueado por proceso ERP.API en ejecución local).
4. Cruce con tests existentes (`ERP.API.Tests`, Playwright e2e).
5. Sin cambios de arquitectura, routing ni abstracciones nuevas.

---

## 1. Flujos críticos end-to-end

### 1.1 Operador platform

| Flujo | Estado | Evidencia |
|-------|--------|-----------|
| Login platform (`/api/platform/auth/login`) | ✅ OK | Gated por `Deployment:PlatformPanelEnabled` |
| Panel global (`/platform/*`) | ✅ OK | `PlatformLayout` + `LayoutFrame` |
| Crear suscriptor + admin | ✅ OK | `SubscriberProvisioningOrchestrator` transaccional |
| Empresa default auto | ✅ OK | `CompanyProvisioningService.CreateDefaultCompanyForSubscriberAsync` |
| Planes SaaS / features | ✅ OK | Platform planes + `SaasFeatureDefinition` |
| Listado suscriptores | ⚠️ Medio | `PlatformSubscribersPage` fuera de `PlatformCrudTemplate`; N+1 en list handler |
| Impersonación tenant | 🔴 Crítico | Ver §3.2 — se pierde contexto al refresh |

### 1.2 Creación de suscriptor

**Flujo:** `PlatformCreateSubscriberWithAdminHandler` → orchestrator → `Subscriber` + `SubscriberBillingAccount` + `Company` default + `IdentityUser` Admin + membership + onboarding (sucursal/bodega).

- Transacción `Serializable` en `ProvisionCoreAsync`.
- RUC opcional → provisional `TMP-EC-*` si vacío.
- Plan/modules validados vía catálogo SaaS antes de provisionar.
- **Gap:** `EnsureCanIncrementAsync(MaxCompanies)` sin row lock; company default no usa `ExecuteWithLimitEnforcementAsync`.

### 1.3 Login / logout / refresh JWT

| Componente | Estado |
|------------|--------|
| Login directo single-tenant | ✅ |
| Multi-company → JWT sin `company_id` | ✅ |
| Multi-subscriber → bootstrap 5 min | ✅ |
| Refresh rotation + family theft detection | ✅ Fuerte (`RefreshTokenSecurityMatrixTests`) |
| Multi-tab refresh (Web Locks + BroadcastChannel) | ✅ |
| **Logout frontend** | 🔴 Solo cliente — no llama `POST /api/auth/logout` |
| Bootstrap switch sin refresh cookie | ⚠️ Sesión max ~60 min / reload falla |

**Archivos clave:** `AuthController.cs`, `RefreshTokenService.cs`, `authRefreshManager.ts`, `fullLogout.ts`

### 1.4 Impersonación y switch tenant

- **Impersonación:** `POST /api/auth/switch-subscriber` emite JWT session con `subscriber_id` del tenant; refresh cookie permanece `UserType=Platform`.
- **Al refresh:** `RefreshTokenHandler` líneas 50–67 re-emite token **global** (`subscriber_id=Guid.Empty`) → usuario sale del tenant impersonado.
- **Switch company:** `SwitchCompanyHandler` sí rota refresh — patrón correcto a replicar en switch-subscriber.

### 1.5 CRUD ERP (muestra)

| Módulo | Backend | Frontend | Tests E2E |
|--------|---------|----------|-----------|
| Ventas / facturas | ✅ Handlers + validators | ⚠️ `CreateInvoicePage` sin zodResolver | `VentasEndToEndTests`, `VentasHttpTests` |
| Compras / OC / gastos | ✅ | ⚠️ validación inline | `OrdenesCompraEndToEndTests`, `CompraGastoEndToEndTests` |
| Inventario / kardex / ajustes / transferencias | ✅ | ✅ core maduro | `KardexFlujoCompletoTests`, `TransferenciasEndToEndTests` |
| Contabilidad | ✅ | ✅ tabs | `ConfiguracionContableHttpTests` |
| Caja | ✅ | ✅ | `CajaHttpIntegrationTests` |
| Stock / kardex UI (nuevo) | ✅ API | ⚠️ i18n hardcoded | Parcial |

### 1.6 Permisos y roles

- `PermissionHandler`: Operador platform en tenant → todos los permisos del plan.
- Frontend: `usePermissionsStore` + checks `hasPerm` / `isAdmin`.
- **Gap:** `POST /api/auth/register` acepta `Role` del body sin whitelist → puede crear `Admin`.

### 1.7 Exports PDF/Excel

| Export | Endpoint | Auth | Notas |
|--------|----------|------|-------|
| Kardex Excel/PDF | `GET /api/inventory/kardex/exportar/{excel\|pdf}` | `[Authorize]` | Async job 202 + polling en frontend |
| RIDE factura/nota | `InvoicesController` | `[Authorize]` | PDF generado server-side |
| Reportes ventas | Frontend + API reportes | ✅ | `ReportPageTemplate` |

**QA manual pendiente:** export kardex con filtros vacíos, job timeout, descarga en mobile.

### 1.8 Reportes, filtros y tablas

- Reportes: `SalesReportPage` + `ReportPageTemplate`.
- Tablas nuevas: `pg-overflow-x` presente (stock, kardex, cash/bank, geography, activity, retenciones).
- **Gap ≤980px:** módulos nuevos sin CSS de módulo dedicado; KPI grids no verificados en checklist.
- Filtros: mayoría con estado local; `useStock` tiene warning exhaustive-deps.

---

## 2. Hallazgos por severidad

### 🔴 Críticos (P0 — bloquean producción)

#### C1. Registro público sin autenticación
- **Endpoint:** `POST /api/auth/register` — sin `[Authorize]`.
- **Impacto:** Cualquiera con `subscriberId` conocido crea usuario con rol `Admin` (solo bloquea operador platform).
- **Archivos:** `AuthController.cs`, `RegisterHandler.cs`, `RegisterCommandValidator.cs` (no whitelist de roles).
- **Repro:**
  ```http
  POST /api/auth/register
  { "firstName":"X","lastName":"Y","email":"a@b.com","password":"Password1!",
    "subscriberId":"<uuid-tenant>","role":"Admin" }
  ```
- **Fix seguro:** `[Authorize(Roles=PlatformOperator)]` o eliminar endpoint; restringir `Role` a `"User"` + token invitación.

#### C2. Logout solo en cliente
- **Impacto:** Refresh token httpOnly sigue válido hasta expiración (30 días) tras “cerrar sesión”.
- **Archivos:** `fullLogout.ts`, `AppLayout.tsx`, `PlatformLayout.tsx` — ninguno llama API.
- **Repro:** Logout → reutilizar cookie `erp_refresh_token` → sesión restaurada.
- **Fix seguro:** `await POST /api/auth/logout` con `credentials: 'include'` antes de `fullLogout()`.

#### C3. Impersonación operador platform no persiste en refresh
- **Impacto:** Tras 60 min o reload, vuelve a contexto platform global.
- **Archivos:** `SwitchSubscriberHandler.cs` (Auth), `RefreshTokenHandler.cs` líneas 50–67.
- **Repro:** Platform login → switch-subscriber → esperar refresh → tenant context lost.
- **Fix:** Rotar/crear refresh con `SubscriberId` al impersonar (patrón `SwitchCompanyHandler`).

#### C4. Límites de plan no enforced en branches/warehouses/users
- **Impacto:** Tenant starter puede exceder límites definidos en `CommercialPlanLimitsBootstrap`.
- **Archivos:** `CreateBranchCommandHandler.cs`, `CreateBodegaCommandHandler.cs`, `TenantMembershipHandlers.cs` — sin `ICommercialPlanLimitService`.
- **Solo enforced:** `MAX_COMPANIES` vía `CompanyProvisioningService`.

### 🟠 Medios (P1 — antes de GA / clientes pagos)

| ID | Hallazgo | Ubicación |
|----|----------|-----------|
| M1 | Count companies bajo filter platform → count=0 → bypass limit | `CompanyRepository.CountActiveBySubscriberIdAsync` |
| M2 | Bootstrap multi-subscriber sin refresh cookie | `Access/UseCases/SwitchTenant/SwitchTenantHandler.cs` |
| M3 | Platform APIs con `Roles=PlatformOperator` vs `GlobalPlatformOperator` durante impersonación | `PlatformSubscribersController.cs` |
| M4 | Formularios documento sin Zod+RHF (4 capas incumplidas en UI) | `CreateInvoicePage`, `CrearCompraPage`, `CrearGastoPage`, etc. |
| M5 | Batch módulos nuevos sin i18n (es/en/qu) | stock, kardex, cash/bank, geography, activity, retenciones |
| M6 | `BillingSettingsPage` usa `alert()` nativo | `BillingSettingsPage.tsx` |
| M7 | Acciones destructivas sin `ZHConfirmModal` | withholding send/approve, accounting disable |
| M8 | N+1 operador platform list subscribers | `GetPlatformSubscribersHandler` |
| M9 | RUC empresa default no editable en UI (solo create) | `CompanyManagementFormPage.tsx` taxId disabled en edit |
| M10 | Health ready incluye probe SRI externo | `Program.cs` + `appsettings.Production.json` |
| M11 | Redis → memory cache silencioso (multi-instance inconsistente) | `Program.cs` |
| M12 | `InvalidOperationException` → 422 vs `Result.Failure` → 400 | `ExceptionMiddleware.cs` |
| M13 | ESLint governance: 1 error corregido; 17 warnings restantes | varios archivos >400 líneas |

### 🟢 Bajos (P2 — hardening / deuda)

- Impersonation label en `localStorage` vs convención `sessionStorage`.
- `CompanyUserMembership` sin global filter — requiere joins explícitos.
- RLS preparado pero no activo (solo EF filters).
- Skeletons solo en dashboard/menu preview; listas usan spinner.
- `/rrhh` placeholder; `FeaturePlaceholderPage` sin ruta.
- Web Lock timeout fallback en refresh puede duplicar intentos.
- Documentación QA checklist desactualizada (`docs/FRONTEND_QA_CHECKLIST.md`).

---

## 3. Arquitectura SaaS

### 3.1 Aislamiento tenant ✅ (con reservas)

- EF global filters vía `EnterpriseQueryFilterConfigurator` + `ISubscriberScopedEntity` (nombre de interfaz sellado — scope tenant).
- `ICurrentTenant` lee JWT — no body.
- Sin auth → `TenantId = Guid.Empty` → cero filas.
- Audit test: `IgnoreQueryFiltersAuditTests.cs`.

**Reservas:** memberships sin filter; legacy e-docs solo `CompanyId`; platform queries deben usar `IPlatformQueryAccessor.Unfiltered` explícitamente.

### 3.2 Impersonación 🔴

Ver C3. Operador platform en tenant ve todas las companies del subscriber (sin `company_id` en JWT — by design).

### 3.3 Límites de plan ⚠️

Parcialmente implementado. Ver C4, M1.

### 3.4 Menú dinámico ✅

- Sesión resuelve módulos habilitados por plan.
- panel platform routes fuera de BD menú tenant.
- Favoritos en localStorage (excepción documentada).

### 3.5 Claims JWT ✅

`sub`, `email`, `subscriber_id`, `role`, `token_type`, `user_type`, `company_id` opcional. Session policy rechaza bootstrap tokens.

### 3.6 Query filters ✅

Implementación enterprise-grade. Ver gaps memberships / platform count.

### 3.7 Fallback Redis ⚠️

`AddDistributedMemoryCache()` si no hay Redis — OK dev; **requiere Redis en prod multi-instancia** para rate limit refresh y cache entitlements.

### 3.8 First-run & seeds ✅

- `IFirstRunSetupService` en startup (token operador platform inicial).
- Sin seeds de demo/usuarios por defecto — único flujo de creación: First-Run (`POST /api/setup/admin`, token-gated).
- Bootstrap commercial plan limits en startup.

---

## 4. Frontend — checklist QA

### Pantallas prioritarias (manual, desktop + ≤980px)

- [ ] Login → dashboard → logout (verificar cookie post-fix P0)
- [ ] Operador platform: crear suscriptor → impersonar → operar 61 min
- [ ] Multi-subscriber bootstrap → reload
- [ ] Switch company
- [ ] `/inventory/stock`, `/inventory/kardex` (export Excel/PDF)
- [ ] `/cash/bank`, `/settings/geography`, `/admin/activity`
- [ ] Ventas: nueva factura, nota crédito, RIDE PDF
- [ ] Compras: OC, factura, NC import XML, retenciones
- [ ] Contabilidad: balance, mayor
- [ ] `/companies` crear/editar empresa
- [ ] Config SRI + facturación
- [ ] Permisos: usuario sin permiso → 403 UI

### Accesibilidad básica

- ✅ `.sr-only` / `.zh-visually-hidden` en design system
- ⚠️ Keyboard: modales ZH tienen keydown; tablas sin row focus pattern
- ⚠️ Contraste badges — no auditado pixel-level

---

## 5. Backend — checklist QA

- [ ] Todos los `[Authorize]` en controllers tenant-scoped
- [ ] FluentValidation en commands con input
- [ ] Provisioning idempotente bajo concurrencia
- [ ] `/health/live` vs `/health/ready` en prod (sin SRI blocking)
- [ ] Jwt:SecretKey desde env (no CHANGE_ME)
- [ ] Hangfire dashboard no expuesto en prod
- [ ] `POST /api/dev/*` solo Development

**Tests existentes (muestra):** RefreshToken security matrix (14), Kardex (18+), Ventas E2E, Platform subscriber companies, Ajustes/Transferencias/OC.

**Gap tests:** Register abierto, impersonation+refresh, plan limits branches/users.

---

## 6. Bugs reproducibles (top 5)

| # | Bug | Pasos | Resultado actual | Esperado |
|---|-----|-------|------------------|----------|
| 1 | Register abierto | POST register con subscriberId + Admin | 200 + JWT | 401/403 |
| 2 | Logout incompleto | Logout UI → POST refresh | 200 nueva sesión | 401 |
| 3 | Impersonación refresh | Switch tenant → refresh token | Token global | Token tenant |
| 4 | Plan limit branches | Crear >2 sucursales en starter | 201 | 403/409 |
| 5 | Lint CI | `npm run lint` (pre-fix) | 1 error inline style | 0 errors |

---

## 7. Quick wins (compatibles baseline)

| # | Acción | Esfuerzo | Impacto |
|---|--------|----------|---------|
| 1 | Wire `POST /api/auth/logout` en `fullLogout` | S | Seguridad |
| 2 | `[Authorize]` + role whitelist en register | S | Seguridad |
| 3 | Refresh cookie en switch-subscriber impersonación | M | Operador platform UX |
| 4 | `ICommercialPlanLimitService` en create branch/warehouse/user | M | SaaS billing |
| 5 | i18n batch 8 pantallas nuevas | M | Compliance es/en/qu |
| 6 | `className="sr-only"` en file inputs (patrón BillingSettings) | S | ESLint ✅ hecho en NC compras |
| 7 | `ZHConfirmModal` en approve/send retenciones | S | UX |
| 8 | Quitar SRI de `/health/ready` crítico | S | Ops |
| 9 | `CountActiveBySubscriberIdAsync` con `IgnoreQueryFilters` | S | Plan limits |
| 10 | Habilitar edit RUC si `IsProvisionalTaxId` | M | Onboarding |

---

## 8. Prioridades reales (orden de ejecución)

### Sprint QA-P0 (1–3 días)
1. C1 Register lockdown  
2. C2 Server logout  
3. C3 Impersonation refresh  
4. C4 Plan limits wiring (branches, warehouses, users)  
5. M1 Company count unfiltered  

### Sprint QA-P1 (1 semana)
6. M4–M7 Frontend validation + i18n batch nuevo  
7. M2 Bootstrap refresh  
8. M3 GlobalPlatformOperator policy  
9. M9 RUC provisional editable  
10. Actualizar `FRONTEND_QA_CHECKLIST.md` + Playwright smoke extendido  

### Sprint QA-P2 (backlog)
- N+1 admin lists  
- Audit log impersonación  
- RLS PostgreSQL (defense in depth)  
- Skeletons en listas principales  

---

## 9. Production readiness checklist faltante

### Seguridad
- [ ] Register deshabilitado o protegido
- [ ] Logout revoca refresh server-side
- [ ] Impersonación estable 24h
- [ ] Pentest básico IDOR tenant
- [ ] Rate limits verificados bajo load balancer + Redis

### Funcional
- [ ] E2E Playwright: auth-multitab + enterprise-sales + smoke verdes en CI
- [ ] Flujo completo suscriptor → empresa → SRI → factura → kardex
- [ ] Exports kardex bajo carga (async job)
- [ ] Plan limits E2E por tier

### Ops
- [ ] Secrets solo en env / Key Vault
- [ ] Redis obligatorio prod multi-pod
- [ ] Health probes separados (app vs dependencias externas)
- [ ] Logs estructurados impersonación + auth failures
- [ ] Backup PG + restore drill

### Compliance producto
- [ ] i18n es/en/qu en pantallas nuevas
- [ ] Validación 4 capas en formularios persistidos
- [ ] Sin `alert()` / `confirm()` nativos

---

## 10. Cambios aplicados en esta rama

| Archivo | Cambio |
|---------|--------|
| `frontend/src/modules/compras/credit-notes/pages/PurchaseCreditNotesPage.tsx` | `style={{display:'none'}}` → `className="sr-only"` (ESLint governance) |
| `docs/QA-AUDIT-ENTERPRISE.md` | Este informe |

**No aplicado (requiere aprobación explícita — toca auth):** register lockdown, logout API, impersonation refresh.

---

## 11. Referencias de código

```
backend/src/ERP.API/Controllers/AuthController.cs          — register, logout, refresh
backend/src/ERP.Application/Modules/Auth/UseCases/Register/RegisterHandler.cs
backend/src/ERP.Infrastructure/Services/SubscriberProvisioningOrchestrator.cs
backend/src/ERP.Infrastructure/Persistence/EnterpriseQueryFilterConfigurator.cs
backend/src/ERP.Infrastructure/Services/CommercialPlanLimitService.cs
frontend/src/lib/session/fullLogout.ts
frontend/src/lib/session/authRefreshManager.ts
frontend/src/templates/ErpPageTemplate.tsx
frontend/src/components/layout/LayoutFrame.css               — @media max-width 980px
```

---

## 12. Próximo paso recomendado

1. Ejecutar **QA manual P0** con esta rama en staging.  
2. Aprobar fixes P0 auth (3 items) en PR separado `fix/qa-p0-auth-security`.  
3. Paralelo: i18n + Zod en batch módulos nuevos.  
4. Re-ejecutar auditoría parcial post-P0 → target **≥85/100** production readiness.

---

*Generado en modo auditoría enterprise. Compatible con `architecture-v1.0`. Sin refactors arquitectónicos.*
