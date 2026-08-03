# PurchaseReturn + SupplierCredit — Diseño Definitivo (P0-02, Fase 2)

## 1. Estado y autoridad del documento

| Campo | Valor |
|---|---|
| Estado | `APPROVED` |
| Aprobación | `DESIGN_APPROVED: YES` — aprobado documentalmente en la segunda revisión ARB, tras corregir `PLAN-REV-01`/`PLAN-REV-06` y el hallazgo residual `P0-02-ARB2-01`; la aprobación documental no autoriza por sí sola la implementación |
| Fase | P0-02 — Fase 2 |
| Fecha | 2026-07-31 |
| Reemplaza | Versión anterior de `P0-02_PURCHASE_RETURN_DESIGN.md` (informe de diseño preliminar, con decisiones pendientes) |
| Documentos de evidencia utilizados | `P0-02_PURCHASE_RETURN_DESIGN.md` (versión previa, informe de diseño inicial); `P0-02_PURCHASE_RETURN_AUDIT.md` (auditoría técnica, AUDIT_CLOSED); `P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md` (cierre de evidencias, AUDIT_CLOSED) |

**Nota explícita**: este documento es el diseño técnico definitivo de `PurchaseReturn + SupplierCredit`. **Todavía no está autorizado para implementación.** No contiene código, migraciones, entidades, handlers, DTOs, controladores, frontend ni pruebas reales. Su propósito exclusivo es servir de base a un futuro plan de implementación por fases, que deberá crearse como documento independiente y aprobarse por separado.

**Nota de enmienda (Architecture Review Board, corrección documental controlada)**: esta versión corrige `PLAN-REV-01` (BLOCKER) del veredicto ARB sobre el plan de implementación derivado de este diseño — la versión previa omitía `BranchId` como propiedad obligatoria de `PurchaseReturn` y `SupplierCredit`, violando la Branch Ownership Rule (`AI-RULES/CORE-ARCHITECTURE.md`), regla de Nivel 1/2 superior a este diseño. Se corrige en §5, §5.2 (nueva), §6, §7.1, §7.4, §20 y §24. La aprobación previa de este diseño (implícita en el plan que lo daba por `APPROVED`) no autorizaba conservar una contradicción con una regla arquitectónica superior — ver §5.2. El estado de este documento pasó de `DRAFT_FOR_REVIEW` a `AMENDED_DRAFT_FOR_REVIEW` y, tras la segunda revisión ARB que confirmó la corrección completa de `PLAN-REV-01` a `PLAN-REV-06` y del hallazgo residual `P0-02-ARB2-01`, a `APPROVED` (`DESIGN_APPROVED: YES`) — la aprobación documental no autoriza por sí sola la implementación (`IMPLEMENTATION_AUTHORIZED` permanece en `NO` en el plan hasta una decisión explícita posterior).

---

## 2. Alcance y exclusiones

### 2.1 Límite funcional

| Concepto | ¿Qué es? | ¿Atómico con la autorización? |
|---|---|---|
| Devolución comercial (decisión de negocio de devolver) | Acto de negocio que origina el documento `PurchaseReturn` en `Draft` | Precede a la autorización — no atómico |
| Salida física de inventario | Movimiento `StockMovement` tipo `PurchaseReturn`, cantidad negativa | **Atómico** con la autorización |
| Ajuste financiero de CxP | Reducción de `PurchasePayable.BalanceDue` vía nuevo componente `ReturnAppliedAmount` | **Atómico** con la autorización |
| Creación del crédito (`SupplierCredit`) | Si el valor reconocido excede el saldo pendiente aplicable | **Atómico** con la autorización (misma transacción, mismo commit) |
| Asiento contable de la devolución | `PostingFact` nuevo, `FactType="PurchaseReturn"` | **Atómico** con la autorización (el dispatcher de eventos de dominio ya es síncrono dentro de la misma unidad de trabajo — infraestructura FROZEN) |
| Auditoría de la autorización | `PurchaseReturnAudit` | **Atómico** con la autorización (mismo mecanismo FROZEN ADR-022) |
| Aplicación del crédito a otra CxP | `SupplierCreditMovement` tipo `Application` + ajuste de otro `PurchasePayable` | **Proceso independiente posterior**, su propia transacción/lock/idempotencia |
| Reembolso recibido del proveedor | `SupplierCreditMovement` tipo `Refund` | **Proceso independiente posterior**, su propia transacción/lock/idempotencia |
| Registro de la Nota de Crédito del proveedor | Vínculo `PurchaseReturn ↔ PurchaseReceptionDocument` (tipo `CreditNote`) | **Proceso independiente**, documental puro — por decisión de negocio (§3, cerrada) no puede volver a tocar inventario/CxP/crédito/contabilidad |
| Cancelación/reversa de cualquiera de los anteriores | Operación dedicada por tipo de efecto, cada una transaccional y auditada | Cada una es **atómica en sí misma**, nunca combinada con otra operación de negocio |

**Regla de diseño**: solo lo que ocurre **dentro de la autorización de `PurchaseReturn`** (inventario + CxP + crédito + contabilidad + auditoría) es una única unidad transaccional. Todo lo demás (aplicar crédito, reembolsar, registrar NC, cancelar) es un proceso propio, con su propia frontera transaccional, su propio lock y su propio mecanismo de idempotencia — nunca se combinan dos de estas operaciones en una sola transacción.

### 2.2 Exclusiones explícitas de v1 (decisiones de negocio ya cerradas, no se repiten aquí como preguntas)

Lotes/series, Nota de Débito, validación en línea contra el SRI — ver §3.

---

## 3. Decisiones de negocio cerradas

Estas decisiones provienen de `P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md` y del encargo de esta fase. No se vuelven a cuestionar.

1. Devolución parcial o total soportada.
2. Cantidad devuelta nunca supera cantidad comprada menos devoluciones autorizadas no canceladas.
3. Costo de reversión de inventario = `PurchaseInvoiceDetail.LandedUnitCost` (mismo que usa `CancelPurchaseUseCases`), nunca costo promedio corriente del kardex.
4. `PurchasePayable.BalanceDue` es la única fuente del saldo pendiente — nunca `PurchasePayable.Status` (que solo transiciona `pending→cancelled`).
5. Devolución ≤ saldo pendiente → reduce la CxP en el valor reconocido completo.
6. Devolución > saldo pendiente → la CxP se reduce hasta cero y el excedente crea `SupplierCredit`.
7. Factura totalmente pagada (`BalanceDue = 0`) → el valor completo reconocido de la devolución crea `SupplierCredit`.
8. El crédito puede aplicarse posteriormente a otra CxP del mismo proveedor.
9. El crédito puede cerrarse mediante el registro auditado de un reembolso recibido.
10. No se puede aplicar un crédito a una CxP de un proveedor distinto.
11. La devolución física puede autorizarse antes de recibir la Nota de Crédito del proveedor.
12. Mientras no exista Nota de Crédito registrada, el estado fiscal de la devolución es `PendingSupplierCreditNote`, explícito y consultable.
13. La Nota de Crédito la emite el proveedor; nuestra empresa únicamente la registra como receptor — nunca se emite un documento SRI propio para esto.
14. Registrar la Nota de Crédito después de la autorización **no** vuelve a afectar inventario, CxP, crédito ni contabilidad operativa.
15. Una retención (`IssuedWithholding.Status == Issued`) bloquea la autorización de la devolución con un error explícito.
16. No hay ajuste proporcional automático de la retención en esta versión — el bloqueo es total, no parcial.
17. Lotes/series quedan fuera de P0-02 (no hay infraestructura de origen en Compras).
18. Nota de Débito queda fuera de P0-02 (sin caso de negocio evidenciado).
19. Validación en línea contra el servicio del SRI queda fuera de v1 (backlog no bloqueante).
20. v1 valida estructura, relaciones y duplicidad de la Nota de Crédito recibida — no su autenticidad ante el SRI.
21. No hay eliminación física de documentos ni movimientos — soft delete/anulación siempre (regla general del proyecto).
22. No se permite modificar una devolución ya autorizada — cualquier corrección pasa por el flujo auditado de cancelación/reversa.

**Terminología obligatoria**: nunca "saldo a favor de proveedor". Siempre "crédito a favor de nuestra empresa frente al proveedor". Nombre técnico: `SupplierCredit`.

---

## 4. Principios SSOT y confiabilidad

### 4.1 SSOT — mapa completo de fuentes de verdad

| Concepto | Fuente de verdad | Tipo | Notas |
|---|---|---|---|
| Cantidad comprada por línea | `PurchaseInvoiceDetail.Quantity` | Almacenado, inmutable tras `Confirm()` | — |
| Cantidad ya devuelta por línea | `SUM(PurchaseReturnDetail.Quantity)` agrupado por `OriginalInvoiceDetailId`, filtrando `PurchaseReturn.Status == Authorized` | **Derivado**, calculado on-demand | Nunca almacenado como contador mutable en `PurchaseInvoiceDetail` (decisión explícita, §10) |
| Saldo pendiente de la CxP | `PurchasePayable.BalanceDue` (fórmula extendida, §12) | Derivado de campos almacenados en el mismo agregado | Nunca `Status` |
| Costo histórico de reversión | `PurchaseInvoiceDetail.LandedUnitCost` (congelado) | Almacenado, inmutable | Nunca `RunningAverageCost` del kardex |
| Valor reconocido de la devolución (financiero) | Prorateo de `PurchaseInvoiceDetail` (`UnitPrice`, `DiscountAmount`, `VatRate`, `IceRate`) por cantidad devuelta, calculado una única vez en `Authorize()` | Calculado en el momento de autorizar, **snapshot almacenado** en `PurchaseReturnDetail` tras el cálculo | Snapshot legítimo — nunca se recalcula después |
| Crédito disponible | Fórmula completa con signo por tipo de movimiento — ver §13.5 (bloqueante 8): `AvailableAmount = OriginalAmount − ΣApplication + ΣReversalOfApplication − ΣRefund + ΣReversalOfRefund − ΣSourceReturnCancelled` | Autoritativo = suma con signo de **todos** los movimientos (no hay movimientos "inactivos" — cada uno es un hecho definitivo, nunca editable/eliminable); **cacheado** en `SupplierCredit.AvailableAmount` recalculado atómicamente en cada transacción que inserta un movimiento (ver §13.3 mecanismo de sincronización) | Nunca editable manualmente |
| Crédito aplicado a una CxP específica | `SupplierCreditMovement` tipo `Application` (monto, `TargetPurchasePayableId`) | Almacenado, inmutable tras creación | Reversa = nuevo movimiento tipo `ReversalOfApplication`, nunca edición del original |
| Saldo del crédito reducido por reembolso | `SupplierCreditMovement` tipo `Refund` (monto con signo, participa en `AvailableAmount`) | Almacenado, inmutable tras creación | **Corrección residual 11** — `SupplierCreditMovement` es la fuente de verdad exclusivamente del *saldo*, nunca del "reembolso recibido" como hecho financiero completo; reversa de saldo = nuevo movimiento tipo `ReversalOfRefund` |
| Hecho financiero del reembolso/reversa (destino usado, método, referencia, fecha efectiva, efecto de caja) | `SupplierCreditRefundTransaction` (`TransactionTypeCode = REFUND_RECEIVED`/`REFUND_REVERSED`) | Almacenado, append-only, 1:1 con el `SupplierCreditMovement` que lo origina vía `SupplierCreditRefundTransaction.SupplierCreditMovementId` (§6.4, §10) | **Corrección residual 11** — nunca `SupplierCreditMovement`, que no porta destino/método/referencia |
| Cuenta contable utilizada por el reembolso/reversa | `SupplierCreditRefundTransaction.AccountingAccountId` (congelado al confirmar, §6.4bis) | Almacenado, inmutable tras creación | Nunca el `AccountingAccountId` mutable actual de `CompanyFinancialDestination` |
| Destino financiero del reembolso | `FinancialDestinationId` (referencia) + `CompanyFinancialDestination` (catálogo maestro persistido, §6.4) | Almacenado | Campos estructurales inmutables tras creación (§6.4ter); `AccountingAccountId`/`Name`/`IsActive` son los únicos editables |
| Estado económico del reembolso (activo/revertido) | Existencia o no de una fila `SupplierCreditRefundTransaction(REFUND_REVERSED)` con `OriginalTransactionId` apuntando al `REFUND_RECEIVED` en cuestión | Derivado, calculado on-demand | Nunca un campo `Status` editable duplicado (§6.4) |
| Estado fiscal de la devolución | `PurchaseReturn.FiscalStatus` | Almacenado, mutado únicamente por la operación de vinculación de NC | — |
| Datos de la Nota de Crédito recibida | `PurchaseReceptionDocument` (tipo `CreditNote`) | Almacenado en la entidad ya existente — `PurchaseReturn` solo guarda el vínculo (`SupplierCreditNoteDocumentId`), nunca copia número/clave de acceso | Evita segunda fuente fiscal duplicada |
| Asiento contable | `PostingFact` → `PostingRule` (config) → motor | Derivado de eventos de dominio | Infraestructura FROZEN, sin cambios |
| Estado operativo de la devolución | `PurchaseReturn.Status` (`Draft/Authorized/Cancelled`) | Almacenado | Máquina de estados cerrada del propio agregado |

### 4.2 Duplicaciones inevitables — declaración explícita

| Dato duplicado | Dato autoritativo | Dato derivado/proyección | Razón de la duplicación | Transacción que mantiene consistencia | Mecanismo de detección de desincronización |
|---|---|---|---|---|---|
| `SupplierCredit.AvailableAmount` | Suma con signo de **todos** los `SupplierCreditMovement` (fórmula completa §13.5 — ningún movimiento es "inactivo": la reversión es siempre un movimiento nuevo, nunca un cambio de estado del original) | Columna cacheada en `SupplierCredit` | Evitar recalcular sumatoria de movimientos en cada lectura (listados, validación bajo lock) | La misma transacción que inserta un `SupplierCreditMovement` recalcula y persiste `AvailableAmount` en el mismo `SaveChanges` (agregado único, límite de consistencia estándar EF) | Prueba de integración obligatoria (§16, prerrequisito de implementación) que recalcula `AvailableAmount` desde movimientos y lo compara contra la columna cacheada en cada escenario de la matriz (§23); adicionalmente, cualquier lectura administrativa de detalle de `SupplierCredit` expone ambos valores (cacheado y recalculado) para que una discrepancia sea visualmente detectable sin job adicional |
| `PurchasePayable.ReturnAppliedAmount` / `SupplierCreditAppliedAmount` | Conjunto de `PurchaseReturn.Authorized` (no cancelados) que aplicaron valor a esa CxP + conjunto de `SupplierCreditMovement.Application` (no revertidos) dirigidos a esa CxP | Dos columnas acumuladoras en `PurchasePayable` | Mismo patrón ya existente en `PurchasePayable` para `PaidAmount`/`TotalRetained` — consistencia con el diseño actual del agregado, evita rehacer `BalanceDue` como consulta agregada costosa en cada validación bajo lock | Cada mutación de estas columnas ocurre en la misma transacción que crea/revierte el documento origen (`PurchaseReturn.Authorize()`/`Cancel()`, `SupplierCredit.ApplyToPayable()`/reversa) | Query de reconciliación (no job nuevo): `SUM` de aplicaciones activas contra `PurchaseReturn`/`SupplierCreditMovement` comparado contra las columnas — incluida como prueba de integración obligatoria (§16) y como consulta de soporte disponible para auditoría manual |

No existe ninguna otra duplicación en este diseño. Todo lo demás se deriva on-demand o se referencia por `Guid`, nunca se copia.

### 4.3 Confiabilidad — regla general

Toda operación descrita en este documento (§9, §16, §22) termina en uno de dos resultados: **todos sus efectos se confirman** (inventario + CxP + crédito + contabilidad + auditoría, según corresponda a esa operación) **o ninguno se confirma**. Los estados legítimamente pendientes (`FiscalStatus.PendingSupplierCreditNote`) son explícitos, consultables por API y auditables — nunca un efecto "a medias" oculto.

---

## 5. Modelo de dominio definitivo

```
PurchaseInvoice (existente, FROZEN por su propio ciclo de vida)
        │  (referenciado por Guid, sin FK de navegación — mismo patrón que SalesReturn → SalesInvoiceId)
        ▼
PurchaseReturn (nuevo agregado, dominio Purchases)
  ├─ BranchId (Guid, NOT NULL)           (Branch Ownership Rule — obligatorio, inmutable tras CreateDraft, nunca recibido del cliente — ver §5.2)
  ├─ PurchaseReturnDetail[]              (líneas devueltas, snapshot financiero + snapshot de costo congelados en Authorize())
  ├─ ReturnNumber (string?)              (asignado en Authorize(), no en CreateDraft — ver §7.1bis, §16.1)
  ├─ FiscalStatus                        (NotApplicable [Draft] / PendingSupplierCreditNote / SupplierCreditNoteRegistered)
  ├─ SupplierCreditNoteDocumentId (Guid?)→ PurchaseReceptionDocument (existente, tipo CreditNote) — vínculo, no copia
  ├─ HistoricalCostTotal / CostVarianceTotal (snapshot, solo tras Authorize()) — ver §19.1bis (bloqueante 7)
  ├─ CreateClientRequestId (Guid, NOT NULL) / AuthorizeClientRequestId / CancelClientRequestId / LinkCreditNoteClientRequestId (Guid?, obligatorios como input de su operación respectiva — ver §7.1) — ver §16.2
  └─ Status                              (Draft → Authorized → Cancelled)

PurchasePayable (existente, EXTENDIDO — no reemplazado)
  ├─ + ReturnAppliedAmount               (nuevo componente acumulador)
  ├─ + SupplierCreditAppliedAmount       (nuevo componente acumulador)
  ├─ + xmin (RowVersion)                 (nuevo — resuelve hallazgo #1)
  └─ BalanceDue = TotalAmount − PaidAmount − TotalRetained − ReturnAppliedAmount − SupplierCreditAppliedAmount

SupplierCredit (nuevo agregado, dominio Purchases — o Finance, ver §6.3)
  ├─ BranchId (Guid, NOT NULL)           (Branch Ownership Rule — obligatorio, heredado inmutablemente de PurchaseReturn.BranchId al originarse, nunca una decisión financiera independiente del cliente — ver §5.2)
  ├─ SupplierCreditMovement[]            (colección única: Application / Refund / ReversalOfApplication / ReversalOfRefund / SourceReturnCancelled — sin referencia hacia la transacción financiera, ver §6.4/§10)
  │    └─ (relación inversa 1:1 vía SupplierCreditRefundTransaction.SupplierCreditMovementId — nunca una FK en sentido contrario, evita dependencia circular)
  ├─ AvailableAmount                     (cacheado, recalculado transaccionalmente — fórmula completa §4.2/§13.5)
  ├─ (sin SourceType — eliminado, ver §6.1; SourcePurchaseReturnId es la única referencia de origen)
  └─ xmin (RowVersion)

CompanyFinancialDestination (nuevo, catálogo maestro persistido, dominio Finance — ver §6.4)
  ├─ DestinationTypeCode (catálogo persistido: BANK_ACCOUNT / CASH_REGISTER)
  ├─ AccountingAccountId → Account (existente, FK real — activa, postable, mismo tenant/company)
  ├─ CashRegisterId (Guid?, solo BANK_ACCOUNT=null/CASH_REGISTER=obligatorio) → CashRegister (existente)
  ├─ BankInstitutionCode / BankAccountIdentifierNormalized (solo BANK_ACCOUNT)
  └─ xmin (RowVersion)

SupplierCreditRefundTransaction (nuevo, entidad hija de SupplierCredit vía SupplierCreditMovement, dominio Finance — ver §6.4)
  ├─ SupplierCreditMovementId (única FK autoritativa hacia el movimiento — relación 1:1, ver §10)
  ├─ TransactionTypeCode (catálogo persistido: REFUND_RECEIVED / REFUND_REVERSED)
  ├─ OriginalTransactionId (Guid?, solo REFUND_REVERSED — reversa append-only, ver §12)
  ├─ FinancialDestinationId → CompanyFinancialDestination (obligatoria)
  ├─ AccountingAccountId → Account (obligatoria — congelada al confirmar; REFUND_RECEIVED la copia del destino, REFUND_REVERSED la hereda del REFUND_RECEIVED original, ver §6.4bis — corrección residual 9)
  ├─ AccountingAccountCodeSnapshot (presentación histórica, congelado igual que AccountingAccountId — ver §6.4bis)
  ├─ PaymentMethodCode (catálogo real PaymentMethod — método, distinto de destino, ver §8)
  ├─ CashSessionId / CashMovementId (Guid?, solo si el destino resuelto es CASH_REGISTER)
  └─ xmin (RowVersion)

StockMovement (existente, EXTENDIDO)
  └─ + SourceDocLineId (Guid?, genérico) — referencia inequívoca a la línea del documento origen

PurchaseReceptionDocument (existente, EXTENDIDO — ver §18.1bis, bloqueante 4)
  ├─ + CurrencyCode (string, nuevo)      — necesario para validar moneda de la NC vs. la devolución (no existía)
  └─ SourceDocType = CreditNote — reutilizado para la NC recibida del proveedor

PurchaseReturnSequence (nuevo, mínimo, dominio Purchases — ver §7.1bis/§16.1, bloqueante 10)
  ├─ (TenantId, CompanyId, CurrentSeq)
  └─ Captura mediante pg_advisory_xact_lock DENTRO de la misma transacción ambiente de Authorize() (nunca transacción propia, corrige la analogía con DocumentSequence — ver §7.1bis), sin EmissionPointId ni DocTypeCode — no es infraestructura SRI
```

No se propone ningún cambio al Posting Engine, a `DocumentSequence` (se documenta explícitamente por qué **no** se reutiliza para `ReturnNumber` — ver §7.1bis), a `ElectronicDocuments`/RIDE, ni a la infraestructura de Entity Audit — todo consumo es vía interfaces ya FROZEN.

### 5.1 Invariantes cruzadas de agregados (resuelve bloqueante 5)

Estos 9 casos cubren toda interacción posible entre `PurchaseReturn`, `PurchasePayable`, `SupplierCredit` y la cancelación de la factura de origen. Todos se revalidan bajo los locks de §15 — nunca se asumen desde una lectura previa al lock.

| # | Caso | ¿Se permite? | Precondiciones | Locks | Revalidación tras el lock | Reversas exigidas antes | Error de negocio | Datos que no cambian si se bloquea |
|---|---|---|---|---|---|---|---|---|
| 1 | Cancelar una `PurchaseInvoice` con una `PurchaseReturn` en `Authorized` asociada | **Bloqueado** | — | Lock A (`PurchaseInvoiceId`) — ya adquirido por `CancelPurchaseUseCases` extendido (§15.2) | Existencia de `PurchaseReturn.Status == Authorized` para esa factura, bajo el mismo lock que serializa con `AuthorizePurchaseReturnUseCases`/`CancelPurchaseReturnUseCases` | Cancelar primero la(s) `PurchaseReturn` asociada(s) por su propio flujo auditado | `PI-CANC-01` (nuevo, catálogo de `CancelPurchaseUseCases`) | Factura, CxP, inventario, devolución — nada cambia |
| 2 | Cancelar una `PurchaseInvoice` cuya CxP recibió una aplicación de `SupplierCredit` (`SupplierCreditAppliedAmount > 0`) | **Bloqueado** | — | Lock A | `PurchasePayable.SupplierCreditAppliedAmount > 0` bajo lock | Revertir primero la(s) aplicación(es) de crédito vía `ReverseSupplierCreditApplicationUseCases` | `PI-CANC-02` (nuevo, catálogo de `CancelPurchaseUseCases`) | Factura, CxP, crédito — nada cambia |
| 3 | Pagar (`RegisterPayment`) una `PurchasePayable` con `Status == cancelled` | **Bloqueado** (ya bloqueado hoy) | — | Lock A | Guard de dominio ya existente (`Status != cancelled`), ahora además protegido por el lock (§12.2, §15.5) — sin cambio de comportamiento, solo se cierra la ventana de carrera | — | Código de negocio ya existente de `RegisterPaymentCommandHandler` (sin cambio) | CxP, pago |
| 4 | Aplicar `SupplierCredit` sobre una `PurchasePayable` con `Status == cancelled` | **Bloqueado** | — | Lock A (destino) + Lock B | `PurchasePayable` destino no `cancelled`, bajo lock | — | `SC-002` (ya existe) | Crédito, CxP destino |
| 5 | Revertir una aplicación de crédito después de que la `PurchasePayable` destino fue cancelada | **Bloqueado** | El movimiento `Application` original existe y no está revertido | Lock A (destino) + Lock B | `PurchasePayable.Status != cancelled` en el destino, bajo lock — una CxP cancelada no puede recibir el ajuste inverso (`SupplierCreditAppliedAmount -=`) porque su `BalanceDue` ya no es un concepto operable | Ninguna reversa previa posible — es un callejón sin salida documentado: la reversa de esa aplicación específica queda permanentemente bloqueada mientras la CxP destino esté cancelada | `SC-014` (nuevo) | Crédito (`AvailableAmount` sin cambio), CxP destino |
| 6 | Cancelar una `PurchaseReturn` `Authorized` cuyo crédito fue aplicado | **Bloqueado** | `SupplierCredit.AvailableAmount < OriginalAmount` | Lock A + Lock B | `AvailableAmount == OriginalAmount` bajo lock (regla mínima ya existente, confirmada) | Revertir primero la(s) aplicación(es) | `PR-011` (ya existe — confirmado que cubre este caso) | Inventario, CxP, crédito — nada se revierte |
| 7 | Cancelar una `PurchaseReturn` `Authorized` cuyo crédito fue reembolsado | **Bloqueado** | `SupplierCredit.AvailableAmount < OriginalAmount` | Lock A + Lock B | Igual que el caso 6 — la fórmula de `AvailableAmount` (§13.5) ya incluye `Refund` con signo negativo, por lo que la misma condición `AvailableAmount == OriginalAmount` detecta también reembolsos sin necesitar una segunda comprobación | Revertir primero el/los reembolso(s) | `PR-011` (mismo código — un reembolso también reduce `AvailableAmount`, no es un caso distinto) | Inventario, CxP, crédito — nada se revierte |
| 8 | Cancelar una `PurchaseReturn` `Authorized` después de registrar la NC (`FiscalStatus == SupplierCreditNoteRegistered`) | **Permitido** (decisión resuelta con evidencia existente, no es una decisión de negocio nueva) | Crédito íntegro (si existe), igual que el caso 6/7 | Lock A (+ Lock B si hay crédito) | Igual que `Cancel` estándar (§9.1) — el registro de NC no agrega ninguna precondición nueva porque, por decisión ya cerrada (§3.14), vincular la NC nunca tuvo efecto financiero/inventario que revertir | Las mismas de un `Cancel` estándar | `PR-011` solo si aplica por crédito, nunca por el estado fiscal | `FiscalStatus` permanece `SupplierCreditNoteRegistered` congelado (histórico, no se desvincula — corregir el documento real del proveedor es un proceso externo al ERP, fuera de alcance) |
| 9 | Cancelación concurrente de la `PurchaseInvoice` y de su `PurchaseReturn` `Authorized` | Serializado, nunca simultáneo | — | Ambas compiten por el mismo Lock A `(TenantId, PurchaseInvoiceId)` | La que adquiere el lock primero procede; la segunda, al adquirir el lock, recarga el estado ya actualizado por la primera y revalida (caso 1 si intenta cancelar la factura primero habiendo ganado la devolución, o directamente falla si la devolución ya fue cancelada) | Ninguna — el lock impide que ambas se ejecuten sobre estado obsoleto | `PI-CANC-01` o resultado exitoso, nunca ambas exitosas de forma inconsistente | Determinista según orden de adquisición del lock — nunca un estado mixto |

**Regla mínima confirmada** (ya existía parcialmente como `PR-011`): una `PurchaseReturn` `Authorized` no puede cancelarse mientras el `SupplierCredit` originado tenga aplicaciones o reembolsos activos (`AvailableAmount < OriginalAmount`) — deben revertirse explícita y auditadamente primero. No se permiten aplicaciones/reembolsos/movimientos huérfanos en ningún caso: los casos 1, 2, 4 y 5 de esta tabla cierran las rutas por las que podría generarse un huérfano desde el lado de la factura/CxP, y los casos 6/7 desde el lado de la propia devolución.

### 5.2 Branch Ownership Rule aplicada a `PurchaseReturn` y `SupplierCredit` (corrige `PLAN-REV-01`, BLOCKER — sin excepción arquitectónica)

**Contexto de la corrección**: la Architecture Review Board (ARB) detectó que la versión previa de este diseño omitía `BranchId` en los dos aggregate roots nuevos (`PurchaseReturn`, `SupplierCredit`), pese a que ambos cumplen las tres preguntas de decisión de la Branch Ownership Rule (`AI-RULES/CORE-ARCHITECTURE.md`): pertenecen a un tenant, pertenecen a una empresa, y su operación ocurre dentro de una sucursal (el borrador de una devolución se crea desde la sesión operativa de una sucursal concreta, igual que `PurchaseInvoice`). El defecto estaba en el diseño, no solo en el plan derivado — se corrige aquí, en la fuente, antes de propagarlo.

**Precedente verificado contra código real**: `PurchaseInvoice.BranchId` (`ERP.Domain/Modules/Purchases/Entities/PurchaseInvoice.cs`, propiedad `Guid BranchId { get; private set; }`, asignada una única vez dentro de `CreateDraft(...)`) se resuelve en el handler de creación del borrador desde `ICurrentBranch.BranchId` (contexto backend de sesión — ver `ERP.Application/Modules/Purchases/UseCases/PurchaseDraftUseCases.cs`, inyección de `ICurrentBranch _b` y uso de `_b.BranchId` como argumento posicional del factory `CreateDraft(...)`), nunca recibido como campo del comando/DTO de entrada. `PurchaseReturn` y `SupplierCredit` siguen exactamente el mismo patrón — no se inventa un mecanismo nuevo.

**Reglas expresas de este diseño (obligatorias, sin excepción)**:

1. `PurchaseReturn.BranchId` es una propiedad obligatoria (`Guid`, no nullable) del agregado, asignada una única vez dentro de `PurchaseReturn.CreateDraft(...)` a partir de `ICurrentBranch.BranchId` del contexto backend del handler de creación (mismo patrón que `PurchaseDraftUseCases.cs`) — **nunca** recibida como propiedad del `CreateDraftCommand`/DTO de entrada, y **nunca** modificable después de creado el `Draft` (sin setter público, sin método `ChangeBranch`/`SetBranch`, igual que exige `AI-RULES/CORE-ARCHITECTURE.md`).
2. `SupplierCredit.BranchId` es una propiedad obligatoria (`Guid`, no nullable), asignada una única vez dentro de `SupplierCredit.CreateFromReturn(...)` (invocado desde `PurchaseReturn.Authorize()`, §11) copiando literalmente `PurchaseReturn.BranchId` — **nunca** recibida como una decisión financiera independiente del cliente ni de ningún comando de `SupplierCredit`.
3. **Invariante permanente**: `SupplierCredit.BranchId == PurchaseReturn.BranchId` (el `PurchaseReturn` referenciado por `SupplierCredit.SourcePurchaseReturnId`) se cumple siempre, por construcción — no existe ninguna ruta de código que pueda crear un `SupplierCredit` con un `BranchId` distinto al de su origen, porque el valor nunca se recibe de una fuente independiente.
4. Ninguna operación posterior (`ApplyToPayable`, `ReverseApplication`, `RegisterRefund`, `ReverseRefund`, `Authorize`, `Cancel`, vínculo de NC) puede sustituir `BranchId` por la sucursal activa del operador en tiempo de ejecución — `BranchId` es siempre el valor persistido en el agregado cargado por el repositorio, nunca un valor tomado de `ICurrentBranch` fuera del momento de creación. `RegisterRefund`/`ReverseRefund` operan siempre bajo el `BranchId` ya persistido en el `SupplierCredit` cargado bajo Lock B (§15) — jamás bajo la sucursal activa de quien ejecuta el reembolso.
5. La persistencia de `BranchId` es, consistente con `AI-RULES/CORE-ARCHITECTURE.md`, para trazabilidad histórica, auditoría y reportes — el control de acceso por sucursal sigue siendo responsabilidad exclusiva de `BranchScopeBehavior`/`IBranchAccessGuard`/`IInterBranchAccessGuard` (infraestructura ya existente, sin cambios), aplicados a los endpoints de `PurchaseReturnController`/`SupplierCreditController` igual que a cualquier otro controlador de un agregado operativo.
6. **No se crea ninguna nueva invariante numerada de §5.1 ni ningún nuevo código de negocio de §21 para esta regla**: la pertenencia de sucursal no es una condición de negocio que el usuario pueda violar desde la API (no se recibe del cliente, §5.2.1/§5.2.2) ni una carrera nueva entre locks (§5.2.4) — es un invariante de construcción, cerrado por diseño, exactamente igual que el resto de agregados que ya siguen este patrón (`PurchaseInvoice`, `StockMovement`, `CashSession`). Por tanto §5.1 conserva sus 9 casos y §21 conserva sus 44 códigos reales (§21, corrección `PLAN-REV-02`) sin adición.
7. **No existe ninguna excepción a la Branch Ownership Rule para `PurchaseReturn` ni para `SupplierCredit`.** No se crea ninguna ADR de excepción, porque no se autoriza ninguna desviación — ambos agregados cumplen la regla igual que cualquier otro aggregate root operativo del ERP.
8. Esta corrección **no reinterpreta** la semántica aprobada de los locks A/B (§15): Lock A sigue precediendo siempre a Lock B cuando ambos son necesarios (§15.4), y `ReverseRefund` conserva exactamente la secuencia ya aprobada (Lock B por `SupplierCreditId` → `FOR SHARE` sobre `REFUND_RECEIVED` original → comprobación de `REFUND_REVERSED` previo → herencia de datos financieros desde la transacción original, §16.1). `BranchId` no participa como componente de la clave de ningún lock (los namespaces y claves de §15.1 permanecen `(TenantId, PurchaseInvoiceId)`/`(TenantId, SupplierCreditId)`, sin cambio).

**Propagación mínima obligatoria** (detallada en el resto del documento — ver referencias): modelo de dominio (§5, arriba), campos/tabla de `PurchaseReturn` (§7.1), campos/tabla de `SupplierCredit` (§7.4), justificación de entidades (§6), auditoría (§20.1), permisos (§20.2, vía infraestructura ya existente), catálogo de cambios por capa (§24 — persistencia/configuración EF, índices, repositorio, DTOs/contratos HTTP).

---

## 6. Justificación de entidades y tablas

