# Anticipos de clientes — SSOT (Finance)

Fuente vigente del comportamiento **implementado y probado** de Anticipos de Clientes: registro y consultas (CA-F3),
aplicación a CxC con su reversa (CA-F4) y devolución, reversa de devolución, reversa del ingreso y anulación
administrativa (CA-F5), sobre el dominio de CA-F1 y la contabilidad de CA-F2. **No existen** pantallas, menú ni
asignación de permisos a perfiles.

Decisiones: [ADR-052](../decisions/ADR-052-customer-advances-v1-architecture.md). Contrato contable:
[Accounting §28](accounting.md#28-anticipos-de-clientes-customer-advances-v1-ca-f2). Idempotencia y locks:
[Backend](backend.md#idempotencia-de-comandos-financieros-creadores-de-dinero-zh-financial-command-idempotency-01).
Entrega: [STATUS](../../STATUS.md).

## 1. Modelo

`CustomerAdvance` (tenant + empresa) con un único libro `CustomerAdvanceMovement`. El anticipo nace `Confirmed`, sin
borrador, con disponible igual al original y un movimiento `Received`. El saldo es el disponible que mantiene ese libro: no
existe otro. No se crea factura, venta, cuenta por cobrar ni `Payment`. El historial no se borra ni se reescribe a través
del contexto; cada operación genera Entity Audit (`customer_advance_audit`).

## 2. API

Base `api/v1/finance/customer-advances`, envelope `ApiResponse<T>`.

| Método y ruta | Permiso | Alcance de contexto |
|---|---|---|
| `POST /` → 201 | `finance.customer-advances.create` | Empresa y sucursal activa autorizada |
| `GET /?page&pageSize&customerId&status&branchId` | `finance.view` | Empresa; `pageSize` 1–100 (25 por defecto) |
| `GET /{id}` (con movimientos) | `finance.view` | Empresa |
| `GET /balance?customerId=` | `finance.view` | Empresa; Σ disponibles de anticipos `Confirmed` |
| `POST /{id}/applications` → 200 | `finance.customer-advances.apply` | Empresa y sucursal activa autorizada |
| `POST /{id}/applications/{operationId}/reverse` → 200 | `finance.customer-advances.reverse` | Empresa y sucursal activa autorizada |
| `GET /by-receivable/{receivableId}` (sin paginación) | `finance.view` | Empresa; aplicaciones de la CxC, vigentes y revertidas |
| `POST /{id}/refunds` → 200 | `finance.customer-advances.refund` | Empresa y sucursal activa autorizada |
| `POST /{id}/refunds/{transactionId}/reverse` → 200 | `finance.customer-advances.refund` | Empresa y sucursal activa autorizada |
| `POST /{id}/reverse-income` → 200 | `finance.customer-advances.refund` | Empresa y sucursal activa autorizada |
| `POST /{id}/cancel` → 200 | `finance.customer-advances.reverse` | Empresa y sucursal activa autorizada |

El detalle incluye los movimientos y `refundTransactions` (devoluciones y sus reversas, con destino, caja y marca de
revertida); `{transactionId}` es el `id` de una devolución de esa lista.

La sucursal identifica el origen y es un filtro opcional del listado: nunca limita el detalle ni el saldo.

**Permisos.** Toda operación que mueve dinero real exige `.refund`. Ninguno de los cuatro permisos
`finance.customer-advances.*` está relacionado todavía con un ítem de navegación, por lo que **no son asignables a
perfiles**: hoy opera solo quien tiene acceso de administrador.

## 3. Registro

Entrada: `clientRequestId`, `customerId`, `amount`, `advanceDate`, `paymentMethodId`, `companyBankAccountId` **o**
`cashRegisterId`, `reference` (≤ 100), `reason` (≤ 500). Tenant, empresa, sucursal, moneda de la empresa y usuario salen
del contexto.

| Regla | Rechazo |
|---|---|
| Monto > 0 con la precisión monetaria fija; exactamente un destino | 422 |
| Fecha del anticipo no posterior al día de negocio de la empresa (evaluada después del replay) | 422 `VALIDATION_ERROR` |
| Socio de negocio del tenant (compartido por sus empresas), activo y con rol Cliente activo | 404 / 422 |
| Medio activo; efectivo físico → caja; transferencia (`DetailType = Transfer`) → cuenta bancaria; tarjeta, cheque, crédito y otros no se admiten | 404 / 422 |
| Referencia obligatoria si el medio la exige | 422 |
| Destino de la empresa y activo; su cuenta contable, de la empresa | 404 / 422 |
| Efectivo: turno abierto de esa caja y usuario titular; la caja puede ser de otra sucursal de la empresa | 422 |
| Empresa con modo contable decidido y fecha fuera de un período Closed/Locked | 422 |

En efectivo se registra en el turno un `ManualIncome` sin motivo con `ReferenceType = CustomerAdvance` (6) y
`ReferenceId` = movimiento `Received`, vinculado al anticipo; la transferencia no genera movimiento de caja. La referencia
visible en Caja se omite si no cabe (no se trunca).

Anticipo, movimiento, caja, auditoría, obligación contable y outbox se confirman en una transacción o no queda nada. El
asiento se intenta después del commit: una cuenta o regla inválida deja la obligación `Failed` reprocesable y el anticipo
vigente. El movimiento de caja nunca es fuente contable.

## 4. Aplicación a cuentas por cobrar

Entrada: `clientRequestId`, `effectiveDate` y `lines` (`receivableId`, `installmentId` opcional, `amount`), en el orden
que numera sus movimientos. No mueve dinero ni crea `Payment`, cobro o movimiento de caja: reduce cada CxC con
`SalesReceivable.RegisterCollection` ([CxC](receivables.md#aplicación-de-anticipos-de-cliente)).

| Regla (con anticipo y CxC bloqueados) | Rechazo |
|---|---|
| Anticipo y CxC en la empresa activa | 404 |
| Monto > 0 con la precisión monetaria fija; al menos una línea | 422 `VALIDATION_ERROR` |
| Anticipo vigente y Σ líneas ≤ disponible | 422 `DOMAIN_RULE_VIOLATION` |
| CxC del mismo cliente, no anulada; cuota de esa CxC con saldo suficiente | 422 |
| Varias líneas sobre una CxC o cuota: el acumulado no excede su saldo | 422 |
| Tras aplicar, Σ cobrado de las cuotas = `PaidAmount` (TA-06); una CxC sin cuotas o desalineada no admite anticipos | 422 |
| Empresa con modo contable decidido y fecha efectiva fuera de período Closed/Locked | 422 |
| Fecha efectiva no posterior al día de negocio de la empresa | 422 |
| Fecha efectiva no anterior a la fecha del anticipo ni al origen contable de cada CxC | 422 |
| Origen contable de cada CxC determinable | 422 |

**Cronología** ([ADR-052 §17](../decisions/ADR-052-customer-advances-v1-architecture.md#17-precisión-posterior-cronología-de-las-aplicaciones-aprobado-2026-11-27)):
el origen de una CxC de factura es `SalesInvoice.IssueDate`; el de una CxC de saldo inicial,
`Company.OpeningBalanceDate` (no la emisión del documento). Se evalúa después del replay, con los registros bloqueados.
Los cobros no tienen esta restricción.

Una línea inválida rechaza la operación completa. La CxC puede ser de otra sucursal de la empresa; cada movimiento guarda
la sucursal activa. Un movimiento, una auditoría y una obligación contable por línea, en una sola transacción.

## 5. Reversa de una aplicación

Entrada: `clientRequestId`, `reason` (≤ 500) y `reference` (≤ 100), ambos obligatorios; `operationId` es el
identificador de la aplicación original (campo `operationId` de sus movimientos). Se revierte la operación completa: CxC,
cuotas e importes salen de los movimientos originales y la fecha efectiva es el día de negocio de la reversa. Un
movimiento compensatorio por línea restaura el anticipo y, con `SalesReceivable.ReverseCollection`, cada CxC. El asiento se
espeja por el reverso común (sin asiento, la obligación pendiente se cancela). No devuelve dinero.

Rechazo completo, sin efectos: operación inexistente (404); ya revertida; alguna CxC anulada, sin cobrado suficiente o con
cuotas que no cuadran (TA-06); período cerrado para la fecha de la reversa (422). Una devolución de venta posterior a un
cobro o a una aplicación reconstruye las cuotas con cobrado 0 y deja esa CxC desalineada: no se corrigen saldos y la
aplicación no puede revertirse mientras siga así.

## 6. Devolución de dinero

Entrada: `clientRequestId`, `amount`, `effectiveDate`, `paymentMethodId`, `companyBankAccountId` **o**
`cashRegisterId`, `reference` (≤ 100) y `reason` (≤ 500), todos obligatorios. Devuelve hasta el saldo disponible; lo
aplicado a CxC no se devuelve sin revertir antes la aplicación. Medio, destino y cuenta contable siguen las reglas del
registro (§3) y el destino puede ser distinto del ingreso. La fecha efectiva va de la fecha del anticipo al día de
negocio de la empresa y es la fecha contable.

En efectivo: turno abierto de esa caja, usuario titular y sin sobregiro; `ManualExpense` con
`ReferenceType = CustomerAdvance` y `ReferenceId` = transacción de devolución. La transferencia **solo se registra**: no
se ejecuta en el banco ni crea movimiento de caja. Se crean el movimiento `Refund`, su transacción, auditoría,
obligación `CustomerAdvanceRefunded` y outbox en una transacción.

## 7. Reversa de una devolución

Solo para una devolución que no llegó a ejecutarse y cuyo dinero sigue en la empresa. Entrada: `clientRequestId`,
`reason`, `reference` de la recuperación y `evidenceDate`, obligatorios. Importe, destino y medio salen de la
transacción original; la fecha contable es el día de negocio de la reversa (TA-07), nunca anterior a la devolución.
`evidenceDate` es trazabilidad, no fecha contable: entre la fecha de la devolución y el día de negocio (422). En efectivo: turno abierto de la caja original, titular, `ManualIncome` con la
misma referencia que el egreso. Genera `ReversalOfRefund`, la transacción compensatoria, restaura exactamente el
importe y espeja el asiento (sin asiento, cancela la obligación). Se rechaza si ya fue revertida o el anticipo está
cancelado. Una devolución entregada al cliente no se revierte: un reintegro es un nuevo anticipo. El sistema exige la
evidencia declarada; no puede comprobar que el dinero se haya recuperado.

## 8. Reversa del ingreso

Para un ingreso que realmente se deshizo. Entrada: `clientRequestId`, `reason`, `reference` y `evidenceDate`,
obligatorios; `evidenceDate` entre la fecha del ingreso y el día de negocio (422). Exige el anticipo vigente e íntegro (sin aplicaciones ni devoluciones vigentes); importe y destino salen
del ingreso. En efectivo: turno abierto de la caja original, titular, sin sobregiro y `ManualExpense` referido al
movimiento `Received`; en transferencia no hay movimiento de caja. Genera `IncomeReversed`, saldo cero, estado
`Cancelled`, y espeja el asiento (sin asiento, cancela la obligación).

## 9. Anulación administrativa

Solo para un registro cuyo dinero **nunca se recibió**. Entrada: `clientRequestId`, `reason`, `reference` de evidencia
y `moneyNotReceived = true`. Exige anticipo vigente, íntegro, **por transferencia** (uno en efectivo tiene movimiento
de caja y no es elegible) y sin asiento publicado: si lo hay, toda la operación se rechaza. Genera `SourceCancelled`,
saldo cero y estado `Cancelled`, y cancela la obligación `Pending`/`Failed`; sin asiento, caja ni banco.

**Destinos históricos.** Las reversas conservan el destino original, que la entrada no admite elegir, y no exigen que
siga activo; el espejo usa las cuentas del asiento original. En efectivo rigen turno abierto, titularidad y, en las
salidas, fondos.

**Período contable** (en §7 a §9): se exige período abierto para el espejo de un asiento publicado
([Accounting §7](accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad)). Cancelar una obligación sin asiento —incluida
la anulación administrativa— no registra efecto contable y no la bloquea un período cerrado.

## 10. Idempotencia y concurrencia

Huella canónica V1 con el tipo de operación y solo los datos del usuario. Replay por tenant + empresa + clave, también
bajo el lock del anticipo y antes de reevaluar fechas, saldo o estado: misma huella → el mismo anticipo en su estado
vigente (201 en el registro, 200 en las demás); otra huella, operación o anticipo → 409; clave ya usada en otra empresa
del tenant → 409 sin datos de ella. Un rechazo no consume la clave. Barrera definitiva: índices únicos de la clave.
Entre operaciones que compiten por un anticipo gana la primera que lo bloquea y las demás se reevalúan. Orden de locks:
[backend.md](backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01).

## 11. Evidencia

`ERP.API.Tests/Integration/CustomerAdvanceApiTests`, `CustomerAdvanceApplicationApiTests` y
`CustomerAdvanceOpeningBalanceApiTests` (HTTP + PostgreSQL),
`CustomerAdvance*` en Domain, Application e Infrastructure.
