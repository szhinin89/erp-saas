# Diseño — Devolución de Venta + Nota de Crédito SRI (P0-01 / P0-02)

**Tipo de documento:** Diseño de arquitectura (Fase de diseño). No contiene código, ni migraciones, ni implementación.
**Fecha:** 2026-07-30
**Origen:** `ERP_CORE_SUMAK_READINESS_AUDIT.md` §16, §19 (P0-01, P0-02).
**Alcance de esta fase:** Sales, Inventory, Caja/Finance, ElectronicDocuments, Accounting, Audit — exclusivamente en relación a Devolución de Venta. P1-01, P1-02, P1-06, P0-03 quedan fuera y no se tocan.
**Metodología:** inspección exhaustiva del código real (4 agentes de solo lectura en paralelo) + síntesis de diseño. Ningún archivo fue modificado.

---

## 1. Estado actual (hechos verificados)

### 1.1 Sales

- `SalesInvoice` (`ERP.Domain/Modules/Sales/Entities/SalesInvoice.cs`): estados `Draft(1) → Authorized(2) → Cancelled(3)`. Solo 3 estados, sin `PartiallyReturned`/`Returned`.
- `Authorize()`: única transición a `Authorized`; congela cada línea (`SalesInvoiceDetail.Freeze()` → `IsFrozen=true`, inmutable en adelante) y snapshotea 4 totales (`AuthorizedSubtotal/TotalTax/TotalDiscount/GrandTotal`) que quedan congelados para siempre. Raise `SalesInvoiceAuthorizedEvent`.
- `Cancel(reason, cancelledBy)`: solo legal desde `Authorized`. **No dispara ningún evento de dominio.** La orquestación vive en `CancelSalesInvoiceHandler` (`CancelSalesUseCases.cs`), que:
  1. Cancela `SalesReceivable` asociado — **solo si `PaidAmount == 0`**, si no, falla (bloquea cancelar factura con cualquier cobro parcial).
  2. Llama `inv.Cancel(...)`.
  3. Revierte inventario: por cada línea con `ItemId`+`WarehouseId`, `AppendMovementAsync(..., StockMovementType.SaleReturn, +qty, ..., sourceDocId=inv.Id, sourceDocType="SalesInvoice")`.
  4. **No reversa Caja ni Contabilidad. No dispara evento.**
- `SalesInvoiceDetail`: snapshot inmutable post-`Freeze()`. **Sin campo `ReturnedQuantity`** — no hay tracking de cantidad ya devuelta en ningún lugar del dominio actual.
- `SalesReceivable` (CxC): `OriginalAmount`, `PaidAmount`, `Status` (string libre `"pending"/"cancelled"`, sin enum), `BalanceDue = OriginalAmount - PaidAmount`. `RegisterCollection`/`ReverseCollection` mutan `PaidAmount`. `Cancel()` solo si `PaidAmount==0`. **Sin ningún método de crédito/ajuste que reduzca `OriginalAmount`.**
- `StockMovementType.SaleReturn = 8` ya existe pero hoy solo se usa para reversar una **cancelación total** de factura, no para una devolución parcial genuina.
- `ElectronicDocumentType.CreditNote = 2` y el código SRI `"04" Nota de Crédito` ya están seedeados y activos en el catálogo `sri_doc_type` — pero **sin builder/provider activo** (`EmbeddedXmlSchemaProviderTests.cs` solo prueba que el XSD carga, no genera XML de negocio).
- Ningún test cubre `SalesInvoice.Cancel()` ni `CancelSalesInvoiceHandler` (no existe `CancelSalesInvoiceHandlerTests.cs`).
- `frontend/.../AccountsReceivablePage.tsx` declara explícitamente: *"No implementa devoluciones, notas de crédito ni reportes — fuera de este alcance."*

### 1.2 Inventory

- `StockMovement` es **append-only**, sin campo de vínculo tipo `ReversedMovementId`/`OriginalMovementId` — una reversión es simplemente una nueva fila de signo opuesto. La única trazabilidad hacia el documento origen es `SourceDocId + SourceDocType`.
- Único punto de entrada autorizado: `IStockRepository.AppendMovementAsync(...)`, seguido de `SaveChangesWithSequenceRetryAsync` (concurrencia **optimista**: retry ante `DbUpdateConcurrencyException`/violación de índice único `uq_stock_movements_company_product_warehouse_sequence`, sin advisory lock).
- `CurrentStock.ApplyMovement` impide stock negativo (`InvalidOperationException` si el resultado sería negativo) — este es el único guard real contra sobreventa.
- Lotes/series: `Lot`/`SerialNumber` existen como agregados completos, pero **Sales nunca los usa hoy** (ni en `AuthorizeSalesUseCases` ni en `CancelSalesUseCases`). `SerialNumber` ya tiene un state machine `InStock→Sold→Returned→InStock` con métodos `Sell()`/`Return()`/`ReInstock()` — completo pero huérfano, nadie lo invoca.
- El costo de una reversión (`RunningAverageCost`) se recalcula sobre el costo promedio **vigente al momento de la reversión**, no sobre el costo original de la venta — no hay forma de "deshacer" exactamente el costo histórico.

### 1.3 Caja / Finance

