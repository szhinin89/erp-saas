# Backend — reglas de implementación

Canónico para .NET 10 / Clean Architecture. Catálogo PR B-xx: [PR-RULES-CATALOG.md](./pr-rules-catalog.md). Seguridad tenant: [SECURITY.md](./security.md).

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

Detalle 4 capas: [ENFORCEMENT.md](./enforcement.md).

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

**Tabla canónica completa (categorías, `Result<T>.Code` obligatorio desde `ApiResponseCodes`, reglas E-B1..E-B8):** ver [`error-handling.md`](./error-handling.md) (ADR-027). Ningún handler/controller decide su propio status a mano — se deriva de la categoría del código.

---

## Multi-tenant (backend)

- `TenantId` desde JWT/contexto (`CurrentTenantService` / `ICurrentTenant`).
- **No** aceptar `TenantId` desde body/query en operaciones tenant-scoped.
- Entidades multi-tenant: `TenantId` + filtro global en `OnModelCreating`.
- Índices únicos compuestos con `TenantId`: `(TenantId, Code)`.
- Nunca unicidad global sin `TenantId`.
- Solo flujos plataforma / operador platform cross-tenant, con autorización explícita.

---

## Idempotencia de comandos financieros creadores de dinero (ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01)

Todo comando que **crea** un efecto económico (pago a proveedor, cobro de CxC, movimiento manual de caja, solicitud de efectivo, aplicación/reembolso de crédito de proveedor) sigue una sola convención. Una intención del usuario produce **como máximo un** efecto económico, aunque haya doble clic, Enter repetido, reintento, timeout o requests concurrentes. El estado `saving` de la UI no cuenta como garantía.

| Pieza | Regla |
|---|---|
| Identidad | `ClientRequestId` (`Guid`, obligatorio, validador `NotEmpty`) = UNA intención. Lo genera el frontend con `useClientRequestId` (`src/lib/idempotency`): el mismo id mientras el payload sea el mismo; se libera tras el éxito. |
| Huella | SHA-256 de la representación canónica V1 del request (`CanonicalRequestFingerprint`: record versionado solo con datos del usuario; nunca tenant/empresa/actor). El pago a proveedor usa el snapshot canónico ya existente de `CashFundingPaymentSnapshot`. |
| Persistencia | `client_request_id` + `request_payload_hash` en el agregado creado; índice **UNIQUE** `(tenant_id, client_request_id)` (parcial `WHERE client_request_id IS NOT NULL` si hay filas históricas o creadas por otra vía). Value object `ClientRequestKey` (set-once vía `BindClientRequest`). |
| Handler | Un solo camino: lectura por clave (camino rápido, sin efectos) → negocio en UNA transacción (o `SaveChanges` atómico) → si el intento falla por **cualquier** motivo (índice único, saldo ya consumido por el ganador, lock) y la clave ya existe, se responde el documento ganador. Nunca `Exists` → `Insert` como única barrera. |
| Semántica | Misma clave + misma huella → el documento original, con el **mismo** status y cuerpo que la creación (201), sin volver a aplicar CxP/CxC, mover caja, postear ni encolar outbox. Misma clave + huella distinta → `409 CONFLICT`. Clave distinta → nueva operación legítima. Un rechazo no consume la clave. |
| Evidencia | Tests HTTP + PostgreSQL reales (`FinancialCommandIdempotencyTests`): secuencial, concurrente, payload distinto, clave distinta, fallo previo, autorización; se cuentan documento, aplicaciones, `CashMovement`, `JournalEntry`, outbox y saldos. |

Un comando nuevo de este tipo sin `ClientRequestId` + índice único es un defecto bloqueante de PR.

### Saldos compartidos: lock pesimista antes de decidir (ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01)

La idempotencia protege **una** intención; dos intenciones **distintas** que compiten por el mismo saldo (dos cobros sobre la misma CxC) se serializan con el patrón oficial de lock de fila, nunca con un segundo mecanismo:

1. Transacción explícita (`IUnitOfWork.BeginTransactionAsync`) — sin ella el `FOR UPDATE` se libera al terminar la sentencia.
2. `Get…ForUpdateAsync` del repositorio: `SELECT 1 … WHERE tenant_id AND company_id AND id … FOR UPDATE` → lectura acotada al alcance operativo → `Entry(x).ReloadAsync`. Una fila fuera del alcance no se bloquea ni se devuelve (fail-closed → 404).
3. **Recién entonces** la regla de dominio decide si el monto cabe; el perdedor reevalúa contra el saldo ya consumido y recibe la regla canónica (`422 DOMAIN_RULE_VIOLATION`) sin ningún efecto.
4. `SaveChanges` (estado + posting + outbox) y `Commit`; cualquier fallo → `Rollback` completo.

Todo escritor del saldo **o del estado** de `SalesReceivable` lo lee por `GetByIdsForUpdateAsync`/`GetByInvoiceIdForUpdateAsync`: registrar cobro, reversar cobro (además bloquea el `Payment`), crédito por devolución de venta y anulación de factura (ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01: además bloquea la `SalesInvoice` con `ISalesInvoiceRepository.GetByIdForUpdateAsync`; decisión y efectos — CxC, Kardex, reverso contable, outbox — en una transacción).

