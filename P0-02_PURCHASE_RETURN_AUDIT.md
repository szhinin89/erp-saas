# Auditoría técnica previa — PurchaseReturn + SupplierCredit (P0-02)

**Tipo de documento:** Auditoría de solo lectura. No contiene código, no crea entidades, migraciones, endpoints ni UI. No propone el diseño definitivo.
**Fecha:** 2026-07-31
**Referencia:** Complementa/verifica `P0-02_PURCHASE_RETURN_DESIGN.md` (informe de diseño previo) contra el código real. SalesReturn (P0-01, CLOSED) usado como referencia de patrón, no como verdad asumida.
**Método:** inspección directa de código (ERP.Domain/Application/Infrastructure/API, frontend), sin asumir capacidades por nombre de clase. Toda afirmación cita archivo y símbolo.

---

## A. Resumen ejecutivo

1. `PurchaseInvoice` (`backend/src/ERP.Domain/Modules/Purchases/Entities/PurchaseInvoice.cs`) tiene exactamente 3 estados (`Draft/Confirmed/Cancelled`), confirmado; `Confirm()` congela costos vía `FreezeCosts()` y `ConfirmedGrandTotal` etc.; `Cancel()` es todo-o-nada, solo desde `Confirmed`. Verificado línea por línea — el informe P0-02 es preciso en este punto.
2. **Cero código de devolución de compra existe.** Búsqueda exhaustiva (`Grep "PurchaseReturn|SupplierReturn|DevolucionCompra"` en todo `backend/src`) devuelve solo 2 archivos: `CancelPurchaseUseCases.cs` (usa `StockMovementType.PurchaseReturn` como reversión total) y el propio enum `StockMovementType.cs`.
3. `CancelPurchaseHandler` (`backend/src/ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs`) es el único precedente de reversión: bloquea si `payable.PaidAmount > 0`, cancela retención `Issued`, cancela CxP, revierte 100% del stock con `StockMovementType.PurchaseReturn`, cancela factura — todo en un solo `SaveChangesWithSequenceRetryAsync()`, **sin transacción explícita ni advisory lock** (verificado: no hay `IUnitOfWork`/`BeginTransactionAsync` en ningún archivo de `ERP.Application/Modules/Purchases`). Hallazgo **adicional** al informe P0-02, que no lo señala.
4. `PurchasePayable.BalanceDue = TotalAmount - PaidAmount - TotalRetained` confirmado (`backend/src/ERP.Domain/Modules/Purchases/Entities/PurchasePayable.cs:17`). `Status` solo transiciona `"pending"→"cancelled"` — nunca `"paid"/"partial"` — confirmado, `CancelPayable()` exige `PaidAmount == 0`.
5. **Hallazgo que matiza al informe P0-02**: `SalesReceivable.ApplyReturnCredit()` (`backend/src/ERP.Domain/Modules/Sales/Entities/SalesReceivable.cs:157-175`) **sí** tiene guard `if (amount > BalanceDue) throw` — el precedente de referencia para CxC **ya bloquea explícitamente** devolver más de lo pendiente. El informe P0-02 (§11 fila 3) describe esto como un vacío sin precedente; en realidad el precedente estructural de guard-contra-exceso ya existe, aunque el escenario de negocio (factura totalmente pagada) sigue sin resolver — solo cambia de "no hay ni patrón" a "hay patrón de guard, falta decisión de qué hacer con el excedente".
6. `IssuedWithholding.Cancel()` solo permite `Issued→Cancelled`, todo-o-nada, sin ajuste parcial — confirmado (`backend/src/ERP.Domain/Modules/Purchases/Entities/IssuedWithholding.cs:123-147`).
7. `StockMovementType` es enum C# fijo con `PurchaseReturn=7`, `SupplierCreditNote=9`, `SupplierDebitNote=10` — confirmado; solo `PurchaseReturn` se usa, únicamente en `CancelPurchaseUseCases`.
8. `CurrentStock.ApplyMovement` (`backend/src/ERP.Domain/Modules/Inventory/Entities/CurrentStock.cs:46-58`) lanza `InvalidOperationException` genérica en inglés si `newQty < 0` — confirmado.
9. `StockRepository.CreateAndTrackMovementAsync` (`backend/src/ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs:152-248`) confirma kardex de costo promedio móvil: `resolvedUnitCost = r.UnitCost ?? lastRunningAvg`.
10. `Lot.cs` (línea 9) tiene el comentario `"Solo se crea desde PurchaseReceipt.Confirm()"` pero esa clase **no existe** en el repo — confirmado por búsqueda; `ConfirmPurchaseHandler` no inyecta `ILotRepository`/`ISerialNumberRepository` — confirmado leyendo el constructor completo.
11. `manifest.json` de ElectronicDocuments (`backend/src/ERP.Infrastructure/ElectronicDocuments/Resources/SRI/manifest.json`) confirma: solo `Invoice` y `CreditNote` tienen `activeVersion` no nulo; `DebitNote`, `ShippingGuide`, `Retention`, `PurchaseSettlement` tienen `activeVersion: null`.
12. `SalesReturnRepository.AcquireReturnLockAsync` (`backend/src/ERP.Infrastructure/Persistence/Repositories/Sales/SalesReturnRepository.cs:89-108`) usa `pg_advisory_xact_lock` con hash de dos partes (namespace + Id) dentro de una transacción abierta explícitamente en `AuthorizeSalesReturnHandler` — patrón confirmado exactamente como lo describe el informe P0-02.
13. `Payment`/`PaymentApplicationLine` (`backend/src/ERP.Domain/Modules/Finance/Entities/Payment.cs`) es agregado genérico direccional (`Collection`/`Payment`), aplicación balanceada, sin lógica de "aplicar nota de crédito contra CxP" — confirmado.
14. Frontend: `frontend/src/modules/purchases/pages/` solo tiene `PurchaseReceptionPage.tsx` y `PurchasesPage.tsx` — sin ningún `PurchaseReturn*`. `frontend/src/modules/finance/pages/` tiene `AccountsPayablePage.tsx`, `AccountsReceivablePage.tsx`, `CreditTermsPage.tsx` — confirmado, CxP vive en `finance`, no en `purchases`.
15. `CancelWithholdingHandler` (`backend/src/ERP.Application/Modules/Purchases/UseCases/CancelWithholdingUseCases.cs`) confirma el único mecanismo de reversión de retención: `wh.Cancel()` + `payable.ReverseRetention()`, sin ajuste parcial, con manejo de `DbUpdateConcurrencyException` → `Result.Conflict`.