- `CashMovementType`: `Opening, SaleIncome, ManualIncome, ManualExpense, Withdrawal`. **Sin tipo de reembolso.** `CashReferenceType`: `None, SalesInvoice`. **Sin tipo de referencia para devolución/nota de crédito.**
- `CashSession`/`CashMovement` **no disparan ningún domain event** — son estado puro persistido.
- `SaleIncome` se registra hoy vía `SalesInvoiceAuthorizedHandler` (`INotificationHandler<SalesInvoiceAuthorizedEvent>`), usando la `CashSessionId` **snapshoteada en la propia factura** (la sesión que originó la venta). Si esa sesión no existe (cerrada, distinta), el handler **solo loguea warning y omite el movimiento** — no falla.
- `Payment` (Finance) es un agregado independiente, `Draft→Applied→Reversed`, con `PaymentApplicationLine` (aplica montos parciales contra `SalesReceivable`/`PurchasePayable`, XOR). `Payment.Amount` **debe ser > 0** — no admite pagos negativos, por lo tanto **no puede reutilizarse directamente para modelar un reembolso**.
- `PurchasePayable.ApplyRetention()`/`RebuildInstallments()` es el precedente más cercano a un "ajuste" de deuda (reconstruye cuotas proporcionalmente tras cambiar el monto base) — no existe equivalente en `SalesReceivable`.
- Migración `20260730223838_AddCashSessionOpenUniqueIndexes` (P1-01, ya cerrado) confirma el patrón reutilizable: pre-check en repositorio + índice único parcial en BD + `IDatabaseExceptionTranslator` para devolver conflicto en vez de 500.

### 1.4 ElectronicDocuments (FROZEN, ADR-023)

- Arquitectura de plugin ya existe y **soporta mecánicamente** un nuevo tipo de documento sin tocar código congelado: `IElectronicDocumentXmlBuilder`/`IElectronicDocumentDataProvider`/`IElectronicDocumentSchemaValidator`, cada uno resuelto por un `Resolver` que arma un `Dictionary<ElectronicDocumentType, T>` desde `IEnumerable<T>` inyectado por DI. Hoy solo existe `InvoiceXmlBuilder` (`DocumentType => Invoice`).
- XSD de Nota de Crédito **ya están embebidos y completos** (`Resources/SRI/CreditNote/NotaCredito_V1.0.0.xsd` y `V1.1.0.xsd`), pero en `manifest.json` la entrada `"CreditNote": { "activeVersion": null }` — el manifest está explícitamente marcado como componente **FROZEN** ("`EmbeddedXmlSchemaProvider` + `manifest.json` + XSD oficiales").
- RIDE: `RideDocumentType.CreditNote` y el mapeo string→enum ya existen, pero `RideTemplateResolver` solo tiene registrado `DefaultInvoiceRideTemplate` — para cualquier otro tipo, `Resolve()` devuelve `null` por diseño (no lanza, pero no hay PDF).
- `IDocumentSequenceRepository.CaptureNextAsync` ya es genérico por `docTypeCode` (string) — `"04"` fluye sin cambios, tal como hoy fluye `"01"`.
- `ElectronicDocumentRetryPolicy`/`ElectronicDocumentRetryJob` son genéricos, sin acoplamiento a `Invoice` — funcionan para `CreditNote` sin cambios.
- CLAUDE.md (ADR-023) es **explícito y textual**: *"Agregar builders/providers/validadores para nuevos tipos de comprobante (CreditNote...) como si fuera mantenimiento — es funcionalidad nueva, requiere su propia fase con roadmap explícito."* Esto confirma independientemente lo que el código muestra: la extensión es mecánicamente posible, pero está gobernada, no es libre.

### 1.5 Accounting

- `PostingFact` es un `record` **cerrado** (comentario explícito "no modificar"): `TenantId, CompanyId, SourceModule, FactType, SourceEventId, EntryDate, Subtotal, TotalVat, TotalIce, TotalDiscount, GrandTotal`. **No transporta costo/COGS** — confirma que el Posting Engine hoy, incluso para la venta original, no contabiliza inventario/costo, solo ingreso+impuesto.
- Pipeline fijo: `Idempotencia (pg_advisory_xact_lock transaccional) → PostingRuleResolver → PostingPeriodResolver/Guard → JournalFactory → JournalValidator → ReserveNextNumberAsync → Post()`. `PostingRule` es **configuración de datos** (`(TenantId, CompanyId, SourceModule, FactType)` → líneas con `AccountId/Nature/AmountKind/SortOrder`), no un switch de código.
- Extensión ya probada: `CollectionReversedPostingTranslator`/`SupplierPaymentReversedPostingTranslator` son el precedente exacto — publican un **nuevo** `PostingFact` con un `FactType` distinto ("CollectionReversed"), nunca revierten literalmente el asiento original. El registro de un nuevo `INotificationHandler<T>` es automático vía MediatR assembly-scan — **cero switch/dispatcher central que modificar**.
- `JournalEntry.Reverse()` existe como método de dominio (crea un nuevo asiento con líneas invertidas, enlazado vía `OriginalJournalEntryId`/`ReverseJournalEntryId`, único índice en BD) pero **hoy nadie lo invoca automáticamente** desde ningún translator — es un caso de uso manual (`ReverseJournalEntryUseCases.cs`).
- Grep de `"Cancelled"` en Accounting: **0 resultados**. Ninguna cancelación de ningún módulo dispara hoy una reacción contable.

### 1.6 Audit

- Patrón piloto (`PricingRuleAudit`/`PricingRuleAuditHandler`, replicado en Items): entidad `XxxAudit : AuditRecordBase` con factory estático `Create(AuditActor actor, ...campos propios..., action, reason?)`, más `XxxAuditHandler : INotificationHandler<TEvent>` que llama `IAuditService.RecordAsync(XxxAudit.Create(...), ct)`. Cero registro manual en DI — MediatR lo descubre solo.
- `IAuditEvent` exige `EntityId, Action, string? Reason`.
- **Sales no tiene ninguna entidad de Entity Audit hoy** — `SalesInvoiceAuthorizedEvent` ya implementa `IAuditEvent` (listo, según hallazgo P1-07 de la auditoría) pero nadie lo escucha para auditoría, solo Accounting lo consume para posting.

---

## 2. Arquitectura propuesta

