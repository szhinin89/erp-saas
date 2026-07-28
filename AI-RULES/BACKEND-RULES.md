# Backend — reglas de implementación

Canónico para .NET 10 / Clean Architecture. Catálogo PR B-xx: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md). Seguridad tenant: [SECURITY.md](./SECURITY.md).

---

## Capas (dependencias solo hacia abajo)

```
ERP.API → ERP.Application → ERP.Domain ← ERP.Infrastructure
```

| Proyecto | Permitido | Prohibido |
|----------|-----------|-----------|
| `ERP.Domain` | Dominio puro | EF, ASP.NET, MediatR, HTTP, NuGet infra |
| `ERP.Application` | Casos de uso, orquestación, validación | Acceso HTTP/UI, BD directa |
| `ERP.Infrastructure` | Persistencia, servicios técnicos | Reglas de negocio |
| `ERP.API` | HTTP, autorización, DTOs | Entidades dominio, lógica negocio |

---

## Patrones obligatorios

### Entidades — factory, nunca `new` público

```csharp
var p = Producto.Create("X", tenantId, actorId);  // ✅
var p = new Producto { Nombre = "X" };             // ❌
```

### Soft delete

```csharp
entidad.Disable();    // IsActive = false  ✅
db.Remove(entidad);   // ❌ en entidades de negocio
```

- UI: botón "Anular" o "Deshabilitar", nunca "Eliminar".
- API: no exponer DELETE que borre registros de negocio.

**Excepciones DELETE físico documentadas:**

| Entidad | Motivo |
|---------|--------|
| `ExpenseCategory` | Configuración contable, no documento de negocio |
| `SaasPlan` | Catálogo planes; solo sin suscripciones activas (`DeletePlanAsync`) |

### Result&lt;T&gt; — no throw al controller

```csharp
return Result<ProductDto>.Failure("Código duplicado.");  // ✅
throw new Exception("Código duplicado.");               // ❌
```

### Sin dependencias cruzadas entre módulos Application

```csharp
// ✅ Contrato de dominio
ICustomerRepository repo

// ❌ Importar handler de otro módulo
using ERP.Application.Modules.Customers.UseCases.GetCustomer;
```

### Sin AutoMapper

Mapeos manuales en handlers/casos de uso.

---

## CQRS y validación (Application)

- Commands/Queries vía **MediatR** (no handlers inyectados directo en controller).
- Cada Command/Query con entrada de usuario → **`[Nombre]Validator`** (FluentValidation).
- **`ValidationBehavior`** en pipeline MediatR.
- Errores de negocio esperados → **`Result<T>`**, no excepciones genéricas.

Detalle 4 capas: [ENFORCEMENT.md](./ENFORCEMENT.md).

---

## Controllers — ApiResultExtensions (obligatorio)

```csharp
return this.ToOkOrBadRequest(result);            // ✅ code default = OK
return this.ToCreatedOrBadRequest(result);       // ✅ code default = CREATED
return this.ToOkOrNotFound(result);

return Ok(new ApiResponse<T> { … });             // ❌ nunca manual
return Ok(new { mensaje = "..." });               // ❌ nunca mensajes a mano
```

### Envelope de respuesta (`ApiResponse<T>`) — `code` es la fuente única de verdad

Todas las respuestas (éxito y error) usan el envelope definido en
`ERP.API/Contracts/ApiResponse.cs`:

```json
{
  "code": "NOT_FOUND",
  "severity": "success | error | warning | info",
  "message": { "user": "Mensaje seguro para el usuario (es)", "dev": "Detalle técnico, null salvo en Development" },
  "data": {},
  "meta": { "correlationId": "...", "timestamp": "utc", "traceId": null }
}
```

