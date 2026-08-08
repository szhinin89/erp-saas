# Plan de Implementación — Devolución de Venta + Nota de Crédito SRI (P0-01)

**Tipo de documento:** Plan de implementación técnico. No contiene código, no modifica archivos, no crea ADR.
**Fecha:** 2026-07-30
**Basado en:** `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md` (arquitectura aprobada). Este plan no la modifica; solo la ordena en fases ejecutables e incorpora las 4 decisiones de producto aprobadas para el MVP.

## Estado de cierre

**P0-01: COMPLETED / CLOSED (2026-07-31).** Las 15 fases descritas abajo fueron ejecutadas y cerradas, incluyendo el gate de gobernanza (Fase 11, `docs/adr/ADR-031-credit-note-v1-activation.md`, Accepted) y la regresión E2E final (Fase 15, `SalesReturnEndToEndTests`, 23/23 en verde contra PostgreSQL real). Ver `docs/STATUS.md` para el registro de cierre y capacidades entregadas. Backlog técnico no bloqueante registrado en la sección 4 de este documento. Esta sección es la única actualización de estado sobre el plan original — el contenido de las Fases 1-15 y la tabla de dependencias quedan como constancia histórica de lo planificado, sin alterarse.

---

## 0. Decisiones de producto aprobadas (entrada de este plan, no se re-discuten)

1. La activación de Nota de Crédito (`manifest.json`) sigue el proceso ADR formal — tratada como **gate externo**, no como una fase de código de este plan.
2. **Sin prorrateo automático** entre formas de pago. La devolución debe registrar **explícitamente** cómo se reembolsa (efectivo / crédito contra CxC), elegido por el operador al autorizar. Esto **completa** (no contradice) el diseño aprobado, que dejaba esto como decisión de negocio pendiente en su §14.
3. RIDE de Nota de Crédito **incluido en el MVP** (no es fast-follow).
4. **Sin workflow de aprobación** — cualquier usuario con el permission key correspondiente autoriza directamente.

### Consecuencia de la decisión 2 sobre el diseño aprobado

El diseño (§2.3, §5 Caja) preveía un reembolso único en efectivo o crédito calculado automáticamente. Con prorrateo automático descartado, `SalesReturn` necesita capturar, al momento de autorizar, una **asignación explícita de reembolso** (cuánto va a efectivo, cuánto va a crédito de CxC), ingresada por el operador y validada contra el total devuelto. Esto se modela como una entidad hija nueva `SalesReturnRefundAllocation` dentro del mismo agregado `SalesReturn` ya definido en el diseño — es una precisión de su forma interna, no una desviación de la arquitectura aprobada (el diseño ya preveía "líneas" como hijas del agregado; esta es una segunda colección hija del mismo tipo de patrón).

---

## 1. Principios de secuenciación

- Cada fase toca **un solo módulo como superficie principal** (Domain, luego Infrastructure, luego Application por partes, luego Caja, luego Accounting, luego Audit, luego API, luego ElectronicDocuments, luego Ride, luego Frontend).
- Ninguna fase depende de que una fase *posterior* exista — cada una es compilable y testeable de forma aislada.
- Las fases de mayor riesgo (concurrencia, dinero, SRI) están deliberadamente separadas de las fases de bajo riesgo (CRUD de Draft, consultas).
- El gate de gobernanza (manifest.json/ADR) se aísla en su propia fase, al final, para no bloquear el resto del trabajo.

---

## Fase 1 — Dominio: `SalesReturn` + `SalesReturnDetail` + `SalesReturnRefundAllocation`

**Objetivo:** Modelar el agregado completo en `ERP.Domain`, sin persistencia ni casos de uso. Compilable y testeable de forma totalmente aislada.

**Archivos esperados:**
- `ERP.Domain/Modules/Sales/Entities/SalesReturn.cs`
- `ERP.Domain/Modules/Sales/Entities/SalesReturnDetail.cs`
- `ERP.Domain/Modules/Sales/Entities/SalesReturnRefundAllocation.cs`
- `ERP.Domain/Modules/Sales/Enums/SalesReturnStatus.cs`
- `ERP.Domain/Modules/Sales/Enums/SalesReturnRefundMethod.cs` (`Cash`, `ReceivableCredit`)
- `ERP.Domain/Modules/Sales/Events/SalesReturnAuthorizedEvent.cs`
- `ERP.Domain.Tests/Sales/SalesReturnTests.cs`
- `ERP.Domain.Tests/Sales/SalesReturnDetailTests.cs`

**Módulos afectados:** Sales (Domain) únicamente.

**Dependencias:** Ninguna — es la fase raíz.

**Criterios de aceptación:**
- `SalesReturn.CreateDraft(...)` exige `salesInvoiceId`, `customerId`, `reason` no vacío.
- `AddLine(...)`/`RemoveLine(...)` solo permitidos en `Draft`.
- `Authorize(...)` exige ≥1 línea, congela líneas (`IsFrozen`), snapshotea `AuthorizedSubtotal/TotalVat/TotalIce/TotalDiscount/GrandTotal`, exige que `Σ RefundAllocation.Amount == AuthorizedGrandTotal` (regla de la decisión 2), transiciona a `Authorized`, dispara `SalesReturnAuthorizedEvent`.
- `Cancel(...)` solo legal desde `Draft`.
- `SalesReturnDetail.Create(...)` exige `Quantity > 0`, hereda snapshot fiscal sin recalcular impuestos.
- `SalesReturnRefundAllocation`: `Amount > 0`, `Method` obligatorio; el agregado permite múltiples allocations (ej. parte efectivo + parte crédito).
- No existe ningún método que permita reabrir un `SalesReturn` `Authorized`.
- 0 dependencias a `ERP.Application`/`ERP.Infrastructure`/otros agregados (`SalesInvoice` se referencia solo por `Guid`).

**Tests necesarios:**
- Creación válida/ inválida de Draft.
- Autorización: éxito, falla sin líneas, falla si `Σ RefundAllocation != GrandTotal`, falla con `Reason` vacío.
- Cancelación: éxito desde Draft, falla desde Authorized.
- Inmutabilidad de líneas post-`Authorize`.
- `SalesReturnAuthorizedEvent` contiene todos los campos requeridos por `PostingFact` (Subtotal/TotalVat/TotalIce/TotalDiscount/GrandTotal) + `SalesInvoiceId`/`ReturnNumber`.