### 2.1 Decisión de dominio: nuevo `AggregateRoot SalesReturn`

**Sí**, `SalesReturn` debe ser un agregado nuevo, independiente de `SalesInvoice` — no una extensión de `SalesInvoice`/`SalesInvoiceDetail`. Razones:

- `SalesInvoiceDetail` queda `IsFrozen` tras `Authorize()` por diseño explícito (snapshot fiscal inmutable) — mutar sus cantidades violaría esa invariante congelada.
- El patrón ya usado en el repo para "documento nuevo que referencia otro por Id, sin navegación" es exactamente `SalesReceivable.InvoiceId` (Guid FK, sin propiedad de navegación) — se replica igual para `SalesReturn.SalesInvoiceId`.
- Una factura puede tener **múltiples** devoluciones parciales a lo largo del tiempo — un objeto-valor embebido en la factura no lo soportaría limpiamente; un agregado propio con su propio ciclo de vida sí.
- Permite que `SalesReturn` tenga su propio evento (`SalesReturnAuthorizedEvent`) sin tocar `SalesInvoice`/`SalesInvoiceAuthorizedEvent` — extensión aditiva pura, cero riesgo de regresión sobre Sales existente.

### 2.2 Relación con `SalesInvoice`

- `SalesReturn.SalesInvoiceId` (Guid, FK física, sin navegación EF) — mismo patrón que `SalesReceivable`.
- La factura debe estar en `Authorized` (nunca `Draft`/`Cancelled`) — se valida contra `ISalesInvoiceRepository.GetByIdAsync` en el handler de aplicación, no en el dominio de `SalesReturn` (que no conoce `SalesInvoice`).
- Cada línea de `SalesReturn` referencia una línea original vía `OriginalInvoiceDetailId` (Guid, FK a `SalesInvoiceDetail.Id`) — trazabilidad exacta línea-a-línea.

### 2.3 Líneas: `SalesReturnDetail`

Snapshot copiado (no recalculado) desde la línea original congelada: `Description`, `SnapshotSku`, `UomCode`, `UnitPrice`, `VatCode/VatRate`, `IceCode/IceRate` — **se consumen los valores ya congelados de la factura, nunca se re-resuelven contra el ítem actual** (coherente con la Regla de Configuración Tributaria: "los documentos no generan impuestos", y aquí ni siquiera deben recalcularlos — deben heredar exactamente lo que ya se facturó, incluso si el ítem cambió de configuración después). Campo propio: `Quantity` (cantidad devuelta, > 0).

Cálculo de montos de línea: mismas fórmulas que `SalesInvoiceDetail` (`LineSubtotal = Quantity*UnitPrice`, `TaxableBase`, `TaxInclusiveTotal`) aplicadas a la cantidad devuelta — no es un simple prorrateo del total de la factura, es el mismo cálculo unitario con la cantidad reducida.

### 2.4 Invariante central — no devolver más de lo vendido menos lo ya devuelto

**No** se agrega un campo `ReturnedQuantity` a `SalesInvoiceDetail` (violaría su inmutabilidad congelada y el límite de agregado — `SalesReturn` no puede mutar un agregado ajeno). En su lugar:

- Query de agregación: `ISalesReturnRepository.GetReturnedQuantityByInvoiceDetailAsync(tenantId, invoiceDetailId)` — suma `Quantity` de todas las `SalesReturnDetail` cuyo `SalesReturn` padre está en estado `Authorized` (excluye `Draft`/`Cancelled`, evita contar devoluciones abandonadas).
- El handler de aplicación (`AuthorizeSalesReturnHandler`) calcula, por cada línea, `remaining = originalDetail.Quantity - alreadyReturned`, y valida `line.Quantity ≤ remaining` **antes** de invocar `salesReturn.Authorize()`. Esta validación vive en Application, no en Domain, porque requiere una consulta cross-agregado (mismo patrón ya usado por `AuthorizeSalesInvoiceHandler` para el pre-check de stock disponible contra `IStockRepository`).
- El dominio de `SalesReturn` sí valida invariantes locales: `Quantity > 0` por línea, ≥1 línea, `Reason` no vacío.

### 2.5 Concurrencia — impedir doble devolución que exceda el saldo

Dos solicitudes de devolución concurrentes contra la misma factura podrían ambas pasar el pre-check de "cantidad remanente" antes de que ninguna se persista (idéntica condición de carrera a la ya identificada y cerrada en Caja, P1-01). Se propone reutilizar el **mismo patrón ya probado dos veces en este repo** (Caja: índice único parcial; Accounting: `pg_advisory_xact_lock` transaccional):

- **Advisory lock transaccional** por `(TenantId, SalesInvoiceId)` al inicio de `AuthorizeSalesReturnHandler`, adquirido **dentro de la misma transacción** que valida y persiste — serializa todas las devoluciones sobre la misma factura sin bloquear devoluciones de otras facturas. Se libera automáticamente al `COMMIT`/`ROLLBACK`, igual que `PostingIdempotencyGuard.AcquireIdempotencyLockAsync`.
- Este mecanismo es **nuevo código, no una modificación** de `PostingIdempotencyGuard` (que es Accounting-específico y FROZEN-adyacente) — se implementa como un método propio en `SalesReturnRepository`, siguiendo el mismo patrón SQL (`pg_advisory_xact_lock(hash1, hash2)`), sin tocar el código existente de Accounting.

### 2.6 Estados de `SalesReturn`

Se replica deliberadamente el mismo vocabulario que `SalesInvoiceStatus` (consistencia de patrón, ya señalado como deseable — evita agregar una cuarta variante al problema ya detectado en la auditoría §9 de "estados de documento fragmentados"):

```
Draft(1) → Authorized(2)
Draft → Cancelled(3)      // solo antes de autorizar
```

