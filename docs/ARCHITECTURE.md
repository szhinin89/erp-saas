# Arquitectura del sistema

## Visión general

> **Monolito modular con Clean Architecture.** El objetivo es que **cada módulo funcional sea independiente** para poder **extraerlo como microservicio** cuando madure, **sin reescribir el dominio**.

Esa frase es el criterio guía del repositorio: el código y la organización de carpetas deben favorecer **límites claros por dominio de negocio** (agregados, repositorios y casos de uso acotados), de modo que un futuro servicio pueda llevarse **principalmente `ERP.Domain` + el slice correspondiente de `ERP.Application`**, con adaptadores nuevos para transporte y persistencia.

**Qué implica en la práctica**

- **Dominio primero:** reglas y entidades viven en `ERP.Domain`; no dependen de API, EF ni MediatR.
- **Casos de uso por vertical slice** en `ERP.Application` (comandos, consultas, DTOs del módulo), invocando solo abstracciones del dominio del mismo contexto acotado.
- **Infraestructura sustituible:** `ERP.Infrastructure` implementa repositorios y detalles técnicos; al extraer un microservicio se reimplementan o comparten según el acoplamiento aceptado.

**Decisiones formales:** ver [ADR en `docs/adr/`](adr/README.md) (p. ej. ADR 0001–0003).

**Inventario de stack/herramientas en uso:** ver [`docs/HERRAMIENTAS-ERP-SAAS.md`](HERRAMIENTAS-ERP-SAAS.md).

**SuperAdmin y primera ejecución (token first-run, cambio de contexto empresa):** ver [`docs/SUPERADMIN-Y-FIRST-RUN.md`](SUPERADMIN-Y-FIRST-RUN.md).