---

## B. Inventario técnico

| Concepto | Entidad/servicio actual | Archivo y símbolo | Fuente de verdad | Estado | Riesgo |
|---|---|---|---|---|---|
| Ciclo de vida factura compra | `PurchaseInvoice.Status` | `PurchaseInvoice.cs:66` (`PurchaseStatus`) | `PurchaseInvoice` | IMPLEMENTED | Cancel es todo-o-nada; sin cancelación/reversión parcial |
| Snapshot fiscal de línea | `PurchaseInvoiceDetail` | `PurchaseInvoiceDetail.cs` (`IsFrozen`, `VatCode/IceCode` congelados) | `PurchaseInvoiceDetail` | IMPLEMENTED | — |
| Saldo CxP | `PurchasePayable.BalanceDue` | `PurchasePayable.cs:17` | `PurchasePayable` (calculado) | IMPLEMENTED | `Status` no confiable (nunca "paid"/"partial") |
| Reversión total de compra | `CancelPurchaseHandler` | `CancelPurchaseUseCases.cs` | N/A (proceso) | IMPLEMENTED (solo total) | **Sin transacción explícita ni advisory lock** — hallazgo nuevo, riesgo de condición de carrera con `ConfirmPurchaseHandler`/pagos concurrentes |
| Crédito de devolución sobre cuenta por cobrar (referencia) | `SalesReceivable.ApplyReturnCredit` | `SalesReceivable.cs:157-175` | `SalesReceivable` | IMPLEMENTED (CxC) | Guard `amount > BalanceDue` ya existe — no hay equivalente en `PurchasePayable` |
| Crédito de devolución sobre CxP | — | — | — | MISSING | Ninguna entidad/método existe |
| Saldo a favor de proveedor | — | — | — | MISSING | Confirmado por búsqueda exhaustiva sin resultados |
| Retención emitida | `IssuedWithholding` | `IssuedWithholding.cs:123-147` (`Cancel`) | `IssuedWithholding` | PARTIAL | Solo cancelación total; sin ajuste proporcional |
| Movimiento de kardex tipo devolución compra | `StockMovementType.PurchaseReturn` | `StockMovementType.cs:11` | Enum fijo | PARTIAL | Reservado, usado solo como reversión total (`CancelPurchaseUseCases`), semántica no probada para devolución parcial real |
| Lotes/series en Compras | `Lot`, `ILotRepository` | `Lot.cs:9` (comentario referencia clase inexistente `PurchaseReceipt`) | N/A | DOC_CODE_MISMATCH / MISSING | Comentario de dominio referencia una clase que no existe; `ILotRepository` no inyectado en ningún handler de Compras |
| Recepción de compra (Purchase Reception) | `PurchaseReceptionDocument`/`PurchaseReceptionLine` | `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/` | Import XML SRI | IMPLEMENTED (como import, no conteo físico) | No aporta cantidades físicas independientes de la factura |
| Documento SRI recibido (NC proveedor) | `PurchaseInvoice.AccessKey/InvoiceNumber` (verbatim) | `PurchaseInvoice.cs:34,36` | `PurchaseInvoice` | IMPLEMENTED (solo para factura, no para NC) | No existe campo/entidad para registrar una Nota de Crédito del proveedor |
| Emisión electrónica CreditNote (como emisor) | Pipeline SRI completo | `manifest.json`, `IElectronicDocumentIssuer` | Infraestructura FROZEN | IMPLEMENTED (rol emisor, no receptor) | No aplica directamente a Compras (ERP es receptor ahí) |
| Posting Compras | `PurchaseInvoiceConfirmedPostingTranslator` | `.../Translators/PurchaseInvoiceConfirmedPostingTranslator.cs` | `PostingFact`→`IPostingEngine` | IMPLEMENTED | Sin traductor para reversión/devolución |
| Auditoría factura compra | `PurchaseInvoiceAudit`/Handler | `Entities/PurchaseInvoiceAudit.cs` | ADR-022 | IMPLEMENTED | Patrón replicable |
| Advisory lock por factura (Compras) | — | — | — | MISSING | Solo existe para `SalesReturn` (`SalesReturnRepository.AcquireReturnLockAsync`), namespace `"SalesReturn.Lock"` — no hay equivalente para Purchases |

