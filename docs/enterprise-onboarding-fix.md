# Enterprise onboarding fix

> **Arquitectura de capas:** ver [platform-runtime-boundaries.md](./platform-runtime-boundaries.md) para Platform / IAM / ERP Runtime, rutas `/api/platform/*` y JWT context.

## Problema resuelto

El alta de empresas (SuperAdmin → Crear empresa) guardaba `Subscriber` + `IdentityUser` antes de crear `Company`. Si fallaba el insert en `company` (p. ej. `uq_company_ruc` por placeholder `0000000000000`), quedaban entidades huérfanas y reintentos mostraban *"El slug ya está en uso"*.

## Arquitectura (sin cambios de modelo SaaS)

```
Subscriber (SaaS / billing)
  └── Company (ERP / RUC global único)
        └── CompanyUserMembership → IdentityUser (auth)
```

## Cambios backend

### `SubscriberProvisioningOrchestrator`

Orquesta en **una transacción Serializable**:

1. Subscriber  
2. Billing account (`subscriber_billing_accounts`)  
3. Company (RUC validado globalmente)  
4. IdentityUser (si aplica)  
5. CompanyUserMembership Admin  
6. Module overrides (SuperAdmin)  
7. Onboarding (perfiles, sucursal, bodega)  

Si cualquier paso falla → **ROLLBACK total**.

Handlers refactorizados:

- `SuperAdminCreateSubscriberWithAdminHandler`
- `RegisterSubscriberWithAdminHandler`

### RUC provisional

- Eliminado placeholder fijo `0000000000000`.
- Sin RUC → `TMP-EC-{ShortGuid}` (único global).
- Nuevos campos en `company`:
  - `is_provisional_tax_id`
  - `tax_id_status` (`Pending` | `Verified` | `Invalid`)

### Errores API

| Código HTTP | Código | Cuándo |
|-------------|--------|--------|
| 409 Conflict | `company.ruc_already_exists` | RUC duplicado global |
| 422 | validación FluentValidation | Campos inválidos |
| 400 | negocio (slug, email, plan) | Reglas de dominio |

### Integridad (solo Development)

```http
POST /api/dev/repair-enterprise-integrity?repair=false   # scan
POST /api/dev/repair-enterprise-integrity?repair=true    # reparar
```

Detecta: subscriber sin company, company sin admin membership, billing huérfano.

## Migración EF

`20260521004512_EnterpriseOnboardingCompanyTaxId`

- `company.ruc` → `varchar(32)` (soporta provisional).
- Columnas `is_provisional_tax_id`, `tax_id_status`.
- SQL: filas legacy `0000000000000` → `TMP-EC-*` provisional.

```bash
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
```

## Frontend

Formulario SuperAdmin **Crear empresa** incluye:

- RUC (opcional)
- País (ISO-3, default `ECU`)
- Zona horaria (default `America/Guayaquil`)

## Tests

- `ERP.Application.Tests/Provisioning/ProvisionalTaxIdGeneratorTests.cs`
- `ERP.Infrastructure.Tests/Provisioning/CompanyProvisioningServiceTests.cs`

```bash
dotnet test backend/src/ERP.Application.Tests
dotnet test backend/src/ERP.Infrastructure.Tests
```

## Smoke manual

1. Crear empresa **sin RUC** → debe completar; `company.ruc` empieza con `TMP-EC-`.
2. Crear segunda empresa sin RUC → debe completar (RUC provisional distinto).
3. Crear con RUC ya usado → **409** `company.ruc_already_exists` (sin huérfanos).
4. Tras fallo, verificar que no hay subscriber huérfano (mismo slug reintetable solo si rollback OK).

## Reparar datos legacy

Si quedaron subscribers huérfanos de intentos anteriores:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5003/api/dev/repair-enterprise-integrity?repair=true"
```

O eliminar manualmente filas huérfanas en `subscribers` / `identity_users` según corresponda.
