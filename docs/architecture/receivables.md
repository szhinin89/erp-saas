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

`ReverseCollectionCommand` (motivo obligatorio) reversa el `Payment` y devuelve el monto a la CxC:
libera primero la cuota del cobro y el resto desde la última cuota con cobrado, sin dejar cobrado
negativo. Hoy no tiene endpoint HTTP ni pantalla.

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

- No existe pantalla de listado o detalle de cobros; se consultan por la CxC (cobrado/saldo).
- Una cuota cobrada antes de que la cuota acumulara lo cobrado conserva cobrado 0; no se reparan
  datos existentes.