**Riesgos:** Bajo. Sin efectos colaterales, sin BD, sin concurrencia real todavía. Riesgo principal: subdimensionar el modelo de `RefundAllocation` y tener que extenderlo en una fase posterior (mitigado revisando este diseño contra la Fase 6 antes de cerrarla).

---

## Fase 2 — Dominio: extensión de `SalesReceivable`

**Objetivo:** Agregar los métodos de crédito necesarios para que una devolución reduzca CxC, sin tocar ningún otro comportamiento existente de `SalesReceivable`.

**Archivos esperados:**
- `ERP.Domain/Modules/Sales/Entities/SalesReceivable.cs` (modificación — solo agregar métodos nuevos, sin tocar los existentes)
- `ERP.Domain.Tests/Sales/SalesReceivableTests.cs` (extensión de tests existentes)

**Módulos afectados:** Sales (Domain).

**Dependencias:** Ninguna sobre la Fase 1 (es independiente, puede ejecutarse en paralelo con la Fase 1 si se desea, pero se numera después por afinidad temática).

**Criterios de aceptación:**
- `ApplyReturnCredit(decimal amount, Guid updatedBy)`: reduce `OriginalAmount -= amount`; lanza si `amount > BalanceDue`; lanza si `Status == "cancelled"`; no toca `PaidAmount`.
- `RebuildInstallments(...)`: replica el patrón ya existente en `PurchasePayable.RebuildInstallments()` — reconstruye `_installments` proporcionalmente al nuevo `OriginalAmount`.
- Ningún test existente de `SalesReceivableTests.cs` (`RegisterCollection`/`ReverseCollection`/`Cancel`) cambia de comportamiento ni de resultado.

**Tests necesarios:**
- `ApplyReturnCredit`: sin pagos previos, con pago parcial, `amount == BalanceDue` exacto, `amount > BalanceDue` → rechazo, sobre receivable cancelado → rechazo.
- `RebuildInstallments`: cuotas recalculadas correctamente tras un crédito parcial.
- Regresión: suite completa de `SalesReceivableTests.cs` sigue en verde.

**Riesgos:** Medio-bajo. Es el único punto donde se modifica una entidad *existente* (no se crea una nueva) — riesgo de regresión sobre CxC ya en producción. Mitigación: cambio estrictamente aditivo (métodos nuevos), correr toda la suite de Sales antes de cerrar la fase.

---

## Fase 3 — Infraestructura: persistencia de `SalesReturn`

**Objetivo:** Persistir el agregado de la Fase 1, incluyendo el repositorio con el advisory lock de concurrencia. Sin casos de uso todavía — solo repositorio + EF + migración.

**Archivos esperados:**
- `ERP.Infrastructure/Persistence/Configurations/Sales/SalesReturnConfiguration.cs`
- `ERP.Infrastructure/Persistence/Configurations/Sales/SalesReturnDetailConfiguration.cs`
- `ERP.Infrastructure/Persistence/Configurations/Sales/SalesReturnRefundAllocationConfiguration.cs`
- `ERP.Domain/Modules/Sales/Interfaces/ISalesReturnRepository.cs`
- `ERP.Infrastructure/Persistence/Repositories/Sales/SalesReturnRepository.cs`
- Migración EF nueva (tablas `sales_returns`, `sales_return_details`, `sales_return_refund_allocations` + índices, incluyendo el índice/consulta usada por `GetReturnedQuantityByInvoiceDetailAsync`)
- `ERP.Infrastructure.Tests/Persistence/SalesReturnRepositoryTests.cs`

**Módulos afectados:** Sales (Infrastructure), Migrations.

**Dependencias:** Fase 1 completa (necesita el agregado y sus entidades hijas ya definidos).

**Criterios de aceptación:**
- `ISalesReturnRepository` expone: `GetByIdAsync`, `AddAsync`, `GetPagedAsync`, `GetReturnedQuantityByInvoiceDetailAsync(tenantId, invoiceDetailId)`, `AcquireReturnLockAsync(tenantId, salesInvoiceId, ct)` (advisory lock transaccional, mismo patrón SQL que `PostingIdempotencyGuard.AcquireIdempotencyLockAsync`), `SaveChangesAsync`.
- `GetReturnedQuantityByInvoiceDetailAsync` suma únicamente líneas de `SalesReturn` en estado `Authorized` (excluye `Draft`/`Cancelled`).
- Migración aplica limpio sobre BD de desarrollo, `ErpDbContextModelSnapshot.cs` actualizado.
- FKs: `SalesReturn.SalesInvoiceId` sin navegación EF (mismo patrón que `SalesReceivable.InvoiceId`), `DeleteBehavior.Restrict`.
- `xmin`/concurrency token configurado igual que el resto de agregados transaccionales del repo.

**Tests necesarios:**
- CRUD básico contra PostgreSQL real (Testcontainers, igual que el resto de `*.Infrastructure.Tests`).
- `GetReturnedQuantityByInvoiceDetailAsync`: devuelve 0 sin devoluciones previas; suma correcta con 2+ devoluciones `Authorized`; ignora devoluciones `Draft`/`Cancelled`.
- Test de concurrencia real: dos transacciones paralelas invocando `AcquireReturnLockAsync` con el mismo `SalesInvoiceId` se serializan (una espera a la otra), dos con `SalesInvoiceId` distintos no se bloquean entre sí — mismo patrón de prueba que `DocumentSequenceConcurrencyTests`/`SalesInvoiceAuthorizedPostingIntegrationTests` (dos tasks, cada una con su propio `ErpDbContext`/transacción).

**Riesgos:** Medio. Es la primera vez que se usa un advisory lock fuera del módulo Accounting — riesgo de errores sutiles en el hash de la clave de lock (colisión con otras claves de lock del sistema si el hashing no es lo suficientemente distintivo). Mitigación: usar un espacio de claves (namespace) de advisory lock claramente distinto al de `PostingIdempotencyGuard` (ej. offset o prefijo distinto en el hash de entrada), verificado con un test que confirme que un lock de `SalesReturn` no interfiere con uno de Accounting corriendo en paralelo.

---

## Fase 4 — Aplicación: CRUD de Draft (sin efectos colaterales)

