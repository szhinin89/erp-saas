# ARQUITECTURA — ERP SaaS ZH Technologies

Reglas de implementación → `CLAUDE.md` | Estado → `STATUS.md` | Funcionalidades → `FEATURES.md`

---

## Visión general

**Monolito modular + Clean Architecture.** Límites claros por dominio para poder extraer módulos como microservicios sin reescribir el dominio.

```
┌──────────────────────────────────────────────┐
│  ERP.API        Controllers / Middleware      │  HTTP, JWT, Swagger
├──────────────────────────────────────────────┤
│  ERP.Application  Handlers / DTOs            │  CQRS, MediatR, FluentValidation
├──────────────────────────────────────────────┤
│  ERP.Infrastructure  EF Core / Repos         │  PostgreSQL, Servicios externos
├──────────────────────────────────────────────┤
│  ERP.Domain     Entidades / Interfaces       │  Sin dependencias de frameworks
└──────────────────────────────────────────────┘
         ▲                     ▲
    React SPA              PostgreSQL 16
    (frontend/)            Redis 7
```

---

## Stack técnico

### Backend
| Herramienta | Versión / Uso |
|---|---|
| .NET SDK | 10.0.201+ (fijado en `backend/src/global.json`) |
| ASP.NET Core Web API | Host HTTP, controllers, middleware |
| MediatR | CQRS (commands / queries / handlers) |
| FluentValidation | Validación + `ValidationBehavior` en pipeline MediatR |
| EF Core 10 + Npgsql | Persistencia y migraciones (PostgreSQL) |
| JWT Bearer | Autenticación de sesión |
| BCrypt.Net-Next | Hash/verify contraseñas (solo en Infrastructure) |
| Serilog | Logging estructurado (Console + File) |
| Swashbuckle | Swagger / OpenAPI |
| Hangfire + Hangfire.PostgreSql | Jobs de background |
| QuestPDF + RazorLight + ClosedXML | Exportes PDF / Excel |

### Frontend
| Herramienta | Uso |
|---|---|
| React 19 + TypeScript | UI SPA |
| Vite 8 | Dev server y build |
| React Router | Ruteo |
| Zustand | Estado global (auth, permisos) |
| Axios | Cliente HTTP (interceptor JWT + refresh automático) |
| React Hook Form + Zod | Formularios y validación |
| Recharts | Gráficos |

### Infraestructura
| Herramienta | Uso |
|---|---|
| PostgreSQL 16 | Base principal (`dberpsaas`, puerto 5435 en dev) |
| Redis 7 | Cache distribuida (puerto 6379; fallback `MemoryCache` si cadena vacía) |
| Docker Compose | Orquestación local (`docker-compose.yml`) |
| GitHub Actions | CI — `ci.yml`: backend + frontend + Playwright |

---

## Estructura de capas del backend

### Responsabilidades por proyecto

| Proyecto | Qué hace | Qué no hace |
|----------|----------|-------------|
| `ERP.Domain` | Entidades, VOs, interfaces de repos, eventos, enums, reglas | Depender de EF Core / ASP.NET |
| `ERP.Application` | Handlers, Commands/Queries, DTOs, Validators | Acceder a HTTP, BD directo, UI |
| `ERP.Infrastructure` | Repos concretos, DbContext, servicios técnicos | Reglas de negocio |
| `ERP.API` | Controllers delgados, Middleware, Swagger | Lógica de negocio; entidades de dominio en contratos |

### Jerarquía de entidades de dominio
```
BaseEntity  (Id: Guid)
└── AuditableEntity  (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
    ├── MasterEntity   ← catálogos maestros
    └── DocumentEntity ← documentos transaccionales
        └── AggregateRoot ← con eventos de dominio
```

### Estructura de carpetas por módulo
```
ERP.Domain/Modules/{Modulo}/
├── Entities/        ← Agregados y entidades hijas
├── ValueObjects/    ← Tipos inmutables con validación
├── Interfaces/      ← Contratos de repositorios
├── Enums/
└── Events/          ← IDomainEvent

ERP.Application/Modules/{Modulo}/
├── DTOs/
└── UseCases/{Nombre}/
    ├── {Nombre}Command.cs
    ├── {Nombre}CommandHandler.cs
    └── {Nombre}CommandValidator.cs

ERP.Infrastructure/
├── Authentication/
│   ├── Services/    ← JwtService, AccessTokenService, CurrentUserService
│   └── Security/    ← BcryptPasswordHasher
├── SaaS/
│   └── Services/    ← SubscriptionService, ConfigService
├── Persistence/
│   ├── ErpDbContext.cs
│   ├── Configurations/  ← IEntityTypeConfiguration<T> por entidad
│   └── Repositories/
├── Seeding/
└── Deployment/      ← FirstRunSetupService

ERP.API/
├── Controllers/
├── Middleware/      ← ExceptionMiddleware (ValidationException → 422)
├── Attributes/      ← [AppFeature], [Authorize]
└── Extensions/      ← ApiResultExtensions, DevDatabaseSeeder
```

