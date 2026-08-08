# P0-02 — Fase 1.1: cierre estricto de evidencias pendientes de la auditoría

**Tipo de documento:** Auditoría de solo lectura, continuación de `P0-02_PURCHASE_RETURN_AUDIT.md`. No contiene código, no crea entidades, migraciones, endpoints ni UI. No propone el diseño definitivo.
**Fecha:** 2026-07-31
**Referencia:** Cierra el punto marcado como "Nota de verificación pendiente" en `P0-02_PURCHASE_RETURN_AUDIT.md` y corrige/confirma con evidencia de línea exacta los hallazgos de esa auditoría previa.
**Terminología obligatoria:** "crédito a favor de nuestra empresa frente al proveedor" (nunca "saldo a favor de proveedor"). Nombre técnico provisional: `SupplierCredit` (no autoriza aún crear la entidad).

**Decisiones de negocio ya cerradas** (no se vuelven a cuestionar en este ni en documentos futuros de esta fase):
1. Si el valor de la devolución supera el saldo pendiente de la CxP, primero se reduce el saldo pendiente hasta cero y el excedente se convierte en crédito a favor de nuestra empresa frente al proveedor.
2. Si la factura está totalmente pagada, el valor completo reconocido por la devolución se convierte en crédito frente al proveedor.
3. Ese crédito debe poder: (a) aplicarse posteriormente a otra CxP del mismo proveedor; o (b) cerrarse mediante el registro auditado de un reembolso recibido.
4. La devolución física y el documento fiscal recibido son procesos relacionados, pero no idénticos.
5. Se permite procesar la devolución sin haber recibido todavía la Nota de Crédito del proveedor, manteniendo un estado fiscal pendiente y explícito.
6. El proveedor emite la Nota de Crédito; nuestra empresa únicamente la registra como receptor.
7. Registrar posteriormente la Nota de Crédito no debe volver a afectar inventario, CxP, crédito ni contabilidad operativa.
8. Si existe una retención emitida o autorizada, la primera versión no realiza ajustes proporcionales automáticos — la autorización de la devolución se bloquea con un error explícito hasta resolver la retención.
9. Lotes y series quedan fuera de P0-02.
10. Nota de Débito queda fuera de P0-02.
11. Validación automática en línea contra el SRI queda como backlog no bloqueante; v1 solo aplica validaciones estructurales y de duplicidad.
12. `PurchasePayable.BalanceDue` (no `Status`) es la fuente del saldo pendiente.

---

## A. Resultado de cierre

**AUDIT_CLOSED.**

El único punto explícitamente marcado como pendiente en la auditoría previa ("Nota de verificación pendiente" — efecto de `ConfirmPurchaseHandler` sobre inventario) quedó resuelto con evidencia de línea exacta (ver sección B, fila 3). Todas las demás verificaciones obligatorias del encargo se completaron con lectura completa de código real (no inferencia). No queda evidencia pendiente para cerrar esta fase de auditoría.

---

## B. Correcciones a la auditoría anterior

