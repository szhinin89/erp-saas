# Base de datos — PostgreSQL

PostgreSQL **15+**. ORM: **EF Core 10**. Schema único `public`, naming `snake_case`.

Relacionado: [ARCHITECTURE.md](./ARCHITECTURE.md), [IDENTITY.md](./IDENTITY.md), [DEVELOPMENT.md](./DEVELOPMENT.md).

---

## Principios

| Principio | Implementación |
|-----------|----------------|
| Schema | `public` |
| Clave Tenant | `tenant_id` |
| Clave ERP (objetivo) | `company_id` |
| Migraciones | Solo EF Core |
| Seguridad fila | Filtros EF (RLS: futuro, no implementado — ver sección RLS) |

```
tenants
  └── company (1:N)
        ├── company_user_memberships
        └── tablas ERP → company_id
```

> Ramas históricas eliminadas de este diagrama (`subscriber_subscriptions`, `subscriber_billing_accounts`, `saas_billing_*`/`commercial_plan_*`) — no existen en el esquema actual. Ver [Suscripciones y billing SaaS](#suscripciones-y-billing-saas-histórico-eliminado-fase-1) más abajo.

### EF Core

| Artefacto | Ruta |
|-----------|------|
| DbContext | `ERP.Infrastructure/Persistence/ErpDbContext.cs` |
| Configurations | `Persistence/Configurations/**` |
| Interceptor | `PostgreSqlSessionContextInterceptor` |
| Snapshot | `Migrations/ErpDbContextModelSnapshot.cs` |

Filtros globales: `ITenantScopedEntity` (excepto raíz `Tenant`).

Conexión: `ConnectionStrings:DefaultConnection`. En Development, `Database.MigrateAsync()` al arrancar API.

| Entorno | Política |
|---------|----------|
| Development | Puede drop/recreate; baseline único |
| Staging / Production | Solo forward migrations |

---

## Migraciones

### Baseline EF (desarrollo)

Una sola migración: `20260929143218_InitialEnterpriseBaseline` (consolidación ZH-DB-FINAL-BASELINE-SQUASH-01, 2026-09-29). Una instalación nueva aplica solo esa migración; no existe upgrade-path desde cadenas anteriores (todas eran BD de desarrollo recreables).

Cambios de schema posteriores: solo migraciones forward con `dotnet ef migrations add`. No regenerar la baseline desde el modelo para "limpiar historial" sin copiar antes el raw SQL (tabla siguiente).

#### Objetos raw SQL (fuera del modelo EF)

El model snapshot no los conoce: una baseline regenerada con `dotnet ef migrations add` los omite en silencio. Viven al final del `Up` de la baseline y los vigilan `ERP.Architecture.Tests/RawSqlDatabaseObjectsSurviveMigrationSquashTests` (existen en las migraciones, y todo objeto raw está registrado) y `ERP.Infrastructure.Tests/Persistence/RawSqlDatabaseObjectsBaselineIntegrationTests` (existen físicamente en una BD recién migrada).

| Objeto | Tipo | Motivo |
|--------|------|--------|
| `uq_mbp_identification` | Índice único | ADR-BP-03; columna del owner + owned type, EF no lo expresa |
| `enforce_purchase_expense_exclusivity()` | Función plpgsql | Exclusividad Compra↔Gasto por AccessKey (ignora Cancelled) |
| `tr_expense_purchase_exclusivity` / `tr_purchase_expense_exclusivity` | Triggers | Invocan la función en `expense_documents` / `purchase_invoices` |
| `pg_trgm` | Extensión | `EF.Functions.TrigramsSimilarity` en Item Matching |
| `ix_items_short_name_trgm` / `ix_items_description_trgm` | Índices GIN trigram | Item Matching por similitud |

Reset local recomendado:

```powershell
.\scripts\db\dev-greenfield-reset.ps1
```

### SQL fuera de EF (mapa único)

| Ubicación | Rol |
|-----------|-----|
| `Migrations/*.cs` | Schema oficial (incluye `HasData()` de catálogos SRI globales) |
| `Seeding/Global/GlobalBootstrapOrchestrator.cs` + `Seeding/Global/Steps/*BootstrapStep.cs` | Único orquestador del bootstrap global (una vez por instalación, en cada arranque): `NavigationBootstrapStep` (navegación/permisos del Platform Kernel desde `KernelRegistry.cs`) e `InstallDataBootstrapStep` (geografía INEC inmutable, `Seeding/InstallData/001_*.sql`) |
| `Seeding/CompanyBootstrapOrchestrator.cs` + `Seeding/Steps/*BootstrapStep.cs` | Único orquestador del bootstrap por empresa nueva, un step por dominio (Organización, Documentos Electrónicos, Inventario, Ventas, Caja, Accesos) |
| `scripts/db/sql/002_unified_documents_*.sql` | Migración opcional documentos (`Documents:UseUnifiedSchema`) |

Detalle operativo: [`scripts/db/sql/README.md`](../scripts/db/sql/README.md).

### Naming schema

- FK/index: `_tenant_` no `_subscriber_` (índices existentes con `_subscriber_` en su nombre son residuo legacy — la columna real ya es `tenant_id`, ver "Trampas de nombres")
- SRI empresa: `billing_settings` (no es billing SaaS)

---

## RLS

> 🚧 **Futuro / no implementado (verificado 2026-07-23).** Row Level Security a nivel PostgreSQL **no existe** en el código actual: no hay `CREATE POLICY`, no hay `FORCE ROW LEVEL SECURITY`, y la clase `EnterpriseBaselineRowLevelSecurity` mencionada en revisiones anteriores de este documento **no existe** en el repositorio (verificado contra `ErpDbContextModelSnapshot.cs` y `InitialEnterpriseBaseline.cs`). El único mecanismo de aislamiento realmente activo hoy son los filtros globales de EF Core (`ITenantScopedEntity`/`ICompanyOperationalEntity`) — ver secciones "Principios" y "EF Core" arriba.

Lo que sí existe hoy (confirmado en código) es la infraestructura de variables de sesión por conexión, aunque **ninguna política RLS las consume todavía**:

| Variable | Propósito | Estado |
|----------|-----------|--------|
| `app.tenant_id` | Aislamiento tenant | ✅ Seteada en cada conexión (`DbSessionContextApplicator`) |
| `app.company_id` | Aislamiento company | ✅ Seteada en cada conexión (`DbSessionContextApplicator`) |
| `app.is_platform_admin` | Bypass operador platform (`'true'`) | No verificado en esta auditoría — no confirmar como implementado sin revisión adicional |

Componentes reales confirmados: `ISessionContext`, `HttpSessionContext`, `DbSessionContextApplicator`, `PostgreSqlSessionContextInterceptor`.

### Tablas con RLS (diseño futuro, no implementado)

Diseño propuesto para cuando se implemente RLS — **no ejecutar como si existiera**:

| Tabla | Lógica propuesta |
|-------|--------|
| `master_business_partners`, tablas de ventas/inventario | admin OR (`tenant_id` + `company_id` opcional) |
| `sales_invoice` | admin OR `company_id` |

### Jobs Hangfire

Antes de acceso BD: `JobTenantContext`, `JobCompanyContext` (ERP por company).

RLS (una vez implementado) **no reemplazaría** `CompanyScopeBehavior`, filtros EF, guards JWT — estos ya son el mecanismo real vigente.

| Síntoma | Revisar |
|---------|---------|
| Resultado vacío tenant | `ICurrentTenant` / filtros EF (no RLS — aún no existe) |
| Job sin filas | contexto job (`JobTenantContext`/`JobCompanyContext`) |
| Operador platform bloqueado | `IPlatformQueryAccessor` / guards de autorización |

---

## Referencia de tablas

**Scope** = clave hoy. **Target** = post Phase 6.

### Platform e IAM

| Tabla | Propósito | Scope |
|-------|-----------|-------|
| `tenants` | Contenedor multiempresa (raíz) | tenant |
| `company` | Empresa fiscal | `tenant_id` |
| `company_user_memberships` | Usuario ↔ company | `company_id` |
| `identity_users` | Identidad única | platform / hint tenant |
| `refresh_tokens` | Refresh sesión | user |
| `access_profiles` | Perfiles rol | `tenant_id` |
| `user_activity` | Auditoría | `tenant_id` |

### Suscripciones y billing SaaS (histórico, eliminado FASE 1)

Ver [`docs/archive/SAAS-COMMERCIAL.md`](./archive/SAAS-COMMERCIAL.md): `commercial_plans*`, `subscriber_subscriptions`, `saas_billing_*`, `payment_provider_*`. Tablas no presentes en el esquema actual.

### ERP — ventas, compras, inventario, contabilidad

| Área | Tablas (ej.) | Scope hoy | Target |
|------|--------------|-----------|--------|
| Ventas | `master_business_partners`, `sales_bill`, `sales_invoice`, `sales_note` | `tenant_id` | `company_id` |
| Compras | `purchase_order`, `purch_bill`, `suppliers` | `tenant_id` | `company_id` |
| Inventario Wave 1 | items/warehouse/stock (nombres físicos reales no verificados en esta auditoría) | `tenant_id` + `company_id` (Wave 1 completada, ver `STATUS.md`) | `company_id` |
| Contabilidad / caja | `accounts`, `journal_entries`, `bank_account` | `tenant_id` | `company_id` |

### Configuración

| Tabla | Nota |
|-------|------|
| `billing_settings` | SRI RIDE — **no** SaaS billing |
| `tenant_custom_menus` | Menú JSON por tenant |

### Trampas de nombres

| Nombre | Significado |
|--------|-------------|
| `billing_settings` | Config FE empresa |
| `saas_billing_invoices` | Facturas plataforma — **no existe** en el esquema actual (Control Plane SaaS eliminado FASE 1, ver [`docs/archive/SAAS-COMMERCIAL.md`](./archive/SAAS-COMMERCIAL.md)) |
| `tenants` | Contenedor multiempresa vigente — **no** `subscribers` (nombre retirado) |

Detalle columnas: `ErpDbContextModelSnapshot.cs` o `\d table` en psql.
