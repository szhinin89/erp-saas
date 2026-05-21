# Auditoría: Platform → Subscriber → Companies (multiempresa)

**Fecha:** 2026-05-20  
**Alcance:** Validación end-to-end sin cambio de arquitectura. Corrección mínima de desalineaciones.

---

## Resumen ejecutivo

| Checklist | Resultado |
|-----------|-----------|
| A) SuperAdmin global | OK |
| B) Creación subscriber por SuperAdmin | OK (orquestador transaccional) |
| C) Multiempresa + MAX_COMPANIES | OK (403 al exceder) |
| D) SuperAdmin no opera ERP global | **Corregido** — bypass eliminado en `CompanyScopeBehavior` |
| E) Impersonación switch-subscriber/company | OK (código + rutas canónicas) |
| F) Integridad de datos | OK (tabla `company`, sin legacy) |

---

## Endpoints canónicos (y alias legacy)

| Paso | Canónico | Alias legacy |
|------|----------|--------------|
| Login platform | `POST /api/platform/auth/login` | — |
| First-run SuperAdmin | `POST /api/setup/superadmin` | — |
| Crear subscriber | `POST /api/platform/subscribers` | `POST /api/access/superadmin/subscribers` |
| Crear company | `POST /api/companies` | — |
| Listar companies del usuario | `GET /api/auth/my-companies` | — |
| Switch subscriber | `POST /api/auth/switch-subscriber` | `POST /api/access/switch-subscriber`, `POST /api/admin/iam/switch-subscriber` |
| Switch company | `POST /api/auth/switch-company` | — |
| ERP (ej. ventas) | `GET /api/sales/invoices` | — |

Envelope API: `{ success, message, responseObject }` (`ApiResponse<T>`).

---

## Servicios y handlers participantes

### B) Provisioning subscriber

```
PlatformSubscribersController.Create
  → SuperAdminCreateSubscriberWithAdminHandler
    → SubscriberProvisioningOrchestrator (tx Serializable)
        → subscribers
        → subscriber_billing_accounts + billing_events
        → ICommercialPlanLimitService.EnsureCanIncrementAsync(MAX_COMPANIES)
        → CompanyProvisioningService.CreateDefaultCompanyForSubscriberAsync
        → identity_users (admin Company)
        → company_user_memberships
        → module overrides + onboarding
```

### C) Crear company adicional

```
CompaniesController.Create
  → CreateCompanyHandler
    → ICompanyAccessGuard.RequireActiveSubscriberAsync
    → CompanyProvisioningService.CreateManagedCompanyAsync
        → ICommercialPlanLimitService.ExecuteWithLimitEnforcementAsync(MAX_COMPANIES)
```

Límite excedido: `CommercialPlanLimitExceededException` → `ExceptionMiddleware` → **403 Forbidden**.

### D) Scope ERP

```
MediatR pipeline → CompanyScopeBehavior
  → ICompanyAccessGuard (subscriber activo + company_id en JWT)
```

---

## Límites comerciales (seed)

| Plan | MAX_COMPANIES |
|------|---------------|
| starter | 1 |
| business | 3 |
| professional | 10 |
| enterprise | 0 (ilimitado) |

Fuente: `CommercialPlanLimitsBootstrap.cs`.

---

## Evidencia SQL (consultas de verificación)

Ejecutar contra PostgreSQL (`dberpsaas`):

```sql
-- A1) SuperAdmin platform global
SELECT id, email, user_type, platform_role, subscriber_id
FROM identity_users
WHERE user_type = 'Platform'
  AND platform_role = 'SuperAdmin'
  AND subscriber_id IS NULL;

-- F) Sin tablas legacy
SELECT tablename FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('users', 'companies', 'tenants');
-- Esperado: 0 filas

-- F) FK company → subscribers
SELECT conname FROM pg_constraint
WHERE conrelid = 'company'::regclass AND contype = 'f';

-- C) Conteo companies por subscriber (antes/después de POST /api/companies)
SELECT subscriber_id, COUNT(*) AS company_count
FROM company
WHERE subscriber_id = :subscriber_id
GROUP BY subscriber_id;

-- B) Membership + billing tras provisioning
SELECT COUNT(*) FROM subscriber_billing_accounts WHERE subscriber_id = :subscriber_id;
SELECT COUNT(*) FROM company_user_memberships m
JOIN company c ON c.id = m.company_id
WHERE c.subscriber_id = :subscriber_id;
```

**Nota:** En sesión de auditoría previa, dev DB confirmó SuperAdmin platform, tablas `company`/`subscribers`, ausencia de `users`/`companies`.

---

## Hallazgo y fix aplicado

### D) SuperAdmin operaba ERP en modo global (backend)

**Problema:** `CompanyScopeBehavior` incluía `IsPlatformBypass()` que permitía a SuperAdmin con `subscriber_id == Guid.Empty` saltarse el scope de company en handlers ERP (Sales, Inventory, etc.).

**Fix mínimo:** Eliminado el bypass en `backend/src/ERP.Application/Behaviors/CompanyScopeBehavior.cs`. Todo request ERP scoped exige contexto subscriber activo + `company_id` (o membership explícita).

**Comportamiento esperado post-fix:** JWT platform global → `GET /api/sales/invoices` → **403** (`CompanyScopeException`).

El frontend ya bloqueaba rutas ERP en modo global (`ProtectedRoute.tsx`); el backend quedó alineado.

---

## Tests de integración añadidos

Archivo: `ERP.API.Tests/Integration/PlatformSubscriberCompaniesFlowTests.cs`

1. `PlatformSuperAdmin_creates_subscriber_with_default_company_and_membership`
2. `Subscriber_admin_creates_companies_until_MAX_COMPANIES_then_403`
3. `GlobalPlatformSuperAdmin_cannot_access_ERP_sales_without_company_context`

Ejecutar:

```powershell
dotnet test ERP.API.Tests --filter "FullyQualifiedName~PlatformSubscriberCompaniesFlowTests"
```

Resultado: **3/3 OK**.

---

## Flujo E) Impersonación (referencia)

1. SuperAdmin platform → `POST /api/auth/switch-subscriber` con `{ subscriberId }`
2. JWT incluye `subscriber_id` real (no `Guid.Empty`)
3. `GET /api/auth/my-companies` → `POST /api/auth/switch-company` con `{ companyId }`
4. JWT incluye `company_id` → ERP runtime permitido

Handlers: `SwitchSubscriberHandler`, auth switch-company en `AuthController`.

---

## Respuesta provisioning (B)

`POST /api/platform/subscribers` → 201 con `responseObject`:

- `subscriberId`
- `companyId` (empresa default)
- `userId` (admin)
- `token` (sesión admin)

Rollback transaccional si falla cualquier paso del orchestrator.