- `code`: único campo "fuente de verdad", catálogo público en
  `ERP.Application/Common/ApiResponseCodes.cs` (SCREAMING_SNAKE_CASE, p. ej.
  `ApiResponseCodes.Common.Ok`, `.Created`, `.ValidationError`, `.NotFound`,
  `.CompanyRucAlreadyExists`). Los códigos transversales viven en la clase
  anidada `ApiResponseCodes.Common`; nuevos módulos (Items, Ventas, Compras,
  Inventario, ...) agregan su propia clase anidada por dominio (p. ej.
  `ApiResponseCodes.Inventory`) — ver sección "API Response Contract V1 —
  LOCKED" más abajo. `severity` y `message.user`/`message.dev` se **derivan
  siempre** de `code` vía `ERP.Application/Common/MessageCatalog.cs`.
- `meta.correlationId`: única fuente de verdad =
  `ERP.API/Middleware/RequestCorrelationMiddleware.cs`, registrado como
  **primer** middleware del pipeline (`Program.cs`, antes de
  `ExceptionMiddleware`). Reutiliza el header `X-Correlation-Id` entrante si
  el cliente/gateway lo envía; si no, usa `HttpContext.TraceIdentifier`.
  Enriquece Serilog (`correlation_id`/`request_id` vía `LogContext`) y lo
  refleja en el header de respuesta `X-Correlation-Id`. Cualquier otro
  componente que necesite el id llama a
  `RequestCorrelationMiddleware.Resolve(context)` — **nunca**
  `context.TraceIdentifier` directo ni `Guid.NewGuid()`.
- `ERP.API/Extensions/ResponseFactory.cs` es el **único** constructor del
  envelope: resuelve `MessageCatalog.Resolve(code)` y arma `severity` +
  `message`. Ningún controller, handler o middleware debe construir
  `ApiResponse<T>` a mano ni escribir `message.user` a mano (extensión de la
  regla B-03).
- `message.dev`: nunca se expone fuera de `Development` (puede contener stack
  trace / detalle de excepción).
- `data.errors: string[]`: detalle dinámico de la instancia (mensajes de
  FluentValidation por campo, valor concreto que generó el conflicto, etc.).
  `message.user`/`message.dev` son SIEMPRE el texto genérico y estable del
  catálogo para ese `code` — el detalle específico va en `data.errors`. Ya no
  existen `errors`/`warnings` como campos raíz del envelope.
- `Result<T>.Code` (antes `ErrorCode`) viaja **sin traducción** desde el
  handler hasta el JSON de respuesta — usa directamente valores de
  `ApiResponseCodes`.
- Códigos de negocio específicos por feature (`ITEM_CREATED`,
  `LOW_STOCK_WARNING`, etc.) son tarea de seguimiento, fuera de esta pasada. Se
  agregan incrementalmente vía `Result<T>.Success(value, "ITEM_CREATED")` /
  `Result<T>.Failure(msg, "LOW_STOCK_WARNING")` + entrada nueva en
  `MessageCatalog`, sin cambios de arquitectura.

#### Enforcement (Reglas A-D — `ERP.Architecture.Tests/ApiResponseContractTests.cs`)

Estas reglas fallan el build si se violan:

- **Regla A** — ningún controller contiene `new ApiResponse<`, `new
  ApiResponse(` ni `new ApiResponse {`; siempre usa `ApiResultExtensions`
  (`ApiOk`/`ApiCreated`/`ToOkOrBadRequest`/...).
- **Regla B** — `ERP.Application` no referencia el ensamblado `ERP.API`
  (`ResponseFactory`/`ApiResponse`/`ApiResultExtensions` viven ahí) y ningún
  archivo de `ERP.Application` (salvo `MessageCatalog.cs`,
  `ApiResponseCodes.cs`, `ApiSeverity.cs`) llama a `MessageCatalog.*`. Los
  handlers solo devuelven `Result<T>` con un `ApiResponseCodes.*`;
  `ResponseFactory` (en `ERP.API`) es el único consumidor de `MessageCatalog`.
- **Regla C** — `ERP.Domain` no referencia `ERP.Application` ni `ERP.API`
  (pureza de dominio: cero conocimiento de `ApiResponse`, `ApiResponseCodes`,
  `MessageCatalog`, `ResponseFactory`).
