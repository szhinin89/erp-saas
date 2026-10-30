# Backend — reglas de implementación

Contabilidad: [accounting.md](./accounting.md).

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
4. Persistir y confirmar el núcleo operativo bajo sus locks. Integración contable: [Accounting](./accounting.md).

Todo escritor del saldo **o del estado** de `SalesReceivable` lo lee por `GetByIdsForUpdateAsync`/`GetByInvoiceIdForUpdateAsync`: registrar cobro, reversar cobro (además bloquea el `Payment`), crédito por devolución de venta y anulación de factura (ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01: además bloquea la `SalesInvoice` con `ISalesInvoiceRepository.GetByIdForUpdateAsync`; decisión y efectos operativos — CxC, Kardex y outbox — en una transacción).

Orden canónico de locks (evita deadlocks; cada flujo toma solo un subconjunto, siempre en este orden):

1. advisory de documento (autorización de devolución, por factura);
2. `SalesInvoice` (anulación y autorización de devolución — ZH-SALES-RETURN-INVOICE-STATE-CONCURRENCY-01: la devolución revalida bajo este lock que la factura siga `Authorized` antes de cualquier efecto);
3. `Payment` (reversa de cobro);
4. `SalesReceivable` en orden ascendente de Id;
5. secuencia documental → Kardex (`CurrentStock`, optimista con reintento). Integración contable: [Accounting](./accounting.md).

### Revertir una venta: un solo camino por factura (ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01)

Una venta autorizada se revierte por **uno** de dos caminos excluyentes, decididos bajo el lock de la `SalesInvoice`:

| Situación de la factura `Authorized` | Anular (`CancelSalesInvoice`) | Devolver (`AuthorizeSalesReturn`) |
|---|---|---|
| Sin devoluciones, CxC sin cobros | Permitido: revierte el núcleo de la factura (CxC, Kardex) | Permitido |
| Con devolución `Draft` (o `Cancelled`) | Permitido; el Draft no tiene efectos, queda Draft y ya no puede autorizarse | Permitido mientras la factura siga `Authorized` |
| Con devolución `Authorized` (parcial o total, efectivo o crédito a CxC) | **Rechazado** — `SalesInvoiceCancellationPolicy` (422 `DOMAIN_RULE_VIOLATION`) | Permitido por el remanente |
| CxC con cobros | Rechazado (`SalesReceivable.Cancel`) | Permitido |
| Factura `Cancelled` | Rechazado (ya anulada) | **Rechazado** — `SalesReturnInvoiceEligibility` |

Una devolución autorizada es terminal y ya reingresó Kardex, reembolsó, emitió su Nota de Crédito; la anulación revierte la factura completa, así que nunca se combinan: Σ devuelto + Σ anulado ≤ vendido en cantidad, costo, reembolso/CxC e impuestos. Mismo criterio que Compras (PI-CANC-01). Ambas reglas se evalúan bajo el lock de la factura antes de cualquier efecto, por lo que la anulación y la autorización concurrentes se serializan y la segunda ve el resultado de la primera. La UI muestra "Anular" en toda factura autorizada; el servidor es la autoridad (ocultarlo requeriría exponer las devoluciones en el DTO — mejora de UX opcional, no implementada).

`SalesInvoice` y `Payment` nunca se toman en el mismo flujo; ningún flujo que tenga la CxC vuelve a pedir factura, pago o advisory. El cobro no toma `CashSession`. Sin `advisory lock` adicional ni migración: la fila existente es el recurso compartido.

### Búsqueda por barcode de presentación (A5)

La búsqueda de productos para facturar incluye el barcode exacto de una presentación
activa entre los candidatos de la empresa y tenant solicitados, dentro del contexto
autenticado. Conserva la prioridad de barcode exacto sobre coincidencias textuales y
devuelve `MatchedPackagingLevelId` junto con las presentaciones activas para la
autoselección existente del POS. No requiere coincidencia adicional por SKU o nombre.

### Tipo de emisión de una venta: snapshot inmutable (POS-EMISSION-TYPE-SNAPSHOT-01)

`SalesInvoice.EmissionPointId` y `SalesInvoice.EmissionType` son **snapshots inmutables** fijados al crear el borrador (`CreateSalesDraftHandler`, desde `ICurrentCashSession` → `EmissionPoint.EmissionType`) y gobiernan **todo** el lifecycle de la factura: estrategia de emisión al autorizar (`ISalesInvoiceEmissionStrategyResolver`), XML/provider electrónico, RIDE, tirilla y DTO.

- Un cambio posterior de `EmissionPoint.EmissionType` solo afecta **ventas nuevas**. `AuthorizeSalesInvoiceHandler` resuelve la estrategia desde `inv.EmissionType`, nunca desde el EP vivo (si divergen, solo registra un warning).
- **Fail-closed al crear**: si la caja no tiene punto de emisión, el punto no existe para la empresa o está desactivado → `ValidationFailure` claro. Prohibido el fallback silencioso a `Electronic` en cualquier capa: `SalesInvoice.CreateDraft` exige `emissionType` explícito (sin parámetro por defecto; un valor no definido lanza) y `sales_invoices.emission_type` no tiene default de BD.
- El frontend deriva el tipo efectivo igual: venta nueva = caja abierta; venta existente = snapshot de la factura (`resolveSalesEmissionType`). La UI electrónica (SRI, clave de acceso, RIDE, XML, diagnóstico, Forma de pago SRI) depende de ese tipo, nunca de la caja actual.