Orden canónico de locks (evita deadlocks; cada flujo toma solo un subconjunto, siempre en este orden):

1. advisory de documento (autorización de devolución, por factura);
2. `SalesInvoice` (anulación y autorización de devolución — ZH-SALES-RETURN-INVOICE-STATE-CONCURRENCY-01: la devolución revalida bajo este lock que la factura siga `Authorized` antes de cualquier efecto);
3. `Payment` (reversa de cobro);
4. `SalesReceivable` en orden ascendente de Id;
5. secuencia documental → Kardex (`CurrentStock`, optimista con reintento) → secuencia de asientos (posting en `SaveChanges`).

### Revertir una venta: un solo camino por factura (ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01)

Una venta autorizada se revierte por **uno** de dos caminos excluyentes, decididos bajo el lock de la `SalesInvoice`:

| Situación de la factura `Authorized` | Anular (`CancelSalesInvoice`) | Devolver (`AuthorizeSalesReturn`) |
|---|---|---|
| Sin devoluciones, CxC sin cobros | Permitido: revierte la factura completa (CxC, Kardex, asientos) | Permitido |
| Con devolución `Draft` (o `Cancelled`) | Permitido; el Draft no tiene efectos, queda Draft y ya no puede autorizarse | Permitido mientras la factura siga `Authorized` |
| Con devolución `Authorized` (parcial o total, efectivo o crédito a CxC) | **Rechazado** — `SalesInvoiceCancellationPolicy` (422 `DOMAIN_RULE_VIOLATION`) | Permitido por el remanente |
| CxC con cobros | Rechazado (`SalesReceivable.Cancel`) | Permitido |
| Factura `Cancelled` | Rechazado (ya anulada) | **Rechazado** — `SalesReturnInvoiceEligibility` |

Una devolución autorizada es terminal y ya reingresó Kardex, reembolsó, contabilizó y emitió su Nota de Crédito; la anulación revierte la factura completa, así que nunca se combinan: Σ devuelto + Σ anulado ≤ vendido en cantidad, costo, reembolso/CxC e impuestos. Mismo criterio que Compras (PI-CANC-01). Ambas reglas se evalúan bajo el lock de la factura antes de cualquier efecto, por lo que la anulación y la autorización concurrentes se serializan y la segunda ve el resultado de la primera. La UI muestra "Anular" en toda factura autorizada; el servidor es la autoridad (ocultarlo requeriría exponer las devoluciones en el DTO — mejora de UX opcional, no implementada).

`SalesInvoice` y `Payment` nunca se toman en el mismo flujo; ningún flujo que tenga la CxC vuelve a pedir factura, pago o advisory. El cobro no toma `CashSession`. Sin `advisory lock` adicional ni migración: la fila existente es el recurso compartido.

---

## Una capacidad, varios contextos de autorización (ZH-COMPANY-IDENTITY-SSOT-01)

Cuando la misma capacidad se expone a actores con fronteras de seguridad distintas, se mantienen endpoints y comandos separados (cada uno con su policy y su resolución de alcance) y **una sola** implementación de la regla: un validator de reglas compartidas incluido por cada comando (`Include`) y una operación de aplicación única que ambos handlers llaman tras resolver su alcance. Nunca Controller → Controller, ni un handler que construye el comando del otro o envía un request MediatR para reutilizarlo.

Caso vigente — identidad de empresa (RUC, razón social, nombre comercial, activo):

| | Operativo | Global |
|---|---|---|
| Endpoint | `PUT /api/v1/companies/{id}` | `PUT /api/v1/admin-core/companies/{id}` |
| Autorización | `perm:erp.companies.update` + membership (`ICompanyAccessGuard`) | policy `PlatformAdmin` (token sin tenant + Admin) + chequeo en handler |
| Alcance | empresa del tenant resuelto server-side (`GetTrackedByIdForTenantAsync`); ajena = 404 | empresa explícita de cualquier tenant (`GetTrackedByIdForAdminCoreAsync`) |
| Comando | `UpdateCompanyCommand` | `UpdateCompanyForAdminCoreCommand` |
| Regla común | `CompanyIdentityRules` (formato, RUC oficial Ecuador) + `CompanyIdentityUpdate.ApplyAsync` (unicidad global del RUC → `Company.UpdateTaxIdentification`/`UpdateAdminIdentity`, `UpdatedBy`) | ídem |

Mismo dato → misma respuesta desde ambos: inválido 422 `VALIDATION_ERROR`, RUC duplicado 409 `COMPANY_RUC_ALREADY_EXISTS`, inexistente 404. Contacto/representante/regional se editan solo en Configuración → Empresa (`UpdateContactProfile`), que muestra la identidad de solo lectura.

## Retenciones: una capacidad, una política de emisión (ZH-RETENTION-EMISSION-SSOT-01, ZH-PURCHASE-RETENTION-CONFIRM-01)