| Hallazgo anterior | Resultado | Evidencia | Redacción corregida |
|---|---|---|---|
| "Ausencia de transacción explícita en Purchases = BLOCKER" | **CORRECTED** | `ErpDbContext.SaveChangesAsync` (`backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs:111-144`): `var ownsTransaction = Database.CurrentTransaction is null; var tx = ownsTransaction ? await Database.BeginTransactionAsync(...) : null;` — con `commit`/`rollback` explícitos en `try/catch/finally`. | **Sí existe una transacción explícita real** (`Database.BeginTransactionAsync`) en cada llamada a `SaveChangesAsync`/`SaveChangesWithSequenceRetryAsync` — la abre automáticamente `ErpDbContext`, no el handler. `CancelPurchaseHandler`/`ConfirmPurchaseHandler` sí son atómicos a nivel de infraestructura para el conjunto payable+withholding+stock+factura en un único `SaveChanges`. Lo que **no existe** es una transacción *abierta por el handler de Application* que envuelva múltiples `SaveChanges` o lecturas-antes-de-decidir bajo lock (ver siguiente fila) — esa es la brecha real, no la atomicidad del `SaveChanges` en sí. |
| "Ausencia de advisory lock en Purchases = BLOCKER general" | **CONFIRMED** | Grep exhaustivo `pg_advisory_xact_lock` en `ERP.Infrastructure/Persistence/Repositories/Purchases/` y `ERP.Application/Modules/Purchases/`: sin resultados. Único uso existente es `SalesReturnRepository.AcquireReturnLockAsync` (Sales). | Confirmado sin matiz: ningún handler de Compras serializa concurrencia sobre la misma factura/CxP mediante advisory lock. Es un prerrequisito real para `PurchaseReturn`. |
| "ConfirmPurchase no afecta inventario" (no confirmado con línea exacta) | **CORRECTED** | `ConfirmPurchaseUseCases.cs:150-187` (STEP 3): `await _stockRepo.AppendMovementAsync(tid, cid, line.ItemId.Value, warehouseId.Value, StockMovementType.PurchaseEntry, line.Quantity, line.UomCode, inv.IssueDate, inv.InvoiceNumber, inv.Id, "PurchaseInvoice", uid, line.LandedUnitCost, ...)` — ejecutado por cada línea con `ItemId` no nulo, **después** de `inv.Confirm(uid)` (que exige `line.IsFrozen`, verificado explícitamente con `if (!line.IsFrozen) return ValidationFailure`). | `ConfirmPurchaseHandler.Handle()` sí crea el movimiento de entrada de inventario (`StockMovementType.PurchaseEntry`) en el mismo `SaveChangesWithSequenceRetryAsync` que persiste la factura confirmada y la CxP. No existe una factura `Confirmed` sin que su entrada de stock haya sido creada en el mismo `SaveChanges` (falla-junto, no en dos pasos separados). |
| "Payment/PaymentApplicationLine puede reutilizarse para crédito" | **CORRECTED** | `PaymentApplicationLineConfiguration.cs:21-28`: `CHECK chk_payment_application_line_document_xor: (receivable_id IS NOT NULL AND payable_id IS NULL) OR (receivable_id IS NULL AND payable_id IS NOT NULL)`. `PaymentApplicationLine.CreateForPayable`/`CreateForReceivable` son `internal`, invocados únicamente por `Payment.AddApplicationLine` según `PaymentDirection` (`Collection`/`Payment`, sin tercer valor). | El **patrón** (aggregate `Payment` + líneas de aplicación + invariante de balance `Σ AppliedAmount == Amount`) es reutilizable conceptualmente, pero el mecanismo **no es reutilizable tal cual**: el CHECK de BD y el enum `PaymentDirection` son binarios (Collection/Payment) — aplicar un crédito de proveedor contra una CxP exigiría o (a) una tercera columna/dirección con migración de esquema, o (b) un mecanismo de aplicación separado. Es una decisión de diseño, no una reutilización directa. |
| "StockMovementType.PurchaseReturn tiene semántica suficiente" | **CORRECTED** | `CancelPurchaseUseCases.cs:132-147`: usa `StockMovementType.PurchaseReturn` con `SourceDocId = inv.Id`, `SourceDocType = "PurchaseInvoice"` (no un documento de devolución propio). Comparar con `AuthorizeSalesReturnUseCases.cs:209-223`: usa `StockMovementType.SaleReturn` con `SourceDocId = salesReturn.Id`, `SourceDocType = "SalesReturn"` (documento propio). | El **valor del enum** (`PurchaseReturn = 7`) es reutilizable y semánticamente correcto para una devolución real. Lo que **no debe copiarse** es el patrón de uso actual de `CancelPurchaseUseCases` (que apunta `SourceDocId`/`SourceDocType` a la propia factura, porque es una cancelación total, no una devolución). Un futuro `PurchaseReturn` debe replicar el patrón de `SalesReturn` (`SourceDocType` = su propio documento), nunca el de `CancelPurchaseUseCases`. |
| "No existe representación reutilizable para documentos fiscales recibidos" | **CORRECTED** | `PurchaseReceptionDocument.cs` (`backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/Entities/`): `PurchaseReceptionSourceDocType` **ya incluye** `CreditNote = 2` (`backend/src/.../Enums/PurchaseReceptionSourceDocType.cs`). El agregado captura RUC/nombre proveedor, `AccessKey` (único por tenant, `uq_purchase_reception_documents_tenant_access_key`), `InvoiceNumber`, `IssueDate`, autorización SRI, `Subtotal/VatAmount/TotalAmount`, líneas — y `PurchaseId` es **nullable**, solo referencia de solo lectura ("nunca crea ni modifica el agregado `PurchaseInvoice` directamente"). Ninguno de sus métodos (`Create`, `AttachSriAuthorization`, `MarkVerified`, `MarkProcessed`, `Cancel`) toca inventario o CxP — grep de `AppendMovementAsync`/`PurchasePayable`/`TrackPayable` en todo `ERP.Application/Modules/Purchases/PurchaseReception` sin resultados. | Existe una entidad genérica reutilizable en su forma de datos (`PurchaseReceptionDocument`) que ya reconoce el tipo `CreditNote` y no dispara efectos de negocio por sí sola. Su **limitación real**: hoy el pipeline completo (parser TXT, `PurchaseXmlDraftParser`, `PurchaseReceptionDetailProcessor`) solo procesa `Invoice` (Fase 1, según el propio comentario del enum); reutilizarla para una NC recibida manualmente (sin factura origen en el ERP) requeriría un flujo de creación distinto al de importación TXT, no una entidad nueva desde cero. |

