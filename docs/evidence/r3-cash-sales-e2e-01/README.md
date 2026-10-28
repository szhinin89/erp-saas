# R3 — Caja y ventas de contado: venta, devolución, movimientos, depósito, arqueo y reporte — E2E

Fecha: 2026-10-28. Clasificación: **PASS 6/6 con tres BUG REAL corregidos**; aprobado por el propietario (R3
CLOSED, 2026-10-28). Escenarios #17, #19, #22, #23, #21 y #39 del plan de validación V1, sobre la empresa B y los
datos de [R1](../r1-initial-load-e2e-01/README.md) y [R2](../r2-purchases-inventory-e2e-01/README.md).
Comportamiento: [Caja contable §25](../../architecture/accounting.md#25-caja-contable-accounting-12c),
[turnos de caja](../../architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro),
[efectivo entregado](../../architecture/backend.md#efectivo-entregado-dato-operacional-del-settlement-pos-cash-tendered-01),
[CxC § Devolución](../../architecture/receivables.md#devolución-y-anulación). Sin commit, push ni deploy; sin
cambios de contrato API.

## Entorno

- Base `erp_zh_test` (baseline vigente, sin recrear); usuario `e2e.admin` del seed E2E con credencial
  efímera no almacenada. El token first-run no se leyó ni se usó. Otras bases sin cambios.
- API aislada en `:5004` (Release del HEAD, Development, seed E2E, JWT efímero), recompilada y reiniciada
  tras cada corrección. Chromium (Playwright 1.59) sobre un Vite propio en `:5181` con proxy `/api` →
  `:5004`. `:5003` no se usó. Procesos detenidos al terminar.
- La empresa B no tiene configuración SRI: los comprobantes electrónicos (factura y NC) quedan en
  «Fallido» y no se envió nada al SRI. No se configuró un mock SRI: R3 no valida transmisión.

## Configuración previa (UI)

- «Caja Principal» 001: bodega predeterminada «Bodega Principal» (la apertura avisaba que sin bodega no
  se venden productos con inventario). La cuenta contable 1.1.01.001 ya estaba configurada.
- Motivo de caja «Compra de insumos menores» (Egreso manual): cuenta 6.1.01.002 Suministros de oficina
  ([captura](r3-22-reason-account.png)).
- Cuenta bancaria «Pichincha Corriente R1» (1.1.02.001) de R1.

## Resultados

| # | Escenario | Resultado |
|---|---|---|
| 17 | Venta de contado con caja abierta, efectivo y vuelto | **PASS** tras corregir un **BUG REAL** (apertura) |
| 19 | Devolución de venta con reembolso en efectivo | **PASS** |
| 22 | Movimiento manual de caja con motivo y asiento | **PASS** |
| 23 | Depósito bancario desde caja | **PASS** tras corregir un **BUG REAL** (sobregiro) |
| 21 | Cierre de caja con arqueo y diferencia | **PASS** tras corregir un **BUG REAL** (total contado) |
| 39 | Reporte de ventas frente a movimientos y cierre | **PASS** |

### #17 — Venta de contado

| Paso | Resultado |
|---|---|
| Abrir caja con 50.00 | **BUG REAL**: «Ya existe una sesión abierta para esta caja o para este usuario» aunque la UI mostraba la caja «Disponible». Tras la corrección: turno abierto ([captura](r3-17-cash-opened.png)) |
| POS: 3 × `R1-ARROZ-01` (2.50, IVA 0 %) + 2 × `R1-ACEITE-01` (3.20, IVA 15 %), efectivo recibido 20.00 ([captura](r3-17-cart-cash-change.png)) | Total 14.86 (base 13.90, IVA 0.96), vuelto 5.14, «Cobro completo» |
| Emitir ([captura](r3-17-emitted.png)) | Factura `001-001-000000001` Autorizada; recibido 20.00, vuelto 5.14 (también en la tirilla: `cashChange` 5.14) |
| Persistencia | Pago `EFECTIVO` 14.86 con `tendered_amount` 20.00; caja SaleIncome **14.86** (no 20.00) |
| Kardex | Salidas −3 arroz (5.41) y −2 aceite (4.78) al promedio |
| Asientos | N° 14 Caja 14.86 / Ventas 13.90 + IVA 0.96; N° 15 Costo de ventas 10.19 / Inventario 10.19 |

**BUG REAL (corregido, autorizado).** El índice `ux_cash_sessions_open_per_user` permitía un solo turno
abierto por usuario en todo el tenant, mientras la validación previa del caso de uso ya era por empresa:
el turno «E2E cash session» de e2e.admin en la empresa A (apertura 0.00, del setup oficial) impedía abrir
caja en B, con un conflicto genérico. El propietario confirmó la regla: turnos independientes por
TenantId + CompanyId. Corrección: índice único parcial `ux_cash_sessions_open_per_company_user`
(tenant, empresa, usuario) en la configuración EF y en la migración forward
`20261028210143_CashSessionOpenPerCompanyUser`, autorizada para que el índice llegue a las bases existentes sin
recrearlas (la API la aplica al arrancar; [DATABASE.md](../../DATABASE.md)). La baseline no cambia. Durante el
E2E el cambio se probó primero como DDL manual en `erp_zh_test`; al cerrar se volvió al estado de la baseline
y se aplicó la migración con `dotnet ef database update` sobre los datos existentes (3 turnos, uno abierto en
A), sin tocar filas. El turno de A no se tocó y sigue abierto.
Regresión `CashSessionOpenUniquenessTests` (PostgreSQL): HEAD 2 FAIL → 2/2.

### #19 — Devolución con reembolso en efectivo

| Paso | Resultado |
|---|---|
| Borrador `DEV-15cffa563d` desde la factura, 1 aceite | Subtotal 3.20, IVA 0.48, total 3.68 |
| Autorizar con «Efectivo (Caja)» 3.68 ([modal](r3-19-authorize-modal.png), [autorizada](r3-19-return-authorized.png)) | Autorizada; caja SaleRefund 3.68 en el turno; NC electrónica «Falló la generación» (sin configuración SRI) |
| Kardex | Entrada `SalesReturn` +1 aceite por 2.39 (costo de la venta) |
| Asientos | N° 16 Inventario 2.39 / Costo de ventas 2.39; N° 17 Devoluciones en ventas 3.20 + IVA 0.48 / Caja 3.68 |
| Rechazo | Devolver 2 aceites con remanente 1 → 422, sin borrador |

### #22 — Movimiento manual

| Paso | Resultado |
|---|---|
| Egreso manual 4.50, motivo «Compra de insumos menores» ([confirmación](r3-22-movement-confirm.png), [registrado](r3-22-movement-recorded.png)) | Movimiento con motivo y descripción |
| Asiento | N° 18 Suministros de oficina 4.50 / Caja 4.50 |
| Rechazos (API) | Sin motivo, motivo de Ingreso en un Egreso («no corresponde a este tipo de movimiento») y monto 0 → 422, sin movimientos |

### #23 — Depósito bancario

| Paso | Resultado |
|---|---|
| Depósito 40.00, papeleta `PAP-R3-0001` ([confirmación](r3-23-deposit-confirm.png), [registrado](r3-23-deposit-recorded.png)) | Movimiento BankDeposit con la cuenta bancaria como referencia |
| Asiento | N° 19 Bancos cuenta corriente 40.00 / Caja 40.00 |
| Rechazos (API) | Cuenta bancaria de la empresa A → 422; monto 0 → 422 |
| Depósito de 100.00 con 16.68 en caja | **BUG REAL**: 201; el esperado quedó en −83.32 y se creó el asiento N° 20 Banco 100.00 / Caja 100.00 sin efectivo real |

**BUG REAL (corregido, autorizado).** El pago a proveedor en efectivo ya bloqueaba el sobregiro (02A-CLOSE),
pero el depósito bancario, el egreso manual y el retiro no validaban el efectivo esperado. Corrección:
`CashSessionAvailability` (regla única) en `RecordCashBankDepositHandler` y `RecordCashMovementHandler`
(Egreso manual y Retiro), con la sesión bloqueada y sin override; los ingresos y los reembolsos del sistema
no cambian. Rechazo en UI tras la corrección ([captura](r3-23-overdraft-rejected.png)): «La caja dispone de
$-83.32 y se intenta registrar una salida de $10.00.»; egreso y retiro por API → 422. Regresión:
`RecordCashBankDepositHandlerTests` y `RecordCashMovementHandlerTests` HEAD 3 FAIL / 16 PASS → 19/19.
**Anomalía histórica E2E (se conserva).** El depósito de 100.00 (movimiento BankDeposit del turno 1,
asiento N° 20 Bancos 1.1.02.001 100.00 / Caja 100.00) se registró antes de la corrección, sin efectivo que lo
respalde. Los movimientos de caja no tienen anulación operativa y **no hubo reversa bancaria**: el movimiento y
el asiento N° 20 permanecen, y la cuenta 1.1.02.001 conserva ese débito de 100.00 sin depósito real. Por
decisión del propietario, el turno 1 se cerró contando el efectivo físico real 16.68; el arqueo dio un sobrante
de 100.00 (asiento N° 21 Caja 100.00 / Diferencias positivas de caja 4.2.01.003 100.00), que repone el saldo
contable de Caja pero no anula el depósito ni corrige la cuenta bancaria. #21 se validó en un turno nuevo.

### #21 — Cierre con arqueo y diferencia

| Paso | Resultado |
|---|---|
| Arqueo por denominaciones | **BUG REAL**: las filas mostraban su total, pero «Total contado» y el modal de cierre mostraban $0.00 y una diferencia falsa |
| Turno 1, tras la corrección ([modal](r3-close1-confirm.png)) | Esperado −83.32, contado 16.68, diferencia +100.00; asiento N° 21 Caja 100.00 / Diferencias positivas de caja 100.00 |
| Turno 2: apertura 30.00 + venta `001-001-000000002` (2 arroz, 5.00, vuelto 5.00) | Asientos N° 22 y 23 (costo 3.61) |
| Cerrar contando 34.50 ([modal](r3-21-close-confirm.png), [cerrado](r3-21-closed.png)) | Esperado 35.00, contado 34.50, diferencia −0.50; desglose $20×1, $10×1, $1×4, $0.50×1 persistido; asiento N° 24 Descuadres de caja 0.50 / Caja 0.50 |
| Turno cerrado | Movimiento, depósito y volver a cerrar → 422 «La caja no está abierta.» |

**BUG REAL (corregido).** `useCajaPage` memorizaba `countedTotal` sobre `watch("closingCounts")`; al
escribir en un input registrado, RHF muta el array en su lugar y la referencia no cambia, así que el total
quedaba en $0.00. El servidor calculaba el arqueo con las cantidades reales, pero el cajero confirmaba
viendo un contado y una diferencia falsos. Corrección: el total se suma en cada render, igual que las
filas. La prueba previa reemplazaba el array completo y ocultaba el caso. Regresión en
`useCajaPage.test.tsx` (cambio por ruta, como `register`): HEAD FAIL (0 en vez de 15) → PASS.

### #39 — Reporte de ventas frente a caja y cierres

| Fuente | Valor |
|---|---|
| Reporte de ventas del 28/10/2026 ([captura](r3-39-sales-report.png)) | 2 facturas autorizadas: total 19.86, subtotal 18.90, IVA 0.96 |
| Caja: SaleIncome de los turnos ([captura](r3-39-cash-sessions.png)) | 14.86 + 5.00 = 19.86; «Total facturado» por turno 14.86 y 5.00 |
| Contabilidad (asientos 14–24, todos balanceados) | Ventas 18.90 (Haber), IVA 0.96; Devoluciones 3.20 (Debe) |
| Caja 1.1.01.001 | Neto −28.82 = (16.68 − 50.00) + (34.50 − 30.00): el movimiento contable de caja coincide con contado − apertura de cada turno |
| Aislamiento | Reporte con token de A: 0 facturas; A no ve los turnos de B; GET de sesión, factura y devolución de B desde A → 404; movimiento en el turno de B desde A → 404 |

El reporte lista facturas (bruto): la devolución no lo reduce, y se refleja en caja (SaleRefund) y en
contabilidad (4.1.03.001).

## Permisos

Endpoints verificados por política: abrir/cerrar/registrar/ver caja (`caja.open`, `caja.close`,
`caja.record`, `caja.view`), configurar motivos y cajas (`caja.manage`), ventas y devoluciones (`sales.*`).
La sesión solo la opera quien la abrió (`IsControlledBy`). No se probó con un segundo usuario: hacerlo
requiere crear o restablecer credenciales, lo cual no está autorizado.

## Observaciones menores (DEBT, sin ticket)

- La regla de contabilización sembrada acredita todo el subtotal a 4.1.01.001 «Ventas tarifa general»,
  incluida la línea con IVA 0 % (no usa 4.1.01.002). Es configuración vigente, no un bug.
- Formulario de devolución: las cantidades muestran el código de la unidad («3 19» en lugar de «3 UN»).
- Kardex de devolución: referencia «DEV-DEV-15cffa563d» con el prefijo duplicado.
- Montos negativos se formatean «$-83.32».

## Cambios de código

- `backend/src/ERP.Infrastructure/Persistence/Configurations/Caja/CashSessionConfiguration.cs`, migración forward
  `20261028210143_CashSessionOpenPerCompanyUser` (+ `.Designer.cs`) y `ErpDbContextModelSnapshot.cs`: índice
  `ux_cash_sessions_open_per_company_user`. `FirstRunFullInstallationTests` acepta la baseline seguida de
  migraciones forward; [DATABASE.md](../../DATABASE.md) registra la excepción autorizada.
- `backend/src/ERP.Application/Modules/Caja/CashSessionAvailability.cs` (nuevo),
  `UseCases/RecordCashBankDepositUseCases.cs`, `UseCases/RecordCashMovementUseCases.cs`: sin sobregiro.
- `frontend/src/modules/caja/hooks/useCajaPage.tsx`: total contado.
- Pruebas: `ERP.Infrastructure.Tests/Persistence/Caja/CashSessionOpenUniquenessTests.cs` (nuevo, registrado
  en `tools/ci/backend-test-groups.json`), `ERP.Application.Tests/Caja/RecordCashBankDepositHandlerTests.cs`
  (nuevo), `RecordCashMovementHandlerTests.cs`, `frontend/src/modules/caja/hooks/useCajaPage.test.tsx`;
  comentarios del índice actualizados en `OpenCashSessionUseCases.cs`, `OpenCashSessionHandlerTests.cs`,
  `CashSessionRepositoryBranchScopeTests.cs` y `NewChildEntityTrackingInterceptorTests.cs`.
- SSOT: [backend.md § Turnos de caja](../../architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro)
  (nuevo) y [accounting.md §25](../../architecture/accounting.md#25-caja-contable-accounting-12c) (depósito).

## Gates

Infrastructure PostgreSQL (Testcontainers) `CashSessionOpenUniquenessTests` RED 2/2 → GREEN, más
`CashSessionRepositoryBranchScopeTests` y `NewChildEntityTrackingInterceptorTests`: 10/10;
`dotnet ef migrations has-pending-model-changes`: sin cambios; migración aplicada a `erp_zh_test` existente; Application filtro `Caja` 142/142 (tras el
índice) y `Caja|CashFunding|SupplierCreditRefund` 168/168 (tras el sobregiro; RED 3/19 sobre HEAD); API
HTTP `CajaVentasEndToEndTests`, `CashAccounting12CTests`, `FinancialCommandIdempotencyTests` 36/36; build
Release de ERP.API; frontend `src/modules/caja` 108/108 (RED 1 sobre HEAD), `npm run lint` (0 errores),
`npx tsc -b`, `npm run build`, `npm run architecture:check` (0 nuevas) y `git diff --check`. Suite completa
no ejecutada.

## Datos que quedan en `erp_zh_test` (empresa B, para R4)

Facturas de contado `001-001-000000001` (14.86) y `001-001-000000002` (5.00), Autorizadas con documento
electrónico Fallido; devolución `DEV-15cffa563d` Autorizada (3.68 en efectivo); turno 1 cerrado (apertura
50.00, esperado −83.32, contado 16.68, diferencia +100.00) y turno 2 cerrado (apertura 30.00, esperado 35.00,
contado 34.50, diferencia −0.50); ningún turno abierto en B; motivo «Compra de insumos menores» con cuenta
6.1.01.002; «Caja Principal» con bodega predeterminada. Asientos 14–24. Stock en Bodega Principal: arroz 104,
aceite 54, azúcar 24, detergente 50. Empresa A: sin cambios de datos (su turno «E2E cash session» sigue
abierto). `erp_zh_test` tiene aplicada la migración `20261028210143_CashSessionOpenPerCompanyUser`. Las demás
bases (`erp_zh_dev`, `erp_zh_purchases_qa`, `erp_zh_items_gate_20261027`) no se modificaron: la recibirán al
arrancar la API sobre ellas. Anomalía E2E conservada: depósito de 100.00 sin efectivo (asiento N° 20) y sobrante
del turno 1 (asiento N° 21); ver #23.

Detalle: [e2e-results.json](e2e-results.json).