**Objetivo:** Exponer los casos de uso de bajo riesgo — crear/editar/cancelar un Draft y consultar líneas devolvibles — sin tocar inventario, caja, CxC, contabilidad ni SRI. Esta fase es deliberadamente "inocua": un Draft sin autorizar no afecta ningún otro módulo.

**Archivos esperados:**
- `ERP.Application/Modules/Sales/UseCases/SalesReturnDraftUseCases.cs` (`CreateSalesReturnDraftCommand`, `UpdateSalesReturnDraftCommand`, `CancelSalesReturnDraftCommand` + handlers)
- `ERP.Application/Modules/Sales/UseCases/GetReturnableLinesByInvoiceUseCases.cs` (`GetReturnableLinesByInvoiceQuery` + handler, usa `GetReturnedQuantityByInvoiceDetailAsync` de la Fase 3)
- `ERP.Application/Modules/Sales/UseCases/SalesReturnQueryUseCases.cs` (`GetSalesReturnByIdQuery`, `GetSalesReturnListQuery`)
- `ERP.Application/Modules/Sales/DTOs/SalesReturnDto.cs`, `ReturnableLineDto.cs`
- `ERP.Application.Tests/Sales/SalesReturnDraftHandlerTests.cs`
- `ERP.Application.Tests/Sales/GetReturnableLinesByInvoiceHandlerTests.cs`

**Módulos afectados:** Sales (Application).

**Dependencias:** Fase 1 (dominio) + Fase 3 (repositorio). No depende de la Fase 2 (CxC) porque un Draft no toca CxC.

**Criterios de aceptación:**
- `CreateSalesReturnDraftCommand` valida: factura existe y está `Authorized` (vía `ISalesInvoiceRepository.GetByIdAsync`), cada línea referencia un `OriginalInvoiceDetailId` real de esa factura, `Quantity ≤ remanente` (usando `GetReturnedQuantityByInvoiceDetailAsync`).
- `GetReturnableLinesByInvoiceQuery` devuelve, por línea original: cantidad original, ya devuelta, remanente.
- Ningún caso de uso de esta fase llama a `IStockRepository`, `ICashSessionRepository`/Caja, `IPostingEngine`, ni `IElectronicDocumentIssuer`.
- 422 estructurado (FluentValidation) ante motivo vacío, cantidad ≤ 0, o cantidad > remanente.

**Tests necesarios:**
- Crear draft válido con 1 y con múltiples líneas.
- Rechazo: factura no encontrada, factura `Draft`/`Cancelled`, cantidad excede remanente, motivo vacío.
- `GetReturnableLinesByInvoiceQuery`: remanente correcto tras 0 y tras N devoluciones autorizadas previas (usa datos sembrados vía la Fase 3).
- Actualizar/cancelar draft: solo permitido en estado `Draft`.

**Riesgos:** Bajo. Sin efectos colaterales reales; el mayor riesgo es un cálculo incorrecto de remanente, mitigado con los tests de la fase anterior + esta.

---

## Fase 5 — Aplicación: `AuthorizeSalesReturnCommand` (núcleo + inventario)

**Objetivo:** Implementar la autorización real, con el efecto de negocio más fundamental de una devolución — reversión de inventario — pero **sin** tocar todavía Caja/CxC, Accounting ni SRI (esas quedan en fases posteriores, cada una activable de forma independiente). El comando ya exige `RefundAllocations` en el payload (validadas en Fase 1), pero en esta fase **no se ejecuta** el movimiento de caja/CxC correspondiente — solo se persiste la asignación elegida, a la espera de la Fase 6.

**Archivos esperados:**
- `ERP.Application/Modules/Sales/UseCases/AuthorizeSalesReturnUseCases.cs` (`AuthorizeSalesReturnCommand` + `AuthorizeSalesReturnHandler`)
- `ERP.Application.Tests/Sales/AuthorizeSalesReturnHandlerTests.cs`

**Módulos afectados:** Sales (Application) → Inventory (vía `IStockRepository`, sin modificarlo).

**Dependencias:** Fases 1, 3, 4.

**Criterios de aceptación:**
- Handler adquiere `AcquireReturnLockAsync(tenantId, salesInvoiceId, ct)` como primer paso, dentro de la misma transacción.
- Revalida remanente por línea (doble check bajo lock — el de la Fase 4 es UX temprana, este es el que realmente garantiza consistencia).
- Llama `salesReturn.Authorize(...)`.
- Por cada línea con `ItemId`+`WarehouseId`: `_stockRepo.AppendMovementAsync(..., StockMovementType.SaleReturn, +qty, ..., sourceDocId: salesReturn.Id, sourceDocType: "SalesReturn", reference: $"DEV-{ReturnNumber} (Factura {InvoiceNumber})")`.
- `SaveChangesWithSequenceRetryAsync` confirma inventario + `SalesReturn` en la misma unidad de trabajo.
- **No** se invoca Caja, CxC, Accounting ni ElectronicDocuments en esta fase (se agregan explícitamente en fases posteriores; el comando ya persiste `RefundAllocations` pero ningún handler las consume todavía).

**Tests necesarios:**
- Autorización feliz con 1 y múltiples líneas, distintas bodegas.
- Rechazo: cantidad excede remanente (bajo lock, revalidado).
- Concurrencia real: dos autorizaciones simultáneas sobre la misma factura donde la suma excede el remanente → solo una debe tener éxito (test contra PostgreSQL real).
- Movimiento de inventario generado con `sourceDocType="SalesReturn"` (no `"SalesInvoice"`) y cantidad positiva correcta.
- Regresión: `GetKardexByDocument` para la factura original sigue devolviendo solo sus propios movimientos `SaleExit`; el nuevo movimiento aparece bajo `sourceDocId=salesReturn.Id`.

**Riesgos:** Alto (es la fase más sensible del plan — dinero e inventario). Mitigación: dejarla deliberadamente sin efectos financieros todavía (Fase 6) para poder validar en aislamiento la lógica de concurrencia/inventario antes de sumarle complejidad financiera. Riesgo secundario: colisión de nombres/semántica entre el advisory lock de `SalesReturn` y el de `PostingIdempotencyGuard` — cubierto por el test de la Fase 3.

---

## Fase 6 — Caja / CxC: ejecución de `RefundAllocations`