---

## C. Entrada y salida de inventario comprobadas

| Operación | Handler/servicio | Movimiento | Cantidad | Costo | Fuente documental | Persistencia |
|---|---|---|---|---|---|---|
| Entrada por confirmación de compra | `ConfirmPurchaseHandler.Handle()` STEP 3, `ConfirmPurchaseUseCases.cs:150-187` | `StockMovementType.PurchaseEntry` | `line.Quantity` (positiva) | `line.LandedUnitCost` (costo congelado por `line.FreezeCosts()` dentro de `inv.Confirm()`, exigido explícitamente: `if (!line.IsFrozen) return ValidationFailure`) | `SourceDocId = inv.Id`, `SourceDocType = "PurchaseInvoice"`, `Reference = inv.InvoiceNumber` | Trackeada en el mismo `DbContext` por `AppendMovementAsync` (no persiste sola); persistida junto con factura+CxP+comunicación en `await _stockRepo.SaveChangesWithSequenceRetryAsync(ct)` al final del handler (STEP 7) |
| Reversión total por anulación de compra | `CancelPurchaseHandler.Handle()`, `CancelPurchaseUseCases.cs:123-148` | `StockMovementType.PurchaseReturn` | `-line.Quantity` (reversión 100%, no hay devolución parcial) | `line.LandedUnitCost` (mismo costo congelado de la línea original) | `SourceDocId = inv.Id`, `SourceDocType = "PurchaseInvoice"` (**no** un documento de devolución propio — ver corrección en sección B), `Reference = "ANULACIÓN: {InvoiceNumber}"` | Un único `await _stockRepo.SaveChangesWithSequenceRetryAsync(ct)` junto con `payable.CancelPayable()`, `wh.Cancel()`, `payable.ReverseRetention()`, `inv.Cancel()` |

**Respuestas puntuales:**
1. La operación que agrega realmente cantidad y kardex es `IStockRepository.AppendMovementAsync` (implementado en `StockRepository.CreateAndTrackMovementAsync`), invocada por `ConfirmPurchaseHandler` (entrada) y `CancelPurchaseHandler` (reversión total).
2. Clase/método: `StockRepository.CreateAndTrackMovementAsync` (`backend/src/ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs:152-248`). Tipos usados: `PurchaseEntry` (confirmación) y `PurchaseReturn` (anulación total).
3. Cantidad = `PurchaseInvoiceDetail.Quantity` (congelada); costo = `line.LandedUnitCost` (congelado por `FreezeCosts()`); bodega = `line.WarehouseId ?? inv.GlobalWarehouseId`; fecha = `inv.IssueDate` (entrada) / `DateTime.UtcNow` (reversión); documento origen = `inv.Id`/`"PurchaseInvoice"` en ambos casos actuales.
4. **No.** `inv.Confirm(uid)` y el movimiento de stock se ejecutan en el mismo `Handle()` y se persisten en el mismo `SaveChangesWithSequenceRetryAsync` — no hay ninguna ruta de código donde una factura llegue a `Confirmed` sin que su `StockMovement.PurchaseEntry` quede trackeado en el mismo `SaveChanges`. Si ese `SaveChanges` falla, ambos fallan juntos (rollback de la transacción interna de `ErpDbContext`).
5. **Sí, de forma confiable** para el caso base: cada línea de entrada tiene `SourceDocId = PurchaseInvoiceId` y `SourceDocType = "PurchaseInvoice"`, y el índice único `uq_stock_movements_company_product_warehouse_sequence` garantiza orden. Sin embargo, **no hay hoy una referencia a nivel de línea de factura** (`PurchaseInvoiceDetail.Id`) en el `StockMovement` de entrada — solo al documento completo. Una devolución parcial que necesite saber "cuánto de esta línea específica ya se devolvió" no tiene ese dato en el propio `StockMovement`; tendría que derivarlo agregando por producto/bodega/documento, con el riesgo de mezclar líneas distintas del mismo `PurchaseInvoiceId` si hay más de un producto igual en dos líneas. Brecha real a resolver en el diseño (no propuesta aquí).
6. La única fuente de costo confiable es `PurchaseInvoiceDetail.LandedUnitCost` (congelado tras `Confirm()`), que es exactamente lo que ya usa `CancelPurchaseHandler` para su reversión total — es el patrón correcto a seguir, **no** el costo corrido del kardex (`RunningAverageCost`), que puede haber cambiado por movimientos posteriores no relacionados con esta compra.

