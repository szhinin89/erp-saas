# PurchaseCreditNote (Descuento/Promoción) — Diseño Definitivo (FLOW-READY-02C)

## 1. Estado y autoridad del documento

| Campo | Valor |
|---|---|
| Estado | `APPROVED` (con ajustes menores obligatorios, ver §0) |
| Aprobación | `DESIGN_APPROVED: YES` — aprobado por el usuario tras ronda de auditoría técnica y decisión de arquitectura sobre delegación a `PurchaseReturn`; la aprobación documental no autoriza por sí sola la implementación |
| Fase | FLOW-READY-02C |
| Fecha | 2026-08-09 |
| Precede a | Plan de implementación por fases (documento independiente, no creado todavía) |
| Documentos relacionados | `docs/archive/designs/P0-02_PURCHASE_RETURN_DESIGN.md` (CLOSED, no modificado por este diseño), `docs/decisions/ADR-031-credit-note-v1-activation.md` (Nota de Crédito SRI para Ventas, referencia de convención, no aplica aquí — este módulo no emite NC, solo registra/aplica una NC propia de descuento) |

**Nota explícita**: este documento es el diseño técnico definitivo de `PurchaseCreditNote` v1 (alcance: descuento/promoción sin stock). **Todavía no está autorizado para implementación** más allá de lo indicado en §9 (plan incremental). No contiene código final, migraciones, DTOs, ni pruebas reales — solo el diseño de dominio, API y frontend que debe servir de base a un plan de implementación por fases.

---

## 0. Decisión de arquitectura y ajustes obligatorios sobre la primera versión del diseño

### 0.1 Decisión de arquitectura (confirmada con el usuario)

La pantalla `/purchases/credit-notes/new` es **centralizada** para el usuario, pero la lógica de negocio se bifurca:

| Tipo elegido en pantalla | Qué ocurre realmente |
|---|---|
| **Devolución de productos** | La pantalla **delega** en el flujo `PurchaseReturn` ya existente (P0-02, CLOSED). No se crea fila de `PurchaseCreditNote`. Inventario, CxP, `SupplierCredit`, reversas y auditoría siguen siendo exactamente los de hoy — cero código nuevo en ese camino. |
| **Descuento / promoción** | Se crea un `PurchaseCreditNote` (aggregate nuevo, este diseño). Nunca mueve stock. Reduce CxP directamente hasta el saldo pendiente. |

Esto evita reimplementar devolución física en un segundo aggregate, lo que habría duplicado inventario+CxP+`SupplierCredit` (regla "1 concepto = 1 implementación", `CLAUDE.md`) y habría requerido reabrir el diseño CLOSED de `SupplierCredit` (origen no-nullable `SourcePurchaseReturnId`). Con esta decisión, `SupplierCredit`, `PurchaseReturn`, `IStockRepository` **no se tocan en absoluto** por este módulo.

### 0.2 Ajustes obligatorios exigidos en la aprobación (reemplazan la versión preliminar del diseño)

1. **Excedente sobre saldo pendiente → bloquear, no truncar.** Si `Subtotal+VatAmount` de la NC de descuento supera `PurchasePayable.BalanceDue` de la factura afectada, `Authorize()` **falla** con un error de validación explícito. **No** se trunca a `BalanceDue` ni se genera `SupplierCredit` en v1 (la versión preliminar proponía truncar; queda descartada). Ver §4.2.
2. **Contabilidad sin efecto en esta fase, de forma explícita y verificable.** Puede publicarse un evento de dominio `PurchaseCreditNoteAuthorized` como punto de extensión, pero **sin traductor contable registrado y sin `PostingFact`** — el evento, si existe, no debe tener ningún handler productivo suscrito en esta fase. Ver §4.3.
3. **Snapshot obligatorio de los valores financieros aplicados al autorizar**, incluso cuando los datos fiscales (número, clave de acceso, autorización) provienen de un `PurchaseReceptionDocument` vinculado. El snapshot financiero (`AppliedToPayableAmount`, `Subtotal`, `VatAmount`, `TotalAmount` congelados) es siempre propiedad de `PurchaseCreditNote`, nunca derivado on-demand del documento de recepción. Ver §5.3.
4. **XML es solo referencia/comparación — documentado explícitamente.** La operación de devolución (tipo Return) se basa siempre en las líneas de la compra original (`PurchaseInvoiceDetail` vía `PurchaseReturn`), nunca en las líneas del XML de la NC recibida. Ver §2.3 y §6.
5. **Persistencia del diseño** en `docs/archive/designs/FLOW-READY-02C_PURCHASE_CREDIT_NOTE_DESIGN.md` (este archivo) antes de iniciar implementación — cumplido con la creación de este documento.