**Objetivo:** Consumir las `RefundAllocation` ya persistidas en la Fase 5 para ejecutar el reembolso real: movimiento de Caja para las asignaciones `Cash`, `SalesReceivable.ApplyReturnCredit` para las asignaciones `ReceivableCredit`. Sin prorrateo automático (decisión 2) — la asignación ya viene decidida por el operador desde la UI/comando.

**Archivos esperados:**
- `ERP.Domain/Modules/Caja/Enums/CashMovementType.cs` (agregar `SaleRefund`)
- `ERP.Domain/Modules/Caja/Enums/CashReferenceType.cs` (agregar `SalesReturn`)
- `ERP.Domain/Modules/Caja/Entities/CashMovement.cs` (clasificar `SaleRefund` en `IsExpense`)
- `ERP.Application/Modules/Caja/UseCases/SalesReturnRefundHandler.cs` (`INotificationHandler<SalesReturnAuthorizedEvent>`, o invocado directamente desde `AuthorizeSalesReturnHandler` — ver nota de diseño abajo)
- `ERP.Application.Tests/Caja/SalesReturnRefundHandlerTests.cs`

**Módulos afectados:** Caja (Domain + Application), Sales (Application — consumo de `RefundAllocations`).

**Dependencias:** Fase 5 (necesita `SalesReturn.Authorize()` y `RefundAllocations` ya persistidas) + Fase 2 (`SalesReceivable.ApplyReturnCredit`).

**Decisión de implementación a resolver en esta fase:** si la ejecución del reembolso es **síncrona** dentro de `AuthorizeSalesReturnHandler` (mismo patrón que hoy usa `AuthorizeSalesUseCases.cs` para el egreso de stock — efectos en la misma transacción/unidad de trabajo) o **asíncrona** vía `INotificationHandler<SalesReturnAuthorizedEvent>` (mismo patrón que `SalesInvoiceAuthorizedHandler` para `SaleIncome`). Se recomienda **síncrono**, porque un reembolso de efectivo que falla silenciosamente (como hoy hace `SalesInvoiceAuthorizedHandler` con un warning y omisión) es inaceptable para dinero saliendo de caja — la autorización de la devolución debe fallar completa si no hay sesión de caja abierta para procesar el efectivo, no autorizar la devolución y perder el reembolso.

**Criterios de aceptación:**
- Por cada `RefundAllocation` con `Method = Cash`: requiere una `CashSession` `Open` para el usuario/caja que procesa (no la sesión original de la venta); si no existe, la autorización completa de la devolución falla (fail-closed, transacción revertida) — no se autoriza una devolución sin poder ejecutar su reembolso en efectivo.
- Movimiento registrado: `CashMovementType.SaleRefund`, `CashReferenceType.SalesReturn`, `referenceId = salesReturn.Id`.
- Por cada `RefundAllocation` con `Method = ReceivableCredit`: llama `receivable.ApplyReturnCredit(amount, ...)` + `RebuildInstallments(...)`; falla si `amount > receivable.BalanceDue` (mismo invariante de Fase 2).
- `Σ RefundAllocation.Amount` ya fue validado == `GrandTotal` devuelto en la Fase 1 (dominio) — esta fase no revalida el total, solo ejecuta cada asignación.
- `CashSession.CurrentBalance` refleja correctamente la salida (`TotalExpense` incluye `SaleRefund`).

**Tests necesarios:**
- Reembolso 100% efectivo, 100% crédito CxC, y mixto (parte efectivo + parte crédito) en una sola devolución.
- Rechazo: `RefundAllocation.Method = Cash` sin sesión de caja abierta → toda la autorización de la devolución se revierte (transacción atómica, no queda un `SalesReturn.Authorized` huérfano sin su reembolso).
- Rechazo: `RefundAllocation.Method = ReceivableCredit` por un monto mayor al `BalanceDue` actual de la CxC.
- Escenario "CxC ya 100% pagada": una `RefundAllocation.ReceivableCredit` contra un `BalanceDue = 0` debe rechazarse explícitamente (el operador, no el sistema, decide en ese caso asignar 100% a `Cash` — consistente con la decisión 2 de no prorratear automáticamente).
- Regresión: `SalesInvoiceAuthorizedHandlerTests.cs` (flujo de `SaleIncome` original) sigue en verde sin cambios de comportamiento.

**Riesgos:** Alto. Es la fase que mueve dinero real de Caja/CxC. Mitigación principal: atomicidad estricta (todo o nada dentro de la misma transacción que autoriza la devolución) y fail-closed explícito ante ausencia de sesión de caja — sin excepciones silenciosas. Riesgo secundario: el operador podría asignar una `RefundAllocation.ReceivableCredit` mayor al saldo antes de que otra operación concurrente reduzca ese saldo — cubierto porque toda la autorización ya corre bajo el advisory lock de la Fase 5 (por `SalesInvoiceId`), pero **no** bajo un lock por `SalesReceivable` si dos devoluciones de facturas distintas afectan el mismo cliente simultáneamente sin compartir factura — se declara fuera de alcance porque `BalanceDue` es por factura, no por cliente, así que no hay condición de carrera cross-factura real aquí.

---

## Fase 7 — Accounting: asiento contable de la devolución

**Objetivo:** Generar el asiento contable correspondiente, siguiendo exactamente el patrón ya usado por `CollectionReversedPostingTranslator` — sin tocar ningún componente compartido del Posting Engine.

**Archivos esperados:**
- `ERP.Application/Modules/Accounting/Posting/Translators/SalesReturnAuthorizedPostingTranslator.cs`
- `ERP.Application.Tests/Accounting/SalesReturnAuthorizedPostingTranslatorTests.cs`
- `ERP.Infrastructure.Tests/Accounting/SalesReturnAuthorizedPostingIntegrationTests.cs`
- Dato de configuración: nueva fila `PostingRule` para `(SourceModule="Sales", FactType="SalesReturn")` — se crea vía los use cases de `PostingRule` ya existentes (dato, no migración de esquema nueva más allá de la fila en sí, que puede sembrarse en el ambiente correspondiente o gestionarse vía la UI de configuración contable ya existente).

**Módulos afectados:** Accounting (Application — solo un translator nuevo, cero cambios a `PostingEngine`/`PostingPipeline`/`PostingFact`/`JournalFactory`).

**Dependencias:** Fase 5 (necesita `SalesReturnAuthorizedEvent` disparado).