- `Draft`: se crean/editan líneas y el motivo. Mutable.
- `Authorize()`: única transición que dispara efectos (inventario, CxC/Caja, evento contable, emisión de Nota de Crédito). Congela las líneas (mismo patrón `Freeze()`). Snapshotea `AuthorizedSubtotal/TotalVat/TotalIce/TotalDiscount/GrandTotal`.
- `Cancel()`: **solo legal desde `Draft`**. Una vez `Authorized`, la devolución es terminal — no existe "devolver una devolución" en este diseño (fuera de alcance; si se necesitara, sería una nueva factura, no una reversión de `SalesReturn`). Esto es una decisión de diseño explícita, no un olvido: una vez autorizada, la devolución ya generó efectos irreversibles en inventario/CxC/Caja/SRI, y revertir eso limpiamente reabre todos los mismos problemas de concurrencia/trazabilidad que esta feature resuelve — se declara fuera de alcance.

### 2.7 Invariantes en Domain (`SalesReturn` + `SalesReturnDetail`)

- `Reason` obligatorio, no vacío, longitud máxima razonable (ej. 500, igual que `SalesInvoice.CancelReason`).
- ≥1 línea al autorizar.
- Cada línea: `Quantity > 0`.
- `SalesInvoiceId` no vacío, inmutable post-creación.
- Líneas inmutables tras `Authorize()` (mismo patrón `Freeze()`/`IsFrozen`).
- El invariante cross-agregado (no exceder lo vendido) vive en Application, no en Domain (ver 2.4).

### 2.8 Trazabilidad a conservar

- `SalesReturn.SalesInvoiceId` + `SalesReturn.ReturnNumber` (numeración interna propia, no SRI — distinta del número de Nota de Crédito).
- `SalesReturnDetail.OriginalInvoiceDetailId` — línea a línea.
- `SalesReturn.CreditNoteElectronicDocumentId` (nullable, poblado tras `RegisterAsync` contra ElectronicDocuments) — vínculo al documento SRI.
- Movimiento de inventario: `sourceDocId = SalesReturn.Id`, `sourceDocType = "SalesReturn"` (**no** reusar `"SalesInvoice"` como hace hoy la cancelación total — ver §3).
- Evento contable: `SourceEventId` = Id del evento (o `SalesReturn.Id`), `SourceModule = "Sales"`, `FactType = "SalesReturn"`.

### 2.9 Lotes/series

**Fuera de alcance del MVP**, documentado explícitamente como limitación heredada: Sales no popula `LotId`/`SerialId` ni en la venta original ni en la cancelación total actual. `SerialNumber.Return()`/`.ReInstock()` ya existen en Inventory pero están huérfanos — nadie en Sales los invoca hoy, ni siquiera para la venta. Cablear devoluciones con series requeriría primero cablear la venta con series (trabajo previo no hecho, fuera de este diseño). Si una factura tiene líneas con `WarehouseId` pero el ítem es serializado (hoy no lo es, porque nada en Sales lo popula), el comportamiento sería idéntico al de una venta normal: movimiento de stock sin lote/serie.

### 2.10 Idempotencia/duplicados

- A nivel de aplicación: el pre-check de cantidad remanente + advisory lock por factura (2.5) impide que dos devoluciones autorizadas concurrentes excedan el saldo.
- A nivel de reintento de request (doble clic / reintento de red del mismo comando): se recomienda que `CreateSalesReturnDraftCommand` no tenga riesgo (crea un Draft, inocuo, el usuario puede cancelar drafts duplicados), y que `AuthorizeSalesReturnCommand` sea idempotente por `SalesReturn.Id` (autorizar dos veces el mismo Id ya-`Authorized` debe fallar limpio vía el guard de estado del dominio, `EnsureDraft()`-equivalente) — no requiere mecanismo adicional, el propio estado del agregado ya lo protege.

---

## 3. Nuevas entidades / agregados

| Entidad | Tipo | Ubicación propuesta |
|---|---|---|
| `SalesReturn` | Nuevo `AggregateRoot` | `ERP.Domain/Modules/Sales/Entities/SalesReturn.cs` |
| `SalesReturnDetail` | Entidad hija | `ERP.Domain/Modules/Sales/Entities/SalesReturnDetail.cs` |
| `SalesReturnStatus` | Enum (`Draft/Authorized/Cancelled`) | `ERP.Domain/Modules/Sales/Enums/` |
| `ISalesReturnRepository` | Interfaz | `ERP.Domain/Modules/Sales/Interfaces/` |
| `SalesReturnRepository` | Implementación (incluye advisory lock) | `ERP.Infrastructure/Persistence/Repositories/Sales/` |
| `SalesReturnConfiguration` | EF config | `ERP.Infrastructure/Persistence/Configurations/Sales/` |

Sin cambios a `SalesInvoice`, `SalesInvoiceDetail`, `SalesReceivable` como *tipos* — `SalesReceivable` sí gana un **método nuevo** (ver §5), no un cambio de forma.

## 4. Nuevos eventos

| Evento | Disparado por | Consumidores propuestos |
|---|---|---|
| `SalesReturnAuthorizedEvent` (implementa `IAuditEvent`) | `SalesReturn.Authorize()` | `SalesReturnAuthorizedPostingTranslator` (Accounting), `SalesReturnAuditHandler` (Audit) |

Campos calcados de `SalesInvoiceAuthorizedEvent` (mismo esquema que exige `PostingFact`) + `SalesInvoiceId`, `ReturnNumber`: `TenantId, CompanyId, SalesReturnId, SalesInvoiceId, ReturnNumber, Subtotal, TotalVat, TotalIce, TotalDiscount, GrandTotal, UserId, IssueDate`.

