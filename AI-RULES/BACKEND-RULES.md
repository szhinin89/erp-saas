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
return this.ToOkOrBadRequest(result, "OK");      // ✅
return this.ToCreatedOrBadRequest(result, "Creado");
return this.ToOkOrNotFound(result);

return Ok(new ApiResponse<T> { … });             // ❌ nunca manual
```

### Status HTTP

| Caso | Status |
|------|--------|
| Éxito lectura | 200 |
| Éxito creación | 201 |
| Regla negocio / entrada inválida | 400 |
| Sin autenticación | 401 |
| Sin permiso | 403 |
| No encontrado | 404 |
| `ValidationException` FluentValidation | **422** (ExceptionMiddleware) |

Declarar `[ProducesResponseType]` por cada status que aplique.

---

## Multi-tenant (backend)

- `TenantId` desde JWT/contexto (`CurrentTenantService` / `ICurrentTenant`).
- **No** aceptar `TenantId` desde body/query en operaciones tenant-scoped.
- Entidades multi-tenant: `TenantId` + filtro global en `OnModelCreating`.
- Índices únicos compuestos con `TenantId`: `(TenantId, Code)`.
- Nunca unicidad global sin `TenantId`.
- Solo flujos plataforma/SuperAdmin cross-tenant, con autorización explícita.

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