**Criterios de aceptación:**
- `SalesReturnAuthorizedPostingTranslator : INotificationHandler<SalesReturnAuthorizedEvent>` construye un `PostingFact(SourceModule: "Sales", FactType: "SalesReturn", ...)` con los 5 campos de monto del evento y llama `IPostingEngine.PostAsync`.
- Si falla el posting (ej. `RULE_NOT_FOUND` porque la `PostingRule` no fue configurada en ese tenant), el translator solo loguea warning — igual que `SalesInvoiceAuthorizedPostingTranslator` — no revierte la autorización de la devolución ya persistida.
- Republicar el mismo evento (reintento) produce exactamente 1 `JournalEntry` (idempotencia heredada del pipeline, sin código nuevo de idempotencia en esta fase).
- Cero líneas modificadas en `PostingFact.cs`, `PostingEngine.cs`, `PostingPipeline.cs`, `JournalFactory.cs`, `PostingIdempotencyGuard.cs`.

**Tests necesarios:**
- Traducción evento→`PostingFact` con valores correctos.
- Fallo de posting no revierte la autorización (mismo test que existe hoy para `SalesInvoiceAuthorizedPostingTranslator`, replicado).
- Idempotencia bajo republicación concurrente del mismo evento (mismo patrón de test que `SalesInvoiceAuthorizedPostingIntegrationTests`, con 2 tasks paralelas).

**Riesgos:** Bajo. Sigue un patrón ya probado 4 veces en el repo (`SalesInvoiceAuthorized`, `PurchaseInvoiceConfirmed`, `CollectionReversed`, `SupplierPaymentReversed`). Riesgo principal: olvidar sembrar la `PostingRule` en un ambiente y que el posting falle silenciosamente sin que nadie lo note — mitigado documentando el requisito de configuración en el checklist de despliegue de esta fase.

---

## Fase 8 — Audit: trazabilidad de la devolución

**Objetivo:** Registrar la devolución en el Entity Audit, siguiendo el patrón `PricingRuleAudit`/`PricingRuleAuditHandler` exactamente.

**Archivos esperados:**
- `ERP.Domain/Modules/Sales/Entities/SalesReturnAudit.cs`
- `ERP.Application/Modules/Sales/EventHandlers/SalesReturnAuditHandler.cs`
- `ERP.Infrastructure/Persistence/Configurations/Sales/SalesReturnAuditConfiguration.cs`
- Migración EF para la tabla `sales_return_audit`
- `ERP.Domain.Tests/Sales/SalesReturnAuditTests.cs`
- `ERP.Application.Tests/Sales/SalesReturnAuditHandlerTests.cs`

**Módulos afectados:** Sales (Domain + Application), Audit (solo consumo, cero cambios a `AuditRecordBase`/`IAuditService`/`IAuditWriter<T>`).

**Dependencias:** Fase 5 (evento) + Fase 1 (agregado).

**Criterios de aceptación:**
- `SalesReturnAudit : AuditRecordBase` con campos propios (`SalesInvoiceId`, `GrandTotal`, `Reason`, etc.) — sin columnas de otros módulos.
- `SalesReturnAuditHandler : INotificationHandler<SalesReturnAuthorizedEvent>` llama `IAuditService.RecordAsync(SalesReturnAudit.Create(_context.Actor, ...), ct)`.
- Configuración EF usa `ConfigureAuditBase<T>()` compartido sin modificarlo.
- Registro de auditoría visible vía `IAuditReader<SalesReturnAudit>` (sin UI todavía — eso es Fase 13/14 si se decide exponerlo).

**Tests necesarios:**
- Handler produce exactamente 1 registro de auditoría por evento.
- Campos del registro coinciden con los del evento + `AuditActor` resuelto correctamente.

**Riesgos:** Bajo. Aditivo puro, patrón ya probado dos veces en el repo (Pricing, Items).

---

## Fase 9 — API: exponer los casos de uso

**Objetivo:** Publicar el controller REST, wiring de validación y permisos, integrando todo lo construido en las Fases 4-6 (Fases 7-8 son transparentes, no requieren superficie API propia).

**Archivos esperados:**
- `ERP.API/Controllers/SalesReturnsController.cs`
- Validators FluentValidation: `CreateSalesReturnDraftCommandValidator`, `AuthorizeSalesReturnCommandValidator`
- Registro de permission keys nuevos en el catálogo de Access (`sales.returns.create`, `.view`, `.authorize`)
- `ERP.API.Tests/SalesReturns/SalesReturnsControllerTests.cs`

**Módulos afectados:** API, Access/IAM (solo catálogo de permisos, aditivo).

**Dependencias:** Fases 4, 5, 6 (los comandos/queries que expone ya deben existir y funcionar).

**Criterios de aceptación:**
- Endpoints: `GET /sales-invoices/{id}/returnable-lines`, `POST /sales-returns`, `PUT /sales-returns/{id}`, `POST /sales-returns/{id}/cancel`, `POST /sales-returns/{id}/authorize`, `GET /sales-returns/{id}`, `GET /sales-returns`.
- 422 estructurado (`applyServerErrors`-compatible) ante violaciones de FluentValidation, con nombres de propiedad camelCase.
- Autorización por permission key en cada endpoint (sin workflow de aprobación — decisión 4: basta con el permiso, sin paso adicional).
- Ningún endpoint expone campos que el dominio no valide (ej. no se puede inyectar `RefundAllocation.Amount` sin que el dominio valide la suma == total).

**Tests necesarios:**
- Contrato de cada endpoint (200/201/400/401/403/404/422).
- Test de permisos: usuario sin el permission key → 403.
- Test end-to-end de contrato (API.Tests, in-memory) para el flujo completo Draft→Authorize sin tocar SRI todavía (SRI llega en Fase 10, puede mockearse/omitirse aquí si `ElectronicDocumentType.CreditNote` aún no tiene builder registrado — ver nota de dependencia cruzada abajo).

**Nota de dependencia cruzada:** si la Fase 10 (SRI) no está lista aún, `AuthorizeSalesReturnHandler` no debe invocar la emisión de Nota de Crédito de forma bloqueante para el resto del flujo — se recomienda que la llamada a `RegisterAsync` para `CreditNote` se agregue recién en la Fase 10, de modo que las Fases 1-9 sean funcionalmente completas y desplegables como "devolución sin Nota de Crédito electrónica" si el negocio necesitara adelantar el lanzamiento antes de que el gate de gobernanza (Fase 11) se resuelva. Esto es una ventaja de secuenciación, no una funcionalidad a medias: cada fase queda en un estado consistente y potencialmente entregable.