---

## 1bis. Alcance y exclusiones

### Dentro de alcance v1

- Registro y autorización de `PurchaseCreditNote` tipo **Discount/Promoción** únicamente.
- Reducción de CxP (`PurchasePayable.BalanceDue`) hasta el saldo pendiente de la factura afectada.
- Vinculación opcional a un `PurchaseReceptionDocument` (tipo `CreditNote`) importado por TXT/XML en el reception tray.
- Pantalla centralizada de entrada que también permite iniciar una devolución física, delegando en `PurchaseReturn` sin duplicar su lógica.
- Cancelación/reversa auditada de `PurchaseCreditNote`.

### Fuera de alcance v1 (decisiones cerradas, no se reabren en este documento)

- Generación de `SupplierCredit` a partir de `PurchaseCreditNote` (bloqueado en su lugar, ver §0.2.1).
- Cualquier asiento contable (`PostingFact`, traductor) para `PurchaseCreditNote`.
- Movimiento de inventario en `PurchaseCreditNote` (nunca aplica a Discount).
- Reimplementación de devolución física — sigue siendo, sin cambios, responsabilidad exclusiva de `PurchaseReturn`.
- Emisión de un documento SRI propio — `PurchaseCreditNote` únicamente **registra** una NC que emitió el proveedor (mismo principio que `PurchaseReturn` con la NC del proveedor).

---

## 2. Modelo de dominio

### 2.1 `PurchaseCreditNote` (nuevo agregado, dominio Purchases — solo tipo Discount)

```
PurchaseInvoice (existente, FROZEN por su propio ciclo de vida)
        │  (referenciado por Guid, sin FK de navegación — mismo patrón que PurchaseReturn → PurchaseInvoiceId)
        ▼
PurchaseCreditNote (nuevo agregado)
        │
        ├── PurchaseCreditNoteDetail[] (líneas de concepto, sin inventario)
        │
        └── ReceptionDocumentId? → PurchaseReceptionDocument (existente, tipo CreditNote) — referencia, no copia
```

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | Guid | PK |
| `TenantId, CompanyId, BranchId` | Guid | Branch Ownership Rule obligatoria — mismo error que P0-02 corrigió en su ronda de enmienda; no se repite aquí |
| `SupplierId` | Guid | debe coincidir con `PurchaseInvoice.SupplierId` de la factura referenciada |
| `PurchaseInvoiceId` | Guid | factura afectada, obligatoria, inmutable tras `Draft` |
| `ReceptionDocumentId` | Guid? | FK a `PurchaseReceptionDocument` (tipo `CreditNote`); opcional — permite alta manual sin recepción TXT/XML |
| `Status` | enum | `Draft \| Authorized \| Cancelled` |
| `CreditNoteNumber` | string | obligatorio al autorizar; si hay `ReceptionDocumentId`, se muestra desde ahí, no se duplica como editable (§2.2) |
| `AccessKey` | string? | idem |
| `AuthorizationNumber, AuthorizationDate` | string?/DateTime? | idem |
| `IssueDate` | DateTime | fecha del documento, obligatoria |
| `Reason` | string | concepto del descuento/promoción, obligatorio |
| `Subtotal, VatAmount, TotalAmount` | decimal(18,2) | editables en `Draft`, **congelados** (snapshot) al autorizar — ver §5.3 |
| `AppliedToPayableAmount` | decimal(18,2)? | snapshot al autorizar — siempre igual a `TotalAmount` en v1 porque el excedente bloquea (§0.2.1), pero se modela como campo independiente para no acoplar el snapshot financiero a `TotalAmount` si una fase futura habilita crédito parcial |
| `AuthorizedAtUtc, AuthorizedByUserId` | DateTime?/Guid? | |
| `CancelledAtUtc, CancelledByUserId, CancellationReason` | DateTime?/Guid?/string? | |
| `CreateClientRequestId, CreateRequestPayloadHash` | Guid?/string? | idempotencia, mismo mecanismo que `PurchaseReturn` |
| `AuthorizeClientRequestId, AuthorizeRequestPayloadHash` | Guid?/string? | idem |
| `CancelClientRequestId, CancelRequestPayloadHash` | Guid?/string? | idem |
| `Lines` | `PurchaseCreditNoteDetail[]` | colección del agregado |