---

## C. Mapa de efectos actuales

| Operación | Inventario | CxP | Pago/crédito | Contabilidad | Auditoría | Transacción |
|---|---|---|---|---|---|---|
| `PurchaseInvoice.Confirm()` (`ConfirmPurchaseHandler`) | No afecta stock (líneas congeladas, sin `AppendMovementAsync` visible en el handler leído — confirmar en fase futura si hace falta detalle línea completa) | Crea `PurchasePayable` vía `_repo.TrackPayable` (no confirmado en el fragmento leído, pero `IPurchaseInvoiceRepository.TrackPayable` existe) | N/A | `PurchaseInvoiceConfirmedPostingTranslator` (`INotificationHandler`) vía evento `PurchaseInvoiceConfirmedEvent` | `PurchaseInvoiceAuditHandler` | Implícita en `SaveChangesAsync`/`SaveChangesWithSequenceRetryAsync`, sin `BeginTransactionAsync` explícito |
| `CancelPurchaseHandler` | `StockRepository.AppendMovementAsync(..., StockMovementType.PurchaseReturn, -qty, ...)` por cada línea con `ItemId` | `payable.CancelPayable()` (exige `PaidAmount==0`) | N/A | Ningún traductor específico de cancelación fue verificado en este alcance | `*AuditHandler` vía domain events de `inv.Cancel()`/`wh.Cancel()` | **Un solo `SaveChangesWithSequenceRetryAsync`, sin transacción explícita ni advisory lock** |
| `CancelWithholdingHandler` | No aplica | `payable.ReverseRetention(inv.PaymentSchedules)` | N/A | No verificado traductor específico | `IssuedWithholdingAuditHandler` (inferido por comentario del propio handler) | `_repo.SaveChangesAsync()`, captura `DbUpdateConcurrencyException` → `Result.Conflict` |
| `AuthorizeSalesReturnHandler` (referencia) | `AppendMovementAsync(..., StockMovementType.SaleReturn, +qty, ...)` | N/A (es CxC) | `SalesReceivable.ApplyReturnCredit` vía `SalesReturnRefundHandler` | `SalesReturnAuthorizedPostingTranslator` | `SalesReturnAuditHandler` | `IUnitOfWork.BeginTransactionAsync` + `AcquireReturnLockAsync` + `CommitAsync`/`RollbackAsync` explícitos |

