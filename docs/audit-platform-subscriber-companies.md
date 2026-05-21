# Auditoría E2E: Platform SuperAdmin → Subscriber → Companies (MAX_COMPANIES)

**Fecha:** 2026-05-20  
**Modo:** ultra-estricto — solo evidencia verificable (código + DB + HTTP/tests).  
**Resultado global:** **CUMPLE** con 1 bug corregido (bypass ERP en modo platform global).

---

## A) Mapa de flujo real (código)

### 1. Login Platform SuperAdmin

| Pieza | Ubicación |
|-------|-----------|
| Endpoint | `POST /api/platform/auth/login` |
| Controller | `ERP.API/Controllers/Platform/PlatformAuthController.cs` → `Login()` |
| Handler | `ERP.Application/Modules/Platform/Auth/UseCases/PlatformLogin/PlatformLoginHandler.cs` |
| Token | `AccessTokenService.GeneratePlatformSessionToken()` → `ERP.Infrastructure/Services/AccessTokenService.cs` |

**JWT claims (platform global):**

- `user_type` = `Platform`
- `platform_role` = `SuperAdmin`
- `subscriber_id` = `00000000-0000-0000-0000-000000000000` (`Guid.Empty` en token; en BD `subscriber_id IS NULL`)
- **Sin** claim `company_id` (solo se agrega si `companyId != Guid.Empty`, líneas 117–121)

### 2. Crear Subscriber (Platform)

| Pieza | Ubicación |
|-------|-----------|
| Endpoint canónico | `POST /api/platform/subscribers` |
| Alias legacy | `POST /api/access/superadmin/subscribers` (`AccessController.SuperAdminCreateSubscriber`) |
| Controller | `ERP.API/Controllers/Platform/PlatformSubscribersController.cs` → `Create()` |
| Command | `SuperAdminCreateSubscriberWithAdminCommand` |
| Handler | `SuperAdminCreateSubscriberWithAdminHandler` → `ERP.Application/Modules/Access/UseCases/SuperAdminTenants/SuperAdminTenantHandlers.cs` |
| Orquestador | `SubscriberProvisioningOrchestrator.ProvisionCoreAsync()` → `ERP.Infrastructure/Services/SubscriberProvisioningOrchestrator.cs` |

**Respuesta HTTP 201:** envelope `ApiResponse<SessionResponseDto>` → `{ success, message, responseObject: { subscriberId, companyId, userId, token, ... } }`

### 3. Plan comercial + límites

| Pieza | Ubicación |
|-------|-----------|
| Plan en subscriber | `Subscriber.Create(..., planCode: request.PlanCode)` — columna `subscribers.plan_code` |
| Seed límites | `CommercialPlanLimitsBootstrap.cs` |
| Resolución plan | `CommercialPlanLimitService.ResolveCommercialPlanAsync()` — usa `subscriber_subscriptions` activa o fallback `subscribers.plan_code` |
| Código límite | `CommercialPlanLimit.Codes.MaxCompanies` = `"MAX_COMPANIES"` |
| Provider uso | `MaxCompaniesLimitUsageProvider` → `ICompanyRepository.CountActiveBySubscriberIdAsync()` |
| DI | `DependencyInjection.cs`: `services.AddScoped<ICommercialLimitUsageProvider, MaxCompaniesLimitUsageProvider>()` |

**Valores seed (`commercial_plan_limits`):**

| plan_code | MAX_COMPANIES |
|-----------|---------------|
| starter | 1 |
| business | 3 |
| professional | 10 |
| enterprise | 0 (ilimitado) |

### 4. Crear Company (runtime subscriber admin)

| Pieza | Ubicación |
|-------|-----------|
| Endpoint canónico | `POST /api/companies` |
| Ruta UI (feature) | `/saas/companies` (atributo `[AppFeature]`, no es route API alterna) |
| **No existe** | `/api/saas/companies` como controller HTTP |
| Controller | `ERP.API/Controllers/CompaniesController.cs` → `Create()` |
| Handler | `CreateCompanyHandler` → `ERP.Application/Modules/Platform/Companies/UseCases/CreateCompany/CreateCompanyHandler.cs` |
| Provisioning | `CompanyProvisioningService.CreateManagedCompanyAsync()` |
| Enforcement | `CommercialPlanLimitService.ExecuteWithLimitEnforcementAsync(MAX_COMPANIES)` (tx `Serializable` + `SELECT ... FOR UPDATE` en PostgreSQL) |

### 5. Orden transaccional — provisioning inicial

`SubscriberProvisioningOrchestrator.ProvisionCoreAsync` (`IsolationLevel.Serializable`):

