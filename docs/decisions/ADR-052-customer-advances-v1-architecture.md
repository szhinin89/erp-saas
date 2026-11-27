# ADR-052 — Anticipos de Clientes V1 (Finance)

Estado: **ACCEPTED / APROBADO** — aprobación formal del propietario el **2026-11-26**, decisiones TA-01 a TA-13 incluidas.
**La aceptación aprueba el diseño; no autoriza CA-F2 a CA-F6** ni sus componentes protegidos. Autorizada aparte, el mismo día,
solo **CA-F1** (estado en [STATUS](../../STATUS.md)).

Fuentes: [diseño funcional y técnico](CUSTOMER-ADVANCES-V1-DESIGN-01.md) (reglas y mecanismos; no se copian aquí) y
[plan de implementación](../agent/CUSTOMER-ADVANCES-V1-IMPLEMENTATION-PLAN-01.md) (fases, rutas y pruebas). El comportamiento
implementado tendrá su SSOT cuando existan código y pruebas ([enforcement](../architecture/enforcement.md#ssot-por-módulo)).
Estado de entrega: [STATUS](../../STATUS.md).

**Etiquetas:** **APROBADO** (decisión funcional del propietario, diseño §0.1) · **EXISTENTE** (comprobado en código) ·
**PROPUESTO** (mecanismo de este ADR, aprobado con él; vigente en el código solo desde la fase que lo implemente) · **NO VERIFICADO** (se comprueba en la fase indicada).

## 1. Contexto

- EXISTENTE: un cobro sin CxC es imposible (`Payment.Apply` exige líneas balanceadas); no hay libro de saldo a favor de
  cliente ni hecho contable de anticipo; la cuenta `2.1.01.002 Anticipos de clientes` está sembrada y sin uso.
- EXISTENTE (precedentes): `SupplierCredit` y su reembolso ([ADR-035](ADR-035-supplier-payment-unapplied-advance.md); precedente
  de patrón, no contrato equivalente), idempotencia y locks de [backend.md](../architecture/backend.md#idempotencia-de-comandos-financieros-creadores-de-dinero-zh-financial-command-idempotency-01),
  obligaciones durables y reversos de [Accounting](../architecture/accounting.md), Entity Audit
  ([ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md)).

## 2. Decisiones funcionales (APROBADO; no se reabren)

D-01 a D-08 y D-10 a D-12 y los siete criterios del [diseño §0.1](CUSTOMER-ADVANCES-V1-DESIGN-01.md#01-aprobación-del-propietario-y-estado-documental-2026-11-24):
Finance propietario; `CustomerAdvance` con un único libro de movimientos; registro sin factura; aplicación parcial o total a CxC
existentes; saldo del cliente y la empresa; aislamiento por tenant y sin operaciones entre empresas; sucursal como origen y
trazabilidad, con aplicación entre sucursales de la misma empresa; efectivo en el turno de caja y transferencia contra cuenta
bancaria sin movimiento de caja; devoluciones con saldo, permiso y evidencia; reversiones trazables; historial inmutable y
auditoría; interfaz junto a CxC; contabilidad oficial; sin cambios en la emisión de facturas. D-09 está **retirada** y no genera
trabajo. Anticipos no se implementa dentro de Spa. La aprobación funcional de D-04, D-05, D-08 y D-10 **no** autoriza tocar
Kernel, Caja, `backend.md` ni Contabilidad.

## 3. Invariantes (PROPUESTO como contrato verificable de las reglas aprobadas)

| ID | Invariante |
|---|---|
| CA-INV-01 | `0 ≤ AvailableAmount ≤ OriginalAmount`; el disponible se deriva de los movimientos y la BD lo refuerza con `CHECK` |
| CA-INV-02 | Un único libro append-only; nada se edita ni se borra; toda corrección es un movimiento compensatorio |
| CA-INV-03 | Una intención produce como máximo un efecto (idempotencia por operación) y un movimiento se revierte como máximo una vez |
| CA-INV-04 | Anticipo, CxC, destino, caja y turno pertenecen al mismo tenant y empresa; fuera de alcance → 404 |
| CA-INV-05 | La sucursal nunca filtra ni limita saldo o aplicación |
| CA-INV-06 | Una CxC solo se reduce o restaura con `SalesReceivable.RegisterCollection` / `ReverseCollection`; nunca se crea un `Payment` ni se dispara `CollectionApplied` |
| CA-INV-07 | `PaidAmount` de una CxC = cobros vigentes + aplicaciones de anticipo vigentes |
| CA-INV-08 | Cada movimiento con efecto contable genera como máximo una obligación, identificada por su id; un movimiento operativo de caja nunca es fuente contable |
| CA-INV-09 | Ninguna operación deja saldo cero mientras el dinero recibido sigue en la empresa sin tratamiento |
| CA-INV-10 | Operación, movimientos, caja, CxC, obligación contable, auditoría y outbox se confirman en una transacción o no queda ningún efecto |

## 4. Agregado, entidades y ciclo de vida (PROPUESTO; el agregado y su libro están APROBADOS)

| Entidad (módulo Finance) | Responsabilidad |
|---|---|
| `CustomerAdvance` (raíz; tenant + empresa) | Cliente, sucursal de origen, moneda de la empresa, original, disponible, fecha de negocio, medio, destino, referencia, motivo, estado; token `xmin` |
| `CustomerAdvanceMovement` (append-only) | `Received`, `Application`, `ReversalOfApplication`, `Refund`, `ReversalOfRefund`, `SourceCancelled`, `IncomeReversed`; monto, `ClientRequestId`, `LineNumber`, huella, `BranchId`, actor, instante UTC, fecha efectiva, motivo, evidencia, `ReceivableId`/`InstallmentId`, `ReversalOfMovementId`, turno y movimiento de caja del ingreso y de su reversa |
| `CustomerAdvanceRefundTransaction` (append-only) | Destino usado, método, referencia, fecha efectiva y vínculo de caja de una devolución y de su reversa (patrón del reembolso de proveedor) |
| `CustomerAdvanceAudit` | Entity Audit oficial, sin modificar la infraestructura FROZEN |

**Ciclo de vida:** sin borrador. `Confirmed` → `Cancelled` solo por anulación administrativa o reversa del ingreso, ambas
exigiendo el anticipo íntegro. Un anticipo agotado sigue `Confirmed`. `Cancelled` es terminal.

**Persistencia y restricciones físicas:** tablas `customer_advances`, `customer_advance_movements`,
`customer_advance_refund_transactions` y `customer_advance_audit`; `numeric(18,2)`; `CHECK` del disponible, de montos positivos,
de exactamente un destino, de CxC obligatoria solo en aplicación y su reversa y de referencia de reversa solo en reversas; índices
únicos `(tenant_id, client_request_id, line_number)`, parcial por `reversal_of_movement_id`, un solo `Received` y una sola
terminación por anticipo; FK `Restrict` a anticipo y CxC. **`installment_id` sin FK física**: las cuotas se reconstruyen o
eliminan (`RebuildInstallments`, `Cancel`) y `PaymentApplicationLine` ya la omite.

## 5. Identidad, idempotencia, aplicaciones y reversas

Se adopta el [diseño §14.1](CUSTOMER-ADVANCES-V1-DESIGN-01.md#141-idempotencia-de-una-aplicación-a-varias-cxc) (PROPUESTO):
una `ClientRequestId` por operación; líneas `1..N` en aplicación y su reversa y `0` en las demás; huella
`CanonicalRequestFingerprint` versionada **que incluye el tipo de operación**; replay buscado por tenant + empresa + clave
bajo el lock del anticipo (misma huella → resultado original; distinta → 409; violación del índice → conflicto sin efectos).

- **Aplicaciones múltiples:** un comando aplica a una o varias CxC del mismo cliente; una línea inválida rechaza todo
  ([diseño §5](CUSTOMER-ADVANCES-V1-DESIGN-01.md#5-aplicación-a-cxc-aplicaciones-múltiples-y-doble-consumo)).
- **Unidad de reversa (TA-04):** se revierte la operación de aplicación completa (todas sus líneas, identificada por la
  `ClientRequestId` original); cada línea genera su movimiento y su espejo contable. Sin reversa parcial.
- **Compensaciones:** las cuatro operaciones y la reversa de devolución del
  [diseño §6](CUSTOMER-ADVANCES-V1-DESIGN-01.md#6-anulación-reversas-y-devoluciones), con las condiciones de §6 de este ADR.

## 6. Condiciones exactas de anulación, reversa y devolución (PROPUESTO; el diseño las remite a este ADR)

Toda anulación, reversa y devolución exige motivo (≤ 500), referencia textual, usuario autenticado, fecha efectiva,
instante UTC y Entity Audit. Sin adjuntos ni segunda aprobación.

| Operación | Condiciones adicionales |
|---|---|
| Reversa de aplicación | En cada CxC de la operación: no cancelada, cobrado suficiente y **suma de lo cobrado en sus cuotas = `PaidAmount`** (TA-06); si una falla, 422 para toda la operación, sin efectos |
| Anulación administrativa | Íntegro; declaración explícita «el dinero no fue recibido» del actor con `.reverse` + referencia; sin movimiento de caja (por tanto, nunca un anticipo en efectivo); `AccountingPostingCancellation.PrepareAsync` no devuelve asiento publicado (si lo devuelve, se rechaza sin efectos) |
| Reversa del ingreso | Íntegro; referencia del deshacer real y su fecha; efectivo: egreso compensatorio en el turno abierto, con titularidad y fondos |
| Devolución | Monto ≤ disponible; medio, destino único, fecha efectiva y referencia de ejecución; efectivo: titularidad y fondos |
| Reversa de devolución | Solo no ejecutada; referencia y fecha de la recuperación; monto igual y mismo destino; efectivo: ingreso compensatorio en turno abierto; una sola vez |

CA-F4 verifica además que no se creen aplicaciones sobre CxC con inconsistencias conocidas que impedirían su reversa
posterior; los saldos no se corrigen artificialmente.

**Fecha efectiva de toda reversa (TA-07):** la de la propia operación de reversión (fecha de negocio oficial: día de empresa de
su instante, como `AccountingReversalRecorder` usa hoy), que el usuario no selecciona; es también la fecha contable. Rige el gate de período
oficial. La fecha que conste en una evidencia externa es un dato, no la fecha contable.

## 7. Concurrencia (PROPUESTO; principio D-08 APROBADO)

`FOR UPDATE` de la fila del anticipo acotado a tenant y empresa, recarga y decisión posterior; CxC con
`GetByIdsForUpdateAsync` en Id ascendente; `xmin` y `CHECK` como última barrera. Orden:
anticipo → CxC; o anticipo → destino `FOR SHARE` → cuenta `FOR SHARE` → turno `FOR UPDATE`. Ningún flujo toma CxC y turno a la
vez ([diseño §14.2](CUSTOMER-ADVANCES-V1-DESIGN-01.md#142-bloqueos-y-concurrencia-de-customeradvance)). Se incorpora a
`backend.md` en la fase que lo implemente.

## 8. Aislamiento multiempresa e integración con CxC

- `ITenantScopedEntity` e `ICompanyOperationalEntity`; requests con alcance de empresa y sucursal activa validada; tenant,
  empresa, sucursal y actor nunca llegan del cliente. La aplicación no filtra la CxC por sucursal.
- CxC: reutiliza validaciones y reparto en cuotas del cobro mediante una **regla compartida extraída** del handler de cobros
  (TA-12), sin copiarla. `Payment`, `EnsureBalanced` y `SalesReceivable` no cambian.
- EXISTENTE (consecuencias): los lectores de `PaidAmount` (listado, estados, filtros, KPI) reflejan la aplicación sin cambios;
  una factura con anticipo aplicado no se anula hasta revertirlo; la consulta y anulación de cobros no muestra aplicaciones.
- Frontend: no existen exportaciones ni reportes de CxC o cobros (verificado por búsqueda estática el 2026-11-26; las únicas
  descargas son RIDE, XML, retenciones y plantillas de carga inicial). La columna «Cobrado» incluirá lo aplicado por anticipo.
- **Limitación EXISTENTE detectada por lectura, no reproducida:** tras `ApplyReturnCredit` + `RebuildInstallments`, las cuotas
  nuevas nacen con cobrado 0, de modo que una reversa posterior reduce `PaidAmount` sin devolver saldo a las cuotas. Afecta
  hoy a la reversa de cobros; este ADR no la corrige ni reabre CxC. TA-06 impide que una reversa de Anticipos la agrave;
  consecuencia: una aplicación sobre una CxC ya desalineada no podrá revertirse mientras siga así.

## 9. Caja y Bancos

- Patrón del reembolso de proveedor: transacción, destino `FOR SHARE`, turno abierto bloqueado, movimiento interno
  `ManualIncome`/`ManualExpense` con `CashReferenceType.CustomerAdvance = 6`. `ReferenceId` = operación original (movimiento
  `Received` para el ingreso y su reversa; transacción de devolución original para la devolución y su reversa).
- **Titular del turno (TA-13):** se aplica la regla vigente `CashSession.IsControlledBy`, usada por todos los flujos de caja
  elegidos por un usuario; solo el titular del turno abierto de esa caja registra o mueve efectivo. Otra regla exigiría cambiar
  Caja y no se propone. La caja puede ser de otra sucursal de la empresa.
- Salidas (devolución y reversa del ingreso): `CashSessionAvailability.OverdraftMessage`.
- Banco: sin `CashMovement`; la evidencia es la referencia.
- **Medio y destino (TA-02):** solo efectivo (`PaymentMethod.AffectsPhysicalCash`) → caja, y transferencia bancaria
  (`PaymentMethod.DetailType = Transfer`, no crediticio ni de efectivo) → cuenta bancaria. Exactamente un destino, validado con
  `PaymentMethodDestinationPolicy` y `RequiresReference`; tarjeta, cheque, crédito y cualquier otro medio se rechazan. La
  restricción vive en Anticipos y no cambia la política para otros flujos. Sustituye el «destino opcional / sin destino» del
  diseño §3, §8 y §10.

## 10. Hechos contables (PROPUESTO; asientos APROBADOS en D-10)

| Hecho (`Finance`) | Identidad (`SourceEventId`) | Regla sembrada | Fecha |
|---|---|---|---|
| `CustomerAdvanceReceived` | Movimiento `Received` | Debe `1.1.01.001` (sustituido por la cuenta del destino) / Haber `2.1.01.002` | Fecha del anticipo |
| `CustomerAdvanceApplied` | Cada movimiento `Application` | Debe `2.1.01.002` / Haber `1.1.03.001` | Fecha efectiva de la aplicación |
| `CustomerAdvanceRefunded` | Movimiento `Refund` | Debe `2.1.01.002` / Haber `1.1.01.001` (sustituido por el destino) | Fecha efectiva |

Montos `GrandTotal`; reversas por `IAccountingReversalRecorder` (espejo; obligación sin asiento → cancelada); sin motor ni
asientos manuales. Los tres hechos se agregan a la **completitud contable** (vigencia y orígenes sin contabilizar). El origen
documental del asiento queda sin resolver, como los de saldo a favor de proveedor.

**Empresas existentes (verificado):** el bootstrap siembra reglas solo al aprovisionar; en una empresa ya aprovisionada el hecho
aparece «Sin regla» con la acción «Configurar» ([Accounting §23](../architecture/accounting.md#23-reglas-contables-y-balance-general-accounting-11)),
y una operación previa a configurarla deja la obligación `Failed` reprocesable. Sin comando de backfill. Las cuentas de
empresas reales no se inspeccionaron.

## 11. Auditoría y permisos

- Eventos desde el agregado (`CustomerAdvanceReceived`, `Applied`, `ApplicationReversed`, `Refunded`, `RefundReversed`,
  `Cancelled`, `IncomeReversed`, con sufijo `Event`) → `CustomerAdvanceAudit`. Nada se audita a mano.
- Permisos independientes (APROBADO; nombres PROPUESTOS): `finance.view` para consultas y
  `finance.customer-advances.{create,apply,reverse,refund}` según el [diseño §7](CUSTOMER-ADVANCES-V1-DESIGN-01.md#7-historial-trazabilidad-y-permisos);
  toda operación con movimiento real de dinero exige `.refund`. Sin asignación automática a perfiles.

## 12. Contrato API (PROPUESTO)

Base `api/v1/finance/customer-advances`; controller delgado, MediatR, envelope `ApiResponse<T>`; paginación según la
convención del módulo (NO VERIFICADO, CA-F3).

| Método y ruta | Permiso | Entrada principal |
|---|---|---|
| `GET /` · `GET /{id}` | `finance.view` | Filtros: cliente, estado, sucursal; el detalle incluye movimientos |
| `GET /balance?customerId=` | `finance.view` | Saldo = Σ disponibles `Confirmed` de la empresa |
| `GET /by-receivable/{receivableId}` | `finance.view` | Aplicaciones recibidas por la CxC |
| `POST /` → 201 | `.create` | `clientRequestId`, cliente, monto, fecha, medio, destino, referencia, motivo |
| `POST /{id}/applications` | `.apply` | `clientRequestId`, fecha efectiva, líneas `{receivableId, installmentId?, amount}` |
| `POST /{id}/applications/{operationId}/reverse` | `.reverse` | `clientRequestId`, motivo, referencia |
| `POST /{id}/cancel` | `.reverse` | `clientRequestId`, motivo, declaración, referencia de evidencia |

| `POST /{id}/reverse-income` | `.refund` | `clientRequestId`, motivo, referencia y fecha de evidencia |
| `POST /{id}/refunds` | `.refund` | `clientRequestId`, monto, fecha, medio, destino, referencia, motivo |
| `POST /{id}/refunds/{transactionId}/reverse` | `.refund` | `clientRequestId`, motivo, referencia y fecha de recuperación |

Replay: mismo status y cuerpo que la operación original. En las reversas, usuario y fecha efectiva salen del servidor.

**Errores de dominio:** el catálogo del [diseño §10](CUSTOMER-ADVANCES-V1-DESIGN-01.md#10-casos-de-prueba-concurrencia-y-errores)
con los códigos vigentes (`VALIDATION_ERROR` 422, `CONFLICT` 409, `DOMAIN_RULE_VIOLATION` 422, 404, 403 y el código de período
existente). No se crean códigos nuevos salvo necesidad demostrada y autorizada. NO VERIFICADO (CA-F3): validación oficial de
«cliente activo de la empresa» que debe reutilizarse (el cobro actual solo valida que la CxC sea del cliente).

## 13. Integración frontend (PROPUESTO; ubicación APROBADA)

Módulo `finance`: ruta `/finance/customer-advances` (lista → detalle), ítem de menú bajo el grupo «Cuentas por cobrar» y, en
`/finance/receivables`, «Aplicar anticipo» y las aplicaciones recibidas. Reutilización y reglas de UI del
[diseño §9](CUSTOMER-ADVANCES-V1-DESIGN-01.md#9-interfaz-mínima-reutilizando-patrones). Sin cambios en Ventas ni en la emisión
de facturas.

## 14. Decisiones técnicas

Estados: **RESUELTA** (confirmada por el propietario; no se vuelve a pedir mientras no cambien las condiciones de despliegue)
· **APROBADA** (aprobación formal del 2026-11-26). Aprobar el ADR **no** autoriza por sí solo fases ni componentes protegidos.

| ID | Mecanismo | Estado |
|---|---|---|
| TA-01 | Agregado `CustomerAdvance`, movimientos append-only, transacción de devolución y Entity Audit (§4); `Payment` sin cambios | APROBADA |
| TA-02 | Destino único y obligatorio: efectivo → caja; transferencia (`DetailType = Transfer`) → cuenta bancaria; otros medios rechazados (§9) | APROBADA |
| TA-03 | Una `ClientRequestId` por operación; único `(tenant_id, client_request_id, line_number)`; huella canónica con tipo de operación; replay o 409 (§5) | APROBADA |
| TA-04 | Reversa de la operación de aplicación completa; un movimiento y un espejo contable por línea, cada uno ligado a su original (§5) | APROBADA |
| TA-05 | `FOR UPDATE` del anticipo, `xmin` y `CHECK`; orden anticipo → CxC (Id asc.) o anticipo → destino → cuenta → turno (§7) | APROBADA |
| TA-06 | Reversa solo si cada CxC está activa, tiene cobrado suficiente y Σ cobrado de cuotas = `PaidAmount`; si no, 422 sin efectos ni ajustes de saldo; CA-F4 evita crear aplicaciones sobre CxC con inconsistencias conocidas (§6) | APROBADA |
| TA-07 | Fecha efectiva = día de empresa de la operación de reversión; período contable oficial (§6) | APROBADA |
| TA-08 | Motivo, referencia, usuario, fecha y Entity Audit obligatorios; sin adjuntos ni doble aprobación; la anulación administrativa conserva la declaración «el dinero no fue recibido» (§6) | APROBADA |
| TA-09 | Tres hechos `Finance/*` con identidad por movimiento, reversas por espejo y completitud; empresas existentes por «Configurar», sin backfill (§10) | APROBADA |
| TA-10 | `finance.view` y `finance.customer-advances.{create,apply,reverse,refund}`; contrato API (§11–§12) | APROBADA |
| TA-11 | El ERP nunca se desplegó en producción y todas las BD son de desarrollo y pruebas: el esquema se incorpora a la [baseline única](../DATABASE.md#baseline-ef-erp-zh-v1-greenfield), sin migraciones incrementales. Vigente mientras no cambien las condiciones de despliegue. No autoriza eliminar bases de datos | **RESUELTA** |
| TA-12 | Regla compartida de validación cliente/cuota/saldo extraída de `PaymentUseCases`, sin cambiar su comportamiento (CA-F4) | APROBADA |
| TA-13 | `CashSession.IsControlledBy`: solo el titular del turno abierto opera efectivo (§9) | APROBADA |

## 15. Alternativas descartadas

Extender `Payment` con remanente sin CxC (rompe un invariante de todos los cobros); reutilizar las tablas de `SupplierCredit`
(atadas a proveedor y CxP); registrar cobros ficticios al aplicar (duplicaría caja y contabilidad); saldo dentro de Spa o
Ventas; motor financiero, contable, de seguridad o de auditoría paralelo.

## 16. Riesgos

| # | Riesgo | Control |
|---|---|---|
| 1 | Una BD de desarrollo que ya aplicó la baseline no recibe las tablas nuevas al editarla en sitio (mismo identificador) | Recrearla solo con autorización expresa para esa operación; nunca automáticamente |
| 2 | Empresa existente opera antes de configurar las tres reglas | Obligaciones `Failed` reprocesables; configurar antes de habilitar permisos |
| 3 | Cobro y anticipo sobre la misma CxC y cuota redistribuyen cuotas al revertir (comportamiento ya existente entre cobros) | Prueba mixta con CA-INV-07 y suma de cuotas |
| 4 | Limitación de cuotas reconstruidas (§8) | TA-06 en Anticipos; informada como hallazgo independiente |
| 5 | La anulación administrativa solo aplica a anticipos bancarios sin asiento publicado; con Contabilidad activa la ventana es corta | Esperado: usar reversa del ingreso o devolución |
| 6 | Anticipos sin asiento no detectados | Completitud contable en la misma fase que las reglas |
| 7 | Efectivo operable solo por el titular del turno | Regla vigente confirmada (TA-13) |

## 17. Precisión posterior: cronología de las aplicaciones (APROBADO 2026-11-27)

Instruida por el propietario en CA-F4; complementa §5 y §7 sin modificar decisiones anteriores.

- La fecha efectiva de una aplicación es igual o posterior a la fecha del anticipo (`CustomerAdvance.AdvanceDate`) y a la
  fecha de origen contable de **todas** las CxC involucradas, y no posterior al día de negocio oficial de la empresa.
- Fecha de origen contable: CxC de factura → `SalesInvoice.IssueDate`; CxC de saldo inicial →
  `Company.OpeningBalanceDate` (cuando la deuda entra en los libros; la emisión del documento nunca es posterior).
- Una fecha incompatible o que no pueda determinarse rechaza la operación completa (422) sin efectos ni consumo de la clave.
- Se valida después del replay y con anticipo y CxC bloqueados: un reintento de una aplicación aceptada no se reevalúa.
- No cambia TA-07, el gate de modo y período contable ni las fechas de los cobros.
- **Registro:** `CustomerAdvance.AdvanceDate` no puede ser posterior al día de negocio oficial de la empresa; se evalúa
  después del replay y un rechazo (422) no produce efectos ni consume la clave.
- **Devolución (CA-F5):** su fecha efectiva no es anterior a la fecha del anticipo ni posterior al día de negocio.
  Ninguna reversa ni anulación es anterior a la operación que compensa; su fecha contable sigue TA-07 y la fecha de la
  evidencia externa es solo trazabilidad.
- **Fecha de evidencia:** en la reversa del ingreso y de una devolución no es posterior al día de negocio ni anterior a
  la operación que se compensa; se evalúa después del replay y un rechazo (422) no produce efectos ni consume la clave.

## Referencias

[Diseño](CUSTOMER-ADVANCES-V1-DESIGN-01.md) · [Plan](../agent/CUSTOMER-ADVANCES-V1-IMPLEMENTATION-PLAN-01.md) ·
[CxC](../architecture/receivables.md) · [Accounting](../architecture/accounting.md) · [Backend](../architecture/backend.md) ·
[Seguridad](../architecture/security.md) · [Estándares de datos](../architecture/data-standards.md) ·
[Auditoría](../architecture/audit-infrastructure.md) · [Enforcement](../architecture/enforcement.md)