### 2.2 `PurchaseCreditNoteDetail` (nuevo — líneas de concepto, no de inventario)

| Campo | Tipo | Notas |
|---|---|---|
| `Id, TenantId, PurchaseCreditNoteId` | Guid | |
| `Description` | string | concepto de la línea de descuento |
| `Subtotal` | decimal(18,2) | |
| `VatCode, VatRate` | string?/decimal? | opcional según ítem afectado, sin `ItemId` obligatorio |
| `VatAmount, TotalAmount` | decimal(18,2) | |

No hay `ItemId`, `WarehouseId`, `Quantity` ni `AffectsStock` — no existe caso Return en esta entidad; la tabla es exclusivamente para Discount. Esto difiere del modelo polimórfico sugerido en el encargo original y es intencional (§0.1): evita una columna `AffectsStock` que siempre sería `false` y campos de inventario siempre `null`.

### 2.3 Relación con el XML de la NC recibida — regla explícita (ajuste obligatorio §0.2.4)

`PurchaseReceptionDocument.Lines` (las líneas del XML/TXT del proveedor) **nunca** alimentan `PurchaseCreditNoteDetail` ni ninguna operación de `PurchaseReturn`. Se muestran en una sección de solo lectura, marcada visualmente como "referencia del proveedor", exclusivamente para comparación manual. La fuente de verdad operativa es siempre:
- Para Discount: lo que el usuario captura en `PurchaseCreditNoteDetail`.
- Para Return: las líneas de `PurchaseInvoiceDetail` de la compra original, vía el editor ya existente de `PurchaseReturn` (`PurchaseReturnableLinesEditor`, sin cambios).

---

## 3. Rutas y pantalla

**Frontend**
- `/purchases/credit-notes/new?invoiceId=<id>` — pantalla centralizada de selección de tipo.
- `/purchases/credit-notes/new?invoiceId=<id>&receptionDocumentId=<id>` — variante desde reception tray.
- `/purchases/credit-notes/:id` — detalle, solo instancias `PurchaseCreditNote` (Discount). Una NC tipo Return no genera `id` en este módulo; redirige a `/purchases/returns/:id`.

**Comportamiento de la pantalla**
1. **Factura afectada**: proveedor, RUC, número, fecha, total, saldo pendiente — igual en ambos tipos, consulta `PurchaseInvoice` + `PurchasePayable.BalanceDue`.
2. **Selector de tipo**:
   - *Devolución* → navega a `/purchases/returns/new?invoiceId=<id>` (pantalla existente sin cambios); si viene `receptionDocumentId`, se propaga como query param para que, en un paso posterior no bloqueante, `PurchaseReturn` pueda ofrecer vincular la NC recibida vía el flujo ya existente `RegisterAndLinkSupplierCreditNoteHandler`.
   - *Descuento/promoción* → formulario propio: concepto, subtotal, IVA, total; sin líneas de inventario obligatorias.