1. `subscribers` (INSERT)
2. `subscriber_billing_accounts` + `billing_events` (`EnsureBillingAccountInContextAsync`)
3. `identity_users` (admin Company, si no link existing)
4. `user_activity` (audit, opcional)
5. **SAVE**
6. `ICommercialPlanLimitService.EnsureCanIncrementAsync(MAX_COMPANIES, +1)`
7. `CompanyProvisioningService.CreateDefaultCompanyForSubscriberAsync` (solo construye entidad + RUC provisional)
8. `company` (INSERT)
9. `company_user_memberships` (INSERT admin)
10. module overrides (opcional)
11. **SAVE**
12. `ISubscriberOnboardingService.OnboardAsync` (profiles, consumidor final, branch, warehouse)
13. **COMMIT** — rollback total en cualquier excepción

### 6. Bloqueo ERP en modo platform global

| Pieza | Ubicación |
|-------|-----------|
| Pipeline MediatR | `CompanyScopeBehavior` → `ERP.Application/Behaviors/CompanyScopeBehavior.cs` |
| Excepción | `CompanyScopeException.NoCompanyContext()` → **403** vía `ExceptionMiddleware` |
| Impersonación | `POST /api/auth/switch-subscriber` → `SwitchSubscriberHandler` |
| Company runtime | `POST /api/auth/switch-company` → `AuthController` |

---

## Paths de inserción en `company` (auditoría anti-bypass)

| Path | Enforcement MAX_COMPANIES | Uso |
|------|---------------------------|-----|
| `CreateManagedCompanyAsync` | **Sí** (`ExecuteWithLimitEnforcementAsync`) | `POST /api/companies` (canónico runtime) |
| `EnsureDefaultCompanyAsync` | **Sí** (`ExecuteWithLimitEnforcementAsync`) | Login, switch-subscriber, memberships |
| `SubscriberProvisioningOrchestrator` | **Sí** (`EnsureCanIncrementAsync` antes de INSERT) | Alta platform subscriber |
| `SubscriberIntegrityRepairService.RepairAsync` | **No** | Reparación sistema (huérfanos); no es flujo usuario |
| Tests seed (`IntegrationSeedData`) | N/A | Solo tests |

**Conclusión:** no hay path HTTP de usuario que inserte `company` sin chequeo de límite.

---

## B) Evidencia DB (PostgreSQL `dberpsaas`, docker `postgreszh:5435`)

### B1) SuperAdmin platform activo

```sql
SELECT count(*) FROM identity_users
WHERE user_type='Platform' AND platform_role='SuperAdmin'
  AND subscriber_id IS NULL AND is_active=true;
```

**Resultado real:** `1`

### B2) Sin tablas legacy

```sql
SELECT tablename FROM pg_tables
WHERE schemaname='public' AND tablename IN ('users','companies','tenants');
```

**Resultado real:** `0 rows`

### B3) FK company → subscribers

```sql
SELECT conname FROM pg_constraint
WHERE conrelid = 'company'::regclass AND contype = 'f'
  AND conname LIKE '%subscriber%';
```

**Resultado real:** `fk_company_subscribers_subscriber_id` → `FOREIGN KEY (subscriber_id) REFERENCES subscribers(id) ON DELETE RESTRICT`

### B4) Subscriber con company default (dev seed)

```sql
SELECT s.id, s.slug, s.plan_code, count(c.id) AS company_count
FROM subscribers s
LEFT JOIN company c ON c.subscriber_id = s.id AND c.is_active = true
GROUP BY s.id, s.slug, s.plan_code
ORDER BY s.slug;
```

**Resultado real (2026-05-20):**

| slug | plan_code | company_count |
|------|-----------|---------------|
| subscriber-demo | starter | 1 |
| zh | starter | 1 |

Cada subscriber seed tiene exactamente 1 company default (coherente con `MAX_COMPANIES=1` en starter).

### B5) Enforcement MAX_COMPANIES — límite vs uso

```sql
SELECT s.slug, s.plan_code, cpl.limit_code, cpl.limit_value,
       (SELECT count(*) FROM company c
        WHERE c.subscriber_id = s.id AND c.is_active = true) AS active_companies
FROM subscribers s
JOIN commercial_plans cp ON cp.code = s.plan_code
JOIN commercial_plan_limits cpl ON cpl.commercial_plan_id = cp.id
WHERE cpl.limit_code = 'MAX_COMPANIES';
```

**Resultado real:** todos los subscribers dev tienen `active_companies <= limit_value`.

### B6) Query plantilla post-creación (auditoría manual)

