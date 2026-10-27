# Cuentas por cobrar — E2E funcional (factura a crédito, cobro parcial y total, reabrir)

Fecha: 2026-10-27. Clasificación: **BUG REAL corregido → PASS**; 21/21 comprobaciones del flujo y
7/7 de aislamiento aprobadas tras la corrección, aprobado por el propietario. Comportamiento:
[SSOT de CxC](../../architecture/receivables.md); cobertura:
[matriz QA](../../QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md). Sin commit, push ni deploy; sin migraciones ni
cambios de contrato API.

## Entorno

- Base `erp_zh_test` en la baseline vigente, sin recrear; usuario `e2e.admin` del seed E2E con
  credencial efímera no almacenada. Otras bases sin cambios.
- API aislada en `:5004` (Release, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright) sobre un Vite de desarrollo propio en `:5181`; `/api/**` redirigido a
  `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`.

## Datos creados vía API

Caja abierta para la sesión E2E (requisito de venta existente), y factura de venta
`001-001-000000002` por 100.00 al cliente `Cliente E2E ZH` (servicio E2E, condición `E2E-30D`,
método `CREDITO`), autorizada: generó la CxC de 100.00 con 1 cuota que vence el 26/11/2026.

## BUG REAL encontrado y corregido

Reproducción (factura `001-001-000000001`, antes de la corrección): tras un cobro de 40.00 la CxC
mostraba saldo 60.00, pero la cuota seguía con cobrado 0.00 y estado `pending`; el modal de cobro
ofrecía «Cuota #1 — 100.00 pendiente» ([captura](bug-installment-stale-before-fix.png)). Con saldo
0.00 la cuota seguía `pending`. Causa: `SalesReceivable.RegisterCollection`/`ReverseCollection` solo
movían el `PaidAmount` de la cabecera; `SalesReceivableInstallment` no tenía mutador. Con varias
cuotas, el vencimiento y la mora (derivados de la primera cuota no cobrada) no avanzaban.

Corrección focal (mismo criterio que `AccountsPayable.RegisterPayment`/`RegisterPaymentToInstallment`):

- `SalesReceivableInstallment`: `OutstandingAmount`, acumulación/liberación del cobro y estado
  `pending`/`partial`/`paid`.
- `SalesReceivable.RegisterCollection`: aplica a la cuota elegida (rechaza exceder su saldo) o,
  sin cuota, por FIFO de vencimiento. `ReverseCollection` libera la cuota del cobro y luego desde la
  última; nunca deja cobrado negativo (cuotas reconstruidas o anteriores a la corrección).
- `RegisterCollectionCommandHandler`: valida antes de mutar que el monto no exceda el saldo de la
  cuota elegida; registro y reversa pasan la cuota de la línea.

`SalesReceivable.Status` no transiciona por diseño documentado (el saldo es la señal de «pagada»,
`SalesReceivableRepository`); el estado visible (`statusLabel`) es correcto y no se modificó.

## Flujo verificado en navegador (tras la corrección)

| Paso | Resultado |
|---|---|
| Cobro parcial desde `/finance/receivables`: 40.00, transferencia, banco «Cuenta corriente E2E», referencia | CxC cobrado 40.00, saldo 60.00, «Parcial»; cuota cobrado 40.00 `partial` |
| Recargar y reabrir CxC | Fila con 40.00 / 60.00 «Parcial» ([captura](receivable-partial-reloaded.png)) |
| Reabrir el modal | Monto por defecto 60.00 y «Cuota #1 — 60.00 pendiente» ([captura](remaining-collection-modal.png)) |
| Cobro del saldo 60.00 | CxC cobrado 100.00, saldo 0.00, «Pagada»; cuota `paid` |
| Recargar | Fuera de «Pendientes»; en «Pagadas» sin acción de cobro ([captura](receivable-paid-list.png)) |
| Reabrir factura (Reporte de Ventas) | `001-001-000000002` «Autorizada», total 100.00 ([captura](invoice-sales-report.png)) |

PostgreSQL: 2 cobros `payments` aplicados (40.00 y 60.00) con banco, referencia y cuota; asientos
`Posted` y balanceados — `Sales/InvoiceIssued` Debe 1.1.03.001 Cuentas por cobrar clientes / Haber
4.1.01.001; `Finance/CollectionApplied` Debe 1.1.02.001 Bancos cuenta corriente / Haber 1.1.03.001
(40.00 y 60.00). Todas las filas en la empresa A.

## Aislamiento empresarial

Con la empresa `ZH E2E B`: el listado de CxC no incluye la de A; `by-invoice` y la factura de A dan
404; `POST /finance/collections` sobre la CxC de A se rechaza (404 «no encontrada»); la UI de B
muestra «Sin cuentas por cobrar.» ([captura](company-b-isolation.png)). Detalle:
[e2e-results.json](e2e-results.json).

## Observaciones fuera de alcance

- No existe pantalla de listado/detalle de cobros (solo `POST /finance/collections`); los cobros se
  verificaron por API y PostgreSQL. `FALTA` funcional, no BUG REAL.
- Dato previo a la corrección: la cuota de `001-001-000000001` (base E2E) conserva cobrado 0.00; no
  se reparan datos existentes.
- Errores de consola esperados: `401` de refresh antes del login y `403` de
  `electronic-invoicing/status` antes de seleccionar empresa.

## Gates

Tests focales: `ERP.Domain.Tests` `SalesReceivable*` 47/47; `ERP.Application.Tests` cobros/CxC/
anulación/devolución 102/102; `ERP.Infrastructure.Tests` `CollectionPostingIntegrationTests`,
`DashboardKpiReaderTests`, `PostOpeningBalanceCommandTests` 33/33; `ERP.API.Tests`
`FinancePaymentsControllerTests`, `SalesCancelCollectionConcurrencyTests` 16/16 (Release: la API de
desarrollo bloquea `bin/Debug`). `architecture:check` PASS (0 nuevas). Sin cambios de frontend:
no aplican lint, `tsc -b` ni build. Suite completa no ejecutada.