---

## D. Mapa SSOT

| Dato de negocio | Fuente actual | Almacenado/derivado | Duplicación posible | Confiabilidad |
|---|---|---|---|---|
| Cantidad comprada | `PurchaseInvoiceDetail.Quantity` | Almacenado (congelado tras `Confirm`) | No | Alta |
| Cantidad disponible para devolver | — | No existe cálculo | N/A | MISSING — no hay `GetReturnedQuantityByInvoiceDetailAsync`/guard equivalente al de Sales (`GetReturnableLinesByInvoiceUseCases.cs`, `SalesReturnRemainingQuantityGuard` — confirmado que existen solo en `ERP.Application/Modules/Sales`) |
| Cantidad ya devuelta | — | No existe | N/A | MISSING |
| Saldo original CxP | `PurchasePayable.TotalAmount` | Almacenado | No | Alta |
| Saldo pendiente CxP | `PurchasePayable.BalanceDue` (calculado) | Derivado (`TotalAmount - PaidAmount - TotalRetained`) | No | Alta, pero `Status` string es una fuente paralela poco confiable (nunca refleja "paid"/"partial") |
| Pagos aplicados | `PurchasePayable.PaidAmount` + `Payment.Amount`/líneas | Ambos almacenan el monto — `RegisterPayment`/`ReversePayment` mantienen sincronía manual, sin invariante cruzado automático verificado | Sí — dos aggregates (`Payment` y `PurchasePayable`) mantienen montos relacionados sin validación de consistencia mutua a nivel de dominio | Media — depende de que el caso de uso de Application siempre llame ambos lados |
| Crédito frente al proveedor | — | No existe | N/A | MISSING |
| Aplicaciones de crédito | — | No existe | N/A | MISSING |
| Reembolsos | — | No existe (existe `SalesReturnRefundAllocation` solo del lado ventas/caja) | N/A | MISSING |
| Existencia (stock) | `CurrentStock.Quantity` | Derivado de `StockMovement` (kardex) | No — kardex es la fuente real, `CurrentStock` es proyección | Alta |
| Costo de inventario | `StockMovement.RunningAverageCost`/`RunningStockValue` | Almacenado por movimiento | No | Alta |
| Asientos contables | `PostingFact`→`PostingRule`(config)→motor | Derivado de eventos | No | Alta (para lo ya implementado) |
| Nota de Crédito recibida (proveedor) | — | No existe campo en `PurchaseInvoice` ni entidad separada | N/A | MISSING |
| Retención emitida | `IssuedWithholding.Status` | Almacenado | No | Alta, pero solo total (sin modelo parcial) |
| Estados de procesamiento (Reception) | `PurchaseReceptionProcessingStatus`/`ItemMatchStatus` | Almacenado | No | Alta (para import XML) |

---

## E. Matriz de escenarios