---

## D. Atomicidad y concurrencia

| Operación | SaveChanges | Transacción | Concurrencia | Idempotencia | Riesgo demostrado |
|---|---|---|---|---|---|
| `ConfirmPurchaseHandler` | 1 (`SaveChangesWithSequenceRetryAsync`) | Explícita, abierta automáticamente por `ErpDbContext.SaveChangesAsync` (línea 111-112) | `PurchaseInvoice` tiene `xmin` (`PurchaseInvoiceConfiguration.cs:133-139`); `StockMovement` protegido por índice único de secuencia; `CurrentStock` tiene `RowVersion`/`xmin` | Ninguna — reintento de todo el `SaveChanges`, sin token de idempotencia de request | Sin advisory lock: dos confirmaciones simultáneas de la misma factura no están serializadas más allá del `xmin` de `PurchaseInvoice` (una de las dos recibiría `DbUpdateConcurrencyException` genérica, no un mensaje de negocio) |
| `CancelPurchaseHandler` | 1 (`SaveChangesWithSequenceRetryAsync`) | Igual que arriba | `PurchasePayable` **sin `xmin`** (confirmado: `PurchasePayableConfiguration.cs` no declara `Property<uint>("xmin")`, a diferencia de `PurchaseInvoiceConfiguration`/`IssuedWithholdingConfiguration`/`CurrentStockConfiguration`); `IssuedWithholding` sí tiene `xmin` (`IssuedWithholdingConfiguration.cs:110-115`) | Ninguna | **Riesgo demostrado (no solo teórico)**: `PurchasePayable` no tiene concurrency token — un `UPDATE` sobre `paid_amount`/`total_retained`/`status` es un blind write sin cláusula `WHERE xmin=...`. Dos operaciones concurrentes que carguen el mismo `PurchasePayable`, cada una calcule un nuevo valor sobre el estado que leyó, y ambas hagan `SaveChanges` exitosamente sin excepción → lost update silencioso (el segundo `SaveChanges` sobrescribe el efecto del primero sin fallar). Esto reproduce exactamente el escenario "devolución concurrente con pago de la misma CxP". |
| `RegisterPaymentCommandHandler`/`ReversePaymentCommandHandler` | 1 (`_payments.SaveChangesAsync`) | Igual que arriba (transacción interna de `ErpDbContext`) | `payable.RegisterPayment()`/`ReversePayment()` mutan `PaidAmount` sin `xmin` en `PurchasePayable`; `Payment`/`PaymentApplicationLine` tampoco tienen `xmin` | Ninguna | Mismo riesgo de lost update descrito arriba, ya presente **hoy** entre dos pagos concurrentes contra la misma CxP — no es un riesgo nuevo introducido por una futura devolución, es preexistente y la devolución lo hereda/agrava al sumar un tercer escritor concurrente sobre el mismo agregado. |
| `AuthorizeSalesReturnHandler` (referencia) | 1 (`SaveChangesWithSequenceRetryAsync`), dentro de una transacción abierta manualmente por el handler | `IUnitOfWork.BeginTransactionAsync` explícito, ANTES del advisory lock ("sin ella, `AcquireReturnLockAsync` corre en su propio statement autocommit... el lock deja de serializar nada") | `pg_advisory_xact_lock(hash1, hash2)` por `(TenantId, SalesInvoiceId)`, liberado automáticamente al `COMMIT`/`ROLLBACK` de la transacción del handler | Ninguna explícita — el lock + revalidación bajo lock cierran la ventana de carrera | Patrón correcto: el lock serializa toda autorización concurrente sobre la misma factura ANTES de revalidar remanente. Nota técnica no bloqueante: `SaveChangesWithSequenceRetryAsync` se invoca **dentro** de esta transacción ambiente manual — si Npgsql aborta la transacción completa ante el primer error de conflicto de secuencia (comportamiento estándar de PostgreSQL), el reintento in-process podría no tener margen real para reintentar con éxito dentro de la misma transacción ya abortada. No se verificó con prueba de integración real en este alcance — se señala como riesgo a confirmar si `PurchaseReturn` reutiliza este mismo patrón compuesto. |