| Elemento | Identidad propia | Ciclo de vida propio | Invariantes propios | Por qué no puede representarse reutilizando una entidad existente |
|---|---|---|---|---|
| `PurchaseReturn` (AggregateRoot nuevo) | Sí — documento de negocio propio con número, motivo, fecha, `BranchId` obligatorio (Branch Ownership Rule, §5.2) | Sí — `Draft → Authorized → Cancelled` | Sí — cantidad devuelta ≤ remanente, líneas inmutables tras `Authorize()`, `BranchId` inmutable tras `CreateDraft()` (§5.2) | `PurchaseInvoice` es FROZEN por su propio ciclo de vida (§CLAUDE.md ElectronicDocuments/Purchases); mezclar devoluciones dentro de la factura violaría su congelamiento de costos y su máquina de 3 estados ya cerrada |
| `PurchaseReturnDetail` (entidad hija) | Sí — referencia inequívoca a una línea de factura específica | Ligado al de `PurchaseReturn` (mismo límite de agregado) | Sí — cantidad > 0, no excede remanente de su línea origen | Es información nueva (snapshot financiero prorateado) que no existe en `PurchaseInvoiceDetail`; agregarla ahí violaría el congelamiento (`IsFrozen`) ya cerrado de la factura |
| `SupplierCredit` (AggregateRoot nuevo) | Sí — un crédito identificable por proveedor, con monto propio, `BranchId` obligatorio heredado de `PurchaseReturn` (Branch Ownership Rule, §5.2) | Sí — abierto mientras `AvailableAmount > 0`, cerrado cuando llega a 0 | Sí — monto original inmutable, `AvailableAmount` nunca negativo, nunca editable fuera de sus movimientos; `BranchId` inmutable, siempre igual al de su `PurchaseReturn` origen (§5.2) | No existe ninguna entidad hoy que represente dinero que el proveedor nos debe (confirmado por búsqueda exhaustiva en la auditoría, y reconfirmado en esta revisión contra el código real de `ERP.Domain/Modules/Finance` y `ERP.Domain/Modules/Caja`); `PaymentApplicationLine` está bloqueada por el `CHECK chk_payment_application_line_document_xor` y por `PaymentDirection` binario — reutilizarla exigiría migrar su esquema para un concepto ajeno a su diseño actual (pagos/cobros), lo cual es una alteración de una infraestructura ya en producción, no una extensión limpia. Ver también §6.4 (bloqueante 6) sobre por qué `Payment`/`CashMovement` tampoco sirven para el reembolso |
| `SupplierCreditMovement` (entidad hija) | Sí — cada aplicación/reembolso/reversa es un hecho de negocio propio, auditable individualmente | Ligado al de `SupplierCredit` | Sí — monto > 0, exactamente un tipo de destino según `MovementType`, reversión referenciada una sola vez | Igual razón que arriba: no es una `PaymentApplicationLine` (CHECK xor lo impide sin migración ajena a P0-02); una única colección evita crear dos tablas casi idénticas para aplicación y reembolso — ver §13.2. Es, además, la **única** estructura capaz de representar el evento financiero de reembolso — ver §6.4 |
| Extensión de `PurchasePayable` (`ReturnAppliedAmount`, `SupplierCreditAppliedAmount`, `xmin`) | No es entidad nueva — son columnas nuevas en el agregado existente | — | Se integran a los invariantes ya existentes de `BalanceDue` | Es la extensión correcta: `PurchasePayable` ya es la fuente de verdad de `BalanceDue`; crear una entidad separada de "saldo pendiente" sería la tabla resumen redundante que el mandato prohíbe explícitamente |
| Extensión de `StockMovement` (`SourceDocLineId`) | No es entidad nueva — columna genérica nueva | — | Nullable, no rompe flujos existentes | Es genérica (no específica de Compras) porque `StockMovement` ya es un componente compartido por todos los módulos que mueven inventario; nombrar la columna de forma específica de Compras violaría la naturaleza genérica del kardex |
| `PurchaseReturnAudit` (Entity Audit) | Sí — sigue ADR-022, un registro por transición relevante | Ligado a eventos de `PurchaseReturn` | — | Patrón ya FROZEN (ADR-022) — reutilizar `UserActivity` está expresamente prohibido por CLAUDE.md para auditoría de negocio con valores tipados |
| `SupplierCreditAudit` (Entity Audit) | Sí — auditoría de creación/aplicación/reversa/reembolso del crédito | Ligado a eventos de `SupplierCredit` | — | Mismo razonamiento — `SupplierCredit` es un agregado nuevo con su propio ciclo de vida, requiere su propia entidad de auditoría por Regla 1 (Open/Closed) de la infraestructura de Auditoría CLOSED |

**No se propone ninguna tabla de encabezado/resumen adicional.** No hay tabla "PurchaseReturnSummary", no hay tabla "SupplierCreditBalance" separada de `SupplierCredit` — el saldo vive en el propio agregado, no en una proyección de otra tabla.

### 6.1 Sobre `SourceType` de `SupplierCredit` — ELIMINADO (resuelve bloqueante 9)

**Corrección de diseño explícita**: no existe campo `SourceType` ni enum `SupplierCreditSourceType`. Verificado contra `P0-02_PURCHASE_RETURN_AUDIT.md`/`P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md` y contra el alcance cerrado de P0-02 (§3): `PurchaseReturn` es el **único** origen evidenciado de crédito frente a proveedores — no hay "crédito por bonificación comercial", "crédito por nota de crédito comercial directa" ni ningún otro generador dentro del alcance de esta fase. Un enum de un único valor útil no aporta información: `SourcePurchaseReturnId` (obligatorio, único por `(TenantId, SourcePurchaseReturnId)`) ya es, por sí mismo, una referencia inequívoca y autoexplicativa del origen — agregar un `SourceType` redundante sería la "abstracción sin consumidor real" que el mandato prohíbe explícitamente (un `switch`/`if` sobre un enum de un solo caso posible nunca se ejecuta con una segunda rama).

Si en el futuro aparece un segundo generador de crédito (p. ej. una bonificación comercial no ligada a una devolución), ese es el momento de introducir `SourceType` — con evidencia real de un segundo caso, no antes. Mientras tanto, `SupplierCredit.SourcePurchaseReturnId` es la única y total fuente de verdad del origen (ver también §13.4).

### 6.2 Cardinalidad `PurchaseReturn ↔ SupplierCredit`

Un `PurchaseReturn` genera **como máximo un** `SupplierCredit` (0 o 1), únicamente si el excedente calculado en `Authorize()` es mayor a cero (§11). `SupplierCredit.SourcePurchaseReturnId` es único (`UNIQUE (TenantId, SourcePurchaseReturnId) WHERE SourcePurchaseReturnId IS NOT NULL`). No se crea un `SupplierCredit` con monto cero.

### 6.3 Ubicación de módulo de `SupplierCredit`

`SupplierCredit` vive en `ERP.Domain/Modules/Purchases/Entities/` (junto a `PurchaseReturn`, su único origen), pero sus casos de uso de aplicación/reembolso (`ApplySupplierCreditUseCases`, `RegisterSupplierCreditRefundUseCases`) viven en `ERP.Application/Modules/Finance/UseCases/` — mismo criterio ya vigente en el repositorio de que CxP/Pagos son un concern de Finance aunque el documento origen sea de Compras (evidenciado: `AccountsPayablePage.tsx` vive en `frontend/src/modules/finance/`, no en `modules/purchases`). El frontend de aplicación/reembolso de crédito se ubica en `frontend/src/modules/finance/`, coherente con dónde ya vive la gestión de CxP.

### 6.4 Integración financiera real del reembolso (resuelve bloqueante 6 — corrección final del destino financiero)

**Investigación realizada contra el código real** (solo lectura — `ERP.Domain/Modules/Accounting/`, `ERP.Domain/Modules/Finance/`, `ERP.Domain/Modules/Caja/`, `ERP.Domain/Modules/Sales/Entities/PaymentMethod.cs`; no existe ningún módulo `BankAccount`/`CashAndBanks` en el repositorio):

| Componente existente evaluado | ¿Sirve como destino financiero persistido del reembolso? | Evidencia concreta del código |
|---|---|---|
| `Account` (`ERP.Domain/Modules/Accounting/Entities/Account.cs`) | **Sí, como cuenta contable del destino** — no como destino en sí. `Account : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity`, `CompanyId`-scoped (ADR-026), con `Code` (`AccountCode`), `Name`, `ParentAccountId`, `AccountType`, `Nature`, **`AllowsPosting`** (bool — la propiedad real que expresa "postable", no un campo a inventar) e `IsActive` (bool). Ya se referencia como FK real en `JournalEntryLine`/`PostingRule`/`PostingRuleLine`. No tiene moneda propia. Es la entidad correcta para `AccountingAccountId` |
| `Payment` (`ERP.Domain/Modules/Finance/Entities/Payment.cs`) | **No.** Ya descartado — `EnsureBalanced()` exige `Σ PaymentApplicationLine.AppliedAmount == Amount` contra un `SalesReceivable`/`PurchasePayable` real; un reembolso de `SupplierCredit` no tiene documento AR/AP que liquidar (§2.1) |
| `CashRegister`/`CashSession`/`CashMovement` (`ERP.Domain/Modules/Caja/Entities/`) | **`CashRegister` sí, como destino tipo caja; `CashSession`/`CashMovement` no como identidad del destino, sí como efecto obligatorio de la integración.** `CashRegister : MasterEntity` tiene `CompanyId`, `BranchId`, `Code`, `Name` — es el destino físico real cuando el reembolso ingresa a caja. `CashMovement` exige `CashSessionId` no vacío (`ArgumentException` si `Guid.Empty` en su factory) y una `CashSession` abierta — por eso el destino persistido es `CashRegisterId` (identidad estable, no cambia), mientras que `CashSessionId`/`CashMovementId` son el efecto transaccional de cada reembolso concreto (§13), nunca la identidad del destino |
| `PaymentMethod` (`ERP.Domain/Modules/Sales/Entities/PaymentMethod.cs`) | **Sí, como método — nunca como destino.** Catálogo real (`Code`, `Name`, `RequiresReference`, `IsCreditAllowed`, `SortOrder`, `DetailType`, `IsActive` heredado de `MasterEntity`), ya usado como FK real en `Payment.PaymentMethodId`/`SalesInvoicePayment.PaymentMethodId`. Responde "cómo llegó el dinero" (transferencia, cheque, efectivo), nunca "dónde ingresó" — ver §8 |
| Cuenta bancaria / `BankAccount` / catálogo de instituciones financieras | **No existe.** Búsqueda exhaustiva en `ERP.Domain/Modules/*`: no hay entidad `BankAccount` ni catálogo de bancos. Único rastro es el campo de texto libre `BankName` en `PaymentTransferDetail` (`Modules/Sales/Entities/`), sin FK a catálogo — confirma que no puede usarse como fuente de verdad de nada |
| `Currency` (tabla/catálogo) | **No existe.** `CurrencyCode` es `string(3)` normalizado (`PurchaseInvoice`, `SalesInvoice`, `Company`), sin tabla `Currency` — el destino financiero hereda el mismo criterio: `CurrencyCode` como `string(3)`, nunca una FK a un catálogo que no existe |

**Decisión de diseño final**: ninguna infraestructura existente representa, por sí sola, un destino financiero persistido, normalizado y auditable de la empresa — pero **sí existen** los dos componentes reales que ese destino debe referenciar (`Account` para la cuenta contable, `CashRegister` para la caja). No se crea un módulo general de Tesorería/Bancos (fuera de alcance de P0-02). Se diseña el **componente financiero mínimo exigido**: una entidad maestra nueva, `CompanyFinancialDestination`, que es el catálogo persistido de destinos reales y controlados de la empresa — y una entidad de hecho financiero nueva, `SupplierCreditRefundTransaction` (reemplaza `SupplierCreditRefundReceipt` de la versión previa de este documento), que almacena tanto el ingreso original como su reversa compensatoria. Esta corrección elimina por completo `ExternalPaymentChannel`/`FinancialDestinationType` de tipo "canal externo conceptual" y `FinancialDestinationReference` como sustituto de identidad — el destino ahora es siempre una fila real de `CompanyFinancialDestination`, nunca un texto ni un enum de un solo valor sin cuenta contable asociada.

#### `CompanyFinancialDestination` (entidad maestra nueva, catálogo persistido — ubicación: `ERP.Domain/Modules/Finance/Entities/`, mismo criterio de §6.3 que ya ubica CxP/Pagos en Finance)

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado | No | PK | Identidad persistida, usada como FK desde la transacción del reembolso |
| `TenantId` / `CompanyId` | Guid | Sí | Contexto de sesión | No | Branch Ownership Rule | — |
| `Code` | string | Sí | Ingresado (configuración de empresa) | **No, inmutable tras creación** (corrección 2, §6.4ter) | Único `(TenantId, CompanyId, Code)` | Código estable apto para reportes — nunca el nombre; cambiar el código exige crear un nuevo destino |
| `Name` | string | Sí | Ingresado | Sí (editable) | No vacío | Solo presentación — no gobierna lógica ni reportes autoritativos |
| `DestinationTypeCode` | string, catálogo persistido cerrado (`BANK_ACCOUNT`/`CASH_REGISTER`) | Sí | Ingresado | No, inmutable tras creación | Uno de los 2 valores del catálogo | Clasificación estructural del destino — nunca texto libre ni comparación dispersa |
| `AccountingAccountId` | Guid | Sí | Ingresado (selección de `Account` existente) | Sí (editable — corrección 2: solo afecta operaciones **nuevas**, nunca transacciones ya confirmadas, §6.4bis) | FK a `Account`; `Account.IsActive = true`; `Account.AllowsPosting = true`; `Account.CompanyId = CompanyFinancialDestination.CompanyId` (exigido al momento de uso, no retroactivo sobre historial) | Determina la cuenta contable real del destino para reembolsos nuevos — nunca "Banco/Caja" genérico ni cuenta recibida manualmente en el comando de reembolso; cada reembolso ya confirmado congela su propia cuenta en `SupplierCreditRefundTransaction.AccountingAccountId` (§6.4bis) |
| `CurrencyCode` | string(3) | Sí | Ingresado | **No, inmutable tras creación** (corrección 2, §6.4ter) | Formato válido, normalizado (`Trim().ToUpperInvariant()`, mismo criterio que `PurchaseInvoice.CurrencyCode`) | Moneda admitida por este destino — sin tabla `Currency` en el ERP (confirmado); cambiar la moneda exige crear un nuevo destino |
| `CashRegisterId` | Guid? | Solo si `DestinationTypeCode = CASH_REGISTER` | Ingresado (selección de `CashRegister` existente) | **No, inmutable tras creación** (corrección 2, §6.4ter) | FK a `CashRegister`; `CashRegister.CompanyId = CompanyFinancialDestination.CompanyId` | Identidad estable de la caja destino — nunca `CashSessionId`/`CashMovementId`, que son efectos por transacción; cambiar la caja exige crear un nuevo destino |
| `BankInstitutionCode` | string, código estructurado | Solo si `DestinationTypeCode = BANK_ACCOUNT` | Ingresado | **No, inmutable tras creación** (corrección 2, §6.4ter) | No vacío cuando aplica | Código de institución bancaria — estructurado, nunca el texto libre `BankName` de `PaymentTransferDetail`; cambiar la institución exige crear un nuevo destino |
| `BankAccountIdentifierNormalized` | string, normalizado | Solo si `DestinationTypeCode = BANK_ACCOUNT` | Ingresado, normalizado por el servidor (trim, sin separadores) | **No, inmutable tras creación** (corrección 2, §6.4ter) | No vacío cuando aplica | Identificador de cuenta bancaria comparable/único — nunca comprobante o referencia de un pago puntual; cambiar el identificador exige crear un nuevo destino |
| `IsActive` | bool | Sí, default `true` | Sistema/ingresado | Sí (editable) | — | Permite deshabilitar sin eliminación física (regla general del proyecto) — nunca afecta transacciones ya confirmadas |
| `CreatedAtUtc`/`CreatedByUserId`/`UpdatedAtUtc`/`UpdatedByUserId` | DateTime/Guid | Sí (creación); update solo tras editar | Sistema | No (creación) / Sistema (update) | — | Auditoría mínima embebida — adicional a `CompanyFinancialDestinationAudit` (§20) |
| `RowVersion` (xmin) | uint (shadow) | Sí | EF/PostgreSQL | Sistema | — | Concurrencia — mismo patrón que el resto de agregados nuevos |