| # Escenario | Soporte actual | Componentes reutilizables | Faltante real | Riesgo |
|---|---|---|---|---|
| 1 Devolución parcial, factura impaga | Ninguno | `StockMovementType.PurchaseReturn`, `StockRepository.AppendMovementAsync`, patrón `SalesReturn` estructural | Agregado `PurchaseReturn`, guard de cantidad remanente, método `PurchasePayable.ApplyReturnCredit` | Medio (caso más simple, pero cero código existente) |
| 2 Devolución total, factura impaga | Ninguno (cercano: `CancelPurchaseHandler`, pero es cancelación total de factura, no devolución) | igual | igual | Medio |
| 3 Devolución < saldo, factura parcial | Ninguno | `PurchasePayable.BalanceDue` | Invariante `PaidAmount <= TotalAmount` tras reducir `TotalAmount` — no existe hoy | Alto (invariante nunca ejercitada) |
| 4 Devolución > saldo, factura parcial | Ninguno | — | Decisión de negocio + modelo de saldo a favor | BLOCKER |
| 5 Devolución, factura totalmente pagada | Ninguno | Guard de `SalesReceivable.ApplyReturnCredit` como patrón (bloquea, no resuelve) | Decisión de negocio (§18.1 del informe P0-02) — coincide con el hallazgo del propio informe | BLOCKER |
| 6 Aplicación posterior de crédito a otra CxP | Ninguno | `Payment`/`PaymentApplicationLine` como patrón genérico de aplicación (no diseñado para créditos) | Entidad de crédito de proveedor + lógica de aplicación cruzada | MISSING total |
| 7 Reembolso del proveedor | Ninguno | `SalesReturnRefundAllocation` como referencia (lado ventas) | Registro de "ingreso de caja por reembolso" — no existe simétrico en Compras | MISSING total |
| 8 Registro posterior de Nota de Crédito | Ninguno | `PurchaseInvoice.AccessKey`/`InvoiceNumber` como patrón de "registrar, no emitir" | Campo/entidad para NC del proveedor | MISSING |
| 9 Factura con retención emitida/autorizada | Solo cancelación total (`CancelWithholdingHandler`/`CancelPurchaseHandler`) | `IssuedWithholding.Cancel()`, `payable.ReverseRetention()` | Ajuste parcial de retención — no existe mecanismo | BLOCKER (riesgo alto, caso de negocio común) |
| 10 Dos usuarios devolviendo simultáneamente | Sin protección — no hay advisory lock para Compras en ningún handler existente | Patrón `pg_advisory_xact_lock` de `SalesReturnRepository` | Repositorio con `AcquireReturnLockAsync` propio para Compras | Alto si se implementa sin este patrón |
| 11 Reintento tras timeout | No aplica (no hay operación de devolución) | Patrón de `SaveChangesWithSequenceRetryAsync` + transacción explícita de `AuthorizeSalesReturnHandler` | Diseño de idempotencia explícito | A definir en diseño |
| 12 Falla entre inventario y CxP | `CancelPurchaseHandler` mitiga parcialmente por ser un solo `SaveChangesAsync` (atomicidad EF), pero **sin transacción explícita** — un fallo de infraestructura entre `AppendMovementAsync` (que no persiste hasta el `SaveChanges` final) y el resto podría dejar estado inconsistente si se interrumpe la conexión a media transacción implícita | El propio patrón de "todo en un `SaveChanges`" de EF Core | Confirmar que el patrón EF-implícito es suficientemente atómico o si se requiere `BeginTransactionAsync` explícito como en `AuthorizeSalesReturnHandler` | Medio — requiere prueba de integración específica, no verificable solo por lectura de código |

---

## F. Entidades candidatas para reutilización

**Entidades a reutilizar (sin modificar):**
- `PurchaseInvoice`/`PurchaseInvoiceDetail` (referenciadas por `Guid`, nunca mutadas por una futura devolución).
- `StockMovementType.PurchaseReturn` (enum ya existe, valor 7) — pero su semántica actual (reversión total de cancelación) debe diferenciarse por `SourceDocType`/`SourceDocId`, nunca asumir intercambiabilidad con una devolución parcial real (riesgo señalado correctamente en el informe P0-02 §17.5).
- `PostingFact`/`IPostingEngine`/`PostingRuleResolver` (sin tocar el motor, solo agregar traductor nuevo).