**Respuestas puntuales:**
1. `CancelPurchaseHandler` hace **una sola** persistencia: `await _stockRepo.SaveChangesWithSequenceRetryAsync(ct)` al final (línea 169).
2. `AppendMovementAsync` (y por tanto `CreateAndTrackMovementAsync`) **no guarda inmediatamente** — solo agrega entidades (`StockMovement` vía `AddAsync`, `CurrentStock` vía `ApplyMovement`) al `ChangeTracker` del mismo `DbContext`, pendientes del `SaveChanges` final. Confirmado leyendo `StockRepository.cs:88-128,152-248`: no hay ninguna llamada a `SaveChangesAsync` dentro de `AppendMovementAsync`.
3. **Sí, atómicamente** — factura+CxP+retención+stock se confirman en el mismo `SaveChanges`, protegido por la transacción interna de `ErpDbContext`. No es una afirmación deducida, es literal en el código: `ErpDbContext.SaveChangesAsync` abre `Database.BeginTransactionAsync` si no hay una ambiente, ejecuta `base.SaveChangesAsync` dos veces (estado inicial + side-effects de domain events publicados in-process) y comitea/revierte como una sola unidad.
4. Un fallo real que deje datos parciales requeriría que la transacción de `ErpDbContext` se comitee parcialmente — no es posible por diseño de PostgreSQL/Npgsql salvo corrupción de infraestructura (pérdida de conexión post-commit sin confirmación al cliente, escenario de "reintento tras timeout", no de "transacción parcial").
5. Una transacción explícita **abierta por el handler de Application** (además de la interna de `ErpDbContext`) es: **necesaria únicamente para mantener el advisory lock** durante la ventana de validación (igual que hace `AuthorizeSalesReturnHandler`) — no es necesaria para la atomicidad de la escritura en sí, que ya está garantizada por `ErpDbContext.SaveChangesAsync`.
6. `SaveChangesWithSequenceRetryAsync` reintenta la **reconstrucción completa** de los movimientos pendientes (`RecoverFromConflictAndRetrackAsync`: detacha `StockMovement` en estado `Added`, recarga `CurrentStock` modificado, y vuelve a ejecutar `CreateAndTrackMovementAsync` para cada `PendingMovement` en la lista `_pending`) — no es un simple "reintentar el `SaveChanges`" sobre el mismo estado ya calculado.
7. **No** — un reintento no puede crear movimientos duplicados persistidos: el fallo de `SaveChangesAsync` aborta el `INSERT` sin comprometerlo (transacción interna revertida), y antes de reintentar se detachan explícitamente las entidades `Added` previas antes de recrearlas. El único riesgo sería si `SaveChangesAsync` lanzara la excepción **después** de comprometer en BD pero antes de que el cliente reciba confirmación (fallo de red post-commit) — escenario "reintento tras timeout sin saber si la primera petición se confirmó", no protegido por ningún token de idempotencia hoy, ni en Compras ni en `SalesReturn`.

**Sobre los 6 escenarios de concurrencia concretos:**
1. **Dos devoluciones simultáneas sobre la misma línea**: sin protección hoy — no existe query de remanente ni lock; sería necesario ambos: guard de remanente + advisory lock por `PurchaseInvoiceId` (patrón `SalesReturn`).
2. **Devolución concurrente con pago**: riesgo real confirmado por ausencia de `xmin` en `PurchasePayable` — ambas mutaciones sobre `PaidAmount`/`BalanceDue` pueden perderse silenciosamente.
3. **Devolución concurrente con cancelación de factura**: `PurchaseInvoice` sí tiene `xmin` — una detectaría conflicto; pero `PurchasePayable`/`IssuedWithholding` referenciados por ambas operaciones no están protegidos con el mismo nivel salvo `IssuedWithholding` (que sí tiene `xmin`).
4. **Devolución concurrente con cancelación/emisión de retención**: `IssuedWithholding` tiene `xmin` → detectaría conflicto entre dos mutaciones simultáneas sobre la misma retención; pero el efecto cruzado sobre `PurchasePayable.TotalRetained` (vía `ReverseRetention`) no está protegido (mismo problema de ausencia de `xmin`).
5. **Dos aplicaciones simultáneas del mismo crédito futuro**: no evaluable — la entidad de crédito no existe; el diseño deberá decidir explícitamente su mecanismo de concurrencia (no hay entidad hoy con la que compararlo salvo el patrón `PurchasePayable` sin `xmin`, que es precisamente el antipatrón a evitar).
6. **Reintento tras timeout**: sin mecanismo de idempotencia en todo el módulo de Compras ni en el patrón de referencia `SalesReturn` — confirmado ausencia total de columna/índice de idempotencia en las configuraciones EF revisadas.