**Convergencia de carpetas y namespaces:** plan por sprints en [`docs/ESTADO-PROYECTO.md`](ESTADO-PROYECTO.md#refactor-modular-por-sprints) (sección *Refactor modular por sprints*; `Domain.Modules.*` / `Application.Modules.*`).

## Capas y dependencias

```
┌─────────────────────────────────────────────┐
│  ERP.API  (controllers, middleware, host)   │
├─────────────────────────────────────────────┤
│  ERP.Application  (handlers, DTOs)          │
├─────────────────────────────────────────────┤
│  ERP.Infrastructure  (EF Core, repos)       │
├─────────────────────────────────────────────┤
│  ERP.Domain  (entidades, VOs, interfaces)   │
└─────────────────────────────────────────────┘
```

**Regla estricta:** cada capa solo puede depender de la capa inferior. El dominio no referencia EF Core, ASP.NET ni ningún framework externo.

## Criterios para cumplir el objetivo (módulo extraíble)

La arquitectura **debe** sostener que, al madurar un módulo, se pueda **levantar un microservicio** llevándose sobre todo **el dominio y los casos de uso de ese contexto**, sin reescribir reglas de negocio. Criterios **obligatorios** para código nuevo y refactors; los desvíos existentes se corrigen de forma incremental.

### 1. Límite del módulo (bounded context)

Un **módulo funcional** es un conjunto coherente de:

- **Dominio:** entidades, VOs, enums, reglas e **interfaces de repositorio y servicios de dominio** que pertenecen al mismo proceso de negocio (p. ej. ventas, compras, inventario).
- **Aplicación:** comandos, consultas, validadores y DTOs bajo el mismo prefijo de carpetas / namespace del módulo.
- **API:** controladores (o grupos de endpoints) que solo delegan en casos de uso de ese módulo y traducen HTTP ↔ comandos/consultas.

### 2. Dependencias entre módulos

| Permitido | Evitar / prohibido |
|-----------|---------------------|
| Usar **interfaces del dominio** de otro módulo solo si el contrato es estable y mínimo (ej. catálogo leído por ID), o tipos **realmente compartidos** en un núcleo acotado (`ERP.Application.Common`, VOs compartidos acordados). | Handlers o DTOs de un módulo **importando namespaces de `UseCases` de otro** (acoplamiento de aplicación cruzado). |
| **Integración explícita:** eventos de dominio, colas, o fachada de aplicación dedicada a “orquestación” documentada. | Lógica de negocio de módulo A **copiada** en módulo B. |
| **Infraestructura** implementando repos definidos en el dominio de cada módulo. | Un repositorio en Infra que **mezcle persistencia** de dos contextos sin dejar claro el límite. |

Objetivo: el grafo de dependencias del **slice** `Domain + Application` de un módulo sea **casi un subárbol**; lo que salga hacia fuera son pocos puntos explícitos.

### 3. Convención física y de namespaces (objetivo de convergencia)

- **Dominio:** puede vivir en `ERP.Domain/{Modulo}/` o `ERP.Domain/Modules/{Modulo}/`; lo importante es que **todo lo del mismo bounded context** quede agrupado y con interfaces de persistencia **en ese árbol**, no dispersas.
- **Aplicación:** preferir carpeta y namespace alineados, p. ej. `ERP.Application/Modules/{Modulo}/…` y `ERP.Application.Modules.{Modulo}.…`, para que un extract sea un **copy-paste de carpeta + ajuste de referencias**. Corregir gradualmente namespaces que hoy omiten `Modules` en el nombre lógico.

### 4. Infraestructura y datos hoy

Un único `ErpDbContext` y una base compartida **no invalidan** el objetivo: el límite modular es **lógico y de código**. La extracción posterior puede implicar **BD propia**, **vista** o **sincronización** según el módulo; el dominio reutilizable reduce el coste.

### 5. Checklist antes de dar por “cerrado” un feature de módulo

- [ ] Reglas y estados nuevos viven en **entidades o servicios de dominio** del módulo, no en el controller.
- [ ] El handler solo usa **repos/interfaces del dominio** del módulo (o contratos compartidos explícitos).
- [ ] No se añaden **dependencias de código** entre casos de uso de dos módulos (imports cruzados de handlers/DTOs ajenos) salvo `ERP.Application.Common` u otros contratos compartidos explícitos.
- [ ] Pruebas del caso de uso (unitarias o integración) pueden **ubicarse** junto al módulo conceptualmente (p. ej. `ERP.Application.Tests/{Modulo}`).

---

## Estructura de archivos por módulo (objetivo)

```
ERP.Domain/Modules/{Modulo}/
├── Entities/        ← Agregados y entidades hijas
├── ValueObjects/    ← Tipos inmutables con lógica de validación
├── Interfaces/      ← Contratos de repositorios (implementados en Infrastructure)
├── Enums/
├── Events/          ← Domain events (IDomainEvent)
└── Rules/           ← Reglas de negocio reutilizables

ERP.Application/Modules/{Modulo}/
├── DTOs/            ← Records de salida (response)
└── UseCases/{Nombre}/
    ├── {Nombre}Command.cs   ← Datos de entrada (record inmutable)
    └── {Nombre}CommandHandler.cs / {Nombre}QueryHandler.cs   ← MediatR IRequestHandler

ERP.Infrastructure/Persistence/
├── Configurations/  ← IEntityTypeConfiguration<T> por entidad
├── Repositories/    ← Implementaciones concretas de los repos del dominio
└── ErpDbContext.cs
```

## Multi-tenant

Cada entidad de negocio tiene `TenantId: Guid`. El aislamiento se logra con **query filters globales** en EF Core aplicados en `ErpDbContext.OnModelCreating`.

El `TenantId` activo se resuelve en cada request desde el claim `tenant_id` del JWT a través de `ICurrentTenant` → `CurrentTenantService`.

**Importante:** el filtro referencia `CurrentTenantId` como propiedad de instancia del DbContext (no como variable local capturada), lo que garantiza que se evalúa en cada query y no en la compilación del modelo.

Cuando se agregue una nueva entidad con `TenantId`, registrar su filtro en `ErpDbContext.OnModelCreating`.

## Registro de handlers (Application)

MediatR (`AddMediatR`) registra los `IRequestHandler<,>` (p. ej. `*CommandHandler`, `*QueryHandler`). `DependencyInjection.cs` además registra **FluentValidation** y un escaneo complementario de clases `*Handler` bajo `UseCases` que **no** implementan `IRequestHandler` (evitar duplicar el registro MediatR).

## Patrón Result<T>

Los handlers retornan `Result<T>` (en `ERP.Application/Modules/Common/Result.cs`) en lugar de lanzar excepciones para errores de dominio esperados. Los controllers traducen el resultado a la respuesta HTTP apropiada.

## Autenticación

JWT generado por `JwtService` (Infrastructure). El token incluye los claims: `sub`, `email`, `tenant_id`, `full_name`, `role`.

La validación del token ocurre en el middleware de ASP.NET. Los controllers protegidos llevan `[Authorize]`.

## CORS

La política `"Frontend"` permite los orígenes configurados en `appsettings.json` bajo `Cors:AllowedOrigins`. En desarrollo el default es `http://localhost:5173`.

## Módulos actuales

| Módulo     | Dominio (resumen)              | Endpoints (resumen)                    |
|------------|--------------------------------|----------------------------------------|
| Auth       | User, Email (VO)               | POST /register, /login                 |
| Tenants    | Tenant                         | POST /tenants                          |
| Products   | Product, ProductBarcode        | GET /products, GET /products/{id}, POST |
| Accounting | Account, JournalEntry, Money, Configuración contable por empresa | `/accounts`, `/journal-entries`, `/configuracion-contable` |
| Ventas     | Factura, notas crédito/débito, retención recibida | `/ventas`, `/ventas/notas`, `/ventas/retenciones-recibidas` |
| Compras    | Factura compra, OC, retención emitida | `/compras`, `/compras/ordenes`, `/compras/retenciones` |
| Caja       | Caja chica, banco, extractos    | `/caja`                                |

> Lista detallada de flujos, permisos y migraciones: [`docs/ESTADO-PROYECTO.md`](ESTADO-PROYECTO.md).

## Migraciones EF Core

```powershell
cd backend/src/ERP.Infrastructure
dotnet ef migrations add {Nombre} --startup-project ../ERP.API
dotnet ef database update --startup-project ../ERP.API
```

## Tests (estructura prevista)

| Proyecto                   | Tipo           | Herramientas sugeridas      |
|----------------------------|----------------|-----------------------------|
| ERP.Domain.Tests           | Unitario       | xUnit, FluentAssertions     |
| ERP.Application.Tests      | Unitario       | xUnit, Moq/NSubstitute      |
| ERP.Infrastructure.Tests   | Integración    | xUnit, Testcontainers       |
| ERP.API.Tests              | Integración    | WebApplicationFactory       |

## Próximos pasos para producción

- ~~Agregar FluentValidation en los Commands~~ (**hecho:** validadores por comando + `ValidationBehavior`)
- Implementar Serilog para logging estructurado
- Configurar secrets reales (no hardcodear en appsettings.json)
- Agregar health checks (`/health`)
- Implementar refresh tokens
- CI/CD via GitHub Actions (ver `.github/workflows/`)