No se agrega ningún evento a `SalesInvoice` en este diseño (no es necesario — la devolución vive en su propio agregado con su propio evento; extender `SalesInvoice` con eventos de cancelación es el hallazgo P1-08, deliberadamente fuera de este alcance).

## 5. Cambios necesarios por módulo

### Sales
- Nuevas entidades (§3).
- Nuevo controller `SalesReturnsController`.
- Nuevos use cases: `CreateSalesReturnDraftCommand`, `UpdateSalesReturnDraftCommand`, `CancelSalesReturnDraftCommand`, `AuthorizeSalesReturnCommand`, `GetSalesReturnByIdQuery`, `GetSalesReturnListQuery`, `GetReturnableLinesByInvoiceQuery`.
- `ISalesReceivableRepository`: sin cambio de forma.
- `SalesReceivable`: **nuevo método de dominio** `ApplyReturnCredit(decimal amount, Guid updatedBy)` — reduce `OriginalAmount -= amount`, guardado `amount ≤ BalanceDue` (nunca reduce por debajo de lo ya cobrado), sin tocar `PaidAmount`. Análogo conceptual a `PurchasePayable.ApplyRetention` pero reduciendo el principal en vez de la retención.
- `SalesReceivable`: **nuevo método** `RebuildInstallments(...)` — mismo patrón que `PurchasePayable.RebuildInstallments()` (hoy `SalesReceivable` no tiene forma de reconstruir cuotas tras un cambio de monto; `PurchasePayable` sí, por `ApplyRetention`). Se replica el patrón ya validado, no se inventa uno nuevo.

### Inventory
- **Sin cambios de código.** Se reutiliza `StockMovementType.SaleReturn` (ya existe) y `IStockRepository.AppendMovementAsync` (ya genérico). Único cambio de *uso*: `sourceDocType = "SalesReturn"` con `sourceDocId = SalesReturn.Id` (nueva identidad de documento fuente, no reutilizar `"SalesInvoice"` como hace la cancelación total hoy — evita mezclar en el mismo `GetMovementsByDocumentAsync` los movimientos de una cancelación total con los de devoluciones parciales genuinas).

### Caja
- `CashMovementType`: **agregar** valor `SaleRefund` (enum, aditivo — Caja no está en la lista de infraestructuras FROZEN de CLAUDE.md).
- `CashReferenceType`: **agregar** valor `SalesReturn`.
- Clasificación `IsExpense`/`IsIncome` en `CashMovement`: incluir `SaleRefund` como salida de efectivo (`IsExpense`).
- Nuevo `INotificationHandler<SalesReturnAuthorizedEvent>` en Caja (ej. `SalesReturnCashRefundHandler`) — **decisión de diseño explícita**: usa la sesión de caja **abierta actualmente** del usuario/caja que procesa la devolución, no `SalesReturn`/`SalesInvoice.CashSessionId` (que probablemente ya está cerrada). Si no hay sesión abierta, falla explícito (fail-closed, sin inventar sesión) — comportamiento distinto y más estricto que `SalesInvoiceAuthorizedHandler` (que hoy solo loguea warning y omite el movimiento silenciosamente; para un reembolso de efectivo real, omitir silenciosamente sería inaceptable).

### Finance/CxC
- `SalesReceivable.ApplyReturnCredit` + `RebuildInstallments` (arriba).
- **No se reutiliza** `Payment`/`PaymentApplicationLine` (no admite montos negativos) — la reducción de CxC se hace directo sobre `SalesReceivable`, no vía el flujo de `Payment`.

### ElectronicDocuments
- Nuevo `CreditNoteXmlBuilder : IElectronicDocumentXmlBuilder` (additivo, `DocumentType => CreditNote`).
- Nuevo `IElectronicDocumentDataProvider` para `CreditNote` (resuelve `SalesReturn` + factura original → `ElectronicDocumentData`, incluyendo referencia obligatoria SRI al comprobante modificado: clave de acceso/autorización/fecha de la factura original).
- Registro en DI: nuevas líneas `AddSingleton<IElectronicDocumentXmlBuilder, CreditNoteXmlBuilder>()` etc.
- **Cambio a archivo FROZEN**: `manifest.json` → `"CreditNote": { "activeVersion": "1.1.0" }` (hoy `null`). Este es el único punto de este diseño que toca un archivo declarado FROZEN por ADR-023 — **señalado explícitamente en §9, no ejecutado**.
- Trigger de emisión: llamada explícita `_edocIssuer.RegisterAsync(..., ElectronicDocumentType.CreditNote, "Sales", salesReturn.Id, ct)` dentro de `AuthorizeSalesReturnHandler`, **después** de `CaptureNextAsync(tid, cid, epId, "04", ct)` y `salesReturn.Authorize()` — mismo patrón exacto que `AuthorizeSalesUseCases.cs`, sin tocar `IElectronicDocumentIssuer`/`RunPipelineAsync` (FROZEN, sin necesidad de tocarlos: el pipeline ya es genérico por `ElectronicDocumentType`).

### Ride
- Nuevo `CreditNoteRideTemplate : IRideTemplate` (additivo, `RideTemplateResolver` ya soporta el registro sin cambios).

### Accounting
- Nuevo `SalesReturnAuthorizedPostingTranslator : INotificationHandler<SalesReturnAuthorizedEvent>` (additivo, siguiendo exactamente `CollectionReversedPostingTranslator`).
- Nueva fila de configuración `PostingRule` para `(SourceModule="Sales", FactType="SalesReturn")` — dato, no código; se crea vía `PostingRuleUseCases` existente, sin nueva migración de esquema.
- **Sin cambios** a `PostingFact`, `PostingEngine`, `PostingPipeline`, `JournalFactory`, `PostingIdempotencyGuard`.