**Riesgos:** Bajo-medio. Riesgo principal: exponer un endpoint antes de que el permission key esté correctamente registrado (fuga de autorización) — mitigado con el test de permisos explícito.

---

## Fase 10 — ElectronicDocuments: builder/provider de Nota de Crédito (sin activar)

**Objetivo:** Construir toda la infraestructura de emisión de Nota de Crédito de forma aditiva, **sin** activar `manifest.json` todavía — esta fase deja el código listo pero inerte (el pipeline seguirá fallando en el paso de validación XSD hasta que `activeVersion` se active en la Fase 11, lo cual es intencional: permite compilar, testear la generación de XML de forma aislada, y mergear sin riesgo de romper producción).

**Archivos esperados:**
- `ERP.Application/Modules/ElectronicDocuments/XmlBuilders/CreditNoteXmlBuilder.cs`
- `ERP.Application/Modules/ElectronicDocuments/DataProviders/CreditNoteDataProvider.cs` (o `IElectronicDocumentDataProvider` para `CreditNote`, resolviendo `SalesReturn` + factura original)
- Registro DI aditivo (`AddSingleton<IElectronicDocumentXmlBuilder, CreditNoteXmlBuilder>()`, etc.)
- Wiring en `AuthorizeSalesReturnHandler`: captura de secuencial `"04"` vía `CaptureNextAsync` + llamada a `_edocIssuer.RegisterAsync(..., ElectronicDocumentType.CreditNote, "Sales", salesReturn.Id, ct)`.
- `ERP.Application.Tests/ElectronicDocuments/CreditNoteXmlBuilderTests.cs`
- `ERP.Application.Tests/ElectronicDocuments/CreditNoteDataProviderTests.cs`

**Módulos afectados:** ElectronicDocuments (Application — solo clases nuevas, cero líneas modificadas en `ElectronicDocument.cs`, `ElectronicDocumentIssuer.cs`, `RunPipelineAsync`), Sales (Application — una llamada nueva en el handler de autorización).

**Dependencias:** Fase 5 (necesita `SalesReturn.Authorize()` funcionando) + el diseño aprobado de la estructura XML de Nota de Crédito según la Ficha Técnica SRI (referencia obligatoria al comprobante modificado: clave de acceso/autorización/fecha de la factura original).

**Criterios de aceptación:**
- `CreditNoteXmlBuilder.Build(data)` produce un `ElectronicDocumentXml` estructuralmente válido contra `NotaCredito_V1.1.0.xsd` (validable en test aunque el pipeline productivo no lo use todavía, porque el test puede invocar el validador de esquema directamente sin pasar por `activeVersion`).
- `CreditNoteDataProvider` resuelve correctamente: datos del cliente (snapshot de la factura original), líneas devueltas, impuestos heredados (no recalculados), referencia al comprobante modificado (número de autorización + fecha de la factura original).
- `CaptureNextAsync(tid, cid, epId, "04", ct)` asigna número secuencial de Nota de Crédito sin colisionar con la numeración de Factura (`"01"`).
- El wiring en `AuthorizeSalesReturnHandler` llama `RegisterAsync` **después** de que el `SalesReturn` ya está persistido con éxito (mismo orden que `AuthorizeSalesUseCases.cs` hace con la Factura) — si `RegisterAsync` falla, la devolución en sí ya quedó autorizada (mismo comportamiento tolerante a fallos que Sales tiene hoy para Facturas: el documento SRI puede reintentarse vía el job genérico sin re-autorizar la devolución).
- **Explícitamente fuera del criterio de aceptación de esta fase**: que la Nota de Crédito realmente llegue a `Authorized` en el ambiente de pruebas SRI — eso requiere `activeVersion` activo (Fase 11).

**Tests necesarios:**
- `CreditNoteXmlBuilder`: build exitoso, build con datos incompletos → `Result` fallido (no excepción).
- Validación XSD directa (invocando el validador sin pasar por `manifest.json.activeVersion`) contra `NotaCredito_V1.1.0.xsd` con datos reales de un `SalesReturn` de prueba.
- `CreditNoteDataProvider`: mapeo correcto de todos los campos obligatorios de la Ficha Técnica (motivo, referencia a comprobante modificado, líneas, impuestos, totales).
- Test de integración parcial: `AuthorizeSalesReturnCommand` con `manifest.json` **sin activar** (estado real en este punto del plan) debe dejar el `ElectronicDocument` en `Failed` (validación XSD sin versión activa) sin que eso revierta la autorización de la devolución — confirma el desacoplamiento de fallas.

**Riesgos:** Medio. Riesgo de interpretar incorrectamente la Ficha Técnica SRI para Nota de Crédito (campos obligatorios, estructura de referencia al comprobante original) — mitigado exigiendo, como parte del criterio de aceptación, validación XSD real contra el esquema oficial ya embebido (no solo tests unitarios con mocks). Riesgo de que esta fase quede "invisible" en producción hasta la Fase 11 y se olvide activarla — mitigado dejándolo como bloqueo explícito y documentado en el plan de despliegue.

---

## Fase 11 — Gate de gobernanza: activación de `manifest.json` (ADR)

**Objetivo:** Fase de **gobernanza, no de código** — obtener la aprobación arquitectónica formal para modificar el único archivo FROZEN que este plan necesita tocar, y luego aplicar el cambio mínimo (`"CreditNote".activeVersion: null → "1.1.0"`).

**Archivos esperados:**
- ADR formal (fuera del alcance de este documento — se referencia, no se redacta aquí, según instrucción explícita de no crear ADR).
- Cambio de una sola línea en `ERP.Infrastructure/ElectronicDocuments/Resources/SRI/manifest.json`.

**Módulos afectados:** ElectronicDocuments (configuración FROZEN).

**Dependencias:** Fase 10 completa y ya probada de forma aislada (para que la activación sea un cambio de "encender interruptor", no de "desarrollar mientras se activa").

**Criterios de aceptación:**
- ADR aprobado por el responsable de arquitectura, con la justificación ya documentada en `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md` §9 como insumo.
- Tras el cambio, el pipeline de emisión de Nota de Crédito pasa de fallar en `XmlGenerated`/validación XSD a completar el flujo hasta `Signed`/`Sent`.
- Sin ningún otro cambio en `manifest.json` más allá de esa línea (no se toca la entrada de `"Invoice"` ni de otros tipos de documento).