### Efectivo entregado: dato operacional del settlement (POS-CASH-TENDERED-01)

El efectivo entregado es dato operacional del settlement y **no modifica el importe aplicado ni el total financiero**.

- `SalesInvoicePayment.Amount` = importe aplicado (único valor financiero: total cobrado, caja, CxC, contabilidad, conciliación).
- `SalesInvoicePayment.TenderedAmount` (`numeric(18,2)`, nullable) = efectivo físico entregado para ESE pago. Solo en formas de cobro con `PaymentMethod.AffectsPhysicalCash`; ≥ `Amount`; > 0; 2 decimales. Lo valida `SalesPaymentHelper` (contrato `SalesPaymentInput.TenderedAmount`), el dominio (`SetTenderedAmount`) y un CHECK de BD.
- `ChangeAmount` = `TenderedAmount − Amount`, derivado (no persistido). En multipago solo la porción efectivo lo tiene.
- La tirilla (`receipt-print-payload`) reconstruye "Efectivo recibido / Vuelto" desde los pagos persistidos, así que la tirilla inicial y cualquier reimpresión muestran lo mismo. Las ventas anteriores a este dato no lo muestran (no se infiere).

### Turnos de caja: uno por empresa y sin sobregiro

- Los turnos son independientes por TenantId + CompanyId: un usuario tiene como máximo un turno abierto en cada empresa (puede tener uno en la empresa A y otro en la B) y cada caja registradora, como máximo uno. Lo valida `OpenCashSessionHandler` con el alcance de la empresa activa y lo garantizan los índices únicos parciales `ux_cash_sessions_open_per_company_user` (tenant, empresa, usuario) y `ux_cash_sessions_open_per_register`.
- Sin sobregiro: una salida de efectivo que elige el cajero (egreso manual, retiro, depósito bancario y pago a proveedor en efectivo) no puede superar el efectivo esperado del turno (`CashSession.CurrentBalance`, el mismo valor del arqueo). Se valida con la sesión bloqueada, sin override, y se rechaza con «La caja dispone de $X y se intenta registrar…». Los reembolsos que registra el sistema (devolución de venta, saldo a favor) no pasan por esta regla.

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

Capacidad única (módulo `Retentions`, compartida por todo origen): `RetentionIssuer.IssueAsync` (unicidad por origen + revalidación server-side de elegibilidad con `IRetentionEligibilityService` + secuencia SRI "07" vía `CaptureNextAsync` + `RetentionDocument.Issue`, solo staging), `RetentionCanceller` (anula y revierte `AccountsPayable.ApplyRetention`, bloquea si la CxP tiene pagos), integración contable según [Accounting](./accounting.md). Invariante 1:1: a lo sumo una retención no anulada por origen (`ExistsActiveBySourceAsync` + índice único parcial `uq_retention_documents_active_source`).

**Política única (RETENTIONS-MODULE-DESIGN-01, decisión 15):** la retención de una Compra o un Gasto se define ANTES de confirmar el documento origen y, si hay `RetentionIntent` (`Retentions/UseCases/RetentionIntent.cs`, contrato único con `RetentionIntentValidator`), se emite DENTRO de la misma unidad de trabajo de la confirmación: documento confirmado + CxP creada con la retención aplicada + `RetentionDocument` emitido, en la misma transacción. Si falla el núcleo operativo/tributario (elegibilidad, punto de emisión, saldo de CxP), falla toda la confirmación y el documento sigue en borrador. Integración contable: [Accounting](./accounting.md). Sin intención, la confirmación es exactamente la de siempre. No existe emisión posterior sobre un documento ya confirmado (`IssueRetentionCommand` y `POST /purchases/{id}/retention` fueron retirados).