**Necesidad de mecanismos (evaluación técnica, sin nombres finales):** advisory lock por `PurchaseInvoiceId` — necesario (replicar patrón `SalesReturn`); advisory lock adicional por `PurchasePayableId` — evaluar si el lock por factura ya cubre la CxP asociada (relación 1:1 vía `uq_purchase_payables_purchase`, por lo que un único lock por `PurchaseInvoiceId` probablemente sea suficiente, pero debe verificarse contra el escenario de pago concurrente que no pasa por el mismo lock hoy); token de idempotencia — necesario si se expone reintento de cliente HTTP; `PurchasePayable` sin `xmin` — brecha preexistente que el diseño debería resolver agregando el token (mismo patrón que `PurchaseInvoice`), no solo mitigar con advisory lock, porque el advisory lock nuevo solo protegería la ruta de devolución, no la ruta de pago existente que seguiría sin protección.

---

## E. Pago y CxP

Flujo exacto confirmado (`PaymentUseCases.cs`, completo): `Payment` es un agregado raíz independiente (`Payment.cs`), dirección `Collection`/`Payment` (`PaymentDirection`), con `PaymentApplicationLine` como entidad hija (sin repositorio propio). El caso de uso (`RegisterPaymentCommandHandler`) carga cada `PurchasePayable` referenciado una sola vez (deduplicado por diccionario), construye `Payment.Create()` → `AddApplicationLine()` por cada línea → `payment.Apply(uid)` (valida balance `Σ AppliedAmount == Amount`, exige `Draft`) → por cada línea, `payablesByDocId[docId].RegisterPayment(line.AppliedAmount, uid)` (guard `amount > BalanceDue` → excepción) → `await _payments.AddAsync(payment, ct)` → **una sola** `await _payments.SaveChangesAsync(ct)`. `Payment` y `PurchasePayable` se actualizan en la misma transacción interna de `ErpDbContext` (mismo mecanismo que sección D).

Invariantes comprobadas: (a) `PaidAmount <= BalanceDue original` está garantizado dentro de una sola invocación del handler por el guard de dominio; (b) no está garantizado entre dos invocaciones concurrentes porque `PurchasePayable` no tiene `xmin` (ver sección D); (c) `ReversePaymentCommandHandler` exige `payment.Direction == PaymentDirection.Payment` y usa el mismo patrón `payable.ReversePayment(line.AppliedAmount, uid)` con guard `amount > PaidAmount`; (d) aplicación duplicada de una misma línea de `Payment` no está protegida por ningún índice único de BD ni por dominio — depende exclusivamente de que el flujo de Application nunca vuelva a llamar `RegisterPayment` sobre el mismo `Payment` ya `Applied` (protegido indirectamente porque `AddApplicationLine`/`Apply` exigen `Status == Draft`, y un `Payment` ya `Applied` no vuelve a pasar por ese camino salvo un nuevo comando explícito).

Sobre reutilización técnica de `PaymentApplicationLine` para créditos (evaluación, no decisión de diseño): bloqueada tal cual por el `CHECK chk_payment_application_line_document_xor` (exactamente uno de `receivable_id`/`payable_id`) y por `PaymentDirection` binario — requeriría migración de esquema (tercera columna/dirección) para representar "aplicación de crédito de proveedor", no una simple reutilización de la entidad existente.

---

## F. Retenciones

`WithholdingStatus` tiene exactamente 3 valores: `Draft = 1, Issued = 2, Cancelled = 3` (`backend/src/ERP.Domain/Modules/Purchases/Enums/WithholdingStatus.cs`) — **no existe un estado "Autorizada" distinto de "Issued"** a nivel de este enum de negocio (existe por separado `SriDocumentStatus` para el ciclo de vida del documento electrónico SRI — `None/Signed/Authorized/Rejected/Voided` — que es un eje independiente de auditoría fiscal, no de negocio).

Relación con `PurchaseInvoice`/`PurchasePayable`: `IssuedWithholding.PurchaseInvoiceId` con índice **único** (`IssuedWithholdingConfiguration.cs:142-143`, `HasIndex(x => x.PurchaseInvoiceId).IsUnique()`) → una factura tiene **como máximo una** retención. `IPurchaseInvoiceRepository.GetWithholdingByPurchaseIdAsync(tenantId, purchaseId, ct)` (`PurchaseInvoiceRepository.cs:142-152`) es el mecanismo exacto que el diseño de `PurchaseReturn` deberá consultar para aplicar el bloqueo ya decidido: cargar por `PurchaseInvoiceId` y verificar `Status == WithholdingStatus.Issued` → bloquear con error explícito, exactamente como ya hace `CancelPurchaseHandler.Handle()` (línea 95-105) para su propio caso de anulación total.

