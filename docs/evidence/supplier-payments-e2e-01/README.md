# Pagos a proveedor — E2E funcional (registrar, reabrir, saldo pendiente)

Fecha: 2026-10-27. Clasificación: **PASS**; 17/17 comprobaciones aprobadas; sin BUG REAL.
Alcance cerrado: [Compras/Gastos/CxP/Pagos](../../QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). Esta
evidencia ejecuta la casilla «pagar parcialmente y totalmente» de esa matriz para el origen gasto.
No cambia código, contratos API, migraciones ni reglas; sin commit, push ni deploy.

## Entorno

- Base `erp_zh_test` en la baseline vigente (`20261021162651_InitialProductionBaseline`), sin
  recrear; usuario `e2e.admin` del seed E2E con credencial efímera no almacenada. Otras bases sin
  cambios.
- API aislada en `:5004` (Release, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright 1.59) sobre un Vite de desarrollo propio en `:5181`; `/api/**` redirigido a
  `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`.

## Datos creados vía API

Contabilidad operativa habilitada (mismo criterio que `e2e/setup-operational-data.ts`), condición
de pago `E2E-30D` (1 cuota, 30 días), cuenta bancaria «Cuenta corriente E2E» (1.1.02.001) y gasto
`01 001-001-000695762` por 100.00 al proveedor `Cliente E2E ZH` (borrador → confirmado), que generó
la CxP con vencimiento 26/11/2026.

## Flujo verificado en navegador

| Paso | Resultado |
|---|---|
| Pago parcial: proveedor, monto a aplicar 40.00, transferencia bancaria con n.º de operación, modal de confirmación | Pago `00000001` confirmado; aplicado 40.00, anticipo 0.00 |
| Recargar y reabrir el detalle del pago | Medio, cuenta, referencia, fecha y cuota aplicada se conservan |
| Reabrir la CxP | Total 100.00, Pagado 40.00, Saldo 60.00, estado «Parcial» |
| Reabrir el formulario de pago | Cartera: total pendiente 60.00, pagado 40.00, cuota «Parcial» |
| Pago del saldo con «Pagar todo el saldo pendiente» | Pago `00000002` por 60.00; CxP Pagado 100.00, Saldo 0.00, `paid` |
| Reabrir formulario y listado | «Este proveedor no tiene cuentas por pagar pendientes.»; listado con ambos pagos «Confirmado» |

Asientos `Payables/SupplierPaymentConfirmed` en `Posted` y balanceados: Debe 2.1.01.001 Cuentas por
pagar proveedores / Haber 1.1.02.001 Bancos cuenta corriente (40.00 y 60.00). Detalle:
[e2e-results.json](e2e-results.json).

Capturas: [confirmación](partial-payment-confirm.png), [pago reabierto](partial-payment-reopened.png),
[CxP tras pago parcial](payable-after-partial.png), [cartera restante](portfolio-remaining-60.png),
[cartera sin saldo](portfolio-empty-after-full.png), [listado](payments-list.png).

## Observaciones fuera de alcance

- `DEBT` menor: la etiqueta «Fecha emision» del detalle de CxP no lleva tilde
  (`PayableDetailPage.tsx`). No es BUG REAL ni se corrige aquí.
- Errores de consola esperados: `401` de refresh antes del login y `403` de
  `electronic-invoicing/status` antes de seleccionar empresa.

## Gates

Sin cambios de código: no aplican lint, `tsc -b`, build ni `architecture:check`. `git diff --check`
sobre esta carpeta. Suite completa no ejecutada.