Ciclo de vida: `Draft` (solo en memoria, dentro de la emisión) → `Issued` (nace, recibe número "07", aplica a la CxP) → `Cancelled`. La parte SRI es otro agregado (`ElectronicDocument`): su transmisión inicial se dispara después del commit para ambos orígenes por la capacidad única de Retentions. Desde que existe el documento, ElectronicDocuments posee su recuperación. XML y RIDE se consultan bajo demanda; detalle fiscal en [verificación de anulación](#verificación-fiscal-de-anulación-de-retenciones).

| | Gastos | Compras |
|---|---|---|
| Vista previa (borrador) | `GET /expenses/{id}/retention-eligibility` — elegibilidad; montos los escribe el usuario | `GET /purchases/{id}/retention-preview` (`CalculateRetentionHandler`) — misma `IRetentionEligibilityService` sobre las mismas bases que la emisión (`PurchaseRetentionSource`: IVA total / suma de bases imponibles) + `RetentionCalculator` para los montos propuestos (precargados, no editables). Solo sobre borradores |
| Emisión | `RetentionIntent` en `ConfirmExpenseDocumentCommand`/`CreateConfirmedExpenseCommand` | `RetentionIntent` en `ConfirmPurchaseCommand` (`POST /purchases/{id}/confirm`, campo `retention`) |
| Orden de efectos | confirmar gasto → emitir retención → CxP staged con `ApplyRetention` → `SaveChanges` del núcleo | precondiciones → recalcular impuestos → `Confirm` (congela costos) → cronograma → inventario/Kardex → CxP staged → **emitir retención + `ApplyRetention` (paso 4b)** → PVP → comunicación → persistencia del núcleo |
| Concurrencia | `xmin` del gasto + índice 1:1 | `xmin` de la compra + índice único de CxP por origen + índice 1:1 (sin lock nuevo) |
| Anular | Solo anulando el gasto (cascada `RetentionCanceller`) | Solo anulando la compra (cascada `RetentionCanceller`) |
| Permisos | `expenses.documents.confirm` | `purchases.update` (confirmar) / `purchases.view` (vista previa); ningún permiso de Gastos |

**Anulación (ZH-RETENTION-CANCELLATION-LIFECYCLE-01) — una sola semántica para todo origen:** la retención forma parte de la confirmación de su documento origen, así que solo se anula como consecuencia de anular ese origen (`CancelPurchaseHandler`/`CancelExpenseDocumentHandler` → `RetentionCanceller`: anula la retención, `ReverseRetention` en la CxP exactamente una vez, bloquea si la CxP tiene pagos, todo en la transacción operativa de anulación del origen; integración contable en [Accounting](./accounting.md)). `Cancelled` es terminal: no hay reemisión ni reemplazo, y una retención anulada no se registra ante el SRI. Corregir una retención = anular el documento origen y registrarlo de nuevo. No existe anulación aislada (`CancelRetentionCommand` y `POST /purchases/{id}/retention/{rid}/cancel` fueron retirados); el gate `RetentionCancellationLifecycleTests` impide reintroducirla. Concurrencia: la anulación del origen serializa (Compras: Lock A + `xmin`; Gastos: `xmin`) y un reintento sobre el origen ya anulado se rechaza sin segundo reverso.

**Ciclo electrónico (ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A, [ADR-036](../decisions/ADR-036-retention-electronic-document-cancellation.md)):**
- **Transmisión automática:** confirmar Compra/Gasto con retención inicia su transmisión DESPUÉS del commit, vía `IRetentionElectronicTransmission.StartAsync` → `IElectronicDocumentIssuer.RegisterAsync` (único pipeline). `RetentionElectronicRecoveryJob` (cada minuto) retoma retenciones `Issued` sin comprobante o con uno en `Draft` (gracia de 2 min). Ambos entran por el mismo `StartAsync`; un solo comprobante por origen (`uq_electronic_document_source`). `POST /retentions/{id}/electronic/register` queda solo como recuperación (`electronic-documents.retry` + permiso de acción del origen).
- **Gate SSOT:** `IElectronicDocumentSourceLifecycleGuard` (resuelto por `SourceModule`; Retentions = solo `Issued`) dentro de `ElectronicDocumentIssuer`: antes de registrar, de generar XML, de reactivar y de cada consulta externa. Jobs, controllers y handlers no repiten la regla. Un origen sin guard (Ventas/NC) conserva el comportamiento v1.0.
- **Despacho:** para orígenes con guard, `XmlGenerated→Signed→Dispatching` se persisten en una transacción corta bajo `SELECT … FOR UPDATE` de la retención (revalidando `Issued`); la llamada SOAP ocurre después del commit. `Dispatching`, `Signed` histórico y `Received` nunca se reenvían: solo se consulta la autorización; una respuesta no concluyente deja el estado intacto (estado compacto "Requiere conciliación").
- **Anulación del origen:** `RetentionCanceller` consulta `IElectronicDocumentSourceCancellation` (mismo lock). Sin intento externo (sin comprobante, `Draft`, `Failed`, `DeadLetter` de un fallo previo a la firma) → se permite y el comprobante queda `Discarded` en el mismo `SaveChanges`. `Signed`/`Dispatching`/`Sent`/`Received`/`DeadLetter` de envío → 422 `ELECTRONIC_DOCUMENT_IN_PROCESS`. `Authorized` → 422 `ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT`. En ambos bloqueos no hay `Cancelled` ni reverso de CxP, asientos o Kardex. Con `requestSriAnnulment: true` el `Authorized` inicia la anulación asistida (ver siguiente punto).
- **Anulación ante el SRI de una retención AUTORIZADA (ZH-RETENTION-SRI-ANNULMENT-01/01B, ADR-036 §24–§25):** no existe WS para SOLICITAR la anulación (solicitud asistida en SRI en Línea); ConsultaComprobante (Ficha Técnica v2.34 §8) existe para CONSULTARLA, y el SRI es la fuente de verdad del estado fiscal (AUTORIZADO / PENDIENTE DE ANULAR / ANULADO).
  - `CancelPurchase`/`CancelExpense` con `requestSriAnnulment` crea una `RetentionAnnulmentRequest` (a lo sumo una abierta por retención). El comprobante pasa a `AnnulmentPending` y la CxP queda retenida; el origen sigue Confirmed y no hay reversos.
  - "Ya presenté la solicitud" (`/retentions/annulments/{id}/submission`) registra la presentación y verifica enseguida; luego verifica a demanda (`…/sri-verification`) y por polling (`RetentionElectronicRecoveryJob`, intervalo 30 min por solicitud).
  - Consulta tipada `ISriDocumentStatusQuery` sobre el mismo `SriSoapClient` (no hay segundo cliente SOAP). El resultado técnico (`SriStatusQueryOutcome`) es distinto del estado fiscal (`SriFiscalStatus`). Siempre se consulta sin transacción y se aplica bajo el lock de la retención.
  - ANULADO informado por el SRI: solicitud `Accepted` con evidencia técnica, y comprobante → `Cancelled` (`ConfirmExternalAnnulment`, el único camino; `MarkCancelled` se eliminó). Después, `FinalizeAsync` anula el origen por su flujo oficial (`IRetentionOriginCancellation`) exactamente una vez.
  - AUTORIZADO / PENDIENTE DE ANULAR / NO AUTORIZADO / consulta fallida: sin cambio fiscal ni reversos. El usuario **nunca** declara ANULADO: no existe resolución manual.
  - Desistir: antes de presentar, o con el SRI confirmando AUTORIZADO (`CanBeAbandoned`). El comprobante vuelve a `Authorized` y la CxP se libera.
  - CxP retenida: la regla vive en `AccountsPayable` (pagos, créditos de proveedor, notas de crédito y devoluciones → 422 `RETENTION_ANNULMENT_PENDING`).
  - Permisos: anular el origen (`purchases.update` / `expenses.documents.cancel`).
  - Plazo ordinario: política versionada GLOBAL por fecha de emisión; detalle en [Compras](purchases.md#5-anulación-confirmed--cancelled). Sin calendario oficial, es informativo.
- **XML/RIDE:** autorizados por el origen, server-side (`IRetentionSourceAccess`): Compra → `purchases.view`, Gasto → `expenses.documents.view`; sin acceso → 404.

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
Read models / analytics: [ai-foundation.md](./ai-foundation.md)

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


## Compras, Inventario e Ítems

Reglas funcionales: [purchases.md](./purchases.md), [inventory.md](./inventory.md), [items.md](./items.md).


## Background Processing

SSOT de orquestación recurrente implementada. Rationale: [ADR-043](../decisions/ADR-043-background-processing-ssot.md).
Operación y despliegue: [runbook](../deployment/README.md).

### Arquitectura única

`HangfireRecurringJobs.All` es el catálogo inmutable ID → tipo concreto y solo define
identidad. Deriva DI, validación, registro y sincronización. Los siete tipos implementan
`IRecurringJob` y reciben BatchSize explícito. `HangfireOptions` es el único modelo de
valores operativos (Server, storage, Workers por cola y, por job, Enabled/Cron/Queue/
BatchSize): binding estricto, IValidateOptions y ValidateOnStart. Claves desconocidas
o faltantes abortan incluso con Server OFF. Storage exige cadena PostgreSQL explícita con
Host/Database/Username, sin fallback a la base ERP; usa el destino explícito del entorno autorizado.
Multiplexing se rechaza porque el lock requiere afinidad de sesión.

`RecurringJobRunner.RunAsync(id)` es la única entrada de Hangfire: revisa Enabled,
adquiere `pg_try_advisory_lock(hashtextextended('ERP.Jobs/' + id, 734108))` en una
conexión física mantenida durante la ejecución, crea scope fresco y emite logs y
métricas. Lock ocupado significa skipped, sin error ni espera. Finalmente libera
el lock también al fallar/cancelar; una caída del proceso lo libera en PostgreSQL.
AutomaticRetry(0): la siguiente corrida descubre nuevamente el trabajo durable.
Los scopes tenant/company por candidato y los controles de persistencia existentes
siguen siendo autoridad de aislamiento e idempotencia.

Un solo hosted service sincroniza antes de iniciar workers: AddOrUpdate para ON;
RemoveIfExists para OFF y todos los IDs fuera del catálogo. La cola explícita se
guarda en `Job.Queue` mediante Hangfire 1.8. Cada cola tiene su servidor y workers
explícitos. Ningún servidor consume `default`.

### Configuración final

| Job | Cola | Cron UTC | BatchSize |
|---|---|---|---:|
| process-outbox | core | `* * * * *` | 50 |
| process-communications | communications | `* * * * *` | 50 |
| reconcile-electronic-document-communications | communications | `*/10 * * * *` | 200 |
| electronic-document-retry | sri | `* * * * *` | 50 |
| sales-electronic-document-recovery | sri | `* * * * *` | 50 |
| retention-electronic-recovery | sri | `* * * * *` | 50 |
| expire-user-sessions | maintenance | `0 4 * * *` | 50 |

Todos declaran Enabled explícito. Cron admite cinco campos numéricos con listas,
rangos y pasos. La cola de cada job es configuración (minúsculas, nunca `default`);
`Workers` declara exactamente las colas usadas por los jobs, con 1..256 workers cada
una: core=1, communications=2, sri=2, maintenance=1 como valores iniciales para afinar
con métricas. Concurrencia máxima 1 por ID es invariante del runner (advisory lock),
no una opción: varios workers corren distintos jobs de la cola, nunca el mismo ID.

API sin workers: `Hangfire:Server:Enabled=false` (versionado). Host combinado:
Server.Enabled=true. Worker separado, desde el directorio publicado de API:

```powershell
dotnet ERP.API.dll --worker --Hangfire:Server:Enabled true
```

El worker usa Generic Host sin Kestrel. API y worker componen la infraestructura común
solo con `ErpHostRegistration` (fuentes de configuración, Serilog, caché Redis/memoria,
Infrastructure/Application, Data Protection y opciones) más el registro único de
Background Processing; lo exclusivo de HTTP queda en `Program.cs`. Con más de una instancia de
API, Redis es obligatorio: el despliegue declara `Redis:Required=true` y el host no arranca sin
conexión Redis ([security.md, SEC-06](security.md)). El despliegue aplica migraciones antes
de arrancar workers separados; API conserva su migración previa al bootstrap.
Ambos requieren misma base ERP, storage Hangfire y volumen FileStorage/keyring.
No hay Dashboard de Hangfire. Se eliminan las claves antiguas del entorno, incluidos
user-secrets: Enabled → Server:Enabled y ConnectionString → StorageConnectionString,
preservando valores; `Dashboard:*` y `Jobs:*:MaxConcurrency` se eliminan. Ninguna se
lee como alias: el binding estricto las rechaza al arrancar.

### Lotes, fairness y ownership

Todas las consultas de candidatos usan LIMIT/Take explícito. Retentions distribuye
el presupuesto total B entre floor(B/3) finalizaciones, B−2×floor(B/3) transmisiones
y floor(B/3) verificaciones. B mínimo=3; cada stream tiene progreso independiente.

La migración `20261015000000_BackgroundJobProgress` crea checkpoints técnicos en
PostgreSQL, sin datos de negocio ni preferencias por empresa. Reintentos, Sales y
Retentions rotan por UUID estable después del último lote descubierto, incluyendo
los candidatos fallidos. Guardar antes de procesar evita que un poison item acapare
los lotes y conserva progreso al reiniciar o cambiar de nodo. Sin OFFSET creciente.

El reconciliador conserva orden `(created_at, id)` y persiste su checkpoint al
terminar cada lote; vuelve al principio al llegar al final. Si el último lote llena
exactamente B, la siguiente corrida vacía detecta el final. Las anomalías se
revisitan en otra vuelta. Se elimina el cursor singleton en memoria. El anti-join
conserva el CTE materializado; LIMIT acota filas cargadas, COUNT/MIN quedan en SQL.

Las preferencias auto_retry_enabled y email_on_authorization se correlacionan por
tenant/company/scope en SQL antes del límite, con la clave de `OrgSettingKeys` y la
semántica de `OperationalPreferencesResolver` (`DefaultOnCompanySwitch`: solo un valor
que se lee como `false` apaga; faltante/corrupto conserva el default activo). Es la
única decisión previa al lote: el job de reintentos y el reconciliador no vuelven a
resolverlas; el servicio de comunicación conserva su guard al encolar. La elegibilidad
de reintento (estados, máximo efectivo por empresa y backoff `min(1·2^n, 16)` minutos) existe una sola
vez como predicado de `ElectronicDocumentRetryPolicy`, traducido a SQL. Communications resuelve el perfil de cada
fila y conserva claims, leases, fencing y política de reintento. No hay preferencias
por empresa nuevas para procesos técnicos.

Retentions recupera la transmisión inicial únicamente antes del registro de
ElectronicDocument. Desde que existe, incluido Draft, ElectronicDocuments es el
único dueño de recuperación fiscal. Retentions excluye cualquier documento por
origen existente; el issuer canónico conserva unicidad e idempotencia ante carreras.
Sales recovery permanece create-only: el mismo `SalesElectronicDocumentRecoveryJob`
descubre facturas y devoluciones autorizadas sin `ElectronicDocument` por origen.
Las facturas conservan su elegibilidad: emisión electrónica, punto de emisión y
número definitivo `EST-PTO-SECUENCIAL`, con secuencial distinto de cero. Las
devoluciones requieren `CreditNoteDocumentNumber` definitivo y distinto de cero;
su factura origen debe cumplir la elegibilidad anterior y pertenecer al mismo
tenant y empresa. La sucursal se obtiene de esa factura. Ambos tipos comparten
el límite del lote y el checkpoint rotativo `sales-recovery`.

Antes de registrar se releen la elegibilidad y el ownership tenant/empresa/sucursal
en el scope del candidato. `IElectronicDocumentRegistration.RegisterMissingAsync`
registra Invoice para la factura o CreditNote para la devolución, siempre con
`SourceModule = "Sales"` y `SourceEntityId` del agregado correspondiente. La ausencia
se verifica por origen; un documento existente, incluso Draft, excluye la recuperación.
La unicidad canónica por origen protege ante carreras; el perdedor descarta su tracker
y verifica la existencia del ganador. No se captura otro secuencial ni se modifica
el número asignado; tampoco se repiten autorización, inventario, caja, reembolsos,
CxC ni contabilidad. El registro queda en Draft y su procesamiento posterior
pertenece exclusivamente al pipeline común de ElectronicDocuments, sin otro job
ni motor electrónico. Esta recuperación no cambia SOAP, firma, XML/XSD ni esquema de BD.

### Health y métricas

No existe masterdata-reconciliation: servicio, interfaz, job ni health.
Integridad de membresías/MasterData sale de ready y conserva endpoints diagnósticos.
Ready conserva conectividad y probes configurados.

Meter ERP.Jobs: duración (s), procesados, fallidos, skipped por lock, backlog y edad
del pendiente más antiguo (s). Etiquetas técnicas job/stream, sin secretos ni PII.
Backlog/edad son gauges por stream; agregados SQL sin cargar históricos. Prometheus
de API incluye ERP.Jobs. Un worker separado emite el mismo Meter; su exportación
requiere un collector en ese proceso (no se añade un servidor HTTP al worker).

### Máximo por empresa y preferencia de correo

`electronic_documents.max_retry_attempts` gobierna candidatos y DeadLetter.
`ElectronicDocumentRetryMaximum` centraliza default 5, rango 1..10 y la expresión
única de resolución (faltante/corrupto: default), compilada para el resolver y
traducida a SQL para candidatos/contador. `ElectronicDocumentRetryPolicy` conserva
estados/backoff y compone la elegibilidad; el filtro efectivo precede al BatchSize.
El job no interpreta configuración. El issuer lee el mismo setting scoped y aplica
la misma resolución para DeadLetter. No hay un límite fijo paralelo `MaxAttempts`.

`communications.sales_invoice_authorized.enabled` duplicaba la decisión vigente
`electronic_documents.email_on_authorization`: se eliminan clave, definición y
contrato legacy, sin conservar claves ni variantes legacy.
No hay alias ni segunda ruta; Communications conserva su guard y perfil SMTP.


## Verificación fiscal de anulación de retenciones

Contrato vigente de consulta; sin segundo cliente SOAP ni resolución manual.

### Contrato oficial (Ficha Técnica Comprobantes Electrónicos Esquema Offline v2.34, julio 2026, §8; idéntico en v2.32)

| Elemento | Valor |
|---|---|
| WSDL pruebas | `https://celcer.sri.gob.ec/comprobantes-electronicos-ws/ConsultaComprobante?wsdl` |
| WSDL producción | `https://cel.sri.gob.ec/comprobantes-electronicos-ws/ConsultaComprobante?wsdl` |
| Namespace | `http://ec.gob.sri.ws.consultas` |
| Operación | `consultarEstadoAutorizacionComprobante(claveAcceso: String)` → `EstadoAutorizacionComprobante` (`RespuestaConsultaComprobante`) |
| Respuesta exitosa | `claveAcceso`, `mensajes`, `estadoAutorizacion`, `tipoComprobante`, `rucEmisor`, `fechaAutorizacion` (con offset, p.ej. `2024-12-12T10:49:37-05:00`) |
| Estados (§8.4) | `AUTORIZADO`, `NO AUTORIZADO`, `PENDIENTE DE ANULAR`, `ANULADO` |
| Respuesta de error | `estadoConsulta = RECHAZADA` + `mensaje` con `identificador = 99` ("ERROR AL CONSULTAR DATOS DEL SERVICIO WEB"); casos documentados: fecha de emisión fuera del rango permitido, y "No existen datos para los parámetros ingresados" |

La prosa de la ficha llama "estadoAutorizacion = RECHAZADA" al caso de error, pero sus ejemplos XML usan `estadoConsulta`. El cliente trata `RECHAZADA` en cualquiera de los dos elementos como **consulta rechazada**, nunca como estado fiscal. La ficha **no** documenta un WS para solicitar la anulación.

### Abstracción (sin duplicar SOAP)

- **`ISriDocumentStatusQuery`** (Application, `Common/Interfaces/SRI`) → `SriDocumentStatusResult`. Es el SSOT tipado del estado fiscal.
- Implementación: `SriDocumentStatusQuery` (Infrastructure). Es un adaptador sobre el **mismo** `SriSoapClient` de recepción y autorización: comparte HTTP, reintentos, SOAP Fault, parsing de `<mensajes>` y `SriSettings.WsdlUrl`. `QueryDocumentStatusAsync` agrega solo el envelope y el parser. El endpoint se deriva del `WsdlUrl` de Recepción de la empresa (mismo ambiente celcer/cel). Si el `WsdlUrl` no apunta a `RecepcionComprobantesOffline`, la consulta devuelve `Unavailable` y no se postea a ningún otro servicio.
- `PostSoapAsync` se generalizó internamente (`PostSoapCoreAsync`) para distinguir timeout de red caída. Recepción y autorización no cambian.

### Estados tipados (Domain, `ElectronicDocuments/Enums`)

| `SriStatusQueryOutcome` (técnico) | `SriFiscalStatus` (fiscal, solo con `Success`) |
|---|---|
| `Success`, `Rejected` (RECHAZADA / 99), `Timeout`, `Unavailable` (red, HTTP, SOAP Fault, sin configuración), `Unknown` (no XML, literal no oficial, otra clave de acceso) | `Authorized`, `NotAuthorized`, `PendingAnnulment`, `Annulled`; `Unknown` si la consulta no fue exitosa |

Invariantes, aplicadas por cliente, servicio y dominio:
- una consulta no exitosa **nunca** trae estado fiscal;
- `RECHAZADA` **nunca** se mapea a `NotAuthorized`;
- un timeout **nunca** es `Annulled`.

### Flujo

1. **Solicitar:** crear la solicitud asistida por el flujo del origen, sin reversos locales ni envío automático de anulación a SRI.
2. **"Ya presenté la solicitud"** (`POST /retentions/annulments/{id}/submission`): registra la presentación y **consulta enseguida** ConsultaComprobante. Si la verificación no se puede aplicar, la presentación queda registrada igual y la respuesta informa `RETENTION_ANNULMENT_SRI_VERIFICATION_FAILED`.
3. **Verificar** (`IRetentionAnnulmentService.VerifyWithSriAsync`, `POST /retentions/annulments/{id}/sri-verification`, sin body). Solo con la solicitud `PendingSriResolution`.
   - La consulta SOAP corre **sin transacción ni lock**.
   - Después se toma el lock de la retención, se relee la solicitud y, si sigue pendiente, se registra la consulta (`RecordSriCheck`: fecha, resultado, estado fiscal, literal crudo, contador).
   - La consulta se audita (`SriChecked`) **solo cuando cambia lo informado**, para que el polling no inunde la auditoría.

| SRI informa | Efecto | Código (200) |
|---|---|---|
| `AUTORIZADO` | Sigue vigente: sin finalización, sin reversos. El usuario puede **desistir**  | `RETENTION_ANNULMENT_SRI_STILL_AUTHORIZED` — "El SRI todavía mantiene vigente el comprobante" |
| `PENDIENTE DE ANULAR` | Comprobante `AnnulmentPending`, retención `Issued`, origen `Confirmed`, CxP retenida; se re-consulta | `RETENTION_ANNULMENT_SRI_PENDING` — "Pendiente de anulación en SRI" |
| `ANULADO` | `AcceptSriAnnulment` (solo si la última consulta registrada es ese ANULADO). Evidencia: fecha de verificación (empresa), referencia `ConsultaComprobante ANULADO <UTC>` y respuesta SOAP cruda (`sri_annulment_evidence`). Comprobante `Cancelled`. Solicitud `Accepted`. Después, **fuera de la transacción**, la finalización idempotente existente (`FinalizeAsync`), una vez | `RETENTION_ANNULMENT_FINALIZED` (o `…_FINALIZATION_PENDING`) |
| `NO AUTORIZADO` | **Sin acción automática** — Decision Required  | `RETENTION_ANNULMENT_SRI_NOT_AUTHORIZED` |
| Rechazada (99) / timeout / red / desconocido | Solo se registra el fallo técnico; sin cambio fiscal ni finalización | `RETENTION_ANNULMENT_SRI_VERIFICATION_FAILED` — "No fue posible verificar el estado en SRI" |

4. **Sin resolución manual de ANULADO.** No existen `ResolveRetentionAnnulmentCommand`, `POST …/resolution`, `RetentionAnnulmentResult`, `RetentionAnnulmentRequest.Resolve` y el modal de resolución. No queda contingencia manual (preferencia del ticket: no tener fallback hasta justificarlo con un caso real). Si se justifica, deberá ser un flujo separado, con permiso fuerte y evidencia, y con su propio ADR. El dominio impide aceptar sin un ANULADO consultado.

### Desistimiento

`RetentionAnnulmentRequest.CanBeAbandoned` es la regla única para el agregado y para la UI (`canAbandon` en el DTO). Permite desistir:
- antes de presentar; o
- ya presentada, **solo** si la última consulta exitosa informa **AUTORIZADO** (el SRI no anuló; p.ej. el receptor no aceptó).

Con PENDIENTE DE ANULAR, NO AUTORIZADO o sin una consulta exitosa no se puede, porque el SRI todavía podría anularlo. Desistir revierte el comprobante a `Authorized` y libera la CxP.


### Límites y finalización

NO AUTORIZADO sobre un comprobante previamente autorizado no causa reversos ni
finalización automática; no se inventa una decisión fiscal. Una consulta no exitosa
no confirma ANULADO. Rejected/Expired no tienen transición operativa en este flujo.
El polling, lotes y ownership de recuperación se definen solo en
[Background Processing](#background-processing), no en una política paralela de Retentions.
`FinalizeAsync` usa el flujo oficial del origen, idempotente. Después del commit,
`IAccountingPostingDispatcher.DispatchPendingAsync` da el intento inicial a sus reversos
antes de marcar la finalización; un fallo contable conserva la obligación Failed sin
revertir la anulación ni generar reintento contable automático. Contrato: [Accounting](accounting.md).

**Respuestas inciertas del SRI (Caso #32, todos los orígenes).** Un comprobante que salió o
pudo salir al SRI sin resultado conocido (`Signed`, `Dispatching`, `Sent`, `Received`, o
`DeadLetter` que venía de ellos; `ElectronicDocument.RequiresSriReconciliation`) nunca se
reenvía automáticamente: el reintento (job o Monitor) solo consulta la autorización por clave
de acceso. AUTORIZADO se conserva; NO AUTORIZADO pasa a `Rejected`; una consulta no concluyente
deja el estado intacto ("Requiere conciliación"). Solo el primer envío del pipeline transmite.

Conciliación manual: `POST /api/v1/electronic-documents/{id}/reconciliation` (Monitor,
"Registrar conciliación"), permiso `electronic-documents.reconcile`, evidencia de texto
obligatoria (referencia ≤ 150 y observación ≤ 300). Registra la evidencia en la auditoría del
comprobante (`ReconciliationRecorded`, sin cambio de estado) y reconsulta por el mismo reintento.
Nunca autoriza, rechaza, descarta ni libera un reenvío por declaración del usuario: el estado
fiscal solo lo decide una respuesta del SRI. En Retenciones con origen que ya no permite
procesamiento, la evidencia queda registrada y no hay consulta. Un comprobante que el SRI nunca
recibió permanece pendiente de conciliación. Pendientes: [STATUS](../../STATUS.md#riesgos-y-limitaciones-vigentes).


## Configuración organizacional

Contrato implementado de configuración persistida por empresa y scope. Defaults de factura
de venta: [Valores por Defecto de Facturación](./frozen-infrastructure.md#valores-por-defecto-de-facturación).
La precisión numérica no es un setting: [CompanyPrecisionPolicy](./data-standards.md#estándar-de-precisión-numérica).

### Almacenamiento y escritura

- `OrgSetting` (`org_settings`) guarda un valor por `(TenantId, CompanyId, Scope, ScopeId, Key)`
  (`uq_org_settings_scope_key`). `OrgScope` persistido: Company, Branch, Establishment,
  EmissionPoint, Warehouse. La columna de sistema `xmin` es el token de concurrencia optimista.
- `ConfigurationDefinitionCatalog` (Domain) es el registro único de keys: módulo, tipo
  semántico (`ConfigurationDataType` y su tipo persistido), scopes permitidos, scope por
  defecto, `FallbackStrategy`, validador y flags `RequiresAudit`/`IsSensitive`/
  `RequiresSnapshot`/`IsUserEditable`. Falla al construirse ante keys duplicadas, sin scopes
  o con `DefaultScope` fuera de `AllowedScopes`. No existen metadatos de presentación ni de acceso.
- `OrgSettingsRepository.UpsertAsync` es el único punto de escritura: rechaza key desconocida,
  scope no permitido, `DataType` distinto del declarado o valor inválido
  (`ConfigurationDefinitionViolationException`), sin corregir ni guardar parcialmente.
  Null o vacío significa "sin configurar". `FallbackStrategy` declara la estrategia; la aplica
  el resolver tipado que consume la key. `RequiresSnapshot` no tiene consumidor: no hay
  snapshots de configuración en documentos.

### Lectura

Application no depende de `IOrgSettingsRepository` para decidir comportamiento: lee mediante
resolvers tipados (`IInvoiceDefaultsResolver`, `ICompanyBrandingResolver`,
`ICatalogConfigurationResolver`, `ISalesFiscalPolicyResolver`, `IOrgConfigResolver`,
`IOperationalPreferencesResolver`). Solo los handlers administrativos de la configuración
enumerados en `OrgSettingsAccessGuardrailTests` leen o escriben el repositorio; el test falla
si otro tipo de Application lo referencia o si una entrada del allowlist deja de existir.
La marca pertenece a Company (`company.branding.*`, `ICompanyBrandingResolver`); RIDE la consume.

### Principal y default únicos

Índices únicos filtrados: una sucursal principal por empresa (`uq_branches_tenant_company_main`),
un establecimiento principal por empresa (`uq_establishment_tenant_company_main`), un punto de
emisión default por establecimiento (`uq_emission_point_establishment_default`), una bodega
principal por sucursal (`uq_warehouses_tenant_branch_main`) y una lista de precios default por
empresa (`uq_price_lists_tenant_company_default`). Sucursal, establecimiento, punto de emisión
y lista de precios desmarcan el anterior al marcar uno nuevo. Ningún caso de uso cambia la
bodega principal; el índice es su protección.

### Historial

`ConfigurationChangeLog` es append-only y se escribe solo por `IConfigurationChangeLogger`.
`UpsertAsync` registra las keys `RequiresAudit=true` cuando el valor cambia; también se
registran los cambios de principal/default anteriores y los campos críticos de `SriSettings`
(sin certificado binario ni contraseña). `ConfigurationChangeLogGuardrailTests` exige que el
repositorio dependa del logger. Consulta de solo lectura: `GET /api/v1/configuration/change-log`
(`erp.companies.update`). No existe UI de historial.


## Condiciones de pago y cronogramas

PaymentTerm es el catálogo tenant-wide de plazos; el default del tercero pertenece a
Company y rol (`CompanyBpPurchaseSettings` / `CompanyBpSalesSettings`).
`PaymentTermDefaultResolver` resuelve documento explícito activo → default activo del
tercero → exigir selección explícita. No elige el primer registro ni infiere por días.
La colección de cuotas pertenece al documento y se conserva al generar CxC/CxP;
un cambio de catálogo/default no recalcula documentos confirmados. Sales conserva su
`SalesPaymentSchedule`; Compras y Gastos sus colecciones propias, sin entidad compartida.
CreditTerm no es el SSOT de esos cronogramas ni se fusiona con PaymentTerm.
Rationale: [ADR-033](../decisions/ADR-033-payment-term-ssot-and-document-schedules.md).


## RIDE

RIDE es lectura/presentación del XML fiscal, sin emitir ni alterar ElectronicDocuments.
Parsers por tipo producen un modelo neutro; plantilla, branding, renderer y storage
son contratos separados. QuestPDF es implementación Infrastructure, no contrato de Application.
El PDF físico se guarda por IFileStorage y su estrategia de nombres; la BD conserva metadatos.
La caché considera XML, plantilla, branding, renderer y versión de especificación.
La metadata de autorización se compone fuera del parser; la fecha se presenta en Company.Timezone,
con ausencia explícita cuando no está disponible. Su huella afecta la versión de plantilla;
SourceXmlHash sigue representando únicamente XML. No hay otro caché.

Las queries HTTP conservan autorización y scope. `IRideBackgroundDocumentService` es
una entrada interna verificada: TenantId/CompanyId no vacíos deben coincidir con el contexto;
delega en IRideDocumentService y el mismo RidePipeline. No autentica artificialmente
al job ni permite HTTP anónimo. `ElectronicDocumentRideSourceXmlProvider` consume el
contrato interno de ElectronicDocuments; Ride no referencia Sales ni sus repositorios.
Adjuntos de Communications conservan PendingSource reintentable y omisión opcional;
fallos registran etapa/tipo de excepción, nunca XML, tokens, destinatarios ni rutas sensibles.
Rationale: [ADR-025](../decisions/ADR-025-ride-design-freeze.md).
