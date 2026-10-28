# Cuentas por cobrar — E2E de cobros a caja y sin destino

Fecha: 2026-10-27. Clasificación: **PASS** — 28/28 comprobaciones en navegador y 14/14 de rechazos,
aislamiento y PostgreSQL; sin BUG REAL ni cambios de código. Aprobado por el propietario (2026-10-27).
Comportamiento: [SSOT de CxC](../../architecture/receivables.md); cobertura:
[matriz QA](../../QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md). No repite los E2E de una cuota
([receivables-e2e-01](../receivables-e2e-01/README.md)) ni de varias cuotas
([receivables-e2e-02](../receivables-e2e-02/README.md)). Sin commit, push ni deploy; sin migraciones
ni cambios de contrato API.

## Regla aplicada

El cobro sin destino **está permitido por diseño**: la SSOT define cuenta bancaria y caja como
opcionales y excluyentes (§ Registrar un cobro, punto 2). Una caja elegida debe existir, ser de la
empresa activa y estar activa; sin destino, el asiento usa la cuenta Debe de la regla
`Finance/CollectionApplied` (semilla: 1.1.01.001 Caja general). Se validó tal cual, sin cambiar reglas.

## Entorno

- Base `erp_zh_test` en la baseline vigente (`InitialProductionBaseline`), sin recrear; usuario
  `e2e.admin` del seed E2E con credencial efímera no almacenada. Otras bases sin cambios.
- API aislada en `:5004` (Release del HEAD, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright) sobre un Vite de desarrollo propio en `:5181`; las rutas `/api/*` se
  redirigieron a `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`, que no se tocó.

## Datos creados vía API

Factura de venta `001-001-000000004` por 90.00 (servicio E2E ×2, no objeto de IVA) al cliente
`Cliente E2E ZH`, condición `E2E-30D`, método `CREDITO`, emitida y autorizada el 27/10/2026 (fecha de
la empresa). Generó la CxC de 90.00 con 1 cuota que vence el 27/11/2026.

## Flujo verificado en navegador

Ambos cobros desde `/finance/receivables`, sin cuota específica. Tras cada cobro se consultó la CxC
por API, se recargó el listado y se reabrió el modal.

| Paso | Resultado |
|---|---|
| Modal inicial | Monto por defecto 90.00; destino ofrece «Sin especificar», «Banco: Cuenta corriente E2E» y solo la caja de la empresa activa «Caja: Caja Principal» |
| Cobro parcial 40.00, Efectivo, «Caja: Caja Principal», referencia `E2E-CAJA-40` ([captura](c1-modal-caja-40.png)) | CxC 40.00 / 50.00 «Parcial»; cuota 40.00 `partial` |
| Recargar | Fila 40.00 / 50.00 «Parcial» ([captura](c1-reloaded-list.png)) |
| Reabrir el modal | Monto por defecto 50.00; «Cuota #1 — 50.00 pendiente»; forma de pago y destino vacíos |
| Cobro del saldo 50.00 con forma de pago y destino «Sin especificar», sin referencia ([captura](c2-modal-sin-destino-50.png)) | CxC 90.00 / 0.00 «Pagada», «Vence» nulo; cuota 90.00 `paid` |
| Recargar | Fuera de «Pendientes»; en «Pagadas» sin acción de cobro ([captura](c2-paid-list.png)) |

## PostgreSQL

- `payments`: 2 cobros `Applied` de la empresa A con `payment_date` 2026-10-27 — 40.00 Efectivo con
  `cash_register_id` de «Caja Principal» (A), sin banco, referencia `E2E-CAJA-40`; 50.00 sin forma
  de pago, sin caja, sin banco y sin referencia. Líneas de aplicación sin cuota (FIFO).
- `sales_receivables`: original 90.00, cobrado 90.00, `status` `pending` (por diseño: el saldo es
  la señal de «pagada»). `sales_receivable_installments`: cuota #1 90.00 cobrada, `paid`.
- Asientos `Posted` y balanceados, cuentas de la empresa A: n.º 25 `Sales/InvoiceIssued` Debe
  1.1.03.001 / Haber 4.1.01.001 (90.00); n.º 26 `Finance/CollectionApplied` a caja: Debe 1.1.01.001
  Caja general (cuenta de la caja) / Haber 1.1.03.001 (40.00); n.º 27 sin destino: Debe 1.1.01.001
  Caja general (cuenta de la regla) / Haber 1.1.03.001 (50.00). Ninguna obligación contable fuera de
  `Posted`.

UI, API y PostgreSQL coincidieron en cada paso (cobrado, saldo, estado, cuota).

## Rechazos y aislamiento empresarial (API)

| Caso | Resultado |
|---|---|
| Empresa A, cobro con la caja de la empresa B | 422 «La caja indicada no existe, no pertenece a esta empresa o está inactiva.» |
| Empresa A, cobro con banco y caja a la vez | 422 «Un cobro no puede tener cuenta bancaria y caja destino a la vez.» |
| Empresa A, cobro sin destino sobre la CxC ya saldada | 422 «El monto del cobro excede el saldo pendiente…» |
| Empresa B: listado de CxC | No incluye la CxC de A |
| Empresa B: `by-invoice` y factura de A | 404 |
| Empresa B: cobro sobre la CxC de A con su caja o sin destino | 404 «Cuenta por cobrar … no encontrada.» |
| Empresa B: cobro con la caja de A | 422 (caja ajena) |
| UI de la empresa B, filtro «Todas» | Sin la factura de A ([captura](company-b-isolation.png)) |

Ningún rechazo creó filas en `payments` ni cambió la CxC (90.00 / 90.00).

Detalle: [e2e-results.json](e2e-results.json).

## Observaciones fuera de alcance

- La factura `001-001-000000001` figura «Pagada» con «Vence» 26/11/2026: es la CxC cobrada antes de
  la corrección CXC-E2E-01, límite ya documentado en la SSOT (no se reparan datos). No es hallazgo
  nuevo.
- No se probó la exigencia de sesión de caja abierta para cobrar a caja: la SSOT no la define y el
  handler no la aplica; no se asume regla.
- Errores esperados en consola: `401` de refresh antes del login y `403` de
  `electronic-invoicing/status` antes de seleccionar empresa.

## Gates

Sin cambios de código de producción ni de tests: no aplican `dotnet test`, lint, `tsc -b` ni build.
Suite completa no ejecutada.