> **Módulo de referencia:** `Accounting` — copiar estructura vertical para módulos nuevos.

---

## Multi-tenant

### Modelo técnico

Cada entidad de negocio tiene `TenantId: Guid`. Aislamiento mediante query filters globales evaluados por instancia de DbContext (no capturados al compilar el modelo):

```csharp
// ErpDbContext.OnModelCreating
modelBuilder.Entity<Producto>()
    .HasQueryFilter(p => p.TenantId == CurrentTenantId);
```

### Flujo de resolución
```
JWT claim tenant_id
    → CurrentTenantService.TenantId (ICurrentTenant)
    → ErpDbContext inyectado
    → query filter en cada SELECT / UPDATE / DELETE
```

### Entidades globales (sin TenantId)
Geografía INEC, tarifas SRI (`sri_vat_rate`), catálogo de planes SaaS — no llevan filtro de tenant.

---

## Autenticación y JWT

### Claims del token
| Claim | Contenido |
|-------|-----------|
| `sub` | IdentityUser.Id |
| `email` | Email |
| `tenant_id` | Guid del tenant activo (`00000000-…` = SuperAdmin global) |
| `full_name` | Nombre completo |
| `role` | `Admin`, `SuperAdmin`, u otro rol |

### Vida del token
- **JWT sesión:** `Jwt:ExpirationMinutes` (default 60 min)
- **Refresh token:** httpOnly cookie; fallback localStorage si cookies bloqueadas
- **Token first-run:** 15 minutos desde emisión

### Flujo de refresh (Axios interceptor)
```
Request → 401
    → POST /api/auth/refresh  (cookie httpOnly automática)
    → nuevo accessToken → reintentar original
    → Si falla → clearSession() → redirect /login
```

### Flujos de login

**Admin de empresa (multi-empresa):**
```
POST /api/access/bootstrap-login   →  lista de empresas
POST /api/access/switch-tenant     →  JWT con tenant_id elegido  (política Bootstrap)
```

**SuperAdmin:**
```
POST /api/auth/superadmin-login    →  JWT con tenant_id = 00000000-…
POST /api/auth/switch-tenant       →  JWT con tenant_id real (operar empresa)
POST /api/auth/switch-tenant       →  { tenantId: "00000000-…" }  volver al panel global
```

---

## SuperAdmin y first-run

### Tabla `first_run_setup_state`
| Columna | Uso |
|---------|-----|
| `is_first_run` | `true` hasta completar alta del primer SuperAdmin |
| `setup_token_hash` | SHA-256 del token mostrado en consola (NULL tras completar) |
| `setup_token_expiry_utc` | 15 minutos desde emisión |
| `completed_at` | UTC de alta completada |

### Secuencia de instalación
```
1. docker compose up -d
2. dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
3. dotnet run --project ERP.API
   → consola: "FIRST-RUN DETECTADO" + token + curl de ejemplo
4. POST /api/setup/superadmin  { setupToken, firstName, lastName, email, password }
5. POST /api/auth/superadmin-login  → JWT global
6. POST /api/auth/switch-tenant { "tenantId": "<guid>" }  → operar empresa
```

### Reset en desarrollo (solo `Development`)
```
POST /api/dev/reset-first-run  →  elimina SuperAdmins, devuelve nuevo setupToken
```

### Scripts PowerShell
| Script | Uso |
|--------|-----|
| `scripts/create-superadmin.ps1 -SetupToken "<token>"` | Alta SuperAdmin via API |
| `Crear-SuperAdmin.ps1` (raíz) | Flujo interactivo en español |
| `scripts/sql/reset-first-run-state.sql` | Reset manual en PostgreSQL |

### Configuración de instancia
| Clave | Efecto |
|-------|--------|
| `Deployment:SuperAdminPanelEnabled` | `false` en prod → bloquea login y rutas SuperAdmin |
| `Deployment:MaxActiveTenants` | Vacío / 0 = sin tope |
| `Deployment:MaxIdentityUsers` | Vacío / 0 = sin tope |

Variables de entorno: `Deployment__SuperAdminPanelEnabled`, etc.

---

## SaaS — Planes, módulos y menú

### Modelo de datos
```
Tenant
└── TenantSubscription
    ├── planCode          ← plan comercial contratado
    └── enabledModules[]  ← módulos habilitados (catalog, accounting, saas, access…)

SaasFeatureDefinition
├── kind: Module | Form | Quota | Integration
└── resourceRef: permiso o ruta

SaasPlanFeature  (plan ↔ feature)
├── featureId / isIncluded / limitPerPeriod
```

### Cadena de control de acceso operativo
```
JWT.role
    → planCode del tenant
    → enabledModules del tenant
    → permisos granulares (perm:modulo.recurso.accion)
    → menú de sesión filtrado (GET /api/me/menu)
```

### Menú dinámico — endpoints
```
GET  /api/superadmin/navigation-menu
PUT  /api/superadmin/navigation-menu/groups/reorder
PUT  /api/superadmin/navigation-menu/items/reorder-levels
POST /api/superadmin/navigation-menu/items
```
Frontend: `frontend/src/components/menu-builder/`

