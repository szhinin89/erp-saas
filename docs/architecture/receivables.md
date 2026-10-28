# Cuentas por Cobrar (CxC) — SSOT funcional

Comportamiento implementado y comprobado de `SalesReceivable`, sus cuotas y los cobros de cliente.
Concurrencia, idempotencia y anulación/devolución se definen en [backend.md](./backend.md); la
contabilización, en [accounting.md](./accounting.md). Este documento enlaza esas reglas, no las
repite. Evidencia de cierre: [STATUS](../../STATUS.md) y
[matriz QA](../QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md).

## Origen

| Origen | Cuándo se crea | Cuotas |
|---|---|---|
| Factura de venta | Al autorizar una factura a crédito (condición con días de crédito o más de una cuota y método de pago `Crédito`) con saldo pendiente. El importe es el saldo no cubierto por medios no-Crédito, nunca el total si hubo abono. | Copia del cronograma congelado de la factura; sin cronograma, se generan desde la condición de pago. |
| Saldo inicial (IL-5) | Carga inicial confirmada, sin factura. Detalle en [FEATURES](../../FEATURES.md). | Una cuota por el saldo. |

La CxC pertenece a la empresa (`company_id`) y al tenant del documento de origen.

## Saldo, cuotas y estado

- `BalanceDue = OriginalAmount − PaidAmount`. El saldo es la señal de «pagada».
- Cada cuota guarda `Amount`, `PaidAmount` y `OutstandingAmount`, con estado `pending` /
  `partial` / `paid` según lo cobrado. La suma del saldo de las cuotas acompaña al saldo de la CxC.
- `SalesReceivable.Status` solo distingue el ciclo de vida (`pending` o `cancelled`); no pasa a
  `paid`. El listado traduce los filtros «Pendientes»/«Pagadas» a saldo > 0 / saldo ≤ 0, y
  «Anuladas» a `cancelled`.
- Estado visible (`statusLabel`), en este orden: `Anulada` (cancelada) → `Pagada` (saldo 0) →
  `Vencida` (la primera cuota no saldada venció según la fecha de la empresa) → `Parcial` (hay
  cobros) → `Pendiente`. «Vence» y los días de mora salen de esa primera cuota no saldada.

## Registrar un cobro

`POST /api/v1/finance/collections` (`RegisterCollectionCommand`, permiso `finance.create`). La UI
`/finance/receivables` (consulta con permiso `sales.view`) registra un cobro contra una CxC, con la
cuota opcional, forma de pago, cuenta bancaria o caja de destino y referencia.

1. `ClientRequestId` obligatorio:
   [idempotencia](./backend.md#idempotencia-de-comandos-financieros-creadores-de-dinero-zh-financial-command-idempotency-01)
   y [bloqueo del saldo](./backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01).
2. Cuenta bancaria y caja de destino son excluyentes; ambas son opcionales.
3. Antes de modificar nada, y con las CxC bloqueadas: cada CxC debe ser del cliente del cobro; la
   cuota indicada debe ser de esa CxC y el monto aplicado no puede exceder el saldo de la cuota.
   Cualquier línea inválida rechaza el cobro completo.
4. La CxC rechaza un cobro mayor a su saldo o sobre una CxC cancelada.
5. Reparto en cuotas: si hay cuota indicada, se aplica solo a ella; si no, por orden de cuota
   (FIFO de vencimiento).
6. Una CxC fuera de la empresa activa no se encuentra (404); nunca se cobra entre empresas.

## Reversa de un cobro

`POST /api/v1/finance/collections/{id}/reverse`, con `{ reason }` y permiso específico
`finance.collections.reverse`, delega en `ReverseCollectionCommand`. La UI de CxC ofrece
«Consultar / anular cobro» y permite seleccionar el cobro, confirmar su importe total e indicar
motivo obligatorio (hasta 500 caracteres). El permiso se asigna desde la pantalla de CxC del
catálogo del Kernel. La consulta `GET /api/v1/finance/collections/receivable/{id}` exige el mismo
permiso y devuelve únicamente cobros de la CxC dentro del tenant y empresa activos.

La anulación es exclusivamente una corrección documental de un registro erróneo, **no una
devolución de dinero**. Conserva el `Payment` original en `Reversed`, sus aplicaciones, motivo,
fecha UTC y actor (`UpdatedBy`, expuesto como `reversedByUserId`); mantiene `CollectionReversed`
en Entity Audit y la contabilización existente. Restaura la CxC: libera primero la cuota del
cobro y el resto desde la última cuota con cobrado, sin dejar cobrado negativo. Se anula el cobro
completo, incluidas sus aplicaciones a otras CxC. Después puede registrarse un cobro correcto
con una nueva intención (`ClientRequestId`).

Precondiciones, antes de mutar y dentro de la transacción existente:

- Lock del `Payment` y de las CxC; todas deben pertenecer a la empresa activa, estar sin anular y
  conservar cobrado suficiente para la suma de sus aplicaciones. Una operación posterior
  incompatible bloquea todo el reverso. La segunda anulación se rechaza sin otro efecto.
- Cualquier movimiento de caja referenciado al cobro bloquea, incluso si su turno está cerrado.
  La comprobación consulta los movimientos mediante sus sesiones, con TenantId + CompanyId.
- El registro vigente de CxC no crea `CashMovement`, no vincula `CashSession` ni crea un movimiento
  bancario operativo; sus destinos son datos del cobro y del posting contable. La anulación no
  modifica turnos, arqueos ni movimientos operativos, ni genera egresos o transferencias.
- Para históricos sin `ClientRequestId` y con destino de caja/banco se bloquea: no se puede
  demostrar la procedencia operativa. Sin destino y sin referencias operativas se permite la
  corrección documental. No se infiere un turno a partir de fechas ni se reabre uno cerrado.
- La reversión contable y su gate de período conservan el contrato de
  [Accounting §7](./accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad); no se reimplementan.

Implementación con validación unitaria focal; prueba funcional integrada de UI/HTTP/PostgreSQL
pendiente por instrucción del propietario. Estado y gates: [STATUS](../../STATUS.md).

## Devolución y anulación

- Devolución de venta autorizada con crédito a CxC: reduce `OriginalAmount` (nunca lo cobrado) y
  reconstruye las cuotas sobre el saldo, con sus números y vencimientos.
- Anular una factura cuya CxC tiene cobros se rechaza. Caminos excluyentes y bloqueos:
  [backend.md](./backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01).

## Contabilidad

Venta (`Sales/InvoiceIssued`: Debe CxC clientes) y cobro (`Finance/CollectionApplied`: Debe caja o
banco elegido —sin destino, la cuenta Debe de la regla—, Haber CxC clientes) registran obligaciones
durables; ver
[accounting.md § 10](./accounting.md#10-integración-y-matriz-de-atomicidad) y
[§ 19](./accounting.md#19-integraciones-durables-accounting-continuity-04).

## Límites vigentes

- Los cobros se consultan desde la CxC; no existe un listado global independiente.
- Las devoluciones de dinero por cobros quedan fuera de esta corrección documental y pendientes
  de una decisión funcional separada.
- Una cuota cobrada antes de que la cuota acumulara lo cobrado conserva cobrado 0; no se reparan
  datos existentes.