No existe ajuste proporcional (`Cancel()` es todo-o-nada, línea 123-147 — solo transiciona `Issued → Cancelled`). Riesgo de carrera entre verificar retención y autorizar devolución: real, porque `IssuedWithholding` tiene `xmin` (detectaría conflicto si dos operaciones mutan la misma retención concurrentemente) pero el propio acto de "leer retención → decidir bloquear/permitir → autorizar devolución" no está bajo ningún lock hoy — la misma ventana TOCTOU que resuelve el advisory lock de `SalesReturn` para su propio caso.

---

## G. Documentos fiscales recibidos

`PurchaseReceptionDocument` puede reutilizarse **estructuralmente** para representar una Nota de Crédito recibida del proveedor: ya reconoce `SourceDocType.CreditNote` en el enum, captura todos los campos fiscales snapshot necesarios (RUC/nombre proveedor, `AccessKey` único, número, fecha emisión, autorización SRI, montos, líneas), y `PurchaseId` es una referencia opcional de solo lectura que **nunca** dispara mutación de `PurchaseInvoice`. Ninguno de sus métodos de dominio invoca inventario o CxP (confirmado por grep negativo en toda la carpeta `PurchaseReception`).

Limitación real: el **pipeline operativo** que hoy rodea a esta entidad (parser TXT del SRI, `PurchaseReceptionDetailProcessor`, `CreatePurchaseReceptionDraftHandler`) está diseñado y probado únicamente para `Invoice` — reutilizar la entidad para una NC exigiría un flujo de creación/registro distinto (posiblemente manual, sin TXT del SRI), no una reescritura del pipeline existente. Restricción única real disponible: `uq_purchase_reception_documents_tenant_access_key` (por tenant, no por tipo de documento) — suficiente para detectar duplicidad de registro de la misma NC dos veces, coincide con el requisito "solo validaciones estructurales y de duplicidad".

Datos que deben conservarse como snapshot: `AccessKey`, `InvoiceNumber` (número de la NC), `IssueDate`, `AuthorizationNumber`/`AuthorizationDate`, montos (`Subtotal`/`VatAmount`/`TotalAmount`), RUC/nombre proveedor. Datos que no deberían duplicarse: cualquier dato ya congelado en la `PurchaseInvoice` original referenciada (proveedor, moneda) — deben leerse por relación, no copiarse dos veces salvo como snapshot fiscal explícito de la propia NC (que puede legítimamente diferir si el proveedor cambió su razón social, por ejemplo).

Registrar la NC en la infraestructura actual (`PurchaseReceptionDocument.Create()` + `AttachSriAuthorization()`) **no activa** procesamiento de compra/inventario/CxP — está separado por diseño explícito ("nunca crea ni modifica el agregado `PurchaseInvoice`"); ese procesamiento solo ocurre si, además, alguien invoca el flujo separado de creación de una `PurchaseInvoice` real a partir del draft (acción humana explícita, no automática).

---

## H. Fuentes de verdad confirmadas

| Concepto | Fuente confirmada | Almacenado/derivado | Confiabilidad | Restricción para P0-02 |
|---|---|---|---|---|
| Saldo pendiente CxP | `PurchasePayable.BalanceDue` | Derivado (`TotalAmount - PaidAmount - TotalRetained`) | Alta en cálculo, **sin protección de concurrencia** (sin `xmin`) | Usar siempre `BalanceDue`, nunca `Status` (confirmado: `Status` solo transiciona `pending→cancelled`); el diseño debe resolver la ausencia de `xmin` antes de sumar un tercer escritor concurrente |
| Costo de línea para reversión | `PurchaseInvoiceDetail.LandedUnitCost` (congelado por `FreezeCosts()`) | Almacenado, inmutable tras `Confirm()` | Alta | Es la fuente correcta — mismo que ya usa `CancelPurchaseHandler` |
| Cantidad comprada por línea | `PurchaseInvoiceDetail.Quantity` | Almacenado, inmutable tras `Confirm()` | Alta | — |
| Cantidad ya devuelta por línea | — | No existe | N/A | Debe derivarse de `StockMovement` filtrando por `SourceDocType`/línea, pero hoy `StockMovement` no referencia `PurchaseInvoiceDetail.Id`, solo `PurchaseInvoiceId` — brecha real a resolver en diseño |
| Retención asociada a la factura | `IssuedWithholding.PurchaseInvoiceId` (único) vía `GetWithholdingByPurchaseIdAsync` | Almacenado | Alta (relación 1:1 garantizada por índice único) | Consultar siempre antes de autorizar, bajo el futuro lock |
| Existencia física | `CurrentStock.Quantity` | Derivado del kardex (`StockMovement`) | Alta | Kardex (`StockMovement`) es la fuente real, no `CurrentStock` |

