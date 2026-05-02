# Arquitectura del sistema

## Visión general

Monolito modular con Clean Architecture. El objetivo es que cada módulo funcional sea independiente para poder extraerlo como microservicio cuando madure, sin reescribir el dominio.

**Decisiones formales:** ver [ADR en `docs/adr/`](adr/README.md) (p. ej. ADR 0001–0003).

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

## Estructura de archivos por módulo

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
    └── {Nombre}Handler.cs   ← Lógica del caso de uso

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

`ERP.Application/DependencyInjection.cs` escanea el assembly en startup y registra como `Scoped` todas las clases que terminan en `Handler`. No es necesario registrar manualmente los nuevos handlers en `Program.cs`.

## Patrón Result<T>

Los handlers retornan `Result<T>` (en `ERP.Application/Modules/Common/Result.cs`) en lugar de lanzar excepciones para errores de dominio esperados. Los controllers traducen el resultado a la respuesta HTTP apropiada.

## Autenticación

JWT generado por `JwtService` (Infrastructure). El token incluye los claims: `sub`, `email`, `tenant_id`, `full_name`, `role`.

La validación del token ocurre en el middleware de ASP.NET. Los controllers protegidos llevan `[Authorize]`.

## CORS

La política `"Frontend"` permite los orígenes configurados en `appsettings.json` bajo `Cors:AllowedOrigins`. En desarrollo el default es `http://localhost:5173`.

## Módulos actuales

| Módulo     | Dominio                        | Endpoints                              |
|------------|--------------------------------|----------------------------------------|
| Auth       | User, Email (VO)               | POST /register, /login                 |
| Tenants    | Tenant                         | POST /tenants                          |
| Products   | Product, ProductBarcode        | GET /products, GET /products/{id}, POST |
| Accounting | Account, JournalEntry, Money   | GET/POST /accounts, /journal-entries   |

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

- Agregar FluentValidation en los Commands
- Implementar Serilog para logging estructurado
- Configurar secrets reales (no hardcodear en appsettings.json)
- Agregar health checks (`/health`)
- Implementar refresh tokens
- CI/CD via GitHub Actions (ver `.github/workflows/`)