**Servicios/motores a reutilizar:**
- `IStockRepository.AppendMovementAsync` + `CreateAndTrackMovementAsync` (kardex, costo promedio, concurrencia optimista + retry).
- Patrón de advisory lock (`pg_advisory_xact_lock` con namespace propio) de `SalesReturnRepository`.
- `IAuditService`/`AuditRecordBase` (ADR-022).
- `IUnitOfWork.BeginTransactionAsync`/`CommitAsync`/`RollbackAsync` (patrón usado por `AuthorizeSalesReturnHandler`, **no usado hoy en ningún handler de Purchases**).

**Contratos parciales (existen pero incompletos para el caso):**
- `PurchasePayable` (existe el agregado, falta el método de crédito).
- `IssuedWithholding` (existe el agregado, falta ajuste parcial).
- `Payment`/`PaymentApplicationLine` (existe el motor de aplicación de pagos, no de créditos).

**Conceptos de negocio sin representación actual:**
- Saldo a favor de proveedor / crédito de proveedor.
- Reembolso de proveedor (ingreso de caja).
- Nota de Crédito recibida del proveedor (campo o entidad).
- Cantidad ya devuelta / cantidad remanente por línea de compra.
- Advisory lock dedicado a Compras.

---

## G. Hallazgos de confiabilidad

- **BLOCKER**: `CancelPurchaseHandler` no abre transacción explícita ni usa advisory lock — a diferencia del patrón ya probado en `AuthorizeSalesReturnHandler`. Dos cancelaciones concurrentes de la misma factura, o una cancelación en carrera con `RegisterPayment`, no tienen ninguna serialización explícita más allá de la concurrencia optimista de EF Core sobre entidades individuales — el conjunto de mutaciones (payable + withholding + N movimientos de stock + factura) no está protegido como unidad atómica de negocio, solo por el alcance del `SaveChangesAsync` de EF. Evidencia: ausencia total de `IUnitOfWork`/`BeginTransactionAsync` en `ERP.Application/Modules/Purchases` (confirmado por grep). Consecuencia: una futura `PurchaseReturn` que replique este patrón (en vez del patrón `SalesReturn`) heredaría el mismo riesgo.
- **BLOCKER** (ya señalado por el informe P0-02, confirmado): `IssuedWithholding` no soporta ajuste parcial — bloquea el caso de negocio de devolución parcial contra factura con retención ya emitida.
- **BLOCKER** (ya señalado, confirmado con matiz): factura totalmente pagada sin mecanismo de saldo a favor — el patrón de guard (`amount > BalanceDue` throw) sí existe como precedente en `SalesReceivable.ApplyReturnCredit`, pero solo bloquea, no resuelve el excedente.
- **REQUIRED**: no existe query de "cantidad remanente por línea de factura de compra" (equivalente a `GetReturnedQuantityByInvoiceDetailAsync` de Sales) — prerrequisito estructural antes de cualquier `PurchaseReturn`.
- **REQUIRED**: no existe repositorio con `AcquireReturnLockAsync` para Compras — prerrequisito de concurrencia.
- **NON_BLOCKING**: `PurchasePayable.Status` nunca transiciona a "paid"/"partial" — brecha preexistente, mitigable usando `BalanceDue` en el diseño nuevo (correcto, como ya indica el informe P0-02).
- **NON_BLOCKING**: comentario obsoleto en `Lot.cs` referenciando `PurchaseReceipt.Confirm()` (clase inexistente) — deuda documental, no bloquea diseño si no se requiere trazabilidad de lote.
- **OUT_OF_SCOPE**: emisión de documentos SRI (`DebitNote`, `Retention`, `PurchaseSettlement`) sin builder/provider — confirmado, fuera del alcance de una devolución que "registra, no emite" (según decisión fiscal propuesta en el informe P0-02, no verificable por código, ver sección H).

---

## H. Preguntas que requieren decisión de negocio

