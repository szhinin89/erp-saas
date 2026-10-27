# Compras → CxP → Pago — E2E funcional (compra a crédito, pago parcial y total, reabrir)

Fecha: 2026-10-27. Clasificación: **BUG REAL corregido → PASS**; 40/40 comprobaciones (flujo, asientos y
aislamiento) tras las correcciones, aprobado expresamente por el propietario (gate PASS, 2026-10-27).
Alcance: casilla «Origen compra: pagar parcialmente y totalmente» de la
[matriz QA](../../QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). Sin commit, push ni deploy; sin
migraciones ni cambios de contrato API ni de backend.

## Entorno

- Base `erp_zh_test` en la baseline vigente (`20261021162651_InitialProductionBaseline`), sin
  recrear; usuario `e2e.admin` del seed E2E con credencial efímera no almacenada. Otras bases sin
  cambios.
- API aislada en `:5004` (Release, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright 1.59) sobre un Vite de desarrollo propio en `:5181`; `/api/**` redirigido a
  `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`.

## Datos

Reutilizados de evidencias previas: proveedor `Cliente E2E ZH`, condición `E2E-30D` (1 cuota,
30 días), cuenta bancaria «Cuenta corriente E2E» (1.1.02.001) y producto `VIS-PRD-762834`.
Preparado vía API: configuración SRI del rol proveedor (sustento 06, forma de pago 20, exento de
retención), requisito existente del borrador de compra («El proveedor no tiene configuración SRI.»,
`KEEP`). La exención deja la retención fuera de este alcance.

## BUG REAL encontrado y corregido

1. **Cuota con monto obsoleto** ([captura](bug-schedule-stale-before-fix.png)). En «Nueva compra»,
   tras agregar la línea (115.00) y pulsar «Regenerar», el pie mostraba «Total cuotas: 115.00» pero
   el campo de la cuota seguía en 0.00; al solo entrar y salir del campo se re-confirmaba 0.00 y
   aparecía «Las cuotas no cuadran con el total. Diferencia: $115.00», que bloquea «Confirmar
   compra». Causa: en `PaymentScheduleSection` el `ZhDecimalInput` no controlado (`defaultValue`) no
   se remontaba al cambiar el monto y confirmaba con `onBlur`. Corrección: `key` por monto y
   `onValueCommit` (contrato del componente para datos de negocio). [Tras la corrección](schedule-after-fix.png).
2. **Clave i18n cruda** ([captura](bug-retention-key-before-fix.png)). Al reabrir una compra
   confirmada sin retención, la tarjeta «Retenciones» mostraba `purchases.retention.noneIssued`:
   la clave no existía en `es.json`/`en.json`. Corrección: textos agregados en ambos idiomas.
   [Tras la corrección](purchase-reopened-after-fix.png).

## Flujo verificado en navegador

| Paso | Resultado |
|---|---|
| Nueva compra: proveedor, factura `001-001-000777001`, condición «Crédito 30 días E2E», bodega, 10 × 10.00 con IVA 15 % | Borrador guardado; total 115.00, 1 cuota de 115.00 al 26/11/2026 |
| «Confirmar compra» y modal de confirmación ([captura](purchase-confirm-modal.png)) | Compra `Confirmed`; stock disponible 10, costo 10.00 |
| CxP generada | Origen Compra, total 115.00, 1 cuota 115.00 vence 26/11/2026, `pending` |
| Pago parcial: aplicar 50.00, transferencia, «Cuenta corriente E2E», referencia ([captura](partial-payment-confirm.png)) | Pago `00000003` confirmado; aplicado 50.00, anticipo 0.00 |
| Recargar CxP ([captura](payable-partial-reloaded.png)) | Pagado 50.00, saldo 65.00, cuota y CxP «Parcial» |
| «Pagar todo el saldo pendiente» | Pago `00000004` por 65.00 confirmado |
| Recargar y reabrir CxP ([captura](payable-paid-reloaded.png)), pago ([captura](partial-payment-reopened.png)), listado ([captura](payments-list.png)), cartera ([captura](portfolio-empty-after-full.png)) y compra | CxP pagado 115.00, saldo 0.00, `paid`; pago conserva medio, cuenta, referencia y cuota; cartera sin pendientes; compra «Confirmada» con cuota 115.00 |

PostgreSQL: aplicaciones 50.00 y 65.00 a la misma cuota; asientos `Posted` y balanceados —
`Purchases/InvoiceReceived` Debe 1.1.04.001 Inventario mercaderías 100.00 y 1.1.05.001 IVA crédito
tributario 15.00 / Haber 2.1.01.001 Cuentas por pagar proveedores 115.00;
`Payables/SupplierPaymentConfirmed` Debe 2.1.01.001 / Haber 1.1.02.001 Bancos cuenta corriente
(50.00 y 65.00). Compra, CxP, pagos y asientos en la empresa A. Detalle:
[e2e-results.json](e2e-results.json).

## Aislamiento empresarial

Con `ZH E2E B`: CxP, compra y pago de A dan 404; los listados de CxP y pagos no los incluyen;
`POST /supplier-payments/{id}/reverse` sobre un pago de A se rechaza (404) y la CxP de A queda
intacta; la UI de B muestra «No hay cuentas por pagar registradas» ([captura](company-b-isolation.png))
y la URL directa «Cuenta por pagar no encontrada.».

## Observaciones fuera de alcance

- `DEBT` menor: la vista previa de retención muestra al usuario el nombre técnico
  `SupplierRoleConfig.IsRetentionExempt=true` (texto del backend). No se corrige aquí.
- Antes de «Regenerar», la cuota de una compra nueva no sigue al total (0.00 con aviso visible); el
  borrador guardado la regenera al reabrir. Comportamiento existente, no BUG REAL.
- Errores de consola esperados: `401` de refresh antes del login, `403` de
  `electronic-invoicing/status` antes de seleccionar empresa y `404` de la sonda de aislamiento.

## Gates

Tests focales: `PaymentScheduleSection.test.tsx` 3/3 y `RetentionSection.test.tsx` 1/1, que fallan
sin la corrección (2/3 y 1/1); `src/modules/purchases/pages`, `src/modules/purchases/hooks` e
`src/i18n` 156/156. `npm run lint` 0 errores (advertencia `max-lines` previa), `npx tsc -b` y
`npm run build` correctos, `architecture:check` PASS (0 nuevas). Sin cambios de backend. Suite
completa no ejecutada.