3. **Detalle XML recibido** (solo si `receptionDocumentId` presente) — lectura de `PurchaseReceptionDocument.Lines`, badge "referencia del proveedor" (§2.3).
4. **Resumen de afectación**: reduce CxP: sí (ambos tipos); mueve inventario: dinámico según tipo; contabilidad: "no se genera en esta fase" (fijo, sin excepción).

**Desde `/purchases/reception`**: si la fila es NC y la factura afectada existe (ya implementado, `PurchaseReceptionVerifier`, commit `1adddaf8`), el botón "Procesar NC" abre `/purchases/credit-notes/new?invoiceId=<affectedPurchaseId>&receptionDocumentId=<id>` en pestaña nueva. El usuario decide el tipo ahí; no se infiere automáticamente.

**Desde detalle de compra**: se complementa (no se reemplaza) el botón `Devolución` actual con una acción `Nota de crédito` que abre la pantalla centralizada. Decisión de UX de bajo riesgo, no bloqueante para el diseño de dominio.

---

## 4. Comportamiento de CxP (revisado por ajuste obligatorio §0.2.1)

### 4.1 Extensión de `PurchasePayable`

Se agrega un cuarto track paralelo, copia estructural exacta de `ApplySupplierCredit`/`ReverseSupplierCredit` (`PurchasePayable.cs:186-221`):

- `CreditNoteAppliedAmount` (decimal, acumulador).
- `ApplyCreditNote(decimal amount, Guid updatedBy)`.
- `ReverseCreditNote(decimal amount, Guid updatedBy)`.
- `BalanceDue` extendido: `TotalAmount − PaidAmount − TotalRetained − ReturnAppliedAmount − SupplierCreditAppliedAmount − CreditNoteAppliedAmount`.

### 4.2 Regla de bloqueo por excedente (reemplaza la versión preliminar que truncaba)

En `AuthorizePurchaseCreditNoteHandler`:

```
saldoPendiente = purchasePayable.BalanceDue   // leído bajo lock, misma disciplina que PurchaseReturn.Authorize()
si (creditNote.TotalAmount > saldoPendiente):
    fallar con error de validación explícito ("La nota de crédito excede el saldo pendiente de la factura; no se puede autorizar")
    no persistir ningún efecto
si no:
    purchasePayable.ApplyCreditNote(creditNote.TotalAmount, actorId)
    creditNote.AppliedToPayableAmount = creditNote.TotalAmount   // snapshot, ver §5.3
    creditNote.Authorize(...)
```

No existe camino donde `PurchaseCreditNote` genere `SupplierCredit` en v1. Si en el futuro aparece un caso de negocio real que lo requiera, es un cambio de dominio que toca `SupplierCredit` (agregar un segundo origen nullable) y **requiere su propia confirmación/ADR**, igual que ADR-031 hizo con la activación de Nota de Crédito SRI — no se preautoriza aquí.

### 4.3 Contabilidad — sin efecto, verificable (ajuste obligatorio §0.2.2)

- `AuthorizePurchaseCreditNoteHandler` puede publicar un evento de dominio `PurchaseCreditNoteAuthorized` (mismo mecanismo síncrono FROZEN de eventos de dominio que usa `PurchaseReturn`), pero **en esta fase no se registra ningún `IDomainEventHandler`/traductor contable para ese evento**.
- No se crea ningún `PostingFact` con `FactType="PurchaseCreditNote"` en esta fase.
- Verificación de cierre: un test de integración debe confirmar explícitamente que autorizar un `PurchaseCreditNote` no inserta ninguna fila en la tabla de `PostingFact` — mismo criterio de verificación que otras fases han usado para confirmar ausencia de efecto no deseado.