Capacidad única (módulo `Retentions`, compartida por todo origen): `RetentionIssuer.IssueAsync` (unicidad por origen + revalidación server-side de elegibilidad con `IRetentionEligibilityService` + secuencia SRI "07" vía `CaptureNextAsync` + `RetentionDocument.Issue`, solo staging), `RetentionCanceller` (anula y revierte `AccountsPayable.ApplyRetention`, bloquea si la CxP tiene pagos), un único asiento `Retentions/DocumentIssued` (traductor estricto: si falla, falla la operación) y su reverso por `ReverseJournalEntryCommand` al anular. Invariante 1:1: a lo sumo una retención no anulada por origen (`ExistsActiveBySourceAsync` + índice único parcial `uq_retention_documents_active_source`).

**Política única (RETENTIONS-MODULE-DESIGN-01, decisión 15):** la retención de una Compra o un Gasto se define ANTES de confirmar el documento origen y, si hay `RetentionIntent` (`Retentions/UseCases/RetentionIntent.cs`, contrato único con `RetentionIntentValidator`), se emite DENTRO de la misma unidad de trabajo de la confirmación: documento confirmado + CxP creada con la retención aplicada + `RetentionDocument` emitido + asientos, en un único `SaveChanges`. Si cualquier paso falla (elegibilidad, punto de emisión, saldo de CxP, asiento), falla toda la confirmación y el documento sigue en borrador. Sin intención, la confirmación es exactamente la de siempre. No existe emisión posterior sobre un documento ya confirmado (`IssueRetentionCommand` y `POST /purchases/{id}/retention` fueron retirados).

Ciclo de vida: `Draft` (solo en memoria, dentro de la emisión) → `Issued` (nace, recibe número "07", aplica a la CxP y contabiliza junto con la confirmación) → `Cancelled`. La parte SRI es otro agregado (`ElectronicDocument`) y un acto explícito POSTERIOR para ambos orígenes, nunca automático al confirmar: `POST /retentions/{id}/electronic/register` (XML → firma → envío → autorización); XML y RIDE se consultan bajo demanda (`/retentions/{id}/electronic/xml`, `/ride/pdf`).

| | Gastos | Compras |
|---|---|---|
| Vista previa (borrador) | `GET /expenses/{id}/retention-eligibility` — elegibilidad; montos los escribe el usuario | `GET /purchases/{id}/retention-preview` (`CalculateRetentionHandler`) — misma `IRetentionEligibilityService` sobre las mismas bases que la emisión (`PurchaseRetentionSource`: IVA total / suma de bases imponibles) + `RetentionCalculator` para los montos propuestos (precargados, no editables). Solo sobre borradores |
| Emisión | `RetentionIntent` en `ConfirmExpenseDocumentCommand`/`CreateConfirmedExpenseCommand` | `RetentionIntent` en `ConfirmPurchaseCommand` (`POST /purchases/{id}/confirm`, campo `retention`) |
| Orden de efectos | confirmar gasto → emitir retención → CxP staged con `ApplyRetention` → `SaveChanges` (asientos de gasto y de retención) | precondiciones → recalcular impuestos → `Confirm` (congela costos) → cronograma → inventario/Kardex → CxP staged → **emitir retención + `ApplyRetention` (paso 4b)** → PVP → comunicación → `SaveChangesWithSequenceRetryAsync` (asientos de compra y de retención) |
| Concurrencia | `xmin` del gasto + índice 1:1 | `xmin` de la compra + índice único de CxP por origen + índice 1:1 (sin lock nuevo) |
| Anular | Solo anulando el gasto (cascada `RetentionCanceller`) | Cascada al anular la compra o acción propia `POST /purchases/{id}/retention/{rid}/cancel` (sin re-emisión posterior) |
| Permisos | `expenses.documents.confirm` | `purchases.update` (confirmar) / `purchases.view` (vista previa); ningún permiso de Gastos |

## Estructura por módulo
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

Checks automáticos: [check-ai-layer-boundaries.mjs](../../tools/architecture/check-ai-layer-boundaries.mjs) (AI-001 → AI-005)
Arquitectura futura IA: [AI-FOUNDATION.md](./ai-foundation.md)
Read models / analytics: [ANALYTICS-FOUNDATION.md](./ai-foundation.md)

---

## Domain Events — reglas de implementación

Ver detalle completo en [EVENT-DRIVEN-RULES.md](./events.md).

Resumen obligatorio:

| Regla | Detalle |
|-------|---------|
| Solo AggregateRoots emiten eventos | `RaiseDomainEvent(...)` dentro del AggregateRoot |
| Naming: past tense | `InvoiceCreatedEvent` ✅ — `CreateInvoiceEvent` ❌ |
| Nuevos eventos extienden `BaseDomainEvent` | Agrega `CorrelationId`, `TenantId`, `CausationId` |
| Handlers son idempotentes | Verificar si ya procesaron el evento |
| IA no vive en Domain/Application | Ver [AI-FOUNDATION.md](./ai-foundation.md) |

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

Guardrails: [ENFORCEMENT.md](./enforcement.md).
