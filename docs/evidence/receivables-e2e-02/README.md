# Cuentas por cobrar — E2E de factura a crédito con varias cuotas

Fecha: 2026-10-27. Clasificación: **PASS** — 58/58 comprobaciones del flujo y 7/7 de aislamiento;
sin BUG REAL ni cambios de código de producción. Aprobado por el propietario (2026-10-27).
Comportamiento: [SSOT de CxC](../../architecture/receivables.md); cobertura:
[matriz QA](../../QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md). No repite el E2E de una cuota
([receivables-e2e-01](../receivables-e2e-01/README.md)). Sin commit, push ni deploy; sin migraciones
ni cambios de contrato API.

## Entorno

- Base `erp_zh_test` en la baseline vigente, sin recrear; usuario `e2e.admin` del seed E2E con
  credencial efímera no almacenada. Otras bases sin cambios.
- API aislada en `:5004` (Release del HEAD, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright) sobre un Vite de desarrollo propio en `:5181`; `/api/**` redirigido a
  `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`.

## Datos creados vía API

Factura de venta `001-001-000000003` por 300.00 (servicio E2E ×3) al cliente `Cliente E2E ZH`,
método `CREDITO`, con cronograma manual de 3 cuotas de 100.00 que vencen el 26/11/2026, 26/12/2026 y
25/01/2027, emitida y autorizada el 27/10/2026 (fecha de la empresa). La CxC copió el cronograma
congelado: 3 cuotas con el mismo número, fecha y monto.

## Flujo verificado en navegador

Todos los cobros desde `/finance/receivables` por transferencia al banco «Cuenta corriente E2E» con
referencia. Tras cada cobro se consultó la CxC por API, se recargó el listado y se reabrió el modal.

| Paso | Resultado |
|---|---|
| Modal inicial | Monto por defecto 300.00; opciones «Sin cuota específica» y cuotas #1–#3 con 100.00 pendiente ([captura](m0-modal-3-cuotas.png)) |
| Cuota #3 elegida con el monto por defecto 300.00 | Rechazado: «El monto aplicado excede el saldo pendiente de la cuota #3.»; modal abierto, sin cambios en la CxC ni filas en `payments` ([captura](c1a-rejected-exceeds-installment.png)) |
| En el mismo modal: 30.00 a la cuota #2 | Cuota #2 cobrado 30.00 `partial`; #1 y #3 sin cambios `pending`; CxC 30.00 / 270.00 «Parcial»; «Vence» sigue en 26/11/2026 (cuota #1) ([captura](c1b-modal-cuota2-30.png)) |
| Recargar y reabrir | Fila 30.00 / 270.00 «Parcial» vence 26/11/2026; modal con 270.00 por defecto y cuota #2 «70.00 pendiente» ([lista](c1-reloaded-list.png), [modal](c1-reopen-modal.png)) |
| 120.00 sin cuota (FIFO) | Cuota #1 100.00 `paid`; cuota #2 50.00 `partial` (30 + 20); #3 `pending`; CxC 150.00 / 150.00 «Parcial»; «Vence» avanza a 26/12/2026 (cuota #2) |
| Recargar y reabrir | Fila 150.00 / 150.00 vence 26/12/2026; el modal ya no ofrece la cuota #1: #2 «50.00», #3 «100.00» ([lista](c2-reloaded-list.png), [modal](c2-reopen-modal.png)) |
| Liquidar con el saldo por defecto 150.00 sin cuota | FIFO: cuota #2 +50.00 y #3 +100.00, las tres `paid`; CxC 300.00 / 0.00 «Pagada», «Vence» «—» |
| Recargar | Fuera de «Pendientes»; en «Pagadas» sin acción de cobro ([captura](c3-paid-list.png)) |
| Reabrir factura (Reporte de Ventas y API) | `001-001-000000003` «Autorizada», total 300.00, cronograma de 3 cuotas ([captura](inv-sales-report.png)) |

En cada paso, la suma del saldo de las cuotas coincidió con el saldo de la CxC y `overdueDays` fue
nulo (ninguna cuota vencida).

## PostgreSQL

- `sales_receivables`: original 300.00, cobrado 300.00, `status` `pending` (por diseño: el saldo es
  la señal de «pagada»). `sales_receivable_installments`: 3 cuotas de 100.00 cobradas y `paid`, con
  las fechas del cronograma de la factura (`sales_payment_schedules`).
- `payments`: 3 cobros aplicados (30.00, 120.00, 150.00) con banco y referencia; la línea del cobro a
  cuota guarda la cuota #2; las de FIFO, sin cuota. El intento rechazado no dejó fila.
- Asientos `Posted` y balanceados: `Sales/InvoiceIssued` Debe 1.1.03.001 Cuentas por cobrar
  clientes / Haber 4.1.01.001 (300.00); `Finance/CollectionApplied` Debe 1.1.02.001 Bancos cuenta
  corriente / Haber 1.1.03.001 (30.00, 120.00 y 150.00). Todas las filas en la empresa A.

Detalle: [e2e-results.json](e2e-results.json).

## Aislamiento empresarial

Con la empresa `ZH E2E B`: el listado no incluye la CxC de A; `by-invoice` y la factura de A dan
404; `POST /finance/collections` sobre la CxC de A con su cuota #2 se rechaza (404 «no encontrada»)
y no crea cobros; la UI de B no muestra facturas de A ([captura](company-b-isolation.png)).

## Mora

No ocurre con datos reales en este entorno: las cuotas vencen después de la fecha de emisión, que no
puede ser anterior al 27/10/2026 porque la política contable E2E rige desde esa fecha, y la API no
admite un reloj simulado. En E2E se verificó la ausencia de mora y que «Vence» sigue a la primera
cuota no saldada. El cálculo de mora con varias cuotas se cubre con un test focal nuevo
(`Build_varias_cuotas_mora_sigue_a_la_primera_cuota_no_saldada`): mora de la cuota #1, que desaparece
al saldarla por FIFO, y mora contada desde la cuota #2 cuando esta vence.

## Observaciones fuera de alcance

- El modal no limita el monto al saldo de la cuota elegida; el backend lo rechaza con un mensaje
  claro y el modal permite corregir. Comportamiento conforme a la SSOT, no BUG REAL.
- Sigue sin existir pantalla de listado/detalle de cobros ni de cuotas de la CxC; las cuotas se
  ven en el modal de cobro y por API.
- Errores esperados: `401` de refresh antes del login, `403` de `electronic-invoicing/status` antes
  de seleccionar empresa y el `422` del rechazo provocado.

## Gates

Tests focales (Release): `ERP.Domain.Tests` `SalesReceivable*` 47/47; `ERP.Application.Tests`
`SalesReceivableUseCasesTests` y `RegisterCollectionCommandHandlerTests` 25/25 (incluye el test
nuevo). Solo cambió un archivo de tests; sin cambios de frontend: no aplican lint, `tsc -b` ni build.
Suite completa no ejecutada.