**Tests necesarios:**
- Re-ejecución del test de integración parcial de la Fase 10 ("dejar `ElectronicDocument` en Failed") — ahora debe pasar a `XmlGenerated`/`Signed` en vez de `Failed`.
- Prueba contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con un caso real de Nota de Crédito, siguiendo el mismo protocolo que se usó para cerrar ADR-023 con Factura (incluye idealmente un caso de rechazo real confirmado, como se hizo con Factura).

**Riesgos:** Bajo técnico, alto de proceso/tiempos — depende de aprobación externa al equipo de desarrollo. Mitigación: al estar aislada como última fase antes de RIDE/Frontend, no bloquea el avance del resto del plan mientras se gestiona la aprobación.

---

## Fase 12 — Ride: plantilla de Nota de Crédito

**Objetivo:** Implementar el RIDE (representación impresa) de Nota de Crédito — incluido en el MVP por decisión 3.

**Archivos esperados:**
- `ERP.Application/Modules/Ride/Templates/CreditNoteRideTemplate.cs` (`IRideTemplate`, `DocumentType => RideDocumentType.CreditNote`)
- `ERP.Application.Tests/Ride/CreditNoteRideTemplateTests.cs`

**Módulos afectados:** Ride (Application — clase nueva, `RideTemplateResolver` ya soporta el registro sin cambios).

**Dependencias:** Fase 10 (necesita el `ElectronicDocumentData`/mapeo de Nota de Crédito ya definido) — **no depende de la Fase 11** (puede desarrollarse y testearse en paralelo con el trámite de gobernanza, usando datos de prueba, ya que el RIDE se genera a partir del mismo `ElectronicDocumentData`, no del estado de autorización SRI).

**Criterios de aceptación:**
- Plantilla renderiza: datos del emisor, datos del cliente, referencia a la factura original (número + fecha + autorización), líneas devueltas, impuestos, totales, motivo, código QR/clave de acceso (si aplica, igual que Factura).
- `RideTemplateResolver.Resolve(RideDocumentType.CreditNote)` deja de devolver `null` para este tipo.
- Reutiliza el mismo mecanismo de generación de PDF que `DefaultInvoiceRideTemplate` (sin duplicar infraestructura de renderizado).

**Tests necesarios:**
- Render exitoso con datos completos.
- Render con datos límite (línea con ICE, línea sin ICE, motivo largo).

**Riesgos:** Bajo. Aditivo puro, sin tocar `RideTemplateResolver` ni el contrato `IRideTemplate`.

---

## Fase 13 — Frontend: flujo de creación y autorización de devolución

**Objetivo:** UI para crear el Draft, seleccionar líneas/cantidades, motivo, y **la asignación explícita de reembolso** (decisión 2 — el operador elige efectivo/crédito, sin cálculo automático).

**Archivos esperados:**
- `frontend/src/modules/sales/api/salesReturnService.ts`
- `frontend/src/modules/sales/pages/SalesReturnPage.tsx` (o modal desde el detalle de factura)
- `frontend/src/modules/sales/schemas/salesReturnSchema.ts` (Zod)
- Botón "Devolución" en la vista de detalle de Factura (visible solo si `Authorized` y hay remanente)

**Módulos afectados:** Frontend (Sales).

**Dependencias:** Fase 9 (API) completa. No depende de Fases 10-12 para poder desarrollarse (puede construirse contra los endpoints ya funcionales, aunque la Nota de Crédito aún no esté activa — el estado del documento electrónico simplemente se mostrará como pendiente/fallido hasta que la Fase 11 cierre).

**Criterios de aceptación (Architecture Gate del CLAUDE.md, F-V1..F-V8):**
- React Hook Form + Zod, errores 422 mapeados exclusivamente con `applyServerErrors<T>()`.
- UI de selección de líneas usa `GetReturnableLinesByInvoiceQuery` para mostrar remanente en tiempo real.
- UI de asignación de reembolso: inputs explícitos para monto en efectivo / monto a crédito de CxC, con validación Zod de que la suma == total devuelto (espejo de la validación de dominio) — el sistema **no** sugiere/calcula un prorrateo, el operador ingresa los montos.
- Mensajes visuales vía `message`/`MSG` (`lib/messages`), sin condicionales manuales de error.
- Montos con `ZhDecimalInput`, formateo con `formatMoney()`.

**Tests necesarios:**
- Prueba manual guiada (dev server) del golden path: factura autorizada → devolución parcial contado → devolución parcial crédito → devolución mixta.
- Verificación de que los valores ingresados se conservan ante un error 422.
- Verificación de que la suma de asignaciones se valida en cliente antes de enviar (feedback inmediato) y en servidor (fuente de verdad).

**Riesgos:** Bajo-medio. Riesgo de UX: si el operador no entiende que debe asignar manualmente el reembolso (sin ayuda de cálculo automático), puede generar fricción operativa — mitigar con un cálculo de referencia visible en la UI ("total a reembolsar: $X") aunque la asignación siga siendo manual, sin autocompletar los campos.

---

## Fase 14 — Frontend: seguimiento de Nota de Crédito + RIDE

**Objetivo:** Mostrar el estado de la Nota de Crédito (reutilizando el patrón ya existente del Monitor de Documentos Electrónicos) y el enlace de descarga del RIDE una vez autorizada.

**Archivos esperados:**
- Extensión del componente/hook de Monitor de Documentos Electrónicos existente para soportar `CreditNote` además de `Invoice` (si el componente ya es genérico por tipo, puede no requerir cambios; si está acoplado a Factura, se generaliza aquí — a verificar en el momento de implementación).
- Enlace de descarga de RIDE en la pantalla de detalle de la devolución.

**Módulos afectados:** Frontend (Sales / ElectronicDocuments Monitor).

**Dependencias:** Fase 11 (Nota de Crédito activa) + Fase 12 (RIDE) + Fase 13 (pantalla base de devolución).

**Criterios de aceptación:**
- Estados visibles: `Draft → XmlGenerated → Signed → Sent → Received → Authorized/Rejected`, igual que Factura.
- RIDE descargable solo cuando `Authorized`.
- Reutilización confirmada del componente de Monitor existente (sin duplicar lógica de polling de estado).