(Ninguna resoluble por inspección de código — coinciden con las ya identificadas en el informe P0-02 §18, confirmadas como genuinamente pendientes tras esta auditoría independiente):
1. Tratamiento financiero del excedente cuando la factura ya está totalmente pagada (saldo a favor / reembolso / aplicación a próxima compra).
2. Tratamiento de retención ya emitida ante devolución parcial (ajuste parcial nuevo / cancelar y reemitir / bloquear).
3. Confirmación normativa fiscal (Ecuador): si el comprador solo registra la NC del proveedor o si existe algún caso de excepción — el informe P0-02 lo trata como conocimiento normativo general, no como hecho verificable en el repositorio; efectivamente no se encontró ninguna referencia normativa citable dentro del código para este punto (confirmado, `Grep "devolución de compra"` sin resultados en ADRs ni ficha técnica).
4. Si se permite autorizar sin tener aún el número/clave de la NC del proveedor.
5. Si se requiere trazabilidad de lote/serie.
6. Si existe caso de negocio real para Nota de Débito emitida por el comprador.

---

## I. Conclusión

1. **Fuentes de verdad que pueden conservarse**: `PurchaseInvoice`/`PurchaseInvoiceDetail` (congelamiento de costos y snapshot fiscal), `PurchasePayable.BalanceDue` (usando `BalanceDue`, nunca `Status`), el motor de kardex (`StockRepository.CreateAndTrackMovementAsync`), el motor de posting (`PostingFact`/`IPostingEngine`), y el patrón ADR-022 de auditoría. Todas verificadas como estables y consistentes.
2. **Duplicaciones a evitar**: no crear una segunda fuente de "saldo pendiente" fuera de `BalanceDue`; no reimplementar kardex/costo promedio fuera de `StockRepository`; no crear un segundo mecanismo de aplicación de pagos fuera de `Payment`/`PaymentApplicationLine` si se decide modelar el crédito de proveedor como un tipo de documento aplicable.
3. **Conceptos que faltan realmente** (verificados como ausentes, no solo "no revisados"): cantidad remanente/devuelta por línea de compra, crédito de proveedor, reembolso de proveedor, ajuste parcial de retención, campo/entidad para Nota de Crédito recibida, advisory lock dedicado a Compras, invariante `PaidAmount <= TotalAmount` en `PurchasePayable` tras cualquier reducción de `TotalAmount`.
4. **Bloqueos que impiden implementar de forma segura hoy**: (a) ausencia de transacción explícita + advisory lock en el módulo de Purchases en general (hallazgo nuevo de esta auditoría, más amplio que lo señalado en P0-02); (b) las 3 decisiones de negocio no resueltas (factura pagada, retención parcial, alcance fiscal SRI).
5. **¿Listo para iniciar diseño detallado?** Técnicamente sí para la fase de diseño (no de código): la inspección confirma que el mapa de brechas del informe P0-02 es sustancialmente correcto y verificable contra el código real, con dos correcciones/adiciones de esta auditoría: (a) `SalesReceivable.ApplyReturnCredit` ya tiene guard contra exceso — matiza, no invalida, el hallazgo de "sin protección equivalente"; (b) el módulo de Purchases carece de transacción explícita/advisory lock en general (no solo para devoluciones), lo cual debe incorporarse como prerrequisito de concurrencia del diseño, no asumirse resuelto solo por replicar el patrón de `AuthorizeSalesReturnHandler`. No está listo para **codificar** hasta resolver las 3 decisiones de negocio de la sección H.

---

## Nota de verificación pendiente

Un punto de la sección C quedó marcado como no confirmado con evidencia de línea exacta durante esta auditoría: el efecto de `ConfirmPurchaseHandler` sobre inventario (si `Confirm()` afecta o no `StockMovement` directamente, más allá del congelamiento de costos). Antes de diseñar el flujo de devolución, confirmar con lectura completa de `ConfirmPurchaseUseCases.cs` si existe algún movimiento de entrada de inventario en la confirmación de factura, para no asumir un punto de partida incorrecto sobre "cuándo entra el stock" en el ciclo de Compras.