- **Regla D** — cada constante pública de `ApiResponseCodes` tiene exactamente
  una entrada en `MessageCatalog` (sin huérfanos en ningún sentido). Verificar
  con `MessageCatalog.RegisteredCodes`.

Consistencia del pipeline `code → severity/message` y no-filtración de
`ExceptionMiddleware` (sin stack trace, `message.dev` solo en Development,
JSON camelCase): `ERP.API.Tests/ResponseFactoryConsistencyTests.cs`.

Forma exacta del JSON serializado (snapshot del envelope, ausencia de campos
legacy, camelCase recursivo, `message.dev` por entorno):
`ERP.API.Tests/ApiResponseContractSnapshotTests.cs`.

Gobernanza del `correlationId` único (nadie fuera de
`RequestCorrelationMiddleware` lee `TraceIdentifier` ni escribe el header
`X-Correlation-Id`; `ResponseFactory`/`ExceptionMiddleware` no generan su
propio id; `ResponseFactory` no depende de `ERP.Infrastructure`):
`ERP.Architecture.Tests/CorrelationGovernanceTests.cs`.

#### API Response Contract V1 — LOCKED

El contrato `{code, severity, message:{user,dev}, data, meta}` y el flujo de
`correlationId` quedan **congelados**. Esta sección es vinculante para
cualquier cambio futuro al envelope de respuesta.

✅ **Permitido** (no requiere revisión arquitectónica):
- Agregar nuevas constantes a `ApiResponseCodes` (en `Common` o en una clase
  anidada nueva por dominio, p. ej. `ApiResponseCodes.Inventory`), siempre con
  su entrada correspondiente en `MessageCatalog` (Regla D la exige
  automáticamente — recorre clases anidadas).
- Agregar nuevas entradas a `MessageCatalog` para códigos nuevos.
- Agregar nuevos módulos/controllers del ERP que consuman
  `ApiResultExtensions`/`ResponseFactory` existentes sin modificarlos.
- Usar `Result<T>.Success(value, "ALGUN_CODE")` /
  `Result<T>.Failure(msg, "ALGUN_CODE")` con códigos ya catalogados o nuevos
  (con su entrada en `MessageCatalog`).

❌ **Restringido** (requiere revisión arquitectónica explícita):
- Modificar la forma de `ApiResponse<T>`, `ApiResponseMessage` o
  `ApiResponseMeta` (`ERP.API/Contracts/ApiResponse.cs`): nombres de
  propiedades, tipos, o estructura del envelope.
- Cambiar los nombres JSON (`code`, `severity`, `message`, `data`, `meta`,
  `user`, `dev`, `correlationId`, `timestamp`, `traceId`) o el casing
  (camelCase obligatorio).
- Crear un segundo `ResponseFactory` o cualquier helper que construya
  `ApiResponse<T>` fuera de `ERP.API/Extensions/ResponseFactory.cs`.
- Reintroducir `success`, `status`, `responseObject`, `userMessage`,
  `developerMessage`, `errors`/`warnings` como campos raíz del envelope, o
  cualquier variante de los mismos.
- Generar `correlationId` fuera de `RequestCorrelationMiddleware` (incluye
  `Guid.NewGuid()` para este propósito, o leer `context.TraceIdentifier`
  directamente desde otro componente).
- Escribir mensajes de usuario (`message.user`) a mano en controllers,
  handlers o middlewares — siempre vía `MessageCatalog` + `code`.

Cualquier cambio en la lista ❌ debe: (1) discutirse explícitamente con el
equipo antes de implementar, (2) actualizar esta sección y
`PR-RULES-CATALOG.md`/`ENFORCEMENT.md` en el mismo cambio, y (3) extender los
tests de `ApiResponseContractTests.cs`,
`ApiResponseContractSnapshotTests.cs`, `ResponseFactoryConsistencyTests.cs` y
`CorrelationGovernanceTests.cs` para reflejar el nuevo contrato.