### Planes SaaS — endpoints
```
GET/POST   /api/superadmin/saas-plans
PUT/DELETE /api/superadmin/saas-plans/{planId}
PUT        /api/superadmin/saas-plans/reorder
PUT        /api/superadmin/saas-plans/{planId}/recommended
```

### Empresas — endpoints
```
GET    /api/access/superadmin/tenants         ← lista
POST   /api/access/superadmin/tenants         ← alta + admin inicial
PATCH  /api/tenants/{id}/company              ← datos empresa
PATCH  /api/tenants/{id}/subscription         ← planCode + enabledModules
```

---

## Arquitectura frontend

### Estructura de carpetas
```
frontend/src/
├── modules/
│   ├── lib/api.ts            ← Axios centralizado (interceptor JWT + refresh)
│   ├── lib/formatApiError.ts
│   └── {dominio}/
│       ├── api/              ← service.ts
│       ├── schemas/          ← schema Zod
│       ├── hooks/            ← useAsync + estado
│       ├── pages/            ← página + {prefix}-page.css
│       └── components/
├── pages/                    ← páginas simples / legacy
├── routes/
│   ├── mainRoutes.tsx
│   ├── catalogRoutes.tsx
│   └── superAdminShellRoutes.tsx
├── components/
│   ├── zh/ZHForm.tsx         ← ZHBtn, ZHField, ZHFormSection, ZHGrid
│   ├── zh/ZHPageNotice.tsx
│   ├── PageShell.tsx         ← LoadingState, EmptyState, NoAccessPage
│   └── ReportPageTemplate.tsx
├── styles/
│   ├── design-tokens.css
│   ├── zh-ui.css
│   └── page-template.css
├── store/
│   ├── authStore.ts          ← Zustand + persistencia localStorage
│   └── permissionsStore.ts
├── i18n/locales/             ← es.json, en.json, qu.json
└── nav/navConfig.ts          ← menú estático, grupos, aliases
```

### Sistema de diseño CSS — 3 niveles
```
design-tokens.css   → variables CSS (--color-*, --space-*, --radius-*, --text-*)
zh-ui.css           → componentes (.table, .badge, .zh-btn, .zh-status, .zh-modal*, .zh-input…)
page-template.css   → layout (.pg-page, .pg-header-row, .pg-kpi, .pg-section, .pg-table-controls…)
{pagina}-page.css   → SOLO clases únicas de esa pantalla con prefijo propio
```

---

## Recuperación de contraseña

- `POST /api/auth/forgot-password` `{ email }` → correo con enlace
- Enlace: `{PublicBaseUrl}/reset-password?token=...&tenantId=...`
- `POST /api/auth/reset-password` `{ token, newPassword, tenantId? }`
- Config en `appsettings.json` → `PasswordReset:PublicBaseUrl`, `TokenLifetimeMinutes` (default 60)
- Migración: `20260512161332_AddPasswordResetTokens`

---

## Decisiones de arquitectura (ADRs)

### ADR 0001 — Monolito modular + Clean Architecture *(Aceptada, 2026-05-02)*
- Monolito modular en un solo deploy; BD compartida por tenant vía filtros EF.
- Clean Architecture estricta; módulos en carpetas verticales por capa.
- `Accounting` como módulo de referencia de estructura.
- Sin AutoMapper — mapeos manuales en handlers.

### ADR 0002 — Multi-tenant con JWT y filtros EF Core *(Aceptada, 2026-05-02)*
- `TenantId` en entidades; query filters globales por instancia de DbContext.
- Tenant desde JWT (`tenant_id`) vía `ICurrentTenant`.
- Soft delete `IsActive = false` como convención.
- BCrypt solo en `ERP.Infrastructure` (no en Application).

### ADR 0003 — CI en GitHub Actions *(Aceptada, 2026-05-02)*
- CI único: `.github/workflows/ci.yml`.
- Backend: SDK fijado por `global.json` + `dotnet test`.
- Frontend: Node 22, `npm ci`, ESLint, build, Playwright smoke.
- Dependabot semanal.
- Disparos: `main`, `development`, `release/**`, `hotfix/**`, `workflow_dispatch`.

### Riesgos arquitectónicos corregidos *(2026-05-08)*
| # | Problema | Solución |
|---|----------|----------|
| 1 | BCrypt en Application | Movido a Infrastructure (`IPasswordHasher`) |
| 2 | Infrastructure desorganizada | Estructura por bounded context (Authentication/, SaaS/, Configuration/) |
| 3 | ErpDbContext (31 DbSets) | Documentado; configuraciones EF por módulo; estrategia futura en comentarios |
| 4 | Controller con 17 parámetros | DTO `GetProductReportRequest` con `ToFilter()` |
| 5 | CompaniesPage 782 líneas | Modularizada en `modules/companies/` |
| 6 | 45+ rutas en App.tsx | Sistema modular en `routes/` (mainRoutes, catalogRoutes, etc.) |
