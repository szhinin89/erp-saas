# Compras/Gastos → CxP → Pago — E2E de bloqueo de anulación, reversa de pagos y anulación posterior

Fecha: 2026-10-27. Clasificación: **PASS**, 46/46 comprobaciones (17 en navegador, 20 por API y 9 en
PostgreSQL); sin BUG REAL. Aprobado expresamente por el propietario (gate PASS, 2026-10-27). Alcance: casilla «Por cada origen
(compra/gasto), bloquear anulación con pagos activos; reversar todos los pagos y anular» de la
[matriz QA](../../QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). Sin cambios de código, contratos API,
migraciones ni reglas; sin commit, push ni deploy.

## Entorno

- Base `erp_zh_test` en la baseline vigente (`20261021162651_InitialProductionBaseline`), sin
  recrear; usuario `e2e.admin` del seed E2E con credencial efímera no almacenada. Otras bases sin
  cambios.
- API aislada en `:5004` (Release, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright 1.59) sobre un Vite de desarrollo propio en `:5181`; `/api/**` redirigido a
  `:5004`; 0 solicitudes hacia la API de desarrollo `:5003`.

## Datos preparados vía API

Flujos ya aprobados en [purchases-payables-e2e-01](../purchases-payables-e2e-01/README.md) y
[supplier-payments-e2e-01](../supplier-payments-e2e-01/README.md); no se repitieron en navegador.

| Origen | Documento | Total | CxP | Pago (transferencia, «Cuenta corriente E2E») |
|---|---|---|---|---|
| Compra | `001-001-000922054`, 10 × 10.00 + IVA 15 %, «Crédito 30 días E2E» | 115.00 | 1 cuota, vence 26/11/2026 | `00000005` por 45.00 |
| Gasto | `001-002-000922054`, servicio 80.00 IVA 0 % | 80.00 | 1 cuota, vence 26/11/2026 | `00000006` por 30.00 |

El primer intento de confirmar la compra con costo 25.00 fue rechazado por la regla existente de
costo/presentación sospechosa (margen negativo); se corrigió el mismo borrador a 10.00 (`KEEP`).

## Flujo verificado en navegador

| Paso | Resultado |
|---|---|
| Compra: «Anular compra» con motivo | `422` «No se puede anular una compra con pagos aplicados. Reverse los pagos primero.», visible en pantalla ([captura](block-purchase-after.png)); al recargar sigue «Confirmada» |
| Gasto: «Anular gasto» con motivo | `422` «No se puede anular una cuenta por pagar con pagos registrados.», en aviso ([captura](block-expense-after.png)); al recargar sigue «Confirmado» |
| PostgreSQL tras ambos intentos | Sin cambios: documentos, CxP, cuotas, pagos, asientos y kardex idénticos; 0 asientos nuevos |
| Detalle del pago → «Reversar pago»: motivo y «Error de registro (no hubo débito real)» → «Confirmar reversa» (ambos pagos) | `200`; al recargar, «Reversado» con fecha, usuario y motivo, sin botón de reversa ([compra](reverse-purchase-reloaded.png), [gasto](reverse-expense-reloaded.png)) |
| Reabrir cada CxP | Pagado 0.00; saldo 115.00 y 80.00; CxP y cuota «Pendiente» ([compra](reverse-purchase-payable.png), [gasto](reverse-expense-payable.png)) |
| «Anular compra» y «Anular gasto» de nuevo | `200`; al recargar, «Anulada» y «Anulado» ([compra](cancel-purchase-reloaded.png), [gasto](cancel-expense-reloaded.png)) |

La reversa sí tiene pantalla: `SupplierPaymentDetailPage` → `SupplierPaymentReverseModal`
(`POST /api/v1/supplier-payments/{id}/reverse`).

## PostgreSQL

Todos los asientos están en `Posted`, son de la empresa A y cuadran:

| N.º | Origen | Debe | Haber |
|---|---|---|---|
| 13 | `Purchases/InvoiceReceived` | 1.1.04.001 100.00; 1.1.05.001 15.00 | 2.1.01.001 115.00 |
| 14 | `Expenses/DocumentConfirmed` | 6.1.01.003 80.00 | 2.1.01.001 80.00 |
| 15 / 16 | `Payables/SupplierPaymentConfirmed` | 2.1.01.001 45.00 / 30.00 | 1.1.02.001 45.00 / 30.00 |
| 17 / 18 | `Accounting/Reversal` de 15 / 16 | 1.1.02.001 45.00 / 30.00 | 2.1.01.001 45.00 / 30.00 |
| 19 | `Accounting/Reversal` de 13 | 2.1.01.001 115.00 | 1.1.04.001 100.00; 1.1.05.001 15.00 |
| 20 | `Accounting/Reversal` de 14 | 2.1.01.001 80.00 | 6.1.01.003 80.00 |

- Tras la reversa, pagos `Reversed` con motivo y motivo bancario `RegistrationError`; cuotas con
  `paid_amount` 0.00 y estado pendiente.
- Tras la anulación, compra y gasto anulados; ambas CxP y sus cuotas `Cancelled`; el kardex de la
  compra registra +10 (`PurchaseEntry`) y −10 (`PurchaseCancelled`).
- Para estos documentos, la cuenta 2.1.01.001 queda con saldo neto 0.00: 270.00 al debe y 270.00 al
  haber.

## Restricciones posteriores (API, empresa A)

Se rechazan con `422`: reversar otra vez un pago reversado («Solo un pago Confirmed puede
reversarse»), anular otra vez la compra («Esta compra ya fue anulada.») o el gasto («Solo se pueden
anular gastos confirmados.») y pagar una cuota de una CxP anulada («… está Cancelled y no admite
pagos.»).

## Aislamiento empresarial

Con `ZH E2E B`, la API devuelve `404` al consultar CxP, pago, compra o gasto de A, y también al
intentar reversar el pago de A o anular su compra o su gasto. Los listados de CxP y de pagos no
incluyen los registros de A. En la UI de B aparecen «Cuenta por pagar no encontrada.»
([captura](isolation-b-payable.png)), «Pago a proveedor no encontrado.»
([captura](isolation-b-payment.png)), «Error al cargar la compra.» y «Gasto no encontrado.».
PostgreSQL no cambió tras estas pruebas. Detalle: [e2e-results.json](e2e-results.json).

## DEBT no bloqueante

Registrados solo como observación: no bloquean el cierre, no abren tareas ni reabren módulos CLOSED.

- El detalle del pago reversado muestra «Reversado por» con el UUID del usuario
  (`SupplierPaymentDetailPage.tsx` muestra `reversedBy`, porque el DTO no trae el nombre). La reversa
  funciona; corregirlo requiere ampliar el contrato.
- El bloqueo del gasto sale de la excepción de dominio de `AccountsPayable.Cancel`
  (`DOMAIN_RULE_VIOLATION`, mensaje sobre la CxP), mientras que la compra lo valida antes
  (`VALIDATION_ERROR`, mensaje sobre la compra). La transacción no deja cambios. No es BUG REAL.

## Otras observaciones

- Error de consola esperado: `404` de `GET /expenses/documents/{id}/retention`; así responde el
  backend cuando el gasto no tiene retención. Los demás son los `401`/`403` iniciales ya conocidos y
  los `404` de las pruebas de aislamiento.

## Gates

No hubo cambios de código, por lo que no aplican lint, `tsc -b`, build ni `architecture:check`. Se
ejecutó `git diff --check` sobre esta carpeta. No se ejecutó la suite completa.