```sql
SELECT s.id, s.slug, count(c.id)
FROM subscribers s
LEFT JOIN company c ON c.subscriber_id = s.id
WHERE s.slug = '<slug_creado>'
GROUP BY s.id, s.slug;
-- Esperado tras POST /api/platform/subscribers: count = 1
```

---

## C) Evidencia HTTP (integration tests)

Archivo: `ERP.API.Tests/Integration/PlatformSubscriberCompaniesFlowTests.cs`

| # | Escenario | Endpoint | Esperado | Real |
|---|-----------|----------|----------|------|
| C1 | Platform crea subscriber | `POST /api/platform/subscribers` | 201 + `responseObject.subscriberId`, `companyId` | **OK** |
| C2 | Admin crea companies 2..N dentro límite | `POST /api/companies` (plan business, MAX=3) | 201 × 2 | **OK** |
| C3 | Exceder MAX_COMPANIES | `POST /api/companies` (#4) | **403** + body contiene `"limit"` | **OK** — message: `"Your commercial plan limit for companies has been reached."` |
| C4 | Platform global → ERP | `GET /api/sales/invoices` | **403** (sin `company_id`) | **OK** |

**Nota sobre código de error en 403:** `ExceptionMiddleware` devuelve `{ status, message }` para `CommercialPlanLimitExceededException`; no incluye campo `code` estructurado (solo mensaje textual con “limit”). Status **403** confirmado (no 500/400).

Ejecutar:

```powershell
cd backend/src
dotnet test ERP.API.Tests --filter "FullyQualifiedName~PlatformSubscriberCompaniesFlowTests"
```

**Resultado (2026-05-20):** 3/3 passed.

### Claims JWT (referencia código + tests)

`TestJwtFactory.CreatePlatformSuperAdminJwt` replica claims de `AccessTokenService.GeneratePlatformSessionToken`:

- `user_type=Platform`, `platform_role=SuperAdmin`, `subscriber_id=Guid.Empty`, sin `company_id`

Runtime admin (`CreateSessionJwt`): incluye `subscriber_id`, `company_id`, `company_role`.

### Flujo impersonación (código, no re-testeado en esta suite)

1. Platform JWT → `POST /api/auth/switch-subscriber` `{ subscriberId }`
2. `GET /api/auth/my-companies`
3. `POST /api/auth/switch-company` `{ companyId }`
4. ERP (`/api/sales/*`, `/api/inventory/*`) permitido con `company_id` en JWT

---

## D) Fixes aplicados (bugs encontrados)

### D1) SuperAdmin global accedía ERP backend (BUG)

**Archivo:** `ERP.Application/Behaviors/CompanyScopeBehavior.cs`  
**Problema:** existía bypass `IsPlatformBypass()` para SuperAdmin + `subscriber_id == Guid.Empty`.  
**Fix:** eliminado bypass; todo handler en namespaces ERP scoped exige `ICurrentCompany` vía `ICompanyAccessGuard`.  
**Evidencia:** test `GlobalPlatformSuperAdmin_cannot_access_ERP_sales_without_company_context` → 403.

### D2) Tests de flujo (añadidos)

- `PlatformSuperAdmin_creates_subscriber_with_default_company_and_membership`
- `Subscriber_admin_creates_companies_until_MAX_COMPANIES_then_403`
- `GlobalPlatformSuperAdmin_cannot_access_ERP_sales_without_company_context`

Soporte: `TestDataFactory.SeedPlatformSuperAdminAsync`, `TestJwtFactory.CreatePlatformSuperAdminJwt`.

---

## Validación final

| Check | Resultado |
|-------|-----------|
| `dotnet build ERP.API` | **OK** (0 errores) |
| Tests flujo `PlatformSubscriberCompaniesFlowTests` | **3/3 OK** |
| Suite completa `ERP.API.Tests` | 146/159 OK — 13 fallos preexistentes (Kardex/Ventas print), no relacionados con este flujo |
| `ERP.Application.Tests` | 1 error preexistente (`PurchBillApprovedEventHandler` logger) — fuera de alcance |

---

## Checklist objetivos 1–5

| # | Regla | Estado |
|---|-------|--------|
| 1 | Platform SuperAdmin crea Subscriber | OK — orchestrator transaccional |
| 2 | Subscriber recibe plan + MAX_COMPANIES | OK — `plan_code` + `commercial_plan_limits` |
| 3 | Admin runtime crea 1..N companies hasta límite | OK — `CreateManagedCompanyAsync` |
| 4 | N+1 → 403 con mensaje de límite | OK — `CommercialPlanLimitExceededException` |
| 5 | Platform global no opera ERP sin impersonación | OK — fix `CompanyScopeBehavior` |