**Catálogo `DestinationTypeCode`**: exactamente 2 valores en v1, `BANK_ACCOUNT` y `CASH_REGISTER`, persistidos (no un enum C# libre sin backing de catálogo — se modela igual que el resto de catálogos cerrados de este diseño, `SupplierCreditMovementType`/`PurchaseReturnFiscalStatus`: un valor fijo verificado por `CHECK`, no una tabla editable por el tenant, porque los dos tipos son estructurales del propio ERP, no configuración de negocio). No se agregan tipos especulativos.

**Reglas condicionales** (`CHECK` combinado por `DestinationTypeCode`):

```
DestinationTypeCode = BANK_ACCOUNT
    ⇒ BankInstitutionCode IS NOT NULL
      AND BankAccountIdentifierNormalized IS NOT NULL
      AND CashRegisterId IS NULL

DestinationTypeCode = CASH_REGISTER
    ⇒ CashRegisterId IS NOT NULL
      AND BankInstitutionCode IS NULL
      AND BankAccountIdentifierNormalized IS NULL

Siempre (ambos tipos):
    AccountingAccountId IS NOT NULL
    AND CurrencyCode IS NOT NULL
```

**Corrección (residual 1)**: `IsActive = true` **no** forma parte del `CHECK` estructural — `IsActive=false` debe ser un valor persistible en cualquier momento, precisamente para poder deshabilitar un destino (§6.4ter) sin violar ninguna restricción de fila. La actividad no es un invariante estructural de la entidad, es una condición de **uso** validada solo en el momento de una operación nueva:

```
RegisterRefund (operación nueva):
    CompanyFinancialDestination.IsActive debe ser true,
    verificado bajo el bloqueo de fila (FOR SHARE, §6.4quater) — SC-021 si no.

ReverseRefund (reversa histórica):
    no exige CompanyFinancialDestination.IsActive=true — la reversa
    no vuelve a leer el destino con fines de validación de actividad
    (§6.4quinquies), usa el AccountingAccountId ya congelado.
```

**Restricciones e índices**:

```
UNIQUE (TenantId, CompanyId, Code)

UNIQUE (TenantId, CompanyId, BankInstitutionCode, BankAccountIdentifierNormalized)
    WHERE DestinationTypeCode = 'BANK_ACCOUNT'

UNIQUE (TenantId, CompanyId, CashRegisterId)
    WHERE DestinationTypeCode = 'CASH_REGISTER'

FK + índice: TenantId, CompanyId, AccountingAccountId, CashRegisterId
```

Ningún nombre interno de índice se expone en errores de aplicación — mismo criterio que el resto del catálogo de errores (§21).

**Ubicación de módulo**: `CompanyFinancialDestination` vive en `ERP.Domain/Modules/Finance/Entities/` — es configuración de empresa consumida por Finance (reembolsos de crédito) y, potencialmente, por cualquier futuro proceso financiero que necesite un destino real (fuera de alcance mencionar cuáles); no vive en Purchases porque no tiene relación de dominio con devoluciones de compra, solo es consumida por ellas a través del reembolso.

#### `SupplierCreditRefundTransaction` (reemplaza `SupplierCreditRefundReceipt` — entidad hija nueva, 1:1 con `SupplierCreditMovement` de tipo `Refund`/`ReversalOfRefund`, almacena tanto el ingreso original como su reversa compensatoria)

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado | No | PK — identidad propia del hecho financiero | — |
| `TenantId` / `CompanyId` | Guid | Sí | Contexto de sesión | No | Branch Ownership Rule | — |
| `SupplierId` | Guid | Sí | Snapshot de `SupplierCredit.SupplierId` | No | — | Permite reportar/filtrar sin join obligatorio |
| `SupplierCreditId` | Guid | Sí | FK | No | — | Trazabilidad directa al crédito de origen |
| `SupplierCreditMovementId` | Guid | Sí | FK al movimiento `Refund`/`ReversalOfRefund` que lo origina | No | **Única FK autoritativa** — `UNIQUE (TenantId, CompanyId, SupplierCreditMovementId)`, relación 1:1 estricta, nunca 1:N | Resuelve la dependencia circular detectada: `SupplierCreditMovement` **no** tiene `RefundReceiptId`/`RefundTransactionId` — la navegación inversa se obtiene consultando esta FK (§10) |
| `TransactionTypeCode` | string, catálogo persistido cerrado (`REFUND_RECEIVED`/`REFUND_REVERSED`) | Sí | Sistema, según operación | No | Uno de los 2 valores | Distingue el ingreso original de su reversa dentro de la misma tabla append-only |
| `OriginalTransactionId` | Guid? | Solo si `TransactionTypeCode = REFUND_REVERSED` | Sistema (referencia a la transacción `REFUND_RECEIVED` que revierte) | No | `UNIQUE (TenantId, CompanyId, OriginalTransactionId) WHERE TransactionTypeCode = 'REFUND_REVERSED'` — una sola reversa por ingreso | Reversa append-only — nunca edición del original (§12) |
| `FinancialDestinationId` | Guid | Sí | Ingresado (`RegisterRefund`) / heredado (`ReverseRefund`) | No | FK a `CompanyFinancialDestination`; mismo tenant/company | **Identificador del destino real** — reemplaza `FinancialDestinationType`/`ExternalPaymentChannel` |
| `AccountingAccountId` (nuevo — Corrección 1) | Guid | Sí | `REFUND_RECEIVED`: copiado de `CompanyFinancialDestination.AccountingAccountId` validado y congelado al confirmar (§13.6). `REFUND_REVERSED`: copiado del `AccountingAccountId` de la transacción `REFUND_RECEIVED` original, nunca de `CompanyFinancialDestination` vigente | No, inmutable tras creación — **fuente autoritativa de la cuenta contable realmente usada por esta transacción**, congelada de forma independiente al valor mutable actual del destino | FK a `Account`, mismo tenant/company que la transacción (la FK persiste aunque la cuenta se desactive después — no se exige `IsActive`/`AllowsPosting` retroactivamente sobre una fila histórica) | Corrige la ausencia de persistencia histórica: sin esta columna, un cambio posterior de `CompanyFinancialDestination.AccountingAccountId` (§8) alteraría silenciosamente la cuenta que los reportes/reversa atribuyen a un reembolso ya confirmado. `AccountingAccountId` es el campo real de la transacción; `AccountingAccountCodeSnapshot` (más abajo) es únicamente su copia de presentación |
| `PaymentMethodCode` (regla corregida — corrección residual 3) | string, catálogo real `PaymentMethod` | Sí | `REFUND_RECEIVED`: ingresado. `REFUND_REVERSED`: heredado del `REFUND_RECEIVED` original, no re-ingresable | No | `REFUND_RECEIVED`: FK/código válido en `PaymentMethod`, `IsActive = true` bajo `FOR SHARE` en el momento de confirmar (§6.4quater). `REFUND_REVERSED`: hereda el código del original **sin exigir que el método siga activo** — no se revalida `IsActive` sobre un dato histórico | **Cómo llegó el dinero** — distinto del destino, ver §8. No existe una regla general de "`IsActive=true` en todo momento" que aplique a ambos tipos de transacción — solo aplica a la confirmación de una operación nueva |
| `Amount` | decimal `numeric(18,2)` | Sí | Igual a `SupplierCreditMovement.Amount` que lo origina | No | `= SupplierCreditMovement.Amount` | Sin segunda fuente de importe — copia congelada del movimiento autoritativo del saldo |
| `CurrencyCode` | string(3) | Sí | Snapshot de `SupplierCredit.CurrencyCode` | No | Debe coincidir con `CompanyFinancialDestination.CurrencyCode` del destino elegido | Sin campo de moneda independiente |
| `EffectiveDate` | DateOnly | Sí | Ingresado | No | — | Fecha real en que el dinero ingresó/revirtió, puede diferir de `CreatedAtUtc` |
| `ExternalReference` (obligatoriedad corregida — Corrección 5) | string?, normalizada (trim; cadena vacía o solo espacios → `null`) | Condicional: obligatoria si y solo si `PaymentMethod.RequiresReference = true` (catálogo real, §6.4); opcional y nullable si `PaymentMethod.RequiresReference = false`. En `REFUND_REVERSED` **no se re-ingresa** — el campo queda `null` en la fila de reversa (§10.4 del corregido: la evidencia original permanece únicamente en la fila `REFUND_RECEIVED`) | Ingresado (`REFUND_RECEIVED`, solo si aplica) | No | Nunca un valor artificial (`N/A`, `SIN REFERENCIA`, `NO APLICA`, `-`) — ausencia real se representa como `null`, nunca como texto de relleno | **Evidencia complementaria únicamente** (comprobante bancario, número de cheque, referencia de depósito) — nunca identifica el destino, nunca sustituye `FinancialDestinationId`, nunca gobierna validaciones ni reportes autoritativos (§8, §19). La obligatoriedad depende del método real de pago, no es incondicional |
| `Reason` | string | Solo `REFUND_REVERSED` | Ingresado | No | No vacío | Motivo de negocio de la reversa — exigido explícitamente (§12) |
| `CashSessionId` | Guid? | Solo si el destino resuelto es `CASH_REGISTER` | Sistema (sesión activa de la caja del destino) | No | Debe pertenecer a la `CashRegisterId` de `CompanyFinancialDestination` | Efecto real de caja — nunca identidad del destino (§13) |
| `CashMovementId` | Guid? | Solo si el destino resuelto es `CASH_REGISTER` | Sistema (`CashMovement` creado atómicamente) | No | 1:1 con la transacción | Trazabilidad del movimiento de caja real generado |
| `ClientRequestId` | Guid | Sí | Ingresado por cliente HTTP | No | Único `(TenantId, ClientRequestId)` | Idempotencia — mismo mecanismo de §16.2 |
| `PayloadHash` | string (hash determinista) | Sí | Calculado por el servidor | No | — | Huella del payload — mismo mecanismo de §16.2 |
| `FinancialDestinationCodeSnapshot` / `FinancialDestinationNameSnapshot` / `DestinationTypeCodeSnapshot` / `AccountingAccountCodeSnapshot` (regla de reversa corregida — corrección residual 4) | string | Sí, congelados al confirmar | `REFUND_RECEIVED`: snapshot de `CompanyFinancialDestination`/`Account` en el instante de la transacción. `REFUND_REVERSED`: **copiados textualmente de los cuatro snapshots del `REFUND_RECEIVED` original** (`REFUND_REVERSED.AccountingAccountCodeSnapshot = AccountingAccountCodeSnapshot del REFUND_RECEIVED original`, y análogamente para los otros tres) — la reversa **no** vuelve a leer `CompanyFinancialDestination`/`Account` para obtener el código vigente | No | No editables, no gobiernan validaciones | Trazabilidad y presentación histórica aunque el destino cambie de nombre/código después — nunca sustituyen las FK (§9, §19). `AccountingAccountId` (fila anterior) continúa siendo el campo autoritativo en ambos tipos de transacción; estos cuatro snapshots son exclusivamente de presentación, nunca se usan para resolver el asiento contable (§19.1ter) |
| `CreatedByUserId` / `CreatedAtUtc` | Guid / DateTime | Sí | Sistema | No | — | — |
| `RowVersion` (xmin) | uint (shadow) | Sí | EF/PostgreSQL | Sistema | — | Concurrencia — mismo patrón que el resto de agregados nuevos |

**Catálogo `TransactionTypeCode`**: exactamente 2 valores, `REFUND_RECEIVED`/`REFUND_REVERSED` — sin estados o tipos futuros especulativos. El estado económico (`ACTIVE`/`REVERSED`) se deriva de la existencia o no de una fila `REFUND_REVERSED` con `OriginalTransactionId` apuntando a esta transacción — nunca un campo editable duplicado.

**Campos especulativos eliminados de v1**: `ReconciledAtUtc`/`ReconciledByUserId` **no existen** — no hay en P0-02 ningún proceso real de conciliación que los escriba, valide o consuma. Si en el futuro se implementa conciliación real, se agregan como columnas nuevas sobre `SupplierCreditRefundTransaction` en ese momento, con su propio caso de uso.

**Fuente de verdad única, sin desincronización posible**: `SupplierCreditMovement` (tipo `Refund`/`ReversalOfRefund`) es la **única fuente de verdad del saldo del crédito** (`AvailableAmount`, §13.5). `CompanyFinancialDestination` es la **única fuente de verdad del destino real y de la cuenta contable vigente para operaciones nuevas** (§6.4bis). `SupplierCreditRefundTransaction.AccountingAccountId` es la **única fuente de verdad de la cuenta contable efectivamente usada por esa transacción concreta**, congelada al confirmar y nunca resuelta de nuevo contra el destino (§6.4bis). `SupplierCreditRefundTransaction` en general es la **única fuente de verdad del hecho financiero del reembolso y su reversa** (destino usado, cuenta usada, método, referencia, fecha efectiva, efecto de caja). Estas responsabilidades son distintas, pero la operación es atómica: `SupplierCredit.RegisterRefund(...)` construye en memoria tanto el `SupplierCreditMovement` como su `SupplierCreditRefundTransaction` 1:1 (validando el destino ya persistido y congelando su `AccountingAccountId` vigente en ese instante), y ambos se persisten en el mismo `SaveChanges` de la misma transacción (§16.1) — no existe ninguna vía de código que cree uno sin el otro. La reversa (`ReverseRefund`) sigue el mismo patrón: crea `SupplierCreditMovement(ReversalOfRefund)` + su propio `SupplierCreditRefundTransaction(REFUND_REVERSED)`, heredando `AccountingAccountId` del `REFUND_RECEIVED` original — nunca resuelto de nuevo contra `CompanyFinancialDestination` (§6.4bis, §10). Si cualquiera de las escrituras falla, ninguna se persiste (§11).

**Origen único del hecho contable**: el dominio dispara **un único** evento (`SupplierCreditRefundedEvent`/`SupplierCreditRefundReversedEvent`) desde la misma operación atómica — el `PostingFact` de §19.1 se origina desde ese evento único, nunca desde eventos paralelos que pudieran generar el asiento dos veces.

**Sin doble fuente de verdad de tesorería externa**: `SupplierCreditRefundTransaction` es el registro terminal del hecho financiero — no hay una entidad adicional que también lo represente. Si en el futuro el ERP incorpora un módulo real de conciliación bancaria, se agregan columnas nuevas sobre esta misma entidad (no una entidad paralela).

**Validez del destino dentro de tenant/company**: `CompanyFinancialDestination.TenantId`/`CompanyId` se validan contra el `SupplierCredit`/`PurchasePayable` operante — el destino debe pertenecer al mismo tenant/company que el reembolso que lo usa (`SC-020`, §21), y estar `IsActive = true` en el instante de la confirmación bajo lock, **exclusivamente para `RegisterRefund`** (§6.4quater). `PaymentMethodCode` se valida igual contra el catálogo `PaymentMethod` (activo, mismo tenant) — ver `SC-015` (§21) — **también solo para `RegisterRefund`**; `ReverseRefund` hereda ambos sin revalidar actividad (§6.4quinquies).

#### 6.4bis Cuenta contable histórica persistida (corrección final 1)

**Problema corregido**: la versión previa de este documento solo mantenía `AccountingAccountId` como propiedad *mutable* de `CompanyFinancialDestination`, resuelta en el momento del asiento contable (§19.1ter) sin congelarla en la propia transacción financiera. Bajo ese diseño, si el destino cambiaba de cuenta contable después de un reembolso, tanto el reporte histórico como el asiento de una reversa posterior habrían recalculado la cuenta *vigente* en lugar de la cuenta *realmente usada* — una desviación contable inaceptable. Se corrige agregando `SupplierCreditRefundTransaction.AccountingAccountId` (tabla de §6.4) como columna propia, congelada.

**Regla para `REFUND_RECEIVED`**:

```
SupplierCreditRefundTransaction.AccountingAccountId
    = CompanyFinancialDestination.AccountingAccountId
      validado (existe, mismo tenant/company, IsActive=true, AllowsPosting=true,
      compatible con la política monetaria aplicable) y congelado
      en el instante de confirmar el reembolso (§13.6).
```

El comando público de `RegisterRefund` **no** recibe `AccountingAccountId` — se deriva exclusivamente de `FinancialDestinationId` (§13.6).

**Regla para `REFUND_REVERSED`**:

```
SupplierCreditRefundTransaction.AccountingAccountId (de la reversa)
    = AccountingAccountId de la transacción REFUND_RECEIVED original.
```

La reversa **no** vuelve a consultar `CompanyFinancialDestination.AccountingAccountId` vigente, **no** sustituye la cuenta por una configuración posterior, y **no** acepta `AccountingAccountId` desde el comando de reversa (§10 del corregido). Si la cuenta fue desactivada después del ingreso original, la reversa histórica **sigue usando esa misma cuenta** para producir el asiento inverso exacto — no se exige que vuelva a estar activa/postable para poder revertir (la exigencia de `IsActive`/`AllowsPosting` aplica únicamente a operaciones **nuevas**, §6.4quater). La política de "deshabilitar, nunca eliminar" (`Account.IsActive=false`) preserva la FK indefinidamente — nunca se rompe la trazabilidad histórica.

**Snapshot vs. fuente autoritativa**: `AccountingAccountCodeSnapshot` (ya declarado en §6.4) sigue existiendo exclusivamente para presentación/exportación histórica — nunca gobierna contabilidad ni validaciones ni sustituye la FK `AccountingAccountId`. La dimensión estructurada y autoritativa de todo reporte de cuenta usada es siempre `SupplierCreditRefundTransaction.AccountingAccountId`, nunca `AccountingAccountCodeSnapshot` ni el `AccountingAccountId` mutable actual de `CompanyFinancialDestination` (§19.6, corregido).

#### 6.4ter Inmutabilidad de la identidad económica del destino (corrección final 2)

**Campos estructurales inmutables tras la creación de `CompanyFinancialDestination`** (no editables por ningún comando de administración futuro): `TenantId`, `CompanyId`, `Code`, `DestinationTypeCode`, `CurrencyCode`, `CashRegisterId`, `BankInstitutionCode`, `BankAccountIdentifierNormalized`. La tabla de campos de §6.4 se corrige en consecuencia: estos ocho campos pasan de "Sí (administrable)" a **"No, inmutable tras creación"** en la columna Mutable.

**Campos editables** (los únicos tres): `Name` (presentación pura, sin efecto en lógica ni reportes autoritativos), `IsActive` (habilita/deshabilita la selección del destino en operaciones **nuevas** — nunca afecta transacciones ya confirmadas), `AccountingAccountId` (editable únicamente para operaciones futuras — un cambio aquí **no altera ninguna transacción histórica**, porque cada reembolso ya confirmado congeló su propia cuenta en `SupplierCreditRefundTransaction.AccountingAccountId`, §6.4bis).

**Cambio de identidad económica**: representar otra cuenta bancaria, otra institución, otro identificador bancario, otra caja, otra moneda, otro tipo de destino o otro código estable **exige crear un nuevo `CompanyFinancialDestination`** y, cuando corresponda, desactivar (`IsActive=false`) el destino anterior — nunca reutilizar el mismo `FinancialDestinationId` para una identidad económica distinta.

**Regla de edición de un destino con historial (corrección residual 2 — reemplaza la afirmación previa de que `IsActive=false` era la única operación admitida)**:

```
Un destino con historial (referenciado por al menos un
SupplierCreditRefundTransaction.FinancialDestinationId) nunca puede
eliminarse físicamente.

Sobre un destino con historial solo pueden modificarse:
    - Name;
    - IsActive;
    - AccountingAccountId.

Los demás campos (Code, DestinationTypeCode, CurrencyCode,
CashRegisterId, BankInstitutionCode, BankAccountIdentifierNormalized)
permanecen inmutables, con o sin historial — la inmutabilidad de §6.4ter
no depende de si el destino ya fue usado.
```

Esta regla es consistente con la regla general de no eliminación física del proyecto y con la tabla de mutabilidad de §6.4: un destino **sin** historial tiene exactamente la misma restricción de edición que uno **con** historial, porque la inmutabilidad de los ocho campos estructurales (§6.4ter) es una propiedad del diseño del destino, no una consecuencia de tener o no transacciones asociadas.

**Reportes históricos**: usan `FinancialDestinationId` (agrupación), los ocho campos estructurales inmutables del destino (banco/caja/moneda/tipo, estables por diseño) y `SupplierCreditRefundTransaction.AccountingAccountId` (cuenta realmente usada) — nunca reconstruyen la cuenta histórica consultando el `AccountingAccountId` *actual* del destino (§19.6, corregido).

#### 6.4quater Concurrencia real de referencias validadas (corrección final 3)

**Corrección explícita**: se elimina cualquier afirmación de que `Lock B` (§15.1, ámbito `(TenantId, SupplierCreditId)`) protege por sí solo `CompanyFinancialDestination`, `Account` o `CashSession` — `Lock B` serializa exclusivamente las operaciones del crédito identificado por `SupplierCreditId`; no bloquea ninguna otra fila. La revisión previa de §13.6 ("recargar `CompanyFinancialDestination`... y revalidar bajo lock") se corrige: la revalidación por sí sola, sin bloqueo de fila, no impide que la fila cambie entre la lectura y el commit bajo el nivel de aislamiento estándar de PostgreSQL (`READ COMMITTED`).

**Bloqueos obligatorios adicionales dentro de `RegisterRefund`** (misma transacción, no advisory locks nuevos — bloqueos de fila PostgreSQL estándar):

```
Lock B (SupplierCreditId, advisory, §15.1)
+ SELECT ... FOR SHARE sobre CompanyFinancialDestination (por FinancialDestinationId)
+ SELECT ... FOR SHARE sobre Account (por AccountingAccountId del destino)
+ SELECT ... FOR SHARE sobre la CashSession activa resuelta (solo si DestinationTypeCode=CASH_REGISTER)
```

**Orden obligatorio dentro de `RegisterRefund`** (reemplaza el orden de §13.6):

1. Adquirir Lock B (`SupplierCreditId`).
2. Recargar y validar `SupplierCredit` (`AvailableAmount`, §15.5).
3. Cargar y bloquear `CompanyFinancialDestination` (`FOR SHARE`) por `FinancialDestinationId`.
4. Validar bajo el bloqueo: mismo tenant/company, `IsActive=true`, `DestinationTypeCode` estructuralmente completo, moneda compatible (`SC-020`/`SC-021`/`SC-022`/`SC-025`, §21).
5. Cargar y bloquear `Account` (`FOR SHARE`) por `AccountingAccountId` del destino.
6. Validar bajo el bloqueo: mismo tenant/company, `IsActive=true`, `AllowsPosting=true` (`SC-023`/`SC-024`, §21).
7. Validar `PaymentMethod.RequiresReference` (§6.4quinquies) contra `ExternalReference` recibida.
8. Si `DestinationTypeCode=CASH_REGISTER`: resolver la `CashSession` activa compatible y bloquearla (`FOR SHARE`) — sin sesión activa bloqueada, `SC-027` (corrección 6, ver más abajo).
9. Crear `SupplierCreditMovement` + `SupplierCreditRefundTransaction` (congelando `AccountingAccountId`, §6.4bis) + `CashMovement` si aplica.
10. Emitir el único hecho contable (§19.1ter).
11. Persistir auditoría e idempotencia en la misma unidad.
12. `SaveChangesWithSequenceRetryAsync` único → `CommitAsync` único.

**Desactivación concurrente**: si otra transacción intenta `UPDATE` sobre `CompanyFinancialDestination.IsActive` (o sobre `Account`, o `CLOSE` de la `CashSession`) mientras el reembolso ya tiene el `FOR SHARE` adquirido, esa otra transacción espera hasta que el reembolso confirme o revierta — nunca lee un estado intermedio. Si la desactivación confirmó **antes** de que el reembolso adquiriera su propio bloqueo, el reembolso lee el estado ya desactivado y rechaza con `SC-021`/`SC-024`/`SC-027` según corresponda. No hay ningún escenario en el que ambas operaciones confirmen sobre versiones mutuamente inconsistentes. `xmin` en `CompanyFinancialDestination`/`Account`/`CashSession` (donde ya exista, caso de `CashSession`) sigue siendo la defensa correcta para comandos que **actualizan directamente** esas filas (segunda defensa tras el `FOR SHARE`) — pero `xmin` por sí solo, sobre una fila que el reembolso únicamente **lee**, no sustituye el bloqueo de fila explícito, porque una lectura sin `FOR SHARE` no detecta ni previene un `UPDATE` concurrente antes del commit propio.

**Sin locks especulativos nuevos**: no se crea un tercer advisory lock financiero: la solución autorizada es Lock B (existente, §15.1) más bloqueos de fila PostgreSQL estándar (`FOR SHARE`) sobre las referencias concretas leídas — nunca un advisory lock adicional, y `PurchasePayable` permanece sin tocar (el reembolso nunca lo tocó, §2.1).

#### 6.4quinquies Política histórica exacta de reversa (corrección final 4)

**Autorización de reversa histórica**: una reversa válida procede aunque, después del ingreso original, el destino haya sido desactivado, el método de pago haya sido desactivado, la cuenta contable haya sido desactivada, el nombre visible del destino haya cambiado, o `CompanyFinancialDestination.AccountingAccountId` haya cambiado para operaciones futuras (§6.4ter). Ninguna de estas modificaciones posteriores impide ni altera el asiento inverso histórico — la reversa opera exclusivamente sobre los datos ya congelados en el `REFUND_RECEIVED` original.

**Datos heredados obligatorios** (`ReverseRefund` carga el `REFUND_RECEIVED` original y hereda, sin volver a resolverlos): `SupplierId`, `SupplierCreditId`, `FinancialDestinationId`, `AccountingAccountId` (§6.4bis), `PaymentMethodCode`, `Amount`, `CurrencyCode`, `CashRegisterId` (derivado de la identidad económica original cuando el destino era `CASH_REGISTER`). La transacción de reversa registra además, propios de la operación: `TransactionTypeCode=REFUND_REVERSED`, `OriginalTransactionId`, `Reason`, `ClientRequestId` nuevo, `PayloadHash` nuevo, `CreatedByUserId`, `CreatedAtUtc`, y su propia `EffectiveDate` (fecha real de la reversa, distinta y no sobrescribe la `EffectiveDate` del ingreso original — cada fila conserva la suya, §6.4).

**El comando de `ReverseRefund` no recibe** (y por tanto no puede seleccionar ni modificar): `FinancialDestinationId`, `AccountingAccountId`, `PaymentMethodCode`, `Amount`, `CurrencyCode`, `CashRegisterId`, `BankInstitutionCode`, `BankAccountIdentifierNormalized`, `ExternalReference`. El contrato del comando acepta únicamente: `OriginalRefundTransactionId` (o el `ReversalOfMovementId` equivalente ya definido en §16.2), `Reason`, `ClientRequestId`, y la `EffectiveDate` propia de la reversa — sin ampliar el contrato más allá de lo necesario.

**`ExternalReference` en la reversa**: permanece únicamente en la fila `REFUND_RECEIVED` original. La reversa no solicita otra referencia, no sustituye la original, no la usa para identificar destino/cuenta/método, y accede a la evidencia original mediante `OriginalTransactionId`. En la fila `REFUND_REVERSED`, `ExternalReference` queda `null` (§6.4, tabla de campos) — nunca se copia como si fuera nueva evidencia ni se exige un valor artificial.

**Reversa bancaria**: mismo `FinancialDestinationId`, mismo `AccountingAccountId` histórico, mismo importe, misma moneda, mismo método — sin movimientos bancarios ficticios; genera únicamente el movimiento compensatorio de crédito, la transacción financiera de reversa y el hecho contable inverso (§19.1ter).

**Reversa en caja**: usa la misma `CashRegisterId` de la identidad económica original; busca y bloquea (`FOR SHARE`, §6.4quater) una `CashSession` activa compatible de esa misma caja; crea un nuevo `CashMovement` compensatorio (nunca edita ni reutiliza el original); persiste `CashSessionId`/`CashMovementId` propios de la reversa; conserva el vínculo con el ingreso original vía `OriginalTransactionId`. Si no existe sesión activa, **toda la reversa falla con `SC-027`** (corrección 6) — no se persiste ninguna parte de la operación.

**Cuenta desactivada y asiento inverso**: se distingue explícitamente — una operación **nueva** (`RegisterRefund`) exige que la cuenta del destino esté activa y sea postable (§6.4quater); una **reversa histórica** usa obligatoriamente la cuenta ya congelada del ingreso original (§6.4bis) para producir el asiento inverso exacto, sin seleccionar una cuenta alternativa aunque la original esté desactivada.

**Reversa única**: se mantiene `UNIQUE (TenantId, CompanyId, OriginalTransactionId) WHERE TransactionTypeCode='REFUND_REVERSED'` (§6.4) — nunca un segundo ingreso, nunca edición de la transacción original, nunca un estado económico editable duplicado (deriva siempre de la existencia o no de la fila de reversa, §6.4).

---

## 7. Campos, relaciones e índices

### 7.1 `PurchaseReturn`

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado (`Guid.NewGuid()` en `Create()`) | No | PK | Identidad del agregado |
| `TenantId` / `CompanyId` / `BranchId` | Guid | Sí | Contexto de sesión (`ICurrentTenant`/`ICurrentCompany`/`ICurrentBranch`, mismo patrón que `PurchaseDraftUseCases.cs`) | No | Branch Ownership Rule | Obligatorio por `AI-RULES/CORE-ARCHITECTURE.md`; `BranchId` nunca se recibe del cliente ni se modifica tras `CreateDraft()` — ver §5.2 |
| `PurchaseInvoiceId` | Guid | Sí | Ingresado (selección de factura) | No | Debe existir y estar `Confirmed` | Referencia por Guid, sin FK de navegación — mismo patrón `SalesReturn → SalesInvoiceId` |
| `SupplierId` | Guid | Sí | Snapshot de `PurchaseInvoice.SupplierId` al crear el draft | No | Debe coincidir con el proveedor de la factura | Snapshot legítimo — evita join obligatorio para mostrar/validar proveedor |
| `ReturnNumber` | string? | Solo tras `Authorize()` (§7.1bis — bloqueante 10) | Generado por `PurchaseReturnSequence.CaptureNextAsync` (nuevo, atómico, análogo a `IDocumentSequenceRepository.CaptureNextAsync` pero **no** es esa infraestructura — ver §7.1bis) | No, asignado una única vez en `Authorize()` | `NULL` en `Draft`; único por `(TenantId, CompanyId, ReturnNumber)` una vez asignado | Identificador de negocio legible, distinto del número SRI (que no aplica, §18); se asigna en `Authorize()`, no en `CreateDraft`, para no consumir consecutivos de drafts descartados |
| `Reason` | string | Sí | Ingresado | Solo en `Draft` | No vacío | Trazabilidad del motivo de negocio |
| `Status` | enum `PurchaseReturnStatus` | Sí | Máquina de estados | Sí (transición controlada) | `Draft → Authorized`, `Draft → Cancelled` (sin efectos de reversa), **y** `Authorized → Cancelled` (con reversas auditadas) — máquina única, ver §9.1 (corregida: la versión previa de este documento tenía una afirmación contradictoria en esta celda; §9.1 siempre fue la versión correcta) | Estado de ciclo de vida — ver §9 |
| `FiscalStatus` | enum `PurchaseReturnFiscalStatus` | Sí, **siempre tiene un valor** (nunca `null`) | `NotApplicable` al crear el `Draft`; recalculado a `PendingSupplierCreditNote` en `Authorize()` | Sí (`NotApplicable→PendingSupplierCreditNote` en `Authorize()`; `PendingSupplierCreditNote→SupplierCreditNoteRegistered` solo por vínculo de NC) | `NotApplicable` solo mientras `Status == Draft`; una vez `Authorized`, nunca vuelve a `NotApplicable`; `SupplierCreditNoteRegistered` es terminal | Estado de ciclo de vida fiscal, independiente del operativo — decisión de negocio §3.12. Corrección de diseño explícita (bloqueante 3): la versión previa marcaba este campo "obligatorio" pero solo lo fijaba en `Authorize()`, dejándolo sin valor definido durante `Draft` — se resuelve con el valor explícito `NotApplicable`, nunca `null` |
| `SupplierCreditNoteDocumentId` | Guid? | No | Ingresado (operación de vínculo) | Sí (una sola vez, de `null` a un valor) | Único (`UNIQUE (TenantId, SupplierCreditNoteDocumentId) WHERE NOT NULL`) — 1:1 | Referencia, no copia — ver §18 |
| `HistoricalCostTotal` | decimal `numeric(18,2)` | Solo tras `Authorize()` | Calculado (`Σ PurchaseReturnDetail.HistoricalCostAmount`) | No | — | Snapshot — costo histórico de inventario revertido, distinto del valor reconocido financiero — ver §19.1bis (bloqueante 7) |
| `CostVarianceTotal` | decimal `numeric(18,2)`, con signo | Solo tras `Authorize()` | Calculado (`HistoricalCostTotal − AuthorizedSubtotal`) | No | Puede ser positivo, negativo o cero | Snapshot — diferencia entre costo histórico y valor reconocido neto, usada por el asiento contable compuesto — ver §19.1bis |
| `CreateClientRequestId` | Guid | **Obligatorio como input de `CreateDraft`** (columna NOT NULL — existe desde la creación del registro, a diferencia de las tres siguientes) | Ingresado por el cliente HTTP en `CreateDraft` | No | Único `(TenantId, CreateClientRequestId)` | Idempotencia obligatoria de creación de draft — ver §16.2 (bloqueante 2; corrige la versión previa, que lo marcaba "recomendado") |
| `AuthorizeClientRequestId` | Guid? | **Obligatorio como input de `Authorize`** — columna nullable a nivel de esquema (no existe valor antes de que la operación ocurra), pero el endpoint de `Authorize` rechaza la solicitud si no se envía | Ingresado por el cliente HTTP en `Authorize` | No, asignado una vez (`null → valor`) | Único `(TenantId, AuthorizeClientRequestId) WHERE NOT NULL` | Idempotencia obligatoria de autorización — ver §16.2 |
| `CancelClientRequestId` | Guid? | **Obligatorio como input de `Cancel`** — misma semántica que `AuthorizeClientRequestId` | Ingresado por el cliente HTTP en `Cancel` | No, asignado una vez | Único `(TenantId, CancelClientRequestId) WHERE NOT NULL` | Idempotencia obligatoria de cancelación — ver §16.2 |
| `LinkCreditNoteClientRequestId` | Guid? | **Obligatorio como input del vínculo de NC** — misma semántica | Ingresado por el cliente HTTP en el vínculo de NC | No, asignado una vez | Único `(TenantId, LinkCreditNoteClientRequestId) WHERE NOT NULL` | Idempotencia obligatoria del vínculo de NC — ver §16.2 |
| `CreateRequestPayloadHash` / `AuthorizeRequestPayloadHash` / `CancelRequestPayloadHash` / `LinkCreditNoteRequestPayloadHash` | string (hash determinista) | Obligatorio junto a cada `ClientRequestId` respectivo | Calculado por el servidor | No, junto con su `ClientRequestId` | — | Huella del payload relevante de cada operación — ver §16.2 |
| `AuthorizedSubtotal/VatTotal/IceTotal/DiscountTotal/GrandTotal` | decimal `numeric(18,2)` | Solo tras `Authorize()` | Calculado, snapshot | No, tras `Authorize()` | Suma de líneas prorateadas | Snapshot congelado, igual patrón que `PurchaseInvoice.ConfirmedGrandTotal` |
| `AppliedToPayableAmount` | decimal `numeric(18,2)` | Solo tras `Authorize()` | Calculado (`min(GrandTotal, BalanceDue antes)`) | No | `AppliedToPayableAmount ≤ GrandTotal` | Snapshot — necesario para poder revertir exactamente el mismo valor en `Cancel()` sin recalcular contra un `BalanceDue` que ya cambió |
| `SupplierCreditAmount` | decimal `numeric(18,2)` | Solo tras `Authorize()` | Calculado (`GrandTotal − AppliedToPayableAmount`) | No | `= GrandTotal − AppliedToPayableAmount` | Snapshot — trazabilidad directa de cuánto de esta devolución se convirtió en crédito |
| `AuthorizedAtUtc` / `AuthorizedByUserId` | DateTime / Guid | Solo tras `Authorize()` | Sistema | No | — | Auditoría mínima embebida (adicional a `PurchaseReturnAudit`) |
| `CancelledAtUtc` / `CancelledByUserId` / `CancellationReason` | DateTime / Guid / string | Solo tras `Cancel()` | Sistema / ingresado | No | — | — |
| `RowVersion` (xmin) | uint (shadow) | Sí | EF/PostgreSQL | Sistema | — | Concurrencia optimista del propio agregado |

### 7.1bis Por qué `ReturnNumber` no reutiliza `DocumentSequence` (resuelve bloqueante 10)

**Evidencia técnica concreta, verificada contra el código real:**

- `DocumentSequence.EmissionPointId` es `Guid` **no nullable**, con `IsRequired()` en `DocumentSequenceConfiguration` y `FK` real (`HasOne<EmissionPoint>().HasForeignKey(x => x.EmissionPointId)`, `DeleteBehavior.Restrict`) — no admite un `Guid` sintético; debe ser un `EmissionPoint` real, que es un concepto SRI (dispositivo/canal de emisión dentro de un `Establishment`).
- `DocumentSequence.DocTypeCode` tiene además una segunda `FK` real: `HasOne<SriDocType>().HasForeignKey(x => x.DocTypeCode)` contra la tabla `sri_doc_type` — un catálogo oficial de tipos de comprobante SRI (`"01"` Factura, `"04"` NC, `"05"` ND, `"07"` Retención, etc.), poblado únicamente con códigos reales reconocidos por el SRI (`SriDocType.IsElectronic`). Insertar un código interno inventado (p. ej. `"PR"`) en esa tabla para satisfacer la FK **corrompería la semántica de un catálogo oficial** — el mismo principio ya vigente para catálogos tributarios (`CLAUDE.md` — "todos los códigos tributarios provienen exclusivamente de catálogos oficiales, nunca listas hardcodeadas ni catálogos reconstruidos manualmente") aplica por analogía directa a un catálogo de tipos de comprobante SRI.
- Por decisión de negocio ya cerrada (§3.13), `PurchaseReturn` **no emite ningún documento SRI propio** — no tiene ni necesita un `EmissionPointId` real asociado a su propio ciclo de vida. Forzar la reutilización exigiría o bien inventar un `EmissionPoint`/`DocTypeCode` ficticios que rompen la integridad semántica de dos catálogos SRI reales, o bien acoplar arbitrariamente el correlativo interno de la devolución al punto de emisión de facturación de la empresa — un acoplamiento sin ninguna relación de negocio real (una devolución no tiene "punto de emisión").

**Imposibilidad técnica concreta confirmada — se diseña la extensión mínima necesaria**: un componente nuevo, `PurchaseReturnSequence` (`ERP.Domain/Modules/Purchases/Entities/`), deliberadamente mínimo y NO genérico (no es un segundo `DocumentSequence`, no sirve para ningún otro tipo documental — evita la "infraestructura de numeración genérica" que el mandato prohíbe crear como tal):

- **Ámbito**: `(TenantId, CompanyId)` — sin `EmissionPointId` ni `DocTypeCode`, porque no hay ni punto de emisión ni tipo de comprobante SRI que numerar.
- **Clave/formato**: entero `CurrentSeq` (empieza en 1), formateado a 8 dígitos (`D8`) — deliberadamente distinto del formato `D9` de `DocumentSequence`, para que un `ReturnNumber` nunca sea visualmente confundible con un número SRI.
- **Momento de asignación**: en `Authorize()`, no en `CreateDraft()` — evita consumir consecutivos de borradores descartados (mismo criterio ya usado para `AuthorizedSubtotal` y el resto de snapshots de autorización).
- **Concurrencia (corrección de diseño explícita — bloqueante 4 de la tercera revisión)**: la versión previa de este documento afirmaba que `CaptureNextAsync` "sigue el mismo patrón atómico ya FROZEN... advisory lock de PostgreSQL + transacción propia", calcado literalmente del comportamiento de `DocumentSequence.CaptureNextAsync`. Esa afirmación es incorrecta para `PurchaseReturnSequence` y se elimina: `DocumentSequence.CaptureNextAsync` puede permitirse una transacción propia porque se invoca como una operación aislada, sin una transacción ambiente ya abierta por el llamador. `PurchaseReturnSequence.CaptureNextAsync`, en cambio, se invoca **desde dentro** de la transacción explícita ya abierta por `AuthorizePurchaseReturnUseCases` (§16.1, `IUnitOfWork.BeginTransactionAsync`) — abrir una segunda transacción propia en ese punto sería, según el motor, una sub-transacción anidada no soportada de forma nativa por Npgsql/PostgreSQL sobre la misma conexión, o bien una transacción realmente independiente sobre una conexión distinta, que rompería exactamente la propiedad que el bloqueante exige: que el número solo quede consumido si el resto de la autorización también se confirma.
- **Regla obligatoria de participación transaccional**: la captura del siguiente número participa en la **misma transacción autoritativa** de `Authorize()` — mismo `DbContext`, misma conexión, misma transacción de base de datos, mismo `commit` final. `IPurchaseReturnSequenceRepository.CaptureNextAsync(tenantId, companyId, ct)` recibe (o utiliza implícitamente, por diseño de repositorio ya vinculado a un `DbContext` con ámbito de request) el `DbContext`/conexión/transacción ambiente ya abierta por el handler — **nunca** abre ni confirma una transacción propia cuando existe una transacción ambiente de autorización. El contrato conceptual del método es el mismo que el resto de operaciones bajo Lock A (§15): opera dentro de la transacción que el llamador ya controla, nunca gestiona su propio ciclo de commit/rollback.
- **Secuencia exacta dentro de `Authorize()`** (documentada aquí punto por punto, sin dejar ningún paso implícito):
  1. Ámbito de la secuencia: `(TenantId, CompanyId)` — sin `EmissionPointId`/`DocTypeCode` (ver más arriba).
  2. Clave de la fila: `(TenantId, CompanyId)` en `PurchaseReturnSequence` — fila creada on-demand si no existe (mismo patrón de creación perezosa que `DocumentSequence`, pero sin abrir transacción propia para ello: la creación on-demand ocurre también dentro de la transacción ambiente).
  3. Lock PostgreSQL utilizado: `pg_advisory_xact_lock` (con ámbito de **transacción**, no de sesión) sobre el hash de `(namespace "PurchaseReturn.Sequence", TenantId, CompanyId)` — se libera automáticamente al `COMMIT`/`ROLLBACK` de la transacción ambiente de `Authorize()`, exactamente el mismo mecanismo ya usado por Lock A/B (§15.1), nunca un lock de sesión que sobreviva más allá de la transacción.
  4. Lectura del valor actual: `SELECT CurrentSeq FROM purchase_return_sequence WHERE tenant_id=... AND company_id=... FOR UPDATE` (o lectura simple protegida ya por el advisory lock transaccional — el `FOR UPDATE` es redundante pero inofensivo como segunda defensa), dentro de la misma conexión/transacción del `DbContext` de `Authorize()`.
  5. Incremento: `CurrentSeq += 1` en memoria, sobre la misma entidad trackeada por el `DbContext` ambiente.
  6. Generación de `ReturnNumber`: formateo `D8` del nuevo `CurrentSeq` (§7.1bis).
  7. Asignación a `PurchaseReturn.ReturnNumber` — mutación en memoria del agregado ya cargado en el mismo `DbContext`.
  8. Restricción única final: `UNIQUE (TenantId, CompanyId, ReturnNumber)` en `PurchaseReturn`, verificada por PostgreSQL en el mismo `SaveChanges` que el resto de mutaciones de `Authorize()` — nunca en un `SaveChanges` separado.
  9. Persistencia dentro de la transacción de autorización: el `UPDATE` de `PurchaseReturnSequence.CurrentSeq` y el `UPDATE`/`INSERT` de `PurchaseReturn`/`PurchaseReturnDetail`/`StockMovement`/`PurchasePayable`/`SupplierCredit` viajan en el **mismo** `SaveChangesWithSequenceRetryAsync` (§16.1).
  10. Commit único: `CommitAsync()` de la transacción ambiente de `Authorize()` confirma o revierte **todo junto** — el incremento de secuencia incluido, nunca por separado.
  11. Comportamiento ante rollback: si cualquier paso posterior de `Authorize()` falla y la transacción hace `ROLLBACK`, el incremento de `CurrentSeq` también se revierte — **el número nunca queda consumido si la autorización completa no se confirma**. Esto corrige explícitamente la afirmación previa ("el número queda quemado si el resto de la transacción hace rollback"), que ya no aplica bajo este mecanismo: al compartir transacción, no hay ningún escenario de rollback de `Authorize()` que dañe la numeración.
  12. Comportamiento ante retry: ver siguiente punto.
  13. Comportamiento ante conflicto concurrente: dos autorizaciones concurrentes en el mismo `(TenantId, CompanyId)` se serializan por el `pg_advisory_xact_lock` transaccional del paso 3 — la segunda espera hasta que la primera haga `COMMIT`/`ROLLBACK` antes de leer `CurrentSeq`, nunca lee un valor obsoleto.
- **Único origen posible de huecos**: dado que la captura ya no abre transacción propia, la numeración **no** puede tener huecos por un fallo interno de la propia transacción de `Authorize()` (corregido respecto a la versión previa). El único hueco posible y ya inevitable es el mismo que afecta a cualquier secuencia con advisory lock transaccional: una autorización que captura el número, comitea exitosamente en PostgreSQL, pero el cliente nunca recibe la confirmación por una falla de red posterior al commit — en ese caso el número **si fue consumido** porque la transacción completa sí se confirmó (inventario+CxP+crédito+número, todo junto); no es un hueco por causa interna de la transacción, es el mismo caso general de "commit exitoso sin respuesta al cliente" ya cubierto por el mecanismo de idempotencia (§16.2/§16.4) — un reintento con el mismo `AuthorizeClientRequestId` encuentra el `ReturnNumber` ya asignado y lo retorna, nunca genera un segundo número para la misma devolución.
- **Interacción con `SaveChangesWithSequenceRetryAsync` y la estrategia de ejecución de Npgsql/EF Core**: el incremento de `PurchaseReturnSequence.CurrentSeq` se trackea en el mismo `DbContext` que el resto de entidades de `Authorize()` y se persiste en el mismo `SaveChangesWithSequenceRetryAsync` — no en un `SaveChanges` propio. Si `SaveChangesWithSequenceRetryAsync` reintenta (por un conflicto de secuencia de `StockMovement`, no relacionado con `ReturnNumber`), el reintento **reutiliza la misma transacción ambiente y el mismo valor de `CurrentSeq` ya incrementado en memoria** — no vuelve a invocar `CaptureNextAsync` una segunda vez dentro del mismo intento de `Authorize()` (evita doble incremento). Si el conflicto de secuencia obliga a abortar la transacción completa (comportamiento estándar de PostgreSQL ante un error dentro de una transacción — ver §16.3, mismo riesgo ya declarado como validación previa obligatoria para el kardex), entonces **toda** la transacción se reinicia desde cero, incluida una nueva invocación de `CaptureNextAsync` bajo un `pg_advisory_xact_lock` fresco — nunca se reutiliza un número capturado por una transacción ya abortada. No se usa `SAVEPOINT` para el número en sí: el número y el resto de efectos de negocio comparten exactamente el mismo destino (ambos se confirman o ambos se pierden), consistente con la regla general de todo-o-nada de §4.3.
- **Restricción única**: `UNIQUE (TenantId, CompanyId, ReturnNumber)` en `PurchaseReturn`, análoga a `uq_doc_seq`.
- **Prueba PostgreSQL obligatoria** (prerrequisito de implementación, no backlog — mismo peso que §16.2ter/§16.3): antes de codificar `AuthorizePurchaseReturnUseCases`, debe existir y aprobarse una suite de integración contra PostgreSQL real que cubra, como mínimo: (a) dos autorizaciones concurrentes en el mismo `(TenantId, CompanyId)` → números consecutivos sin duplicados, la segunda espera el lock de la primera; (b) autorizaciones en ámbitos `(TenantId, CompanyId)` diferentes → proceden en paralelo sin bloquearse entre sí; (c) rollback forzado después de capturar el número (falla simulada en un paso posterior de `Authorize()`, p. ej. stock insuficiente detectado tras capturar) y antes del commit → el número **no** queda consumido, la siguiente autorización exitosa recibe ese mismo número; (d) conflicto de restricción única (`UNIQUE (TenantId, CompanyId, ReturnNumber)`) forzado artificialmente → error de negocio traducido, nunca 500; (e) retry transaccional de `SaveChangesWithSequenceRetryAsync` por conflicto de secuencia de `StockMovement` → el número final asignado es único y consistente con el resto de efectos, sin doble incremento; (f) idempotencia tras commit exitoso sin respuesta al cliente (§16.2/§16.4) → el reintento retorna el mismo `ReturnNumber`, nunca genera uno nuevo; (g) ausencia de doble numeración y ausencia de dos devoluciones con el mismo número, verificado por consulta directa tras cada escenario. La prueba existente de retry de stock (kardex) **no sustituye** esta prueba de secuencia — cubren riesgos distintos.
- **Por qué no es un generador paralelo genérico**: está deliberadamente acoplado en nombre y alcance a `PurchaseReturn` (no a "cualquier documento interno") — si en el futuro otro documento interno no-SRI necesita numeración similar, ese es el momento de evaluar generalizar el patrón (nueva decisión, no anticipada aquí sin un segundo consumidor real).

### 7.2 `PurchaseReturnDetail`

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado | No | PK | — |
| `PurchaseReturnId` | Guid | Sí | FK | No | — | Pertenencia al agregado |
| `OriginalInvoiceDetailId` | Guid | Sí | Ingresado (selección de línea devolvible) | No | Debe pertenecer a `PurchaseInvoice` referenciada por el header; único por `(PurchaseReturnId, OriginalInvoiceDetailId)` | **Referencia inequívoca** — resuelve hallazgo #3, ver §14.1 |
| `ItemId` | Guid | Sí | Snapshot de `PurchaseInvoiceDetail.ItemId` | No | — | Snapshot — evita join para mostrar/validar |
| `Quantity` | decimal `numeric(18,4)` | Sí | Ingresado | Solo en `Draft` | `0 < Quantity ≤ remanente` (revalidado bajo lock en `Authorize()`) | Cantidad de negocio |
| `WarehouseId` | Guid | Sí | Snapshot de `PurchaseInvoiceDetail.WarehouseId` | No | Debe coincidir con la bodega congelada de la línea original — no seleccionable por el usuario | Congela la bodega histórica; una devolución sale de donde entró |
| `UnitCost` | decimal `numeric(18,6)` | Sí (solo tras `Authorize()`) | Snapshot de `PurchaseInvoiceDetail.LandedUnitCost` | No | — | Decisión de negocio §3.3 — costo de reversión |
| `VatCode`/`IceCode` | string | Sí (`VatCode`), opcional (`IceCode`) | Snapshot de `PurchaseInvoiceDetail` | No | — | Trazabilidad tributaria, sin recalcular |
| `VatRate`/`IceRate` | decimal `numeric(5,2)` | Sí (`VatRate`), opcional (`IceRate`) | Snapshot de `PurchaseInvoiceDetail` | No | — | Usados para proratear, nunca resueltos de nuevo contra `ISriTaxResolver` |
| `ReturnedSubtotal`/`ReturnedDiscountAmount`/`ReturnedVatAmount`/`ReturnedIceAmount` | decimal `numeric(18,2)` | Sí (solo tras `Authorize()`) | Calculado por prorateo (§11) | No | Suma = parte de `PurchaseReturn.GrandTotal` | Snapshot financiero de la línea — necesario para el asiento contable y la auditoría sin recomputar |
| `HistoricalCostAmount` | decimal `numeric(18,2)` | Sí (solo tras `Authorize()`) | Calculado (`ROUND(UnitCost × Quantity, 2)`, `MidpointRounding.AwayFromZero`) | No | Suma = `PurchaseReturn.HistoricalCostTotal` | Snapshot del costo histórico de inventario revertido por línea — distinto de `ReturnedSubtotal` (valor reconocido financiero); resuelve bloqueante 7, ver §19.1bis |
| `IsFrozen` | bool | Sí | Sistema | Sistema (`false→true` en `Authorize()`) | — | Mismo patrón `Freeze()` que `PurchaseInvoiceDetail`/`SalesReturnDetail` |

### 7.3 `PurchasePayable` — campos nuevos

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `ReturnAppliedAmount` | decimal `numeric(18,2)`, default 0 | Sí | Acumulado por `ApplyReturnCredit()`/`ReverseReturnCredit()` | Sí (solo vía esos métodos) | `≥ 0`; `TotalAmount − PaidAmount − TotalRetained − ReturnAppliedAmount − SupplierCreditAppliedAmount ≥ 0` | Componente nuevo de `BalanceDue` — ver §12 |
| `SupplierCreditAppliedAmount` | decimal `numeric(18,2)`, default 0 | Sí | Acumulado por `ApplySupplierCredit()`/`ReverseSupplierCredit()` | Sí (solo vía esos métodos) | Igual que arriba | Componente nuevo de `BalanceDue`, separado de `ReturnAppliedAmount` para trazabilidad diferenciada (devolución directa de esta factura vs. crédito externo aplicado) |
| `RowVersion` (xmin) | uint (shadow) | Sí | EF/PostgreSQL | Sistema | — | Resuelve hallazgo #1 — mismo mecanismo que `PurchaseInvoiceConfiguration` |

### 7.4 `SupplierCredit`

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado | No | PK | — |
| `TenantId`/`CompanyId`/`BranchId` | Guid | Sí | `TenantId`/`CompanyId` de contexto; `BranchId` heredado de `PurchaseReturn.BranchId` al crearse desde `Authorize()` (nunca contexto independiente) | No | Branch Ownership Rule; `BranchId == PurchaseReturn.BranchId` siempre (§5.2) | `BranchId` nunca es una decisión financiera independiente del cliente — ver §5.2 |
| `SupplierId` | Guid | Sí | Snapshot del `PurchaseReturn` origen | No | — | Fuente de la compatibilidad de proveedor en aplicaciones |
| `CurrencyCode` | string | Sí | Snapshot de `PurchaseInvoice.CurrencyCode` | No | — | Fuente de la compatibilidad de moneda en aplicaciones |
| ~~`SourceType`~~ | — | — | — | — | — | **Eliminado** — ver §6.1 (bloqueante 9). `SourcePurchaseReturnId` es la única referencia de origen |
| `SourcePurchaseReturnId` | Guid | Sí | FK lógica | No | Único `(TenantId, SourcePurchaseReturnId)` | 1:1 con el `PurchaseReturn` que lo originó — única fuente de verdad del origen |
| `OriginalAmount` | decimal `numeric(18,2)` | Sí | `PurchaseReturn.SupplierCreditAmount` al crearse | No, inmutable | `> 0` | Monto fijado una única vez |
| `AvailableAmount` | decimal `numeric(18,2)` | Sí | Cacheado, recalculado en cada transacción de movimiento | Sí (solo por recálculo transaccional) | `0 ≤ AvailableAmount ≤ OriginalAmount`, verificado tras cada movimiento — fórmula completa §13.5 | Ver §4.2, §13.5 |
| `SupplierCreditMovement[]` | colección | — | — | Solo se agregan filas, nunca se editan | — | Fuente autoritativa de `AvailableAmount` |
| `RowVersion` (xmin) | uint (shadow) | Sí | EF/PostgreSQL | Sistema | — | Concurrencia del agregado — ver §15 |

`Status` **no es un campo persistido** — se deriva siempre como `AvailableAmount > 0 ? Open : Closed` en el momento de lectura, exactamente como exige el mandato ("derivado de saldo real, no un campo que pueda contradecir sus movimientos").

### 7.5 `SupplierCreditMovement`

**Corrección de diseño explícita (bloqueante 5 de la tercera revisión — consistencia de `SourceReturnCancelled`)**: la lista de valores de `MovementType` en esta tabla debe ser, en todo momento, idéntica a la ya definida en §5, §9.3, §13.3 y §13.5 — **5** valores: `Application/Refund/ReversalOfApplication/ReversalOfRefund/SourceReturnCancelled`. Una versión previa de esta tabla listaba solo 4, omitiendo `SourceReturnCancelled` únicamente en esta celda mientras el resto del documento ya lo usaba — corregido aquí para que no exista ninguna sección con la lista incompleta.

**Corrección de diseño explícita (bloqueante 2, corrección final — dependencia circular eliminada)**: los campos `PaymentMethodId`/`ExternalReference`/`EffectiveDate`/`ReconciledAtUtc`/`ReconciledByUserId` **no viven en `SupplierCreditMovement`** — viven en la nueva entidad `SupplierCreditRefundTransaction` (§6.4), fuente de verdad del hecho financiero del reembolso, distinta de `SupplierCreditMovement` (fuente de verdad del saldo del crédito). `SupplierCreditMovement` **no tiene ninguna columna de referencia hacia la transacción financiera** (se elimina `RefundReceiptId`/cualquier `RefundTransactionId`): la única FK autoritativa de la relación 1:1 vive en el sentido inverso, `SupplierCreditRefundTransaction.SupplierCreditMovementId` (§6.4, §10) — esto elimina la dependencia circular detectada entre ambas entidades. La navegación desde un movimiento hacia su transacción financiera se obtiene con una consulta por esa FK, nunca con una columna adicional en `SupplierCreditMovement`.

| Campo | Tipo conceptual | Obligatorio | Fuente | Mutable | Invariante | Justificación |
|---|---|---|---|---|---|---|
| `Id` | Guid | Sí | Generado | No | PK | — |
| `SupplierCreditId` | Guid | Sí | FK | No | — | Pertenencia al agregado |
| `MovementType` | enum (`Application/Refund/ReversalOfApplication/ReversalOfRefund/SourceReturnCancelled`) — **5 valores**, ver §9.3 | Sí | Ingresado según operación (los primeros 4) o generado automáticamente por el sistema (`SourceReturnCancelled`, nunca por el usuario) | No | Cerrado — ver §13.2 | — |
| `Amount` | decimal `numeric(18,2)` | Sí | Ingresado (validado ≤ `AvailableAmount` en el momento, bajo lock) | No | `> 0` (CHECK) | — |
| `TargetPurchasePayableId` | Guid? | Solo `Application`/`ReversalOfApplication` | Ingresado | No | CHECK combinado con `MovementType` (§13.2) | — |
| `ReversalOfMovementId` | Guid? | Solo `ReversalOfApplication`/`ReversalOfRefund` | Ingresado (referencia al movimiento revertido) | No | CHECK combinado; único `(ReversalOfMovementId) WHERE NOT NULL` — un movimiento se revierte como máximo una vez | Previene doble reversa |
| `ClientRequestId` | Guid | **Sí, obligatorio** | Ingresado por cliente HTTP | No | Único `(TenantId, ClientRequestId)` | Idempotencia obligatoria — ver §16.2 |
| `RequestPayloadHash` | string (hash determinista) | Sí, obligatorio | Calculado por el servidor a partir del payload relevante de la operación, incluyendo siempre el identificador del agregado/recurso objetivo | No | — | Huella del contenido de la solicitud — permite distinguir "mismo `ClientRequestId` + mismo contenido" de "mismo `ClientRequestId` + contenido distinto" — ver §16.2 |
| `CreatedAtUtc`/`CreatedByUserId` | DateTime/Guid | Sí | Sistema | No | — | — |

### 7.6 `CompanyFinancialDestination` y `SupplierCreditRefundTransaction` (nuevas — resuelven el bloqueante del destino financiero)

Ver tabla completa de campos en §6.4. `CompanyFinancialDestination` es el catálogo maestro persistido del destino real (banco o caja) y de la cuenta contable correspondiente. `SupplierCreditRefundTransaction` es la entidad hija de `SupplierCredit` (a través de `SupplierCreditMovement`, relación 1:1 vía `SupplierCreditRefundTransaction.SupplierCreditMovementId` — nunca en sentido inverso) que almacena tanto el ingreso original (`REFUND_RECEIVED`) como su reversa append-only (`REFUND_REVERSED`). Es la única fuente de verdad del hecho financiero del reembolso (destino usado, método, referencia, fecha efectiva, efecto de caja) — nunca del saldo del crédito, que sigue siendo exclusivo de `SupplierCreditMovement`/`SupplierCredit.AvailableAmount` (§13.5).

---

## 8. Datos almacenados, derivados y snapshots

Consolidado (ver también §4.1):

| Categoría | Ejemplos en este diseño |
|---|---|
| **Dato ingresado** | `PurchaseReturnDetail.Quantity`, `PurchaseReturn.Reason`, `SupplierCreditMovement.Amount`, `SupplierCreditRefundTransaction.ExternalReference` |
| **Snapshot legítimo** (copiado una vez, congelado) | `PurchaseReturnDetail.UnitCost/VatCode/IceCode/VatRate/IceRate/WarehouseId`, `PurchaseReturn.SupplierId`, `SupplierCredit.SupplierId/CurrencyCode` |
| **Dato derivado** (nunca almacenado, calculado on-demand) | Cantidad remanente por línea, `PurchasePayable.BalanceDue`, `SupplierCredit.Status` |
| **Acumulado técnico** (almacenado, mantenido transaccionalmente) | `PurchasePayable.ReturnAppliedAmount`/`SupplierCreditAppliedAmount`, `SupplierCredit.AvailableAmount` |
| **Referencia** (Guid sin copiar datos) | `PurchaseReturn.PurchaseInvoiceId`, `PurchaseReturn.SupplierCreditNoteDocumentId`, `SupplierCreditMovement.TargetPurchasePayableId` |
| **Estado de ciclo de vida** | `PurchaseReturn.Status`, `PurchaseReturn.FiscalStatus` |

No existe ningún campo tipo `Balance`, `RemainingAmount`, `ReturnedQuantity` o `IsPaid` sin la clasificación anterior explícita — cada uno de los campos de §7 está etiquetado.

---

## 9. Estados y transiciones

### 9.1 `PurchaseReturn.Status`

| Estado origen | Operación | Estado destino | Validaciones | Efectos atómicos | Operaciones prohibidas |
|---|---|---|---|---|---|
| — | `CreateDraft` | `Draft` | Factura existe y `Confirmed`; líneas seleccionadas pertenecen a la factura; cantidad ≤ remanente (preventivo, no bajo lock) | Ninguno externo — solo persiste el draft | — |
| `Draft` | `UpdateDraft` (reemplazar líneas/motivo) | `Draft` | Igual que creación | Ninguno externo | Modificar si `Status != Draft` |
| `Draft` | `Authorize` | `Authorized` | Retención no `Issued` (bajo lock); remanente revalidado bajo lock; stock suficiente | Inventario (salida) + `PurchasePayable.ApplyReturnCredit()` + creación condicional de `SupplierCredit` + `PostingFact` + `PurchaseReturnAudit` — todo en una transacción (§16) | Autorizar dos veces (idempotente — ver §16); autorizar sin líneas |
| `Draft` | `Cancel` (de un borrador) | `Cancelled` | Ninguna (no tiene efectos que revertir) | `PurchaseReturnAudit` | — |
| `Authorized` | `Cancel` (reversa) | `Cancelled` | `SupplierCredit` asociado (si existe) debe tener `AvailableAmount == OriginalAmount` (sin aplicaciones ni reembolsos activos) — revalidado bajo lock (§15) | Reversa de inventario (movimiento inverso) + `PurchasePayable.ReverseReturnCredit()` + si existe `SupplierCredit`, se anula (ver §9.3) + `PostingFact` reverso + `PurchaseReturnAudit` | Cancelar si el crédito ya tiene movimientos activos (`AvailableAmount < OriginalAmount`) — error explícito `PR-011` |
| `Authorized` | Vínculo de NC (`RegisterAndLinkSupplierCreditNote`) | `Authorized` (sin cambio de `Status`) | `PurchaseReceptionDocument` tipo `CreditNote`, mismo proveedor, misma moneda, no vinculada previamente | Solo `FiscalStatus: PendingSupplierCreditNote → SupplierCreditNoteRegistered` + `PurchaseReturnAudit` | Modificar inventario/CxP/crédito/contabilidad (decisión §3.14) |
| `Cancelled` | cualquiera | — | — | — | Toda operación — estado terminal |

### 9.2 `PurchaseReturn.FiscalStatus`

**Corrección de diseño explícita (bloqueante 3)**: `FiscalStatus` tiene **3** valores, no 2: `NotApplicable=0, PendingSupplierCreditNote=1, SupplierCreditNoteRegistered=2`. El campo es obligatorio (nunca `null`) en toda su vida — la versión previa de este documento lo declaraba obligatorio en §7.1 pero solo lo fijaba desde `Authorize()`, dejando un hueco sin valor definido durante `Draft`; se resuelve con `NotApplicable` como valor explícito y consultable mientras `Status == Draft` (no aplica estado fiscal a un borrador que aún podría no autorizarse nunca).

| Estado origen | Operación | Estado destino | Notas |
|---|---|---|---|
| — | `CreateDraft` | `NotApplicable` | Valor inicial explícito — mientras `Status == Draft`, no existe estado fiscal aplicable |
| `NotApplicable` | `Authorize` | `PendingSupplierCreditNote` | Se fija en el momento de autorizar, independientemente de si ya se conoce el número de NC. Transición única e irreversible — nunca vuelve a `NotApplicable` |
| `PendingSupplierCreditNote` | Vínculo de NC | `SupplierCreditNoteRegistered` | Único cambio posible, una sola vez |
| `SupplierCreditNoteRegistered` | — | — | Terminal — no se permite desvincular ni volver a `Pending` (una corrección exige cancelar la devolución completa, no editar el vínculo fiscal) |

Si `PurchaseReturn` se cancela estando en `PendingSupplierCreditNote` **o** en `SupplierCreditNoteRegistered` (ver §5.1 caso 8), el estado fiscal queda congelado en ese valor exacto — deja de ser relevante para negocio porque `Status == Cancelled` lo hace irrelevante, pero no se sobrescribe ni se fuerza a un cuarto valor "cancelado" — preserva el historial fiscal exacto tal como ocurrió. Un `PurchaseReturn` cancelado desde `Draft` conserva `FiscalStatus == NotApplicable` (nunca llegó a tener relevancia fiscal).

**Sin ambigüedad en ningún momento**: en borrador → `NotApplicable` (sin estado fiscal aplicable); autorizada esperando NC → `PendingSupplierCreditNote`; NC registrada-vinculada → `SupplierCreditNoteRegistered`; cancelada → conserva el valor fiscal que tenía en el instante de la cancelación (uno de los tres anteriores), nunca un cuarto estado.

### 9.3 `SupplierCredit` (vía sus movimientos — no tiene `Status` propio, ver §7.4)

| Estado (derivado) origen | Operación | Estado (derivado) destino | Validaciones | Efectos atómicos | Operaciones prohibidas |
|---|---|---|---|---|---|
| — | Creación (dentro de `Authorize()` de `PurchaseReturn`) | `Open` (`AvailableAmount = OriginalAmount`) | Solo si excedente > 0 | Inserción de `SupplierCredit`, sin movimientos | — |
| `Open` | `ApplyToPayable` | `Open` o `Closed` (según resultado) | `Amount ≤ AvailableAmount`; proveedor y moneda coinciden con el `PurchasePayable` destino; destino no está `cancelled` | `SupplierCreditMovement(Application)` + `AvailableAmount -= Amount` + `PurchasePayable.ApplySupplierCredit()` en el destino + `PostingFact` + `SupplierCreditAudit` | Aplicar a CxP de otro proveedor (`SC-004`); sobreaplicar (`SC-003`) |
| `Open`/`Closed` | `RegisterRefund` | `Open` o `Closed` | `Amount ≤ AvailableAmount` | `SupplierCreditMovement(Refund)` + `AvailableAmount -= Amount` + `PostingFact` + `SupplierCreditAudit` | Reembolsar más de lo disponible |
| — | `ReverseApplication` | Aumenta `AvailableAmount` | El movimiento original debe existir, no estar ya revertido, y su `PurchasePayable` destino debe seguir permitiendo la reversa (`SupplierCreditAppliedAmount` destino ≥ monto a revertir) | `SupplierCreditMovement(ReversalOfApplication)` + `AvailableAmount += Amount` + `PurchasePayable.ReverseSupplierCredit()` en el destino + `PostingFact` reverso + `SupplierCreditAudit` | Revertir un movimiento ya revertido (`SC-011`) |
| — | `ReverseRefund` | Aumenta `AvailableAmount` | El movimiento original debe existir y no estar ya revertido | `SupplierCreditMovement(ReversalOfRefund)` + `AvailableAmount += Amount` + `PostingFact` reverso + `SupplierCreditAudit` | Igual que arriba |
| — | Anulación por cancelación del `PurchaseReturn` origen | `AvailableAmount → 0` de forma definitiva (registro de cierre, no eliminación) | Solo permitido si `AvailableAmount == OriginalAmount` (§9.1, §5.1 casos 6/7) | Se marca como consumido en su totalidad por la cancelación del origen — se modela como un movimiento técnico adicional `ReversalOfApplication`/equivalente no aplica; se implementa como parte del mismo `PurchaseReturnCancelledEvent`: el `SupplierCredit` completo queda con `AvailableAmount = 0` mediante un movimiento reservado tipo `ReversalOfApplication` con `TargetPurchasePayableId = null` **no es válido bajo el CHECK actual** — en su lugar, el diseño reserva un quinto valor de `MovementType`: `SourceReturnCancelled` (sin destino, sin `PaymentMethodId`), exclusivo para este caso, generado automáticamente por el mismo `Authorize`/`Cancel` handler de `PurchaseReturn`, nunca por el usuario. **`Amount` de este movimiento es siempre exactamente `AvailableAmount` en el instante de la cancelación** — que, por la precondición `AvailableAmount == OriginalAmount` ya exigida, es siempre igual a `OriginalAmount` (nunca puede haber reembolsos/aplicaciones parciales previos, porque esos ya habrían bloqueado la cancelación con `PR-011`). Ver fórmula completa y esta confirmación de consistencia en §13.5 | — |

**Corrección de diseño explícita**: `MovementType` tiene **5** valores, no 4: `Application=1, Refund=2, ReversalOfApplication=3, ReversalOfRefund=4, SourceReturnCancelled=5`. El quinto valor es cerrado y de sistema (nunca seleccionable por el usuario en la API pública de aplicación/reembolso) — se usa exclusivamente como efecto colateral atómico de `PurchaseReturn.Cancel()` cuando existe un `SupplierCredit` íntegro (sin aplicaciones/reembolsos) asociado. Justificación: mantiene la regla "una sola colección de movimientos es la fuente de verdad de `AvailableAmount`" incluso para el caso de cancelación del origen, sin necesitar un campo `Status` editable directamente.

---

## 10. Cálculos de cantidades

### 10.1 Trazabilidad por línea (resuelve hallazgo #3)

- `PurchaseReturnDetail.OriginalInvoiceDetailId` referencia **la fila exacta** de `PurchaseInvoiceDetail`, nunca el producto/bodega de forma ambigua. Esto resuelve por diseño el riesgo señalado en la auditoría de "dos líneas del mismo producto y bodega con distinto costo" — la referencia es a la línea, no a la combinación producto+bodega.
- Único `(PurchaseReturnId, OriginalInvoiceDetailId)` — una misma línea de factura no puede aparecer dos veces dentro del mismo `PurchaseReturn` (una segunda devolución de la misma línea requiere un `PurchaseReturn` distinto).

### 10.2 Cantidad ya devuelta (resuelve hallazgo #4)

```
CantidadDevuelta(invoiceDetailId) =
    SUM(pr_detail.Quantity)
    FROM PurchaseReturnDetail pr_detail
    JOIN PurchaseReturn pr ON pr_detail.PurchaseReturnId = pr.Id
    WHERE pr_detail.OriginalInvoiceDetailId = invoiceDetailId
      AND pr.Status = Authorized   -- excluye Draft (no comprometido) y Cancelled
```

`CantidadRemanente(invoiceDetailId) = PurchaseInvoiceDetail.Quantity − CantidadDevuelta(invoiceDetailId)`.

Esta consulta es la **única** fuente de "cantidad ya devuelta" — se deriva de documentos autorizados no cancelados, nunca de un contador mutable en `PurchaseInvoiceDetail` (consistente con la decisión de negocio §3.2 y con el mandato explícito de no crear ese contador salvo demostrar que es imprescindible — no lo es: la consulta es directa sobre dos tablas nuevas, sin agregaciones costosas sobre `StockMovement`).

### 10.3 `StockMovement.SourceDocLineId` (columna genérica nueva)

- Para el movimiento de salida de una devolución: `SourceDocId = PurchaseReturn.Id`, `SourceDocType = "PurchaseReturn"`, `SourceDocLineId = PurchaseReturnDetail.Id`.
- Semántica: referencia genérica (no específica de Compras) a la línea del documento origen del movimiento — reutilizable por cualquier módulo futuro que necesite trazabilidad línea-a-línea del kardex, sin requerir refactorización posterior.
- **No es la fuente de "cantidad ya devuelta"** (eso es §10.2) — es trazabilidad de kardex/auditoría: dado un `StockMovement`, poder identificar sin ambigüedad qué línea específica de qué documento lo originó, incluso cuando dos líneas del mismo documento comparten producto y bodega pero difieren en costo.

---

## 11. Cálculos financieros y redondeo

### 11.1 Fórmula única de autorización

Para cada `PurchaseReturnDetail` con `OriginalInvoiceDetailId → PurchaseInvoiceDetail (D)`:

```
fraccion            = Quantity_devuelta / D.Quantity                          (numeric(18,4) / numeric(18,4))
ReturnedSubtotal     = ROUND(fraccion × (D.LineSubtotal − D.DiscountAmount), 2)
ReturnedVatAmount     = ROUND(fraccion × D.VatAmount, 2)
ReturnedIceAmount     = ROUND(fraccion × D.IceAmount, 2)
ReturnedDiscountAmount = ROUND(fraccion × D.DiscountAmount, 2)
LineGrandTotal        = ReturnedSubtotal + ReturnedVatAmount + ReturnedIceAmount
```

Redondeo: `MidpointRounding.AwayFromZero`, aplicado independientemente a cada componente monetario de cada línea, en `numeric(18,2)`, con `CultureInfo.InvariantCulture` (regla del proyecto). No se prorratea contra un total global recalculado — cada línea es responsable de su propio redondeo; el residual acumulado posible (máximo ±0.01 por componente por línea) se acepta como parte del snapshot congelado, igual criterio que ya usa el proyecto para snapshots fiscales (no se ajusta artificialmente para "cuadrar" un total).

A nivel de `PurchaseReturn` (header):

```
GrandTotal = SUM(LineGrandTotal) sobre todas las líneas
```

### 11.2 Fórmula de tratamiento CxP / crédito

```
balanceDueAntes      = PurchasePayable.BalanceDue                              (antes de cualquier mutación, bajo lock)
appliedToPayable     = MIN(GrandTotal, balanceDueAntes)
supplierCreditAmount = GrandTotal − appliedToPayable
balanceDueDespues    = balanceDueAntes − appliedToPayable                      (siempre ≥ 0 por construcción)
```

Si `supplierCreditAmount > 0` → se crea `SupplierCredit.OriginalAmount = supplierCreditAmount`. Si `= 0` → no se crea ningún `SupplierCredit`.

Moneda: `SupplierCredit.CurrencyCode = PurchaseInvoice.CurrencyCode` (snapshot). No se contempla en v1 devolución con moneda distinta a la de la factura original — la línea no ofrece selector de moneda (hereda la de la factura).

### 11.3 Ejemplos numéricos completos

**(a) Factura impaga.** `TotalAmount=1000, PaidAmount=0, TotalRetained=0 → BalanceDue=1000`. Devolución `GrandTotal=300`. `appliedToPayable = MIN(300,1000) = 300`. `supplierCreditAmount = 0`. `BalanceDue después = 700`. No se crea `SupplierCredit`.

**(b) Parcialmente pagada, devolución menor al saldo.** `TotalAmount=1000, PaidAmount=600, TotalRetained=0 → BalanceDue=400`. Devolución `GrandTotal=250`. `appliedToPayable=250`. `supplierCreditAmount=0`. `BalanceDue después=150`.

**(c) Parcialmente pagada, devolución igual al saldo.** `BalanceDue=400`. Devolución `GrandTotal=400`. `appliedToPayable=400`. `supplierCreditAmount=0`. `BalanceDue después=0`. No se crea `SupplierCredit` (excedente exactamente cero).

**(d) Parcialmente pagada, devolución mayor al saldo.** `BalanceDue=400`. Devolución `GrandTotal=550`. `appliedToPayable=400`. `supplierCreditAmount=150`. `BalanceDue después=0`. Se crea `SupplierCredit.OriginalAmount=150`.

**(e) Totalmente pagada.** `TotalAmount=1000, PaidAmount=1000, TotalRetained=0 → BalanceDue=0`. Devolución `GrandTotal=300`. `appliedToPayable=0`. `supplierCreditAmount=300`. `BalanceDue después=0` (sin cambio). Se crea `SupplierCredit.OriginalAmount=300`.

**(g) Diferencia entre valor reconocido y costo histórico (resuelve bloqueante 7 — desglose contable completo en §19.1bis).** Factura con una línea: `Quantity=10, LineSubtotal=1000 (UnitPrice=100), DiscountAmount=0, VatAmount=120 [12%], LandedUnitCost=115` (incluye flete/nacionalización, mayor que `UnitPrice=100` porque el costo de importación no forma parte del precio pactado con el proveedor). `BalanceDue=1120` (impaga). Devolución de 3 unidades (`fraccion=0.3`).
- `ReturnedSubtotal = ROUND(0.3 × 1000, 2) = 300.00`. `ReturnedVatAmount = ROUND(0.3 × 120, 2) = 36.00`. `GrandTotal = 336.00`.
- `HistoricalCostAmount = ROUND(3 × 115, 2) = 345.00` (costo histórico de inventario revertido, sobre `LandedUnitCost`, nunca sobre `UnitPrice`).
- `CostVarianceTotal = HistoricalCostTotal − AuthorizedSubtotal = 345.00 − 300.00 = 45.00` (positivo: el costo histórico revertido es mayor que el valor reconocido neto pactado con el proveedor).
- `appliedToPayable = MIN(336.00, 1120) = 336.00`. `supplierCreditAmount = 0`. `BalanceDue después = 784.00`.
- Asiento balanceado (derivación completa en §19.1bis): Débitos = CxP (336.00) + Variación de costo (45.00) = **381.00**. Créditos = Inventario (345.00) + IVA crédito tributario (36.00) = **381.00**. `Σdébitos = Σcréditos`.

**(f) Devolución parcial con varias líneas e impuestos diferentes.** Factura con dos líneas: Línea 1 (`Quantity=10, LineSubtotal=1000, DiscountAmount=0, VatAmount=120 [12%], IceAmount=0`); Línea 2 (`Quantity=5, LineSubtotal=500, DiscountAmount=0, VatAmount=0 [0%], IceAmount=50`). `BalanceDue=1200` (impaga). Devolución: 3 unidades de Línea 1 (`fraccion=0.3`) y 2 unidades de Línea 2 (`fraccion=0.4`).
- Línea 1: `ReturnedSubtotal=300.00, ReturnedVatAmount=36.00, ReturnedIceAmount=0.00 → LineGrandTotal=336.00`.
- Línea 2: `ReturnedSubtotal=200.00, ReturnedVatAmount=0.00, ReturnedIceAmount=20.00 → LineGrandTotal=220.00`.
- `GrandTotal=556.00`. `appliedToPayable=MIN(556,1200)=556.00`. `supplierCreditAmount=0`. `BalanceDue después=644.00`.

Ningún ejemplo recalcula tasas — todos usan `VatRate`/`IceRate`/montos ya congelados en `PurchaseInvoiceDetail`.

---

## 12. Integración con `PurchasePayable`

### 12.1 Fórmula extendida

```
BalanceDue = TotalAmount − PaidAmount − TotalRetained − ReturnAppliedAmount − SupplierCreditAppliedAmount
```

### 12.2 Métodos de dominio nuevos (nunca reutilizan `RegisterPayment`/`ReversePayment`/`ApplyRetention`/`ReverseRetention`)

| Método | Efecto | Guard | Usado por |
|---|---|---|---|
| `ApplyReturnCredit(recognizedAmount, uid)` | `appliedAmount = MIN(recognizedAmount, BalanceDue)`; `ReturnAppliedAmount += appliedAmount`; retorna `(appliedAmount, recognizedAmount − appliedAmount)` | `Status != "cancelled"` (misma guarda que `RegisterPayment`) | `AuthorizePurchaseReturnUseCases` |
| `ReverseReturnCredit(appliedAmount, uid)` | `ReturnAppliedAmount -= appliedAmount` | `ReturnAppliedAmount − appliedAmount ≥ 0` (`PR-999` interno, no debería fallar nunca si el flujo es correcto — defensivo) | `CancelPurchaseReturnUseCases` |
| `ApplySupplierCredit(amount, uid)` | `amount ≤ BalanceDue` (evita que la CxP quede negativa por sobreaplicación de crédito); `SupplierCreditAppliedAmount += amount` | `amount ≤ BalanceDue` → si no, `PR-010` | `ApplySupplierCreditUseCases` |
| `ReverseSupplierCredit(amount, uid)` | `SupplierCreditAppliedAmount -= amount` | `≥ 0` | `ReverseSupplierCreditApplicationUseCases` |

### 12.3 Concurrencia (resuelve hallazgo #1)

- Se agrega `xmin` (RowVersion, shadow property) a `PurchasePayableConfiguration` — mismo mecanismo ya usado por `PurchaseInvoiceConfiguration`/`IssuedWithholdingConfiguration`/`CurrentStockConfiguration`.
- Toda mutación de `PurchasePayable` (los 4 métodos nuevos + los ya existentes `RegisterPayment`/`ReversePayment`/`ApplyRetention`/`ReverseRetention`/`CancelPayable`) queda protegida por `DbUpdateConcurrencyException` en el `SaveChanges` correspondiente.
- El handler que la produce debe traducirla a un error de negocio de concurrencia (`PR-008`/`PY-CONCURRENCY-01` según el caso — ver §21), nunca dejarla propagar como 500. Aplica también a los handlers **existentes** `RegisterPaymentCommandHandler`/`ReversePaymentCommandHandler`/`IssueWithholdingHandler`/`CancelWithholdingHandler`/`CancelPurchaseHandler`, que hoy no la capturan porque `PurchasePayable` no tenía `xmin` — este es un cambio de comportamiento necesario y ya justificado en §15 (coordinación de locks).
- El `xmin` es la **segunda defensa** (detecta conflicto si dos transacciones llegan a `SaveChanges` sin haber estado serializadas por el advisory lock — p. ej. un bug futuro que omita adquirir el lock). La **primera defensa** es el advisory lock (§15).

---

## 13. `SupplierCredit`, aplicaciones y reembolsos

### 13.1 Modelo mínimo

Ver §7.4 y §7.5. Un único agregado (`SupplierCredit`) con una única colección de movimientos (`SupplierCreditMovement`).

### 13.2 Por qué una sola colección y no dos tablas

Aplicaciones y reembolsos comparten exactamente los mismos invariantes estructurales: monto positivo, no exceder `AvailableAmount` en el momento de la operación, requerir auditoría idéntica, y ser reversibles mediante un movimiento nuevo (nunca edición). La única diferencia es el **destino** (una CxP para aplicación, un método de pago + referencia para reembolso) — modelada con columnas nullable condicionadas por `MovementType`, no con dos tablas que duplicarían columnas comunes (`Amount`, `ClientRequestId`, auditoría, reversión). Dos tablas obligarían además a calcular `AvailableAmount` con un `UNION` entre ambas, incrementando el riesgo de desincronización que el diseño evita centralizando la suma en una sola colección.

### 13.3 Restricciones (resumen, ver §7.5 para detalle completo)

- `CHECK Amount > 0`.
- `CHECK` combinado: `MovementType IN (Application, ReversalOfApplication) ⇒ TargetPurchasePayableId NOT NULL`.
- `CHECK` combinado: `MovementType = SourceReturnCancelled ⇒ TargetPurchasePayableId IS NULL`.
- `CHECK` combinado: `MovementType IN (ReversalOfApplication, ReversalOfRefund) ⇒ ReversalOfMovementId NOT NULL`.
- `UNIQUE (ReversalOfMovementId) WHERE ReversalOfMovementId IS NOT NULL` — un movimiento se revierte una sola vez.
- `UNIQUE (TenantId, ClientRequestId)` — idempotencia obligatoria (§16.2; ya no es `WHERE ClientRequestId IS NOT NULL` porque el campo pasó de opcional a obligatorio — bloqueante 2).
- `SupplierCreditMovement` **no** tiene columna de referencia hacia el hecho financiero — la relación 1:1 con `SupplierCreditRefundTransaction` (`MovementType ∈ {Refund, ReversalOfRefund}`) vive exclusivamente como FK en sentido inverso (`SupplierCreditRefundTransaction.SupplierCreditMovementId`, `UNIQUE (TenantId, CompanyId, SupplierCreditMovementId)`, §6.4, §7.6) — evita la dependencia circular detectada entre ambas entidades.

### 13.4 Prohibición de sobreaplicación y compatibilidad

- **Sobreaplicación**: guard de dominio en `SupplierCredit.ApplyToPayable(amount)` — `amount ≤ AvailableAmount` calculado bajo lock (§15), si no `SC-003`.
- **Proveedor incompatible**: `SupplierCredit.SupplierId` debe igualar el `SupplierId` del `PurchasePayable`/`PurchaseInvoice` destino — verificado en la capa de aplicación antes de invocar el dominio, y repetido como guard defensivo dentro del método de dominio (defensa en profundidad) — si no `SC-004`.
- **Moneda incompatible**: `SupplierCredit.CurrencyCode` debe igualar la del `PurchaseInvoice` destino — si no `SC-005`.
- **Comportamiento concurrente**: ver §15.
- **Reversa de aplicaciones/reembolsos**: cada una genera un movimiento nuevo (`ReversalOfApplication`/`ReversalOfRefund`), nunca edita el original — mantiene el historial íntegro (regla de no eliminación física, §3.21).
- **Relación con el documento origen**: `SupplierCredit.SourcePurchaseReturnId` es la única relación de origen — no existe ninguna otra vía de creación de `SupplierCredit` en v1.

### 13.5 Fórmula completa de `SupplierCredit.AvailableAmount` (resuelve bloqueante 8)

```
AvailableAmount =
      OriginalAmount
    − Σ Amount(MovementType = Application)
    + Σ Amount(MovementType = ReversalOfApplication)
    − Σ Amount(MovementType = Refund)
    + Σ Amount(MovementType = ReversalOfRefund)
    − Σ Amount(MovementType = SourceReturnCancelled)
```

- **Signo por tipo de movimiento**: `Application` y `Refund` reducen el saldo (signo `−`); `ReversalOfApplication` y `ReversalOfRefund` lo aumentan (signo `+`); `SourceReturnCancelled` lo reduce a 0 (signo `−`, ver más abajo por qué siempre equivale a restar exactamente el saldo restante).
- **Qué movimientos participan**: **todos**, sin excepción — no existen movimientos "inactivos" ni un estado que los desactive. Cada fila de `SupplierCreditMovement` es un hecho definitivo e inmutable; una reversión es siempre un **movimiento nuevo** (`ReversalOfApplication`/`ReversalOfRefund`), nunca un cambio de estado ni una edición del movimiento original. Esto corrige la ambigüedad de la frase "movimientos activos" usada en versiones previas de este documento (§4.1, §4.2, §7.4), que sugería falsamente que algún movimiento pudiera desactivarse — ningún movimiento se desactiva jamás.
- **Prohibición de editar/borrar movimientos**: ya declarada en §13.4 — la fórmula la refuerza estructuralmente, porque el saldo se deriva de la suma con signo de filas append-only, no de un subconjunto filtrado por un flag mutable.
- **Invariante en todo momento** (no solo al final): `0 ≤ AvailableAmount ≤ OriginalAmount` se revalida bajo Lock B (§15.5) inmediatamente después de insertar **cada** movimiento — nunca se permite persistir un estado intermedio fuera de este rango, ni siquiera transitoriamente dentro de la misma transacción.
- **Comportamiento cuando el origen se cancela**: `SourceReturnCancelled` fuerza `AvailableAmount = 0` de forma exacta. Su `Amount` es siempre igual al `AvailableAmount` vigente en el instante de la cancelación — **nunca** `OriginalAmount` a ciegas. Sin embargo, dado que `PR-011` (§9.1, §5.1 casos 6/7) **ya bloquea** la cancelación de una `PurchaseReturn` `Authorized` mientras su `SupplierCredit` tenga cualquier `Application`/`Refund` activo (`AvailableAmount < OriginalAmount`), la única forma de llegar a ejecutar `SourceReturnCancelled` es con `AvailableAmount == OriginalAmount` en ese instante — por lo tanto, **en la práctica, `SourceReturnCancelled.Amount` siempre es igual a `OriginalAmount`**, nunca a un remanente parcial. Esta es una consistencia confirmada por diseño, no una coincidencia: la fórmula general (`Amount = AvailableAmount vigente`) es la regla correcta y completa; el hecho de que siempre coincida con `OriginalAmount` es una consecuencia de `PR-011`, no una regla distinta que haya que codificar por separado.
- **Estado derivado**: `Status = AvailableAmount > 0 ? Open : Closed` (ya definido en §7.4) — se deriva de la misma fórmula, nunca un campo independiente que pueda contradecirla.
- **Redondeo**: todos los montos de `SupplierCreditMovement.Amount` son `numeric(18,2)`, ya en 2 decimales en origen (heredados de `GrandTotal`/`SupplierCreditAmount`, ambos ya redondeados en §11.1) — la suma de valores ya redondeados en `numeric(18,2)` no introduce redondeo adicional.
- **Concurrencia**: `xmin` (RowVersion) en `SupplierCredit`, ya declarado en §7.4 — cualquier inserción de movimiento que compita por el mismo `SupplierCredit` queda protegida por Lock B (§15.1) como primera defensa y `xmin` como segunda defensa, mismo patrón que `PurchasePayable` (§12.3).
- **Auditoría**: cada movimiento genera su propia fila `SupplierCreditAudit` (§20.1) — ya declarado, sin cambios.

### 13.6 Destino financiero del reembolso — flujo transaccional completo (corregido — ver §6.4bis/§6.4ter/§6.4quater/§6.4quinquies)

**Orden de persistencia dentro de una sola transacción** (`RegisterRefund`, §16.1) — el orden autoritativo, con los bloqueos de fila obligatorios, está definido en §6.4quater; resumen:

1. Adquirir Lock B (`SupplierCreditId`) — protege exclusivamente el crédito, nunca el destino/cuenta/sesión (§6.4quater).
2. Recargar `SupplierCredit` y revalidar `AvailableAmount` (§15.5).
3. Cargar y **bloquear** `CompanyFinancialDestination` (`SELECT ... FOR SHARE`) por `FinancialDestinationId`; validar bajo el bloqueo: existe, mismo `TenantId`/`CompanyId`, `IsActive=true`, configuración estructural completa, moneda compatible (`SC-020`/`SC-021`/`SC-022`/`SC-025`, §21).
4. Cargar y **bloquear** `Account` (`SELECT ... FOR SHARE`) por `AccountingAccountId` del destino; validar bajo el bloqueo: mismo tenant/company, `IsActive=true`, `AllowsPosting=true` (`SC-023`/`SC-024`, §21).
5. Validar `PaymentMethod.RequiresReference` contra `ExternalReference` recibida (§6.4quinquies) — obligatoria solo si el método real lo exige.
6. Si `DestinationTypeCode = CASH_REGISTER`: resolver la `CashSession` activa compatible de `CompanyFinancialDestination.CashRegisterId` y **bloquearla** (`FOR SHARE`) — sin sesión activa bloqueada, `SC-027` (§21, corrección 6); el usuario nunca escribe manualmente `CashSessionId`.
7. Crear `SupplierCreditMovement(Refund, Amount)`; recalcular `AvailableAmount` (§13.5).
8. Crear `SupplierCreditRefundTransaction(REFUND_RECEIVED)` vinculada al movimiento anterior (`SupplierCreditMovementId`), con `FinancialDestinationId`, **`AccountingAccountId` congelado desde `CompanyFinancialDestination.AccountingAccountId` validado en el paso 4** (§6.4bis), `PaymentMethodCode`, snapshots (§6.4) — todo congelado en este instante.
9. Si el destino resuelto es `CASH_REGISTER`: crear el `CashMovement` real dentro de la sesión bloqueada (paso 6) y persistir `CashSessionId`/`CashMovementId` en la transacción financiera del paso 8 — nunca se duplica el ingreso de caja durante un retry (mismo mecanismo de idempotencia de §16.2, `ClientRequestId`/`PayloadHash` incluyen `FinancialDestinationId`).
10. Generar un único evento de dominio (`SupplierCreditRefundedEvent`) que originará el único `PostingFact` autoritativo, usando `SupplierCreditRefundTransaction.AccountingAccountId` recién congelado (§19.1ter).
11. `SaveChangesWithSequenceRetryAsync` único → `CommitAsync` único.

Si cualquiera de los pasos 3 a 10 falla, **ROLLBACK completo** — no cambian saldo del crédito, transacción financiera, caja, contabilidad, auditoría ni idempotencia confirmada (§4.3).

**Reversa (`ReverseRefund`)** — secuencia obligatoria de bloqueos (corrección residual 10, alineada con §6.4quinquies):

1. Adquiere `Lock B` por `SupplierCreditId`.
2. Carga y **bloquea** (`SELECT ... FOR SHARE`) el `SupplierCreditRefundTransaction(REFUND_RECEIVED)` original.
3. Bajo ese bloqueo, verifica que no exista previamente una `SupplierCreditRefundTransaction(REFUND_REVERSED)` asociada mediante `OriginalTransactionId` (`SC-011` si ya existe).
4. Hereda del `REFUND_RECEIVED` original todos los datos financieros congelados requeridos para la reversa (`FinancialDestinationId`, `AccountingAccountId`/`AccountingAccountCodeSnapshot`, `PaymentMethodCode`, `Amount`, `CurrencyCode`, `CashRegisterId`/`BankInstitutionCode`/`BankAccountIdentifierNormalized` cuando aplique).
5. **No** bloquea ni revalida el estado activo actual de `CompanyFinancialDestination`, `PaymentMethod` ni `Account` — no vuelve a resolverlos (§6.4quinquies).
6. Si el `REFUND_RECEIVED` original corresponde a caja: utiliza la `CashRegisterId` autoritativa heredada, resuelve una `CashSession` activa y compatible, y la **bloquea** (`FOR SHARE`).
7. Si no existe una `CashSession` activa y compatible: responde `SC-027`, ejecuta rollback completo, no persiste ningún efecto parcial.

No se crea ningún advisory lock adicional — el bloqueo del `REFUND_RECEIVED` original y de la `CashSession` (cuando aplica) son bloqueos de fila PostgreSQL estándar (`FOR SHARE`), igual mecanismo que §6.4quater. `REFUND_REVERSED.ExternalReference` queda `null`; la evidencia original se conserva mediante `OriginalTransactionId`; la reversa nunca vuelve a resolver `AccountingAccountId`/`AccountingAccountCodeSnapshot` desde el destino o la cuenta vigente.

**Desactivación concurrente del destino/cuenta/sesión**: resuelta mediante los bloqueos de fila (`FOR SHARE`) del paso 3/4/6 — ver mecanismo completo, incluida la distinción entre `xmin` y bloqueo de fila, en §6.4quater. Se elimina la afirmación previa de que la sola revalidación bajo `Lock B` bastaba.

---

## 14. Inventario, costo y trazabilidad por línea

### 14.1 Movimiento de autorización

```
StockMovementType = PurchaseReturn (valor 7, ya existente en el enum)
Quantity           = −PurchaseReturnDetail.Quantity                      (negativa — salida)
UnitCost           = PurchaseReturnDetail.UnitCost (= PurchaseInvoiceDetail.LandedUnitCost congelado)
WarehouseId        = PurchaseReturnDetail.WarehouseId (congelado de la línea original)
SourceDocId        = PurchaseReturn.Id                                   (nunca la factura — corrige el patrón de CancelPurchaseUseCases)
SourceDocType       = "PurchaseReturn"
SourceDocLineId     = PurchaseReturnDetail.Id                             (nueva columna genérica, §10.3)
Fecha               = DateTime.UtcNow (fecha de autorización)
Usuario             = uid autorizante
```

- **Semántica del enum diferenciada por `SourceDocType`/`SourceDocId`** (nunca por el valor del enum en sí): `CancelPurchaseUseCases` seguirá usando `PurchaseReturn` con `SourceDocType="PurchaseInvoice"` para su reversión total existente (sin cambios — es infraestructura ya en producción y fuera del alcance de P0-02 modificarla); `PurchaseReturn` (nuevo) siempre usa `SourceDocType="PurchaseReturn"`. Ambos casos son distinguibles sin ambigüedad por consulta.
- **Validación de existencia disponible**: se reutiliza el guard ya existente de `CurrentStock.ApplyMovement` (lanza si `newQty < 0`); el handler lo captura y lo traduce a `Result.ValidationFailure` con código `PR-005` (mismo patrón ya usado por `AuthorizeSalesReturnHandler` para su caso equivalente).
- **Política ante stock insuficiente**: la autorización completa se rechaza (no hay autorización parcial de líneas) — todo o nada, consistente con §4.3.
- **Cancelación**: movimiento inverso (`Quantity = +PurchaseReturnDetail.Quantity`, mismo `UnitCost`, `SourceDocId = PurchaseReturn.Id`, `SourceDocType = "PurchaseReturn"`, nueva fila de kardex) — nunca se borra el movimiento original (regla de no eliminación física).

### 14.2 Política definitiva de bodega y stock insuficiente en la bodega original (resuelve bloqueante 6)

**Decisión de negocio cerrada** (confirmada directamente por el propietario del ERP, ver §27): la devolución sale **exclusivamente** de la misma bodega registrada en cada detalle de la factura de compra original — `PurchaseReturnDetail.WarehouseId = PurchaseInvoiceDetail.WarehouseId` (snapshot congelado, §7.2), nunca seleccionable ni editable por el usuario (la UI la muestra como dato informativo de solo lectura). Una `PurchaseReturn` puede contener líneas de distintas bodegas cuando los detalles originales de la factura ingresaron en bodegas distintas — cada línea usa **su propia** bodega congelada, nunca una bodega común elegida para todo el documento.

**Validación bajo lock**: `AuthorizePurchaseReturnUseCases` valida, para cada línea y bajo Lock A (§15.5), que `CurrentStock.Quantity` en `(TenantId, CompanyId, ItemId, PurchaseReturnDetail.WarehouseId)` sea `≥ PurchaseReturnDetail.Quantity` — nunca se consulta ni se permite tomar existencia de una bodega distinta a la congelada en la línea, ni siquiera si esa otra bodega tiene stock suficiente. Los locks de stock (los ya existentes de `CurrentStock`/kardex, más el Lock A de esta devolución) se adquieren usando la bodega original de cada línea. El movimiento de salida (§14.1) usa exactamente ese `WarehouseId` congelado; la reversa (`Cancel`) reingresa exactamente a esa misma bodega — nunca a una distinta.

**Todo o nada por artículo transferido**: si el artículo fue trasladado a otra bodega después de la compra y ya no queda stock suficiente en la bodega original, la autorización se bloquea completa (ninguna línea se autoriza parcialmente, §4.3) — el usuario debe realizar un traslado de inventario hacia la bodega original **antes** de poder autorizar la devolución; el diseño no ofrece ni permite una ruta alterna que tome existencia de otra bodega automáticamente, ni que distribuya una misma línea entre varias bodegas. La creación del `Draft` sí se permite sin stock suficiente (§9.1) — solo la autorización lo exige.

**Código de error estable — `PR-005` (ampliado, no un código nuevo — mismo código ya usado para "stock insuficiente", ahora con el detalle obligatorio por línea/bodega)**:

| Campo del error | Valor |
|---|---|
| Operación | `Authorize` |
| Línea | `PurchaseReturnDetail.Id` (y su `OriginalInvoiceDetailId` para trazabilidad) |
| Bodega | `PurchaseReturnDetail.WarehouseId` (la bodega original congelada — nunca otra) |
| Existencia actual | `CurrentStock.Quantity` en esa `(ItemId, WarehouseId)` en el momento de la revalidación bajo lock |
| Cantidad solicitada | `PurchaseReturnDetail.Quantity` |
| Resultado HTTP futuro | 422 |
| Datos que no deben cambiar | Inventario, CxP, crédito, `PurchaseReturn.Status` (permanece `Draft`) — nada se aplica, consistente con §4.3 y con la fila `PR-005` de §21 |

El mensaje de negocio expuesto al usuario indica la bodega, la cantidad disponible y la cantidad solicitada, sin exponer nombres de índices ni excepciones internas de `CurrentStock.ApplyMovement` — mismo criterio de `IDatabaseExceptionTranslator`/`ExceptionMiddleware` que el resto del catálogo (§21).

---

## 15. Concurrencia, locks y orden de adquisición

### 15.1 Locks nuevos

| Lock | Namespace (hash Postgres) | Clave | Protege |
|---|---|---|---|
| **A — Financial Lock de factura de compra** | `"PurchaseInvoice.FinancialLock"` (namespace propio, distinto de `"SalesReturn.Lock"` y de `IJournalEntryRepository.AcquireIdempotencyLockAsync`) | `(TenantId, PurchaseInvoiceId)` | Toda mutación que lea o escriba `PurchasePayable`/`IssuedWithholding` asociados a esa factura |
| **B — Lock de crédito de proveedor** | `"SupplierCredit.Lock"` (namespace propio) | `(TenantId, SupplierCreditId)` | Toda mutación de `SupplierCredit`/`SupplierCreditMovement` |

### 15.2 Handlers que deben adquirir el Lock A (existentes y nuevos)

| Handler | Estado actual | Cambio requerido |
|---|---|---|
| `AuthorizePurchaseReturnUseCases` (nuevo) | — | Adquiere Lock A por `PurchaseInvoiceId` de la devolución |
| `CancelPurchaseReturnUseCases` (nuevo) | — | Adquiere Lock A (mismo `PurchaseInvoiceId`) + Lock B (si existe `SupplierCredit` asociado) |
| `RegisterPaymentCommandHandler` (existente) | Sin transacción explícita, sin lock | **Modificación necesaria**: abrir transacción explícita (`IUnitOfWork.BeginTransactionAsync`) y adquirir Lock A por **cada** `PurchaseInvoiceId` distinto involucrado (puede pagar varias CxP a la vez), en orden ascendente de `PurchaseInvoiceId` |
| `ReversePaymentCommandHandler` (existente) | Igual | Igual |
| `IssueWithholdingHandler` (existente) | Sin transacción explícita, sin lock | **Modificación necesaria**: abrir transacción explícita + adquirir Lock A por `PurchaseInvoiceId` antes de emitir |
| `CancelWithholdingHandler` (existente) | Igual | Igual |
| `CancelPurchaseHandler` (existente) | Sin transacción explícita, sin lock (hallazgo adicional de la auditoría) | **Modificación necesaria**: abrir transacción explícita + adquirir Lock A por `PurchaseInvoiceId` |

Esta lista responde directamente al requisito del encargo: los handlers existentes que hoy pueden mutar `PurchasePayable`/`IssuedWithholding` de la misma factura deben compartir el mismo lock para cerrar las carreras cruzadas — no solo el nuevo handler de devolución.

### 15.3 Handlers que deben adquirir el Lock B

| Handler | Función |
|---|---|
| `ApplySupplierCreditUseCases` (nuevo) | Aplicar crédito a otra CxP — adquiere Lock B por `SupplierCreditId`, **y** Lock A por el `PurchaseInvoiceId` del `PurchasePayable` destino |
| `RegisterSupplierCreditRefundUseCases` (nuevo) | Registrar reembolso — adquiere solo Lock B (no toca ningún `PurchasePayable`); además bloquea por fila (`FOR SHARE`) `CompanyFinancialDestination`/`Account`/`CashSession` según §6.4quater — `Lock B` no protege esas filas por sí solo |
| `ReverseSupplierCreditApplicationUseCases` (nuevo) | Revertir aplicación — adquiere Lock B **y** Lock A del `PurchasePayable` afectado |
| `ReverseSupplierCreditRefundUseCases` (nuevo) | Revertir reembolso (corrección residual 10) — primero `Lock B` por `SupplierCreditId` y después `FOR SHARE` del `SupplierCreditRefundTransaction(REFUND_RECEIVED)` original, **antes** de comprobar bajo ese bloqueo la ausencia de una `REFUND_REVERSED` previa (`OriginalTransactionId`); hereda del original `FinancialDestinationId`/`AccountingAccountId`/`PaymentMethodCode`/`Amount`/`CurrencyCode` sin bloquear ni revalidar la actividad vigente de `CompanyFinancialDestination`/`PaymentMethod`/`Account`; si el original corresponde a caja, bloquea además (`FOR SHARE`) la `CashSession` activa compatible de la misma `CashRegisterId` heredada — sin sesión, `SC-027` (§6.4quinquies) |
| `CancelPurchaseReturnUseCases` (nuevo) | Si hay `SupplierCredit` asociado, adquiere Lock B para revalidar `AvailableAmount == OriginalAmount` bajo lock |

### 15.4 Orden fijo de adquisición (evita deadlock)

1. **Siempre Lock A antes que Lock B.** Si una operación necesita ambos (aplicar crédito a otra CxP, revertir aplicación, cancelar devolución con crédito íntegro), adquiere primero A y luego B.
2. Si una operación necesita **más de un Lock A** (caso `RegisterPaymentCommandHandler` con múltiples facturas en un mismo pago), se adquieren en **orden ascendente de `PurchaseInvoiceId` (comparación de `Guid` como texto)** — orden determinista, evita ciclos entre dos pagos que involucren las mismas dos facturas en orden distinto.
3. No existe ningún escenario en este diseño que requiera más de un Lock B simultáneo (una operación siempre opera sobre un único `SupplierCredit`).

### 15.5 Revalidaciones tras el lock

| Operación | Qué se revalida bajo lock |
|---|---|
| `Authorize` | Cantidad remanente por línea (§10.2); `BalanceDue` actual (§11.2); `IssuedWithholding.Status != Issued` (§17) |
| `Cancel` (devolución) | `SupplierCredit.AvailableAmount == OriginalAmount` (si existe) |
| `ApplyToPayable` | `SupplierCredit.AvailableAmount` actual; `PurchasePayable.BalanceDue` actual del destino; proveedor/moneda |
| `RegisterRefund` | `SupplierCredit.AvailableAmount` actual |
| `RegisterPayment`/`ReversePayment` (existentes) | `PurchasePayable.BalanceDue` actual (ya lo hacía vía guard de dominio; ahora además protegido por el lock, no solo por el guard optimista) |
| `IssueWithholding`/`CancelWithholding` (existentes) | Estado actual de `PurchasePayable`/factura antes de mutar |

### 15.6 Errores de concurrencia

Todo conflicto de `xmin` capturado (`DbUpdateConcurrencyException`) se traduce vía `IDatabaseExceptionTranslator` (infraestructura ya existente) a `Result.Conflict` con código de negocio — nunca 500. Ver catálogo completo en §21.

### 15.7 TOCTOU de retención (resuelve hallazgo #9)

La ventana "consultar retención → decidir bloquear/permitir → autorizar devolución" se cierra porque `AuthorizePurchaseReturnUseCases` consulta `GetWithholdingByPurchaseIdAsync` **después** de adquirir el Lock A, y `IssueWithholdingHandler` **también** adquiere el mismo Lock A antes de emitir (§15.2). Ambas operaciones quedan serializadas por la misma clave `(TenantId, PurchaseInvoiceId)`: si la devolución adquiere el lock primero, la emisión de retención espera hasta que la devolución termine (commit o rollback) antes de poder emitir; si la emisión de retención adquiere el lock primero, la devolución espera y al adquirir el lock revalida el estado de retención ya actualizado — nunca hay una lectura obsoleta entre ambas.

---

## 16. Transacciones e idempotencia

### 16.1 Frontera transaccional por operación

| Operación | Transacción | Locks | Orden exacto |
|---|---|---|---|
| `Authorize` (PurchaseReturn) | Propia (`IUnitOfWork.BeginTransactionAsync`) | A | abrir tx → adquirir Lock A → recargar `PurchasePayable`/`IssuedWithholding`/línea de factura → revalidar (§15.5) → verificar idempotencia (`AuthorizeClientRequestId`, §16.2) → `PurchaseReturnSequence.CaptureNextAsync(tenantId, companyId, ct)` (participa en la **misma** transacción/`DbContext`/conexión ya abierta por este handler — nunca abre ni confirma transacción propia; corrección de diseño explícita, bloqueante 4 de la tercera revisión, detalle completo en §7.1bis) → `PurchaseReturn.Authorize(returnNumber)` congela líneas y calcula `HistoricalCostTotal`/`CostVarianceTotal` (§19.1bis) → `StockRepository.AppendMovementAsync` (salida) → `PurchasePayable.ApplyReturnCredit()` → si excedente > 0, crear `SupplierCredit` → `SaveChangesWithSequenceRetryAsync` (persiste, en el mismo `SaveChanges`, el incremento de `PurchaseReturnSequence.CurrentSeq` junto con el resto de efectos) → `CommitAsync` (evento de dominio dispara `PurchaseReturnAudit` + `PurchaseReturnAuthorizedPostingTranslator` de forma síncrona dentro del mismo `SaveChanges`, infraestructura FROZEN); si la transacción hace `ROLLBACK` en cualquier punto posterior, el incremento de secuencia se revierte junto con todo lo demás — el número nunca queda consumido sin que la autorización completa se confirme |
| `Cancel` (PurchaseReturn) | Propia | A (+ B si hay crédito) | abrir tx → adquirir Lock A (y B si aplica) → recargar → revalidar `SupplierCredit.AvailableAmount == OriginalAmount` → movimiento inverso de inventario → `PurchasePayable.ReverseReturnCredit()` → si hay `SupplierCredit`, movimiento `SourceReturnCancelled` → `PurchaseReturn.Cancel()` → `SaveChangesWithSequenceRetryAsync` → `CommitAsync` |
| `ApplyToPayable` (SupplierCredit) | Propia | A (destino) + B | abrir tx → adquirir A luego B → recargar ambos agregados → revalidar → `SupplierCredit.ApplyToPayable()` → `PurchasePayable.ApplySupplierCredit()` (destino) → `SaveChangesWithSequenceRetryAsync` → `CommitAsync` |
| `RegisterRefund` (corrección residual 10 — locks explícitos, ya no "Locks: B" a secas) | Propia | `Lock B` + `FOR SHARE CompanyFinancialDestination` + `FOR SHARE Account` + `FOR SHARE CashSession` (solo si `CASH_REGISTER`) | abrir tx → adquirir Lock B → recargar/revalidar `SupplierCredit` → cargar y bloquear (`FOR SHARE`) `CompanyFinancialDestination`, revalidar (activo, tenant/company, tipo, moneda) → cargar y bloquear (`FOR SHARE`) `Account`, revalidar (activo, postable, tenant/company) → validar `PaymentMethod.RequiresReference` → si `CASH_REGISTER`, resolver y bloquear (`FOR SHARE`) sesión activa → `SupplierCredit.RegisterRefund()` (crea `SupplierCreditMovement` + `SupplierCreditRefundTransaction` con `AccountingAccountId` congelado + `CashMovement` si aplica, §13.6/§6.4quater) → `SaveChangesWithSequenceRetryAsync` → `CommitAsync` |
| `ReverseApplication` | Propia | A (+B para reversa de aplicación) | mismo patrón que su operación directa, invertido |
| `ReverseRefund` (corrección residual 10) | Propia | `Lock B` + `FOR SHARE REFUND_RECEIVED original` + `FOR SHARE CashSession activa` (solo si corresponde a caja) — **no** repite `FOR SHARE` sobre `CompanyFinancialDestination`/`Account`, hereda ambos ya congelados | 1. Adquirir `Lock B` por `SupplierCreditId`. 2. Cargar y **bloquear** (`FOR SHARE`) el `SupplierCreditRefundTransaction(REFUND_RECEIVED)` original. 3. Bajo ese bloqueo, verificar ausencia de una `REFUND_REVERSED` previa vía `OriginalTransactionId` (`SC-011` si ya existe). 4. Heredar del original todos los datos financieros congelados (`FinancialDestinationId`, `AccountingAccountId`/`AccountingAccountCodeSnapshot`, `PaymentMethodCode`, `Amount`, `CurrencyCode`, `CashRegisterId`/`BankInstitutionCode`/`BankAccountIdentifierNormalized` cuando aplique). 5. No bloquear ni revalidar el estado activo actual de `CompanyFinancialDestination`, `PaymentMethod` ni `Account`. 6. Si el original corresponde a caja: usar la `CashRegisterId` heredada, resolver una `CashSession` activa compatible y **bloquearla** (`FOR SHARE`). 7. Si no existe `CashSession` activa y compatible: `SC-027`, rollback completo, ningún efecto parcial. 8. Crear `SupplierCreditMovement(ReversalOfRefund)` + `SupplierCreditRefundTransaction(REFUND_REVERSED)` (con `ExternalReference=null`) + `CashMovement` compensatorio si aplica → `SaveChangesWithSequenceRetryAsync` → `CommitAsync` |
| Vínculo de NC (`RegisterAndLinkSupplierCreditNote`) | Propia (documental) | Ninguno (no compite con inventario/CxP/crédito) | abrir tx → registrar/obtener `PurchaseReceptionDocument` (o reutilizar uno existente por `AccessKey`) → validar proveedor/moneda/duplicidad → `PurchaseReturn.FiscalStatus = SupplierCreditNoteRegistered` → `SaveChangesAsync` → `CommitAsync` |

### 16.2 Idempotencia obligatoria por operación (resuelve bloqueante 2)

**Corrección de diseño explícita**: `ClientRequestId` es **obligatorio** (nunca "recomendado") en las 8 operaciones siguientes: crear devolución, autorizar, cancelar, aplicar crédito, revertir aplicación, registrar reembolso, revertir reembolso, registrar/vincular NC. Un endpoint que reciba una de estas 8 operaciones sin `ClientRequestId` la rechaza con `422` (validación de entrada, FluentValidation — `B-V1`) antes de tocar ningún agregado. Se sigue usando unicidad + clave de solicitud dentro de los propios agregados/movimientos — **no** se crea una infraestructura de idempotencia genérica global, consistente con el mandato y con la ausencia actual de ese mecanismo en el resto del ERP.

**Corrección de diseño explícita (bloqueante 1 de la tercera revisión)**: la versión previa dejaba la huella de `Authorize`/`Cancel`/vínculo de NC sin el identificador del agregado objetivo (`PurchaseReturnId`), asumiendo implícitamente que la unicidad de la columna bastaba para evitar colisiones entre devoluciones distintas. Es insuficiente: la búsqueda del paso 3 localiza una fila por `(TenantId, ClientRequestId)` **antes** de saber si esa fila pertenece realmente al `PurchaseReturnId` que el cliente cree estar operando. Sin `PurchaseReturnId` dentro del propio `RequestPayloadHash`, dos solicitudes con el mismo `ClientRequestId` dirigidas a dos devoluciones distintas producirían el **mismo hash** (ambas "sin payload variable") y la segunda se trataría erróneamente como un reintento legítimo de la primera, devolviendo el snapshot de una devolución que no es la solicitada. Se corrige incluyendo expresamente `OperationType` + el identificador del agregado objetivo en la huella de las tres operaciones, de modo que una reutilización cruzada del mismo `ClientRequestId` contra un agregado distinto produzca un `RequestPayloadHash` distinto y sea rechazada como conflicto de idempotencia (`PR-012`), nunca reconstruida como si fuera la misma operación.

**Sobre `ExpectedVersion`/token de concurrencia**: el contrato de estas 8 operaciones no expone un campo `ExpectedVersion` de concurrencia optimista provisto por el cliente — la concurrencia se resuelve del lado servidor mediante el Lock A/B (§15) más `xmin` como segunda defensa (§12.3, §13.5), nunca mediante un token que el cliente deba leer y reenviar. Por tanto, el "token de concurrencia requerido por el contrato" para estas operaciones es, explícitamente, el propio identificador del agregado objetivo (`PurchaseReturnId`/`SupplierCreditId`) combinado con el lock adquirido en el momento de ejecutar el efecto — no existe un campo adicional que declarar. Esta decisión se documenta aquí de forma explícita para que no quede como un vacío implícito: no se introduce un campo `ExpectedVersion` en la API pública de `PurchaseReturn`/`SupplierCredit`.

**Mecanismo común a las 8 operaciones**:

1. El endpoint recibe `ClientRequestId` (Guid) obligatorio + el payload de negocio de la operación (incluido el identificador del agregado objetivo cuando la operación actúa sobre uno ya existente).
2. El servidor calcula `RequestPayloadHash` = hash determinista (SHA-256, `CultureInfo.InvariantCulture`, campos canonicalizados en orden fijo — nunca dependiente del orden de serialización JSON recibido) sobre `OperationType` + el **payload relevante** de esa operación específica, incluyendo siempre el identificador del agregado/recurso objetivo cuando la operación no es de creación (columna "Huella de contenido" de la tabla siguiente).
3. Antes de ejecutar efectos, se busca una fila existente con el mismo `(TenantId, ClientRequestId)` para esa operación:
   - **No existe** → procede normalmente bajo el lock correspondiente (§15), persiste `ClientRequestId` + `RequestPayloadHash` en la misma transacción que los efectos de negocio.
   - **Existe con el mismo `RequestPayloadHash`** → operación repetida con contenido idéntico (mismo agregado objetivo, mismos datos): se reconstruye/retorna el resultado ya confirmado (mismo `Id`/snapshot ya persistido) **sin volver a ejecutar ningún efecto** — ni inventario, ni CxP, ni crédito, ni contabilidad, ni auditoría duplicada.
   - **Existe con un `RequestPayloadHash` distinto** (incluye el caso de mismo `ClientRequestId` contra un agregado objetivo distinto, porque el identificador del agregado forma parte del hash) → conflicto de idempotencia: se rechaza con `PR-012`/`SC-006` (§21, generalizado a las 8 operaciones), **sin modificar ningún dato** — el resultado original persistido permanece intacto.

| Operación | Alcance de unicidad | Entidad/columna | Agregado objetivo incluido en el hash | Huella de contenido (`RequestPayloadHash` sobre…) | Respuesta ante mismo contenido | Error ante contenido distinto |
|---|---|---|---|---|---|---|
| `CreateDraft` | `(TenantId, CreateClientRequestId)` | `PurchaseReturn.CreateClientRequestId` | No aplica (aún no existe agregado — el objetivo es el `PurchaseInvoiceId` origen, ya incluido) | `OperationType="CreateDraft"` + `PurchaseInvoiceId` + `Reason` + lista canonicalizada (`OriginalInvoiceDetailId`, `Quantity`) ordenada por `OriginalInvoiceDetailId` | Retorna el `Id` del draft ya creado, sin duplicar | `PR-012` |
| `Authorize` | `(TenantId, AuthorizeClientRequestId)` | `PurchaseReturn.AuthorizeClientRequestId` | **Sí — `PurchaseReturnId` obligatorio en el hash** (corrige la versión previa, que dejaba el hash constante sin identificar el agregado) | `OperationType="AuthorizePurchaseReturn"` + `PurchaseReturnId` + `ClientRequestId` — sin más datos variables, porque `Authorize` no recibe payload de negocio adicional; el token de concurrencia del contrato es el propio `PurchaseReturnId` + Lock A (ver nota de `ExpectedVersion` arriba) | Retorna el snapshot ya confirmado (`ReturnNumber`/`AuthorizedSubtotal`/`GrandTotal`/etc.) sin reejecutar efectos | `PR-012` — ahora también cuando el mismo `AuthorizeClientRequestId` se reutiliza contra un `PurchaseReturnId` distinto: el hash difiere porque `PurchaseReturnId` difiere, nunca se reconstruye el resultado del agregado incorrecto |
| `Cancel` | `(TenantId, CancelClientRequestId)` | `PurchaseReturn.CancelClientRequestId` | **Sí — `PurchaseReturnId` obligatorio en el hash** | `OperationType="CancelPurchaseReturn"` + `PurchaseReturnId` + `ClientRequestId` + `CancellationReason` normalizado (trim + colapso de espacios, mismo criterio que el resto de campos de texto libre auditados) | Retorna el resultado ya confirmado (`Status = Cancelled`) sin reejecutar reversas | `PR-012` |
| `ApplyToPayable` | `(TenantId, ClientRequestId)` en `SupplierCreditMovement` | `SupplierCreditMovement.ClientRequestId` (fila `Application`) | Sí — `SupplierCreditId` (agregado) + `TargetPurchasePayableId` (recurso destino), ya presentes | `OperationType="ApplySupplierCredit"` + `SupplierCreditId` + `TargetPurchasePayableId` + `Amount` | Retorna el movimiento ya creado | `SC-006` (generalizado — ver §21) |
| `ReverseApplication` | Igual, fila `ReversalOfApplication` | `SupplierCreditMovement.ClientRequestId` | Sí — `ReversalOfMovementId` identifica inequívocamente el movimiento objetivo (y transitivamente el `SupplierCreditId`) | `OperationType="ReverseSupplierCreditApplication"` + `ReversalOfMovementId` | Retorna la reversa ya creada | `SC-006` |
| `RegisterRefund` | Igual, fila `Refund` | `SupplierCreditMovement.ClientRequestId` + `SupplierCreditRefundTransaction.ClientRequestId` | Sí — `SupplierCreditId` + `FinancialDestinationId` | `OperationType="RegisterSupplierCreditRefund"` + `SupplierCreditId` + `SourcePurchaseReturnId` + `FinancialDestinationId` + `PaymentMethodCode` + `Amount` + `CurrencyCode` + `EffectiveDate` + `ExternalReference` normalizada **o `null`** (obligatoria únicamente si `PaymentMethod.RequiresReference=true`, §6.4quinquies) — nunca incluye `AccountingAccountId` (el comando no lo recibe, se deriva y congela en servidor, §6.4bis) | Retorna el reembolso ya creado (incluida la transacción financiera con su `AccountingAccountId` congelado y, si aplica, `CashMovementId`) | `SC-006` |
| `ReverseRefund` | Igual, fila `ReversalOfRefund` | `SupplierCreditMovement.ClientRequestId` + `SupplierCreditRefundTransaction.ClientRequestId` | Sí — `ReversalOfMovementId` (transitivamente `OriginalTransactionId`) | `OperationType="ReverseSupplierCreditRefund"` + `SupplierCreditId` + `OriginalTransactionId` + `Reason` normalizada — `Amount`/`FinancialDestinationId`/`AccountingAccountId`/`PaymentMethodCode`/`CurrencyCode` se derivan del `REFUND_RECEIVED` original para calcular el hash, **nunca** se reciben del comando (§6.4quinquies) | Retorna la reversa ya creada | `SC-006` |
| Vincular NC | `(TenantId, LinkCreditNoteClientRequestId)` | `PurchaseReturn.LinkCreditNoteClientRequestId` | **Sí — `PurchaseReturnId` + `PurchaseReceptionDocumentId` obligatorios en el hash** (corrige la versión previa, que no identificaba ni el `PurchaseReturnId` ni el documento de NC objetivo) | `OperationType="LinkSupplierCreditNote"` + `PurchaseReturnId` + `PurchaseReceptionDocumentId` + `AccessKey` + `InvoiceNumber` + `IssueDate` + `CurrencyCode` + `TotalAmount` de la NC (el importe se incorpora aquí porque a partir del bloqueante 3, §18.4, la vinculación queda condicionada a su validación monetaria — ver §18.4bis) | Retorna éxito idempotente — el documento ya registrado y el vínculo ya hecho | `PR-012` (consolida el antiguo `PR-013` de versiones previas en un único código para toda la familia de conflictos de idempotencia de `PurchaseReturn`) |

### 16.2bis Carrera entre búsqueda e inserción (resuelve bloqueante 1)

El paso 3 del mecanismo común ("buscar `ClientRequestId` → si no existe, insertar") tiene una ventana de carrera real bajo PostgreSQL/EF Core cuando dos solicitudes con el mismo `ClientRequestId` llegan concurrentemente y ambas superan la búsqueda inicial sin encontrar la fila (ninguna la ha insertado todavía). Aplica a `CreateDraft`, al vínculo de NC, y en general a cualquier operación de la tabla anterior. Algoritmo obligatorio, en este orden exacto:

1. Buscar una fila confirmada por la clave idempotente `(TenantId, ClientRequestId)` de la operación.
2. Si existe, comparar `RequestPayloadHash`.
3. Si el hash coincide, reconstruir/retornar el resultado ya confirmado (rama "mismo contenido" de §16.2).
4. Si el hash difiere, devolver conflicto idempotente (`PR-012`/`SC-006`) sin tocar ningún dato.
5. Si no existe, intentar insertar la fila (o el agregado completo, según la operación) dentro de la transacción y el lock correspondientes (§15, §16.1).
6. Si la inserción falla porque el índice único `(TenantId, ClientRequestId)` fue ganado por otra transacción concurrente (violación de restricción única detectada por `IDatabaseExceptionTranslator`):
   - **No** se devuelve directamente la excepción SQL traducida como error genérico de negocio.
   - Se descarta el `DbContext` de la transacción actual (un `DbContext` cuyo `SaveChanges` fue abortado por PostgreSQL tras un error de restricción queda en un estado transaccional inválido — PostgreSQL aborta la transacción completa ante cualquier error de sentencia; ningún comando adicional es válido sobre esa misma transacción salvo `ROLLBACK`). Se ejecuta `ROLLBACK` explícito de esa transacción y se abre una **transacción nueva** con una **instancia nueva de `DbContext`** (o, si el patrón de la operación reutiliza un `DbContext` de larga vida por request, se re-obtiene una conexión/transacción limpia del mismo `DbContext` tras el `ROLLBACK` — nunca se reintenta un `SaveChanges` adicional sobre la transacción abortada).
   - Con la transacción nueva y limpia, se vuelve a consultar el registro ganador por la clave idempotente `(TenantId, ClientRequestId)` — ahora sí existe, porque fue la otra transacción quien lo insertó primero y ya comiteó.
   - Se compara `RequestPayloadHash` contra el registro ganador.
   - Si coincide, se devuelve el resultado confirmado del registro ganador (rama "mismo contenido").
   - Si difiere, se devuelve conflicto idempotente (`PR-012`/`SC-006`).

Este algoritmo se aplica en particular a `CreateDraft` (inserción del `PurchaseReturn` completo) y a la vinculación de NC (inserción/reutilización de `PurchaseReceptionDocument` + actualización de `PurchaseReturn.LinkCreditNoteClientRequestId`) — ambas siguen el patrón "buscar → si no existe, insertar" descrito en el encargo. Las operaciones que actúan sobre un agregado ya existente identificado por su propio `Id` (`Authorize`, `Cancel`, aplicaciones/reembolsos de crédito) tienen una ventana de carrera estructuralmente menor porque el lock (Lock A/B, §15) ya serializa el acceso al agregado objetivo antes de verificar idempotencia — pero igualmente seguirán el mismo algoritmo de recuperación si la escritura del propio `ClientRequestId` colisiona por una causa distinta al lock (p. ej. un reintento de cliente que llega mientras la transacción anterior todavía no comiteó y el lock ya se liberó por rollback).

### 16.2ter Prueba de concurrencia de idempotencia obligatoria (PostgreSQL real)

Antes de codificar cualquiera de las 8 operaciones idempotentes, debe existir y aprobarse una suite de integración contra PostgreSQL real (no mock/in-memory) que cubra, como mínimo, para `CreateDraft` y para `Authorize` (representativos de "crea agregado nuevo" y "actúa sobre agregado existente bajo lock"):

- Dos solicitudes concurrentes con el **mismo `ClientRequestId` y el mismo payload** → exactamente un efecto de negocio persistido (una sola devolución/un solo movimiento/un solo crédito/una sola aplicación/un solo reembolso/un solo vínculo fiscal/un solo hecho contable), ambas respuestas HTTP devuelven el mismo resultado confirmado.
- Dos solicitudes concurrentes con el **mismo `ClientRequestId` y payload diferente** → una tiene éxito, la otra recibe `PR-012`/`SC-006`, sin ningún efecto duplicado ni parcial.
- Dos solicitudes con **claves distintas** → dos efectos de negocio independientes, cada uno correcto.
- **Commit exitoso sin respuesta al cliente** (se fuerza cortando la conexión después del `COMMIT` pero antes de que la prueba lea la respuesta) seguido de un reintento con el mismo `ClientRequestId`/payload → el reintento retorna el resultado ya confirmado, cero efectos adicionales.
- **Timeout antes del commit** (se fuerza abortando la conexión antes del `COMMIT`) seguido de un reintento con el mismo `ClientRequestId`/payload → el reintento ejecuta la operación completa de cero (ningún efecto parcial de la ejecución abortada quedó persistido).

El resultado de esta suite debe demostrar explícitamente, para cada escenario, que no se duplican: devoluciones, movimientos de inventario, ajustes de CxP, créditos, aplicaciones, reembolsos, vínculos fiscales, ni hechos contables (`PostingFact`). Esta validación es un prerrequisito de implementación obligatorio — no un ítem de backlog —, en el mismo sentido y con el mismo peso que la prueba de secuencia obligatoria de §16.3.

**Prueba adicional obligatoria — Branch Ownership Rule (§5.2)**: la misma suite (o una equivalente contra PostgreSQL real) debe verificar, como mínimo: (a) `CreateDraft` persiste `PurchaseReturn.BranchId` igual al `ICurrentBranch.BranchId` del contexto del handler, nunca un valor distinto aunque el payload HTTP intente enviarlo (el comando no expone la propiedad, §24); (b) `Authorize()` crea `SupplierCredit.BranchId` idéntico al `PurchaseReturn.BranchId` de origen, sin excepción; (c) un usuario autorizado en negocio pero sin acceso a la sucursal del documento recibe rechazo de `BranchScopeBehavior`/`IBranchAccessGuard` antes de tocar cualquier efecto, para `Authorize`, `Cancel`, `ApplyToPayable`, `RegisterRefund`, `ReverseRefund`.

**Comportamiento ante timeout antes del commit**: si el cliente no recibe respuesta porque la conexión se cortó antes del `CommitAsync`, la transacción de PostgreSQL nunca se confirmó — no existe fila con ese `ClientRequestId`. Un reintento con el mismo `ClientRequestId` y el mismo contenido ejecuta la operación completa de cero (comportamiento idéntico a la primera vez, porque nunca hubo "primera vez" persistida).

**Comportamiento tras commit exitoso sin respuesta al cliente**: la fila con `(ClientRequestId, RequestPayloadHash)` ya está persistida. Un reintento con el mismo `ClientRequestId` y el mismo contenido cae en la rama "mismo contenido" del mecanismo común: retorna el resultado ya confirmado, sin duplicar ningún efecto de inventario/CxP/crédito/contabilidad/auditoría. Ver también §16.4 (marco general de reintento tras timeout, aplicable a las 8 operaciones).

Se prefiere unicidad + clave de solicitud dentro de los propios agregados/movimientos, **no** una infraestructura de idempotencia genérica global — consistente con el mandato y con la ausencia actual de ese mecanismo en el resto del ERP.

### 16.3 Validación previa obligatoria (no backlog, prerrequisito de implementación) — EJECUTADA EN FASE 0, RESULTADO: PASS

**Antes de codificar `AuthorizePurchaseReturnUseCases`**, debía ejecutarse y aprobarse una prueba de integración contra PostgreSQL real que reprodujera exactamente el patrón compuesto: transacción explícita del handler (`IUnitOfWork.BeginTransactionAsync`) → advisory lock → `SaveChangesWithSequenceRetryAsync` → forzar un conflicto de secuencia (dos escritores concurrentes sobre el mismo `CurrentStock`) dentro de esa transacción ambiente, y verificar que el reintento in-process (`RecoverFromConflictAndRetrackAsync`) efectivamente reintenta con éxito **sin que PostgreSQL haya abortado la transacción completa** tras el primer error. Esta validación era un requisito de implementación obligatorio, explícitamente **no** un ítem de backlog — bloqueaba el inicio de la fase de código de `Authorize`/`Cancel`/`ApplyToPayable` hasta resolverse.

**Resultado empírico (Fase 0 del plan de implementación, prueba `backend/src/ERP.Infrastructure.Tests/Persistence/PurchaseReturnSequenceTransactionInteractionTests.cs`, ejecutada contra PostgreSQL real vía Testcontainers.PostgreSql, 4/4 corridas deterministas, sin excepción `PostgresException 25P02`)**: el reintento in-process **sí se recupera con éxito** dentro de la misma transacción explícita ambiente. Causa raíz confirmada: EF Core/Npgsql crea automáticamente un `SAVEPOINT` implícito cuando `SaveChangesAsync` se ejecuta dentro de una transacción externa ya abierta por el llamador (comportamiento por defecto del provider, activo hoy en todo el ERP, sin código adicional). En consecuencia, de las dos alternativas que este documento dejaba planteadas — (a) reabrir la transacción completa del handler, o (b) `SAVEPOINT` manual alrededor del `SaveChanges` interno — **ninguna requiere código nuevo**: el comportamiento (b) ya está provisto automáticamente por el framework. `AuthorizePurchaseReturnUseCases` reutilizará, sin modificación, la misma composición ya usada por `AuthorizeSalesReturnUseCases` (`BeginTransactionAsync` → advisory lock → `SaveChangesWithSequenceRetryAsync` → `CommitAsync`), sin `SAVEPOINT` manual y sin reapertura de transacción. Esta validación queda cerrada como prerrequisito satisfecho — `PHASE_0_ACCEPTED: YES`.

### 16.4 Resultado ante reintento tras timeout

Si el cliente HTTP reintenta tras un timeout de red (sin saber si el commit se confirmó), el mecanismo de idempotencia de §16.2 garantiza uno de dos resultados deterministas: (1) si el commit original se confirmó, el reintento con el mismo `ClientRequestId`/`Id` retorna el resultado ya confirmado sin duplicar ningún efecto; (2) si el commit original falló, el reintento ejecuta la operación completa de cero (el estado previo no cambió). Nunca hay un tercer resultado de "duplicado parcial".

### 16.5 Pruebas PostgreSQL obligatorias del destino financiero del reembolso (prerrequisito de implementación, no backlog — mismo peso que §16.2ter/§16.3)

Antes de codificar `RegisterSupplierCreditRefundUseCases`/`ReverseSupplierCreditRefundUseCases`, debe existir y aprobarse una suite de integración contra PostgreSQL real que cubra, como mínimo:

1. Reembolso bancario válido (`DestinationTypeCode=BANK_ACCOUNT`) — efectos correctos en `SupplierCreditMovement`, `SupplierCreditRefundTransaction`, `PostingFact` (§19.1ter).
2. Reembolso hacia `FinancialDestinationId` inexistente → `SC-020`, ningún efecto.
3. Destino de otro tenant → `SC-020`.
4. Destino de otra company (mismo tenant) → `SC-020`.
5. Destino inactivo (`IsActive=false`) → `SC-021`, ningún efecto.
6. `AccountingAccountId` del destino no postable → `SC-024`, ningún efecto (validado en alta del destino y revalidado en el reembolso).
7. Moneda del reembolso distinta de la del destino → `SC-025`.
8. Reembolso en caja con `CashSession` activa (`DestinationTypeCode=CASH_REGISTER`) — `CashMovement` real creado, vinculado.
9. Reembolso en caja sin `CashSession` activa → `SC-027`, ningún efecto (ni siquiera el movimiento de crédito).
10. Mismo `ClientRequestId` y mismo payload (incluido `FinancialDestinationId`) → resultado idéntico, sin duplicar.
11. Mismo `ClientRequestId` y payload distinto (p. ej. `FinancialDestinationId` diferente) → `SC-006`, sin efecto.
12. Dos solicitudes concurrentes del mismo reembolso → exactamente un efecto persistido.
13. Commit exitoso sin respuesta al cliente, seguido de reintento → resultado ya confirmado, cero efectos adicionales.
14. Reversa válida (`ReverseRefund`) — hereda destino/cuenta contable histórica/moneda/importe/método (§6.4bis/§6.4quinquies), `CashMovement` compensatorio si aplica.
14bis. Reversa válida después de que la cuenta contable del destino fue desactivada o reemplazada por otra (`CompanyFinancialDestination.AccountingAccountId` distinto al usado en el ingreso) → la reversa usa exactamente la cuenta congelada del `REFUND_RECEIVED` original, nunca la vigente.
15. Segunda reversa del mismo `REFUND_RECEIVED` → `SC-011`, ningún efecto adicional.
16. Dos reversas concurrentes del mismo `REFUND_RECEIVED` → una tiene éxito, la otra `SC-011`/`SC-010` según el punto exacto de conflicto, nunca dos reversas persistidas.
17. Intento de cambiar el destino/cuenta contable/moneda/importe/método en la reversa → rechazado a nivel de contrato (el comando de reversa no acepta esos campos, §6.4quinquies) — verificado por ausencia de parámetro, no por validación en runtime.
18. Rollback después de crear el `SupplierCreditMovement` pero antes de `SupplierCreditRefundTransaction` (falla simulada) → ningún efecto persistido, ni saldo del crédito modificado.
19. Rollback después de crear el `CashMovement` pero antes del `PostingFact` → ningún efecto persistido, incluida la caja.
20. Rollback antes del asiento contable → ningún `PostingFact` huérfano, ningún efecto parcial.
21. Ausencia de FK circular: consulta directa confirma que `SupplierCreditMovement` no tiene columna de referencia hacia `SupplierCreditRefundTransaction` en ningún escenario.
22. Unicidad 1:1 entre `SupplierCreditRefundTransaction` y `SupplierCreditMovement` — un segundo intento de vincular el mismo movimiento falla por `UNIQUE (TenantId, CompanyId, SupplierCreditMovementId)`, traducido a `SC-029`.
23. Reporte neto (§19.6) excluye correctamente ingresos revertidos al agrupar por destino/proveedor.
24. Separación correcta de reportes por destino, proveedor, moneda, método y fecha (§19.6) — verificado con datos de al menos dos destinos y dos monedas distintas.
26. **Reversa de reembolso de caja sin sesión activa** (nueva — corrección 6, §12.3): existe un `REFUND_RECEIVED` válido en caja; no existe una `CashSession` activa compatible al solicitar `ReverseRefund`; la operación responde `SC-027`; no se crea `SupplierCreditMovement(ReversalOfRefund)`; no se crea `SupplierCreditRefundTransaction(REFUND_REVERSED)`; no se crea `CashMovement`; no se crea `PostingFact` inverso; no se confirma `SupplierCreditAudit` de reversa; no queda idempotencia confirmada (`ClientRequestId` de la reversa no persiste); el saldo del crédito (`AvailableAmount`) permanece sin cambios; el `REFUND_RECEIVED` original permanece intacto y activo (sin fila `REFUND_REVERSED` asociada); un intento posterior con sesión válida ejecuta la reversa correctamente con el mismo `ClientRequestId` (recuperación idempotente estándar, §16.2/§16.4 — el intento fallido no dejó ningún registro con ese `ClientRequestId`, por lo que el reintento no es un "mismo contenido ya confirmado" sino la primera ejecución real).

**Total de pruebas obligatorias: 26** (no 24 — se corrige explícitamente el conteo: los 24 escenarios originales, más el escenario 14bis de cuenta contable desactivada/reemplazada — corrección 1 — y el escenario 26 de reversa en caja sin sesión activa — corrección 6). Cada prueba debe verificar explícitamente ausencia de duplicación y atomicidad de todos los efectos (movimiento de crédito, transacción financiera, movimiento de caja, hecho contable, auditoría).

---

## 17. Retenciones

- Bloqueo: `AuthorizePurchaseReturnUseCases` invoca `IPurchaseInvoiceRepository.GetWithholdingByPurchaseIdAsync(tenantId, purchaseInvoiceId, ct)` **después** de adquirir el Lock A (§15.7), y verifica `Status == WithholdingStatus.Issued` → si es así, rechaza con `PR-006` (mismo criterio ya usado por `CancelPurchaseHandler` para su propio caso).
- Lock compartido necesario: Lock A `(TenantId, PurchaseInvoiceId)`, el mismo que adquieren `IssueWithholdingHandler`/`CancelWithholdingHandler` (§15.2) — cierra la ventana TOCTOU.
- No hay ajuste proporcional (decisión §3.16) — el bloqueo es binario: si hay retención `Issued`, la devolución no se autoriza hasta que la retención se cancele por su propio flujo (`CancelWithholdingHandler`, fuera del alcance de esta devolución) o hasta que exista una decisión de negocio formal futura (fuera de v1).
- No se permite que se emita una retención entre la validación y el commit de la devolución — resuelto en §15.7 por el lock compartido, no por un mecanismo adicional.
- Error de negocio expuesto al usuario: mensaje que indica que existe una retención emitida sobre la factura y que debe resolverse antes de continuar — sin exponer detalles internos de la consulta SQL/índice.

---

## 18. Nota de Crédito recibida

### 18.1 Reutilización

Se reutiliza `PurchaseReceptionDocument` + `PurchaseReceptionSourceDocType.CreditNote` (ya evidenciado como estructura preparada, sin efectos colaterales sobre inventario/CxP). No se crea ningún encabezado fiscal nuevo.

### 18.1bis Campos reales verificados y columna nueva necesaria (resuelve bloqueante 4)

**Verificación contra el código real** (`ERP.Domain/Modules/Purchases/PurchaseReception/Entities/PurchaseReceptionDocument.cs`, `PurchaseReceptionDocumentConfiguration.cs`): los campos hoy existentes son `SupplierRuc`, `SupplierName`, `SupplierId`, `AccessKey`, `InvoiceNumber`, `IssueDate`, `AuthorizationDate`, `AuthorizationNumber`, `DocTypeCode`, `SriPaymentMethodCode`, `Subtotal`, `VatAmount`, `TotalAmount`, `Status`, `PurchaseId`, `XmlContent`. **No existe ningún campo de moneda** — la versión previa de este documento asumía implícitamente que `PurchaseReceptionDocument` podía validar "moneda de la NC" (§18.4 original) sin haber confirmado que el campo existiera; verificado ahora que no existe.

**Modificación necesaria explícita** (no una entidad paralela — extensión de columna sobre la entidad ya existente, mismo criterio que el resto de este diseño para `PurchasePayable`/`StockMovement`): agregar `CurrencyCode` (`string`, no nullable, nuevo) a `PurchaseReceptionDocument`, poblado por `RegisterSupplierCreditNoteUseCases` (§18.2) al momento de registrar la NC. Se declara en §5/§24 como cambio previsto sobre la entidad existente. `TotalAmount` (ya existente) es la fuente exacta de la validación cuantitativa **bloqueante** de §18.4bis (bloqueante 3) — no se requiere ningún campo adicional de impuestos (`ICE`) porque la comparación obligatoria se define a nivel de total, no de componente por componente (§18.4bis).

### 18.2 Registro manual (nuevo caso de uso, sin modificar la entidad existente salvo la columna nueva de §18.1bis)

`RegisterSupplierCreditNoteUseCases` (Application, nuevo) invoca directamente el factory ya existente `PurchaseReceptionDocument.Create(...)` (genérico, no depende del parser TXT) con `SourceDocType = CreditNote`, capturando: RUC/nombre proveedor, `AccessKey`, `InvoiceNumber` (número de la NC), `IssueDate`, `CurrencyCode` (columna nueva, §18.1bis), autorización SRI si el proveedor ya la emitió. Esto **no** modifica el comportamiento de `PurchaseReceptionDocument` — es un caso de uso nuevo que llama a un método de fábrica ya público y genérico, ahora con un parámetro adicional (`currencyCode`) que la firma de `Create(...)` deberá aceptar.

### 18.3 Cardinalidad — decisión de v1 (reevaluada con evidencia, misma decisión confirmada)

**Una NC recibida se vincula a exactamente un `PurchaseReturn` (1:1)**, mediante `PurchaseReturn.SupplierCreditNoteDocumentId` único (`UNIQUE (TenantId, SupplierCreditNoteDocumentId) WHERE NOT NULL`, ya existente, ambas direcciones de la relación quedan cerradas: una `PurchaseReturn` no puede tener dos NC — `SC-009` — y una NC no puede vincularse a dos `PurchaseReturn` — `SC-012`, nuevo).

Justificación (no solo "es más simple" — justificación de negocio explícita): las decisiones de negocio ya cerradas (§3.6–§3.7) establecen que el tratamiento financiero completo (reducción de CxP + creación de `SupplierCredit`) ya se resuelve íntegramente en el momento de `Authorize()`, **antes** de que exista la NC — la NC es puramente documental/fiscal, no dispara ningún efecto financiero nuevo (§3.14). El proceso de negocio real de la empresa (evidenciado en las auditorías cerradas) es: se autoriza una devolución física, el proveedor emite una NC que la sustenta, se registra esa NC — una relación 1:1 natural del flujo normal. Soportar varias NC parciales por devolución o una NC que cubra varias devoluciones exigiría lógica de prorrateo o consolidación **sin ningún efecto financiero que ese prorrateo module** (el dinero/inventario/crédito ya se resolvió en `Authorize()`) — sería complejidad sin consumidor real dentro de P0-02 (violaría el principio de "no abstracciones sin consumidor real"). El caso 1:1 es el único evidenciado como necesario por las auditorías y cubre el flujo normal de negocio.

**Rechazo exacto de un caso no soportado**: un intento de vincular una segunda NC a una `PurchaseReturn` ya vinculada se rechaza con `SC-009` (422); un intento de vincular una NC ya vinculada a otra `PurchaseReturn` distinta se rechaza con `SC-012` (422, nuevo). Ninguno de los dos casos modifica el vínculo existente.

**La estructura elegida no obliga a refactorizar `PurchaseReceptionDocument` si mañana se amplía**: el vínculo vive en `PurchaseReturn.SupplierCreditNoteDocumentId` (lado `PurchaseReturn`), no en `PurchaseReceptionDocument` — si en el futuro se necesita N:M, la extensión es una tabla puente nueva (`PurchaseReturnCreditNoteLink` con montos parciales) sin tocar el esquema de `PurchaseReceptionDocument` en absoluto (más allá de la columna `CurrencyCode` ya agregada en §18.1bis, que es necesaria en cualquier cardinalidad).

**Consecuencia futura declarada explícitamente**: si el negocio requiere que el proveedor consolide varias devoluciones en una sola NC, o fraccione una devolución en varias NC parciales, eso es una extensión de modelo (relación N:M `PurchaseReturn ↔ PurchaseReceptionDocument` con montos parciales) que requiere su propio diseño — no está soportada en v1 y **no debe inferirse ni improvisarse** por ningún handler. Se documenta como backlog no bloqueante (§25) porque no afecta la integridad financiera ya resuelta en la autorización — solo limita la flexibilidad de emparejamiento documental.

### 18.4 Validaciones

- Proveedor de la NC == proveedor de la `PurchaseInvoice` original referenciada por el `PurchaseReturn` (`SC-008` si no coincide).
- **Moneda de la NC (`PurchaseReceptionDocument.CurrencyCode`, columna nueva §18.1bis) == moneda de la `PurchaseInvoice`/`PurchaseReturn` de origen** — validación estructural, bloqueante (`SC-013`).
- Tipo de documento == `CreditNote`.
- `AccessKey` único por tenant (constraint ya existente `uq_purchase_reception_documents_tenant_access_key`) — detecta duplicidad de registro de la misma NC (`SC-007`).
- `PurchaseReturn.SupplierCreditNoteDocumentId` único — un documento de NC no puede vincularse a más de un `PurchaseReturn` (`SC-012`, dirección inversa de `SC-009`, ver §18.3).
- Fecha de emisión de la NC no anterior a la fecha de la factura original (validación estructural, no fiscal).
- **Monto de la NC — validación cuantitativa obligatoria y bloqueante** (corrección de diseño explícita, bloqueante 3 de la tercera revisión): ver §18.4bis. Se corrige la decisión previa de este documento, que trataba los montos de la NC como puramente informativos (§3.20 original) — esa decisión de negocio (§3.20) sigue vigente y sin cambio para lo que realmente cubre: **v1 no valida la autenticidad de la NC ante el servicio del SRI** (eso permanece backlog no bloqueante, §25.1). Lo que se corrige aquí es distinto: una comparación **interna** entre el monto declarado en el documento registrado (`PurchaseReceptionDocument.TotalAmount`) y el monto ya reconocido por nuestra propia autorización (`PurchaseReturn.GrandTotal`) — esto no es una consulta a un servicio externo, es una verificación aritmética con datos que el propio ERP ya tiene, y su ausencia permitía que una NC de importe manifiestamente distinto (error de digitación, NC equivocada) cambiara el estado fiscal a `SupplierCreditNoteRegistered` solo porque coincidían proveedor y factura. No contradice §3.20 — la complementa.

### 18.4bis Regla cuantitativa obligatoria (resuelve bloqueante 3)

**Fórmula exacta**:

```
ExpectedCreditNoteAmount = PurchaseReturn.GrandTotal              (numeric(18,2), snapshot ya congelado en Authorize(), §7.1)
ActualCreditNoteAmount   = PurchaseReceptionDocument.TotalAmount  (numeric(18,2), campo ya existente, capturado al registrar la NC, §18.2)
Difference               = ABS(ActualCreditNoteAmount − ExpectedCreditNoteAmount)
```

**Tolerancia única, explícita, no configurable**:

```
FiscalAmountTolerance = 0.01   (una unidad de la escala numeric(18,2), moneda base de la operación)
```

La vinculación **solo** puede confirmarse cuando `Difference ≤ FiscalAmountTolerance` (es decir, `Difference ≤ 0.01`).

**Justificación de la tolerancia** (coherente con la política monetaria ya congelada del proyecto, "Estándar de Precisión Numérica", `numeric(18,2)` para montos/totales): `GrandTotal` se calcula en `Authorize()` (§11.1) sumando líneas cuyo redondeo (`ROUND(..., 2, MidpointRounding.AwayFromZero)`) se aplica **independientemente por línea y por componente**, nunca ajustado contra un total global — el propio §11.1 ya reconoce explícitamente un residual posible de hasta `±0.01` por componente por línea, absorbido en el snapshot. `TotalAmount` de la NC es, a su vez, un total calculado independientemente por el proveedor (u obtenido de su propio sistema/SRI), con su propio redondeo interno. Dos totales calculados de forma independiente sobre el mismo conjunto de líneas de negocio, cada uno con redondeo `numeric(18,2)` propio, solo pueden diferir por el residual de redondeo de la unidad mínima de esa escala — `0.01` — nunca por una cantidad mayor sin que exista una diferencia comercial real (una línea de más/menos, una tasa distinta, un importe distinto). Una tolerancia mayor a `0.01` ocultaría diferencias comerciales materiales (viola el mandato explícito de "no permitir una diferencia comercial material"); una tolerancia de `0` rechazaría vinculaciones válidas por el simple residual de redondeo ya reconocido como legítimo en §11.1. `0.01` es, por tanto, la única tolerancia consistente con la propia política de redondeo ya congelada del sistema — no es un valor arbitrario ni "configurable a futuro".

**Fuente exacta de cada componente de la comparación**:

| Dato | Fuente exacta |
|---|---|
| Total esperado | `PurchaseReturn.GrandTotal` (snapshot congelado en `Authorize()`, §7.1, §11.1) |
| Total real de la NC | `PurchaseReceptionDocument.TotalAmount` (campo ya existente, capturado en el registro manual de la NC, §18.2) |
| Moneda de la devolución | `PurchaseReturn.SupplierId`/`PurchaseInvoice.CurrencyCode` — snapshot ya usado en toda fórmula financiera del documento (§11.2) |
| Moneda de la NC | `PurchaseReceptionDocument.CurrencyCode` (columna nueva, §18.1bis) |
| Subtotal/IVA/ICE de la devolución (componentes) | `PurchaseReturn.AuthorizedSubtotal`/`VatTotal`/`IceTotal` (§7.1) — no se recomparan componente a componente contra la NC en v1; la validación cuantitativa obligatoria es sobre el total (`GrandTotal` vs. `TotalAmount`), consistente con que `PurchaseReceptionDocument` no descompone `Subtotal`/`VatAmount` con el mismo detalle por tasa que `PurchaseReturnDetail` — comparar únicamente el total es la granularidad verificable con los campos hoy existentes en ambas entidades |
| Redondeo | `MidpointRounding.AwayFromZero`, `numeric(18,2)`, `CultureInfo.InvariantCulture` — mismo criterio que el resto del diseño (§11.1) |

**Validaciones obligatorias, cada una bloqueante** — la vinculación se rechaza (sin mutar `FiscalStatus` ni ningún otro dato) si:

| Condición | Código |
|---|---|
| `PurchaseReceptionDocument.TotalAmount` es `null`/no verificable (documento incompleto) | `SC-016` |
| `Difference > FiscalAmountTolerance` y `ActualCreditNoteAmount < ExpectedCreditNoteAmount` | `SC-017` |
| `Difference > FiscalAmountTolerance` y `ActualCreditNoteAmount > ExpectedCreditNoteAmount` | `SC-018` |
| `PurchaseReceptionDocument.CurrencyCode` es `null`/no verificable | `SC-019` |
| `PurchaseReceptionDocument.CurrencyCode` distinto de la moneda de origen | `SC-013` (ya existente, §18.1bis) |
| La NC ya está vinculada a otra `PurchaseReturn` | `SC-012` (ya existente) |
| La `PurchaseReturn` ya tiene otra NC vinculada | `SC-009` (ya existente) |

El estado `SupplierCreditNoteRegistered` (§9.2) **solo** puede alcanzarse después de que **todas** las validaciones de §18.4 (proveedor, tipo, duplicidad, moneda **y** la regla cuantitativa de este apartado) sean exitosas, y la relación (`PurchaseReturn.SupplierCreditNoteDocumentId`) quede confirmada en la **misma transacción** que la validación — nunca en dos pasos separados donde el monto se valide después de haber mutado el estado fiscal.

**Sin repetición de efectos**: superar esta validación y confirmar la vinculación sigue sin disparar ningún efecto sobre inventario, CxP, `SupplierCredit`, pagos, reembolso o contabilidad operativa de la devolución — la regla cuantitativa es una condición de entrada al único efecto ya existente (`FiscalStatus → SupplierCreditNoteRegistered`), no un efecto nuevo (consistente con §18.5, decisión §3.14, sin cambio).

### 18.5 Independencia de efectos

Vincular la NC solo muta `PurchaseReturn.FiscalStatus`. No hay ningún evento de dominio de este proceso que dispare inventario, CxP, crédito o contabilidad — consistente con §9.1 y decisión §3.14.

### 18.6 ¿Se permite autorizar sin tener la NC todavía?

Sí (decisión §3.11/§3.12, ya cerrada) — `Authorize()` no exige `SupplierCreditNoteDocumentId`. El estado fiscal queda `PendingSupplierCreditNote` de forma explícita y consultable hasta que se registre el vínculo.

---

## 19. Contabilidad

### 19.1 Hechos contables (`PostingFact`, `FactType`, `SourceModule="Purchases"`)

| Evento de dominio | `FactType` | Hecho compuesto o simple | Concepto débito/crédito (conceptual, sin cuentas) |
|---|---|---|---|
| `PurchaseReturnAuthorizedEvent` | `PurchaseReturn` | **Único hecho compuesto** (evita doble contabilización) — payload incluye `AppliedToPayableAmount`, `SupplierCreditAmount`, `ReturnedSubtotal`, `ReturnedVatAmount`, `ReturnedIceAmount`, `HistoricalCostTotal`, `CostVarianceTotal` por separado (ver §19.1bis para la ecuación completa balanceada) | Débito: Cuentas por Pagar (por `AppliedToPayableAmount`) + Crédito a favor frente a proveedores (por `SupplierCreditAmount`) + condicional, Cuenta de Ajuste/Variación de Costo (por `CostVarianceTotal` si es positivo). Crédito: Inventario (por `HistoricalCostTotal`, costo histórico revertido — nunca por el valor reconocido) + IVA/ICE crédito tributario (reversión proporcional, por `ReturnedVatAmount`/`ReturnedIceAmount`) + condicional, misma Cuenta de Ajuste/Variación (por `|CostVarianceTotal|` si es negativo) |
| `PurchaseReturnCancelledEvent` | `PurchaseReturnCancelled` | Hecho nuevo (reverso), no edición del asiento original | Reverso exacto del hecho anterior — mismos montos (incluida la variación de costo, si existió), dirección invertida |
| `SupplierCreditAppliedEvent` | `SupplierCreditApplied` | Simple | Débito: Cuentas por Pagar (CxP destino). Crédito: Crédito a favor frente a proveedores |
| `SupplierCreditApplicationReversedEvent` | `SupplierCreditApplicationReversed` | Simple | Reverso del anterior |
| `SupplierCreditRefundedEvent` | `SupplierCreditRefunded` | Simple | Débito: `SupplierCreditRefundTransaction.AccountingAccountId`, recién congelado desde `CompanyFinancialDestination.AccountingAccountId` en la misma transacción (§6.4bis, §19.1ter — cuenta contable real, nunca "Banco/Caja" conceptual). Crédito: Crédito a favor frente a proveedores. El evento se dispara una única vez desde la operación atómica `SupplierCredit.RegisterRefund()` que crea `SupplierCreditMovement` + `SupplierCreditRefundTransaction` juntos (§6.4, §13.6) — el `PostingFact` no depende de una cuenta bancaria física genérica, deriva la cuenta del destino persistido y congelado, y no hay dos eventos paralelos (uno del movimiento, otro de la transacción) que pudieran generar el asiento dos veces |
| `SupplierCreditRefundReversedEvent` | `SupplierCreditRefundReversed` | Simple | Reverso exacto del anterior — Débito: Crédito a favor frente a proveedores. Crédito: la **misma** `SupplierCreditRefundTransaction.AccountingAccountId` heredada del `REFUND_RECEIVED` original (§6.4bis, §19.1ter) — nunca el `AccountingAccountId` vigente del destino |

### 19.1bis Ecuación contable completa — valor reconocido vs. costo histórico (resuelve bloqueante 7)

**El problema**: `GrandTotal` (§11.1 — basado en `UnitPrice`/`DiscountAmount`/impuestos de `PurchaseInvoiceDetail`, lo que se le reconoce financieramente al proveedor) y `HistoricalCostTotal` (§7.2 — basado en `LandedUnitCost`, que puede incluir flete/nacionalización/otros costos de importación ajenos al precio pactado) casi nunca son iguales. Un asiento que solo usara `AppliedToPayableAmount + SupplierCreditAmount` como débito y `HistoricalCostTotal + impuestos` como crédito **no balancea** salvo coincidencia.

**Variable de ajuste**:

```
CostVarianceTotal = HistoricalCostTotal − AuthorizedSubtotal
```

Donde `AuthorizedSubtotal = Σ ReturnedSubtotal` (§7.1, ya neto de descuento — valor reconocido pre-impuestos). `CostVarianceTotal` puede ser positivo, negativo o cero.

**Ecuación balanceada completa** (un único hecho compuesto, calculado una vez en `Authorize()`, snapshot congelado — nunca recalculado después, evitando doble contabilización):

```
Σ Débitos = AppliedToPayableAmount + SupplierCreditAmount + max(CostVarianceTotal, 0)
Σ Créditos = HistoricalCostTotal + ReturnedVatAmount + ReturnedIceAmount + max(−CostVarianceTotal, 0)
```

**Demostración algebraica de que siempre balancea**: por construcción, `GrandTotal = AppliedToPayableAmount + SupplierCreditAmount = AuthorizedSubtotal + ReturnedVatAmount + ReturnedIceAmount` (§11.1/§11.2). Sustituyendo `AuthorizedSubtotal = HistoricalCostTotal − CostVarianceTotal`:

```
Σ Débitos = (HistoricalCostTotal − CostVarianceTotal + ReturnedVatAmount + ReturnedIceAmount) + max(CostVarianceTotal, 0)
          = HistoricalCostTotal + ReturnedVatAmount + ReturnedIceAmount + (max(CostVarianceTotal,0) − CostVarianceTotal)
          = HistoricalCostTotal + ReturnedVatAmount + ReturnedIceAmount + max(−CostVarianceTotal, 0)
          = Σ Créditos
```

Verificado numéricamente en el ejemplo (g) de §11.3 (`381.00 = 381.00`).

**Tratamiento conceptual de la diferencia**: cuando `CostVarianceTotal > 0` (costo histórico mayor que el valor reconocido — típico cuando `LandedUnitCost` incorpora costos de importación no facturados por el proveedor), se debita una cuenta conceptual de "Ajuste/Variación de Costo en Devoluciones de Compra" (concepto configurable vía `PostingRule`, sin número de cuenta fijo en este diseño — decisión de catálogo contable, fuera de alcance de este documento). Cuando `CostVarianceTotal < 0`, se acredita la misma cuenta por `|CostVarianceTotal|`. Cuando `CostVarianceTotal = 0`, no aparece ninguna línea de ajuste — el asiento simple ya balancea.

**Redondeo**: `HistoricalCostAmount` por línea (§7.2) se redondea igual que el resto de componentes de §11.1 (`ROUND(..., 2)`, `AwayFromZero`, `InvariantCulture`) — el residual posible de redondeo por línea (máximo ±0.01) queda absorbido dentro del propio `CostVarianceTotal` calculado a nivel de header (diferencia de sumas ya redondeadas), sin necesitar un ajuste de redondeo adicional.

**Reversa**: `PurchaseReturnCancelledEvent` reversa exactamente los mismos montos snapshot (incluida la línea de variación de costo, si existió) — nunca recalcula `CostVarianceTotal` de nuevo, evitando que un cambio posterior de `LandedUnitCost` (que de todas formas es inmutable tras `Confirm()`, §3.3) pudiera producir un reverso distinto al asiento original.

**Un único hecho compuesto, nunca doble contabilización**: `PurchaseReturnAuthorizedEvent` es el único evento que contabiliza inventario + CxP + crédito + impuestos + variación de costo — no existe un segundo evento (p. ej. uno específico de "ajuste de costo") que module la misma diferencia por separado; el traductor único (`PurchaseReturnAuthorizedPostingTranslator`, §19.2) construye las hasta 4 líneas del asiento (CxP, crédito, inventario, impuestos) más la línea condicional de variación en una sola invocación del Posting Engine.

### 19.1ter Cuenta contable del reembolso — derivación obligatoria desde el destino persistido, congelada en la transacción (corregido — §6.4bis)

```
Al registrar el reembolso (REFUND_RECEIVED):
    Débito:  SupplierCreditRefundTransaction.AccountingAccountId,
             congelado desde CompanyFinancialDestination.AccountingAccountId
             al confirmar REFUND_RECEIVED.
    Crédito: cuenta contable configurada para el crédito de proveedor

Al revertir (REFUND_REVERSED):
    Débito:  la misma cuenta configurada para el crédito de proveedor
             utilizada por el hecho contable original.
    Crédito: SupplierCreditRefundTransaction.AccountingAccountId
             heredado del REFUND_RECEIVED original.
```

**Reglas obligatorias**: la cuenta contable **no** se recibe manualmente en el comando `RegisterRefund`/`ReverseRefund` — en `RegisterRefund` se deriva de `CompanyFinancialDestination.AccountingAccountId` (§6.4), resuelto por `FinancialDestinationId` y **congelado** en `SupplierCreditRefundTransaction.AccountingAccountId` (§6.4bis) en el mismo instante; en `ReverseRefund` **nunca** se vuelve a consultar `CompanyFinancialDestination.AccountingAccountId` vigente — se hereda directamente de la transacción `REFUND_RECEIVED` original, aunque esa cuenta haya sido desactivada o el destino haya cambiado de cuenta contable para operaciones futuras después (§6.4quinquies). Para una operación **nueva**, la cuenta debe pertenecer al mismo `TenantId`/`CompanyId`, estar `IsActive = true` y `AllowsPosting = true` en `Account`, bloqueada por fila (`FOR SHARE`) dentro de la transacción (§6.4quater, §15.5) — esta exigencia de actividad/postabilidad **no** aplica a la cuenta histórica congelada que usa una reversa. No se usa ningún concepto genérico "Banco/Caja" — el `PostingFact` referencia siempre el `AccountId` real congelado en la transacción, nunca el valor mutable actual del destino. Nunca se emiten dos hechos contables por la misma transacción financiera: la contabilidad nace únicamente del evento único disparado por la operación atómica de §13.6. La reversa referencia el hecho contable original conforme al motor existente (§19.3) — nunca recalcula ni reinterpreta la cuenta usada en el ingreso original. `AccountingAccountCodeSnapshot` (§6.4) es únicamente la copia de presentación histórica; la fuente autoritativa del asiento y de todo reporte es siempre `SupplierCreditRefundTransaction.AccountingAccountId`, nunca el `AccountingAccountId` mutable actual de `CompanyFinancialDestination`.

### 19.2 Traductores nuevos (`INotificationHandler<TEvent>`, sin tocar el Posting Engine)

`PurchaseReturnAuthorizedPostingTranslator`, `PurchaseReturnCancelledPostingTranslator`, `SupplierCreditAppliedPostingTranslator`, `SupplierCreditApplicationReversedPostingTranslator`, `SupplierCreditRefundedPostingTranslator`, `SupplierCreditRefundReversedPostingTranslator` — todos siguiendo el patrón ya FROZEN de `SalesReturnAuthorizedPostingTranslator` (un `PostingFact` nuevo, nunca reversa automática de un asiento anterior editándolo; `EntryDate = DateOnly.FromDateTime(e.OccurredOn)`).

### 19.3 Idempotencia contable

`SourceModule="Purchases"` + `SourceEventId` (Id estable del evento — `PurchaseReturnId` para autorización/cancelación, `SupplierCreditMovement.Id` para aplicación/reembolso/reversas) + `FactType` — mismo mecanismo de idempotencia ya evidenciado en el motor (`PostingIdempotencyGuard`), sin cambios.

### 19.4 Configuración necesaria (dato, no código)

Se requieren `PostingRule` nuevas (registros de configuración) para cada `FactType` de la tabla 19.1 — fuera del alcance de este documento de diseño, se registra como tarea de un futuro plan de implementación.

### 19.5 No duplicación con el registro de NC

Registrar/vincular la Nota de Crédito (§18) no dispara ningún `PostingFact` — el asiento operativo ya fue generado íntegramente por `PurchaseReturnAuthorizedEvent` en el momento de la autorización (§3.14).

### 19.6 Reportes y estadísticas del reembolso — dimensiones estructuradas disponibles

El modelo de §6.4/§13.6 permite reportar de forma confiable, sin depender de texto libre, por cada una de las siguientes dimensiones:

| Dimensión | Campo estructurado utilizado | Fuente |
|---|---|---|
| Tenant / Company | `TenantId`/`CompanyId` | `SupplierCreditRefundTransaction` |
| Proveedor | `SupplierId` | `SupplierCreditRefundTransaction` (snapshot) |
| Devolución de compra de origen | `SupplierCredit.SourcePurchaseReturnId` | `SupplierCredit` |
| Crédito de proveedor | `SupplierCreditId` | `SupplierCreditRefundTransaction` |
| Fecha efectiva / mes / año | `EffectiveDate` | `SupplierCreditRefundTransaction` |
| Destino financiero (código/nombre) | `FinancialDestinationId` (agrupación autoritativa) + `FinancialDestinationCodeSnapshot`/`FinancialDestinationNameSnapshot` (presentación histórica) | `SupplierCreditRefundTransaction` / `CompanyFinancialDestination` |
| Tipo de destino | `DestinationTypeCode`/`DestinationTypeCodeSnapshot` | `CompanyFinancialDestination`/`SupplierCreditRefundTransaction` |
| Institución bancaria | `BankInstitutionCode` | `CompanyFinancialDestination` (vía `FinancialDestinationId`) |
| Cuenta bancaria configurada | `BankAccountIdentifierNormalized` | `CompanyFinancialDestination` |
| Caja / sesión de caja | `CashRegisterId` (destino) / `CashSessionId` (transacción) | `CompanyFinancialDestination` / `SupplierCreditRefundTransaction` |
| Método de pago | `PaymentMethodCode` | `SupplierCreditRefundTransaction` |
| Moneda | `CurrencyCode` | `SupplierCreditRefundTransaction` |
| Importe | `Amount` | `SupplierCreditRefundTransaction` |
| Cuenta contable **realmente usada** por cada transacción (corregido, §6.4bis) | `SupplierCreditRefundTransaction.AccountingAccountId` (autoritativo, congelado) — `AccountingAccountCodeSnapshot` solo para presentación | `SupplierCreditRefundTransaction` — **nunca** el `AccountingAccountId` mutable actual de `CompanyFinancialDestination` |
| Usuario | `CreatedByUserId` (vía `AuditActor` en `SupplierCreditAudit`) | `SupplierCreditAudit` |
| Ingresos activos vs. revertidos | `TransactionTypeCode = REFUND_RECEIVED` sin fila `REFUND_REVERSED` asociada vs. con ella | `SupplierCreditRefundTransaction` (join por `OriginalTransactionId`) |
| Motivo de reversa | `Reason` | `SupplierCreditRefundTransaction` (`REFUND_REVERSED`) |

**Regla de agregación**: todas las agrupaciones usan FK, códigos persistidos (`Code`, `DestinationTypeCode`, `TransactionTypeCode`, `PaymentMethodCode`) o campos estructurados (`Amount`, `CurrencyCode`, `EffectiveDate`) — ningún reporte autoritativo agrupa por `Name` ni por `ExternalReference`. `ExternalReference` es exclusivamente evidencia complementaria y filtro de búsqueda puntual (localizar una transacción por su comprobante), nunca dimensión de agrupación. Un reembolso con reversa (`REFUND_REVERSED`) se presenta separadamente o como importe compensatorio explícito — nunca se suma como ingreso neto vigente junto al `REFUND_RECEIVED` que revirtió.

**Regla adicional (corrección 1/2)**: los campos estructurales del destino (`DestinationTypeCode`, `CurrencyCode`, `CashRegisterId`, `BankInstitutionCode`, `BankAccountIdentifierNormalized`) son inmutables tras la creación de `CompanyFinancialDestination` (§6.4ter), por lo que su uso como dimensión histórica es siempre estable — un reporte agrupado por institución bancaria o caja nunca cambia retroactivamente. La única dimensión de `CompanyFinancialDestination` que es mutable es `AccountingAccountId`; por eso ningún reporte histórico la usa directamente — usa siempre `SupplierCreditRefundTransaction.AccountingAccountId`, congelado por transacción e inmune a cambios posteriores del destino.

---

## 20. Auditoría y permisos

### 20.1 Entidades de Entity Audit (ADR-022)

| Entidad de auditoría | Eventos que audita | Campos nullable según etapa |
|---|---|---|
| `PurchaseReturnAudit` | `Draft` creado/actualizado, `Authorized`, `Cancelled`, vínculo de NC | `GrandTotal`/`AppliedToPayableAmount`/`SupplierCreditAmount` nulos hasta `Authorized` (mismo patrón que `SalesReturnAudit`) |
| `SupplierCreditAudit` | Creación, `Applied`, `ApplicationReversed`, `Refunded`, `RefundReversed`, **`SourceReturnCancelled`** (corrección explícita — bloqueante 5 de la tercera revisión: la versión previa de esta fila no mencionaba este quinto tipo de movimiento pese a ya existir en §5, §9.3, §13.5, §7.5 — toda tabla que enumere eventos/movimientos de `SupplierCredit` debe incluirlo, sin excepción) | `TargetPurchasePayableId` nulo salvo en `Applied`/`ApplicationReversed`; `FinancialDestinationId`/`FinancialDestinationCodeSnapshot`/`DestinationTypeCodeSnapshot`/`AccountingAccountId`/`CashRegisterId`/`CashSessionId`/`CashMovementId`/`PaymentMethodCode`/`ExternalReference`/`EffectiveDate` nulos salvo en `Refunded`/`RefundReversed` (§6.4/§7.6); en `SourceReturnCancelled`, ambos grupos de campos nulos — ver §20.1bis para los campos propios de este evento |
| `CompanyFinancialDestinationAudit` (nueva — resuelve la exigencia de auditoría del catálogo de destinos; alcance corregido — corrección residual 5) | Creación, edición exclusivamente de `Name`, `IsActive` y `AccountingAccountId` (§6.4ter — los únicos tres campos editables). `Code`, `DestinationTypeCode`, `CurrencyCode`, `CashRegisterId`, `BankInstitutionCode` y `BankAccountIdentifierNormalized` **nunca** figuran como editables ni generan eventos de edición — son inmutables desde la creación (§6.4ter) | Ninguno estructuralmente nulo — todo destino tiene siempre `DestinationTypeCode` y sus campos condicionales correctos por invariante (§6.4); registra valores antes/después únicamente de los tres campos realmente editables |

Las tres heredan `AuditRecordBase`, se escriben exclusivamente desde `*AuditHandler` (`INotificationHandler<TEvent>`), conservan tenant, company, usuario (`AuditActor` — snapshot histórico, sin columnas de identidad propias), fecha UTC, entidad, identificador, operación, documento origen y valores antes/después relevantes — sin excepción al patrón FROZEN. `PurchaseReturnAudit`/`SupplierCreditAudit` registran también el `BranchId` del agregado auditado (mismo `BranchId` persistido y congelado de la entidad, §5.2/§7.1/§7.4) — no es una columna de identidad del actor (que sigue viviendo exclusivamente en `AuditActor`), es el `BranchId` de negocio del documento auditado, consistente con el resto de columnas propias de cada dominio ya reflejadas en su entidad de auditoría.

**Regla explícita de `ExternalReference` en la auditoría de `Refunded`/`RefundReversed` (corrección residual 6)**:

```
Refunded (REFUND_RECEIVED):
    SupplierCreditAudit.ExternalReference se audita con su valor
    normalizado (§6.4quinquies) o null, según PaymentMethod.RequiresReference.

RefundReversed (REFUND_REVERSED):
    SupplierCreditAudit.ExternalReference de la nueva fila de auditoría
    (y del nuevo evento de dominio) queda null.
    La evidencia original no se copia ni se inventa — se consulta
    mediante SupplierCreditAudit.OriginalTransactionId, que conduce
    a la fila de auditoría del REFUND_RECEIVED original y a su
    ExternalReference ya auditado allí.
```

Esto es consistente con `SupplierCreditRefundTransaction.ExternalReference` (§6.4, tabla de campos): la fila `REFUND_REVERSED` nunca porta una referencia propia, ni la auditoría de la reversa la sustituye por una nueva.

### 20.1bis Auditoría obligatoria de `SourceReturnCancelled` (resuelve bloqueante 5)

Cada movimiento `SourceReturnCancelled` (§9.3, §13.5 — generado automáticamente por `PurchaseReturn.Cancel()` cuando existe un `SupplierCredit` íntegro asociado) produce, en la misma transacción que lo crea, una fila de `SupplierCreditAudit` con, como mínimo, los siguientes campos poblados:

| Campo de auditoría | Valor |
|---|---|
| `SupplierCreditId` | El crédito afectado |
| `SourcePurchaseReturnId` | El `PurchaseReturn` cuya cancelación originó el movimiento (mismo valor que `SupplierCredit.SourcePurchaseReturnId`, §7.4) |
| `MovementType` | `SourceReturnCancelled` |
| `BalanceBefore` (saldo anterior) | `SupplierCredit.AvailableAmount` inmediatamente antes del movimiento — siempre `= OriginalAmount` por la precondición de `PR-011` (§13.5) |
| `Amount` (importe del movimiento) | Igual a `BalanceBefore` (§13.5 — nunca un valor distinto) |
| `BalanceAfter` (saldo posterior) | `0` |
| `StatusBefore`/`StatusAfter` (estado derivado) | `Open → Closed` (derivado de `AvailableAmount`, §7.4) |
| `CreatedByUserId` / `CreatedAtUtc` | Usuario y fecha UTC de la operación `Cancel` de la `PurchaseReturn` que lo generó (`AuditActor`, snapshot histórico — ver regla general de `AuditActor`) |
| `Reason` (motivo) | `PurchaseReturn.CancellationReason` de la operación que lo originó — mismo motivo de negocio, sin un motivo distinto propio del movimiento de crédito |
| `ClientRequestId` | El `CancelClientRequestId` de la `PurchaseReturn` que lo originó (el movimiento de crédito no tiene su propio `ClientRequestId` de usuario porque es un efecto colateral de sistema, no una operación invocable directamente — §9.3) |
| `RequestPayloadHash` | El mismo hash calculado para la operación `Cancel` de la `PurchaseReturn` (§16.2) — no se calcula un segundo hash independiente para un movimiento que el usuario no invoca directamente |

Esta fila se genera en la misma transacción y el mismo `SaveChanges` que el resto de efectos de `Cancel()` (§16.1) — nunca de forma diferida ni desde un proceso separado.

### 20.2 Permisos

| Operación | Permiso | Justificación de separación |
|---|---|---|
| Crear/editar/cancelar `Draft`, autorizar/cancelar `PurchaseReturn` | Extiende el conjunto de permisos ya existente de Compras (mismo perfil de riesgo que confirmar/cancelar una factura de compra) | Mismo criterio ya usado por `SalesReturn` al reutilizar `SalesPermissions` sin crear nuevos — es una operación de Compras, mismo dominio de riesgo |
| Aplicar crédito a otra CxP, registrar reembolso, revertir aplicación/reembolso | Extiende el conjunto de permisos ya existente de Finance (mismo perfil de riesgo que registrar/reversar un pago) | Es una operación que mueve dinero/afecta CxP de una factura potencialmente distinta a la de origen — mismo dominio de riesgo que `RegisterPayment`, que ya vive en Finance; separarlo de "autorizar la devolución física" permite que un rol de bodega/compras autorice la devolución sin poder mover crédito financiero, y viceversa — riesgo operativo genuinamente distinto, consistente con que CxP/Pagos ya viven en `modules/finance`, no en `modules/purchases` |
| Registrar/vincular Nota de Crédito recibida | Extiende el conjunto de permisos ya existente de Compras (mismo perfil que registrar una recepción/factura) | Es documental, mismo dominio que ya gestiona `PurchaseReceptionDocument` |
| Administrar el catálogo `CompanyFinancialDestination` (alta/edición/activación) | Extiende el conjunto de permisos ya existente de configuración de empresa (Company Settings, mismo perfil que administrar cuentas contables/métodos de pago) | Es configuración estructural que determina cuentas contables reales — mismo riesgo que administrar el catálogo de `Account`/`PaymentMethod`, nunca el mismo permiso que registrar un reembolso puntual |

No se crea un motor de permisos nuevo — únicamente nuevas claves dentro del catálogo de permisos ya existente, en los módulos que ya gestionan el riesgo correspondiente.

**Alcance por sucursal (Branch Ownership Rule, §5.2)**: todas las operaciones de esta tabla que actúan sobre un `PurchaseReturn`/`SupplierCredit` ya persistido quedan sujetas, además del permiso de negocio correspondiente, a `BranchScopeBehavior`/`IBranchAccessGuard`/`IInterBranchAccessGuard` (infraestructura ya existente, sin cambios) evaluados contra el `BranchId` persistido del agregado — mismo mecanismo de defensa en profundidad ya vigente para `PurchaseInvoice`. La persistencia de `BranchId` no sustituye este control de acceso (§5.2, regla 5).

---

## 21. Errores de negocio

| Código | Condición exacta | Operación | Resultado HTTP futuro | Datos que no deben cambiar |
|---|---|---|---|---|
| `PR-001` | La `PurchaseInvoice` referenciada no existe | Crear draft | 404 | Ninguno |
| `PR-002` | La `PurchaseInvoice` no está `Confirmed` | Crear draft / Autorizar | 422 | Ninguno |
| `PR-003` | La línea seleccionada no pertenece a la factura referenciada | Crear/editar draft | 422 | Ninguno |
| `PR-004` | Cantidad solicitada excede la cantidad remanente (§10.2) | Crear/editar draft / Autorizar (revalidado bajo lock) | 422 | Ninguno |
| `PR-005` | Stock insuficiente para la cantidad a devolver **en la bodega original congelada de la línea** (`PurchaseReturnDetail.WarehouseId` — nunca otra bodega, §14.2, bloqueante 6) — el error identifica línea, bodega, existencia actual y cantidad solicitada | Autorizar | 422 | Inventario, CxP, crédito — nada se aplica |
| `PR-006` | Retención `Issued` sobre la factura | Autorizar | 422 | Ninguno |
| `PR-007` | `PurchaseReturn` modificada concurrentemente (conflicto `xmin` del propio agregado) | Cualquier mutación de `PurchaseReturn` | 409 | Ninguno — el cliente debe recargar y reintentar |
| `PR-008` | `PurchasePayable` modificada concurrentemente (conflicto `xmin`) — cubre tanto conflictos originados por la propia devolución como por una cancelación de compra o un pago concurrente sobre la misma CxP (§5.1) | Autorizar / Cancelar / Aplicar crédito / `RegisterPayment`/`ReversePayment`/`CancelPurchase` (existentes, extendidos por §15.2) | 409 | Ninguno |
| `PR-009` | Transición de estado inválida (p. ej. autorizar algo ya `Cancelled`, cancelar algo ya `Cancelled`) | Cualquiera | 422 | Ninguno |
| `PR-010` | Monto de crédito a aplicar excede el `BalanceDue` del destino | Aplicar crédito | 422 | Ninguno |
| `PR-011` | Se intenta cancelar un `PurchaseReturn` `Authorized` cuyo `SupplierCredit` ya tiene movimientos activos (`AvailableAmount < OriginalAmount`, por aplicación **o** por reembolso — la fórmula §13.5 cubre ambos casos con la misma condición, ver §5.1 casos 6/7) | Cancelar devolución | 422 | Inventario, CxP, crédito — nada se revierte |
| `PR-012` | Solicitud idempotente repetida con `ClientRequestId` igual pero `RequestPayloadHash` (contenido) distinto — código único que cubre las 8 operaciones idempotentes de `PurchaseReturn` (crear, autorizar, cancelar, vincular NC — §16.2); las operaciones de `SupplierCreditMovement` usan `SC-006` (mismo mecanismo, catálogo de crédito) | Crear draft / Autorizar / Cancelar / Vincular NC | 422 | Ninguno — se conserva el resultado original |
| `PR-013` | Transición de `FiscalStatus` inválida no cubierta por `SC-009` (p. ej. cualquier intento de mutar `FiscalStatus` fuera de las dos transiciones válidas de §9.2 — `NotApplicable→PendingSupplierCreditNote` en `Authorize()` y `PendingSupplierCreditNote→SupplierCreditNoteRegistered` en el vínculo de NC) | Cualquier operación que intente mutar `FiscalStatus` | 422 | `FiscalStatus` permanece en su valor actual |
| `PI-CANC-01` (nuevo — extiende el catálogo de `CancelPurchaseUseCases`, fuera del catálogo propio de `PurchaseReturn`) | Se intenta cancelar una `PurchaseInvoice` que tiene una `PurchaseReturn` en `Authorized` asociada (§5.1 caso 1) | Cancelar factura de compra | 422 | Factura, CxP, inventario, devolución |
| `PI-CANC-02` (nuevo — extiende el catálogo de `CancelPurchaseUseCases`) | Se intenta cancelar una `PurchaseInvoice` cuya CxP recibió una aplicación de `SupplierCredit` activa (`SupplierCreditAppliedAmount > 0`, §5.1 caso 2) | Cancelar factura de compra | 422 | Factura, CxP, crédito |
| `SC-001` | El `SupplierCredit` no existe o no pertenece al tenant/empresa | Cualquier operación de crédito | 404 | Ninguno |
| `SC-002` | El `PurchasePayable` destino no existe o está `cancelled` (cubre también §5.1 caso 4) | Aplicar crédito | 422 | Ninguno |
| `SC-003` | Monto a aplicar/reembolsar excede `AvailableAmount` (crédito insuficiente) | Aplicar / Reembolsar | 422 | Ninguno |
| `SC-004` | Proveedor del `PurchasePayable` destino distinto al del `SupplierCredit` | Aplicar crédito | 422 | Ninguno |
| `SC-005` | Moneda del `PurchasePayable` destino distinta a la del `SupplierCredit` | Aplicar crédito | 422 | Ninguno |
| `SC-006` | Solicitud idempotente repetida con `ClientRequestId` igual pero `RequestPayloadHash` distinto — código único para las 4 operaciones idempotentes de `SupplierCreditMovement` (aplicar, revertir aplicación, reembolsar, revertir reembolso — §16.2; antes limitado solo a "reembolso duplicado") | Aplicar crédito / Revertir aplicación / Registrar reembolso / Revertir reembolso | 422 | Ninguno — se conserva el resultado original |
| `SC-007` | NC duplicada (`AccessKey` ya registrado para el tenant) | Registrar NC | 422 | Ninguno |
| `SC-008` | NC de un proveedor distinto al de la factura original | Vincular NC | 422 | Ninguno |
| `SC-009` | `PurchaseReturn` ya tiene una NC vinculada | Vincular NC | 422 | Ninguno |
| `SC-010` | `SupplierCredit` modificado concurrentemente (`xmin`) | Cualquier mutación de crédito | 409 | Ninguno |
| `SC-011` | Se intenta revertir un movimiento ya revertido | Reversa de aplicación/reembolso | 422 | Ninguno |
| `SC-012` (nuevo — bloqueante 4) | El `PurchaseReceptionDocument` (NC) ya está vinculado a otra `PurchaseReturn` distinta (dirección inversa de `SC-009` — la unicidad `UNIQUE (TenantId, SupplierCreditNoteDocumentId) WHERE NOT NULL` la detecta a nivel de BD; este código es su traducción de negocio) | Vincular NC | 422 | Ambos documentos permanecen como estaban |
| `SC-013` (nuevo — bloqueante 4) | La moneda de la NC (`PurchaseReceptionDocument.CurrencyCode`, campo nuevo §5, §18.1bis) no coincide con la moneda de la `PurchaseInvoice`/`PurchaseReturn` de origen | Vincular NC | 422 | Vínculo no se establece |
| `SC-014` (nuevo — bloqueante 5) | Se intenta revertir una aplicación de crédito cuya `PurchasePayable` destino ya fue cancelada después de la aplicación original (§5.1 caso 5) | Revertir aplicación | 422 | Crédito (`AvailableAmount` sin cambio), CxP destino |
| `SC-015` | El `PaymentMethodCode` del reembolso no referencia un método de pago activo del catálogo `PaymentMethod` | Registrar reembolso | 422 | Ninguno — reembolso no se registra |
| `SC-016` (bloqueante 3 de la tercera revisión, §18.4bis) | `PurchaseReceptionDocument.TotalAmount` no verificable (nulo o documento incompleto) | Vincular NC | 422 | Vínculo no se establece |
| `SC-017` (bloqueante 3, §18.4bis) | Monto de la NC inferior al esperado fuera de tolerancia (`ActualCreditNoteAmount < ExpectedCreditNoteAmount`, `Difference > 0.01`) | Vincular NC | 422 | Vínculo no se establece |
| `SC-018` (bloqueante 3, §18.4bis) | Monto de la NC superior al esperado fuera de tolerancia (`ActualCreditNoteAmount > ExpectedCreditNoteAmount`, `Difference > 0.01`) | Vincular NC | 422 | Vínculo no se establece |
| `SC-019` (bloqueante 3, §18.4bis) | `PurchaseReceptionDocument.CurrencyCode` no verificable (nulo) | Vincular NC | 422 | Vínculo no se establece |
| `SC-020` (nuevo — corrección del destino financiero, §6.4) | `FinancialDestinationId` no existe o no pertenece al `TenantId`/`CompanyId` del reembolso | Registrar reembolso | 404 | Ninguno — reembolso no se registra |
| `SC-021` (nuevo, §6.4, §13.6) | El destino financiero existe pero `IsActive = false` en el instante de la confirmación bajo lock (incluye desactivación concurrente) | Registrar reembolso | 422 | Ninguno — reembolso no se registra |
| `SC-022` (nuevo, §6.4) | `CompanyFinancialDestination` con configuración incompleta para su `DestinationTypeCode` (banco sin `BankInstitutionCode`/`BankAccountIdentifierNormalized`, o caja sin `CashRegisterId`) | Alta/edición de destino financiero | 422 | Destino no se crea/actualiza |
| `SC-023` (nuevo, §6.4) | `AccountingAccountId` del destino no existe o no pertenece al mismo `TenantId`/`CompanyId` | Alta/edición de destino financiero | 422 | Destino no se crea/actualiza |
| `SC-024` (nuevo, §6.4, §19.1ter) | `Account` del destino no es postable (`AllowsPosting = false`) o está inactiva (`IsActive = false`) | Alta/edición de destino financiero / Registrar reembolso (revalidado bajo lock) | 422 | Destino no se crea/actualiza; reembolso no se registra |
| `SC-025` (nuevo, §6.4) | `CurrencyCode` del `SupplierCredit`/reembolso distinto de `CompanyFinancialDestination.CurrencyCode` del destino elegido | Registrar reembolso | 422 | Ninguno — reembolso no se registra |
| `SC-026` (nuevo, §6.4) | `CashRegisterId` del destino no existe o no pertenece al mismo `TenantId`/`CompanyId` | Alta/edición de destino financiero (`DestinationTypeCode = CASH_REGISTER`) | 422 | Destino no se crea/actualiza |
| `SC-027` (nuevo, §6.4, §13.6, §6.4quater/§6.4quinquies — corrección 6) | El destino financiero corresponde a `CASH_REGISTER`, pero no existe una `CashSession` activa compatible y bloqueada (`FOR SHARE`) para la `CashRegisterId` autoritativa en el instante de la confirmación | Registrar reembolso **y** Revertir reembolso (destino `CASH_REGISTER`) | 422 | Saldo del crédito, `SupplierCreditMovement`, `SupplierCreditRefundTransaction`, `CashMovement`, asiento contable, auditoría, idempotencia — nada se persiste; en `ReverseRefund`, el `REFUND_RECEIVED` original permanece intacto y activo |
| `SC-028` (nuevo, §6.4) | Importe del reembolso inválido (`Amount ≤ 0`, o distinto de `SupplierCreditMovement.Amount` que lo origina) | Registrar reembolso | 422 | Ninguno — reembolso no se registra |
| `SC-029` (nuevo, §6.4, §12) | La relación 1:1 `SupplierCreditRefundTransaction.SupplierCreditMovementId` ya existe para el movimiento objetivo (violación de `UNIQUE (TenantId, CompanyId, SupplierCreditMovementId)`) | Registrar/revertir reembolso | 409 | Ninguno — se conserva la transacción financiera ya persistida |

Ningún mensaje expone nombres de índices, SQL ni excepciones internas — todos se traducen a texto de negocio en español orientado a corregir, vía `IDatabaseExceptionTranslator`/`ExceptionMiddleware` ya existentes.

---

## 22. Condiciones finales de confiabilidad

| Operación | Inventario final | CxP final | Crédito final | Contabilidad final | Estado documental | Auditoría | Invariante comprobable |
|---|---|---|---|---|---|---|---|
| Autorizar, factura impaga | Reducido exactamente en la cantidad devuelta, a `HistoricalCostTotal` (costo histórico, no valor reconocido) | `BalanceDue` reducido en `GrandTotal` completo | Sin crédito creado | 1 `PostingFact` compuesto balanceado (§19.1bis: `Σdébitos = Σcréditos` incluyendo `CostVarianceTotal` si `≠ 0`) | `Authorized` / `PendingSupplierCreditNote` | 1 fila `PurchaseReturnAudit` | `appliedToPayable == GrandTotal`; `Σdébitos == Σcréditos` (ejemplo verificado en §11.3(g)) |
| Autorizar, factura parcial (devolución < saldo) | Igual | `BalanceDue` reducido en `GrandTotal` | Sin crédito | 1 `PostingFact` balanceado | Igual | Igual | `BalanceDue` después `≥ 0`; `Σdébitos == Σcréditos` |
| Autorizar, factura totalmente pagada | Igual | `BalanceDue` sin cambio (ya era 0) | `SupplierCredit` creado por `GrandTotal` completo | 1 `PostingFact` balanceado | Igual | Igual + creación de crédito auditada | `SupplierCreditAmount == GrandTotal`; `Σdébitos == Σcréditos` |
| Aplicar crédito a otra CxP | Sin cambio | `BalanceDue` del destino reducido en el monto aplicado | `AvailableAmount` reducido en el mismo monto | 1 `PostingFact` | Sin cambio (no es una operación de `PurchaseReturn`) | 1 fila `SupplierCreditAudit` | `Σ Application activas == SupplierCreditAppliedAmount` del destino |
| Registrar reembolso | Sin cambio (o +1 `CashMovement` si el destino es `CASH_REGISTER`) | Sin cambio | `AvailableAmount` reducido; 1 fila `SupplierCreditRefundTransaction(REFUND_RECEIVED)` con `AccountingAccountId` congelado | 1 `PostingFact` — débito `SupplierCreditRefundTransaction.AccountingAccountId` (recién congelado desde el destino, §19.1ter), crédito cuenta de crédito de proveedor | Sin cambio | 1 fila `SupplierCreditAudit` | `AvailableAmount ≥ 0`; `SupplierCreditRefundTransaction.Amount == SupplierCreditMovement.Amount` |
| Cancelar devolución sin crédito usado | Reversado (movimiento inverso) | `BalanceDue` restaurado (`ReturnAppliedAmount` revertido) | Si existía `SupplierCredit`, queda en 0 vía `SourceReturnCancelled` | 1 `PostingFact` reverso | `Cancelled` | 1 fila `PurchaseReturnAudit` | Suma de movimientos de inventario de esa línea = 0 |
| Intentar cancelar con crédito ya usado | **Sin cambio — operación rechazada** | Sin cambio | Sin cambio | Ningún efecto | Sin cambio (`Authorized`) | 0 filas nuevas (solo el intento fallido, si se audita el intento) | Rechazo determinista `PR-011`, nunca cancelación parcial |
| Revertir aplicación de crédito | Sin cambio | `BalanceDue` del destino aumentado (se revierte la reducción) | `AvailableAmount` aumentado | 1 `PostingFact` reverso | Sin cambio | 1 fila `SupplierCreditAudit` | `AvailableAmount ≤ OriginalAmount` |
| Revertir reembolso | Sin cambio (o +1 `CashMovement` compensatorio si el destino original era `CASH_REGISTER`) | Sin cambio | `AvailableAmount` aumentado; 1 fila `SupplierCreditRefundTransaction(REFUND_REVERSED)` heredando destino/cuenta contable/moneda/importe/método del original, sin volver a resolverlos (§6.4quinquies) | 1 `PostingFact` reverso — mismos montos, misma `SupplierCreditRefundTransaction.AccountingAccountId` heredada del `REFUND_RECEIVED` original (§19.1ter) — nunca la cuenta vigente del destino | Sin cambio | 1 fila `SupplierCreditAudit` | `UNIQUE (TenantId, CompanyId, OriginalTransactionId) WHERE TransactionTypeCode='REFUND_REVERSED'` — una sola reversa por ingreso |
| Registrar NC posterior | Sin cambio | Sin cambio | Sin cambio | **Sin nuevo asiento** | `FiscalStatus → SupplierCreditNoteRegistered` | 1 fila `PurchaseReturnAudit` | Ningún `PostingFact` nuevo generado |
| Reintento tras timeout (cualquier operación) | Idéntico al de la ejecución original (nunca duplicado) | Idéntico | Idéntico | Idéntico (mismo `SourceEventId`, guard de idempotencia contable) | Idéntico | Sin filas duplicadas | Resultado determinista — ver §16.4 |
| Conflicto concurrente (dos autorizaciones simultáneas) | Solo la que gana el lock aplica su efecto; la segunda revalida y falla si corresponde | Igual | Igual | Solo un `PostingFact` por operación exitosa | Solo la exitosa avanza de estado | Solo 1 fila por operación exitosa | Serialización total por Lock A — nunca ambas aplican simultáneamente |
| Fallo antes del commit | Ningún efecto persistido | Ningún efecto persistido | Ningún efecto persistido | Ningún `PostingFact` | Sin cambio | Sin fila nueva | Rollback completo de la transacción (§16.1) |
| Fallo después del commit sin respuesta al cliente | Efecto persistido (correcto) | Efecto persistido (correcto) | Efecto persistido (correcto) | `PostingFact` persistido | Estado avanzado correctamente | Fila de auditoría persistida | El reintento del cliente (§16.4) encuentra el resultado ya confirmado, no lo duplica |

Ninguna fila de esta tabla admite un resultado "parcial" — cada una demuestra o bien todos los efectos confirmados, o bien ninguno.

---

## 23. Matriz completa de escenarios

Para cada escenario: precondiciones → operación → locks → validaciones → cambios atómicos → resultado → auditoría → contabilidad → comportamiento ante falla.

**1. Devolución parcial, factura impaga.**
Precondiciones: factura `Confirmed`, `BalanceDue=1000`, sin retención. Operación: `Authorize` con 3 de 10 unidades de una línea. Locks: A sobre `PurchaseInvoiceId`. Validaciones: remanente, stock, retención. Cambios atómicos: salida de inventario, `ApplyReturnCredit(300)`→ `appliedToPayable=300`, sin crédito. Resultado: `Authorized`/`PendingSupplierCreditNote`. Auditoría: 1 fila. Contabilidad: 1 `PostingFact`. Falla: rollback total, ningún efecto.

**2. Devolución total, factura impaga.**
Igual patrón, cantidad = 100% de cada línea. `appliedToPayable = GrandTotal` completo (si `≤ BalanceDue`). Resto igual al escenario 1.

**3. Menor al saldo, factura parcial.**
`BalanceDue=400`, devolución `GrandTotal=250`. `appliedToPayable=250`, sin crédito, `BalanceDue después=150`. Resto igual.

**4. Superior al saldo, factura parcial.**
`BalanceDue=400`, devolución `GrandTotal=550`. `appliedToPayable=400`, `SupplierCredit=150` creado. `BalanceDue después=0`. Auditoría: 1 fila `PurchaseReturnAudit` + creación de crédito referenciada dentro de la misma fila (o evento adicional según payload — el crédito no tiene su propia fila de `SupplierCreditAudit` "Applied" en este paso, solo su creación implícita en el evento de autorización, ya que su primer estado es simplemente `Open` sin movimientos).

**5. Factura totalmente pagada.**
`BalanceDue=0`. `appliedToPayable=0`, `SupplierCredit = GrandTotal` completo. Resto igual al patrón general (§22, fila 3).

**6. Crédito aplicado a otra CxP.**
Precondiciones: `SupplierCredit.AvailableAmount=150`, otra `PurchaseInvoice` del mismo proveedor con `BalanceDue=500`. Operación: `ApplyToPayable(150, targetPayableId)`. Locks: A (destino) → B. Validaciones: proveedor/moneda coinciden, `150 ≤ AvailableAmount`, `150 ≤ BalanceDue destino`. Cambios atómicos: `SupplierCreditMovement(Application, 150)`, `AvailableAmount→0`, `PurchasePayable(destino).ApplySupplierCredit(150)` → `BalanceDue destino=350`. Resultado: crédito `Closed` (derivado). Auditoría: 1 fila `SupplierCreditAudit`. Contabilidad: 1 `PostingFact`. Falla: rollback total.

**7. Crédito cerrado por reembolso.**
Precondiciones: `AvailableAmount=150`, `CompanyFinancialDestination` activo (`DestinationTypeCode=BANK_ACCOUNT`, cuenta contable postable). Operación: `RegisterRefund(150, financialDestinationId, paymentMethodCode, externalReference)`. Locks (corrección residual 10): `Lock B` + `FOR SHARE CompanyFinancialDestination` + `FOR SHARE Account` (sin `CashSession`, el destino es `BANK_ACCOUNT`). Validaciones: destino existe/activo/mismo tenant-company, cuenta postable, moneda coincide, bajo los bloqueos de fila (§6.4quater). Cambios atómicos: `SupplierCreditMovement(Refund,150)` + `SupplierCreditRefundTransaction(REFUND_RECEIVED,150,financialDestinationId,AccountingAccountId congelado)`, `AvailableAmount→0`. Resultado: crédito `Closed`. Contabilidad: 1 `PostingFact` — débito `SupplierCreditRefundTransaction.AccountingAccountId` (congelado en este instante desde el destino, §19.1ter), crédito cuenta de crédito de proveedor. Auditoría: 1 fila `SupplierCreditAudit`. Falla: rollback total, ningún efecto (ni siquiera el `CashMovement` si el destino fuera de caja).

**7bis. Reembolso a caja con reversa posterior.**
Precondiciones: `AvailableAmount=200`, `CompanyFinancialDestination` activo (`DestinationTypeCode=CASH_REGISTER`), `CashSession` abierta para esa `CashRegister`. Operación: `RegisterRefund(200, financialDestinationId, paymentMethodCode, externalReference)`. Locks (corrección residual 10): `Lock B` + `FOR SHARE CompanyFinancialDestination` + `FOR SHARE Account` + `FOR SHARE CashSession` activa (destino `CASH_REGISTER`). Cambios atómicos: `SupplierCreditMovement(Refund,200)` + `SupplierCreditRefundTransaction(REFUND_RECEIVED,200,AccountingAccountId congelado)` + `CashMovement` real dentro de la sesión bloqueada, `CashSessionId`/`CashMovementId` persistidos (§13.6). Resultado: crédito `Closed`. Reversa posterior (corrección residual 10): `ReverseRefund` adquiere `Lock B` por `SupplierCreditId`, carga y **bloquea** (`FOR SHARE`) el `SupplierCreditRefundTransaction(REFUND_RECEIVED)` original, verifica bajo ese bloqueo que no exista una `REFUND_REVERSED` previa (`SC-011`) y, por corresponder a caja, resuelve y **bloquea** (`FOR SHARE`) la `CashSession` activa compatible de la misma `CashRegisterId` heredada — sin repetir `FOR SHARE` sobre `CompanyFinancialDestination`/`Account`, que se heredan ya congelados (§6.4quinquies). Si no existiera `CashSession` activa compatible: `SC-027`, rollback completo, ningún efecto parcial. Con sesión bloqueada: crea `SupplierCreditMovement(ReversalOfRefund,200)` + `SupplierCreditRefundTransaction(REFUND_REVERSED,200,OriginalTransactionId=…,ExternalReference=null)` heredando destino/`AccountingAccountId`/moneda/importe/método, y su propio `CashMovement` compensatorio dentro de la sesión bloqueada. `AvailableAmount→200` de nuevo. Falla en cualquier paso: rollback total.

**8. NC recibida después.**
Precondiciones: `PurchaseReturn.Authorized`/`PendingSupplierCreditNote`. Operación: registrar `PurchaseReceptionDocument(CreditNote)` + vincular. Locks: ninguno de A/B. Validaciones: proveedor, tipo, duplicidad (`AccessKey`), no vinculada previamente, moneda (`SC-013`/`SC-019`) **y** validación cuantitativa bloqueante del monto (`Difference ≤ FiscalAmountTolerance`, §18.4bis — `SC-016`/`SC-017`/`SC-018` si falla). Cambios atómicos: solo `FiscalStatus→SupplierCreditNoteRegistered`, y únicamente si la validación cuantitativa fue exitosa. Resultado: sin efectos financieros/inventario. Auditoría: 1 fila. Contabilidad: ninguna (§19.5). Falla (incluida la validación de monto): rollback documental únicamente, `FiscalStatus` permanece `PendingSupplierCreditNote`.

**9. Factura con retención emitida.**
Precondiciones: `IssuedWithholding.Status=Issued` sobre la factura. Operación: intento de `Authorize`. Locks: A adquirido. Validaciones: falla en la consulta de retención bajo lock. Cambios atómicos: **ninguno** — rechazo `PR-006`. Resultado: `PurchaseReturn` permanece `Draft`. Auditoría: opcionalmente se registra el intento fallido (no obligatorio si el patrón de auditoría solo cubre transiciones exitosas — consistente con `SalesReturnAudit`, que audita transiciones, no intentos). Contabilidad: ninguna.

**10. Dos devoluciones simultáneas sobre la misma factura.**
Ambas intentan `Authorize` concurrentemente. Locks: A serializa — la segunda espera. La primera revalida remanente bajo lock y autoriza. La segunda, al adquirir el lock, revalida remanente ya actualizado por la primera; si la cantidad solicitada ya no cabe, falla con `PR-004` determinista (no una condición de carrera).

**11. Devolución y pago simultáneos.**
`RegisterPaymentCommandHandler` y `AuthorizePurchaseReturnUseCases` compiten por Lock A de la misma factura. Una se serializa detrás de la otra. La segunda, al adquirir el lock, recarga `PurchasePayable` con el estado ya actualizado por la primera y calcula sobre datos frescos — nunca sobre una lectura obsoleta. Sin lost update (a diferencia del comportamiento actual sin `xmin`/lock, §D del cierre de auditoría).

**12. Devolución y emisión de retención simultáneas.**
Igual mecanismo — Lock A serializa `AuthorizePurchaseReturnUseCases` e `IssueWithholdingHandler`. Resuelto en §15.7.

**13. Dos aplicaciones simultáneas del mismo crédito.**
Ambas intentan `ApplyToPayable` sobre el mismo `SupplierCredit`. Locks: B serializa. La segunda, al adquirir el lock, revalida `AvailableAmount` ya reducido por la primera; si el monto solicitado excede el disponible restante, falla con `SC-003` determinista.

**14. Cancelación de devolución.**
Ver fila correspondiente de §22 (con y sin crédito usado).

**15. Timeout y reintento.**
Ver §16.4 — resultado determinista sin duplicación en ningún caso (draft, autorización, aplicación de crédito, reembolso).

**16. Autorización con diferencia entre valor reconocido y costo histórico.**
Ver ejemplo numérico completo y balanceado en §11.3(g) y la derivación algebraica en §19.1bis. Precondiciones: `LandedUnitCost` de la línea distinto del `UnitPrice` pactado (típico cuando el costo incluye flete/nacionalización). Cambios atómicos: los del patrón general (§22, fila "Autorizar") más la línea condicional de "Ajuste/Variación de Costo" en el `PostingFact` compuesto. Invariante comprobable: `Σdébitos == Σcréditos` con la variación incluida.

**17. Las 9 invariantes cruzadas de agregados.**
Ver tabla completa en §5.1 (bloqueante 5) — cancelar factura con devolución `Authorized`, cancelar factura con CxP que recibió crédito aplicado, pagar/aplicar sobre CxP `cancelled`, revertir aplicación tras cancelar la CxP destino, cancelar devolución con crédito aplicado/reembolsado/con NC ya registrada, y cancelación concurrente factura/devolución. Cada caso indica explícitamente locks, revalidaciones, reversas exigidas y código de error — no se repiten aquí para no duplicar la tabla.

---

## 24. Cambios previstos por capa

| Capa | Archivo o componente existente | Acción futura | Elemento nuevo o modificado | Justificación |
|---|---|---|---|---|
| `ERP.Domain/Modules/Purchases/Entities/` | — | Elemento nuevo justificado | `PurchaseReturn.cs`, `PurchaseReturnDetail.cs`, `SupplierCredit.cs`, `SupplierCreditMovement.cs`, `PurchaseReturnAudit.cs`, `SupplierCreditAudit.cs`, `PurchaseReturnSequence.cs` (§7.1bis) — `PurchaseReturn.CreateDraft(...)`/`SupplierCredit.CreateFromReturn(...)` reciben/propagan `BranchId` obligatorio (Branch Ownership Rule, §5.2) | Ver §6, §7.1bis, §5.2 |
| `ERP.Domain/Modules/Finance/Entities/` | — | Elemento nuevo justificado | `CompanyFinancialDestination.cs`, `SupplierCreditRefundTransaction.cs`, `CompanyFinancialDestinationAudit.cs` (§6.4/§7.6, corrección del destino financiero) | §6.3, §6.4 — ubicación consistente con que CxP/Pagos/crédito de proveedor ya viven en Finance |
| `ERP.Domain/Modules/Purchases/Enums/` | — | Elemento nuevo justificado | `PurchaseReturnStatus`, `PurchaseReturnFiscalStatus` (3 valores: `NotApplicable/PendingSupplierCreditNote/SupplierCreditNoteRegistered`, §9.2), `SupplierCreditMovementType` (5 valores, §9.3) | Máquinas de estado cerradas del propio agregado (§9). **Sin** `SupplierCreditSourceType` — eliminado explícitamente, ver §6.1 (bloqueante 9) |
| `ERP.Domain/Modules/Finance/Enums/` | — | Elemento nuevo justificado | `FinancialDestinationTypeCode` (catálogo persistido, 2 valores: `BANK_ACCOUNT`/`CASH_REGISTER`, §6.4), `RefundTransactionTypeCode` (catálogo persistido, 2 valores: `REFUND_RECEIVED`/`REFUND_REVERSED`, §6.4) | Reemplazan por completo `FinancialDestinationType`/`ExternalPaymentChannel` (un solo valor conceptual, eliminado) de la versión previa de este documento |
| `ERP.Domain/Modules/Purchases/Interfaces/IPurchaseReturnSequenceRepository.cs` | — | Elemento nuevo justificado | `CaptureNextAsync(tenantId, companyId, ct)` — `pg_advisory_xact_lock` dentro de la transacción ambiente del llamador, **nunca** transacción propia (corrección bloqueante 4, tercera revisión); análogo en propósito pero **distinto** en mecanismo transaccional de `IDocumentSequenceRepository` | §7.1bis (bloqueantes 10 y 4) |
| `ERP.Domain/Modules/Purchases/Entities/PurchasePayable.cs` | Existente | Modificación necesaria | `ReturnAppliedAmount`, `SupplierCreditAppliedAmount`, `ApplyReturnCredit()`, `ReverseReturnCredit()`, `ApplySupplierCredit()`, `ReverseSupplierCredit()`, fórmula `BalanceDue` extendida | §12 — nunca se reutilizan los métodos existentes con semántica falsa |
| `ERP.Infrastructure/Persistence/Configurations/Purchases/PurchasePayableConfiguration.cs` | Existente | Modificación necesaria | Agregar `xmin` (RowVersion) + columnas nuevas | Resuelve hallazgo #1 |
| `ERP.Infrastructure/Persistence/Configurations/Purchases/PurchaseReturnConfiguration.cs`, `SupplierCreditConfiguration.cs` | — | Elemento nuevo justificado | `BranchId` mapeado `NOT NULL` en ambas configuraciones (Branch Ownership Rule, §5.2); índice `(TenantId, CompanyId, BranchId)` en `PurchaseReturn` para listados/reportes filtrados por sucursal, mismo criterio que `PurchaseInvoiceConfiguration` | §5.2, §7.1, §7.4 |
| `ERP.Domain/Modules/Inventory/Entities/StockMovement.cs` (o configuración EF equivalente) | Existente | Modificación necesaria | `SourceDocLineId (Guid?)`, genérico, nullable | §10.3 — resuelve hallazgo #3 sin romper flujos existentes |
| `ERP.Domain/Modules/Purchases/PurchaseReception/Entities/PurchaseReceptionDocument.cs` + `PurchaseReceptionDocumentConfiguration.cs` | Existente | Modificación necesaria (columna nueva, no rediseño) | `CurrencyCode (string)` | §18.1bis — resuelve bloqueante 4: el campo no existía y era necesario para validar moneda de la NC |
| `ERP.Infrastructure/Persistence/Repositories/Purchases/` | — | Elemento nuevo justificado | `IPurchaseReturnRepository`/`PurchaseReturnRepository` (con `AcquireFinancialLockAsync` — Lock A), `ISupplierCreditRepository`/`SupplierCreditRepository` (con `AcquireLockAsync` — Lock B), `PurchaseReturnSequenceRepository` (§7.1bis) — consultas/listados admiten filtro por `BranchId` como defensa en profundidad adicional a `BranchScopeBehavior` (§5.2, §20.2) | §15, §7.1bis, §5.2 |
| `ERP.Infrastructure/Persistence/Repositories/Finance/` | — | Elemento nuevo justificado | `ICompanyFinancialDestinationRepository`/`CompanyFinancialDestinationRepository`, `ISupplierCreditRefundTransactionRepository`/`SupplierCreditRefundTransactionRepository` (persistidos en la misma transacción que `SupplierCreditMovement`, §13.6) | §6.4, §13.6 |
| `ERP.Infrastructure/Persistence/Configurations/Finance/` | — | Elemento nuevo justificado | `CompanyFinancialDestinationConfiguration` (CHECK combinados sin `IsActive` estructural + índices únicos parciales, §6.4). `SupplierCreditRefundTransactionConfiguration` (`xmin`; FK única a `SupplierCreditMovement`; **corrección residual 9** — declara expresamente `AccountingAccountId` obligatorio (`NOT NULL`), FK real a `Account`, índice por `(TenantId, CompanyId, AccountingAccountId)`, y columna `AccountingAccountCodeSnapshot` — no queda documentada únicamente en la tabla de campos de §6.4, sino también como elemento de persistencia explícito de esta fila) | §6.4, §6.4bis |
| `ERP.Application/Modules/Purchases/UseCases/RegisterPaymentUseCases.cs` (o equivalente) | Existente | Modificación necesaria | Adquisición de Lock A por cada `PurchaseInvoiceId` involucrado, dentro de transacción explícita nueva | §15.2 |
| `ERP.Application/Modules/Purchases/UseCases/IssueWithholdingUseCases.cs`, `CancelWithholdingUseCases.cs` | Existentes | Modificación necesaria | Igual — adquisición de Lock A | §15.2, §15.7 |
| `ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs` | Existente | Modificación necesaria | Transacción explícita + Lock A (hallazgo adicional de la auditoría, no solo de esta devolución) + nuevas validaciones `PI-CANC-01`/`PI-CANC-02` (§5.1 casos 1/2, §21) | §15.2, §5.1 |
| `ERP.Application/Modules/Purchases/UseCases/` | — | Elemento nuevo justificado | `PurchaseReturnDraftUseCases`, `AuthorizePurchaseReturnUseCases`, `CancelPurchaseReturnUseCases`, `RegisterSupplierCreditNoteUseCases`, `PurchaseReturnQueryUseCases`, `PurchaseReturnAuditHandler` | §16, §18, §20 |
| `ERP.Application/Modules/Finance/UseCases/` | — | Elemento nuevo justificado | `ApplySupplierCreditUseCases`, `RegisterSupplierCreditRefundUseCases` (resuelve destino + crea `CashMovement` si aplica, §13.6), `ReverseSupplierCreditApplicationUseCases`, `ReverseSupplierCreditRefundUseCases`, `SupplierCreditAuditHandler`, `CompanyFinancialDestinationAuditHandler`, y — reemplazando el CRUD genérico previamente descrito (corrección residual 7) — cuatro casos de uso limitados y explícitos, ninguno de los cuales permite update estructural ni delete físico: `CreateCompanyFinancialDestinationUseCase` (crea con los 8 campos estructurales, inmutables desde ese momento, §6.4ter), `UpdateCompanyFinancialDestinationNameUseCase` (edita únicamente `Name`), `ChangeCompanyFinancialDestinationAccountingAccountUseCase` (edita únicamente `AccountingAccountId`, valida `Account` activa/postable/mismo tenant-company, nunca afecta transacciones ya confirmadas, §6.4bis), `SetCompanyFinancialDestinationActiveUseCase` (edita únicamente `IsActive`, `true`↔`false`, nunca elimina físicamente) | §6.3, §6.4, §6.4ter, §13, §20.2 |
| `ERP.Application/Modules/Accounting/Posting/Translators/` | — | Elemento nuevo justificado | `PurchaseReturnAuthorizedPostingTranslator` (asiento compuesto con línea condicional de variación de costo, §19.1bis), `PurchaseReturnCancelledPostingTranslator`, `SupplierCreditAppliedPostingTranslator`, `SupplierCreditApplicationReversedPostingTranslator`, `SupplierCreditRefundedPostingTranslator` (**corrección residual 8** — consume `SupplierCreditRefundTransaction.AccountingAccountId` ya validado y congelado; nunca vuelve a resolverlo desde `CompanyFinancialDestination`, §19.1ter/§6.4bis), `SupplierCreditRefundReversedPostingTranslator` (consume el `AccountingAccountId` heredado en la `SupplierCreditRefundTransaction(REFUND_REVERSED)` y referencia el hecho contable original para construir el inverso exacto, §19.3) | §19 — Posting Engine sin cambios (Open/Closed) |
| `ERP.API/Controllers/` | — | Elemento nuevo justificado | `PurchaseReturnController` (`api/v1/purchases/returns`), `SupplierCreditController` (`api/v1/finance/supplier-credits`), `CompanyFinancialDestinationController` (`api/v1/finance/financial-destinations`, Company Settings) — expone únicamente los cuatro casos de uso limitados de la fila anterior (crear, renombrar, cambiar cuenta contable, activar/desactivar), **sin** endpoint de update estructural genérico ni de delete físico (corrección residual 7). `CreateDraftCommand`/`ApplySupplierCreditCommand`/comandos equivalentes **no** exponen `BranchId` como campo de entrada (Branch Ownership Rule, §5.2); los DTOs de lectura (`PurchaseReturnDto`, `SupplierCreditDto`) sí exponen `BranchId` como dato de solo lectura para listados/reportes | Ubicación consistente con §6.3, §6.4, §5.2 |
| `ElectronicDocuments`, `Ride`, `DocumentSequence` | Existentes | **Elemento expresamente prohibido** — sin cambios | Ninguno | Decisión fiscal §3.13 — no se emite documento propio; `ReturnNumber` usa `PurchaseReturnSequence` nuevo, no `DocumentSequence` (§7.1bis) |
| `Payment`/`PaymentApplicationLine` | Existente | **Elemento expresamente prohibido** — sin modificar su esquema | Ninguno (no se reutiliza tal cual, §6, §6.4) | Migrar su CHECK/enum/`PaymentDirection` para forzar el caso de crédito o de reembolso alteraría infraestructura en producción ajena a P0-02 |
| `ERP.Domain/Modules/Caja/Entities/CashMovement.cs`/`CashSession.cs`/`CashRegister.cs` | Existente | **Esquema sin modificar — solo consumo vía repositorio ya existente** | Ninguna columna/entidad nueva en `Caja`; `CompanyFinancialDestination.CashRegisterId` referencia `CashRegister` ya existente por FK real, y `SupplierCreditRefundTransaction` crea un `CashMovement` real dentro de una `CashSession` activa (§13.6) usando el factory ya existente | Corrección del destino financiero — a diferencia de la versión previa de este documento (que descartaba `Caja` por completo), ahora `CashRegister` **sí** es el destino real cuando `DestinationTypeCode = CASH_REGISTER`; lo que permanece prohibido es acoplar el reembolso a una `CashSession`/`CashMovement` como **identidad** del destino — la identidad estable es `CashRegisterId`, la sesión/movimiento son el efecto transaccional de cada reembolso (§6.4) |
| `ERP.Domain/Modules/Accounting/Entities/Account.cs` | Existente | **Elemento expresamente prohibido** — sin modificar su esquema | Ninguno | `CompanyFinancialDestination.AccountingAccountId` consume `Account` por FK real ya soportada (`AllowsPosting`, `IsActive`, `CompanyId`) — ninguna columna nueva necesaria (§6.4) |
| Posting Engine (`PostingFact`/`IPostingEngine`/`PostingRuleResolver`) | Existente | Reutilización sin cambios | Ninguno — solo traductores nuevos | §19.2 |
| Entity Audit (`AuditRecordBase`, `IAuditWriter<T>`, etc.) | Existente | Reutilización sin cambios | Ninguno — solo entidades/handlers nuevos por dominio | §20.1, Regla 1 de la infraestructura CLOSED |
| `frontend/src/modules/purchases/` | — | Elemento nuevo justificado | Páginas/componentes/servicio/schema de `PurchaseReturn` (draft, autorización, vínculo de NC) — espejo estructural de `SalesReturn` tras auditoría de reutilización obligatoria | §CLAUDE.md regla de reutilización DS |
| `frontend/src/modules/finance/` | — | Elemento nuevo justificado | Páginas/componentes de `SupplierCredit` (aplicación, reembolso, reversas) — consistente con que CxP/Pagos ya viven ahí | §6.3 |
| `PurchaseReceptionDocument` | Existente | Modificación necesaria (ver fila de columna `CurrencyCode` arriba) — el factory `Create(...)` se invoca desde un caso de uso nuevo, sin cambiar su comportamiento existente | Parámetro nuevo `currencyCode` en `Create(...)` | §18.1bis, §18.2 |

Ninguna ruta arriba es especulativa fuera de lo evidenciado — donde la carpeta exacta aún no existe (p. ej. `ERP.Application/Modules/Finance/UseCases/` para `SupplierCredit`), se indica la ubicación arquitectónica prevista según el criterio ya vigente en el repositorio (CxP/Pagos = Finance), no una ruta inventada sin base.

---

## 25. Deudas permitidas y exclusiones reales

### 25.1 Backlog no bloqueante (permitido explícitamente)

- Validación automática en línea de la NC contra el servicio público de consulta del SRI (decisión §3.19).
- Automatización avanzada de conciliación XML de la NC recibida (más allá de validación estructural y de duplicidad).
- Lotes/series (decisión §3.17 — Compras no los origina hoy, prerrequisito de infraestructura ajeno a P0-02).
- Nota de Débito emitida por el comprador (decisión §3.18 — sin caso de negocio evidenciado).
- Cardinalidad N:M entre `PurchaseReturn` y NC recibidas (§18.3) — v1 soporta 1:1; la extensión a varias NC por devolución o varias devoluciones por NC no afecta la integridad financiera ya resuelta en la autorización, es puramente de emparejamiento documental.
- Mejoras visuales/UX no indispensables del frontend de aplicación de crédito.
- Refactors generales no requeridos por este diseño.

### 25.2 No permitido como backlog (debe resolverse en el diseño de implementación, ya resuelto aquí)

Concurrencia de `PurchasePayable` (§12.3, §15), cantidades por línea (§10), idempotencia (§16.2), advisory locks (§15), crédito (§13), aplicación (§13, §15.3), reembolso (§13), reversas (§9.3, §22), contabilidad (§19), bloqueo por retención (§17), consistencia transaccional (§16.1), trazabilidad (§10.3), pruebas necesarias para validar el diseño transaccional.

**Prueba de validación obligatoria previa a la implementación** (no backlog, prerrequisito — repetido de §16.3 para visibilidad en esta sección): verificación empírica contra PostgreSQL real de la interacción entre `SaveChangesWithSequenceRetryAsync` y una transacción explícita ambiente abierta por el handler, ante un conflicto de secuencia. Debe ejecutarse y su resultado debe condicionar el diseño final del handler de autorización **antes** de escribir el código de producción.

---

## 26. Riesgos cerrados por el diseño

| Hallazgo obligatorio (encargo) | Solución exacta de este diseño |
|---|---|
| 1. `PurchasePayable` sin `xmin` | §12.3 — se agrega `xmin` en `PurchasePayableConfiguration`; todos los handlers mutadores (nuevos y existentes) lo capturan y traducen a error de negocio |
| 2. Sin advisory lock para devoluciones concurrentes | §15.1–§15.4 — Lock A `(TenantId, PurchaseInvoiceId)`, namespace `"PurchaseInvoice.FinancialLock"`, adquirido antes de revalidar bajo transacción explícita |
| 3. `StockMovement` sin referencia inequívoca a la línea | §10.3 — columna genérica nueva `SourceDocLineId`, poblada con `PurchaseReturnDetail.Id` |
| 4. Sin cantidad devuelta acumulada por línea | §10.2 — consulta derivada sobre `PurchaseReturnDetail`/`PurchaseReturn.Status`, sin contador mutable |
| 5. Sin `SupplierCredit` | §7.4, §13 — agregado nuevo completo, con movimientos, saldo derivado-cacheado y auditoría propia |
| 6. Sin aplicación de crédito a CxP | §13.4, §15.3, §22 (escenario 6) — `ApplyToPayable` + `PurchasePayable.ApplySupplierCredit()`, con locks A+B en orden fijo |
| 7. Sin registro de reembolso que cierre el crédito | §13, §22 (escenario 7) — `RegisterRefund`, movimiento tipo `Refund`, reutiliza catálogo `PaymentMethod` existente |
| 8. Sin idempotencia para solicitudes de devolución | §16.2 — `ClientRequestId` único por agregado/movimiento, sin infraestructura genérica nueva |
| 9. TOCTOU en el chequeo de retención | §15.7 — mismo Lock A compartido entre `AuthorizePurchaseReturnUseCases` e `IssueWithholdingHandler`/`CancelWithholdingHandler` |
| 10. Riesgo de `SaveChangesWithSequenceRetryAsync` + transacción explícita + conflicto de secuencia abortado | §16.3 — declarado como validación previa obligatoria (prueba de integración contra PostgreSQL real), no asumido, no dejado como backlog |

Adicionalmente, riesgos identificados por las auditorías más allá de los 10 obligatorios, también resueltos:

- Falta de transacción explícita/lock en handlers existentes de Compras en general (`CancelPurchaseHandler`, `RegisterPaymentCommandHandler`, `IssueWithholdingHandler`, `CancelWithholdingHandler`) — resuelto en §15.2 como modificación necesaria coordinada.
- `PurchasePayable.Status` no confiable — el diseño usa exclusivamente `BalanceDue` en toda fórmula (§3.4, §11, §12).
- Semántica ambigua de `StockMovementType.PurchaseReturn` compartida entre `CancelPurchaseUseCases` (reversión total) y el nuevo `PurchaseReturn` — diferenciada sin ambigüedad por `SourceDocType`/`SourceDocId` (§14.1).
- Reutilización indebida de `PaymentApplicationLine` para crédito — descartada explícitamente con justificación (§6, §24).
- Omisión de `BranchId` en `PurchaseReturn`/`SupplierCredit` (`PLAN-REV-01`, BLOCKER — hallazgo de la Architecture Review Board sobre el plan derivado de este diseño, corregido en la fuente) — resuelto en §5.2, con propagación a §6, §7.1, §7.4, §20, §24; sin excepción ni ADR, mismo patrón ya vigente de `PurchaseInvoice.BranchId`.

---

## 27. Preguntas de negocio realmente pendientes

`NINGUNA.`

**Corrección de diseño explícita (bloqueante 1)**: esta sección contiene únicamente decisiones genuinas de negocio que cambiarían comportamiento visible y que no puedan resolverse con evidencia ya existente. Tras la revisión completa de los 10 bloqueantes de esta corrección, ninguna decisión de ese tipo quedó pendiente — cada punto que en una versión previa de este documento se presentaba como "a confirmar" (§28 anterior) era en realidad una decisión técnica ya resuelta por el propio diseño con evidencia verificable:

- Cardinalidad 1:1 de la NC (§18.3) — resuelta con evidencia de que el tratamiento financiero ya ocurre íntegramente en `Authorize()`, antes de que exista la NC (§3.14, cerrada); no hay ningún caso de negocio evidenciado que requiera N:M dentro de P0-02.
- Separación de permisos Compras/Finance (§20.2) — resuelta por precedente ya vigente en el repositorio (CxP/Pagos ya viven en `modules/finance` con separación de riesgo equivalente).
- Modelo de `SupplierCreditMovement` como colección única (§13.2) — resuelto por invariantes estructurales idénticos entre aplicación y reembolso, evidenciados en el propio análisis de columnas comunes.
- Destino financiero persistido del reembolso (bloqueante 6, §6.4 — corrección final) — resuelto con evidencia positiva y negativa concreta contra el código real: `Payment`/`CashMovement`/`BankAccount` no sirven como identidad del destino (evidencia negativa, ya documentada), pero `Account` (`AllowsPosting`, `IsActive`, `CompanyId`-scoped) y `CashRegister` (`CompanyId`, `BranchId`) sí existen y son las entidades correctas para respaldar un destino real (evidencia positiva, re-verificada en esta corrección). Se diseña `CompanyFinancialDestination` (catálogo maestro persistido, con `AccountingAccountId`/`CashRegisterId` reales) y `SupplierCreditRefundTransaction` (hecho financiero append-only, sin dependencia circular con `SupplierCreditMovement`) — sin inventar un módulo general de Tesorería/Bancos.
- Numeración de `ReturnNumber` (bloqueante 10, §7.1bis) — resuelta con evidencia concreta de las dos FK reales de `DocumentSequence` que impiden su reutilización sin corromper catálogos SRI oficiales; se diseña la extensión mínima justificada.
- Cancelar una devolución después de registrar la NC (bloqueante 5, §5.1 caso 8) — resuelta por la decisión ya cerrada §3.14 (vincular NC nunca tiene efecto financiero) sin necesitar una regla nueva no evidenciada.

**Política de bodega (bloqueante 6 de la tercera revisión) — `DECISIÓN DE NEGOCIO CERRADA`, no una pregunta pendiente**: la devolución sale exclusivamente de la misma bodega registrada en cada detalle de la factura de compra original (`WarehouseId = PurchaseInvoiceDetail.WarehouseId`, §7.2, §14). Esta decisión fue confirmada directamente por el propietario del ERP y no se reinterpreta ni se vuelve a presentar como abierta en ningún documento futuro de esta fase: el usuario no selecciona ni edita la bodega (solo lectura, §7.2), una devolución puede tener líneas de distintas bodegas cuando los detalles originales ingresaron en bodegas distintas, la autorización valida stock suficiente en la bodega original de cada línea bajo Lock A (§15.5), y una falta de stock en cualquier línea bloquea toda la autorización (todo o nada, §14.2) exigiendo un traslado de inventario previo hacia la bodega original — nunca una toma automática de otra bodega ni una distribución de la misma línea entre varias bodegas.

Ninguna de estas es una decisión de negocio nueva sin resolver — son decisiones de diseño técnico derivables de los principios y evidencia ya proporcionados, verificadas contra el código real en esta revisión. Este documento no oculta ninguna decisión pendiente real para poder declararse completo: si alguno de los 10 bloqueantes hubiera generado una pregunta genuina sin evidencia suficiente para resolverla técnicamente, se habría declarado explícitamente en esta sección.

---

## 28. Checklist de completitud del diseño

**Corrección de diseño explícita (bloqueante 1)**: esta sección ya no pide "confirmación" de decisiones técnicas — eso duplicaba innecesariamente el contenido ya resuelto en el propio documento y sugería falsamente que había una aprobación de negocio pendiente cuando en realidad son verificaciones técnicas. Se reformula como checklist de completitud: cada punto es verificable leyendo el propio documento, no una opinión a recabar.

1. **Cardinalidad de la NC**: §18.3 define la cardinalidad 1:1 con justificación de negocio explícita, consecuencia futura declarada y códigos de rechazo exactos (`SC-009`, `SC-012`) — verificable, no pendiente.
2. **Separación de permisos**: §20.2 define qué operaciones son de Compras y cuáles de Finance, con justificación de riesgo operativo — verificable, no pendiente.
3. **Modelo de `SupplierCreditMovement`**: §13.2 y §7.5 definen la colección única con sus `CHECK` combinados completos — verificable, no pendiente.
4. **Prueba de validación previa**: §16.3 la declara explícitamente como prerrequisito bloqueante de implementación (no backlog) — su ejecución es un paso del futuro plan de implementación, no una condición de aprobación de este documento.
5. **Modificaciones a handlers existentes de Compras/Finance**: §15.2 y §24 las enumeran exhaustivamente (`RegisterPaymentCommandHandler`, `ReversePaymentCommandHandler`, `IssueWithholdingHandler`, `CancelWithholdingHandler`, `CancelPurchaseHandler` — 5 handlers, mismo criterio único de Lock A) con su alcance exacto de cambio — verificable, no pendiente.
6. **Idempotencia**: §16.2 cubre las 8 operaciones obligatorias con mecanismo, alcance de unicidad y comportamiento de reintento completos (bloqueante 2) — verificable, no pendiente.
7. **Máquina de estados**: §9.1/§9.2 no contienen ninguna afirmación contradictoria entre celdas (bloqueante 3) — verificable, no pendiente.
8. **Invariantes cruzadas de agregados**: §5.1 cubre los 9 casos exigidos con locks, revalidaciones y códigos de error (bloqueante 5) — verificable, no pendiente.
9. **Ecuación contable balanceada**: §19.1bis demuestra algebraicamente que `Σdébitos = Σcréditos` en todos los casos, incluida la diferencia costo histórico/valor reconocido (bloqueante 7) — verificable, no pendiente.
10. **Fórmula de `SupplierCredit.AvailableAmount`**: §13.5 la define completa, con signo por movimiento (bloqueante 8) — verificable, no pendiente.
11. **Destino financiero del reembolso**: §6.4 define `CompanyFinancialDestination` (persistido, normalizado, con `AccountingAccountId`/`CashRegisterId` reales) y `SupplierCreditRefundTransaction` (sin dependencia circular con `SupplierCreditMovement`), §13.6/§6.4quater el flujo transaccional completo con bloqueos de fila, §19.1ter/§6.4bis la derivación y congelamiento histórico de la cuenta contable, §6.4ter la inmutabilidad de la identidad económica del destino, §6.4quinquies la política histórica exacta de reversa, §21 el catálogo de errores (`SC-020`–`SC-029`, con `SC-023`/`SC-027` diferenciados sin contradicción), §16.5 las 26 pruebas obligatorias — verificable, no pendiente.
12. **Branch Ownership Rule**: §5.2 define `PurchaseReturn.BranchId`/`SupplierCredit.BranchId` como propiedades obligatorias, inmutables, nunca recibidas del cliente, sin excepción arquitectónica ni ADR — corrige `PLAN-REV-01` (BLOCKER, ARB) en la fuente del diseño, con propagación verificable a §6, §7.1, §7.4, §20.1, §20.2, §24, §16.2ter — verificable, no pendiente.

Este documento **no requiere aprobación de negocio** para considerarse completo — es un diseño técnico. Cuando exista un futuro plan de implementación (documento independiente, fuera de este alcance), **ese** documento es el que requerirá aprobación formal, tanto técnica como de negocio, antes de iniciar código — consistente con §1 ("Todavía no está autorizado para implementación").