**Tests necesarios:**
- Prueba manual guiada contra el ambiente de Pruebas del SRI (dev server), reproduciendo al menos un caso de autorización exitosa.

**Riesgos:** Bajo. Última fase, depende de que todas las anteriores estén cerradas; sin riesgo técnico nuevo, solo de integración visual.

---

## Fase 15 — Integración E2E completa y regresión final

**Objetivo:** Validar los 15 escenarios E2E del diseño aprobado (§14) de punta a punta, contra PostgreSQL real, y confirmar cero regresión sobre Sales/Caja/Accounting/ElectronicDocuments existentes.

**Archivos esperados:**
- `ERP.API.Tests/Integration/SalesReturnEndToEndTests.cs` (o carpeta equivalente, siguiendo el patrón de `CajaVentasEndToEndTests.cs`)

**Módulos afectados:** Todos los tocados por las Fases 1-14 (validación cruzada, sin código de producción nuevo más allá de los tests).

**Dependencias:** Fases 1-14 completas.

**Criterios de aceptación:**
- Los 15 escenarios de `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md` §14 pasan.
- Suite completa de regresión (`ERP.Domain.Tests`, `ERP.Application.Tests`, `ERP.Infrastructure.Tests`, `ERP.API.Tests`, `ERP.Architecture.Tests`) en verde, incluyendo explícitamente los tests ya existentes de `SalesInvoice`, `CancelSalesInvoiceHandler` (aunque no tenga tests hoy, se documenta que sigue sin cambios de comportamiento), `SalesReceivable`, `SalesInvoiceAuthorizedHandler`, `SalesInvoiceAuthorizedPostingTranslator`.
- Gates de arquitectura CI (`SEQ-GATE-01..04`, `ATT-GATE-01`) siguen en verde sin cambios.

**Tests necesarios:** los 15 escenarios + regresión completa (detallado arriba).

**Riesgos:** Bajo si cada fase anterior cerró sus propios criterios de aceptación de forma aislada — esta fase es principalmente de confirmación, no de descubrimiento de problemas nuevos grandes.

---

## 2. Tabla resumen de dependencias entre fases

| Fase | Depende de | Puede correr en paralelo con |
|---|---|---|
| 1 — Dominio SalesReturn | — | 2 |
| 2 — Dominio SalesReceivable | — | 1 |
| 3 — Infraestructura | 1 | — |
| 4 — Application Draft/consultas | 1, 3 | — |
| 5 — Application Authorize + Inventario | 1, 3, 4 | — |
| 6 — Caja/CxC | 2, 5 | — |
| 7 — Accounting | 5 | 6, 8 |
| 8 — Audit | 1, 5 | 6, 7 |
| 9 — API | 4, 5, 6 | — |
| 10 — ElectronicDocuments (sin activar) | 5 | 7, 8 |
| 11 — Gate ADR + activación | 10 | — (bloqueante externo) |
| 12 — Ride | 10 | 11 (no depende de 11) |
| 13 — Frontend devolución | 9 | 10, 11, 12 |
| 14 — Frontend NC + RIDE | 11, 12, 13 | — |
| 15 — E2E + regresión final | 1-14 | — |

## 3. Nota final

Este plan no altera en ningún punto la arquitectura ya aprobada en `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md`; únicamente la secuencia, la precisa donde el diseño dejaba una decisión de negocio abierta (asignación explícita de reembolso, ya resuelta por la decisión 2), y aísla el único punto de contacto con infraestructura FROZEN (Fase 11) para que no bloquee el resto del trabajo. **Actualización de cierre (2026-07-31):** las 15 fases de este plan fueron ejecutadas — ver "Estado de cierre" al inicio del documento y `docs/STATUS.md`. El resto de esta nota se conserva sin cambios como constancia de la intención original del plan.

---

## 4. Backlog técnico no bloqueante (registrado al cierre, 2026-07-31)

Hallazgos detectados durante la auditoría de hardening posterior al cierre funcional de P0-01 (revisión exclusiva de deuda técnica, sin cambios de comportamiento ni refactors arquitectónicos). Ninguno bloquea el cierre de P0-01 — se registran aquí para una fase futura explícita, no para acción inmediata:

| # | Ítem | Motivo por el que no se corrigió en el cierre |
|---|---|---|
| 1 | `SalesReturnFormPage.tsx` (Draft) no usa React Hook Form + Zod — usa `useState` manual pese a que `salesReturnDraftSchema` ya existe en `schemas/salesReturnSchema.ts` (hoy solo consumido por su propio test) | Corregirlo exige recablear el motor de formulario — refactor arquitectónico, fuera del alcance de un cierre documental/de hardening |
| 2 | `SalesReturnCreditNoteSection.tsx` usa `formatApiError`; el resto del módulo usa `formatApiRequestError` | Firmas y comportamiento distintos entre ambos utilitarios (labels offline/generic, manejo de 401, fuente del mensaje) — unificar cambiaría el texto de error mostrado en casos borde, riesgo de cambio de comportamiento |
| 3 | `GetReturnableLines` vive en `SalesReturnController` con ruta absoluta fuera de su propio prefijo (`api/v1/sales/invoices/.../returnable-lines`) — resource-ownership ambiguo entre `SalesController`/`SalesReturnController` | Corregirlo implica mover/renombrar un endpoint — cambio de contrato público, fuera de alcance |
| 4 | Fixture `BuildAuthorizedInvoice` (construcción de `SalesInvoice` autorizada de prueba) repetida en 5 archivos de `ERP.Application.Tests/Sales` | Riesgo de regresión en 5 archivos de test por beneficio cosmético; solo 2 de 5 ocurrencias superan el umbral estricto de duplicación idéntica |
| 5 | `SalesReturnConfiguration.cs` reutiliza `SalesInvoice.InvoiceNumberMaxLen` para la columna `CreditNoteDocumentNumber` en vez de una constante propia de `SalesReturn` | Mismo formato SRI por diseño (ambos documentos comparten formato de numeración); sin evidencia de que deban divergir, tocarlo sin necesidad real roza el refactor gratuito |

Ninguno de estos 5 ítems es un defecto funcional, de seguridad ni de integridad de datos — son observaciones de consistencia interna detectadas por revisión de código, documentadas para que una fase futura decida si vale la pena atenderlas.