### Audit
- Nuevo `SalesReturnAudit : AuditRecordBase` (additivo, mismo patrón que `PricingRuleAudit`).
- Nuevo `SalesReturnAuditHandler : INotificationHandler<SalesReturnAuthorizedEvent>` (additivo, mismo patrón que `PricingRuleAuditHandler`).
- Nueva `SalesReturnAuditConfiguration` (EF, usa `ConfigureAuditBase<T>()` compartido, sin tocarlo).

### Access/IAM
- Nuevos permission keys (`sales.returns.create`, `.view`, `.authorize` o equivalente) — registro aditivo en el catálogo de permisos existente.

---

## 6. Flujo E2E completo (feliz)

```
1. Cajero/vendedor abre Factura autorizada → botón "Devolución" (visible solo si Authorized y hay cantidad remanente en al menos 1 línea)
2. GET returnable-lines/{invoiceId} → UI muestra cada línea con: cantidad original, ya devuelta, remanente
3. Usuario selecciona cantidades a devolver por línea + escribe motivo (obligatorio)
4. POST /sales-returns (Draft) → valida líneas > 0, motivo no vacío
5. Usuario confirma → POST /sales-returns/{id}/authorize
   a. Advisory lock (TenantId, SalesInvoiceId)
   b. Valida cada línea: Quantity ≤ (original.Quantity - Σ ya devuelto en SalesReturn Authorized)
   c. salesReturn.Authorize() → congela líneas, snapshotea totales, raise SalesReturnAuthorizedEvent
   d. Reversión de inventario: AppendMovementAsync(SaleReturn, +qty, sourceDocType="SalesReturn", sourceDocId=salesReturn.Id) por línea
   e. Ajuste financiero:
      - Si venta contado: refund = GrandTotal devuelto → movimiento de Caja SaleRefund contra sesión ABIERTA actual
      - Si venta crédito: creditApplied = min(GrandTotal devuelto, receivable.BalanceDue)
                          receivable.ApplyReturnCredit(creditApplied) + RebuildInstallments
                          cashRefund = GrandTotal devuelto - creditApplied  (si receivable ya estaba 100% pagado)
                          si cashRefund > 0 → mismo camino que venta contado, por el remanente
   f. CaptureNextAsync(..., "04", ct) → número de Nota de Crédito
   g. _edocIssuer.RegisterAsync(..., CreditNote, "Sales", salesReturn.Id) → dispara pipeline SRI (async/en el mismo request, según patrón actual de Sales)
   h. SaveChangesWithSequenceRetryAsync
   i. Tras SaveChanges → SalesReturnAuthorizedEvent despachado → PostingTranslator (asiento) + AuditHandler (traza)
6. UI muestra estado de la Nota de Crédito (Draft→XmlGenerated→Signed→Sent→Received→Authorized/Rejected), igual que el Monitor de facturas
7. Una vez Authorized por SRI → RIDE descargable
```

---

## 7. Reglas / invariantes (resumen)

| # | Regla | Capa |
|---|---|---|
| 1 | `Reason` obligatorio, no vacío | Domain (`SalesReturn`) |
| 2 | ≥1 línea, `Quantity > 0` por línea | Domain |
| 3 | Línea devuelta ≤ remanente (original − ya devuelto autorizado) | Application (cross-agregado) |
| 4 | Factura origen debe estar `Authorized` | Application |
| 5 | No exceder saldo remanente bajo concurrencia | Application + advisory lock por `(TenantId, SalesInvoiceId)` |
| 6 | `SalesReturn.Authorized` es terminal (sin "devolución de devolución") | Domain |
| 7 | Impuestos/precios de línea se heredan del snapshot congelado de la factura, nunca se recalculan | Domain (constructor de `SalesReturnDetail`) |
| 8 | Reembolso de crédito nunca reduce `OriginalAmount` por debajo de `PaidAmount` | Domain (`SalesReceivable.ApplyReturnCredit`) |
| 9 | Reembolso en efectivo requiere sesión de caja abierta vigente; fail-closed si no existe | Application (Caja handler) |

---

## 8. Concurrencia / idempotencia

- **Devolución duplicada/concurrente sobre la misma factura**: advisory lock transaccional `(TenantId, SalesInvoiceId)`, mismo patrón ya probado en `PostingIdempotencyGuard` (Accounting) — código nuevo, no modificación de código existente.
- **Movimiento de inventario concurrente**: sin cambios, reutiliza `SaveChangesWithSequenceRetryAsync` (optimista + reintento, ya FROZEN-adyacente y probado).
- **Posting contable**: idempotencia automática heredada del pipeline existente (`PostingIdempotencyGuard` por `SourceEventId+FactType`), sin código nuevo.
- **Reintento de `AuthorizeSalesReturnCommand`** sobre un `SalesReturn` ya `Authorized`: rechazado por el guard de estado del propio agregado (mismo patrón `EnsureDraft()`).

---

## 9. Integración SRI — punto de decisión formal requerido

Todo lo mecánico (builders, providers, DI, RIDE, DocumentSequence, retry) es **aditivo puro**, sin tocar ni un método de los componentes FROZEN listados en CLAUDE.md/ADR-023.

**Única excepción real**: `manifest.json` (parte explícita de la infraestructura FROZEN "`EmbeddedXmlSchemaProvider` + `manifest.json` + XSD oficiales") requiere cambiar `"CreditNote".activeVersion` de `null` a `"1.1.0"` para que el validador de esquema encuentre una versión activa.

CLAUDE.md es taxativo: los únicos 4 motivos válidos para tocar un archivo FROZEN de ElectronicDocuments son (1) cambio obligatorio del SRI, (2) bug demostrado, (3) vulnerabilidad, (4) rendimiento crítico. **Habilitar un tipo de documento nuevo no encaja literalmente en ninguno de los 4**, pero el propio CLAUDE.md anticipa exactamente este caso y lo resuelve no prohibiéndolo sino exigiéndole *"su propia fase con roadmap explícito"* — es decir, gobernanza (una ADR nueva o un addendum a ADR-023), no una re-arquitectura.