---

## 5. Comportamiento de inventario y snapshot financiero

### 5.1 Inventario

`PurchaseCreditNote` (Discount) **nunca** mueve stock — no se toca `IStockRepository` en absoluto para este módulo. Tipo Return sigue siendo responsabilidad exclusiva de `PurchaseReturn` (sin cambios, `StockMovementType.PurchaseReturn`).

### 5.2 Duplicados

- `(TenantId, ReceptionDocumentId)` único cuando no nulo — 1 documento de recepción → máx. 1 `PurchaseCreditNote` (patrón inverso al que ya usa `PurchaseReturn.SupplierCreditNoteDocumentId`).
- `(TenantId, AccessKey)` único filtrado (`access_key IS NOT NULL`) — mirror de `PurchaseInvoiceConfiguration`/`PurchaseReceptionDocumentConfiguration`.
- `(TenantId, CompanyId, SupplierId, CreditNoteNumber)` único — mirror exacto de `uq_purchase_invoices_tenant_company_supplier_number`.
- Violaciones capturadas vía `IDatabaseExceptionTranslator.TryGetUniqueViolation` + `ConstraintName`, mismo patrón que `RegisterAndLinkSupplierCreditNoteHandler.cs:310-330`.

### 5.3 Snapshot financiero obligatorio (ajuste obligatorio §0.2.3)

Independientemente de si `ReceptionDocumentId` está presente:

- `Subtotal`, `VatAmount`, `TotalAmount` y `AppliedToPayableAmount` de `PurchaseCreditNote` son **siempre** propiedad almacenada del propio agregado, calculados/congelados en el momento de `Authorize()` — nunca derivados on-demand desde `PurchaseReceptionDocument`.
- Lo único que se lee por referencia desde `PurchaseReceptionDocument` (nunca se copia como editable) son los **datos fiscales de identificación** del documento: `CreditNoteNumber`, `AccessKey`, `AuthorizationNumber`, `AuthorizationDate` — mismo principio SSOT que P0-02 aplicó a `PurchaseReturn.SupplierCreditNoteDocumentId` (dato fiscal por referencia, dato financiero por snapshot propio).
- Esta separación existe precisamente para que una futura corrección/reproceso del `PurchaseReceptionDocument` (p. ej. reimportación) nunca pueda alterar retroactivamente el efecto financiero ya aplicado y auditado de un `PurchaseCreditNote` autorizado.

---

## 6. Relación con recepción y con la compra original

- `PurchaseCreditNote.PurchaseInvoiceId` es obligatorio y es la única fuente de la factura afectada (nunca el XML) — igual para el flujo delegado de Return, que usa `PurchaseInvoiceDetail` vía `PurchaseReturn` (§2.3).
- `PurchaseCreditNote.ReceptionDocumentId`, cuando existe, vincula con `PurchaseReceptionDocument` (tipo `CreditNote`) y al autorizar se llama `PurchaseReceptionDocument.MarkProcessed(...)` (método ya existente, `PurchaseReceptionDocument.cs:213-221`), sin cambios a esa entidad.

---

## 7. Endpoints / comandos / queries

Nuevo controller `PurchaseCreditNoteController`, `[Route("api/v1/purchases/credit-notes")]`, `[Authorize]`, reutilizando `PurchasePermissions.{View,Create,Update}` (sin permisos nuevos, mismo criterio documentado en `PurchaseReturnController`).

