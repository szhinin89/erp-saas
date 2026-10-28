# R1 — Arranque: carga inicial, apertura contable y usuario limitado — E2E

Fecha: 2026-10-28. Clasificación: **PASS 5/5 con dos BUG REAL corregidos**; aprobado por el
propietario (R1 CLOSED, 2026-10-28). Escenarios #37, #6, #7, #8 y #3 del
plan de validación V1. Comportamiento: [FEATURES § Carga Inicial](../../../FEATURES.md),
[Inventario §7](../../architecture/inventory.md#7-transferencias-y-saldo-inicial),
[Accounting § Importes de apertura](../../architecture/accounting.md#importes-de-pagos-costos-y-apertura),
[CxC](../../architecture/receivables.md), [Seguridad](../../architecture/security.md). No repite los
E2E de cobros y pagos ya aprobados. Sin commit, push ni deploy; sin migraciones ni cambios de
contrato API.

## Entorno

- Base `erp_zh_test` en la baseline vigente (`InitialProductionBaseline`), sin recrear. Usuario
  `e2e.admin` del seed E2E con credencial efímera no almacenada. Otras bases sin cambios.
- API aislada en `:5004` (Release del HEAD, Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright 1.59) sobre un Vite de desarrollo propio en `:5181`, con una config temporal
  fuera del repositorio cuyo proxy `/api` apunta a `:5004`. La interceptación de Playwright no reenvía
  el cuerpo multipart con archivo, por eso se usó proxy. `:5003` no se usó.
- Empresa de arranque: **ZH E2E B** (`1790016919001`), sin ítems, movimientos ni asientos al empezar.
  La empresa A conserva la historia de los E2E anteriores.
- Datos sintéticos: plantillas oficiales descargadas de la API y completadas con identificaciones
  con dígito verificador válido, nuevas en el tenant.

## Configuración previa (UI)

- Modo operativo de Contabilidad: «Habilitada» desde 27/10/2026, primera elección y sin hechos previos.
- Fecha de apertura de saldos: 27/10/2026.
- Cuenta bancaria «Pichincha Corriente R1» (cuenta contable 1.1.02.001), necesaria para el pago por
  transferencia de #8.

## Resultados por escenario

| # | Escenario | Resultado |
|---|---|---|
| 37 | Carga inicial de productos (IL-1) | **PASS** tras corregir un BUG REAL de presentación |
| 6 | Clientes y proveedores (IL-2/IL-3) | **PASS** |
| 7 | Stock inicial (IL-4) | **PASS** |
| 8 | CxC/CxP iniciales, apertura contable, cobro y pago parcial | **PASS** tras corregir un BUG REAL de presentación (autorizado) |
| 3 | Usuario con perfil limitado | **PASS** |

### #37 — Productos (IL-1)

| Paso | Resultado |
|---|---|
| Lote de 4 filas; la fila 4 tiene Disponible POS=SI sin PVP ([captura](il1-v1-preview.png)) | 3 válidas / 1 con error («Disponible POS=SI exige un PVP válido mayor que cero»); «Confirmar importación» deshabilitado |
| Lote válido de 3 filas con autocreación de catálogo | 3 válidas con aviso de creación de categoría y marca; confirmación: «Se importaron 3 productos correctamente» |
| PostgreSQL | 3 ítems de naturaleza Product, UoM 19, IVA venta/compra 0/0, 4/4 y 4/4, PVP 2.50/3.20/1.80, POS sí; **1** categoría «Abarrotes R1» y **1** marca «Marca R1»; barcodes EAN13, EAN13 e Internal, principales |
| Reabrir `R1-ACEITE-01` en Ítems ([captura](il1-item-aceite-view.png)) | SKU, nombre, Físico/Producto, marca, categoría, unidad 19, IVA 15 % en compra y venta, PVP 3.20 |

**BUG REAL (corregido).** El modal de confirmación decía «El preview muestra 3 categorías y 3 marcas
pendientes de creación» cuando se iban a crear 1 y 1 ([captura](il1-v2-confirm-modal.png)): contaba
avisos por fila, no valores distintos. Se corrigió en `ImportWizardPage.tsx`, que ahora cuenta
mensajes distintos por código. Test focal `ImportWizardPage.catalogCreation.test.tsx`: RED sobre HEAD
(«3 categorías y 3 marcas») → GREEN. En Chromium, un lote nuevo de 2 productos con la misma categoría
y marca muestra «1 categoría y 1 marca» ([captura](il1-fix-modal-distinct-count.png)); ese lote se
canceló sin confirmar. Límite previo que se mantiene: el aviso usa las filas de la página visible del
preview (25 por página).

### #6 — Clientes y proveedores (IL-2/IL-3)

| Paso | Resultado |
|---|---|
| Clientes con identificación repetida ([captura](il2-v1-preview.png)) | Las 2 filas repetidas en Error («La identificación se repite en otras filas del archivo»); confirmación bloqueada |
| 3 clientes (2 cédulas y 1 RUC) | Confirmados |
| 2 proveedores RUC | Confirmados, con aviso «sin datos de contacto» |
| PostgreSQL | Roles Cliente/Proveedor activos; condición de pago guardada solo para la empresa B (E2E-30D/E2E-30D/CONTADO y E2E-30D/CONTADO), 0 filas en otras empresas; datos fiscales del proveedor según el archivo (SI/NO y NO/NO); contacto solo para el cliente con email y teléfono |
| UI | Listados de Clientes y Proveedores con los 5 terceros; la ficha de proveedor reabre con RUC y tipo de entidad deducido «Persona Natural» ([captura](il3-supplier-view.png)) |

### #7 — Stock inicial (IL-4)

| Paso | Resultado |
|---|---|
| Lote con bodega `BOD-NOEXISTE` ([captura](il4-v1-preview.png)) | Fila 3 en Error («No existe la bodega… en la sucursal activa»); confirmación bloqueada |
| Lote válido de 3 filas (una identificada solo por barcode) | Confirmado: «Se importaron 3 existencias correctamente» |
| PostgreSQL | 3 movimientos de saldo inicial con fecha efectiva 27/10/2026: 100 × 1.80, 50 × 2.40 y 30 × 1.10; total 333.00 |
| Reporte de Stock ([captura](il4-stock-report.png)) | 3 productos, valor 333.00 |
| Kardex de `R1-ARROZ-01` ([captura](il4-kardex-arroz.png)) | 100 und, promedio 1.800000, 180.00; último movimiento «Saldo Inicial — 27/10/2026» |

### #8 — CxC/CxP iniciales, apertura contable, cobro y pago

| Paso | Resultado |
|---|---|
| CxC con dos fechas de corte ([captura](il5-v1-preview.png)) | Ambas filas en Error (varias fechas de corte; 2026-10-20 ≠ fecha de apertura) |
| CxP con saldo `99.255` ([captura](il6-v1-preview.png)) | Error «tiene 3 decimales; se admiten como máximo 2. No se redondea» |
| 2 CxC (200.00 y 85.50, una vencida) y 2 CxP (150.75 y 99.25) | Confirmadas; origen saldo inicial y una cuota por el saldo, con sus vencimientos |
| Contabilizar los 3 lotes desde Conciliación ([captura](il8-reconciliation-after-lots.png)) | Asientos N° 1–3, los tres «Conciliado»; puente 3.1.04.001 con 368.50 por reclasificar (333.00 + 285.50 − 250.00) |
| Publicar el ASI: Debe 3.1.04.001 / Haber 3.1.01.001 Capital, 368.50 ([captura](il8-asi-drawer.png)) | Asiento N° 4 «Publicado»; puente en 0.00; «Apertura conciliada: la carga inicial puede cerrarse» ([captura](il8-reconciliation-after-asi.png)) |
| PostgreSQL | Asientos 1–4 `Posted`, balanceados, fecha 27/10/2026: Inventario/CxC/CxP contra el puente y ASI del puente a Capital |
| Cobro parcial de 80.00 a la CxC de 200.00, en efectivo a «Caja Principal» | CxC 200.00 / 80.00 / 120.00 «Parcial» tras recargar ([captura](il5-receivables-after-collection.png)); al reabrir, saldo 120.00; cuota acumulada `partial`; asiento N° 5 Debe 1.1.01.001 / Haber 1.1.03.001 |
| Pago en efectivo a la CxP sin sesión de caja ([captura](il6-payment-cash-rejected.png)) | 422 «No existe una sesión de caja abierta…»; sin filas persistidas. Rechazo correcto por regla de negocio |
| Pago parcial de 50.75 por transferencia ([captura](il6-payment-result.png)) | Pago 00000001 confirmado; CxP 150.75 / 50.75 / 100.00 «Parcial» tras recargar ([captura](il6-payables-after-payment.png)) y en el detalle; asiento N° 6 Debe 2.1.01.001 / Haber 1.1.02.001 |

La carga inicial **no se cerró**: es irreversible y R1 no lo requiere.

**BUG REAL (corregido con autorización expresa del propietario).** En `/finance/receivables`, la
columna «Fecha factura» de una CxC de saldo inicial mostraba la fecha y hora de la carga
(28/10/2026 07:29:28) en lugar de la emisión del documento (01/10/2026 y 15/09/2026)
([captura previa](il5-receivables-list.png)). `AccountsReceivablePage.tsx` mostraba
`invoiceCreatedAt`, que el backend rellena con `CreatedAt` de la CxC cuando no hay factura; el DTO ya
traía la emisión propia (fecha de negocio `DateOnly`). Contradecía FEATURES IL-5B. El listado de CxP ya
mostraba la emisión propia (10/10/2026). Corrección limitada a esa columna: sin factura muestra la
emisión (fecha); con factura conserva la fecha y hora de la factura. Para respetar ADR-034 (sufijo `At`
solo para instantes) y el guardrail F-DT, el campo de respuesta de CxC se renombró, con aprobación del
propietario, de `InvoiceIssuedAt`/`invoiceIssuedAt` a `InvoiceIssueDate`/`invoiceIssueDate`: mismo tipo y
valor, solo cambia el nombre en `SalesReceivableDto` y sus consumidores directos (`receivableService.ts`,
la pantalla y los tests afectados). Regresión focal `AccountsReceivablePage.initialBalanceDate.test.tsx`:
sobre HEAD, 1 FAIL (saldo inicial) y 1 PASS (factura); con la corrección, 2/2 PASS. No se repitió el E2E
en navegador.

### #3 — Usuario con perfil limitado

| Paso | Resultado |
|---|---|
| Alta `r1.digitador`, rol User, perfil DataEntry de la empresa B ([captura](sec-user-new-filled.png)) | Creado; el modal advierte «Sucursales autorizadas: 0» |
| Primer inicio de sesión | Exige cambiar la contraseña temporal; luego, sin sucursal asignada, queda en «No tiene sucursales asignadas» (cerrado por defecto) hasta que el administrador le autoriza la Sucursal Principal |
| Permitido (UI) | Ítems (3, [captura](sec-r1user-allowed-items.png)) y Clientes |
| Denegado (UI) | Carga Inicial ([captura](sec-r1user-denied-initial-load.png)), CxC y Asientos: «Acceso restringido» |
| API con el token del usuario | 200 en ítems y terceros; 403 en `GET`/`POST initial-load/batches`, `GET sales-receivables`, `GET accounting/journal-entries` y `POST finance/collections`; ninguna fila nueva |
| Cambio a la empresa A (sin membership) | Rechazado (400) |

## Cambios de código

- `frontend/src/modules/initialLoad/pages/ImportWizardPage.tsx`: conteo de categorías y marcas
  distintas en el aviso de creación (5+/6−).
- `frontend/src/modules/initialLoad/pages/ImportWizardPage.catalogCreation.test.tsx`: test focal nuevo.
- `frontend/src/modules/finance/pages/AccountsReceivablePage.tsx`: «Fecha factura» de una CxC de saldo
  inicial muestra la emisión propia (6+/1−).
- `frontend/src/modules/finance/pages/AccountsReceivablePage.initialBalanceDate.test.tsx`: test focal nuevo.
- `FEATURES.md`: fila de Carga Inicial — Productos (IL-1).

## Gates

Vitest `src/modules/initialLoad` 32/32 PASS. Tras la corrección de CxC se repitieron Vitest de
Carga Inicial y de `src/modules/finance`, ESLint, `tsc -b`, build, `architecture:check` y
`git diff --check` (resultados en el cierre de R1 en STATUS). Primera pasada: ESLint de los archivos modificados sin errores;
`tsc -b`, `npm run build` y `npm run architecture:check` PASS (0 violaciones nuevas);
`git diff --check` PASS. El build reescribió solo los timestamps de dos reportes generados, que se
restauraron. Suite completa no ejecutada.

## Observaciones fuera de alcance (DEBT, sin ticket)

- El rechazo de pago en efectivo sin sesión de caja muestra el GUID de la caja en lugar de su nombre.
- En el preview de IL-4, una fila identificada solo por barcode muestra «—» en la columna SKU.

## Datos que quedan en `erp_zh_test` (empresa B, reutilizables en R2–R4)

3 productos, 3 clientes, 2 proveedores, stock inicial de 333.00, CxC 120.00 + 85.50, CxP 100.00 + 99.25,
asientos 1–6, cuenta bancaria «Pichincha Corriente R1», modo contable Habilitada desde 27/10/2026,
usuario `r1.digitador` (DataEntry) y carga inicial abierta, no cerrada. Los 9 lotes no confirmados
(inválidos, de arnés y de verificación) quedaron cancelados.

Detalle: [e2e-results.json](e2e-results.json).