**No implementado en esta fase.** Se señala como el único gate formal pendiente antes de escribir código en ElectronicDocuments.

---

## 10. Integración Accounting

Cero cambios a componentes compartidos (`PostingFact`, `PostingEngine`, `PostingPipeline`, `JournalFactory`, `PostingIdempotencyGuard`). Solo: 1 evento nuevo, 1 translator nuevo (`INotificationHandler`), 1 fila de configuración `PostingRule` nueva. Sigue el precedente ya construido y probado por `CollectionReversedPostingTranslator`.

**Limitación heredada, no nueva**: `PostingFact` no transporta costo (`COGS`), por lo que el asiento de devolución —igual que el de la venta original— solo puede reflejar reversión de ingreso+impuesto, no de costo de inventario/costo de venta. Esto no es una carencia de este diseño: es una limitación ya existente en todo el sistema de Posting que esta feature hereda sin agravar ni resolver.

---

## 11. API propuesta

**Controller**: `SalesReturnsController` (`api/v1/sales-returns`)

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/sales-invoices/{invoiceId}/returnable-lines` | Líneas + cantidad remanente (podría vivir en `SalesReturnsController` o en el existente de Sales) |
| POST | `/sales-returns` | `CreateSalesReturnDraftCommand` |
| PUT | `/sales-returns/{id}` | `UpdateSalesReturnDraftCommand` (solo Draft) |
| DELETE / POST `/sales-returns/{id}/cancel` | — | `CancelSalesReturnDraftCommand` (solo Draft) |
| POST | `/sales-returns/{id}/authorize` | `AuthorizeSalesReturnCommand` |
| GET | `/sales-returns/{id}` | `GetSalesReturnByIdQuery` |
| GET | `/sales-returns` | `GetSalesReturnListQuery` (paginado, filtro por factura/cliente/fecha/estado) |

Validación: FluentValidation (motivo no vacío, líneas>0, cantidades>0) + regla de negocio de remanente resuelta en el handler (no en el validator, porque requiere consulta a BD). Autorización vía permission keys nuevos, siguiendo el mismo patrón IAM ya usado en Sales.

---

## 12. UI propuesta (flujo mínimo)

Factura (detalle, `Authorized`) → botón "Devolución" → pantalla/modal con líneas de la factura (cantidad original/ya devuelta/remanente vía `GetReturnableLinesByInvoiceQuery`) → input de cantidad por línea (ZhDecimalInput/ZhNumberInput según corresponda) + campo de motivo (RHF+Zod) → confirmar → llamar create-draft + authorize → pantalla de resultado con número de devolución y estado de la Nota de Crédito (reutilizar el patrón ya existente del Monitor de Documentos Electrónicos para el polling de estado) → enlace a RIDE una vez `Authorized`.

Reutilización obligatoria (regla de reutilización DS del repo): `ZhForm`/RHF+Zod, `applyServerErrors`, `message`/`MSG`, componentes ya usados en `SalesPage.tsx` para líneas de factura, componentes ya usados en el Monitor de Documentos Electrónicos para seguimiento de estado SRI.

---

## 13. Tests necesarios

- Domain: `SalesReturn.Authorize()`/`Cancel()` invariantes; `SalesReturnDetail.Create()`; `SalesReceivable.ApplyReturnCredit()` (casos: sin pago previo, con pago parcial, ya pagado 100%, excede BalanceDue → rechazo).
- Application: `AuthorizeSalesReturnHandler` — los 15 escenarios de §14 (E2E). Incluye test de concurrencia real (dos autorizaciones simultáneas sobre la misma factura, contra PostgreSQL real, análogo a `DocumentSequenceConcurrencyTests`).
- Infrastructure: `SalesReturnRepository` (advisory lock), `StockMovement` con `sourceDocType="SalesReturn"`.
- Accounting: `SalesReturnAuthorizedPostingTranslatorTests` + integración de idempotencia (mismo patrón que `SalesInvoiceAuthorizedPostingIntegrationTests`).
- ElectronicDocuments: `CreditNoteXmlBuilder` (build + validación XSD real contra los XSD ya embebidos), data provider.
- API: contrato de los 6-7 endpoints, 422 de validación.
- **Regresión obligatoria**: ningún test existente de `SalesInvoice`/`CancelSalesInvoiceHandler` debe romperse — este diseño no toca `SalesInvoice.Cancel()` en absoluto.

---

## 14. Escenarios E2E mínimos — cobertura por el diseño

| # | Escenario | Soportado |
|---|---|---|
| 1 | Devolución parcial contado | Sí — refund en efectivo por el monto devuelto |
| 2 | Devolución total contado | Sí |
| 3 | Devolución parcial crédito | Sí — `ApplyReturnCredit` |
| 4 | Devolución total crédito | Sí |
| 5 | Devolución > cantidad vendida → rechazo | Sí — invariante §2.4 |
| 6 | Segunda devolución que excede saldo → rechazo | Sí — mismo invariante, recalculado sobre remanente actual |
| 7 | Factura inexistente → rechazo | Sí — `ISalesInvoiceRepository.GetByIdAsync` null-check en el handler |
| 8 | Factura no autorizada (Draft/Cancelled) → rechazo | Sí — regla §2.2 |
| 9 | Devolución concurrente → solo una válida hasta el límite | Sí — advisory lock §2.5 |
| 10 | Impacto correcto en inventario | Sí — `SaleReturn` movement, `sourceDocType="SalesReturn"` |
| 11 | Impacto correcto en caja/CxC | Sí, con matiz — ver limitación multi-tender abajo |
| 12 | Nota de Crédito generada | Sí, condicionado a §9 (gate de manifest.json) |
| 13 | Nota de Crédito autorizada por SRI | Sí, vía pipeline genérico existente |
| 14 | Asiento contable generado | Sí, con la limitación heredada de costo/COGS (§10) |
| 15 | Idempotencia/reintento | Sí — §8 |

**Limitaciones explícitas del modelo actual, documentadas, no resueltas por este diseño:**

- **Venta con múltiples formas de pago** (`SalesInvoicePayment` con mezcla efectivo/tarjeta/transferencia): este diseño reembolsa el **monto total devuelto como un único movimiento de Caja** (`SaleRefund`), sin reconstruir el reembolso proporcional por tarjeta/transferencia (liquidar un reembolso de tarjeta es una operación externa a una pasarela de pago, genuinamente fuera del alcance de este ERP). **Decisión de negocio pendiente**: ¿el reembolso completo sale siempre de caja aunque la venta original haya sido con tarjeta? Requiere confirmación explícita del usuario antes de implementar.
- **Cliente con saldo a favor tras devolución de venta 100% pagada**: se resuelve como reembolso en efectivo inmediato (no se modela un "saldo a favor del cliente" persistente/reutilizable en compras futuras) — si el negocio requiere una billetera de crédito de cliente, es una feature distinta y mayor, no incluida aquí.

---

## 15. Riesgos

| Riesgo | Naturaleza |
|---|---|
| Tocar `manifest.json` (FROZEN) sin ADR formal previo | Gobernanza — bloqueante hasta resolver §9 |
| Reembolso de efectivo requiere sesión de caja abierta en el momento de la devolución, que puede no coincidir con quien atendió la venta original — riesgo de fricción operativa si el negocio espera "reembolso automático sin importar quién esté en caja" | Producto/UX |
| Costo/COGS no reversado contablemente (limitación heredada) | Contable — ya existe hoy, no se agrava |
| RIDE de Nota de Crédito no tiene plantilla — si se lanza sin ella, no hay PDF imprimible para el cliente | Producto — decidir si es MVP-bloqueante |
| Ausencia total de tests sobre `CancelSalesInvoiceHandler` hoy — este diseño no lo toca, pero cualquier futura unificación de "cancelación total" vs "devolución parcial" heredaría ese hueco de cobertura | Calidad, informativo |

## 16. Dependencias

- Ninguna dependencia de P1-01/P1-02/P1-06/P0-03 (todos cerrados, no se tocan).
- Depende de que el usuario/arquitecto resuelva el gate de gobernanza de §9 antes de tocar ElectronicDocuments.
- Depende de decisiones de negocio explícitas: reembolso multi-forma-de-pago (§14), alcance de RIDE en MVP, si se requiere aprobación gerencial para autorizar una devolución (no contemplado en este diseño — hoy cualquier usuario con el permission key podría autorizar).

## 17. Orden exacto de implementación por fases

1. **Gate de gobernanza SRI** (§9): decidir y documentar (ADR addendum) el cambio a `manifest.json` antes de tocar ElectronicDocuments.
2. **Domain Sales**: `SalesReturn`, `SalesReturnDetail`, enum de estado, invariantes locales + tests de dominio.
3. **`SalesReceivable.ApplyReturnCredit` + `RebuildInstallments`** + tests de dominio.
4. **Infrastructure**: `SalesReturnRepository` (incl. advisory lock), EF configuration, migración.
5. **Application Sales**: use cases CRUD de Draft + `AuthorizeSalesReturnCommand` (inventario + CxC/Caja, sin SRI todavía) + tests, incluyendo el test de concurrencia real.
6. **Caja**: nuevos enums `SaleRefund`/`SalesReturn`, handler de reembolso + tests.
7. **Accounting**: evento, translator, `PostingRule` de datos + tests.
8. **Audit**: `SalesReturnAudit` + handler + tests.
9. **API**: controller + validators + tests de contrato.
10. **ElectronicDocuments** (solo tras resolver el gate del paso 1): data provider + XML builder + activar manifest + tests contra XSD real.
11. **Ride**: plantilla de Nota de Crédito (paralelo al paso 10, o fast-follow según decisión de negocio).
12. **Frontend**: flujo UI completo.
13. **E2E de integración** (PostgreSQL real) cubriendo los 15 escenarios de §14.

---

## 18. Recomendación final

### NO LISTO PARA IMPLEMENTAR

Falta, específicamente:

1. **Decisión de gobernanza formal** sobre el único punto de este diseño que toca un archivo FROZEN (`manifest.json` de ElectronicDocuments, ADR-023) — requiere una ADR nueva o un addendum explícito antes de escribir código en ese módulo. Sin esto, la Fase 10 de implementación no puede empezar.
2. **Decisión de negocio**: cómo se reembolsa una venta que se pagó con más de una forma de pago (tarjeta/transferencia mezclada con efectivo) — este diseño asume reembolso total en efectivo por defecto, pero es una decisión de producto, no técnica.
3. **Decisión de negocio**: si el MVP requiere RIDE (PDF imprimible) de la Nota de Crédito desde el día 1, o puede lanzarse solo con XML autorizado por SRI y RIDE como fast-follow.
4. **Decisión de negocio**: si autorizar una devolución requiere algún control adicional (aprobación de supervisor, límite de monto sin aprobación, etc.) — el diseño actual no contempla ningún flujo de aprobación, solo el permission key estándar.

El resto del diseño (dominio, inventario, caja/CxC, contabilidad, auditoría, API, frontend, tests, orden de fases) está completo y es implementable sin ambigüedad una vez resueltos los 4 puntos anteriores.
