## Arquitectura (ERP SaaS)

Este repositorio está diseñado como **monolito modular** con separación estricta por capas (estilo Clean Architecture) y con el objetivo de poder **extraer módulos como microservicios** en el futuro sin reescribir el dominio.

### Estructura del repo

```
erp-saas/
├── backend/
│   └── src/
│       ├── ERP.slnx
│       ├── ERP.API/                 # Host HTTP (Controllers, auth, middleware, swagger)
│       ├── ERP.Application/         # Casos de uso (handlers), DTOs y contratos
│       ├── ERP.Domain/              # Dominio (entidades, VOs, reglas, eventos)
│       ├── ERP.Infrastructure/      # Persistencia/EF Core, repos, servicios técnicos
│       ├── ERP.*.Tests/             # Tests por capa (Domain/App/Infra/API)
│       └── erp_auth.ps1
├── frontend/                        # Vite + React + TS
└── docs/
```

### Dependencias permitidas (reglas)

- **`ERP.Domain`**: no depende de nada (solo BCL).
- **`ERP.Application`**: depende de `ERP.Domain`. No depende de EF Core ni de `ERP.Infrastructure`.
- **`ERP.Infrastructure`**: depende de `ERP.Application` y `ERP.Domain`. Aquí vive EF Core y repositorios concretos.
- **`ERP.API`**: depende de `ERP.Application` y `ERP.Infrastructure`. Expone HTTP + Swagger.

Regla práctica: **el dominio no conoce la base de datos ni el framework web**.

### Módulos (vertical slices)

La modularidad se organiza por **módulos funcionales** dentro de Domain/Application/Infrastructure, por ejemplo:

- `ERP.Domain/Modules/Products/*`
- `ERP.Application/Modules/Products/*`
- `ERP.Infrastructure/Persistence/Configurations/*` y `Repositories/*`

Esto permite que cada módulo tenga:

- **Dominio**: entidades, value objects, reglas, eventos
- **Aplicación**: comandos/handlers, DTOs, validaciones (si aplica)
- **Infra**: repos concretos, mapping EF, migraciones

### Multi-tenant

La solución está pensada para multi-tenant.

- `ErpDbContext` aplica filtros globales por `TenantId`.
- En API, `ICurrentTenant` debe resolver el `TenantId` (hoy puede venir de header/JWT).

Recomendación: estandarizar el origen del tenant:

- **Corto plazo**: header `X-Tenant-Id` (dev).
- **Mediano plazo**: claim de JWT (`tenant_id`) + validación centralizada.

### Persistencia (EF Core)

- `ErpDbContext` vive en `ERP.Infrastructure/Persistence`.
- Configuración de entidades en `ERP.Infrastructure/Persistence/Configurations`.
- Repositorios en `ERP.Infrastructure/Persistence/Repositories`.
- Migraciones en `ERP.Infrastructure/Migrations`.

### API + Swagger

El host HTTP está en `ERP.API`.

- Swagger se habilita en `Development`.
- Puertos locales (launchSettings): `https://localhost:7253` y `http://localhost:5003`.

### Pruebas

Se crearon proyectos de test por capa:

- `ERP.Domain.Tests`: pruebas unitarias (VOs/reglas)
- `ERP.Application.Tests`: pruebas de casos de uso (mocks)
- `ERP.Infrastructure.Tests`: pruebas de persistencia (InMemory)
- `ERP.API.Tests`: smoke/integration tests con `WebApplicationFactory`

Comando:

```powershell
cd c:\ProyectCursor\erp-saas\backend\src
dotnet test .\ERP.slnx -c Release
```

## Guía para escalar y evolucionar a microservicios (recomendado)

### 1) Definir límites (Bounded Contexts)

Ejemplos típicos en ERP:

- Accounting (contabilidad)
- Products/Catalog
- Inventory
- Orders
- Invoicing

Regla: **cada bounded context debe tener su propio modelo de dominio** (evitar “God entities” compartidas).

### 2) Contratos entre módulos (hoy) y entre servicios (mañana)

Evitar dependencias directas entre módulos usando:

- **Interfaces** en `ERP.Application` (puertos) + implementaciones en `ERP.Infrastructure`
- **Eventos de dominio / integración** para comunicación asíncrona

Cuando extraigas a microservicio:

- El contrato se vuelve **HTTP/gRPC + eventos** (en lugar de llamadas in-process).

### 3) Estrategia de datos (database-per-service)

Para microservicios reales, la recomendación es:

- **Una base por servicio**, sin joins cross-service.
- Consistencia eventual con **eventos** (y proyecciones si se necesita “read model”).

### 4) Outbox + eventos (para evitar pérdida de eventos)

Si vas a publicar eventos al crear/actualizar entidades:

- Implementar **Outbox pattern** en `ERP.Infrastructure` (tabla outbox + publisher).
- Publicar a un bus (RabbitMQ/Kafka/Azure Service Bus) desde un worker.

### 5) Extraer un microservicio: pasos prácticos

Cuando un módulo madure, la extracción típica es:

- Copiar `Domain + Application` del módulo a un repo nuevo
- Crear un `API host` nuevo para ese servicio
- Mantener contratos (DTOs/eventos) compatibles
- Mantener `TenantId` como parte del contrato (header/claim) y del storage

### 6) Checklist de “microservicio listo”

- El módulo tiene **tests** y CI estable
- Dependencias con otros módulos son **por contrato** (no por referencia)
- Persistencia encapsulada (repos + migraciones)
- Logging/observabilidad listos (correlationId/traceId)