---

## I. Insumos autorizados para el diseño

**Entidades existentes reutilizables (sin modificar):**
- `PurchaseInvoice`/`PurchaseInvoiceDetail` (solo lectura por Guid).
- `PurchasePayable` (agregado existente, requiere método nuevo de crédito — no modificar los existentes `RegisterPayment`/`ReversePayment`/`ApplyRetention`/`ReverseRetention`).
- `IssuedWithholding` (agregado existente, solo consulta para el bloqueo ya decidido).
- `StockMovementType.PurchaseReturn` (valor de enum ya existente, valor 7).
- `PurchaseReceptionDocument`/`PurchaseReceptionSourceDocType.CreditNote` (estructura de datos ya preparada para representar una NC recibida).

**Motores/servicios existentes reutilizables:**
- `IStockRepository.AppendMovementAsync`/`CreateAndTrackMovementAsync` (kardex, costo, reintento de secuencia).
- Patrón de advisory lock `pg_advisory_xact_lock` de `SalesReturnRepository.AcquireReturnLockAsync` (namespace propio análogo).
- `IUnitOfWork.BeginTransactionAsync`/`CommitAsync`/`RollbackAsync` (patrón `AuthorizeSalesReturnHandler`), consciente del comportamiento real de `ErpDbContext.SaveChangesAsync` (ya abre su propia transacción si no hay una ambiente).
- `IAuditService`/`AuditRecordBase` (ADR-022).
- `IDatabaseExceptionTranslator` para traducir violaciones de índices únicos a errores de negocio, sin exponer excepciones técnicas.

**Invariantes que deben conservarse:**
- `PurchasePayable.BalanceDue` como única fuente de saldo pendiente (nunca `Status`).
- Costo de reversión = `PurchaseInvoiceDetail.LandedUnitCost` (congelado), nunca costo corrido del kardex.
- `SourceDocType`/`SourceDocId` de un `StockMovement` de devolución deben apuntar al documento de devolución propio, nunca a la factura (patrón `SalesReturn`, corrigiendo el patrón de `CancelPurchaseUseCases`).
- Retención `Issued` bloquea la devolución con error explícito — consultar vía `GetWithholdingByPurchaseIdAsync` antes de autorizar.
- Ningún registro posterior de NC debe volver a tocar inventario/CxP/crédito/contabilidad — coherente con el hecho verificado de que `PurchaseReceptionDocument` no dispara esos efectos por sí sola.

**Conceptos realmente ausentes (verificados, no solo "no revisados"):**
- Crédito a favor de nuestra empresa frente al proveedor (`SupplierCredit`) — entidad y lógica de aplicación cruzada.
- Reembolso de proveedor recibido (registro auditado de cierre de crédito).
- Cantidad remanente/devuelta por línea de compra (requiere referencia línea-nivel en `StockMovement` o un cálculo derivado nuevo).
- Advisory lock dedicado a Compras (`PurchaseInvoiceId`, posiblemente `PurchasePayableId`).
- Token de concurrencia (`xmin`) en `PurchasePayable` — ausente hoy, riesgo preexistente independiente de P0-02.
- Mecanismo de idempotencia de request (ausente en todo el ERP, no solo en Compras).

**Riesgos que el diseño debe resolver explícitamente:**
- Lost update sobre `PurchasePayable` por ausencia de `xmin`, agravado por sumar devolución como tercer escritor concurrente junto a pago y cancelación.
- Ventana TOCTOU entre "verificar retención Issued" y "autorizar devolución" sin lock.
- Interacción no verificada entre `SaveChangesWithSequenceRetryAsync` (reintento in-process) y una transacción explícita ambiente abierta por el handler, ante un statement fallido en PostgreSQL (riesgo señalado en sección D, no confirmado con prueba de integración en este alcance).
- Ausencia de referencia línea-a-línea en `StockMovement` para calcular remanente de forma inequívoca cuando una factura tiene múltiples líneas del mismo producto/bodega.

Fin del informe. No se propuso diseño de entidades, migraciones ni código.