### Status HTTP

| Caso | Status |
|------|--------|
| Éxito lectura | 200 |
| Éxito creación | 201 |
| Regla de negocio (`BusinessRule`) | 400 |
| Sin autenticación | 401 |
| Sin permiso | 403 |
| No encontrado | 404 |
| `ValidationException` FluentValidation / categoría `Validation` | **422** (ExceptionMiddleware) |
| Conflicto de unicidad (categoría `Duplicate`) | **409** |
| Falla técnica interna (categoría `Infrastructure`) | 503 |
| Falla de sistema externo (SRI, categoría `Integration`) | 502 |

Declarar `[ProducesResponseType]` por cada status que aplique.

**Tabla canónica completa (categorías, `Result<T>.Code` obligatorio desde `ApiResponseCodes`, reglas E-B1..E-B8):** ver [`AI-RULES/ERROR-HANDLING.md`](./ERROR-HANDLING.md) (ADR-027). Ningún handler/controller decide su propio status a mano — se deriva de la categoría del código.

---

## Multi-tenant (backend)

- `TenantId` desde JWT/contexto (`CurrentTenantService` / `ICurrentTenant`).
- **No** aceptar `TenantId` desde body/query en operaciones tenant-scoped.
- Entidades multi-tenant: `TenantId` + filtro global en `OnModelCreating`.
- Índices únicos compuestos con `TenantId`: `(TenantId, Code)`.
- Nunca unicidad global sin `TenantId`.
- Solo flujos plataforma / operador platform cross-tenant, con autorización explícita.

---

## Estructura por módulo

```
ERP.Domain/Modules/{Modulo}/Entities|ValueObjects|Exceptions
ERP.Application/Modules/{Modulo}/Commands|Queries|DTOs|Validators
ERP.Infrastructure — configurations, repos, ErpDbContext
ERP.API — Controllers delgados
```

Controller delgado: recibe request → delega → devuelve contrato HTTP.

---

## AI + Analytics — prohibiciones absolutas en ERP Core

```
❌ NO llamar OpenAI/Anthropic/LLMs desde ERP.Domain o ERP.Application
❌ NO referenciar paquetes IA (OpenAI SDK, SemanticKernel, LangChain) en ERP.*.csproj
❌ NO agregar lógica de analytics o reporting en Controllers — usar CQRS queries
❌ NO consultar tablas transaccionales desde ERP.AI.* — usar read models o Outbox
❌ NO mezclar proyecciones OLAP con transacciones OLTP en el mismo SaveChanges
```

Checks automáticos: [check-ai-layer-boundaries.mjs](../tools/architecture/check-ai-layer-boundaries.mjs) (AI-001 → AI-005)
Arquitectura futura IA: [AI-FOUNDATION.md](./AI-FOUNDATION.md)
Read models / analytics: [ANALYTICS-FOUNDATION.md](./ANALYTICS-FOUNDATION.md)

---

## Domain Events — reglas de implementación

Ver detalle completo en [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md).

Resumen obligatorio:

| Regla | Detalle |
|-------|---------|
| Solo AggregateRoots emiten eventos | `RaiseDomainEvent(...)` dentro del AggregateRoot |
| Naming: past tense | `InvoiceCreatedEvent` ✅ — `CreateInvoiceEvent` ❌ |
| Nuevos eventos extienden `BaseDomainEvent` | Agrega `CorrelationId`, `TenantId`, `CausationId` |
| Handlers son idempotentes | Verificar si ya procesaron el evento |
| IA no vive en Domain/Application | Ver [AI-FOUNDATION.md](./AI-FOUNDATION.md) |

---

## Tarifas SRI

No existe formulario para crear tarifas — vienen de `sri_vat_rate`. `POST /api/tax-rates` eliminado. Usar `GET /api/tax-rates` para dropdowns.

---

## Tests

```powershell
cd backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj
```

Guardrails: [ENFORCEMENT.md](./ENFORCEMENT.md).
