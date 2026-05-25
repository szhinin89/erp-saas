# Base de datos — PostgreSQL

PostgreSQL **15+**. ORM: **EF Core 10**. Schema único `public`, naming `snake_case`.

Relacionado: [ARCHITECTURE.md](./ARCHITECTURE.md), [IDENTITY.md](./IDENTITY.md), [DEVELOPMENT.md](./DEVELOPMENT.md).

---

## Principios

| Principio | Implementación |
|-----------|----------------|
| Schema | `public` |
| Clave SaaS | `subscriber_id` |
| Clave ERP (objetivo) | `company_id` |
| Migraciones | Solo EF Core |
| Seguridad fila | RLS Wave 1 + filtros EF |

```
subscribers
  ├── company (1:N)
  │     ├── company_user_memberships
  │     └── tablas ERP → company_id
  ├── subscriber_subscriptions
  ├── subscriber_billing_accounts
  └── saas_billing_* / commercial_plan_*
```

### EF Core

| Artefacto | Ruta |
|-----------|------|
| DbContext | `ERP.Infrastructure/Persistence/ErpDbContext.cs` |
| Configurations | `Persistence/Configurations/**` |
| Interceptor | `PostgreSqlSessionContextInterceptor` |
| Snapshot | `Migrations/ErpDbContextModelSnapshot.cs` |

Filtros globales: `ISubscriberScopedEntity` (excepto raíz `Subscriber`).

Conexión: `ConnectionStrings:DefaultConnection`. En Development, `Database.MigrateAsync()` al arrancar API.

| Entorno | Política |
|---------|----------|
| Development | Puede drop/recreate; baseline único |
| Staging / Production | Solo forward migrations |

---

## Migraciones

### Baseline EF (desarrollo)

Una sola migración: `20260525224928_InitialEnterpriseBaseline`. Detalle: [`backend/src/ERP.Infrastructure/Migrations/README.md`](../backend/src/ERP.Infrastructure/Migrations/README.md).

Cambios de schema posteriores: solo migraciones forward con `dotnet ef migrations add`.

Reset local recomendado:

```powershell
.\scripts\db\dev-greenfield-reset.ps1
```

### SQL fuera de EF (mapa único)

| Ubicación | Rol |
|-----------|-----|
| `Migrations/*.cs` | Schema oficial |
| `Seeding/InstallData/001_*.sql` | Geografía INEC inmutable |
| `Seeding/InstallData/002_*.sql` | Perfiles default + menú global EN |
| `scripts/db/sql/002_unified_documents_*.sql` | Migración opcional documentos (`Documents:UseUnifiedSchema`) |

Detalle operativo: [`scripts/db/sql/README.md`](../scripts/db/sql/README.md).

### Naming schema

- FK/index: `_subscriber_` no `_tenant_`
- Billing SaaS: `saas_billing_*`, `subscriber_billing_accounts`
- SRI empresa: `billing_settings` (no es billing SaaS)

---

## RLS

Complementa filtros en aplicación. Variables por conexión:

| Variable | Propósito |
|----------|-----------|
| `app.subscriber_id` | Aislamiento subscriber |
| `app.company_id` | Aislamiento company |
| `app.is_platform_admin` | Bypass operador platform (`'true'`) |

Componentes: `ISessionContext`, `HttpSessionContext`, `DbSessionContextApplicator`, `PostgreSqlSessionContextInterceptor`.

### Tablas con RLS (baseline)

Políticas `rls_{table}_enterprise`, `FORCE ROW LEVEL SECURITY`.

| Tabla | Lógica |
|-------|--------|
| `products`, `warehouse`, `stock_movement`, `current_stock`, `stock_transfer`, `stock_adjustment`, `sales_bill`, `sales_document` | admin OR (`subscriber_id` + `company_id` opcional) |
| `master_business_partners` | admin OR `subscriber_id` |
| `sales_invoice` | admin OR `company_id` |

SQL en `EnterpriseBaselineRowLevelSecurity.Apply()` al final de `InitialEnterpriseBaseline.Up()`.

### Jobs Hangfire

Antes de acceso BD: `JobSubscriberContext`, `JobCompanyContext` (ERP por company).

RLS **no reemplaza** `CompanyScopeBehavior`, filtros EF, guards JWT.

| Síntoma | Revisar |
|---------|---------|
| Resultado vacío tenant | variables sesión |
| Job sin filas | contexto job |
| Operador platform bloqueado | `app.is_platform_admin` |

---

## Referencia de tablas

**Scope** = clave hoy. **Target** = post Phase 6.

### Platform e IAM

| Tabla | Propósito | Scope |
|-------|-----------|-------|
| `subscribers` | Raíz SaaS | platform |
| `company` | Empresa fiscal | `subscriber_id` |
| `company_user_memberships` | Usuario ↔ company | `company_id` |
| `identity_users` | Identidad única | platform / hint subscriber |
| `refresh_tokens` | Refresh sesión | user |
| `access_profiles` | Perfiles rol | `subscriber_id` |
| `user_activity` | Auditoría | `subscriber_id` |

### Suscripciones y billing SaaS

Ver [SAAS-COMMERCIAL.md](./SAAS-COMMERCIAL.md): `commercial_plans*`, `subscriber_subscriptions`, `saas_billing_*`, `payment_provider_*`.

### ERP — ventas, compras, inventario, contabilidad

| Área | Tablas (ej.) | Scope hoy | Target |
|------|--------------|-----------|--------|
| Ventas | `master_business_partners`, `sales_bill`, `sales_invoice`, `sales_note` | `subscriber_id` + RLS | `company_id` |
| Compras | `purchase_order`, `purch_bill`, `suppliers` | `subscriber_id` | `company_id` |
| Inventario Wave 1 | `products`, `warehouse`, `stock_*` | subscriber + nullable `company_id` | `company_id` |
| Contabilidad / caja | `accounts`, `journal_entries`, `bank_account` | `subscriber_id` | `company_id` |

### Configuración

| Tabla | Nota |
|-------|------|
| `billing_settings` | SRI RIDE — **no** SaaS billing |
| `subscriber_custom_menus` | Menú JSON por subscriber |

### Trampas de nombres

| Nombre | Significado |
|--------|-------------|
| `billing_settings` | Config FE empresa |
| `saas_billing_invoices` | Facturas plataforma |
| `subscribers` | Tenant SaaS (no `tenants`) |

Detalle columnas: `ErpDbContextModelSnapshot.cs` o `\d table` en psql.