| Endpoint | Comando/Query | Notas |
|---|---|---|
| `POST /` | `CreateDraftPurchaseCreditNoteCommand` | valida `SupplierId` de la factura, `BalanceDue > 0` |
| `PUT /{id}` | `UpdatePurchaseCreditNoteDraftCommand` | solo en `Draft` |
| `GET /{id}` | `GetPurchaseCreditNoteByIdQuery` | incluye datos de `PurchaseReceptionDocument` si `ReceptionDocumentId` presente (§5.3) |
| `GET /` | `GetPurchaseCreditNoteListQuery` | filtros `status/supplierId/page/pageSize` |
| `POST /{id}/authorize` | `AuthorizePurchaseCreditNoteHandler` | bloqueo por excedente (§4.2), snapshot financiero (§5.3), sin efecto contable (§4.3), auditoría (ADR-022) |
| `POST /{id}/cancel` | `CancelPurchaseCreditNoteHandler` | reversa simétrica: `ReverseCreditNote` sobre `PurchasePayable` |

---

## 8. Reutilización de componentes

| Componente | Decisión |
|---|---|
| `PurchaseReturnableLinesEditor` | No se usa por `PurchaseCreditNote` — Discount no tiene líneas de inventario; queda intacto, sin tocar. Se reutiliza sin cambios en el flujo delegado de Return (`PurchaseReturn`) |
| `IStockRepository` | No se usa en este módulo |
| `SupplierCredit` | No se toca en v1 (§4.2) |
| `PurchaseReceptionDocument.MarkProcessed` | Reutilizado tal cual |
| `IDatabaseExceptionTranslator` + patrón `ConstraintName` | Reutilizado, mismo patrón que `RegisterAndLinkSupplierCreditNoteHandler` |
| `PurchasePermissions` | Reutilizado sin nuevas constantes |

---

## 9. Riesgos

1. **UX de "dos flujos detrás de un botón"**: al elegir Devolución, el usuario "sale" del módulo NC hacia `/purchases/returns`. Debe comunicarse claramente en la UI — es la decisión confirmada (§0.1), no un enrutamiento roto.
2. **Bloqueo por excedente sin alternativa en v1** (§4.2): si aparece un caso real de descuento que supera el saldo de la factura, requiere decisión de negocio explícita y su propia confirmación/ADR antes de tocar `SupplierCredit`.
3. **Dos accesos a "Nota de crédito" desde detalle de compra** (botón existente `Devolución` + nueva acción `Nota de crédito`): riesgo de confusión de UX, no bloqueante para el backend.
4. **Ninguna infraestructura FROZEN se modifica** (`SupplierCredit`, `PurchaseReturn`, `IStockRepository`, `PurchaseReceptionDocument` fuera de `MarkProcessed` ya existente) — riesgo de regresión sobre `/purchases/returns` y `/purchases/reception` es bajo.

---

## 10. Plan incremental sugerido

1. **Fase 1 — Dominio**: `PurchaseCreditNote`/`PurchaseCreditNoteDetail` (Domain + EF config + migración), extensión de `PurchasePayable` (`CreditNoteAppliedAmount` + `ApplyCreditNote`/`ReverseCreditNote`), sin exponer API todavía.
2. **Fase 2 — Application/API**: comandos/queries CRUD + `authorize`/`cancel`, tests de integración (idempotencia, duplicados, bloqueo por excedente §4.2, ausencia de `PostingFact` §4.3, snapshot financiero §5.3).
3. **Fase 3 — Frontend**: pantalla centralizada, formulario Discount, sección XML de referencia (solo lectura), enlace desde reception tray y desde detalle de compra.
4. **Fase 4 (futuro, fuera de alcance, requiere su propia confirmación/ADR)**: contabilidad (`PostingFact`/traductor) y, si aparece caso real, `SupplierCredit` con segundo origen para Discount que exceda el saldo.

---

**Estado: `APPROVED` (ajustes §0.2 incorporados).** **Alcance: `PurchaseCreditNote` v1 — solo tipo Descuento/Promoción.** **Fecha: 2026-08-09.** **Responsable: Sebastian Zhinin (Lead/Architect del proyecto).**

No autoriza implementación por sí solo — requiere plan de implementación por fases derivado de este documento (§10), siguiendo el mismo protocolo que P0-02 (diseño → plan → implementación por fases → gate de gobernanza).
