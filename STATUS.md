# Project Status

## IL-7 — Apertura contable de la Carga Inicial (2026-10-10)

**Estado: EN CURSO — IL-7A implementado (sin commit).** IL-7B (posting efectivo + retry/backfill de lotes ya confirmados) e IL-7C (conciliación + UI) pendientes. Asiento manual de apertura de caja/bancos/otros (`ASI`) → ticket separado, requerido antes de cerrar la apertura contable/piloto, contra la misma cuenta puente. IL-8 bloqueará el paso a Operativa con posting de apertura Pending/Failed o conciliación con diferencia.

- **Decisiones:** contrapartida = cuenta patrimonial propia **"3.1.04.001 Saldos de apertura"** (agrupador 3.1.04; nunca Capital ni Resultados acumulados; 3.1.04 verificado libre en `dberpsaas` y `erp_sumak_pilot`). Un asiento por `ImportBatch` de saldos (`SourceEventId` = lote), `EntryDate` = `Company.OpeningBalanceDate`, período abierto obligatorio (PERIOD_NOT_OPEN nunca se salta), sin renumerar. La carga operativa (IL-4/5/6) se confirma independiente: un fallo contable nunca la revierte. Montos solo de datos confirmados del dominio (nunca staging/Excel), redondeo `FiscalPrecision.TaxAmount` + `MidpointRounding.AwayFromZero`. Permisos (IL-7B): confirmación InitialLoad + permiso contable existente, sin permiso paralelo.
- **Reglas sembradas (`AccountingBootstrapStep.MinimalPostingRules`):** `InitialLoad/OpeningInventory` (Debe 1.1.04.001 / Haber 3.1.04.001), `InitialLoad/OpeningReceivables` (Debe 1.1.03.001 / Haber 3.1.04.001), `InitialLoad/OpeningPayables` (Debe 3.1.04.001 / Haber 2.1.01.001), todas `GrandTotal`. Plan retail 105 → 107 cuentas; reglas mínimas 16 → 19. Si 3.1.04/3.1.04.001 ya existen con otro sentido no se siembra ni la cuenta ni ninguna regla de apertura.
- **Empresas existentes (incluida Production):** comando de despliegue `dotnet run -- backfill-opening-balance-posting-setup [apply]` (dry-run por defecto; solo crea lo faltante; nunca modifica cuentas/reglas existentes ni genera asientos). Probado sobre copia de `erp_sumak_pilot`: dry-run → apply → re-ejecución "Canonical", 0 asientos.
- **Modelo:** `OpeningBalancePosting` (tabla `opening_balance_postings`, migración `OpeningBalancePosting`): uno por empresa + lote (índice único, FK a `import_batches`), FactType, monto numeric(18,2), fecha, estado `Pending/Posted/Failed`, `JournalEntryId`, error, intentos, `xmin`. Posted es terminal (corrección = `ReverseJournalEntryCommand`); Failed es reintentable.
- **Validaciones previas (sin generar asiento):** `OpeningBalancePostingPreflight` — lote de saldos `Completed` de la empresa activa, no contabilizado, fecha de apertura definida y = corte registrado (Kardex `EffectiveDate` / CxP `AccountingDate`), monto > 0 desde `IOpeningBalanceSourceReader` (inventario: Σ `TotalCost` del Kardex `InitialBalance` de los documentos del lote; CxC: Σ `OriginalAmount`; CxP: Σ cuotas originales), y luego `PostingPreflight`: las mismas etapas del Posting Engine en seco (regla activa, período abierto, cuentas postables, partida doble) sin lock, sin reservar `JournalEntrySequence` y sin persistir. Sin endpoint ni UI todavía (IL-7B/IL-7C).
- **Gates focales:** Domain `OpeningBalancePostingTests` 15/15; Infrastructure `OpeningBalancePostingPreflightTests` + `OpeningBalancePostingSetupTests` 20/20; regresión seed/backfill (`AccountingBootstrapStepTests`, `AccountingChartBackfill*`, `*RuleMaintenance*`) 129/129; migración aplicada en BD temporal + `has-pending-model-changes` = no; arquitectura PASS (0 nuevas). Suites completas/PostgreSQL: pendientes (ejecución manual).

## SRI-VAT-SEED-ACTIVE-FLAGS — BUG REAL (2026-10-09)

**Estado: CORREGIDO (sin commit) — ADR-042 + revisión de compatibilidad.** `global.sri_vat_rate` códigos históricos 2 (12%) y 3 (14%) estaban declarados inactivos en `SriVatRateConfiguration` pero quedaban activos en BD (verificado en `dberpsaas`): el `InsertData` de la migración base omitía `is_active` para esas filas (DEFAULT true + generación "on add": EF no escribe el `false`), por lo que `SriTaxResolver`/`SriGlobalRateReader` aún resolvían 12%/14% por código. Fix con el estándar de `SriRetentionCodeConfiguration`/`SriDocTypeConfiguration` (`HasDefaultValue(true).ValueGeneratedNever()`) + migración `SriVatRateSeedActiveFlags` (solo `UpdateData` de 2 y 3). Sin datos afectados (`dberpsaas` y `erp_sumak_pilot` no usan 2/3 en ítems, compras, recepciones, ventas ni gastos). Tarifas activas (0, 4, 5, 6, 7, 8, 10) sin cambios.
- **Gates:** `SriVatRateActiveFlagsPostgreSqlTests` 11/11 (flags y tarifas = seed; 2/3 inactivos para resolver y lookup dentro de su vigencia; tarifas activas intactas; insert inactivo por EF persiste false); catálogos SRI PostgreSQL 104/105 (1 preexistente en HEAD: `SriIncomeCatalogMigrationSupplierDefaultsTests`); `has-pending-model-changes` = no; arquitectura PASS; diff check PASS.

## IL-6 — Carga Inicial de Cuentas por Pagar (2026-10-09)

**Estado: EN CURSO — IL-6A e IL-6B CLOSED; IL-6C implementado (sin commit).** Contabilidad de la apertura → IL-7.

- **Confirmación atómica (IL-6B):** una `AccountsPayable` InitialBalance por fila (`OriginId` = `ImportBatchRow.Id`, tipo SRI real, número + normalizado, emisión, sucursal activa, `ImportBatchId`, una cuota por el saldo neto con su vencimiento, `AccountingDate` = `Company.OpeningBalanceDate`), todo el lote en una transacción por el camino por lote de IL-4/IL-5 (`IBatchImportConfirmation`, `FOR UPDATE`; sobrecarga con el Id de la fila, por defecto ignorada por IL-4/IL-5). Antes de escribir se revalida todo: proveedor activo con rol Proveedor, documento no duplicado contra CxP de cualquier origen, tipo de documento activo en el catálogo SRI y de deuda (no 04/06/07), corte = apertura vigente y no futuro, sucursal, saldo/fechas/moneda. Cualquier fallo → rollback total (sin CxP, cuotas ni Outbox parciales; lote sigue Validated). Nunca crea compras ni gastos.
- **SupplierCredit (IL-6B):** `SupplierCreditPayableTarget` admite InitialBalance explícitamente (concurrencia `xmin` de la CxP, moneda = `Company.CurrencyCode`, sin documento de origen); mismo asiento CxP contra Anticipos y reversa espejo.
- **Fecha de apertura (IL-6B):** `OpeningBalanceConstraintsReader` cuenta el `AccountingDate` de las CxP InitialBalance confirmadas como corte confirmado: fija/protege `Company.OpeningBalanceDate`.
- **UI (IL-6B):** confirmación habilitada (todo-o-nada); detalle de CxP muestra el origen "Saldo inicial" sin link a Compra/Gasto.
- **Gates IL-6B:** PostgreSQL IL-6 (validación + confirmación) 18/18 — 1 fila, varias filas/proveedores, 201 filas, duplicado tras preview, proveedor inactivado, tipo de documento inactivado, fallo intermedio → rollback y reintento, listado/detalle, protección de la fecha de apertura, sin compras/gastos —; SupplierCredit contra InitialBalance (asiento + reversa) en `SupplierCreditApplyPayablesIntegrationTests`; regresión PostgreSQL InitialLoad/OpeningBalance/Payables/SupplierCredit/catálogos SRI 282/282; Application 773/773; Domain 127/127; `has-pending-model-changes` = no; frontend initialLoad/payables/finance 70/70 + lint 0 errores + `tsc -b` + build PASS; arquitectura PASS; diff check PASS. Gate UI real: pendiente.
- **IL-6C (concurrencia/idempotencia/recovery):** sin cambios de código ni BUG REAL — CxP Inicial ya usa el camino compartido de IL-4C/IL-5C (`FOR UPDATE` por lote + Tenant + Company, Completed devuelve el resultado sin re-ejecutar, staging reemplazado en transacción, guarda de sucursal del staging). Probado en PostgreSQL real (13 nuevos, estables en 3 corridas): confirmaciones concurrentes + retry post-commit (una sola ejecución, sin CxP/cuotas/Outbox extra), cancelación mientras espera lock, cancel vs confirm (nunca Cancelled con CxP), fallo antes del commit + retry, revalidación mismo/otro archivo sin duplicados y Completed no revalidable, revalidación fallida/cancelada conserva staging, primera validación cancelada conserva Uploaded, otro tenant/company/sucursal no valida/confirma/cancela, 201 filas sin bucle legacy, fila 201 invalidada → rollback total. Suite IL-6 PostgreSQL 30/30; regresión PostgreSQL InitialLoad/SupplierCredit/OpeningBalance/Payables 233/233; Application InitialLoad/Finance 384/384; `has-pending-model-changes` = no; arquitectura PASS; diff check PASS.

- **Modelo (IL-6A):** `AccountsPayableOriginType.InitialBalance` (= 3, nuevo; `Manual` no se reutiliza) y `ImportType.InitialPayables` (= 7). `AccountsPayable.CreateInitialBalance`: saldo NETO pendiente al corte en una única cuota, tipo real del documento histórico, `OriginId` = fila del lote, `AccountingDate` = corte, `DocumentNumberNormalized` + `ImportBatchId` propios. Migración `InitialPayableOrigin`: CHECK `chk_accounts_payables_initial_balance_shape` (solo y siempre el saldo inicial lleva número normalizado y lote) e índice único parcial `uq_accounts_payables_initial_balance_document` (tenant, company, proveedor, número normalizado). La anulación genérica rechaza un saldo inicial (sin flujo de corrección todavía; nunca simula Compra/NC). Normalización de número única (`DocumentNumberKey`, compartida con CxC IL-5 sin cambio de comportamiento). Nunca genera retención ni asiento.
- **Validación fiel (IL-6A):** proveedor existente, activo, no ambiguo y con rol Proveedor activo (nunca se crea; no se exigen defaults de retención; Consumidor Final rechazado); tipo de documento obligatorio y activo en el catálogo SRI (`ISriCatalogLookupRepository.GetActiveDocTypesAsync`) (se admiten no electrónicos; 04 NC, 06 guía y 07 retención no son deuda); número obligatorio, ≤ 30 y no duplicado (normalizado) en el archivo ni contra CxP del proveedor en la empresa de cualquier origen y estado; emisión ≤ corte; vencimiento ≥ emisión; saldo > 0 con máx. 2 decimales sin redondeo; moneda obligatoria solo USD; corte único por lote, no futuro e igual a `Company.OpeningBalanceDate`; lote atado a la sucursal activa. Validación transaccional bajo `FOR UPDATE` (camino compartido). Confirmación bloqueada en backend y UI (`/initial-load/initial-payables`). CxP: origen "Saldo inicial" en filtro/badge.
- **SSOT de saldos iniciales (IL-6A):** `OpeningBalanceImportRules`/`OpeningBalanceRowRules`/`OpeningBalanceImportColumns` centralizan lo idéntico entre CxC IL-5 y CxP IL-6 (fechas invariantes, saldo numeric(18,2), USD, campos obligatorios, sucursal, tercero con rol vía `PartnerImportRole`, número normalizado `DocumentNumberKey`, corte = apertura, fecha de corte única, duplicados en archivo, guarda de staging); IL-4 reutiliza formatos de fecha, decimal invariante, `AddMissing`, fecha de corte única y `WithIssues`. `BusinessPartnerImportRules` expone Consumidor Final y `LEADING_ZERO_LOST`. `Company.OpeningBalanceDate` se lee solo vía `IOpeningBalanceConstraintsReader` (sale de los lookups de CxC/CxP). Refactor mecánico de IL-4/IL-5, sin cambio funcional (mismos códigos y mensajes).
- **Bug SRI corregido:** `global.sri_doc_type` declaraba 02/08/09/18 inactivos (08/09/18 no electrónicos) pero EF omitía el `false` del seed (DEFAULT true + generación "on add") y quedaban activos. Fix con el estándar existente de `SriRetentionCodeConfiguration` (`HasDefaultValue(true).ValueGeneratedNever()`) + migración `SriDocTypeSeedActiveFlags` (solo `UpdateData`). Mismo síntoma detectado y NO corregido en `global.sri_vat_rate` códigos 2 y 3 (Configuración Tributaria CLOSED).
- **Gates IL-6A (tras centralización + fix SRI):** Application InitialLoad/Payables/OpeningBalance/Company 687/687; Domain CxP/CxC 75/75; PostgreSQL InitialLoad IL-1..IL-6 + catálogo SRI + fecha de apertura + Payables 306/308 (2 ajenas: `SriIncomeCatalogMigrationSupplierDefaultsTests` PREEXISTENTE — falla igual en HEAD limpio `a6252db21`, inserta `Company` con el modelo actual sobre una migración anterior a `opening_balance_date` de IL-5; `SupplierPaymentEndToEndTests` FLAKY preexistente — 35/35 en 3 corridas consecutivas, sin relación con IL-6A); cierre: focal PostgreSQL IL-4/IL-5/IL-6 + flags SRI + fecha de apertura 99/99, Application InitialLoad 285/285; API InitialLoad/endpoints/permisos 119/119; `has-pending-model-changes` = no; frontend initialLoad/payables 20/20 + lint 0 errores + `tsc -b` + build PASS; arquitectura PASS (0 nuevas); diff check PASS. Gate UI real: pendiente.

## IL-5 — Carga Inicial de Cuentas por Cobrar (2026-10-09)

**Estado: CLOSED — IL-5A, IL-5B e IL-5C cerrados.** Contabilidad de la apertura → IL-7. Pago por cuota → CXC-INSTALLMENTS-PAID-01.

- **Modelo (migración `InitialReceivableOrigin`, no aplicada aún en BD persistente):** `SalesReceivable.Origin` = Invoice | InitialBalance; `InvoiceId` nullable; `DocumentNumber`, `DocumentNumberNormalized` (calculado por la fábrica), `IssueDate`, `BranchId`, `ImportBatchId` propios del saldo inicial. CHECK de forma por origen, FK a sucursal y lote, índice único parcial InitialBalance sobre tenant+empresa+cliente+número normalizado (protege también la concurrencia). Filas existentes quedan `origin = 1`. `CreateInitialBalance` genera una única cuota por el saldo; la cancelación genérica rechaza InitialBalance.
- **Fecha de apertura SSOT:** `Company.OpeningBalanceDate` (nullable) — único corte de apertura de saldos; IL-6/IL-7 lo reutilizan; IL-5 lee solo este campo. Se define/corrige mientras no haya operaciones reales (venta/devolución autorizada, compra/NC/devolución de compra, gasto confirmado, cobro/pago, sesión de caja, Kardex fuera de `InitialBalance`; borradores y cargas iniciales no cuentan) y debe coincidir con las cargas iniciales ya confirmadas (inventario IL-4, CxC InitialBalance). Con operaciones reales: definitiva; si sigue null, solo admite la fecha de la apertura confirmada (compatibilidad Sumak 2026-09-30). Configuración real: `GET/PUT /api/v1/initial-load/opening-balance-date` (ver: `initialload.batches.view`; cambiar: `initialload.batches.confirm`) y tarjeta en Configuración → Carga Inicial.
- **Validación fiel (IL-5A):** cliente existente, activo, no ambiguo y con rol Cliente activo (nunca se crea; Consumidor Final no admite CxC); número obligatorio y no duplicado (normalizado) en el archivo ni contra CxC existentes del cliente en la empresa (factura o saldo inicial); emisión ≤ corte; vencimiento ≥ emisión; saldo > 0 con máx. 2 decimales sin redondeo; moneda obligatoria, solo USD (vacío = error); corte único por lote, no futuro e igual a `Company.OpeningBalanceDate` (sin fecha definida = error; no depende de IL-4); lote atado a la sucursal activa.
- **Confirmación atómica (IL-5B):** una `SalesReceivable` InitialBalance por fila (sin factura, `ImportBatchId` del lote, sucursal activa, una cuota por el saldo con su vencimiento), todo el lote en una transacción por el mismo camino por lote de IL-4 (`IBatchImportConfirmation`, `FOR UPDATE`). Antes de escribir se revalida todo: cliente activo con rol Cliente, documento no duplicado (normalizado), corte = `Company.OpeningBalanceDate` vigente y no futuro, sucursal activa, saldo/fechas/moneda. Cualquier fallo → rollback total (sin CxC, cuotas ni Outbox parciales; lote sigue Validated). Fuera del bucle genérico. Nunca crea facturas.
- **Listado CxC:** el saldo inicial muestra número propio, cliente e identificación (maestro BP), sucursal y fecha de emisión sin `SalesInvoice`. La cancelación genérica sigue rechazando InitialBalance.
- **Gates:** Domain 1372/1372; PostgreSQL fecha de apertura 6/6; API 373/373 (inventario de endpoints/permisos); frontend initialLoad 9/9; Application InitialLoad/CxC/Company 542/542 (suite completa: 1 falla preexistente en `HEAD`, `CreateSalesDraftHandlerTests.Congela_el_snapshot…`, alcance de bodega); PostgreSQL IL-5A 7/7 (incluye 201 filas, empresa sin fecha de apertura y unicidad normalizada en BD) + lector 5/5; regresión previa InitialLoad/CxC 200/201 (1 falla preexistente en `HEAD`: QA-INVENTORY-INFRA-SUITES-01); `has-pending-model-changes` = no; frontend lint/tsc/build PASS; arquitectura PASS. Gate UI real: pendiente.
- **IL-5B gates:** PostgreSQL IL-5 24/24 (confirmación 1 fila, 201 filas, duplicado tras preview, cliente inactivado tras preview, fallo intermedio → rollback y reintento, listado InitialBalance, cancelación genérica bloqueada, sin facturas); InitialLoad PostgreSQL completo 141/141; Application 2825/2826 (falla preexistente en `HEAD`); cobros/fecha de apertura HTTP 40/40; Domain 1372/1372; frontend initialLoad 9/9 + lint/tsc/build; arquitectura PASS; sin cambios de modelo EF.
- **IL-5C (concurrencia/idempotencia/recovery):** sin cambios de código — CxC Inicial ya usa el camino compartido de IL-4C (`FOR UPDATE` por lote + Tenant + Company, Completed devuelve el resultado sin re-ejecutar, staging reemplazado en transacción, guardas de alcance por sucursal del staging). Probado en PostgreSQL real (13 nuevos, estables en 3 corridas): confirmaciones concurrentes + retry post-commit (una sola ejecución, sin CxC/cuotas/Outbox extra), cancelación mientras espera lock, cancel vs confirm (nunca Cancelled con CxC), fallo antes del commit + retry, revalidación mismo/otro archivo sin duplicados y Completed no revalidable, revalidación fallida/cancelada conserva staging, primera validación cancelada conserva Uploaded, otro tenant/company/sucursal no valida/confirma/cancela, 201 filas sin bucle legacy, fila 201 invalidada → rollback total. Suite IL-5 PostgreSQL 26/26; InitialLoad PostgreSQL completo 154/154.
- **Core CxC previo:** (B) cobro cruzado de clientes corregido (`e598d8dd1`); (A) pago por cuota → ticket CXC-INSTALLMENTS-PAID-01 (no afecta saldos iniciales de una cuota).

## IL-4 — Carga Inicial de Inventario (2026-10-09)

**Estado: CLOSED.** IL-4A (validación), IL-4B (apertura atómica) e IL-4C (idempotencia/concurrencia/recovery) implementados. Sin migración. Contabilidad de la apertura → IL-7.

- **Saldo inicial al corte, sin compras ficticias:** un documento de apertura por bodega (`PostInitialBalanceCommand`), movimiento propio `StockMovementType.InitialBalance` ("Saldo Inicial" en Kardex) con la Fecha de Corte como fecha efectiva. Ajustes normales siguen sin backdating.
- **Validación fiel:** decimal invariante con punto; precisión excedida = error (sin redondeo); bodega por código en la sucursal activa; SKU+barcode del mismo ítem; sin lotes/series ni servicios; solo ítem+bodega sin stock ni Kardex previo; fecha de corte obligatoria, no futura y única por lote.
- **Todo-o-nada:** una transacción (documentos, líneas, stock, Kardex, numeración, Outbox) con revalidación antes de escribir; fuera del bucle genérico de 200 filas.
- **Idempotencia/concurrencia/recovery:** `FOR UPDATE` por lote; Completed devuelve el resultado sin reejecutar; revalidar reemplaza staging; fallo/cancelación no deja estados intermedios; lote atado a la sucursal de su staging.
- **Documento de apertura** no se anula con la anulación genérica de ajustes (discriminador: movimientos InitialBalance).
- **Gates PASS:** unitarios 267/267 (InitialLoad/Inventario); PostgreSQL real 18/18 Inventario Inicial (InitialLoad 117/117, incluye 201 filas); arquitectura 143/143; gate UI negativo y positivo; verificación real SKU 10001204 / WH-E686E280: stock 10, costo 2.50, valor 25.00, InitialBalance al 2026-09-30, sin PositiveAdjust, 1 documento.

## Pendiente antes del piloto — QA-INVENTORY-INFRA-SUITES-01 (registrado 2026-10-09)

**Estado: ABIERTO — no investigado.** Detectado durante IL-4B; no lo causa IL-4.

- Suites de Infrastructure con PostgreSQL real fallan en `HEAD` limpio (`2c3387e5f`): `StockRepositoryCompanyScopeIntegrationTests`, `StockMovementBranchOwnershipIntegrationTests`, `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests`, `PurchaseInvoiceConfirmedPostingIntegrationTests` — 24/32 fallidos en ese conjunto.
- Síntoma principal: `StockRepository.AppendMovementAsync` lanza "El ítem no es un producto de la empresa actual." desde el seeding de las suites.
- Riesgo: esas suites cubren Kardex, costo promedio y confirmación/anulación de compras; mientras fallen no protegen esos flujos. Resolver (causa raíz: datos de prueba vs. regla real) antes del piloto.

## IL-3 — Carga Inicial de Proveedores (2026-10-09)

**Estado: CLOSED.** IL-3A (validación), IL-3B (confirmación atómica) e IL-3C (idempotencia/concurrencia/recovery) implementados; MasterData sin cambios.

- **Validación fiel:** reglas compartidas con Clientes (`BusinessPartnerImportRules`); solo 04 RUC y 08 Exterior (catálogo SRI de uso), Tipo Entidad Legal para Exterior, RUC/cero inicial, duplicados en archivo, contacto.
- **Datos fiscales explícitos:** "Obligado a llevar contabilidad" y "Exento de retención" obligatorios SI/NO, sin default; solo se aplican al crear o asignar el rol, nunca sobre un proveedor existente.
- **BP existente reutilizado:** sin rol → asignar; ya Proveedor → idempotente; rol revocado con datos fiscales → reactivar preservándolos (preview advierte que el SI/NO no se aplica); revocado sin datos fiscales → ERROR. Nunca se duplica BP.
- **Condición de pago por Company:** obligatoria, sin default, en `CompanyBpPurchaseSettings`; distinta a la existente → ERROR.
- **Todo-o-nada:** una sola transacción (BP + rol + contacto + condición + actividad/outbox) con rollback total y revalidación contra el maestro; fuera del bucle genérico de 200 filas.
- **Idempotencia/concurrencia/recovery:** validar/confirmar/cancelar bajo `FOR UPDATE` por lote + Tenant + Company; Completed devuelve el resultado ya confirmado; revalidar reemplaza staging; fallo/cancelación no deja Validating/Confirming.
- **Gates PASS:** unitarios InitialLoad 146/146; PostgreSQL 16 real 20/20 escenarios de Proveedores (InitialLoad 94/94, incluye 201 filas); arquitectura 143/143; gate UI real negativo y positivo PASS.
- Observación: la confirmación toma ~100 ms por fila con el lote bloqueado; medir con el volumen real antes del piloto. Inventario Inicial sigue en el camino genérico.

## IL-2 — Carga Inicial de Clientes (2026-10-09)

**Estado: CLOSED.** IL-2A (validación), IL-2B (confirmación atómica) e IL-2C (idempotencia/concurrencia/recovery) implementados; MasterData sin cambios.

- **Validación fiel:** Validate aplica las mismas reglas de dominio que Confirm (RUC/cédula, cero inicial perdido, tipo de entidad legal para 06/08/09, catálogo de uso SRI, contacto, duplicados en archivo). Consumidor Final (07) no se importa; Categoría/Segmento/Zona salen de la plantilla.
- **Todo-o-nada:** un lote con cualquier fila con error no se confirma; la confirmación corre en una sola transacción (BP + rol Cliente + contacto + condición) con rollback total y revalidación contra el maestro (stale preview).
- **BP existente reutilizado:** nuevo → crear; sin rol Cliente → asignar rol; ya Cliente → idempotente. Nunca se duplica BP (coincidencia sin distinguir mayúsculas).
- **Condición de pago por Company:** obligatoria, sin default, en `CompanyBpSalesSettings`; nunca en el BP global ni sobrescrita si ya difiere.
- **Idempotencia/concurrencia/recovery:** validar/confirmar/cancelar bajo `FOR UPDATE` por lote + Tenant + Company; Completed devuelve el resultado ya confirmado; revalidar reemplaza staging; fallo/cancelación no deja Validating/Confirming. Cancel ya no puede sobrescribir un lote confirmado.
- **Gates PASS:** unitarios InitialLoad 120/120; PostgreSQL 16 real 16/16 escenarios de Clientes (InitialLoad 71/71); gate UI real (prueba negativa y positiva con confirmación) PASS.

## IL-1C — Idempotencia, concurrencia y recovery de Productos (2026-10-08)

**Estado: IMPLEMENTADO y validado; apto para declarar CLOSED. Sin migración, commit ni push.** IL-1A, IL-1B e Items core permanecen CLOSED.

- Confirmación y revalidación toman el mismo lock PostgreSQL por lote + Tenant + Company dentro de la transacción y recargan el estado después de esperar. Completed devuelve el resultado persistido sin ejecutar filas ni crear Items/catálogos/outbox otra vez; retry tras respuesta perdida converge al mismo resultado.
- Revalidación reemplaza incidencias y filas en una sola transacción. Fallo, archivo faltante o cancelación revierte staging y estado; primera validación conserva Uploaded y revalidación conserva Validated. Estados intermedios no se confirman por separado. Lotes completados rechazan revalidación. Otro lote conserva create-only y detecta duplicados existentes.
- Gates PASS: unitarios focales InitialLoad **92/92**; PostgreSQL 16 con migraciones completas **28/28** (14 regresiones IL-1B + 14 escenarios IL-1C); API build y diff check PASS. Incluye requests concurrentes forzados bajo lock real, retry post-commit sin duplicados/outbox adicional, confirmación contra revalidación, reemplazos concurrentes, rollback tras DELETE, cancelación esperando lock, recovery inicial, scope y preview/confirmación/retry de 201 filas.
- **BUG REAL adicional fuera del alcance de Productos:** el bucle legacy de ConfirmImportBatchHandler para otros tipos deja filas fallidas sin marcar; con una página de 200 fallos persistentes puede pedirla indefinidamente y volver a añadir incidencias. Observado en código; no modificado. Productos aborta y revierte en el primer fallo.

## IL-1B — Confirmación atómica del catálogo de Productos (2026-10-08)

**Estado: IMPLEMENTADO y validado; apto para declarar CLOSED. Sin migración, commit ni push.** IL-1A e Items core permanecen CLOSED.

- Productos rechaza lotes con errores; Items, Categorías, Marcas, marcas de filas y finalización del lote comparten una transacción. Primer rechazo, excepción o cancelación aborta todo; rollback con token no cancelable y limpieza del tracker. Los otros tipos de importación conservan su comportamiento.
- Autocreación leída del lote: OFF no crea faltantes; ON crea al confirmar. Categoría/Marca se releen; inactivos, categoría con hijos activos o rama deshabilitada se rechazan. La UI de Productos exige todas las filas válidas y comunica confirmación completa.
- Gates PASS: confirmación unitarios **8/8**; regresión focal de processor/semántica de errores **60/60**; PostgreSQL 16 con migraciones completas **14/14** (13 escenarios + caso de 201 filas con fallo en segunda página). Comprueba rollback real de Items, catálogos, filas y outbox desde contexto nuevo; ON/OFF ante faltantes tras preview, clasificación invalidada, rechazo de dominio, fallo técnico y cancelación. API build, frontend build/platform guard y diff check PASS.
- Sin BUG REAL adicional confirmado. Idempotencia, concurrencia, retry, revalidación del lote y recovery permanecen fuera de alcance (IL-1C).

## A9 — Commercial → ElectronicDocument recovery (2026-10-08)

**Estado: IMPLEMENTADO y validado; apto para declarar CLOSED. Sin commit/push.** A1–A8 permanecen CLOSED.

- Job recurrente `sales-electronic-document-recovery` cada minuto (con Hangfire habilitado): detecta SalesInvoice Authorized + snapshot Electronic + punto de emisión y número definitivo, sin ElectronicDocument por origen. Sin ventana de antigüedad ni dependencia de la request original.
- Descubrimiento cross-tenant de identificadores; contexto Tenant/Company y scope nuevos por candidato. Relectura scoped y validación de Tenant + Company + Branch, estado y número antes del registro.
- Entrada aditiva create-only `IElectronicDocumentRegistration.RegisterMissingAsync` en el issuer existente: crea Draft sin pipeline; cualquier existente, incluidos Draft/Failed, es no-op. El retry electrónico normal conserva ownership del procesamiento posterior y no cambia. Sin Authorize, secuencia ni efectos comerciales repetidos.
- Unicidad existente `uq_electronic_document_source` como garantía final. El perdedor de una carrera descarta su tracker fallido y verifica el documento confirmado; no reanuda ni envía. Sin migración.
- Validación: PostgreSQL 16 con migraciones completas **19/19 escenarios** (18 PASS en la corrida final y 1/1 multitenant tras corregir el fixture de operador de caja); guard de filtros **1/1**; job/scopes **1/1**; regresión issuer **31/31**; Architecture **143/143**; `architecture:check` PASS, 0 nuevas violaciones; API build PASS; `git diff --check` PASS.
- Incluye crash tras commit/contexto nuevo, carreras forzadas en INSERT entre recuperadores y contra la request original, ejecuciones repetidas, existentes sin pipeline, scopes reales, exclusiones, cancelación después del descubrimiento y fallo previo al INSERT recuperable. Ningún BUG REAL adicional confirmado dentro de A9.

## A2 — Allow Sell Without Stock + Negative Inventory Costing (2026-10-06)

**Estado: CLOSED. Cierre operativo aprobado; commit único autorizado, sin push.** A1 permanece CLOSED en `43cce57a9`.

- Política compartida de disponibilidad: controles Company/Item y AllowSellWithoutStock; reservas incluidas y validación transaccional por Item + Warehouse.
- Saldo negativo autorizado, base conocida conservada y costo desconocido explícitamente pendiente. Obligaciones por línea y asignaciones trazables/idempotentes; las entradas cubren obligaciones vigentes por SequenceNumber ascendente dentro de Company + Item + Warehouse, sin cambiar el promedio ponderado.
- COGS provisional, regularización posterior, pendientes contables durables/reintentables, anulación sin residual y devolución vinculada a unidades originales (pendientes primero; costo reconocido proporcional después). Kardex histórico sin reescritura destructiva y modelo base Accounting intacto.
- Gates: Domain **1340/1340**; Sales/COGS Application **84/84**; guardrails existentes **143/143**; API build PASS; `git diff --check` PASS. PostgreSQL: **23 escenarios verificados** (corrida general 21 PASS y dos fallas de fixture por número de devolución demasiado largo; reintento focalizado **3/3 PASS**, incluidos ambos casos corregidos y prioridad de compensación).
- Migración `20261006120230_NegativeSaleCostObligations` y ModelSnapshot; validada en PostgreSQL efímero y **aplicada sin errores a desarrollo dberpsaas**. Smoke mínimo de persistencia **PASS**: registro en historial y lectura de las tres tablas y columnas de valoración nuevas, en transacción de solo lectura. Nuevos endpoints de consulta/reintento documentados en backend/README.md. Decisión y límites: [ADR-041](docs/decisions/ADR-041-negative-sale-inventory-cost-obligations.md).

**Single source of truth** for delivery state. Updated: **2026-10-06** · Kernel refactor: **2026-06-05**.

## POS-RELIABLE-SALE-01 — Cobro POS confiable y tipo de emisión coherente (2026-10-03)

**Estado: IMPLEMENTADO (sin commit). Smoke real pendiente de ejecución por el usuario.**
- **Cobro (flujo B inline):** un único estado del cobro (`utils/salesCollectionStatus.ts`) para resumen, mensaje y bloqueo: vacío = neutro, Falta, Pago exacto, Vuelto. Usa la tolerancia de settlement de la empresa (`CompanyPrecisionPolicy`) en vez de las constantes de UI 0.02/0.01. El efectivo recibido se actualiza en cada pulsación; F8/Enter funcionan desde ese campo. Si el efectivo es el único cobro, lo aplicado sigue al total; en multipago no se redistribuye nada.
- **`canEmit` único:** `emitBlockers` en `useSalesPage` gobierna EmitButton, checklist, F8/Enter y la tarjeta de configuración. Solo los issues `error` de la tarjeta bloquean, y siempre bloquean.
- **Tipo de emisión:** snapshot inmutable de la factura (ver [backend.md § Tipo de emisión](docs/architecture/backend.md#tipo-de-emisión-de-una-venta-snapshot-inmutable-pos-emission-type-snapshot-01)). Authorize ya no lee el EP vivo y la creación del borrador es fail-closed (no cae a `Electronic`). El DTO del listado expone `EmissionType`.
- **Visibilidad:** RIDE, XML, clave, estado, diagnóstico, conectividad y Forma de pago SRI solo aparecen en electrónica, decidido por el snapshot de la factura. El modal de emisión ya no muestra pasos XML/SRI simulados. La tirilla recibe recibido/vuelto desde el payload del POS.
- **Cierre de confiabilidad (POS-CASH-TENDERED-01):** `SalesInvoice.CreateDraft` exige `emissionType` explícito (51 callers de tests actualizados) y `sales_invoices.emission_type` ya no tiene default de BD. El efectivo entregado se persiste como `SalesInvoicePayment.TenderedAmount` (nullable, solo efectivo, ≥ aplicado, CHECK en BD); el vuelto es derivado. La tirilla y la reimpresión lo leen del backend. Migración `20261003233053_PosCashTenderedAndEmissionTypeSnapshotNoDefault`, aplicada y verificada en PostgreSQL de desarrollo.
- **Tests:** Domain 1310/1310 · Application 2613/2613 · Infrastructure focalizados 701 (3 intermitentes por ejecución concurrente; 3/3 en aislado) · API Ventas/Caja 104/104 · Architecture 143/143 · frontend Ventas 667/667 · `architecture:check` PASS.
- **Pendiente:** smoke real autenticado (`run-smoke-pos.ps1`, lo ejecuta el usuario con sus credenciales E2E).

## BUG-PILOT-SRI-CONFIG-500 — La configuración SRI exige contexto de empresa (2026-10-03)

**Estado: COMPLETADO.** Desbloquea la configuración SRI del piloto Sumak (`erp_sumak_pilot`).
- **Bug:** `Get/UpsertSriConfiguration`, `ValidateSriConfiguration`, `UploadSriCertificate` e `InspectSriCertificate` no tenían marker de scope. Sin `X-Company-Id`, el Upsert llegaba a guardar `SriSettings` con `CompanyId = Guid.Empty` y el TenantGuard respondía 500. Con un `X-Company-Id` ajeno no se validaba la membership, una brecha de aislamiento entre empresas del mismo tenant.
- **Fix:** los cinco requests implementan `IRequiresCompanyContext`, el mismo marker que Access, y pasan por `CompanyScopeBehavior` → `ICompanyAccessGuard`. Sin contexto o con una empresa ajena la respuesta es 403 `COMPANY_SCOPE_FORBIDDEN`. No cambian handlers ni contratos. Los sales-defaults (FROZEN) no se tocaron.
- **Tests:** `ElectronicInvoicingCompanyScopeTests` (marker en los 5 requests; Upsert sin contexto se rechaza antes del handler) · Application focalizados 57/57 · Architecture 143/143 · API focalizados 14/14 · `git diff --check` limpio.
- **Pendiente aparte:** `setup/admin` con un email mal formado responde 500 en vez de 422 (el validador no revisa el formato del email).

## ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01 — El reintento automático de comprobantes vuelve a funcionar en Hangfire (2026-10-03)

**Estado: COMPLETADO (sin commit).** Cierra el hallazgo de [ADR-036 §23.3](docs/decisions/ADR-036-retention-electronic-document-cancellation.md). Sin migración ni ADR nuevo: no cambian estados fiscales, XML, firma, SOAP, política de reintento ni el issuer.
- **Bug:** `ElectronicDocumentRetryJob` leía los candidatos antes de fijar `JobExecutionContext`. Hangfire no tiene contexto de tenant, así que el filtro fail-closed devolvía 0 filas y el reintento automático estaba inactivo para todos los tipos de comprobante.
- **Fix (mismo patrón que `RetentionElectronicRecoveryJob`):** `GetRetryCandidatesAsync` es cross-tenant vía `AsPlatformQuery()`, sin tracking, y solo devuelve `ElectronicDocumentRetryCandidate` (tenant, empresa, id, `RetryCount`, `LastAttemptUtc`). El job aplica `ElectronicDocumentRetryPolicy` sin cambios. Cada candidato se procesa con `JobExecutionContext` de su tenant y empresa y en un scope propio. Ahí se resuelve la preferencia `auto_retry_enabled` y el issuer vuelve a leer el documento con los filtros fail-closed de esa empresa.
- **Efecto de comportamiento (esperado):** se reactiva el reintento automático de Factura, Nota de crédito y Retención en Draft, Failed, Signed y Received, con backoff de 1/2/4/8/16 min y DeadLetter a los 5 intentos. Las retenciones siguen pasando por su guard de ciclo de vida (ADR-036 I-1, sin reenvío ciego). La concurrencia con `RetentionElectronicRecoveryJob` sobre comprobantes de retención en `Draft` la serializan el `xmin` del comprobante y el reclamo `Dispatching` bajo lock.
- **Tests:** `ElectronicDocumentRetryJobTenantContextTests` (ERP.API.Tests, PostgreSQL real, filtros reales) tiene 5 casos: candidato encontrado sin contexto; A procesado bajo A; dos empresas del mismo tenant más otro tenant sin contaminarse; no elegibles (Authorized, DeadLetter, intentos agotados, en backoff, preferencia apagada) sin reintento; dos corridas sin duplicar el efecto. Una mutación sin `AsPlatformQuery` hace fallar 4/5. El test de caracterización `Hallazgo_GetRetryCandidatesAsync_…` se invirtió.
- **Validación:** job 6/6 · Infrastructure focalizados 2/2 (incluye `IgnoreQueryFiltersAuditTests`) · Architecture 143/143 · `architecture:check` PASS (0 nuevas) · `git diff --check` limpio.

## ZH-EDOC-COMMUNICATIONS-01 — Correo de comprobantes autorizados: Factura, Nota de crédito y Retención (2026-10-03)

**Estado: COMPLETADO (sin commit).**
- Cubre la fase 5 de [ADR-039](docs/decisions/ADR-039-communications-architecture.md) y la fase de comunicaciones
  de [ADR-038](docs/decisions/ADR-038-sri-electronic-compliance-architecture.md).
- Detalle en [`COMMUNICATIONS-ARCHITECTURE.md` §M](docs/communications/COMMUNICATIONS-ARCHITECTURE.md).

**Funcionalidad nueva:** la nota de crédito autorizada se envía al cliente de la factura y la retención autorizada al
sujeto retenido (correo de su contacto real). La factura conserva su correo idéntico (golden).

**Un solo camino:**
- `ElectronicDocumentAuthorizedEvent` llega a un único handler genérico.
- `IElectronicDocumentCommunicationService` lo comparten el evento y la reconciliación.
- El servicio enruta a un contributor por (`SourceModule`, `DocumentType`): Sales para Factura y NC, Retentions
  para Retención.
- El contributor aporta datos tipados; la cola renderiza el template y escribe la outbox.
- `SalesInvoiceAuthorizedCommunicationHandler` se eliminó. No hay handlers por documento, switches ni reflexión.

**Fuera de la transacción fiscal:**
- Al autorizar solo se inserta, idempotente, la fila.
- El XML autorizado (ElectronicDocuments, byte a byte) y el RIDE (Ride) se adjuntan **por referencia** y se
  resuelven al enviar, desde el almacenamiento oficial.
- Se corrigió un bug real: el transporte usaba `File.Exists` sobre rutas relativas de `IFileStorage`.

**Sin correo:** fila `Failed`/`Permanent` `COMMUNICATION_RECIPIENT_MISSING`, sin envío y sin duplicar. Antes la
factura solo dejaba un log; es una semántica transversal de la cola.

**Template inválido:** misma semántica que la fase 4 para los tres tipos.

**Reconciliación:**
- Job `reconcile-electronic-document-communications`, cada 10 min.
- Encola comprobantes `Authorized` que no tienen comunicación, **de cualquier antigüedad** (verificación final: se
  eliminó la ventana de 7 días, que no ahorraba escaneos), del más antiguo al más nuevo, con antigüedad mínima de
  5 min y hasta 200 por corrida.
- Un cursor continúa entre corridas, así los faltantes omitidos no bloquean a los siguientes.
- Usa la misma identidad que el evento, así que evento + duplicado + reconciliación concurrentes producen una sola
  fila.
- No reencola filas `Failed` y no crea tablas.
- Cross-tenant por ids, con `JobExecutionContext` por documento.

**Migración:** `20261003045206_EdocCommunications`, aditiva: `communication_outbox_attachments.reference_id` + CHECK
de fuente de contenido.

**Diferido:** `CompanyCopy`, retiro del setting `communications.sales_invoice_authorized.enabled`, requeue manual,
monitor y password reset.

**Tests:**
- Application: handler genérico con golden, NC, retención, rutas y robustez; cola con destinatario ausente;
  resolvedor y proveedores de adjuntos.
- Domain: adjunto por referencia y fallo sin destinatario.
- Infrastructure contra PostgreSQL: 3 tipos end-to-end con XML/RIDE, sin correo ×3, override inválido ×3, evento
  duplicado, reconciliación simple/repetida/concurrente, preferencia/antigüedad, multi-tenant.
- Architecture: 4 reglas nuevas, baseline 0.

## ZH-COMMUNICATIONS-TEMPLATES-01 — Subsistema de templates de Communications (2026-10-02)

**Estado: COMPLETADO (sin commit).** Fase 4 de [ADR-039](docs/decisions/ADR-039-communications-architecture.md); detalle en [`COMMUNICATIONS-ARCHITECTURE.md` §J](docs/communications/COMMUNICATIONS-ARCHITECTURE.md).
- **Una sola vía:** TemplateKey (= Purpose) → default embebido versionado (`CommunicationDefaultTemplates`) u override activo de la empresa (`CommunicationTemplate` existente + `revision`) → `ICommunicationTemplateResolver` → `CommunicationTemplateRenderer` → asunto/HTML/texto. Render AL ENCOLAR; la fila guarda el contenido y `template_key`/`template_version`/`template_source`; el envío nunca re-renderiza.
- **Renderer propio** (sin RazorLight): placeholders `{{Nombre}}`, escape HTML por defecto, fail-closed (variable faltante o no declarada, placeholder desconocido o mal formado), variables tipadas por template. Errores `COMMUNICATION_TEMPLATE_NOT_FOUND`/`_INVALID`/`_RENDER_FAILED` (Validation). Un override inválido falla sin fallback silencioso.
- **Durabilidad ante fallo de template (verificación final):** antes, un fallo de template al autorizar una factura dejaba solo un log (la autorización seguía, pero la intención se perdía). Ahora la comunicación se persiste en la misma outbox como `Failed` (Configuration/Permanent), sin contenido inventado, con la misma identidad (no duplica) y las variables para re-renderizarla y reencolarla tras corregir el template. La CHECK `ck_communication_outbox_content` impide que sea enviable. Probado por el flujo real `ErpDbContext` → handler → cola → PostgreSQL.
- **Factura:** el handler ya no contiene HTML ni texto; aporta `SalesInvoiceAuthorizedTemplateModel`. Salida idéntica (golden byte a byte capturado antes de migrar).
- **Aislamiento:** el override de una empresa nunca se usa en otra; System nunca consulta overrides. Cambiar un template no duplica ni altera comunicaciones ya encoladas.
- **Migración:** `20261003035732_CommunicationTemplates` (columnas de template en la outbox, `revision` en `communication_templates`; facturas previas marcadas `Legacy` sin versión).
- **RazorLight:** sin consumidores → REMOVE EVENTUALLY.
- **Tests:** Application 33 nuevos (renderer, resolver, golden de Factura, cola con templates); Domain 2; Infrastructure 5 contra PostgreSQL (override A/B, inactivo, inválido, System, cambio de override tras encolar); Architecture 3. Regresión fases 2 y 3 intacta.

## ZH-COMMUNICATIONS-CONTRACT-01 — Contrato de Communications: alcance, identidad, origen e intentos (2026-10-02)

**Estado: COMPLETADO (sin commit).** Fase 3 de [ADR-039](docs/decisions/ADR-039-communications-architecture.md); detalle en [`COMMUNICATIONS-ARCHITECTURE.md`](docs/communications/COMMUNICATIONS-ARCHITECTURE.md) (§D y "Implementación efectiva — Fase 3").
- **Decisión de almacenamiento System/Company (enmienda de ADR-039 D3):** NULL, no `Guid.Empty`. Auditoría: `CompanyTenantInterceptor` rechaza `Guid.Empty` al guardar, y `Guid.Empty` ya significa "tenant global"/"sin contexto"/"actor sistema". Las entidades de Communications heredan `SystemAggregateRoot`/`SystemBaseEntity` e implementan `IOptionalCompanyScopeEntity`, con filtro global centralizado (fail-closed: los mensajes System son invisibles para toda empresa). CHECK `ck_communication_outbox_scope` e índice de idempotencia con `NULLS NOT DISTINCT`.
- **Contrato:** `ICommunicationQueue.EnqueueAsync(CommunicationRequest)` con `CommunicationScope` explícito (guarda: no se encola para otra empresa que la autenticada). Único constructor de identidad `CommunicationIdentity` (sin email/asunto/cuerpo/SMTP). Origen `SourceModule/SourceType/SourceId` (renombre de `correlation_*`), `RecipientRole`, registro de propósitos (`PASSWORD_RESET` reservado sin productor), reenvío manual explícito. `QueueEmailCommand` (sin llamadores) retirado.
- **Encolado idempotente real:** `INSERT … ON CONFLICT DO NOTHING` dentro de la transacción ambiente. Una colisión ya no puede revertir la autorización SRI (antes, `Add` + índice único sí podía).
- **Historial:** `CommunicationDeliveryAttempt` integrado al claim y al fencing (`Sent`/`Failed`/`Abandoned`/`ClaimLost`, `ProviderCode`, `ProviderMessageId` real o null, texto de error seguro sin destinatario).
- **Factura:** mismo correo; ahora con alcance, origen `Sales/SalesInvoice` y rol `Customer`. Las facturas ya encoladas conservan su clave legacy.
- **Migración:** `20261003020718_CommunicationContract` (backfill demostrable: todas `Company`; `Sales`/`Customer` solo filas de factura).
- **Tests:** Domain 15 nuevos (`CommunicationIdentityTests`, `CommunicationOutboxTests` reescrito); Application 6 (`CommunicationQueueTests`) + handler de factura actualizado; Infrastructure 16 contra PostgreSQL (`CommunicationContractIntegrationTests`: visibilidad por alcance, processor System, CHECK, encolado concurrente y frente a transacción abierta, cambio de email, adjuntos, intentos Transient→Sent / Abandoned / ClaimLost / sin datos sensibles); Architecture 5 (`CommunicationsBoundaryTests`). Regresión de la fase 2 intacta (37/37).

## ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — Entrega de Communications segura con varios workers (2026-10-02)

**Estado: COMPLETADO (sin commit).** Fase 2 de [ADR-039](docs/decisions/ADR-039-communications-architecture.md); detalle en [`COMMUNICATIONS-ARCHITECTURE.md` §G/§H](docs/communications/COMMUNICATIONS-ARCHITECTURE.md).
- **Defecto reproducido:** el processor leía las `Pending` y luego las marcaba `Processing` solo en memoria. Con 3 processors concurrentes y 30 mensajes, PostgreSQL real registró **90 envíos** (cada mensaje 3 veces). Además, una fila podía quedar en `Processing` para siempre, `MaxRetries` de la configuración no se aplicaba y no había timeout SMTP explícito.
- **Corrección:** claim atómico en PostgreSQL (`UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING`, una fila por iteración) con `ClaimToken` + lease de 5 min. Finalizaciones con fencing (`WHERE status = Processing AND claim_token = @token`). Recuperación de `Processing` vencido en el mismo claim. Sin transacción abierta durante SMTP. Timeout SMTP explícito (`Communications:Email:SmtpTimeoutSeconds`, 30 s, acotado a [5, 120] s < lease). Clasificación Transient/Permanent/Configuration/Unknown con política única de reintento. `MaxRetries` copiado al encolar. Message-ID determinístico. `[DisableConcurrentExecution]` solo como defensa secundaria.
- **Garantía real:** claim único + fencing + entrega **al menos una vez** (si SMTP acepta y el proceso muere antes de `MarkSent`, se reenvía tras el lease con el mismo Message-ID).
- **Comportamiento nuevo a conocer:** un fallo `Configuration` (SMTP deshabilitado o incompleto, host inexistente, credenciales inválidas) termina en `Failed` + `failure_category = Configuration` al primer intento, sin reintentos. Tras corregir la configuración hay que reencolar explícitamente (no existe todavía; fase 7).
- **Migración:** `20261003010236_CommunicationDeliveryHardening` (aditiva: `claim_token`, `lease_until_utc`, `failure_category`, índice parcial `ix_communication_outbox_claimable`).
- **Tests:** Infrastructure 10 contra PostgreSQL real (`CommunicationOutboxDeliveryIntegrationTests`: 3 workers, dos nodos, fencing tras lease vencido, worker muerto, recuperación agotada, SMTP lento, configuración sin busy-loop, categorías, multi-tenant, índice de idempotencia) + 26 de transporte (`SmtpDeliveryTransportTests`: Message-ID en el `.eml` real, timeout contra un servidor mudo, clasificación); Domain 15 (`CommunicationRetryPolicyTests`); Application 4 (`CommunicationQueueTests`, timeout en `SendTestEmail`).

## ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — Forgot Password sin fuga de token ni enumeración (2026-10-02)

**Estado: COMPLETADO (sin commit).** Fase 1 de [ADR-039](docs/decisions/ADR-039-communications-architecture.md). Solo Auth: sin Communications, SMTP, templates ni migraciones.
- **Token fuera de los logs:** `LoggingPasswordResetLinkSender` registraba `"Password reset link for {Email}: {Link}"` (enlace con token raw, nivel Information, en todos los entornos). Ahora solo registra `PasswordResetDeliverySimulated`, sin enlace, token ni email.
- **Respuesta neutral:** `forgot-password` responde el mismo 200 para cuenta existente, inexistente, inactiva, sin membresía, con tenants ambiguos o con cupo agotado (antes 400 "No existe una cuenta…" / "Hay múltiples cuentas…"). Sin cuenta inequívoca no se emite token. El motivo real queda en `PasswordResetRequestSuppressed` (solo `UserId`, nunca email). El formato inválido de email (validación) y los fallos técnicos reales siguen el contrato global.
- **Rate limit:** por IP con la política ASP.NET `auth-forgot-password-ip` (10/15 min, 429 `RATE_LIMITED`; `reset-password` no la comparte); por identidad con `PasswordResetRequestThrottle` sobre `IDistributedCache` (Redis si está configurado, memoria si no; clave SHA-256 del email; 3/60 min), contado antes de buscar la cuenta y con supresión neutral. Configurable en `PasswordReset:*`. **Atómico** (verificación final): `IDistributedCache` Get+Set no lo era (20 solicitudes concurrentes → 20 permisos); ahora Redis usa un script Lua `INCR`+`PEXPIRE` sobre una conexión compartida con `RedisCache`, y el fallback en memoria serializa por clave con locks process-local. Aplica también a `RefreshTokenRateLimiter`. Probado con 20 concurrentes / límite 3 en memoria y en Redis real (incluye dos nodos).
- **Sin cambios:** emisión, hash, expiración, single-use e invalidación del token (Auth); `ResetPasswordWithToken`/`CompletePasswordReset`; frontend (ya mostraba "Si existe una cuenta asociada…").
- **Limitación conocida:** el reset por email sigue sin entregarse en producción (y en Development el enlace ya no aparece en el log) hasta la fase 6 de ADR-039.
- **Tests:** Application 14 nuevos (`ForgotPasswordHandlerTests`, `ResetPasswordWithTokenHandlerTests`), Infrastructure 3 (`PasswordResetSecurityServicesTests`), API 5 por HTTP real + PostgreSQL (`ForgotPasswordSecurityHttpTests`), frontend 1 (`ForgotPasswordPage.test.tsx`).

## ZH-COMMUNICATIONS-ARCHITECTURE-01/02 — Arquitectura de Communications (2026-10-02)

**Estado: DISEÑO COMPLETADO (sin implementación, sin commit).** [ADR-039](docs/decisions/ADR-039-communications-architecture.md) (Accepted) + [`docs/communications/COMMUNICATIONS-ARCHITECTURE.md`](docs/communications/COMMUNICATIONS-ARCHITECTURE.md). Sin cambios de código, tablas ni frontend.
- **Hallazgo central:** Communications ya existe como capacidad transversal (outbox genérica, cola, un solo `IEmailSender`/`SmtpEmailSender`, job, SMTP por empresa). Se evoluciona; no se crea otro módulo ni otra outbox.
- **Riesgos vigentes:** (1) el enlace de password reset con el token raw se escribe en el log (`LoggingPasswordResetLinkSender`, única implementación; no se envía correo en producción), `forgot-password` permite enumeración y no tiene rate limit; (2) `process-communications` puede enviar dos veces el mismo correo (sin claim atómico ni `[DisableConcurrentExecution]`) y una fila puede quedar en `Processing` para siempre.
- **Decisiones cerradas:** DA-1 — los mensajes System (reset, verificación, alertas, invitaciones) usan siempre el SMTP de instancia, nunca "la primera empresa del usuario". DA-2 — templates por defecto embebidos y versionados + override opcional por empresa (`CommunicationTemplate` existente), renderer propio; RazorLight no se adopta.
- **Siguiente:** Fase 1 `ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01` y Fase 2 `ZH-COMMUNICATIONS-DELIVERY-HARDENING-01` (independientes).

## ZH-SRI-ANEXO26-PROVIDER-RUC-01 — RUC Proveedor en comprobantes electrónicos (2026-10-02)

**Estado: IMPLEMENTED / PENDING REAL SRI VALIDATION (sin commit).** P0 fiscal; fase 1 de [ADR-038](docs/decisions/ADR-038-sri-electronic-compliance-architecture.md). Ficha Técnica 2.34 Anexo 26; Res. NAC-DGERCGC26-00000027.
- **Arquitectura:** `SystemProviderSettings` → `SystemProviderRucAdditionalInfoContributor` → `IElectronicDocumentAdditionalInfoComposer` (SSOT de `infoAdicional`) → `CommercialElectronicDocumentXmlSupplier` (01/04) y `RetentionElectronicDocumentXmlService` (07: pipeline, vista previa XML y RIDE) → builders sin cambios → XSD/firma/SRI → RIDE sin cambios.
- **Regla fiscal (ADR-038 D7):** `EffectiveDate` es la fecha de aplicabilidad y se compara con la fecha de emisión de negocio. Sin fecha + deshabilitado → sin campo. Sin fecha + habilitado → fallo. Emisión anterior → sin campo (XML idéntico al anterior). Desde la fecha: habilitado + RUC válido → `RUC Proveedor`; deshabilitado o RUC inválido → fallo. Todo fallo es `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` (422): ED `Failed`, sin XML, firma ni llamada al SRI; reintentable al corregir.
- **Composer:** orden determinístico (contributors por `Order`, luego campos del documento), nombres únicos sin distinguir mayúsculas, normativos reservados, nombre y valor 1..300 sin truncado, máximo 15, `ELECTRONIC_DOCUMENT_ADDITIONAL_INFO_INVALID` (422).
- **Invariante de configuración:** `Configure` y el validador rechazan `Enabled = true` sin `EffectiveDate` (422, error en `effectiveDate`). Admin-core exige RUC y fecha al habilitar, con ayuda "Fecha desde la cual el RUC del proveedor será obligatorio en los comprobantes electrónicos.".
- **Verificado:** los XML de Factura 1.1.0, NC 1.1.0 y Retención 1.0.0 con el campo pasan sus XSD actuales; los parsers RIDE existentes lo muestran; vista previa y pipeline de retención producen el mismo XML. Tests de arquitectura con baseline 0.
- **Antes de desplegar en cada instalación:** ejecutar la consulta de solo lectura de ADR-038 § Implementación (habilitado sin fecha / RUC inválido) y revisar la configuración. DEV: sin configuración (0 filas).
- **Pendientes de cierre:** confirmación del registro de ZH Technologies en el listado de proveedores; fecha legal exacta en `EffectiveDate` (no calculada); configuración revisada antes del despliegue; autorización real en `celcer` de al menos una factura con el campo (configurando una `EffectiveDate` propia en la instancia de pruebas).

## ZH-SRI-ARCHITECTURE-DESIGN-01 — Arquitectura de cumplimiento electrónico SRI (2026-10-02)

**Estado: DISEÑO COMPLETADO (sin implementación, sin commit).** [ADR-038](docs/decisions/ADR-038-sri-electronic-compliance-architecture.md) (Accepted) + [`docs/sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md`](docs/sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md). Sin cambios de código, tablas, XML, catálogos ni frontend.
- **Decisión central:** ElectronicDocuments evoluciona (no hay "SRI Core" paralelo). Los módulos solo aportan datos fiscales (providers); firma, `SriSoapClient`, 70/43/45, XSD y ConsultaComprobante se conservan.
- **Piezas nuevas previstas:** composer único de `infoAdicional` (fase 1), perfil fiscal del emisor proyectado desde `Company` (fase 2), versión de ficha separada de la versión XML (fase 3), servicio de estado por empresa (fase 4/5).
- **Hallazgos de código:** la versión XML activa está en 3 fuentes (builder, validador, `manifest.json`, con Retention `null`); recepción y autorización se interpretan con strings (`StartsWith("[70]")`); `RetentionAnnulmentService` usa el gateway SRI directamente. Solo existen dos orquestadores provider→builder (`CommercialElectronicDocumentXmlSupplier`, `RetentionElectronicDocumentXmlService`): ahí se invocará el composer.
- **Siguiente P0:** `ZH-SRI-ANEXO26-PROVIDER-RUC-01`, con diseño exacto en §X (sin cambios en builders/providers/RIDE). Regla definitiva (DA-2): `EffectiveDate` es la fecha de aplicabilidad fiscal; antes de ella nunca se emite el campo (sin emisión anticipada); desde ella, `Enabled=false` o un RUC inválido fallan cerrado (`SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`, sin XML); `Enabled=true` sin `EffectiveDate` también falla cerrado. Se compara con la `IssueDate` de negocio. Base normativa confirmada (DA-1): Res. NAC-DGERCGC26-00000027, RO No. 335 (Quinto Suplemento) del 28/07/2026, 60 días hábiles. Pendiente solo lo administrativo: registro de ZH en el listado de proveedores y fecha legal exacta para `EffectiveDate`.

## ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — Renta alineada con el Catálogo ATS oficial 06/08/2026 (2026-10-02)

**Estado: COMPLETADO (sin commit).** P0 fiscal; segundo slice de ADR-037.
- **Fuente:** `Catalogo_ATS.xls` del SRI (SHA-256 `bd3f7834…776e3e`), hoja TABLAS RETENCIONES, Tabla 3.10, bloque DESDE 06/AGOSTO/2026. La matriz completa (127 códigos) está en `docs/sri/SRI-ATS-INCOME-RETENTION-MATRIX-2026-08-06.md`.
- **Defecto:** de los 13 códigos de Renta del ERP, 9 tenían un nombre que no correspondía al significado oficial del código (p. ej. 304 rotulado "mano de obra" al 2 %; oficialmente es "intelecto" al 10 %). Las tasas de 312, 304, 307, 309, 320, 325 y 343 estaban desactualizadas.
- **Corrección (decisión del propietario: mismo Id y código, versionado):**
  - Las 13 versiones heredadas solo cierran `ValidUntil = 2026-08-05`.
  - Hay versiones ATS nuevas desde 2026-08-06, con `AtsCode` confirmado.
  - El nombre y la tasa operativa del concepto se alinean con la versión vigente; un compliance test impide que diverjan.
  - Snapshots y documentos no cambian.
- **Tasas condicionales** (310 "1 /0 según resolución", 327 "12 o 14"): nuevo `SriRetentionRateKind.Conditional` + `RateRuleText`, sin porcentaje. Concepto no habilitado; la resolución falla cerrado (`ConditionalRateUndetermined`).
- **Retirados** 341/342/344 (no existen en el bloque vigente): sin versión nueva y no habilitados; los históricos siguen legibles. No se remapean a 3440.
- **Defaults de proveedor:** la migración deshabilita (no borra ni remapea) los que apuntan a conceptos cuyo significado cambió o que se retiraron (304, 307, 309, 310, 320, 325, 327, 341, 342, 343, 344). Deben revisarse y reactivarse explícitamente. Antes de desplegar al piloto, revisar cuántos hay.
- **Sin altas** de los demás códigos ATS (decisión: solo la matriz).
- **Migración** `SriRetentionIncomeCatalogAts20260806`. DEV quedó al día hasta `SriRetentionCatalogVersioning` (autorizado); esta migración nueva **no** se aplicó a DEV ni al piloto.

## ZH-SRI-RETENTION-CATALOG-SSOT-01 — codigoRetencion oficial desde catálogo global versionado (2026-10-02)

**Estado: IMPLEMENTED / PENDING REAL SRI VALIDATION (sin commit).** P0 de `docs/sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md` (R24); primer slice de ADR-037. No se cierra hasta autorizar en `celcer` una retención real con Renta + IVA: el entorno no tiene empresa con certificado configurado.
- **Defecto corregido:** el XML de retención emitía la clave de negocio del catálogo (`725`) como `codigoRetencion`. Ahora emite el código oficial de la Ficha v2.34 Tabla 20 (IVA 10%→9, 20%→10, 30%→1, 50%→11, 70%→2, 100%→3), resuelto a la fecha de emisión. `RetentionXmlBuilder` no cambia.
- **Modelo (ADR-037):** `global.sri_normative_source` + `global.sri_retention_code_version` (vigencia `ValidFrom/ValidUntil`, `Percentage`, `XmlCode`, `AtsCode` nulo hasta tener fuente). Sin TenantId/CompanyId, sin CRUD/UI. 721–728 se conservan como clave de negocio. `IsActive` sigue siendo la habilitación operativa (sin renombrar).
- **Conceptos nuevos:** `IVA-50` (habilitado). `IVA-0` (retención en cero, código 7) e `IVA-NP` (no procede, código 8) quedan **no habilitados**, porque `RetentionDocumentLine` exige tasa > 0.
- **728 (IVA 15%):** se conservan su Id y sus referencias históricas, pero queda **no habilitado** (`IsActive=false`, mediante `UpdateData` de la migración): la Tabla 20 no lo define. No aparece en las lecturas seleccionables y no se pueden crear defaults nuevos con él (`AddSupplierRetentionDefaultValidator` usa ahora `IRetentionCodeResolver.GetSelectableByIdAsync`). Los defaults existentes siguen legibles. Emitirlo falla cerrado con `SRI_FISCAL_CATALOG_CONFIGURATION_ERROR` (422).
- **Renta:** comportamiento sin cambios (código XML = código del catálogo). Fuente: catálogo ATS **no verificado** y porcentajes sin exigir (ADR-037 DR-4).
- **Resolver:** `IRetentionCodeResolver` extendido con `GetSelectable…` / `GetByIdIncludingDisabledAsync` / `ResolveForDateAsync` (fail-closed tipado: sin versión, ambigua, sin XmlCode, tasa distinta, concepto inexistente). Se usa en `RetentionElectronicDocumentDataProvider`.
- **Migración aditiva** `SriRetentionCatalogVersioning` (solo `CreateTable`/`CreateIndex`/`InsertData`; `InitialEnterpriseBaseline` intacta).
- **Tests:** `SriCatalogComplianceTests` (19) + `SriRetentionCatalogResolutionIntegrationTests` (20, PostgreSQL real: XML de punta a punta provider → resolver → builder → XSD).

## ZH-RETENTION-SRI-ANNULMENT-01B — Anulación SRI alineada con la Ficha Técnica v2.34 (2026-10-02)

**Estado: COMPLETADO (sin commit).** ADR-036 §25. Evoluciona 01 sin descartarlo. Ventas y Notas de Crédito no cambian; el bug de tenant del job genérico de reintento sigue pendiente (ticket propio).
- **Corrección:** el precheck de 01 ("no existe servicio oficial para consultar la anulación") era **incorrecto**. No existe WS para SOLICITAR la anulación, pero **ConsultaComprobante** (Ficha v2.34 §8; también en la v2.32 del repo) existe para CONSULTARLA, y el SRI es la fuente de verdad de AUTORIZADO / PENDIENTE DE ANULAR / ANULADO.
- **`ISriDocumentStatusQuery`** sobre el mismo `SriSoapClient`, sin un segundo cliente SOAP. Resultado técnico Success / Rejected / Timeout / Unavailable / Unknown, separado del estado fiscal Authorized / NotAuthorized / PendingAnnulment / Annulled / Unknown. `RECHAZADA` (99) nunca es NO AUTORIZADO; un timeout nunca es ANULADO.
- **Flujo:**
  - "Ya presenté la solicitud" → consulta automática; además, verificación a demanda y polling en `RetentionElectronicRecoveryJob` (cada 30 min por solicitud, sin transacción durante el SOAP, idempotente).
  - Solo un **ANULADO** informado por el SRI acepta la solicitud (con evidencia técnica: fecha, respuesta cruda, clave) y finaliza el origen exactamente una vez.
  - AUTORIZADO / PENDIENTE DE ANULAR / NO AUTORIZADO / fallos: sin cambio fiscal ni reversos.
  - **Se eliminó la resolución manual** (comando, endpoint, modal); no queda fallback manual.
  - Desistir ya presentada solo con el SRI confirmando AUTORIZADO.
- **NO AUTORIZADO** sobre un comprobante ya autorizado: sin acción automática; **Decision Required** (ADR-036 §25.7).
- **Migración aditiva** `RetentionSriAnnulmentVerification` (columnas de la última verificación y evidencia, índice de polling).

## ZH-RETENTION-SRI-ANNULMENT-01 — Retención autorizada: anulación ante el SRI asistida y segura (2026-10-01)

**Estado: COMPLETADO (sin commit) — evolucionado por 01B** (la resolución manual y el permiso reforzado se retiraron; ver arriba). ADR-036 §24. Ventas y Notas de Crédito no cambian.
- **Precheck (corregido en 01B):** se concluyó, erróneamente, que no existía servicio oficial para consultar anulaciones. El portal SRI en Línea no se automatiza: la solicitud es **asistida**; no se inventan endpoints.
- **Solicitud desde Compra/Gasto:** `cancel` con `requestSriAnnulment: true` crea una `RetentionAnnulmentRequest` (a lo sumo una abierta; índice único parcial). El comprobante pasa a `AnnulmentPending`, la CxP queda retenida y el origen sigue **Confirmed**, sin reversos. Se conserva el motivo para la finalización.
- **Pasos auditados:** presentar (fecha, referencia), ~~resolver manualmente~~ (reemplazado por la verificación en el SRI, 01B), desistir.
- **ANULADO:** comprobante → `Cancelled` (`ConfirmExternalAnnulment`; `MarkCancelled` eliminado, `Authorized→Cancelled` imposible). Luego `FinalizeRetentionOriginCancellation` anula el origen por su **flujo oficial** (`CancelPurchaseHandler`/`CancelExpenseDocumentHandler.ExecuteAsync`) exactamente una vez; un fallo queda registrado y `RetentionElectronicRecoveryJob` lo reintenta.
- **Desistida:** comprobante → `Authorized`, CxP liberada; origen y retención intactos.
- **CxP retenida (regla en el agregado):** pagos, créditos de proveedor, notas de crédito y devoluciones → 422 `RETENTION_ANNULMENT_PENDING` (nuevo `IApiCodedDomainRule`, opt-in).
- **Permisos:** todos los pasos = anular el origen (`purchases.update` / `expenses.documents.cancel`; el `electronic-documents.retry` adicional de "resolver" se retiró con la resolución manual en 01B). Sin permiso global nuevo.
- **UI** (Compra y Gasto): modal "La retención ya fue autorizada por el SRI → Iniciar proceso de anulación", y un panel con estado, datos copiables para SRI en Línea, plazo ordinario (día 7 del mes siguiente, con advertencia de validar contra el SRI) y acciones según permiso. Nunca muestra "anulada" antes del ANULADO finalizado. El Monitor conoce `AnnulmentPending`.
- **Migración aditiva** `RetentionSriAnnulment`. Los históricos (retención anulada con comprobante autorizado) no se corrigen: consulta "Requiere conciliación histórica" en ADR-036 §24.5.
- **Pendientes externos:** O-11 (plazo / días hábiles), O-12/O-14 (aceptación del receptor), P-3 (resuelto para la consulta en 01B; la solicitud sigue sin WS), O-9, ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01, Decision Required NO AUTORIZADO (§25.7).

## ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — Retención: ciclo electrónico seguro + transmisión automática (2026-10-01)

**Estado: COMPLETADO (sin commit).** Implementa ADR-036 fases 1–2 + transmisión inmediata. Sin migración: `Dispatching=11`/`Discarded=12` en un enum persistido como `int`. Ventas/NC sin cambios: el protocolo nuevo aplica solo a orígenes con guard (hoy Retentions).
- Gate SSOT `IElectronicDocumentSourceLifecycleGuard` en `ElectronicDocumentIssuer`: una retención no `Issued` nunca registra, genera XML, firma, envía, reenvía ni reactiva su comprobante (ni manual, ni job, ni Monitor).
- Cancel vs. Send: el reclamo `Dispatching` se persiste bajo `FOR UPDATE` de la retención antes del SOAP, y la anulación toma el mismo lock. `Dispatching`, `Signed` histórico y `Received` nunca se reenvían: solo se consultan. Una respuesta no concluyente queda como "Requiere conciliación".
- Anulación de Compra/Gasto:
  - sin intento externo → se permite y el comprobante queda `Discarded`, con reversos exact-once;
  - en proceso → 422 `ELECTRONIC_DOCUMENT_IN_PROCESS`;
  - `Authorized` → 422 `ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT`;
  - en ambos bloqueos no hay `Cancelled` ni reversos. Se acabó la anulación local silenciosa ERP Cancelled / SRI Authorized.
- Transmisión automática después del commit de la confirmación (Compra y Gasto), más `RetentionElectronicRecoveryJob` (cada minuto) para retenciones `Issued` sin comprobante. Idempotente: un solo comprobante por origen.
- UI: se eliminó "Registrar electrónicamente" de Compras. Estado electrónico compacto en la retención de Compras y Gastos. El Monitor conoce `Dispatching`/`Discarded`.
- Permisos XML/RIDE por origen, server-side (`purchases.view` / `expenses.documents.view`). La recuperación además exige `purchases.update` / `expenses.documents.confirm`.
- **Hallazgo (no corregido, ADR-036 §23.3):** `ElectronicDocumentRetryJob` no ve candidatos en Hangfire (filtro fail-closed sin contexto de tenant): el reintento automático genérico está inactivo para todos los tipos. Corregirlo activaría reintentos de Ventas/NC (CLOSED); requiere su propio ticket.
- Pendiente: ZH-RETENTION-SRI-ANNULMENT-01 (anulación oficial SRI). Consultas de solo lectura A–E del piloto en ADR-036 §23.5 (no ejecutadas: sin acceso).

## ZH-RETENTION-ELECTRONIC-CANCELLATION-ADR-01 — Documento electrónico de una retención anulada: auditoría + ADR (2026-10-01)

**Estado: AUDITORÍA + ADR-036 Accepted (política, decisiones D-1…D-12 del 2026-10-01) — implementación pendiente.** Sin cambios productivos, de esquema ni de frontend; el comportamiento vigente no cambia todavía y ElectronicDocuments v1.0 sigue CLOSED hasta implementar la extensión.
- Reproducido en PostgreSQL (6 tests de caracterización, `PurchaseRetentionConfirmIntegrationTests.ElectronicCancellation.cs`, a invertir al implementar): con la retención `Cancelled`, `RetryAsync` reenvía el XML firmado (`Signed`, `DeadLetter` con pre=Signed) y el documento termina `Authorized`; la anulación de la compra no se bloquea con un reintento en vuelo ni con el ED `Authorized` (anulación local silenciosa con reversos); un `Sent` persistido queda varado; `Received` solo consulta; `Draft`/`Failed` no generan XML pero siguen siendo candidatos del job.
- Política aprobada: una retención anulada nunca avanza su ED; `Discarded` solo sin intento externo; estado `Dispatching` antes de la llamada (no `Sent`); `Signed` existente y `TIMEOUT`/desconocido nunca reenvían sin consulta concluyente; `Received`/en proceso bloquean la anulación del origen; `Authorized` exige anulación oficial en línea (`AnnulmentPending`, comprobante vigente, sin reversos) y los reversos se completan solo con ANULADO confirmado (`MarkCancelled` con evidencia); XML/RIDE con permiso por origen (`purchases.view` / `expenses.documents.view`). Normativa NAC-DGERCGC25-00000014/-00000017 incorporada (el texto literal debe adjuntarse antes de implementar).
- Hallazgo D-12: la transmisión al SRI de una retención `Issued` es manual (decisión de QA de 04B), Gastos no tiene camino de UI para transmitir y nada alerta de retenciones `Issued` sin ED; con la obligación de transmisión inmediata esto es una brecha. Decisión pendiente O-16 (recomendación técnica: disparo posterior al commit + job idempotente de respaldo).
- **Pendientes:** O-1, O-4, O-9…O-17, P-2 (ADR-036 §22). Consultas de solo lectura del piloto Q1–Q6 en ADR-036 §16 (no ejecutadas: sin acceso).

## ZH-RETENTION-CANCELLATION-LIFECYCLE-01 — Retención: se anula solo con su documento origen, terminal (2026-10-01)

**Estado: COMPLETADO (política C).** Sin migración. Sin cambios en cálculo tributario, Compras (inventario/costos), lógica de Gastos ni pipeline SRI/RIDE.
- Dead-end reproducido en PostgreSQL (antes del cambio): compra confirmada + retención anulada sola → CxP 115 sin retención, asiento de retención reversado, reconfirmar y vista previa rechazados; única salida, anular la compra.
- Política: la retención es parte de la confirmación del origen (decisión 15), `Cancel()` solo desde la anulación de Compra/Gasto (`RetentionCanceller`), `Cancelled` terminal ("No hay transición Cancelled → *" del diseño). Corregir = anular el origen y registrarlo de nuevo. Una sola semántica para Compras y Gastos.
- Retirados: `CancelRetentionCommand`/`Handler`/`Validator`, `POST /purchases/{id}/retention/{rid}/cancel`, `cancelForPurchase` y el botón/modal "Anular retención". Nuevo gate de arquitectura `RetentionCancellationLifecycleTests`. Advertencia de retención terminal en los modales de anular compra y gasto.
- Tests PostgreSQL: terminal tras anular la compra (no se reconfirma ni se registra ante el SRI), doble anulación concurrente y reintento en Compras y Gastos (un solo `ReverseRetention` y un solo reverso contable), otra sucursal sin efectos, comportamiento vigente con comprobante autorizado.
- **Pendiente de definición funcional (SRI):** ninguna anulación revisa el `ElectronicDocument`; el ERP no anula ante el SRI y el reintento del pipeline reenvía XML firmado (`Signed`/`Received`) sin revisar el estado de la retención. Corregirlo requiere ADR (ElectronicDocuments v1.0 CLOSED).
- Datos: BD de desarrollo sin compras/retenciones/documentos electrónicos; la base piloto no fue consultada (sin acceso) — ejecutar la consulta de solo lectura del informe antes de desplegar.

## ZH-PURCHASE-RETENTION-CONFIRM-01 — Compras: retención definida en el borrador y emitida al confirmar (2026-10-01)

**Estado: COMPLETADO.** Compras reabierta SOLO para alinear la emisión de retenciones con `RETENTIONS-MODULE-DESIGN-01` decisión 15 (decisión funcional aprobada). Sin migración. Inventario, costos, cronograma, impuestos, documento electrónico de compra y posting de compra sin cambios de semántica.
- Regla: la retención de una Compra o Gasto se define antes de confirmar y, con `RetentionIntent`, se emite dentro de la misma confirmación (compra + inventario + CxP con `ApplyRetention` + `RetentionDocument` + asientos de compra y de retención, un único `SaveChanges`); si falla cualquier paso, la compra sigue en borrador. El registro SRI sigue siendo posterior y manual.
- Backend: `RetentionIntent`/`RetentionIntentValidator` movidos a `Retentions` (contrato único Gastos/Compras); `ConfirmPurchaseCommand` acepta `Retention` (paso 4b tras la CxP, vía `RetentionIssuer.IssueAsync` + `PurchaseRetentionSource`); vista previa `retention-preview` solo sobre borradores y sobre `IRetentionEligibilityService` (corrige la divergencia: ahora considera si la empresa es agente de retención). Retirados `POST /purchases/{id}/retention`, `IssueRetentionCommand`/`IssueRetentionHandler`/`IssuePurchaseRetentionRequest` (único consumidor: la UI de Compras).
- Frontend: sección de retención del borrador (`PurchaseRetentionDraft`): vista previa automática, montos precargados no editables, opción "Emitir la retención al confirmar la compra", punto de emisión (preselecciona el por defecto) y fecha; "Confirmar compra" envía la intención. Retirados "Calcular"/"Emitir", el modal de punto de emisión por ID, `issueForPurchase` y `withholdingMessages`. `RetentionIntentRequest` declarado una vez en `retentions` (Gastos lo reexporta).
- Sin cambio: anulación (cascada `RetentionCanceller` al anular la compra y anulación propia de la retención), XML/RIDE/registro SRI, permisos (`purchases.update` confirma; ningún permiso de Gastos).
- Tests: 12 de integración PostgreSQL (`PurchaseRetentionConfirmIntegrationTests`: sin intención, atómico con intención, empresa no agente, intención inválida, punto de emisión inexistente, retención > saldo, fallo del asiento → rollback, doble confirmación concurrente, reintento, intenciones incompatibles concurrentes, anulación en cascada, otra sucursal); unitarios de handler, vista previa, controller y frontend (hook, componente, modelo).
- Datos existentes: la BD de desarrollo no tiene compras; compras ya confirmadas sin retención no tienen camino de emisión posterior (decisión: no crear flujo paralelo). Antes de desplegar en el piloto, ejecutar la consulta de solo lectura del informe de la entrega; si hubiera compras que requieran retención, la vía es anular y volver a registrar la compra.

## ZH-RETENTION-EMISSION-SSOT-01 — Retenciones: una capacidad, momento de emisión por origen (2026-10-01)

**Estado: AUDITORÍA COMPLETADA + limpieza en Retentions; el cambio de Compras se implementó en ZH-PURCHASE-RETENTION-CONFIRM-01.** Sin migración ni cambios de frontend, montos, cuentas o reglas fiscales.
- Capacidad NO duplicada: Gastos y Compras emiten con el mismo `RetentionIssuer.IssueAsync` (unicidad por origen, revalidación con `IRetentionEligibilityService`, secuencia "07", `Issue`), anulan con el mismo `RetentionCanceller` (revierte la CxP, bloquea con pagos) y contabilizan con el mismo asiento `Retentions/DocumentIssued` (traductor estricto) y su reverso. 1:1 por origen: chequeo + índice único parcial `uq_retention_documents_active_source` + `xmin` de gasto/compra + Lock A en Compras.
- Duplicidad real = política de MOMENTO: Gastos emite dentro de la confirmación (una sola `SaveChanges`: gasto + CxP con retención + retención + asientos; si falla la retención no se confirma), conforme a la decisión 15 de `RETENTIONS-MODULE-DESIGN-01` ("integrada en la confirmación transaccional del documento origen (Compra/Gasto)"); Compras emite después de confirmar (`POST /purchases/{id}/retention`, transacción propia con Lock A). La migración de Compras a `RetentionDocument` (2026-09-04) mantuvo la emisión posterior sin justificación documentada.
- **Propuesta (no implementada — requiere reabrir Compras):** confirmar compra con `RetentionIntent` opcional (mismo record que Gastos) dentro de la misma `SaveChanges` atómica de `ConfirmPurchase` (mismo modelo que Gastos: compra + CxP con la retención aplicada + retención + asientos), reutilizando `RetentionIssuer.IssueAsync` + `ApplyRetention`; conservar la emisión posterior solo si se decide como excepción (p. ej. retención emitida días después dentro del plazo legal). Impacto: `ConfirmPurchaseUseCases`, `PurchasesPage`/`usePurchasesPage` (sección de retención), `IssueRetentionHandlerTests` (camino de Compras), `PurchasesControllerRetentionTests`, `usePurchasesPage.retention.test.ts`, E2E de devoluciones (orden retención/devolución).
- Implementado (fuera de Compras): `IssueRetentionHandler` ya no emite retenciones aisladas para Gastos — ese camino no tenía endpoint y creaba la retención y su asiento SIN aplicarla a la CxP (incoherente con la confirmación); ahora responde un rechazo explícito. Sus 21 tests de núcleo pasan a ejercitar `RetentionIssuer.IssueForExpenseAsync` (el que usa la confirmación).
- Tests nuevos (PostgreSQL real): doble emisión concurrente de retención sobre la misma compra (1 retención, CxP descontada una vez) y doble confirmación concurrente del mismo gasto con retención (1 gasto confirmado, 1 retención, 1 CxP con la retención una vez, 1 asiento de gasto y 1 de retención); unit: rechazo de la emisión aislada para Gastos (sin lecturas ni escrituras).
- Observaciones (sin cambio): la vista previa de Compras (`RetentionCalculator`) no evalúa si la empresa es agente de retención (la emisión sí lo rechaza); ninguna anulación verifica si la retención ya fue autorizada por el SRI; XML/RIDE/registro electrónico de la retención de Compras pasan por `/retentions/{id}`, cuyas lecturas exigen `perm:expenses.documents.view` (siguiente ticket: RIDE).

## ZH-ACCOUNTING-DATE-BOUNDARY-01 — Frontera de día UTC vs. día de empresa en contabilización (2026-10-01)

**Estado: COMPLETADO.** Sin cambios de reglas fiscales, montos, cuentas, posting rules, permisos ni UX; sin migración.
- Los 12 fallos (ejecución 2026-10-01 00:53 UTC, también sobre HEAD limpio): `SupplierCreditApplicationReversedPostingIntegrationTests` (2), `SupplierCreditAppliedPostingIntegrationTests` (2), `ApplySupplierCreditConcurrencyTests` (2), `CollectionPostingIntegrationTests` (2, reversa de cobro), `PurchaseReturnCancelledPostingIntegrationTests` (1), `PurchaseReturnAuthorizedPostingIntegrationTests` (3).
- Causa raíz (en los tests, no en producción): esos traductores no reciben fecha de documento y fechan el asiento con el "hoy" de la empresa (`ICompanyClock.TodayAsync`, America/Guayaquil — regla ADR-034); los tests sembraban el período y las fechas con `DateOnly.FromDateTime(DateTime.UtcNow)`. Entre 19:00 y 23:59 Ecuador del último día del mes, el día UTC ya es del mes siguiente: período de octubre sembrado, asiento con fecha 30-sep → `PERIOD_NOT_OPEN` → el traductor registra y omite (sin asiento). Producción ya cumplía ADR-034: auditados los 23 traductores (fecha de documento cuando existe, `ICompanyClock` cuando no; ninguno usa el día UTC) y los reversos (las 5 anulaciones usan `ReverseJournalEntryCommand`, que conserva el `EntryDate` original; la reversa de reembolso de crédito de proveedor usa la `EffectiveDate` de su propia transacción). Deuda idéntica latente (fin de año) en `PurchaseCreditNoteDiscountPostingIntegrationTests` (año UTC).
- Corrección: `CompanyClock` obtiene el instante de `TimeProvider` (BCL; producción `TimeProvider.System`, comportamiento idéntico). Los 7 tests afectados fijan el reloj en la frontera (`AccountingDateBoundary`: UTC 2026-10-01 00:30 = empresa 2026-09-30) y siembran desde el día de empresa — ejercitan la frontera en cada ejecución, a cualquier hora. Regla documentada en `docs/architecture/data-standards.md` (Fecha contable).
- Nuevos: `AccountingDateBoundaryIntegrationTests` (PostgreSQL real, mismo camino que los traductores: `ICompanyClock` → `PostingFact.EntryDate` → `PostingEngine`): día normal, UTC 1-oct/empresa 30-sep, 1-oct en ambos, UTC 1-ene/empresa 31-dic (año fiscal 2026), 1-ene en ambos, período de la empresa cerrado y período inexistente (`PERIOD_NOT_OPEN`, sin desviarse al mes UTC); asiento verificado desde otro DbContext (fecha, período, año fiscal, Posted, cuadrado).

## ZH-INVENTORY-STOCK-ITEM-LOOKUP-01 — Ajustes/Transferencias buscan solo ítems con stock desde la fuente (2026-09-30)

**Estado: COMPLETADO.** Cierra el pendiente "filtro `tracksStock` sobre la página devuelta" de ZH-PRODUCT-SELECTOR-SSOT-01.
- Reproducción (HTTP + PostgreSQL reales): 12 coincidencias sin control de stock que ordenan antes por SKU y una con stock después → `GET /api/v1/items?search=…&isActive=true&pageSize=12` devolvía 12 ítems sin stock (total 13); `StockItemPicker` filtraba esa página en React y mostraba "sin resultados" aunque el ítem válido existía (página 2).
- Contrato: `GET /api/v1/items` acepta el filtro opcional `tracksStock` (null = sin filtro, comportamiento previo; true/false = con/sin control de stock), atravesando `ItemsController` → `GetItemsQuery` → `ItemReportFilter` (parámetro opcional al final) → `ItemRepository.GetPageAsync`, aplicado con el resto de filtros antes de `OrderBy`/`Skip`/`Take` (el total también lo respeta). Scope por tenant, `isActive`, búsqueda SKU/nombre/descripción, orden por SKU y paginación sin cambios; `/items/report` no cambia. Sin migración ni endpoint nuevo.
- Frontend: `GetItemsParams.tracksStock` (la URL sin el parámetro queda idéntica), `useItemLookupSearch`/`ItemLookupPicker` aceptan `tracksStock` en lugar del predicado client-side `filter` (su único consumidor era Inventario) y `StockItemPicker` lo pide; se elimina el filtrado en React.
- Tests: 6 HTTP + PostgreSQL (`ItemLookupStockFilterHttpTests`: sin filtro = resultado actual, true, false, paginación/orden después del filtro, búsqueda por descripción + `isActive`, otro tenant); frontend: URL con/sin parámetro, hook envía/omite `tracksStock`, picker general sin cambios, `StockItemPicker` encuentra el ítem con stock detrás de una página completa de coincidencias sin stock.

## ZH-PRODUCT-SELECTOR-SSOT-01 — Una búsqueda manual de Item, pickers especializados donde corresponde (2026-09-30)

**Estado: COMPLETADO.** Cierra P2-03 de la auditoría UX. Solo frontend; sin cambios de backend, UX visual amplia ni Compras.
- Inventario (8 selecciones de Item): Compras `ProductPicker` (líneas, + perfil costo/PVP) y búsqueda global de `usePurchasesPage`; Inventario `AdjustmentProductPicker` y `TransferProductPicker`; Kardex (búsqueda inline); Precios `RemoteItemPicker` (excepciones de lista; el segundo "picker" de Precios es de clientes); Ventas/POS `SalesItemSearchResultsGrid`; resolución manual de recepción (`ResolvePendingProductsModal`, reutiliza `ProductPicker`). Las 6 selecciones manuales ya usaban el mismo endpoint (`GET /api/v1/items`: contiene sobre SKU/nombre/descripción, solo activos, orden por SKU) por la misma facade (`itemLookupFacade`); el backend no se toca.
- Duplicidad real (frontend): la mecánica de búsqueda (mínimo 2, debounce 300 ms, `isActive`, pageSize) copiada ×5 —Kardex sin debounce y ninguna con descarte de respuestas viejas salvo Precios— y dos componentes idénticos (Ajustes/Transferencias, solo cambiaba el perfil y los textos).
- Legítimos y separados: Ventas/POS (`GET /sales/item-search`: solo `IsForSale`, ranking barcode exacto → SKU exacto → parcial → nombre, stock por bodega, precio vía `IPricingResolver`, presentación por barcode) y el matching automático de recepción de Compras (código de proveedor / similitud trigram). Ningún picker resuelve precio ni existencias.
- Final: `items/facades/itemPickerFacade` (patrón `supplierPickerFacade`) con `useItemLookupSearch` (búsqueda canónica sobre `itemLookupFacade.search`, con descarte de respuestas viejas y estado de error) y `ItemLookupPicker` (DS `zh-picker` + `ZHPickerResultItem`, ↑/↓/Enter/Escape, cargando/sin resultados/error). Inventario: un `StockItemPicker` (regla `tracksStock` + perfil `StockItemProfile` con `baseUomCode`) para Ajustes y Transferencias; Precios usa `ItemLookupPicker`; Kardex usa el hook (conserva su marcado). Eliminados `AdjustmentProductPicker.tsx`, `TransferProductPicker.tsx` y `RemoteItemPicker`.
- Tests: hook (mínimo, debounce con una sola consulta, parámetros, regla del consumidor, respuesta vieja descartada, error, reset, enabled), picker (SKU/nombre, clic, teclado, Escape, elegibilidad, sin resultados, error, deshabilitado), `StockItemPicker` (solo stock + perfil); la página de ajustes sigue agregando líneas por el picker real.
- Pendiente fuera de alcance: Compras (CLOSED) conserva su `ProductPicker` y su búsqueda global con la misma mecánica local (adoptar `useItemLookupSearch` cuando se reabra); Kardex conserva el marcado `pf-picker-*` (deuda visual ya registrada en la auditoría); el filtro `tracksStock` sobre la página devuelta se cerró en ZH-INVENTORY-STOCK-ITEM-LOOKUP-01.

## ZH-COMPANY-IDENTITY-SSOT-01 — Identidad de empresa: dos contextos, una regla (2026-09-30)

**Estado: COMPLETADO.** Cierra P2-02 de la auditoría UX.
- Inventario: la identidad (RUC, razón social, nombre comercial, activo) se edita por `PUT /api/v1/companies/{id}` (`UpdateCompanyCommand`, usuario del tenant: `perm:erp.companies.update` + membership, empresa del tenant resuelto server-side; frontend `company-management`) y por `PUT /api/v1/admin-core/companies/{id}` (`UpdateCompanyForAdminCoreCommand`, Admin Global: policy `PlatformAdmin`, empresa explícita de cualquier tenant; modal del dashboard global). Configuración → Empresa la muestra de solo lectura (edita contacto/representante/regional con `UpdateContactProfile`). La activación por integración Platform (`ActivateIntegrationCompanyHandler`) es ciclo de vida, no edición de identidad.
- Resultado: **no es duplicidad de capacidad** — dos contextos de autorización legítimos (frontera tenant vs plataforma); se conservan ambos endpoints, comandos y pantallas. Sí había una regla con dos semánticas: el endpoint global no tenía validator (validaba a mano dentro de un helper estático del otro handler, construyendo el comando ajeno) y respondía **400** ante RUC inválido / razón social vacía (el operativo: 422 `VALIDATION_ERROR`) y **400** ante empresa inexistente (operativo: 404); el operativo validaba dos veces. Reproducido con HTTP + PostgreSQL reales (4 casos).
- SSOT: `CompanyIdentityRules` (validator interno incluido por ambos validators: formato + RUC oficial Ecuador) y `CompanyIdentityUpdate.ApplyAsync` (unicidad global del RUC → `Company.UpdateTaxIdentification`/`UpdateAdminIdentity`, auditoría `UpdatedBy`), llamada por ambos handlers tras resolver su alcance. Nuevo `UpdateCompanyForAdminCoreCommandValidator`; global inexistente → `NotFound`. Eliminado `UpdateCompanyHandler.UpdateEntityAsync`. Política de RUC sin cambios (mismo validador, misma unicidad, RUC vacío conserva el actual). Frontera documentada en `docs/architecture/backend.md`. Sin migración ni cambios de frontend.
- Tests: 20 HTTP + PostgreSQL (`CompanyIdentityUpdateHttpTests`): cada regla desde ambos endpoints con el mismo status/código (válido con normalización y `UpdatedBy`, RUC inválido ×2, razón social vacía, RUC duplicado de otro tenant, inexistente, RUC vacío), empresa de otro tenant por el operativo = 404 sin cambios, global sobre cualquier tenant, token de tenant en endpoint global = 403, token global en endpoint de tenant = rechazado. Application: mismas propiedades/mensajes desde ambos validators; global inexistente = NOT_FOUND.
- Observaciones (sin cambio, sin evidencia de una regla que decida): en la UI el RUC solo se cambia desde la consola global (en `company-management` el campo está deshabilitado en edición y se reenvía el actual), pero la API operativa acepta cambiarlo con el mismo validador y la misma unicidad — si el cambio de RUC debe ser exclusivo de plataforma es una decisión funcional pendiente; `Company` expone setters públicos y la activación por integración escribe `IsActive` directamente.

## ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — Un cliente HTTP por endpoint (2026-09-30)

**Estado: COMPLETADO.** Cierra P2-04 y P2-05 de la auditoría UX y el tipo duplicado de P2-06 (la decisión funcional de emisión de retenciones sigue abierta). Sin cambios de UX, CSS ni flujos; Compras no se toca funcionalmente.
- Inventario (script sobre `src/`, URLs resueltas contra constantes del archivo): 10 endpoints con más de un cliente y 8 archivos con HTTP fuera de `api/`. Duplicados: `GET /accounting/accounts` (accountingApi + cashRegisters + finance), `/catalog/{brands,category-nodes,sri-uom,sri-vat-rates,sri-ice-rates,barcode-types}` (catalogService/categoryNodeService + `ItemFormTabs` + `useItemCreationCatalogs` + `VariantsSection`), `/catalog/sri-supplier-types` y `/catalog/sri-id-types` (catalogService + masterData), `/settings/establishments/lookups` (establishments + emissionPoints), `/payment-methods` (paymentMethodService + salesService). HTTP inline además en `auth/{Forgot,Reset}PasswordPage` y `SetupPage`.
- Owners: accounting (`accountLookupFacade`), items/catalog (`brandService`, `categoryNodeService`, `sriLookupService`/`sriLookupFacade`, nuevo `barcodeTypeService` — el endpoint no tenía cliente canónico), establishments (nueva `establishmentLookupFacade`), sales (`paymentMethodService`), auth (`authService.forgotPassword/resetPassword`, nuevo `setupService`).
- Consumidores migrados sin cambiar requests (mismo endpoint y parámetros efectivos; `category-nodes` ahora envía `includeInactive=true`, que es el default del backend) ni manejo de error. `ItemFormTabs` usa `useItemCreationCatalogs` (carga única + categorías hoja, antes normalizadas dos veces).
- Tipos unificados: `PaymentMethodDto`/`PaymentMethodDetailType` (salesService tenía una copia parcial), `EstablishmentLookupDto`, `RetentionDocumentDto`/`RetentionDocumentLineDto`/`RetentionTaxType`/`RetentionStatus` (expenses → `retentions/facades/retentionDocumentFacade`), `SriSupplierTypeOption`/`SriIdTypeOption` (alias del lookup canónico), `AccountDto` en lugar de tipos ad hoc.
- Guard `frontend-http-access` (`F-http-outside-api`, `F-http-stale-exception`), sin grandfather; reemplaza la sub-regla textual `F-cross-layer-module-page` (solo fetch/axios en pages) de `no-cross-layer`. Excepciones de infraestructura con motivo: `hooks/useAuthenticatedImage` y el service de diagnóstico de `components/zh/electronicDocuments` (ADR-024). Tests del guard: 15 (fixtures PASS/FAIL, repo real 0, estado previo al ticket = exactamente los 8 archivos). Tests por capacidad: 7 archivos nuevos (endpoint, params, delegación de facade, propagación de error, ausencia de los clientes eliminados).
- Pendiente fuera de alcance: no hay guard de "dos services `api/` para el mismo endpoint" (URLs compuestas no resolubles sin falsos positivos).

## ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01 — Factura con devolución autorizada no se anula (2026-09-30)

**Estado: COMPLETADO.** Cierra el hallazgo P1 de ZH-SALES-RETURN-INVOICE-STATE-CONCURRENCY-01.
- Reproducción antes del fix (HTTP + PostgreSQL reales, 2 unidades vendidas): con devolución autorizada, anular respondía 200 y revertía la factura completa encima de la devolución — parcial 1/2: Kardex reingresado 1 + 2 = 3 (vendido 2); total 2/2: 2 + 2 = 4; asientos de la factura reversados completos además del asiento de la devolución; reembolso en efectivo o crédito a CxC conservado; Nota de Crédito emitida sobre una factura anulada.
- Regla recuperada (no inventada): Compras ya la define — PI-CANC-01 "No se puede anular una compra que tiene una devolución de compra autorizada asociada. Cancele primero la devolución."; en Ventas una devolución autorizada es terminal (`SalesReturn.Cancel()` solo desde Draft) y la anulación siempre revierte la factura completa (no hay anulación neta de devoluciones). Política A: devolución `Authorized` bloquea la anulación; `Draft`/`Cancelled` no bloquean (sin efectos; el Draft queda inautorizable por la regla del ticket anterior).
- SSOT: `SalesInvoiceCancellationPolicy` (Domain, "La factura tiene una devolución autorizada y no puede anularse." → 422 `DOMAIN_RULE_VIOLATION`) + `ISalesReturnRepository.ExistsAuthorizedBySalesInvoiceIdAsync` (alcance tenant+empresa), evaluada en `CancelSalesInvoiceHandler` bajo el lock de la factura y antes de cualquier efecto. Sin nuevo lock, sin migración, sin cambios de frontend. Camino único documentado en `docs/architecture/backend.md`.
- Tests: 12 HTTP + PostgreSQL (`SalesCancelAuthorizedReturnRuleTests`): parcial/total × efectivo/crédito CxC (422 sin efectos, Kardex exacto), sin devoluciones, Draft (no bloquea, queda inautorizable y cancelable), Draft cancelado + autorizada, autorizar primero con lock forzado (×3), simultáneos ×8, doble anulación ×5, otra empresa. `SalesReturnInvoiceStateConcurrencyTests` actualizado: la anulación que espera a una devolución autorizada ahora se rechaza.
- UX opcional no implementada: "Anular" sigue visible en toda factura autorizada (el DTO no expone devoluciones); el servidor rechaza con mensaje claro.

## ZH-SALES-RETURN-INVOICE-STATE-CONCURRENCY-01 — Devolución solo sobre factura autorizada, bajo lock (2026-09-30)

**Estado: COMPLETADO.** Cierra el pendiente de ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01.
- Regla vigente recuperada del código (no inventada): una devolución solo procede sobre una factura `Authorized` (`CreateSalesReturnDraft`: "Solo se pueden devolver facturas autorizadas."; `SalesInvoiceStatus` = Draft/Authorized/Cancelled, sin estados adicionales). El handler de autorización ya declaraba que la validación del Draft es solo preventiva y revalidaba el remanente bajo lock, pero no el estado de la factura.
- Reproducción antes del fix (HTTP + PostgreSQL reales): Draft → anular factura → autorizar con reembolso en efectivo = 200: devolución `Authorized` sobre factura `Cancelled` con reingreso de Kardex, `CashMovement` SaleRefund, asiento, outbox y secuencial de Nota de Crédito consumido. Con crédito a CxC se rechazaba solo de forma incidental (CxC anulada). Simultáneos: 200 + 200 con la devolución autorizada después de la anulación.
- Fix: la regla pasa a una sola definición (`SalesReturnInvoiceEligibility`) usada por el Draft y por la autorización; la autorización bloquea la factura (`GetByIdForUpdateAsync`, después del advisory de la devolución y antes de CxC/secuencias) y la revalida antes de cualquier efecto, dentro de la transacción existente. Rechazo = mismo resultado canónico que el Draft (422 `VALIDATION_ERROR`). Sin migración, sin advisory nuevo, sin cambios de frontend.
- Doble autorización de la misma devolución: ya protegida por el `xmin` de `SalesReturn` (una 200, otra 409 `CONCURRENCY_CONFLICT`, un solo conjunto de efectos) — demostrado, sin defensa nueva.
- Tests: 9 HTTP + PostgreSQL (`SalesReturnInvoiceStateConcurrencyTests`): anular→autorizar (efectivo y crédito a CxC), orden forzado con el lock de la factura retenido (autorizar primero / anular primero, ×3), simultáneos ×8 (efectivo y crédito), doble autorización ×5, otra empresa. Estado final desde otro DbContext (Kardex, reembolso, CxC, asiento, outbox, Nota de Crédito).
- **Hallazgo P1 (cerrado en ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01):** `CancelSalesInvoice` no consideraba devoluciones autorizadas. Anular una factura con una devolución ya autorizada procede y reingresa al Kardex y reversa en contabilidad la factura completa, además de lo que ya reingresó/reembolsó la devolución (reproducido: factura `Cancelled` + devolución `Authorized` con su reembolso y su Nota de Crédito). Ticket propio.

## ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01 — Anulación de factura vs cobros concurrentes (2026-09-30)

**Estado: COMPLETADO.** Cierra el pendiente de ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01.
- Regla vigente preservada (no se inventó ninguna): una factura solo se anula si su CxC no tiene cobros registrados (`SalesReceivable.Cancel`: `PaidAmount > 0` → rechazo); una CxC anulada no admite cobros ni créditos; tras reversar el cobro la anulación vuelve a ser posible. La anulación nunca reversa cobros automáticamente.
- Reproducción antes del fix (HTTP + PostgreSQL reales, factura a crédito autorizada por el flujo oficial): anular + cobrar a la vez terminó en factura `Cancelled` + CxC `cancelled` con `PaidAmount` 50 y el cobro aplicado y contabilizado (Kardex y asientos de la factura reversados) — 1 de cada 3–4 rondas. Dos anulaciones simultáneas: la perdedora respondía 500 `INTERNAL_ERROR`. Causa: `CancelSalesInvoiceHandler` leía factura y CxC sin lock ni transacción explícita.
- Fix: transacción explícita; `SalesInvoice` FOR UPDATE (`ISalesInvoiceRepository.GetByIdForUpdateAsync`, tenant+empresa) → CxC FOR UPDATE (`GetByInvoiceIdForUpdateAsync`) → validar → CxC, Kardex, reverso contable y outbox → commit. Orden global de locks ampliado en `docs/architecture/backend.md`. Sin migración, sin advisory nuevo, sin cambios de frontend.
- Tests: 11 HTTP + PostgreSQL (`SalesCancelCollectionConcurrencyTests`): anulación→cobro, cobro→anulación, simultáneos ×8, orden forzado con el lock de la CxC retenido desde otra conexión (anulación primero / cobro primero, ×3 c/u), replay idempotente, reversa + anulación ×8, dos anulaciones ×8 (Kardex y asientos reversados una sola vez, perdedora 422), devolución con crédito + anulación ×8, factura sin cobros, otra empresa (fail-closed). Estado final leído desde otro DbContext.
- Pendiente fuera de alcance: autorizar una devolución no revalida que la factura siga autorizada (con reembolso en efectivo se puede autorizar sobre una factura ya anulada; hallazgo por lectura de código, no reproducido) — ticket propio.

## ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01 — Cobros distintos concurrentes sobre la misma CxC (2026-09-30)

**Estado: COMPLETADO.** Cierra el pendiente de ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 (actualización perdida de `SalesReceivable`).
- Reproducción antes del fix (HTTP + PostgreSQL reales, 2 corridas idénticas): 60+40 sobre 100 → ambos 201, 2 cobros, 2 asientos, 2 outbox, pero la CxC quedó con pagado 60 / saldo 40; 80+80 → ambos 201, 160 cobrado y posteado, CxC pagado 80 / saldo 20. Causa: lectura de `PaidAmount` sin lock ni token de concurrencia, regla de dominio evaluada contra saldo obsoleto y UPDATE absoluto que pisa el incremento concurrente.
- Fix: patrón oficial `SELECT … FOR UPDATE` → lectura acotada → `Reload` dentro de transacción explícita, **antes** de decidir si el monto cabe. `ISalesReceivableRepository.GetByIdsForUpdateAsync` (orden ascendente por Id, alcance tenant+empresa, fail-closed) y `GetByInvoiceIdForUpdateAsync`; `IPaymentRepository.GetByIdForUpdateAsync`. Escritores cubiertos: registrar cobro, reversar cobro (lock pago → CxC) y crédito de devolución de venta. Idempotencia intacta (camino rápido, replay del ganador, la clave del perdedor no se consume). Sin migración, sin advisory lock nuevo, sin cambios de frontend. Orden de locks documentado en `docs/architecture/backend.md`.
- Tests: 8 HTTP + PostgreSQL (`ReceivableCollectionConcurrencyTests`): 60+40 (saldo 0), 80+80 (uno 201, otro 422 `DOMAIN_RULE_VIOLATION`, saldo 20), 7×20 sobre 100 (exactamente 5), misma clave concurrente (1 cobro), perdedor reintentado con su clave (reevaluado; luego la misma clave registra 20), rechazo bajo lock no deja lock residual, destino caja (sin `CashMovement`), CxC de otra empresa (404 sin efectos). Verificación desde otro DbContext: Σ aplicaciones ≤ original y pagado = Σ aplicaciones.
- Pendiente fuera de alcance: anular factura (`CancelSalesInvoice` → `SalesReceivable.Cancel`) lee la CxC sin lock ni transacción explícita; un cobro concurrente con la anulación puede dejar una CxC anulada con pagos — ticket propio.

## ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — Una intención financiera = como máximo un efecto (2026-09-30)

**Estado: COMPLETADO.** Cierra P1-01 de la auditoría UX. Pago a proveedor, cobro de CxC y movimiento manual de caja son idempotentes por `ClientRequestId` con barrera en PostgreSQL.
- Reproducción antes del fix (HTTP + PostgreSQL reales): pago sin comprobante ×2 → 2 pagos, cuota 200, 2 `CashMovement`, 2 asientos, 2 outbox (secuencial y concurrente); cobro ×2 → 2 cobros y 2 asientos (concurrente: la CxC registró solo 100 — actualización perdida); movimiento manual ×2 secuencial → 2 movimientos. Barreras parciales previas: `receipt_number` único (respondía 409 en vez del original) y versión de fila de la sesión (409 `CONCURRENCY_CONFLICT`).
- Patrón reutilizado de `CashFundingRequest`/`SupplierCreditMovement`: `ClientRequestId` + huella SHA-256 canónica + índice UNIQUE `(tenant_id, client_request_id)`. Mismo id + misma huella → el documento original (201, sin nuevos efectos); huella distinta → 409 `CONFLICT`; un rechazo no consume la clave. Si el intento falla por cualquier motivo y la clave ya existe (reintento concurrente ganador), responde el ganador.
- Dominio: `ClientRequestKey` + `BindClientRequest` en `SupplierPayment`, `Payment` y `CashMovement`. Application: `CanonicalRequestFingerprint` (extraído sin cambios de `CashFundingPaymentSnapshot`, fijado con test golden), huellas V1 de cobro y movimiento; el movimiento manual ahora bloquea la sesión `FOR UPDATE` (movimientos concurrentes distintos ya no fallan con 409). Migración aditiva `FinancialCommandIdempotency` (columnas anulables + 3 índices UNIQUE parciales).
- Frontend: `useClientRequestId` (mismo id mientras el payload no cambie, incluso al reabrir el modal; se libera tras el éxito) en pago, cobro y movimiento; caja con guarda síncrona antes de la confirmación.
- Convención documentada en `docs/architecture/backend.md`. Tests: 22 HTTP (`FinancialCommandIdempotencyTests`, conteo físico de documento, aplicaciones, caja, asientos, outbox, saldos), UI de doble clic/reintento en los 3 flujos.
- Pendiente fuera de alcance: dos cobros DISTINTOS concurrentes sobre la misma CxC siguen pudiendo perder una actualización de `SalesReceivable` (sin lock ni token de concurrencia) — ticket propio.

## ZH-FRONTEND-UX-SSOT-AUDIT-01 — Auditoría UX SSOT del frontend (2026-09-29)

**Estado: AUDITORÍA COMPLETADA (sin cambios de código).** Hallazgos en [`docs/architecture/UX-SSOT-AUDIT.md`](docs/architecture/UX-SSOT-AUDIT.md): 1 P1 candidato, 7 P2, 9 P3, 8 P4.
- **P1-01 (a verificar con reproducción):** pago a proveedor, cobro de CxC y movimiento manual de caja son comandos de creación sin `ClientRequestId`/idempotencia de servidor; la defensa es solo estado de UI.
- P2 principales: vínculo NC↔devolución de compra con dos endpoints; identidad de empresa con dos comandos; ≥5 pickers de producto; HTTP crudo a `/accounting/accounts` saltando `accountLookupFacade`; catálogos de items con varios clientes; dos momentos de emisión de retención; dos pipelines de RIDE.
- Sin duplicidad: confirmaciones (un visual), modales, feedback `message.*`, rutas (alias = redirects), flujos de caja/CxP/CxC/inventario/ventas.

## ZH-FRONTEND-CROSS-MODULE-CSS-01 — Sin CSS privado entre módulos (2026-09-29)

**Estado: COMPLETADO.** 0 imports CSS entre `modules/`; guard `F-subscriber-css-import` en `frontend-subscriber-naming` (sin grandfather). Sin cambios de lógica, APIs ni rediseño: 49 reglas movidas con declaraciones idénticas (verificado HEAD vs ahora, incluidos media queries).
- purchases → `sales-return.css` (3 páginas): `sr-list-filters`, `sr-general-grid*`, `sr-reason-readonly`, `sr-draft-actions` eran patrones genéricos (B) que además caja, finance y el editor de líneas de compra usaban **sin importar la hoja** (D: solo tenían estilo si el chunk de Ventas ya estaba cargado), y `pcn-summary-grid*` era una copia declarada de `sr-general-grid*`. Pasan al DS: `ZHFilterBar plain`, `.zh-summary-grid*`, `.zh-readonly-text`, `.zh-inline-empty` (zh-ui.css §31), `.zh-form-actions-row--flush`, y `sr-lines-table__error` → `.zh-field-hint--error` existente. `sales-return.css` queda solo con clases de Ventas; `pcn-summary-grid` eliminada.
- admin-core → `LoginPage.css`: la pantalla ya usaba `zh-auth-*` del DS; solo tomaba `lp-brand*` y `lp-error`, compartidos con /login y /setup → `.zh-auth-logo*` y `.zh-auth-error` en zh-ui.css §20 (variante horizontal de `.zh-auth-brand`, documentada; unificar ambas es decisión visual pendiente). `lp-brand-desc` (solo /login) sigue local.
- SalesPage → `electronic-documents-monitor.css`: lo importaba solo para vestir `components/zh/electronicDocuments/*` (ADR-024). 25 clases de esos componentes pasan a `components/zh/electronicDocuments/electronic-documents.css` como `zh-edoc-*`, importado por cada componente; el Monitor conserva solo `dashboard/retry/last-message/access-key/detail-grid`.
- Diferencias mínimas por adoptar el DS existente (no rediseño): error inline de línea con margen 4px (antes 2px) y `line-height` de ayuda; fila de acciones con `align-items: center` y `flex-wrap`.
- Tests: checker 16/16 (PASS: CSS propio, `src/styles/`, `src/components/zh/`, asset no-estilo; FAIL: css/scss de otro módulo; rutas Windows/POSIX); contra `HEAD` reporta exactamente los 5 imports. Frontend completo 2566/2566; `tsc -b`, lint (0 errores), build, `architecture:check` (58 tests, 0 nuevas, baseline 0) y `git diff --check` en verde.

## ZH-GEOGRAPHY-COUNTRY-CONTEXT-01 — País de la dirección INEC de socios de negocio (2026-09-29)

**Estado: COMPLETADO (Caso B: restricción deliberada del dominio, centralizada).** Sin cambios de backend, endpoints, UX ni CSS.
- `countryId` de `provinces()` = ISO-3166 alpha-2 (`GetGeoCountries` expone `SriCountry.Iso2`; filtra `global.geo_provinces.country_id`). El catálogo `geo_*` solo tiene la DPA INEC de Ecuador (25 provincias `EC`).
- SSOT: `PhysicalAddress` (dominio MasterData) es "dirección física con codificación geográfica INEC Ecuador", sin campo país y con FK a `geo_*`. Descartadas como fuente: `BusinessPartner.countryCode` (país del socio, no de la ubicación — un socio extranjero puede tener sede con dirección INEC), `Company.CountryCode` (`ECU`, alpha-3 SRI: otro sistema y otro concepto); no existe configuración operativa de país de direcciones.
- `"EC"` de `MasterDataBusinessPartnerDetailPage` → `masterData/constants/physicalAddressGeography.ts` (`PHYSICAL_ADDRESS_GEO_COUNTRY_ID`, documentado con la restricción). Flujo Provincia → Cantón → Parroquia sin selector de país (la dirección no tiene país); la cascada existente ya limpia hijos. Restricción documentada en `docs/architecture/architecture.md` (catálogos globales).
- Tests: provincias con la fuente central aunque el socio sea `US`, recarga de cantones/parroquias al editar, cambio de provincia limpia cantón/parroquia y descarta opciones previas; guard: ningún consumidor pasa un país literal a `provinces()`.
- Fuera de alcance (otro concepto, reportado): `"EC"` como default/fallback de `BusinessPartner.countryCode` (`MasterDataPartnerWizard`, `MasterDataBpFormFields`, fila "País" del detalle), país de banco (`BanksPage`, `Bank.DefaultCountryCode`) y país inicial de sucursal (`useBranchesPage`).
- Frontend completo 2566/2566; `tsc -b`, lint (0 errores), build, `architecture:check` (0 nuevas, baseline 0) y `git diff --check` en verde.

## ZH-FRONTEND-GEOGRAPHY-SSOT-01 — Un solo cliente HTTP de geografía (2026-09-29)

**Estado: COMPLETADO.** Toda consulta geográfica del frontend pasa por `branches/facades/geographyLookupFacade` → `branchService` → `GET /api/v1/settings/geography/{countries|provinces|cantons|parishes}`. Sin cambios de backend, endpoints, parámetros, UX ni CSS.
- Owner: Branches — los casos de uso `GetGeoCountries/Provinces/Cantons/Parishes` y `GeographyItemDto` viven en `ERP.Application/Modules/Branches`; Settings solo aporta ruta/permiso/menú (`settings.geography.view`, página consumidora) y masterData es consumidor (direcciones de socios).
- Eliminado `masterData/api/geographyService.ts` (cliente duplicado de provincias/cantones/parroquias) y su tipo `GeoOption`; tipo único `GeographyItemDto { id, name }`. El normalizador canónico (branches) es superconjunto del eliminado: acepta además tuplas `[id, name]` y tolera filas `null` (el anterior lanzaba `TypeError`); el backend devuelve `{ id, name }`, así que el contrato vigente no cambia.
- `MasterDataBusinessPartnerDetailPage` (+ test) migrada a la facade; `settings/GeographyPage` ya la usaba.
- Tests: contrato del cliente (4 endpoints, parámetros codificados, DTO/normalización, propagación de errores, facade = mismas funciones) y guard `geographyClientSingleSource.test.ts`: el endpoint de geografía solo puede aparecer en `branches/api/branchService.ts`.
- Frontend completo 2562/2562; `tsc -b`, lint (0 errores), build, `architecture:check` (0 nuevas, baseline 0) y `git diff --check` en verde.

## ZH-FRONTEND-SUBSCRIBER-NAMING-RESTORE-01 — Contrato owner → subscriber entre módulos frontend (2026-09-29)

**Estado: COMPLETADO.** `frontend-subscriber-naming` reactivado en `architecture:check` como guard de contratos públicos: 0 violaciones, baseline 0, grandfather 0, sin excepciones. Sin cambios de backend, API, payload, permisos ni UX.
- Por qué estaba deshabilitado: validaba naming `Tenant → Subscriber` en `frontend/src`, con polaridad invertida respecto a `naming.md` (`Subscriber` retirado, `Tenant` canónico) → 278 falsos positivos. Reescrito: `F-subscriber-internal-import` (cross-módulo solo hacia archivos directos de `modules/<owner>/**/facades/`, incluidos `import type`/`import()`) y `F-subscriber-facade-naming` (`<concepto><Propósito>Facade.ts`, objeto exportado con el mismo nombre). 14 tests con fixtures POSIX/Windows.
- Deuda real corregida (88 imports de código; el checker contra `HEAD` reporta exactamente esos 88): masterData ×46, inventory ×17, auth/session/company-management ×17 (admin-core), branches ×7, caja ×3, sales/purchases (reportes, cashRegisters) ×4, items ×2, payables ×1, más `modules/lib` → auth (test). Categoría C: `masterData/api/businessPartnerFacade` (facade interna usada como contrato) y `inventory|branches|caja/types.ts` (segunda superficie pública paralela a las facades, eliminadas).
- Facades extendidas: `businessPartnerLookupFacade` (+locations/contacts/salesSettings), `paymentTermLookupFacade` (+getById), `salesLookupFacade` (+dailyReport), `purchaseLookupFacade` (+supplierReport). Nuevas (mínimas): `masterData/businessPartnerRegistrationFacade`, `masterData/supplierPickerFacade`, `inventory/stockLookupFacade`, `branches/geographyLookupFacade`, `caja/manualCashMovementFacade`, `items/itemTypeLookupFacade`, `items/itemDetailNavigationFacade` (reemplaza `useViewMatchedItem`, que tocaba el store privado de items), `sales/customerPickerFacade`, `auth/globalAdminAuthFacade`, `company-management/companyRegistrationFacade`.
- Utilidad duplicada sin dominio: `ride/utils/downloadBlob` + `electronicDocuments/monitor/utils/download` → única `src/lib/download.ts`.
- `module-boundaries` queda solo con pares prohibidos (`forbiddenCrossImports`); se retiró su rama api/pages + `allowedCrossImports` (subconjunto de esta regla y lista blanca por pares).
- Pendiente fuera de alcance (estilos, ola DS/visual): 5 imports CSS cross-módulo — `sales/styles/sales-return.css` en 3 páginas de purchases, `auth/pages/LoginPage.css` en `AdminCoreLoginPage`, `electronic-documents-monitor.css` en `SalesPage` (lo requieren componentes `components/zh/electronicDocuments/*` por `.edm-hint-sm`). Registrado también: geografía con dos clientes HTTP (`branchService` y `masterData/api/geographyService`) — resuelto en ZH-FRONTEND-GEOGRAPHY-SSOT-01.
- Frontend completo 2552/2552; `tsc -b`, lint (0 errores), build y `architecture:check` (56 tests + 20 checks) en verde.

## ZH-DOMAIN-RULE-ERROR-SSOT-01B — Validación de atomicidad, pipeline y compatibilidad (2026-09-29)

**Estado: COMPLETADO.** Verificación del cambio masivo ZH-DOMAIN-RULE-ERROR-SSOT-01 antes de cerrarlo.
- Pipeline real (probado en la API con trazas externas/internas, PostgreSQL): `Validation → CompanyScope → BranchScope → DomainRule → Caching → handler`. No existe TransactionBehavior: la transacción vive en el handler (`catch { Rollback; throw; }`), así que la regla atraviesa la transacción, se hace rollback (`HasActiveTransaction=false`) y recién después DomainRuleBehavior la convierte. Nada persiste (ni la escritura flusheada dentro de la transacción ni su outbox).
- Atomicidad (144 sitios auditados sobre HEAD): A 105 (regla antes de efectos o un único SaveChanges), B 24 (efectos dentro de transacción con rollback), 15 candidatos revisados a mano → todos A/B salvo uno real: `UploadImportFile` escribía el Excel en el almacenamiento antes de que el lote rechazara el adjunto (archivo huérfano, preexistente) → compensación best-effort + relanzar la regla. **C = 0.**
- Cache: un rechazo por regla nunca se cachea y la ejecución válida siguiente vuelve al handler (probado con una query ICacheable real).
- Sin traducción paralela: se eliminaron las 4 traducciones locales regla → texto/código que quedaban (`ExpenseDraftRules.BuildLinesAsync`, `CancelSalesInvoiceHandler` y `AuthorizePurchaseCreditNoteHandler` relanzan con el mismo mensaje compuesto, `SalesReturnRefundHandler` deja subir la regla). Callers fuera de MediatR (`UpdateCompanyForAdminCore`, `CreateExpenseDraftFromReception`, `SalesReturnRefundHandler`) corren dentro de un handler MediatR.
- Compatibilidad: ningún consumidor (frontend, interceptors, formularios, importación, Compras, Retenciones, Items, POS) decide por `VALIDATION_ERROR`/`DOMAIN_RULE_VIOLATION`; el frontend trata 422 con `data.errors` lista como error de formulario (igual que antes). Único mapeo por código en backend (descarga XML en lote) mantiene su estado "Error". Los 5 casos 400→422: `CreateCompany`, `Enable/DisableEmissionPoint`, `Enable/DisableEstablishment` (estado de negocio rechazado → 422 correcto).
- Módulos CLOSED (Purchases, ElectronicDocuments, Items, Caja, Retentions, Expenses, Sales): diff sin espacios = solo plumbing de error (tipo de excepción, catch, rollback, relanzado); mensajes públicos idénticos; sin cambios en cálculos, estados, posting, permisos, endpoints, DTOs ni UX.
- Evidencia: `DomainRulePipelineIntegrationTests` (orden efectivo + rollback + outbox + cache, PostgreSQL) · `ItemDomainRuleContractsTests` (409/404 deliberados) · cancelación con mensaje compuesto · compensación de archivo · Domain 1221 · Application 2441 · Architecture 124 · API 784/784 · Infrastructure 1003/1003 · `architecture:check` 0 nuevas (baseline 0, grandfather 0).

## ZH-DOMAIN-RULE-ERROR-SSOT-01 — Un solo mecanismo para las reglas de negocio (2026-09-29)

**Estado: COMPLETADO.** `InvalidOperationException` significaba a la vez "regla de negocio" (422 con su texto) y "error interno" (500): 247 `throw` en Domain, 20 en Application, 130 `catch (InvalidOperationException)` + 11 filtros `ArgumentException or InvalidOperationException` que convertían *cualquier* IOE (incluidas las del framework: `Single()`, EF…) en `ValidationFailure(ex.Message)`. Detalle normativo: [`docs/architecture/error-handling.md § Reglas de negocio`](docs/architecture/error-handling.md).
- Mecanismo (no existía ninguno: sin `DomainException` previa): `DomainRuleViolationException` (Domain, deriva de `Exception`) → `DomainRuleBehavior` (MediatR, constraint type-safe `IDomainRuleResult<TSelf>` con miembro estático abstracto; registrado antes de Caching) → `Result<T>.FromDomainRule` → `DOMAIN_RULE_VIOLATION` 422; fuera de MediatR, `ExceptionMiddleware` con el mismo código y mensaje. `InvalidOperationException` → siempre 500 sin texto (se retiró la regla "por origen" del ticket anterior). `SystemSeededRecordException`, `DocumentFlowPolicyViolationException` y los 6 `*PostingFailedException` ahora derivan del tipo semántico.
- Domain: 238 `throw` migrados con su mensaje intacto (A); 9 quedan técnicos con prefijo `Invariante violada:` (B: secuencias `CurrentSeq`, saldo de `SupplierCredit`, catálogo de definiciones, `StockMovement` interno). Application: 11 migrados (A), 9 técnicos (B: validador de menú, snapshot de pago, invariantes de XmlBuilders, resolvers de estrategia). Infrastructure alcanzable: lectores Excel (sin el texto interno de ClosedXML), `ItemRepository` "Ítem no encontrado.", certificado SRI no vigente/sin clave RSA.
- Handlers: 80 catch eliminados (el behavior produce el mismo Result), 33 rollback+rethrow → redundantes con el `catch { rollback; throw; }` existente y eliminados, 11 filtros → `catch (ArgumentException)` (+ rollback/`FromDomainRule` donde aplica). Quedan 21 catch tipados con contrato explícito: servicios que devuelven Result a su llamador (`FromDomainRule`), 409 Conflict (secuencia, código de barras, variante), 404, `PERIOD_NOT_OPEN`/`ValidationFailedCode`, texto compuesto, importadores.
- Cambio observable: un rechazo por regla de negocio ahora responde `code: DOMAIN_RULE_VIOLATION` (antes `VALIDATION_ERROR`; 5 casos `Failure(ex.Message)` pasan de 400 a 422); mismo mensaje y mismo 422. Una IOE técnica que antes salía 422 con texto (framework, `DocumentSequenceRepository` cross-tenant, invariantes) ahora es 500 sanitizado.
- Importadores: fila/archivo inválido desde el error semántico; error técnico → "Error interno al confirmar la fila…"/"No se pudo leer el archivo." con detalle en log (antes el texto crudo quedaba en la incidencia/lote). Jobs (`catch (Exception)`) sin cambios de comportamiento.
- Residuales documentados: parsers de XML externo (Purchases/Ride, CLOSED) capturan `FormatException/ArgumentException/InvalidOperationException` como "XML mal formado" (frontera declarada en el guard); `UploadImportFile` expone `ex.Message` del almacenamiento de archivos (no es IOE); `ArgumentException` de Domain se sigue traduciendo localmente a `ValidationFailure` (validación de entrada, fuera de alcance).
- Evidencia: `DomainRuleErrorSemanticsTests` (Architecture, 3 reglas, sin grandfather, verificadas por mutación) · `DomainRuleBehaviorTests` (traducción, IOE atraviesa, contenedor real omite respuestas no-Result, orden vs Caching) · `ImportErrorSemanticsTests` · `ExceptionClassificationTests` (regla → 422 con mensaje, invariante de dominio/framework → 500 sin texto) · HTTP real `PATCH …/business-partners/{id}/activate` → 422 `DOMAIN_RULE_VIOLATION` · Domain 1221 · Application 2436 · Architecture 124 · API 782/782 · Infrastructure 1003/1003 · `architecture:check` 0 nuevas (baseline 0, grandfather 0, 62/100).

## ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01 — Timing de login, InvalidOperationException y fallos de BD (2026-09-29)

**Estado: COMPLETADO.** Cierra las brechas pendientes tras unificar Error → HTTP y la semántica de scope. Sin cambios de lógica funcional.
- **Timing de login**: el usuario inexistente no ejecutaba BCrypt (≈0 ms vs ≈250 ms de una contraseña incorrecta → enumeración por tiempo). Nuevo `IPasswordHasher.SimulatePasswordVerification`: verifica contra un hash ficticio con el mismo algoritmo y costo (BCrypt, `WorkFactor = 12`, constante única); 01B: hash BCrypt ficticio **constante precomputado** (`BcryptPasswordHasher.DummyVerificationHash`, `$2a$12$`, no es secreto ni usuario real) — cada login inexistente ejecuta exactamente 1 `VerifyPassword` y ningún `HashPassword`. `LoginHandler` y `GlobalLoginHandler` lo usan; mismo 401/cuerpo; "Usuario inactivo." solo tras contraseña correcta; rate limit sin cambios.
- **InvalidOperationException**: 324 `throw` auditados. Domain (≈250) y Application: reglas de negocio con mensaje curado — algunos invariantes internos con texto no sensible (resolvers de estrategias, "Invariante violada" en XmlBuilders, validador de menú). Infrastructure: estado interno con texto técnico (`[TenantGuard] <Entidad>`, Snowflake, InstallData, seeds); los alcanzables desde un request (lectores Excel, `ItemRepository`) ya los capturan los handlers. El framework además lanza IOE por defectos (`Single()` vacío, `Nullable.Value`, tracking de EF) que salían 422 con su texto. Ahora `ExceptionMiddleware` decide por el **origen del `throw`**: ERP.Domain/ERP.Application → 422 `DOMAIN_RULE_VIOLATION` con mensaje; cualquier otro → 500 `INTERNAL_ERROR` sin texto. `ArgumentException` conserva 400 pero solo expone mensajes de esas capas.
- **Base de datos**: todo `DbUpdateException` salía 503 (incluso una violación UNIQUE) y un `DbException` crudo salía 500. Único punto de clasificación: `IDatabaseExceptionTranslator.ClassifyFailureCode` (en `PostgresDatabaseExceptionTranslator`, junto al `TryGetUniqueViolation` existente): conexión/timeout/servidor no disponible (Npgsql transitorio, SQLSTATE 08/53/57P01-03/57014, reintentos agotados) → 503 `DATABASE_UNAVAILABLE`; 23505 → 409 `UNIQUE_VIOLATION`; resto de clase 23 → 409 `CONFLICT`; concurrencia optimista/40001/40P01 → 409 `CONCURRENCY_CONFLICT`; SQL inesperado o sin causa identificable → 500 `INTERNAL_ERROR`. Las excepciones semánticas (p. ej. `CompanyRucAlreadyExists`) se clasifican antes.
- **Detalle técnico**: ningún 500/503 (ni 409 de BD) expone SQL, connection string, host/puerto, stack, inner exception, tabla o constraint — verificado en Production y Development; el detalle completo queda solo en el log de `ExceptionMiddleware`.
- Residual documentado: los handlers que capturan `InvalidOperationException` y devuelven `ValidationFailure(ex.Message)` (≈120) no distinguen origen; tocarlos es lógica de módulo (Purchases/ElectronicDocuments CLOSED).
- Evidencia: `ExceptionClassificationTests` (IOE por origen incl. await real, ArgumentException, 14 escenarios de BD × 2 entornos sin detalle) · `PostgresDatabaseExceptionClassificationTests` · `BcryptPasswordHasherTests` (mediana simulación/verificación real en 0,4–2,5, costo 12) · `LoginHandlerTests`/`GlobalLoginHandlerTests` (una verificación por camino, respuesta idéntica) · Domain 1221 · Application 2423 · Architecture 121 · API 780/780 · Infrastructure relevante 471/471 · `architecture:check` 0 nuevas (62/100).

## ZH-SCOPE-ERROR-SEMANTICS-01 — La semántica de scope viaja por `Code`, nunca por el texto (2026-09-29)

**Estado: COMPLETADO.** `CompanyScopeBehavior` decidía `NoCompanyContext` vs `AccessDenied` con `ctx.Error?.Contains("empresa operativa")` (único caso en todo el backend) y 19 consumidores de `CompanyAccessGuard`/`BranchAccessGuard`/`InterBranchAccessGuard` re-envolvían solo el mensaje (`Failure(access.Error!)`, `Forbidden(...)`, `ValidationFailure(...)`), perdiendo el código. Semántica final en [`docs/architecture/error-handling.md § Semántica de scope`](docs/architecture/error-handling.md).
- Guards: métodos de **recurso** (`RequireMembershipAsync(id)`, `RequireBranchAsync(id)`) → NOT_FOUND (inexistente = ajeno) / FORBIDDEN; métodos de **contexto** (`RequireActiveTenantAsync`, `RequireCurrentCompanyAsync`, nuevo `RequireCurrentBranchAsync`) → `*_SCOPE_FORBIDDEN`, traduciendo por código en un único lugar. Behaviors: estado explícito + `Code` (UNAUTHORIZED → 401). Todos los consumidores propagan `Code`.
- Cambios de status observables: endpoints de empresa activa (`companies/current`, `profile/*`, branding, logos upload, fiscal/operación/documentos, `GET /companies`) con empresa de contexto sin acceso → 403 `COMPANY_SCOPE_FORBIDDEN` (antes 400; igual que cualquier endpoint con `CompanyScopeBehavior`); logos: `FORBIDDEN` → `COMPANY_SCOPE_FORBIDDEN` (mismo 403); `PUT /companies/{id}` inaccesible → 404 (antes 400); `POST /session/switch-branch` → 404 inexistente/ajena, 403 `FORBIDDEN` no autorizada (antes 400; `FORBIDDEN` y no `BRANCH_SCOPE_FORBIDDEN` para no resetear la sucursal activa en el frontend); transferencias → 404/403/422 por código (antes 400); reversa de pago con caja y sucursal activa inválida → 403 `BRANCH_SCOPE_FORBIDDEN` (antes 422); fondeo de caja: sucursal del documento → 404/403 según el guard (antes siempre 403 `FORBIDDEN`).
- Defectos corregidos: (1) **enumeración de usuarios en login** — usuario inexistente ("No estás registrado…"), contraseña incorrecta ("Credenciales inválidas…") e inactivo ("Usuario inactivo." ANTES de verificar la contraseña) eran distinguibles sin conocer la contraseña; ahora los tres dan el mismo 401 y "inactivo" solo tras contraseña correcta (`LoginHandler` y `GlobalLoginHandler`); (2) `GetCompanyById` distinguía por texto "sin membership" de "inexistente/otro tenant" → ahora mismo NOT_FOUND "Empresa no encontrada."; (3) `InterBranchAccessGuard` revelaba bodegas de otras empresas ("no pertenece…", "está deshabilitada" antes de validar pertenencia) → NOT_FOUND idéntico; y persistía `OperationBranchId` desde el header sin validarlo (fail-open) → ahora `RequireCurrentBranchAsync`.
- Auth: el 409 de conflicto de sesión solo es alcanzable tras `VerifyPassword` (igual que el 422 de preferencias de sucursal); ningún fallo previo a autenticar lleva código → siempre 401 idéntico.
- 500/503: `data.errors` nunca lleva detalle para InternalError/Infrastructure, ni desde `Result` (`ApiFailure` lo registra en log) ni desde excepciones (`ApiErrorStatus.ExposesDetail`, también en Development). Hallazgo no corregido: `InvalidOperationException` expone su texto como 422 `DOMAIN_RULE_VIOLATION` (fuera de 500/503, ver ERROR-HANDLING-AUDIT); un `DbException` crudo sale 500 (sin detalle) en vez de 503.
- Evidencia: `ScopeErrorSemanticsTests` (arquitectura: sin comparaciones de texto en `*ScopeBehavior`/`*AccessGuard`, sin re-wrap de resultados de guard sin `Code`; verificado por mutación) · guards: Company/Branch/InterBranch 34 (tenant A vs B, empresa A vs B, sucursal A vs B, contexto vs recurso) · behaviors por código · login/GetCompanyById/SwitchBranch/Companies propagación · `ApiErrorContractTests` (500/503 sin detalle en Production y Development) · `ApiErrorContractHttpTests` 12/12 (PostgreSQL) · Domain 1221 · Application 2418 · Architecture 121 · API 754/754 · Infrastructure relevante 340/340 · `architecture:check` 0 nuevas (62/100).

## ZH-API-ERROR-CONTRACT-HARDENING-01 — Un solo contrato Error → HTTP para todo el ERP (2026-09-29)

**Estado: COMPLETADO.** Había dos tablas desincronizadas: `MapFailure` (switch de 7 códigos; el resto → 400) y `ExceptionMiddleware` (switch por tipo de excepción). Ahora, como define ADR-027 §8-9, cada código de error declara su `ApiErrorCategory` en `MessageCatalog` y `ERP.API/Extensions/ApiErrorStatus` es la única tabla categoría → HTTP. La consultan `ApiResultExtensions.ApiFailure` (todo fallo de `Result`), `ExceptionMiddleware` (solo decide el `code`) y el rechazo del rate limiter. Matriz y excepciones intencionales: [`docs/architecture/error-handling.md`](docs/architecture/error-handling.md).
- Brechas cerradas en `Result` (antes 400): `RATE_LIMITED` 429 · `SRI_COMMUNICATION_ERROR` 502 · `CONCURRENCY_CONFLICT` 409 · `COMPANY_SCOPE_FORBIDDEN`/`BRANCH_SCOPE_FORBIDDEN` 403 · `DOMAIN_RULE_VIOLATION` 422 · `DATABASE_UNAVAILABLE` 503 · `INTERNAL_ERROR`/`INVALID_DATETIME_KIND` 500 (mismos status que ya daba el middleware).
- Cambios observables: `POST /purchases/reception/{id}/download-xml` con SRI sin XML autorizado → 502 (antes 400; solo el SSOT, sin tocar handler ni flujo — Purchases sigue CLOSED); `GET /retentions/{id}/electronic/xml` y `/ride/pdf` inexistente → 404 y validación → 422 (antes 400 por `ApiBadRequest` manual; `RetentionsControllerTests` fijaba ese 400 "por no-enumeración", pero el cuerpo ya decía "La retención no existe." — la protección real es que inexistente y de otro tenant/empresa dan el mismo NOT_FOUND, que se conserva); `POST /auth/login` con conflicto de sesión → 409 `CONFLICT` (antes 401: `MapAuthFailure` descartaba el código que `CreateAuthenticatedSessionHandler` documenta como 409); rechazo del rate limiter → 429 con envelope `RATE_LIMITED` (antes sin cuerpo).
- Mapeos paralelos eliminados: `ApiTooManyRequests` + rama manual de `/auth/refresh`, `MapAuthFailure`, `ApiConflict`, `ToOkOrUnauthorized`/`ToOkOrInternalServerError` (sin uso, ignoraban el código), `SetupController` que decidía 409/401 comparando el texto del error (ahora el handler devuelve `CONFLICT`/`UNAUTHORIZED`; status y mensajes iguales), `ApiBadRequest(result.Error)` en `RideController.GetContent` (ElectronicDocuments reabierto solo para el contrato HTTP: hoy todos sus fallos son sin código → mismo 400; rutas, payloads, permisos, PDF y no-enumeración intactos), `RetentionsController` y `SecurityController`.
- Guards: `CompanyAccessGuard`/`BranchAccessGuard` devuelven códigos canónicos (NOT_FOUND para inexistente o ajeno — indistinguibles —, `COMPANY_SCOPE_FORBIDDEN`/`BRANCH_SCOPE_FORBIDDEN` para acceso prohibido, UNAUTHORIZED sin sesión). Todos sus consumidores re-envuelven el mensaje, así que ningún status cambia (el 404 de `GetCompanyById` sobre empresa ajena se mantiene).
- Pendiente (documentado, no silencioso): los códigos de módulo aún literales (`SKU_DUPLICATE`, `BARCODE_DUPLICATE`, `PERIOD_NOT_OPEN`, `POSTING_ACCOUNT_INVALID`, `JOURNAL_ENTRY_NOT_FOUND`, `PURCHASE_XML_RECONCILIATION_REQUIRED`…) caen al fallback del catálogo (400 con su propio `code`). Registrarlos es ADR-027 Fase 1 por módulo (Items FROZEN, Purchases CLOSED).
- Evidencia: `ApiErrorContractTests` (matriz exhaustiva que falla si un código nuevo no declara status · categoría obligatoria · misma condición por excepción y por Result → mismo status y `code`) · `ResultStatusMappingTests` · `ApiErrorContractHttpTests` 6/6 (PostgreSQL: SRI 502 sin tocar el documento, recepción inexistente = ajena 404, retenciones 404, sucursal inexistente = ajena, rate limiter 429) · `RideControllerTests` +5 · guards con asserts de código · Domain 1221 · Application 2398 · Architecture 119 · API 734/734 · Infrastructure relevante 275 (1 flaky conocido `RideGoldenFileTests` bajo carga paralela, 6/6 aislado) · `architecture:check` 0 nuevas (62/100).

## ZH-DB-FINAL-BASELINE-SQUASH-01 — Una sola `InitialEnterpriseBaseline` instalable desde cero (2026-09-29)

**Estado: COMPLETADO.** Las 7 migraciones de desarrollo (`20260926002259_InitialEnterpriseBaseline` … `20260929105048_RestorePurchaseExpenseAccessKeyExclusivity`) se reemplazan por `20260929143218_InitialEnterpriseBaseline` (UTF-8 sin BOM, LF). Sin upgrade-path: no hay producción y las BD eran recreables. Snapshot regenerado idéntico al anterior (sin drift de modelo).
- Raw SQL fuera del modelo EF, incorporado al final del `Up`: `uq_mbp_identification`, `enforce_purchase_expense_exclusivity()` + `tr_expense_purchase_exclusivity`/`tr_purchase_expense_exclusivity`, y **restaurados** `pg_trgm` + `ix_items_short_name_trgm`/`ix_items_description_trgm` (perdidos en una consolidación anterior: `ItemRepository.SearchBySimilarityAsync` fallaba en toda BD nueva por falta de `similarity()`). No vigentes (no se incorporan): políticas RLS (ADR-005/015: no implementado), `ck_identity_users_platform_no_subscriber` (columnas eliminadas), `mv_saldos_diarios` e `ix_inventario_movimientos_kardex` (tabla `inventario_movimientos` inexistente; lector degrada con gracia). Pre-checks `DO $$`, `LOCK TABLE` y backfills de datos no aplican a BD vacía.
- DB_A (cadena histórica) vs DB_B (baseline): 194 tablas, 2586 columnas, 463 constraints, índices, función (md5 idéntico) y triggers iguales. Diferencias: + `pg_trgm` y sus 2 índices (restauración intencional); `supplier_payments.confirmed_by_user_id` sin `DEFAULT '0000…'` (artefacto del `AddColumn` de backfill; el dominio siempre lo asigna); orden físico de columnas. Ninguna pérdida de esquema.
- Causa raíz de las pérdidas: `scripts/dev-launcher.ps1` opción 2 (reset) borraba `Migrations/` y regeneraba la baseline desde el snapshot en cada reset. Ahora el reset solo borra la BD y aplica las migraciones versionadas.
- Anti-regresión: `RawSqlDatabaseObjectsSurviveMigrationSquashTests` registra los 7 objetos y exige que todo `CREATE …` raw de las migraciones esté registrado; nuevo `RawSqlDatabaseObjectsBaselineIntegrationTests` (PostgreSQL) verifica su existencia física y la búsqueda por similitud. Eliminados los tests de upgrade-path que migraban a migraciones intermedias (backfills de precisión, pre-checks de duplicados/conflictos); `PrecisionCapacityAlignmentMigrationTests` conserva la verificación de CHECKs sobre la baseline.
- Instalación local desde cero: `dberpsaas` eliminada y recreada con `dotnet ef database update` → solo `20260929143218_InitialEnterpriseBaseline` en `__EFMigrationsHistory`; bootstrap/InstallData, token first-run, `POST /setup/admin`, login, empresa `Principal` accesible (`companies/current` 200).
- Evidencia: Infrastructure 960/960 (en `HEAD`: 952/957 — 4 tests apuntaban a migraciones ya borradas + 1 timeout de Docker) · API 638/638 · Application 2398 · Domain 1221 · Architecture 119 · `architecture:check` sin cambios (62/100, 0 nuevas).

## ZH-PURCHASES-RETENTION-OWNERSHIP-01 — Pertenencia de retenciones de compra en Application (2026-09-29)

**Estado: COMPLETADO. Purchases vuelve a CLOSED** (reapertura acotada a la garantía de pertenencia de retenciones).
- Causa: `POST /purchases/{purchaseId}/retention/{retentionId}/cancel` validaba la pertenencia en el controller (check-then-act: `GetRetentionBySourceQuery` → comparar Id → `CancelRetentionCommand`); el handler solo miraba tenant+sucursal, así que la regla no existía en Application.
- Contrato: `CancelRetentionCommand(SourceDocumentType, SourceDocumentId, RetentionDocumentId, Reason)` — el origen (de la RUTA) es obligatorio; el handler solo anula la retención ACTIVA de ese origen en la sucursal actual. Inexistente, de otra compra, de un gasto, de otra sucursal, de otro tenant o ya anulada → mismo 404 `NOT_FOUND` "La retención no existe o no pertenece a esta compra." (contrato HTTP previo intacto). Controller: route → command → respuesta.
- Mismo tipo de hallazgo corregido: `GET /purchases/{id}/retention-preview` (`CalculateRetentionHandler`) no validaba la sucursal (a diferencia de `GetPurchaseByIdHandler`) y exponía el cálculo de retención de compras de otra sucursal → ahora el mismo NotFound que una compra inexistente. `GET`/`POST retention` ya filtraban por el origen de la ruta y sucursal.
- No se separó `PurchaseRetentionsController`: los 4 endpoints ya están delimitados, la lógica es transversal en Application, Purchases seguiría >150 líneas y hay una decisión documentada de mantenerlos en `PurchasesController`.
- Evidencia: `CancelRetentionOwnershipIntegrationTests` (PostgreSQL: caso válido + 6 inválidos sin modificar filas) · `CancelRetentionHandlerTests` 14 · `CalculateRetentionBranchScopeTests` 2 · `PurchasesControllerCancelRetentionTests` 5 · Application 2398 · API 638/638 · Infra Retention/Purchase 194/194 · Architecture 118 · `architecture:check` sin cambios (62/100, 19 warnings, 0 nuevas).

## ZH-API-RESULT-STATUS-MAPPING-01 — `ToOkOrNotFound` deja de convertir todo fallo en 404 (2026-09-29)

**Estado: COMPLETADO.** `ToOkOrNotFound` devolvía 404 para cualquier `Result` fallido (FORBIDDEN, VALIDATION_ERROR, CONFLICT…), ignorando la tabla única de `MapFailure` que ya usan `ToOkOrBadRequest`/`ToCreatedOrBadRequest`.
- SSOT Result → HTTP (`ApiResultExtensions.MapFailure`, sin cambios): NOT_FOUND 404 · FORBIDDEN 403 · UNAUTHORIZED 401 · VALIDATION_ERROR 422 · CONFLICT / UNIQUE_VIOLATION / COMPANY_RUC_ALREADY_EXISTS 409 · resto (BAD_REQUEST, código desconocido o nulo en ToOkOrBadRequest) 400. `ToOkOrNotFound` y el nuevo `ToFileOrNotFound` delegan en ella todo fallo CON código; un fallo SIN código conserva su 404 histórico (handlers "obtener por id" con `Failure("X no encontrado")`).
- Auditoría de los 47 usos (31 controllers): 45 solo pueden fallar con NOT_FOUND o sin código → sin cambio; `GetCompanyById` (fallo sin código del guard de membresía) → sin cambio, 404 intencional; `ResolveItem` (VALIDATION_ERROR con código en blanco) → hoy inalcanzable por HTTP ([ApiController] responde 400 antes). Ningún handler de estos devuelve FORBIDDEN → no se introduce leakage.
- Único cambio de contrato observable: `GET /api/v1/companies/profile/logo/content` y `/logo-alt/content` sin empresa operativa o sin membresía → 403 FORBIDDEN (antes 404; mismo criterio que `CompanyScopeBehavior`); logo inexistente sigue 404. El controller usa `ToFileOrNotFound` y los 2 handlers devuelven `Forbidden` en vez de `Failure` sin código (único cambio en Application, necesario para distinguir). Frontend sin impacto (`BrandingSettingsSection` captura cualquier error).
- Excepciones 404 intencionales: `GetCompanyById` sobre empresa ajena (indistinguible de inexistente); recursos fuera de tenant/company/branch por query filter (NOT_FOUND).
- Hallazgos no corregidos (fuera de alcance): `MapFailure` no cubre RATE_LIMITED/SRI_COMMUNICATION_ERROR/CONCURRENCY_CONFLICT como `ExceptionMiddleware` (SRI_COMMUNICATION_ERROR lo usa Purchase Reception, CLOSED); `RideController` (ElectronicDocuments, CLOSED) mapea todo fallo a 400; los guards de `CompanyAccessGuard` devuelven `Failure` sin código (contradice E-B1).
- Evidencia: `ResultStatusMappingTests` 19/19 (cada código, paridad con ToOkOrBadRequest) · `ResultStatusMappingHttpTests` 10/10 (en `HEAD`: solo fallan los 2 casos 403 de logos) · Application 2391 · API 638/638 · Architecture 118 · `architecture:check` sin cambios (62/100, 19 warnings, 0 nuevas).

## ZH-API-THIN-STOCK-01 — StockController separado en consultas / ajustes / transferencias (2026-09-29)

**Estado: COMPLETADO.** `StockController` (13 endpoints, 282 líneas) mezclaba tres responsabilidades con agregado, permiso, pantalla y módulo frontend propios: consultas de existencias (`StockController`, 5, `inventory.stock.view`, sin menú), ajustes (`StockAdjustmentsController`, 6, agregado StockAdjustment Draft → Executed → Cancelled, `inventory.adjustments.*`, menú "Ajustes de inventario") y transferencias (`StockTransfersController`, 2, agregado StockTransfer Draft → Confirmed, `inventory.stock.manage`, menú "Transferencias entre bodegas"). Métodos movidos tal cual (XML de acción, `[AppFeature]` de método, `CancelStockAdjustmentRequest` en el mismo namespace); cada controller repite `[Authorize]` de clase y `[Tags("Stock")]`. Sin cambios en Application, frontend ni endpoints.
- Aislamiento Company/Branch intacto: vive en Application (marcadores `IBranchScopedRequest` / `ICompanyScopedRequest` / `IInterBranchOperationRequest` + behaviors/guards); los controllers no leen ni reenvían tenant/company/branch. Test explícito: cada endpoint envía el request con su marcador.
- Contrato demostrado antes/después: snapshots de Stock (rutas + policies efectivas, OpenAPI de `/api/v1/inventory/stock*` byte a byte, filas AppFeature de inventario) y `StockEndpointsHttpContractTests` (21: request exacto por endpoint, parámetros intactos, status éxito/fallo, id ruta≠cuerpo → 400 sin llamar a Application, 401 sin sesión) · `InventoryAdjustmentsEndToEndTests` 10/10 (válido, inválido, 403 por permiso).
- Métricas (efecto secundario, no objetivo): 122 / 145 / 68 líneas → warnings 20 → 19, score 60 → 62/100 · API 609/609 · Architecture 118.

## ZH-API-THIN-COMPANIES-01 — CompaniesController separado por responsabilidad (2026-09-29)

**Estado: COMPLETADO.** `CompaniesController` (19 endpoints, 300 líneas) mezclaba dos responsabilidades que el frontend ya consume desde módulos distintos: administración de empresas del tenant (`company-management`: listar, `current`, `{id}`, crear, editar) y configuración de la empresa activa (`configuracion/empresa`: `profile/*`, fiscal, operación, documentos, branding, logos, `fiscal-policy`, `operational-readiness`). Los 14 endpoints de la segunda se movieron tal cual a `CompanyProfileController`; `CompaniesController` conserva los 5 de administración y el `[AppFeature]` "Empresas operativas". Sin lógica nueva ni cambios en Application.
- Compatibilidad demostrada con snapshots grabados en `HEAD` (`CompaniesApiSurfaceSnapshotTests`): rutas + métodos + policies efectivas (19 endpoints), documento OpenAPI de `/api/v1/companies*` + tags globales byte a byte (el nuevo controller usa `[Tags("Companies")]` y comentario no-XML: un `<summary>` de clase agregaría un tag al documento), y filas de AppFeature descubiertas.
- Métricas (sin maquillar): 20 warnings y 60/100 sin cambios — `CompaniesController` 96 líneas sale del reporte, `CompanyProfileController` 232 entra.
- Evidencia: snapshots 3/3 · uploads de logo 18/18 · API 585/585 · Architecture 118 · `architecture:check` 0 nuevas.

## ZH-API-THIN-MEDIA-UPLOAD-01 — Conversión IFormFile → MediaUploadContent unificada (2026-09-29)

**Estado: COMPLETADO.** La conversión (buffer en memoria + rebobinar + `MediaUploadContent(stream, FileName, ContentType, Length)`) estaba copiada en 5 endpoints multipart: Companies `profile/logo` y `profile/logo-alt`, ElectronicInvoicing `sri-configuration/certificate`, InitialLoad `batches/{id}/upload`, PurchaseReception `import`. Ahora es `ERP.API/Uploads/BufferedFormFile` (IAsyncDisposable, solo capa API). Cada endpoint conserva explícito su 400 por archivo ausente/vacío (mensajes distintos) y sigue liberando el buffer con `await using` después del `mediator.Send` (los handlers consumen el stream dentro del Send). Sin cambios en rutas, permisos, Swagger, AppFeature, DTOs ni validaciones de tipo/tamaño (viven en los validators de Application).
- Nota: se descartó `ERP.API/Files/` porque en Windows coincide con `ERP.API/files/` (almacenamiento runtime, gitignored).
- Evidencia: contrato `MediaUploadEndpointsContractTests` 15/15 antes y después (nombre, ContentType, tamaño, bytes, stream legible en posición 0 durante el Send y liberado al final, 400 propio sin llamar al mediator, error del handler igual) · `BufferedFormFileTests` 3/3 (copia fallida no deja el buffer abierto) · API 582/582 · Architecture 118 · `architecture:check` sin cambios (60/100, 20 warnings, 0 nuevas).

## ZH-API-THIN-DEVCACHE-01 — Sonda de cache extraída de DevCacheController (2026-09-29)

**Estado: COMPLETADO.** La sonda de `/api/dev/redis-health` (Redis configurado, conexión + PING, round-trip set/get/remove sobre `IDistributedCache`) pasa de inline en el controller a `ERP.API/Diagnostics/CacheHealthProbe` (servicio técnico de ERP.API, scoped). Controller: guard Development → probe → respuesta; 152 → 85 líneas. `/api/dev/cache-metrics` sin cambios (solo proyección de snapshots).
- Bug corregido: el `ConnectionMultiplexer` de la sonda nunca se liberaba (una conexión abierta por request). Ahora es efímero y se libera siempre (`DisposeAsync` en `finally`, también si PING falla); no existe un multiplexer compartido en DI para reutilizar (el de `IDistributedCache` es interno de StackExchangeRedisCache).
- Contrato idéntico (fijado antes del cambio): rutas, `IgnoreApi`, 404 `NOT_FOUND` fuera de Development, mismo JSON (claves y orden) con Redis ausente / disponible / inalcanzable. Se conserva a propósito una rareza preexistente: `instanceName` informa `Redis:InstanceName` tal cual (`?? "ERP_"`), aunque Program.cs trata vacío como `ERP_`.
- Evidencia: `DevCacheEndpointsHttpContractTests` 6/6 en `HEAD` y después (Redis real vía Testcontainers) · `CacheHealthProbeTests` 7/7 (mutación sin dispose → 3 fallan) · API 564/564 · Architecture 118 · `architecture:check`: warnings 21 → 20, score 58 → 60/100 (drift high → medium), 0 nuevas.

## ZH-API-THIN-AUTH-01 — Emisión de la cookie de refresh unificada (2026-09-29)

**Estado: COMPLETADO.** El bloque "si la respuesta trae RefreshToken y RefreshTokenExpiry → emitir cookie" estaba copiado 7 veces (6 en `AuthController`: Login, GlobalLogin, CompletePasswordReset, Refresh, Reauthenticate, SwitchCompany; 1 en `GlobalAuthController.CompleteAuthResponse`: operate-company/return). Ahora es una sola operación `AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, AuthResponseDto?)` (solo transporte HTTP; `SetRefreshCookie` pasa a privado). Sin cambios en JWT, rotación, ventana absoluta, claims, permisos, rutas, payloads, status ni política de cookie (`AuthRefreshCookie`). `AuthController` 292 → 238 líneas (sigue sobre 150).
- Contrato fijado ANTES del refactor (`AuthRefreshCookieContractTests`, 31 casos sobre el header Set-Cookie real, http y https): éxito con token → una cookie `erp_refresh_token` HttpOnly, SameSite=Strict, Path=/api, Expires=RefreshTokenExpiry, Secure=Request.IsHttps; éxito sin token/expiración/valor → ninguna; fallo (incl. 429) → ninguna y mismo status; forgot/reset-password y my-companies nunca la tocan; logout borra /api y /api/auth aunque el comando falle.
- Evidencia: contrato 31/31 antes y después · API 551/551 · Architecture 118 (incl. `AuthAttackSurfaceGuardTests`) · `architecture:check` sin cambios (58/100, 21 warnings, 0 nuevas).

## ZH-API-THIN-BP-ROLES-01 — Config de roles de BP construida en Application (2026-09-29)

**Estado: COMPLETADO.** `BusinessPartnerRolesController` construía `SupplierRoleConfig`/`CarrierRoleConfig`/`CustomerRoleConfig` (Domain) y traducía su `ArgumentException` a 400 en los 3 PATCH de config y en `AssignRole`. Ahora es Request → Command → mediator → ApiResult; sin `ERP.Domain.MasterData.ValueObjects` ni try/catch (269 → 207 líneas; sigue sobre 150, el warning se mantiene — no se dividió).
- Commands `Update{Supplier,Carrier,Customer}RoleConfigCommand.Config` y `AssignBusinessPartnerRoleCommand.*Config` reciben los DTOs primitivos ya existentes (`*RoleConfigDto`, sin tipos nuevos). `RoleConfigFactory` (Application/MasterData/Services) es el único punto DTO → VO; los handlers construyen la config ANTES de resolver rol/BP y devuelven `Result.ValidationFailure(mensaje de Domain, BAD_REQUEST)`.
- Compatibilidad exacta: 400 `BAD_REQUEST` con `data.errors = [ArgumentException.Message]`, 404 idéntico para rol de otro BP/inexistente, config inválida responde 400 exista o no el rol. Validators validan la config normalizada por Domain (trim, vacío → null) con las mismas claves `Config.X`. Ownership (bpId + roleId) intacto. Importadores de clientes/proveedores envían el DTO.
- Evidencia: contrato HTTP `BusinessPartnerRoleConfigHttpContractTests` 4/4 en `HEAD` (antes del cambio) y 4/4 después · Application 2391 · API 520 · Architecture 118 (guard `BusinessPartnerRolesControllerThinTests`) · Infra MasterData/InitialLoad 23 · `architecture:check` sin cambios (58/100, 21 warnings, 0 nuevas).

## ZH-PURCHASE-EXPENSE-EXCLUSIVITY-RESTORE-01 — Exclusividad Compra↔Gasto por AccessKey restaurada (2026-09-29)

**Estado: COMPLETADO.** Purchases sigue CLOSED: solo se reabrió esta garantía de integridad en BD; sin cambios de UX, endpoints ni flujo.
- Regla vigente recuperada de `20260911023601_ReceptionReprocessAfterCancelStandard` (versión que ignora Cancelled; la original de `20260908175033_ExpensesFromPurchaseReception` no miraba status): una factura (tenant + AccessKey) no puede estar activa como compra (`status <> 3`) y como gasto (`status <> 2`) a la vez; triggers `BEFORE INSERT OR UPDATE OF tenant_id, access_key` en `purchase_invoices`/`expense_documents`, candado consultivo por tenant+AccessKey, `23505` con CONSTRAINT `uq_purchase_expense_access_key` (lo traducen `PurchaseDraftUseCases`/`ExpenseDocumentDraftUseCases`). Sin AccessKey no participa. La unicidad dentro de cada tabla (índices parciales EF) sí sobrevivió.
- Causa histórica: raw SQL fuera del modelo EF; la consolidación `4cbc4b12` (2026-09-25) lo perdió junto con `uq_mbp_identification`.
- Migración `20260929105048_RestorePurchaseExpenseAccessKeyExclusivity` al final de la cadena (snapshot intacto): `LOCK TABLE` + pre-check que falla listando cada par tenant/AccessKey/compra/gasto activos (no anula ni borra nada) + `CREATE OR REPLACE FUNCTION` + `DROP TRIGGER IF EXISTS`/`CREATE TRIGGER`. BD local `dberpsaas`: 0 conflictos — aplica limpio.
- Anti-regresión: `RawSqlDatabaseObjectsSurviveMigrationSquashTests` ahora exige función (incluidos los filtros de status) y ambos triggers.
- Evidencia: `PurchaseExpenseReprocessAfterCancelConstraintsTests` 20/20 (los 2 históricos vuelven a pasar + 6 nuevos) · Infra Purchase/Expense/Reception/MasterData 208/208 · API 515/515 · Application 2358 · Architecture 117 · `architecture:check` sin cambios (58/100, 21 warnings, 0 nuevas); baseline/grandfather intactos.

## ZH-BP-IDENTIFICATION-UNIQUE-01 — Índice único de identificación de Business Partner restaurado (2026-09-28)

**Estado: COMPLETADO.** `PG_unique_business_partner_identification_enforced` fallaba porque la BD no tenía `uq_mbp_identification` (ADR-BP-03: UNIQUE incondicional `tenant_id + identification_type + identification_number`; BP tenant-scoped, sin company_id, un BP por identificación con todos sus roles). El test era correcto.
- Causa (B): el índice es raw SQL (EF no puede indexar columna del owner + owned type) y se perdió por TERCERA vez al consolidar migraciones — `4cbc4b12` (2026-09-25) borró `20260914034857_AddBusinessPartnerIdentificationUniqueIndex` y la nueva `InitialEnterpriseBaseline` se regeneró desde el snapshot, que no lo conoce. Create/Update/bootstrap de Consumidor Final confiaban en él como barrera real.
- Corrección: migración `20260929034558_AddBusinessPartnerIdentificationUniqueIndex` al final de la cadena (sin cambio de modelo; snapshot intacto) con pre-check que detiene la migración listando duplicados si una BD los acumuló. BD local `dberpsaas`: 0 duplicados, índice ausente — aplica limpio.
- Anti-regresión: `RawSqlDatabaseObjectsSurviveMigrationSquashTests` (Architecture, sin Docker) falla si una consolidación deja fuera un objeto raw SQL registrado.
- **Hallazgo (RESUELTO 2026-09-29 en ZH-PURCHASE-EXPENSE-EXCLUSIVITY-RESTORE-01):** la misma consolidación perdió `enforce_purchase_expense_exclusivity` + triggers `tr_expense_purchase_exclusivity`/`tr_purchase_expense_exclusivity` (origen `20260908175033_ExpensesFromPurchaseReception`); 2 tests de `PurchaseExpenseReprocessAfterCancelConstraintsTests` fallan por eso. Al restaurarlos, registrarlos en el test anti-regresión.
- Evidencia: PostgreSQL unicidad 9/9 · Infra MasterData 24/24 · API 515/515 · Application 2358 · Architecture 117 · `architecture:check` sin cambios (58/100, 21 warnings, 0 nuevas).

## ZH-BP-NESTED-RESOURCE-OWNERSHIP-01 — Ownership de rutas anidadas de Business Partners (2026-09-28)

**Estado: COMPLETADO.** Las 15 rutas `/business-partners/{bpId}/(roles|contacts|locations)/{childId}` descartaban `bpId` (`_ = bpId`) y los handlers solo recibían el id hijo: dentro del mismo tenant, un hijo de BP-B se consultaba/modificaba con el bpId de BP-A.
- Application: `BusinessPartnerId` agregado a los 15 commands/queries; cada handler exige `child.BusinessPartnerId == bpId` y responde el mismo NotFound que un id inexistente (sin revelar existencia cross-parent). Validators con `BusinessPartnerId.NotEmpty()`; el bypass legacy de `CustomerClassification` ya no lee el valor de un rol de otro BP (evita oráculo 400/404).
- Mismo tipo de hueco, también cerrado: `POST contacts|locations` valida que el BP de la ruta exista en el scope (antes creaba hijos colgando de un BP de otro tenant); la `LocationId` de un contacto (create/update) debe ser del mismo BP (el comentario decía "validado en handler" pero no lo estaba).
- Controllers: usan el bpId real; URLs, payloads y permisos sin cambios. Único cambio público: el caso inválido responde 404 (400 para `LocationId` ajena).
- Evidencia: Application 2358 · Architecture 116 · API 514/515 (falla preexistente `PG_unique_business_partner_identification_enforced`, falla igual en `HEAD`) · PostgreSQL MasterData 15/15 (incluye 5 nuevos de ownership) · `architecture:check` sin cambios (58/100, 21 warnings, 0 nuevas).

## ZH-ARCH-CSS-PREFIXES-02 — Barra de filtros propia de Bancos; baseline en 0 (2026-09-28)

**Estado: COMPLETADO.** Últimas 6 `css-prefixes` resueltas; el baseline versionado queda vacío (`violations: []`, `byCheck: {}`).
- Causa: `settings/banks/BanksPage.tsx` usaba `.coa-list-filters` de `ChartOfAccountsPage.css` sin importarlo — solo tenía estilo si Contabilidad ya se había cargado (en carga directa de `/settings/banks` la barra no tenía grilla).
- Solución: `ZHFilterBar` descartado (panel con borde/padding/fondo propio, pensado fuera de tarjetas; en las acciones de `ZHCard` cambiaría la UX). Nueva `BanksPage.css` (importada por la página) con `.banks-list-filters`: mismas reglas (grid, gap, `max-width` 900, alineación a la derecha, input 100%, botón `nowrap`, colapso a 1 columna ≤920px) con la grilla ajustada a sus 2 elementos (`minmax(280px, 1fr) 180px`, antes 4 columnas con 2 vacías). En Contabilidad `.coa-list-filters` → `.acc-coa-list-filters` (CSS, página y test).
- Ratchet: test del baseline versionado vacío + test de que con baseline vacío el gate pasa sin hallazgos y falla ante cualquiera nuevo.
- Resultado real de `npm run architecture:check`: gate PASS, 19 checks (18 PASS, `backend-controller-thin` WARN con 19 controllers >150 líneas desde ZH-API-THIN-STOCK-01), baseline 0, nuevas 0. Score reportado 62/100 (critical <70, drift medium): 100 − 19 warnings×2 − 0 entradas grandfather. Grandfather en 0: `designSystemGrandfathered` (ZH-ARCH-GRANDFATHERED-DESIGN-01, 4 `<Link className="zh-btn…">` de Sucursales → `ZHLinkButton`) y `namingConventionsGrandfathered` (ZH-ARCH-NAMING-GRANDFATHER-01: las 162 entradas apuntaban a archivos inexistentes desde el rename a inglés `188d682f`; 493 archivos backend sujetos a la regla, 0 violaciones sin grandfather). La única deuda restante del score son los controllers >150 líneas (20 tras ZH-API-THIN-DEVCACHE-01). Desde ZH-ARCH-GRANDFATHER-INTEGRITY-01 el check `grandfather-integrity` impide volver a acumular entradas muertas.
- Hallazgo aparte (sin corregir): `OperationalReadinessSection.tsx` usa `cfg-opreadiness-section` (antes `opreadiness-section`) sin ninguna regla CSS — preexistente.

## ZH-ARCH-CSS-PREFIXES-01 — Prefijos CSS de página (2026-09-28)

**Estado: COMPLETADO (106 de 112; 6 requieren migración coordinada).** 112 ocurrencias clasificadas: A 106 · B 0 · C 6 · D 0. Renombre por prefijo (CSS + TSX + tests), manteniendo el segmento semántico y los modifiers BEM; estilos y layout sin cambios (mismo set de clases que HEAD, solo con prefijo permitido); checker intacto.
- `ProfilesPage.css` + `PermissionsAssignmentPage.tsx`: `pa-` → `access-pa-` (34). `CompanySelectPage.css/.tsx`: `cs-` → `auth-cs-` (40; incluye el modifier dinámico `auth-cs-chip--${tone}`). `ChartOfAccountsPage.css/.tsx/.test`: `coa-` → `acc-coa-` (20). `operational-readiness.css` + `OperationalReadinessSection.tsx`: `opreadiness-` → `cfg-opreadiness-` (11). `document-flow-policies-page.css` + `DocumentFlowPoliciesPage.tsx`: `dfp-` → `cfg-dfp-` (1).
- C pendiente (6): `.coa-list-filters` — definida en `ChartOfAccountsPage.css` pero usada también por `settings/banks/BanksPage.tsx`, que no importa ese CSS (solo recibe estilo si el chunk de Contabilidad ya se cargó). Resolverlo exige decidir el estilo propio de la barra de filtros de Bancos.
- Baseline: 112 → 6; `css-prefixes` 112 → 6. Frontend completo 2551/2551.

## ZH-ARCH-MODULE-BOUNDARIES-06-RETENTIONS — Contrato público de retención de compra (2026-09-28)

**Estado: COMPLETADO.** `module-boundaries` en 0. Purchases depende solo de la facade pública de Retentions; backend, API, payload, permisos y UX sin cambios.
- Auditoría (consumo de Purchases): `getForPurchase` (GET `/purchases/{id}/retention`, `purchases.view`, `GetRetentionBySourceQuery`), `issueForPurchase` (POST `/purchases/{id}/retention`, `purchases.update`, `IssueRetentionCommand` — número por secuencia server-side), `cancelForPurchase` (POST `/purchases/{id}/retention/{rid}/cancel`, `purchases.update`, `CancelRetentionCommand` — reversa CxP + asiento), `getElectronicXmlBlob`/`getRidePdfBlob` (GET `/retentions/{id}/electronic/xml|ride/pdf`, `expenses.documents.view`, on-demand sin persistir), `registerElectronic` (POST `/retentions/{id}/electronic/register`, `electronic-documents.retry`, firma + SRI). Tipos: `RetentionDocumentDto`, `IssueRetentionLineRequest`. Todos los casos de uso viven en `ERP.Application/Modules/Retentions` → owner Retentions.
- `retentions/facades/purchaseRetentionFacade`: delega las 6 operaciones en `retentionsService` (lectura / emitir / anular / registrar explícitas en su documentación) y re-exporta solo los 2 tipos consumidos. `usePurchasesPage` y su test de retención (mock) migrados.
- Baseline: 114 → 112; `module-boundaries` 2 → 0. Frontend completo 2551/2551.

## ZH-ARCH-MODULE-BOUNDARIES-05-EXPENSE-PURCHASE — Precarga de Gasto desde recepción (2026-09-28)

**Estado: COMPLETADO.** Resuelta la violación expenses → purchases con una facade pública explícita en el módulo propietario (Purchases); backend, API, payload y UX sin cambios.
- Auditoría: `createExpenseDraft` → `POST /purchases/reception/{id}/create-expense-draft` (`CreateExpenseDraftFromReceptionQuery`, permiso `purchases.view`). Valida elegibilidad sobre `PurchaseReceptionDocument` (factura Verified con XML, sin compra/gasto con la misma clave, proveedor activo con rol) y devuelve `ExpenseReceptionDraftDto` para precargar el formulario. No crea `ExpenseDocument` (eso lo hace `CreateExpenseDraftCommand` al guardar en Gastos). Efecto persistente: si la recepción no tenía `SupplierId`, `ReceptionSupplierResolver` lo vincula (`AssignSupplier` + `SaveChanges`) — estado de Purchases.
- Owner: Purchases (fuente de datos, reglas de elegibilidad y único efecto persistente son de la recepción). Facade `purchases/facades/purchaseReceptionExpenseFacade.prepareExpenseDraft` (nombre semántico: prepara precarga; documentado como POST con efecto, no lookup) + tipo `ExpenseReceptionDraft`.
- Observación (sin cambio): el nombre backend `create-expense-draft`/`CreateExpenseDraftFromReceptionQuery` sugiere creación y "Query" oculta el vínculo de proveedor persistido; renombrar/separar sería un cambio de contrato → decisión aparte.
- Queda 1 D (2 ocurrencias): purchases → retentions. Baseline: 115 → 114; `module-boundaries` 3 → 2.

## ZH-ARCH-MODULE-BOUNDARIES-04-PAYABLES — Contrato público de payables (2026-09-28)

**Estado: COMPLETADO.** Las 2 violaciones hacia payables resueltas con el contrato en el módulo propietario; sin cambios de backend, API, pagos ni SupplierCredit.
- `payables/facades/payableLookupFacade` (nueva): `list` (delegado a `payablesService.list`), `PayableListItemDto`, `payableOriginLabel` → consumido por `finance/ApplySupplierCreditModal`.
- `pendingPayablesFacade` (+ su test) movido con `git mv` de `supplier-payments/api/` a `payables/facades/` sin cambiar su lógica (lectura de cuotas pendientes vía `payablesService.list/getById`); supplier-payments (página de pago, cartera, preview, modal de confirmación y tests) lo importa desde payables. El adaptador en el consumidor quedó eliminado.
- Quedan 3 D (rediseño): expenses→purchases `createExpenseDraft`, purchases→retentions ×2.
- Baseline: 117 → 115; `module-boundaries` 5 → 3. Frontend completo 2551/2551.

## ZH-ARCH-MODULE-BOUNDARIES-03 — Lookups read-only restantes vía facades del owner (2026-09-28)

**Estado: COMPLETADO.** Las 33 violaciones A de `module-boundaries` resueltas con facades en el módulo propietario que solo delegan al servicio existente; API/payload/comportamiento sin cambios, backend intacto.
- Facades nuevas: `caja/facades/cashRegisterLookupFacade` (`getCashRegisters`, `CashRegisterDto`), `finance/facades/bankAccountLookupFacade` (`list`, `CompanyBankAccountDto`), `accounting/facades/accountLookupFacade` (`listAccounts`, `AccountDto`), `configuracion/facades/operationalPreferencesLookupFacade` (`getPreferences`, `OperationalPreferencesDto`), `configuracion/facades/electronicInvoicingLookupFacade` (solo `export type ElectronicInvoicingStatusDto`). Reutilizada: `settings/banks/facades/bankLookupFacade` (sales).
- Consumidores (27 archivos): caja, sales, finance, supplier-payments, expenses; mocks de tests movidos a la facade (incluidos `ExpenseDocumentFormPage.reception`/`.vatMismatch` y `SupplierPaymentFormPage.cashFunding`, que no importaban el servicio).
- Quedan solo las 5 D (rediseño, sin tocar): expenses→purchases `createExpenseDraft`, purchases→retentions ×2, finance→payables `list`, supplier-payments `pendingPayablesFacade`→payables.
- Baseline: 150 → 117; `module-boundaries` 38 → 5. Frontend completo 2551/2551.

## ZH-ARCH-MODULE-BOUNDARIES-02 — Facades públicas para lookups seguros (2026-09-28)

**Estado: COMPLETADO (parcial por diseño).** 48 ocurrencias clasificadas: A 42 · B 1 · C 0 · D 5 · E 0. Corregidas 10 (prioridades sin módulo protegido + lookup read-only de masterData); API/payload sin cambios, facades solo delegan.
- Facades nuevas: `company-management/facades/companyLookupFacade` (`list`, `getCurrent` → admin sesiones, auth `syncCompanySelection` + tests), `pricing/facades/priceListDefaultFacade` (`setDefault`, mutación nombrada explícitamente; `list` sale de la existente `priceListLookupFacade` → `CompanySalesSettingsSection`), `settings/banks/facades/bankLookupFacade` (`list` → `BankAccountsPage`), `masterData/facades/businessPartnerLookupFacade` (`getBusinessPartner` → páginas de pago a proveedor + tests, incluido el mock de `SupplierPaymentFormPage.cashFunding.test`).
- Pendientes A (lookups read-only con módulo protegido, requieren aprobación): caja `cajaService.getCashRegisters` (finance ×2, supplier-payments ×5), finance `bankAccountService.list` (sales ×2, supplier-payments ×5), accounting `listAccounts`/`AccountDto` (expenses ×10, sales ×1), configuracion `getPreferences` (caja ×3, sales ×1) y `ElectronicInvoicingStatusDto` (sales ×2), settings/banks `list` (sales ×2). D (rediseño): expenses→purchases `createExpenseDraft`, purchases→retentions (emitir/anular), finance→payables, supplier-payments `pendingPayablesFacade`→payables.
- Baseline: 160 → 150; `module-boundaries` 48 → 38. Frontend completo 2551/2551.

## ZH-ARCH-DESIGN-SYSTEM-03 — Últimos tokens `design-system` (2026-09-28)

**Estado: COMPLETADO (3 de 3).** Sin tokens nuevos, sin inline, sin cambios de checker.
- `CajaPage.css` `.cj-collection-detail-wrap`: `var(--color-surface-subtle, transparent)` → `var(--color-surface-container-low)`. Evidencia: los bloques resumen/informativos con borde + radius + padding usan `--color-surface-container-low` (20 usos: `.sales-summary-box`, `.sales-cash-box`, `.cs-summary`, …); el nombre `surface-subtle` expresa esa intención y `transparent` era solo fallback. Efecto: el panel expandido de sesión de caja muestra fondo sutil `#f2f4f6`.
- `expense-documents.css` `.exp-doc-total-row--grand` (`--text-title-md-size`/`-weight`): decisión visual aprobada → `--text-headline-sm-size`/`--text-headline-sm-weight` (18px/600; el monto sigue dominante con `ZHMoneyValue emphasis="grand"` 20px/800). Contexto de la decisión: No existe escala `title-md` (solo `headline-sm` 18px/600 y `title-sm` 16px) y los totales equivalentes no tienen patrón único (Ventas factura `headline-lg`/800, Compras factura `headline-md`/800, Devolución venta `headline-sm`, Caja arqueo/Ventas caja 700 con tamaño heredado). El monto ya lo dimensiona `ZHMoneyValue emphasis="grand"`; solo afecta la etiqueta "Total".
- Baseline: 163 → 160; `design-system` 3 → 0.

## ZH-ARCH-DESIGN-SYSTEM-02 — Custom properties locales en `F-04-token` (2026-09-28)

**Estado: COMPLETADO.** El checker `design-system` distingue custom properties locales legítimas de tokens globales sin allowlist por archivo ni wildcard. Sin cambios en CSS/TSX.
- Regla: `var(--x)` válido si `--x` está en `design-tokens.css`, declarado en el mismo CSS (sin contar comentarios) o fijado en runtime por un componente que importa ese CSS (`"--x":` en `style` / `setProperty("--x")`). Siguen fallando tokens inexistentes, typos globales y custom properties declaradas solo en otro CSS.
- Resueltos 18: `--account-tree-*` ×9 (`ChartOfAccountsPage.css`), `--sfl-cols`/`--sfl-col-gap` ×8 (`sales-product-card.css`), `--batch-progress` ×1 (fijado por `ZhBatchProgress`). Pendientes (decisión visual): `CajaPage.css --color-surface-subtle`, `expense-documents.css --text-title-md-*`.
- `check-design-system.test.mjs` (9 tests) agregado a `npm run architecture:check`. Baseline: 181 → 163; `design-system` 21 → 3.

## ZH-ARCH-DESIGN-SYSTEM-01 — Auditoría `design-system` (2026-09-28)

**Estado: COMPLETADO (parcial por diseño).** 26 ocurrencias clasificadas: A 2 · B 3 · C 0 · D 21 · E 0. Solo se corrigieron A/B inequívocas; sin tokens nuevos, sin estilos inline, checker intacto.
- A (2): badge crudo `badge badge--neutral` → `<Badge variant="neutral">` en `PurchaseCreditNoteDetailPage`/`PurchaseCreditNoteFormPage` (DOM idéntico).
- B (3): `--color-danger` → `--color-error` (`purchase-reception.css`, `purchases-invoice.css`); `--color-surface-secondary` → `--color-surface-container-low` (`purchase-reception.css`, mismo token del bloque XML crudo del monitor de comprobantes). Efecto visual: el color/fondo previsto vuelve a aplicarse (antes el `var()` indefinido lo anulaba).
- D (21, pendientes): custom properties locales de componente — `--account-tree-*` ×9 (`ChartOfAccountsPage.css`), `--sfl-*` ×8 (`sales-product-card.css`, contrato de grid probado), `--batch-progress` ×1 (valor dinámico de `ZhBatchProgress`) — requieren decisión de regla sobre custom properties locales; `--color-surface-subtle` ×1 (`CajaPage.css`, fallback `transparent` explícito) y `--text-title-md-*` ×2 (`expense-documents.css`, sin escala `title-md`; los totales de otros módulos usan tamaños distintos) requieren decisión visual.
- Baseline: 186 → 181; `design-system` 26 → 21.

## ZH-ARCH-BACKEND-SUBSCRIBER-03 — Backfill SRI de PaymentMethod por tenant (2026-09-28)

**Estado: COMPLETADO.** Última violación `backend-subscriber-rules` resuelta rediseñando el scope, no solo el nombre del bypass.
- Antes: `PaymentMethods.IgnoreQueryFilters()` cargaba filas trackeadas de TODOS los tenants y un único `SaveChanges` las persistía juntas.
- Después: `AsPlatformQuery()` solo para descubrir `TenantId`s con filas pendientes (proyección `Distinct`, sin tracking); por cada tenant `JobExecutionContext.Begin(tenantId)` → lectura con filtro global + `TenantId` explícito → `SaveChanges` por tenant → `ChangeTracker.Clear()`. TenantId nunca viene de input externo; reglas funcionales intactas (no pisa mapeos manuales, valida contra catálogo activo, idempotente). Entrada retirada de la allowlist de `IgnoreQueryFiltersAuditTests`.
- PostgreSQL (Testcontainers, Tenant A/B): interceptor de `SaveChanges` exige un único tenant por guardado e igual al `JobTenantContext` activo; A y B solo actualizan sus filas; mapeo manual preservado; segunda corrida 0 escrituras. El mismo test falla contra la implementación anterior ("un SaveChanges nunca mezcla tenants").
- `PaymentMethodSriMappingBackfillServiceTests`: fixture pasó de `ICurrentTenant` fijo en Tenant A a `CurrentTenantService` sin HttpContext (idéntico al CLI real); aserciones sin cambios.
- Baseline: 187 → 186; `backend-subscriber-rules` 1 → 0.

## ZH-ARCH-BACKEND-SUBSCRIBER-02 — `IgnoreQueryFilters()` directo → `AsPlatformQuery()` (2026-09-28)

**Estado: COMPLETADO.** 12 violaciones `backend-subscriber-rules` auditadas: 11 A (reemplazables), 1 B (legítima, pendiente), 0 C. Sin cambio funcional: `AsPlatformQuery()` es exactamente `IgnoreQueryFilters()` sobre el `DbSet` (mismo SQL); ningún filtro TenantId/CompanyId se quitó.
- A (11 en 8 archivos, `Seeding/`): `BankCatalogBackfillService`, `CashRegisterAccountingAccountBackfillService`, `DocumentFlowPolicyBackfillService`, `ExpensesCatalogBackfillService`, `PurchaseReturnPostingRemediationService` (scan de descubrimiento cross-tenant seguido de `JobExecutionContext.Begin` y/o revalidación TenantId+CompanyId antes de escribir); `DocumentFlowPolicyBootstrapStep`, `ExpensesCatalogBootstrapStep` ×4, `PrecisionPolicyBootstrapStep` (`Where` explícito TenantId+CompanyId del `CompanyBootstrapContext`). Se retiraron sus 8 entradas stale de la allowlist de `IgnoreQueryFiltersAuditTests` (reintroducir `IgnoreQueryFilters()` ahí vuelve a fallar).
- B (1, sin tocar): `PaymentMethodSriMappingBackfillService` carga entidades trackeadas de todos los tenants y las guarda sin scope por tenant; migrarla requiere refactor con `JobExecutionContext.Begin` por tenant + validación PostgreSQL — resuelto en ZH-ARCH-BACKEND-SUBSCRIBER-03.
- Baseline: 198 → 187; `backend-subscriber-rules` 12 → 1. Otras categorías sin cambios.
- Verificación: Infrastructure focalizados 52/52 (incluye `IgnoreQueryFiltersScopeIntegrationTests` PostgreSQL, `IgnoreQueryFiltersAuditTests`, `ExpensesCatalogBootstrapStepTests`); `CajaVentasEndToEndTests` 5/5; `ERP.Architecture.Tests` 116/116; `npm run architecture:check` PASS, 187, 0 nuevas.

## ZH-ARCH-DUPLICATE-SERVICES-SCANNER-01/02 — Scanner DI con namespace completo (2026-09-28)

**Estado: COMPLETADO.** Resuelve el hallazgo pendiente de ZH-ARCH-DUPLICATE-SERVICES-01: el checker ahora escanea el `DependencyInjection.cs` productivo (registros con namespace completo). Sin cambios en DI productivo.
- Identidad: cada registro conserva `fullName` + `simpleName`. Simple y calificado de la misma interfaz cuentan juntos; homónimas en namespaces distintos cuentan por separado; registro sin calificar junto a ≥2 homónimas falla cerrado (ambigüedad). Allowlist por nombre simple, cerrada.
- Auditoría: 8 hallazgos nuevos → 7 strategy/plugin intencionales (consumidor `IEnumerable<T>` confirmado) agregados a `allowedMultiRegistration`: `IJournalEntrySourceModuleResolver`, `IElectronicDocumentDataProvider`, `ISalesInvoiceEmissionStrategy`, `IElectronicDocumentXmlBuilder`, `IElectronicDocumentSchemaValidator`, `IPricingAdjustmentStrategy`, `IGlobalBootstrapStep`; 1 falso positivo (`ISriTaxResolver`: dos interfaces distintas Common/Purchases) eliminado por la nueva identidad. 0 duplicaciones reales.
- Baseline sin cambios (198): los hallazgos nunca estuvieron en el snapshot. `check-duplicate-services.test.mjs` (9 tests) se agregó a `npm run architecture:check`.

## ZH-ARCH-DUPLICATE-SERVICES-01 — `duplicate-services` en 0 (2026-09-28)

**Estado: COMPLETADO.** Las 2 violaciones (`IRideTemplate`/`IRideXmlParser` ×2 en `RetentionWiringDependencyInjectionTests`) eran multi-registro intencional: patrón strategy/plugin consumido vía `IEnumerable<T>` por `RideTemplateResolver`/`RideXmlParserResolver`, espejo exacto del wiring productivo (Invoice + CreditNote). Sin cambios de código productivo ni de test.
- Corrección: ambas interfaces se agregaron por nombre explícito a `backend.duplicateServices.allowedMultiRegistration` (mismo mecanismo que `ICompanyBootstrapStep`/`IImportProcessor`); la regla sigue activa para el resto.
- Baseline: 200 → 198; `duplicate-services` 2 → 0 (se retiró solo esas 2 identidades).
- Hallazgo (resuelto en ZH-ARCH-DUPLICATE-SERVICES-SCANNER-01/02): el regex del checker no detecta registros con nombre calificado (`AddScoped<ERP.Application...IFoo, ...>`), por lo que `DependencyInjection.cs` productivo no se escanea de hecho. Endurecerlo expondría multi-registros hoy invisibles (p. ej. `IJournalEntrySourceModuleResolver`, `IPricingAdjustmentStrategy`, `ISriTaxResolver`) que requieren clasificación caso a caso.
- Verificación: tests Node 32/32; `npm run architecture:check` PASS, 198/198, 0 nuevas; `ERP.Infrastructure.Tests` Ride 92/92; `ERP.Architecture.Tests` 116/116.

## ZH-ARCHITECTURE-DEBT-IGNOREQUERYFILTERS-02 — Scope seguro y precisión del scanner (2026-09-28)

**Estado: COMPLETADO.** Se validó con PostgreSQL 16 real vía Testcontainers; no se modificaron los usos productivos legítimos ni se hizo commit.
- Reemplazos: `BankCatalogSeeder` consulta con filtro tenant normal; el backfill de cajas entra a contexto tenant/company, revalida TenantId+CompanyId y `AccountingAccountId == null`; la deduplicación de PurchaseReturn exige TenantId+CompanyId. Dry-run/apply se conserva.
- Scanner: ignora comentarios C#/XML preservando líneas, no se acusa a sí mismo por la expresión exacta de `PlatformQueryAccessor.AsPlatformQuery`, pero detecta llamadas adicionales en ese archivo. Solo `backend-subscriber-rules` excluye proyectos `*.Tests`; otros checks no cambian. Se retiró la entrada stale de `BankCatalogSeeder` en la allowlist de auditoría.
- Baseline: 244 → 217 por 25 findings de test de esta regla y 2 falsos positivos; PostgreSQL aprobó los 4 reemplazos productivos y el snapshot bajó 217 → 213. `backend-subscriber-rules` quedó en baseline 12; otras categorías no cambiaron.
- PostgreSQL: 1/1 suite focalizada con Tenant A/Companies A-B y Tenant B/Company C. Bancos 9 por tenant; cajas con cuenta exclusiva de su company; asignación concurrente entre scan y reconsulta preservada; remediación dry-run no escribe, apply postea dentro del scope y segunda corrida deduplica idempotentemente.
- Verificación final: tests Node checker/ratchet 13/13; `IgnoreQueryFiltersAuditTests` 1/1; `npm run architecture:check` PASS, baseline/current 213, 0 nuevas, 0 resueltas; `duplicate-services` permanece en 2; `git diff --check` limpio.

## ZH-ARCHITECTURE-RATCHET-01 — Ratchet del gate de arquitectura (2026-09-28)

**Estado: COMPLETADO.** `npm run architecture:check` es el gate único: PASS si no aparecen violaciones nuevas frente a `tools/architecture/architecture-baseline.json`; la deuda histórica sigue visible y no se aprueba. El baseline inicial versionado es 244 (module-boundaries 61, css-prefixes 112, design-system 26, backend-subscriber-rules 43, duplicate-services 2). No existe actualización automática del snapshot.
- Tests del ratchet 9/9; gate real 244 históricas, 0 nuevas, 0 resueltas; score/status de arquitectura permanece crítico. Baseline inválido falla cerrado.
- Incluye caso de regresión de archivo nuevo, swap de finding, reducción de deuda y baseline malformado.

## ZH-ARCHITECTURE-GATE-RECOVERY-01 — Resolución masiva XML y recuperación del baseline del gate (2026-09-28)

**Estado: cambio modular corregido; gate absoluto BLOQUEADO por baseline histórico.** La resolución masiva XML de productos queda en `b2041a06`; esta revisión no crea commit.

- **Baseline medido** en el padre `1db7108` frente al HEAD final: 244 violaciones en ambos; el commit original `b2041a06` elevaba temporalmente el total a 245 por un único `F-module-boundary` nuevo en `ResolvePendingProductsModal.tsx` (`purchases` importaba directamente `items/api/itemService`). Se reemplazó por `items/facades/itemLookupFacade`, la superficie pública read-only existente y ya usada por `ProductPicker`. No se añadió excepción ni se duplicó servicio.
- **Comparación por check**: `module-boundaries` 61→61 (delta 0); `css-prefixes` 112→112 (0); `design-system` 26→26 (0); `backend-subscriber-rules` (`IgnoreQueryFilters()`) 43→43 (0); `duplicate-services` 2→2 (0). No hay archivos nuevos con violaciones en el HEAD final. Las categorías históricas permanecen sin cambios.
- **Gate**: `npm run architecture:check` termina con 5 checks fallidos y 244 violaciones, las mismas del baseline (`architectureScore` 0; 21 warnings). El runner no admite baseline conocido; el gate exige cero absoluto. No se corrigieron usos históricos de `IgnoreQueryFilters()`, CSS, Design System ni servicios duplicados.
- **Pruebas**: modal/utilidades frontend 35/35; `ResolvePurchaseReceptionLinesHandlerTests` 8/8; `npm run lint` 0 errores (35 warnings); `npm run build` correcto. No se declaran pruebas PostgreSQL verdes: el intento de `ResolvePurchaseReceptionLinesIntegrationTests` no pudo ejecutar sus 8 casos porque Docker Desktop no estaba disponible (falta el pipe `dockerDesktopLinuxEngine`). Queda **pendiente de validación de integración**.
- **FRONTEND-BUNDLE-SPLIT-01 (deuda separada, sin optimizar en esta tarea)**: el build midió el chunk principal `index` en 699.69 kB (gzip 177.62 kB), `PurchasesPage` 168.63 kB y `SalesPage` 111.23 kB. Hay lazy loading en `main.tsx` y `routes/lazyPage.tsx`; Vite aún advierte que un chunk supera 500 kB.

## COMPRAS-METODO-ZH-01A2 — Atomicidad de domain events ante reintentos de Kardex (2026-09-27)

**Estado: COMPLETADO (sin commit).** Infraestructura compartida; sin cambios de reglas de negocio.
- **Causa**: `ErpDbContext.SaveChangesAsync` retiraba los domain events de los agregados ANTES del primer guardado; ante un conflicto de secuencia de Kardex el rollback dejaba los eventos borrados y el reintento de `StockRepository` guardaba documento + Kardex + outbox sin publicar → documento Authorized/Confirmed sin asiento, caja ni efectos de handlers (evidencia `TEMPDIAG_R1`).
- **Fix**: los eventos se retiran solo tras el primer guardado exitoso; un fallo previo a publicar desvincula los `OutboxMessages` del intento (unidad de trabajo restaurada: reintento = 1 outbox + 1 publicación). `ErpDbContext.LastSaveFailureIsRetryable` indica si el fallo fue previo a publicar; `SaveChangesWithSequenceRetryAsync` solo reintenta en ese caso — un fallo posterior (cambios ya aceptados de una transacción revertida) se propaga, nunca se reintenta a medias.
- Evidencia: tests nuevos Ventas/Compras (retry con todos los efectos exactamente una vez) y fallo post-publicación no reintentado — los 3 fallan sin el fix; `TEMPDIAG_R1` pasa · Domain 1221 · Application 2310 · Architecture 116 · API 499/500 (baseline) · Infrastructure 915/922 (7 preexistentes/ajenos: 4 migraciones del squash, 2 trigger compra↔gasto, `TEMPDIAG_R2` por diseño).

## COMPRAS-METODO-ZH-01A — Integridad de compra confirmada (2026-09-27)

**Estado: COMPLETADO (sin commit).** Sin cambios de UX, esquema ni frontend.
- **Atomicidad**: el asiento `Purchases/InvoiceReceived` es obligatorio — un fallo lanza `PurchasePostingFailedException` y `ErpDbContext` revierte Compra + Kardex + CxP + Contabilidad juntos; Confirm devuelve error de validación con el código del motor (mismo criterio que Ventas/Gastos/Pagos).
- **Costo SSOT**: Confirm ya no reprorratea flete/otros costos sobre todas las líneas (preserva la distribución revisada); el evento expone `CostSubtotal` (= Σ `TotalLineCost`, neto de descuentos + flete/otros) y la regla contable lo usa como Subtotal — Debe cuadra con GrandTotal con descuentos y flete, sin segunda fórmula.
- **Kardex**: varias líneas del mismo ítem/bodega en una compra encadenan secuencia/costo promedio con los movimientos pendientes; `SourceDocLineId` por línea; el reintento de secuencia no duplica `CurrentStock` nuevo.
- **Conciliación XML** (`PurchaseXmlConfirmationGuard`, antes de cualquier efecto): bloquea recepción ajena (empresa/sucursal/proveedor/comprobante), XML con líneas sin procesar o con errores, Σ bases ≠ `totalSinImpuestos`, snapshot ≠ XML, líneas XML omitidas/duplicadas/sin vínculo (incluye "eliminar línea y repartirla como flete") y líneas con cantidad/precio/descuento/impuestos distintos. Informativo (no bloquea): advertencias de matching, diferencia `importeTotal` vs líneas (redondeo/propina, ya visible como RoundingDifference). Acepta la recepción `Processed` por esta misma compra (flujo real).
- Evidencia: Domain 1221 · Application 2310 · Architecture 116 · API 499/500 (baseline `PG_unique_business_partner_identification_enforced`) · Infrastructure 901/919 antes de ajustar fixtures → 10 tests con fixture corregido (29/29 focalizados); restantes 8 preexistentes/ajenos (4 migraciones eliminadas en el squash, 2 trigger compra↔gasto ausente tras el squash, 2 TEMP-DIAG de ZH-SALES-CONCURRENCY-POSTING-AUDIT-01) · `architecture:check` 244 = `HEAD`.
- GAPs abiertos: ~~reintento de secuencia de Kardex pierde los domain events~~ (cerrado en 01A2); trigger `uq_purchase_expense_access_key` ausente del baseline; flete desde línea XML requiere conciliación trazable (modelo) — hoy se bloquea.

## ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — UI de solicitudes de efectivo + integración con Pago a proveedor — SPAY-02E CLOSED (2026-09-26)

**Estado: COMPLETADO (sin commit).**
- **Pantalla** `/treasury/cash/funding-requests` (Caja > Solicitudes de efectivo): pestañas por permiso — Pendientes/Historial (`caja.funding-requests.view`, bandeja de la sucursal activa) y Mis solicitudes (`supplier-payments.create`); filtros estado/caja y paginación en servidor. Detalle `/treasury/cash/funding-requests/:id`: resumen (proveedor, solicitante, caja, sucursal, fecha, estado), origen de fondos (caja/banco con medio, cuenta, fecha, referencia), aplicaciones a CxP (Compra/Gasto, documento, cuota) y resolución con enlace al pago generado. Acciones solo por `canFulfill/canReject/canCancel` del servidor; motivo obligatorio en rechazar/cancelar; confirmación "Confirmo que estoy entregando $X…". Sin GUIDs visibles.
- **Pago a proveedor**: mismo formulario. Efectivo de una caja con sesión abierta de OTRO usuario ⇒ botón "Solicitar efectivo" (si no, "Pagar") y la confirmación crea UNA solicitud con el pago completo (banco + caja), con ClientRequestId estable por intento; nunca el pago. Una sola línea de efectivo por solicitud. Tras crear: "Solicitud de efectivo creada" + [Ver solicitud].
- **Detección de caja ajena**: ampliación mínima del listado existente de cajas de la sucursal activa (`GET /cash-registers`): `HasOpenSession`, `OpenSessionControlledByCurrentUser`, `OpenSessionUserName` (1 consulta de sesiones + 1 de usuarios). El backend revalida siempre.
- **Caja**: aviso "Solicitudes de efectivo: N [Ver]" con el `totalCount` de la bandeja filtrada por Pending (sin endpoint de conteo).
- Detalle de solicitud ahora incluye `BranchName`. `/api/cash-funding-requests` añadido a la allowlist del Platform guard (`tools/ci/platform-guard-config.json`).
- Evidencia: Domain 1220 · Application 2290 · Architecture 116 · API 499/500 (baseline `PG_unique_business_partner_identification_enforced`) · PostgreSQL focalizado (solicitudes/pagos/caja/saldos/posting/concurrencia) 263/264 — `SalesInvoiceAuthorizedPostingIntegrationTests.Dos_publicaciones_concurrentes…` intermitente bajo carga (3/3 verde aislado, fuera de 02E) · vitest 2516/2516 · `tsc -b`/build OK · lint 0 errores (35 warnings preexistentes) · `architecture:check` 244 = `HEAD`.
- Limitación preexistente: el formulario de pago obtiene las cajas con `caja.view`; un solicitante sin ese permiso no puede elegir caja (igual que antes para pagos en efectivo).

## ZH-CASH-FUNDING-REQUEST-API-02E-D — API, modelo de lectura y permisos de solicitudes de efectivo (2026-09-26)

**Estado: COMPLETADO (sin commit).** Sin UI. Lógica financiera de 02E-C sin cambios.
- **Endpoints** `api/v1/cash-funding-requests`: `POST` (`supplier-payments.create`), `GET` bandeja (`caja.funding-requests.view`, empresa + sucursal activa), `GET mine` (`supplier-payments.create`, solicitante forzado al usuario autenticado), `GET {id}` (solicitante O `view` sobre la sucursal activa, resuelto en Application con `IRuntimePermissionAuthorizer`; si no → 404), `POST {id}/fulfill|reject` (`caja.funding-requests.fulfill` + control real de la sesión), `POST {id}/cancel` (`supplier-payments.create` + solicitante). Los comandos responden el detalle re-leído.
- **Modelo de lectura**: `CashFundingRequestListItemDto` + `CashFundingRequestDto` (detalle con origen efectivo/banco, aplicaciones a CxP con documento/origen/cuota, `CanFulfill/CanReject/CanCancel` derivados en servidor). Nunca expone payload, huella ni ClientRequestId. Nombres en lote (proveedores, usuarios, cajas, cuentas, formas de pago, cuotas vía `IAccountsPayableRepository.GetInstallmentRefsByIdsAsync`). Orden `RequestedAt` desc, `Id` desc; contador = `TotalCount` con filtro `Pending`.
- **Permisos/menú**: `caja.funding-requests.view|fulfill` vía NavItem "Solicitudes de efectivo" (`/treasury/cash/funding-requests`) bajo Caja; nada bajo Cuentas por pagar. La ruta frontend llega con la UI (fase siguiente).
- Evidencia: PostgreSQL solicitudes 43/43 (13 nuevas de modelo de lectura) · Application focalizadas 404/404 · API focalizadas 36/36 · Domain Kernel 44/44 · Architecture 116/116 · `architecture:check` 244 = `HEAD`.

## ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — Workflow de solicitudes de efectivo (2026-09-26)

**Estado: COMPLETADO (sin commit).** Sin endpoints, permisos, menú ni UI.
- **Create**: exactamente una línea de efectivo; la caja debe tener sesión abierta de OTRO usuario (si el usuario la controla → pago directo) en la sucursal activa (guard oficial). Transacción con CashSession FOR UPDATE; validación completa del pago vía `ISupplierPaymentRegistrar.ValidateAsync` ejecutada por quien controla la sesión (incluye chequeo temprano de efectivo, sin reserva). Sin efectos financieros. Idempotencia ClientRequestId + PayloadHash (mismo → misma solicitud; distinto → 409).
- **Fulfill**: CashSession FOR UPDATE (abierta, misma empresa/sucursal/caja, cajero la controla, guard oficial) → request FOR UPDATE → Pending → versión + huella + coherencia del snapshot (fail-closed) → núcleo de pagos (originador = solicitante, ejecutor = cajero) → `Fulfill` → commit. Cualquier fallo: rollback total, la solicitud queda Pending. Reintento sobre Fulfilled → misma respuesta, sin re-ejecutar.
- **Reject** (cajero, motivo) y **Cancel** (solo solicitante, motivo): mismo orden de locks, sin efectos financieros.
- **Cierre de caja**: ahora transaccional con CashSession FOR UPDATE; cancela en la misma transacción las solicitudes Pending de la sesión (FOR UPDATE, orden por Id, motivo "Caja cerrada").
- Núcleo: `ISupplierPaymentRegistrar` separa `ValidateAsync` (sin efectos) de `RegisterAsync` (valida + ejecuta); pago directo sin cambios.
- Evidencia: PostgreSQL workflow 19/19 (A–J, snapshot manipulado/versión, empresa, sucursal, ownership, idempotencia, Close∥Fulfill, SupplierCredit residual, fallo de posting) · PostgreSQL focalizadas pagos/caja/saldos 115/115 · Domain 1219 · Application 2290 · Architecture 116 · API focalizadas 32/32 · `architecture:check` 244 = `HEAD`.

## ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — Base de solicitudes de efectivo (2026-09-26)

**Estado: COMPLETADO (sin commit).** Sin endpoints, permisos, menú ni UI (fases siguientes).
- **Dominio**: `CashFundingRequest` (Caja) — Pending/Fulfilled/Rejected/Cancelled; Pending único mutable, terminales no reutilizables; efectivo > 0, total ≥ efectivo, una sola caja/sesión objetivo; `SupplierPaymentId` solo en Fulfilled; motivo obligatorio en Rejected/Cancelled.
- **Snapshot**: `CashFundingPaymentSnapshotV1` (contrato explícito, nunca el command CLR) en `payment_payload` jsonb + `payload_version` + `payload_hash` (SHA-256 de la forma canónica: orden fijo, camelCase, decimales sin ceros de relleno; estable ante la normalización de jsonb).
- **Actores**: `SupplierPayment.ConfirmedByUserId` (nuevo; backfill = `created_by`). Originador = `CreatedBy`; ejecutor = `ConfirmedByUserId`, dueño del ownership de caja, del `CashMovement` y de la mutación de CxP.
- **Núcleo extraído**: `ISupplierPaymentRegistrar` (Payables/Services) con contexto explícito de actores; no maneja la transacción (la abre/cierra el llamador). El pago directo delega en él sin cambios de comportamiento (rechazos tempranos siguen sin transacción vía `PrevalidateAsync`).
- **Persistencia**: tabla `cash_funding_requests` (migración `CashFundingRequestFoundation`), CHECKs de estado, índices (empresa+sesión+estado, empresa+solicitante+estado), únicos `(tenant, client_request_id)` y `supplier_payment_id` (filtrado), `xmin`. Repositorio con `GetByIdForUpdateAsync` (patrón oficial lock → recarga). Orden único de locks: CashSession → CashFundingRequest → resto.
- Evidencia: Domain 1219 · Application 2290 · Architecture 116 · PostgreSQL focalizadas 59/59 (repositorio nuevo + E2E de pagos + saldos a favor) · API focalizadas 32/32 · `architecture:check` 244 = `HEAD`.

## ZH-SUPPLIER-BALANCES-02D-FINAL-QA — SPAY-02D CLOSED (2026-09-26)

- Regresión: Domain 1206/1206 · Application 2279/2279 · Architecture 116/116 · API 486/487 (baseline `PG_unique_business_partner_identification_enforced`) · PostgreSQL focalizado 256/258 (baseline `PurchaseExpenseReprocessAfterCancelConstraintsTests.Trigger_cruzado_*`, fallan igual en `HEAD` limpio) · vitest 2495/2495 · `tsc -b`/build OK · lint 0 errores (35 warnings preexistentes, ninguno en archivos 02D) · `architecture:check` 244 = `HEAD` (mismo conjunto).
- Regresiones 02D corregidas: GUID del proveedor visible en los subtítulos de los modales Aplicar/Reembolso (ahora nombre) y en la descripción del `CashMovement` de reembolso/reversa (texto sin GUID; trazabilidad por `ReferenceId`).
- Pendientes fuera de 02D: `PurchaseExpenseExclusivityTests.cs` no compila (migración eliminada en el squash); `ISupplierCreditRepository.GetBySourcePurchaseReturnIdAsync` sin uso desde antes de 02D; bodegas N+1 en el detalle de devolución; lock pesimista de Gastos; permisos `supplier-balances.*`; SupplierPaymentRefund/CashFundingRequest/BankMovement/conciliación.

## ZH-SUPPLIER-BALANCES-CROSS-LINKS-02D-F — Navegación contextual CxP ↔ Pago ↔ Devolución ↔ Saldos a favor (2026-09-26) — cierra SPAY-02D

**Estado: COMPLETADO (sin commit).** Saldos a favor sigue siendo la ÚNICA pantalla que administra el saldo.
- CxP: `AccountsPayableDetailDto.SupplierAvailableCredit` (total disponible, nº de saldos abiertos, Id si es único) vía `ISupplierCreditRepository.GetOpenBalanceBySupplierAsync` (1 agregado + 1 Id solo si hay uno; empresa operativa). Bloque "Este proveedor tiene $X a favor" con "Ver saldos" (`/suppliers/credits?supplierId=`) y "Aplicar saldo" (detalle del saldo `?applyTo=<CxP>` → modal oficial con la CxP preseleccionada; con varios saldos, listado filtrado que propaga `applyTo`). Sin import frontend payables→finance.
- Devolución: `PurchaseReturnDto.SupplierCreditAmount` (dominio) + `SupplierCreditId` (detalle, por FK de origen) → "Saldo a favor generado: $X [Ver saldo]".
- Pago: botón renombrado a "Ver saldo a favor" (misma navegación al detalle correcto).
- Listado de saldos inicializa el filtro de proveedor desde `?supplierId=` (el buscador oficial hidrata el nombre por Id); sin sync continuo URL↔filtros (no existe ese patrón).
- Evidencia: vitest finance/payables/purchase-return/supplier-payments 125/125 · `tsc -b`/eslint/build OK · `architecture:check` 244 = `HEAD` · Application 2279 · Domain 1206 · Architecture 116 · API focalizadas 83/83 · PostgreSQL read-model 6/6 (agregado + aislamiento por empresa).
- Deuda preexistente anotada (no tocada): `GetPurchaseReturnByIdHandler` resuelve bodegas con una consulta por bodega distinta.

## ZH-SUPPLIER-BALANCES-UX-02D-E — Pantalla "Saldos a favor de proveedores" (2026-09-26)

**Estado: COMPLETADO (sin commit).** Misma ruta `/suppliers/credits` y mismos Id/permisos/LabelKey del menú; solo cambia la etiqueta (es/en + NavItem).
- Listado: Proveedor, Origen ("Anticipo / pago mayor" / "Devolución de compra"), Documento (enlace a `/supplier-payments/:id` o `/purchases/returns/:id`), Fecha, Monto original, Saldo disponible, Estado, Ver. Filtros server-side (`SupplierSearchSelect`, origen, estado; default Abiertos; "Restablecer filtros"). Eliminado el botón "Cuentas bancarias".
- Detalle: Resumen (proveedor, origen, documento con enlace, fecha, montos, estado), Acciones solo con saldo > 0 ("Aplicar a CxP", "Registrar reembolso"), Historial cronológico (CxP Compra/Gasto con enlace a `/payables/:id`; Caja/Banco + destino + medio legible + referencia; reversas indican el movimiento revertido y el motivo cuando existe).
- Medio de pago legible: `PaymentMethodName` en el read-model (catálogo oficial, 1 consulta solo si hay reembolsos). Etiqueta de origen de CxP movida a `lib/payableOrigin` (contrato compartido, sin import cruzado nuevo).
- Evidencia: vitest finance/payables/supplier-payments 100/100 · `tsc -b`/eslint/build OK · `architecture:check` 244 = `HEAD` · backend Domain 1206 · Application 2274 · Architecture 116 · read-model PostgreSQL 5/5 · controller 19/19.

## ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — Lectura enriquecida de saldos a favor (2026-09-26)

**Estado: COMPLETADO (sin commit).** Sin UI nueva (02D-E).
- DTOs (máx. 2): `SupplierCreditListItemDto` (nuevo) + `SupplierCreditDto` (detalle enriquecido: proveedor, origen tipo/Id/número/fecha, movimientos cronológicos con CxP destino, datos del reembolso, reversas enlazadas, autor). Proyección única `SupplierCreditReadModel` para list/detail/comandos; Apply/ReverseApplication responden con el detalle re-leído.
- `GET /finance/supplier-credits`: filtros en BD `supplierId`, `sourceType`, `isOpen`; orden estable (CreatedAt desc, Id desc); consultas fijas por página (sin N+1).
- Origen: devolución → `ReturnNumber` + `AuthorizedAtUtc` convertido a fecha de la empresa (ADR-034); pago → `SystemNumber` + `PaymentDate` (antes número visible = recibo externo si existía).
- Evidencia: Application 2274 · Architecture 116 · PostgreSQL focalizadas 32/32 · API controller 19/19 · vitest finance/payables/supplier-payments 91/91 · `tsc -b`/eslint OK · `architecture:check` 244 = `HEAD`.

## ZH-SUPPLIER-CREDIT-APPLY-PAYABLES-02D-C — Saldo a favor aplicable a CxP de Compra y de Gasto (2026-09-26)

**Estado: COMPLETADO (sin commit).**
- `SupplierCreditPayableTarget` (única resolución, compartida por Apply/Reverse): Lock A según `AccountsPayable.OriginType` — Compra → `PurchaseInvoice.FinancialLock`; Gasto → sin advisory lock (no existe uno oficial; concurrencia optimista `xmin` de `AccountsPayable`, choque → SC-010); otro origen → rechazo fail-closed. Moneda: Compra = `PurchaseInvoice.CurrencyCode`, Gasto = `Company.CurrencyCode`. `IAccountsPayableRepository.GetOriginIdAsync` → `GetOriginAsync` (tipo + Id).
- **Seguridad corregida**: la CxP destino se cargaba solo por tenant y nunca se validaba `CompanyId` (un saldo de la empresa A podía aplicarse a una CxP de la empresa B del mismo tenant/proveedor). Ahora se rechaza como inexistente (Apply y Reverse).
- **Integridad**: `CancelExpenseDocument` bloquea si la CxP del gasto tiene saldo a favor aplicado (espejo de PI-CANC-02 de Compras) — sin esto, anular el gasto dejaba el saldo consumido sin reversa posible (SC-014).
- Frontend: modal Aplicar lista CxP pendientes y parcialmente pagadas de Compra y Gasto (antes solo Compra y solo `pending`), con "Compra/Gasto · documento — Saldo pendiente"; etiqueta de origen única en `payablesService.payableOriginLabel`.
- Evidencia: Domain 1206 · Application 2274 · Architecture 116 · PostgreSQL focalizadas 31/31 · API focalizadas 33/33 · vitest finance+payables 27/27 · `tsc -b`/eslint OK · `architecture:check` 244 = `HEAD` (mismo conjunto).

## ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — Asiento fail-closed del reembolso de SupplierCredit (2026-09-26)

**Estado: COMPLETADO (sin commit).** Cierra el 6.º gap de PostingRule (traductor real sin regla sembrada).
- **Causa raíz**: `SupplierCreditRefunded(Reversed)PostingTranslator` publicaban `FactType="SupplierCreditRefunded:{DestinationCodeSnapshot}"` (una regla por caja/banco) que nadie sembraba → `RULE_NOT_FOUND` + `LogWarning`; el saldo bajaba y la caja/banco recibía el dinero **sin asiento**.
- **Posting**: FactType canónico único `Purchases/SupplierCreditRefunded` (regla: Haber `1.1.03.004` `GrandTotal`; Debe Caja/Banco dinámico vía `PostingFact.Allocations` con `SupplierCreditRefundTransaction.AccountingAccountId`, congelada desde `CashRegister`/`CompanyBankAccount.AccountingAccountId`). Reversa `Purchases/SupplierCreditRefundReversed` = espejo exacto con la cuenta heredada (nunca la vigente del destino).
- **Fail-closed**: fallo de posting o transacción inexistente ⇒ `SupplierCreditRefundPostingFailedException` ⇒ rollback total (movimiento, transacción, `CashMovement`, asiento). Sin warning silencioso.
- **Validaciones**: medio ↔ destino (efectivo ⇒ caja, bancario ⇒ banco, crédito prohibido) vía `PaymentMethodDestinationPolicy` (extraída de 02A, compartida con `RegisterSupplierPayment`, mismos mensajes); moneda del crédito = `Company.CurrencyCode` (banco/caja no tienen moneda propia).
- **Reversa desde la UI**: la ruta `/refund/{movementId}/reverse` recibía el Id del movimiento pero el handler solo buscaba por Id de transacción (la UI nunca podía reversar); ahora acepta ambos.
- **Reglas**: seed de empresa nueva + backfill automático (no Production) vía `MinimalPostingRules`. Production: `dotnet run -- backfill-supplier-credit-refund-posting-rules [apply]` (dry-run por defecto; solo CREA faltantes; nunca modifica reglas existentes; reglas obsoletas `SupplierCreditRefunded:{código}` se reportan, no se borran). Sin migraciones.
- **Evidencia**: Domain 1206 · Application 2270 · Architecture 116 · Infrastructure 847/855 (6 fallos preexistentes, idénticos en `HEAD` limpio: migraciones de precisión y triggers compra/gasto; 2 intermitentes bajo carga que pasan 9/9 aislados) · focalizadas 02D-B 49/49 (PostgreSQL real) · API 485/486 (mismo fallo preexistente `PG_unique_business_partner_identification_enforced`) · `architecture:check` 243 = `HEAD`.
- **Deuda detectada (no corregida, fuera de alcance)**: `ERP.Infrastructure.Tests/Persistence/Purchases/PurchaseExpenseExclusivityTests.cs` referencia la migración `ExpensesFromPurchaseReception`, eliminada por el squash `4cbc4b12` → `ERP.Infrastructure.Tests` no compila en `main`.

## ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — Remanente no aplicado como anticipo (2026-09-26)

**Estado: COMPLETADO (sin commit).** [ADR-035](docs/decisions/ADR-035-supplier-payment-unapplied-advance.md). `SupplierCredit` = SSOT del anticipo; sin `SupplierAdvance` ni otro libro.

- **SupplierPayment**: Σ aplicaciones ≤ total; `AppliedAmount`/`UnappliedAmount` derivados (no persistidos). Remanente ⇒ `ConfirmUnappliedAmount` obligatorio (Application rechaza antes de abrir transacción; dominio revalida). Matriz: aplicaciones cubiertas al 100%, ningún medio sobre-distribuido; el remanente de cada medio = `Amount − Σ allocations`.
- **Setting** `payables.allow_supplier_payment_without_payable` (org_settings, default false, pipeline OperationalPreferences + toggle en /settings/operations → Cuentas por pagar): solo gobierna cero aplicaciones; el anticipo por sobrepago existe siempre. Expuesto al formulario por `GET /api/v1/supplier-payments/policy` (permiso `supplier-payments.create`).
- **SupplierCredit generalizado**: `SourcePurchaseReturnId?` XOR `SourceSupplierPaymentId?` (CHECK `chk_supplier_credits_exactly_one_source`, índice único filtrado por pago), `SourceType` derivado; nace por exactamente el remanente en la misma transacción, sin evento contable. DTO: `SourceType`/`SourceSupplierPaymentId`/`SourceDocumentNumber`. Nuevo movimiento de sistema `SourcePaymentReversed`.
- **Posting**: `Payables/SupplierPaymentConfirmed` = CxP `AppliedToPayable` + `1.1.03.004` `SupplierCredit` (Reversed espejo). Corrección de la forma previa exacta en `AccountingBootstrapStep` (conserva Id de línea). Regla sin línea de anticipos + remanente ⇒ rechazo fail-closed (`IsAmountKindConfiguredAsync`). **Production no corre el backfill**: los pagos con remanente se rechazan con mensaje claro hasta actualizar la regla; los pagos exactos no cambian.
- **Reversa**: con anticipo íntegro (`SupplierCredit.IsIntact`) → Lock B antes de caja, anula el crédito (`SourcePaymentReversed`) + asiento inverso exacto; aplicado/reembolsado → rechazo sin efectos.
- **Migración** `SupplierPaymentUnappliedAdvance` (columna nullable + FK Restrict + CHECK + índice; sin SQL de datos).
- **Frontend**: resumen Total/Aplicado/Anticipo, aviso "Este pago quedará pendiente de aplicar.", modal "El pago supera el saldo que puede aplicarse en $X" con Aplicado a CxP / Anticipo proveedor y "Confirmar pago"; detalle del pago con enlace al anticipo; listado/detalle de créditos con columna Origen.
- **02C-PROD-CLOSE**:
  - Setting movido a `payables.allow_supplier_payment_without_payable`: grupo `Payables` propio (`PayablesPreferences`/`PayablesPreferencesDto`/`PayablesPreferencesInput`, `PayablesConfigurationDefinitions`), tab "Cuentas por pagar". Una sola key, sin alias.
  - Empresa nueva: nace canónica (test).
  - Production: comando `backfill-supplier-payment-posting-rules [apply]`, mismo mecanismo que `backfill-sales-invoice-posting-rule`. Diagnóstico `Canonical`/`Legacy`/`Custom`/`MissingRule`; solo `Legacy` exacto se actualiza, con Ids preservados y de forma idempotente. Las reglas personalizadas no se tocan y quedan como warning.
  - Dev: migraciones 02A/02B/02C aplicadas. Schema verificado (FK Restrict, CHECK `chk_supplier_credits_exactly_one_source`, índice único filtrado).
  - Dev: comando ejecutado — `Legacy -> Canonical: applied` y, en la segunda corrida, `Canonical`.
  - Dev: smoke real A–E con handlers reales sobre la BD dev (datos `SMOKE-02C`: 3 pagos, 2 créditos de 20/150, asientos balanceados con `1.1.03.004`); setting dejado en `false`.
- **Evidencia (PROD-CLOSE)**: Domain 1206 · Application 2262 · Infrastructure focalizadas 245/245 + mantenimiento 10/10 (Postgres real) · API 485/486 (1 fallo preexistente `PG_unique_business_partner_identification_enforced`) · Architecture 116 · vitest 2464/2465 (1 flaky no relacionado, `PermissionsAssignmentPage`, 3/3 aislado) · `tsc -b`, lint 0 errores, build OK · `architecture:check` 243 = `HEAD`.
- **Preexistente (sin cambio)**: `ERP.Infrastructure.Tests` no compila en `HEAD` por `PurchaseExpenseExclusivityTests` (se apartó temporalmente solo para ejecutar la suite).

---

## ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B — Autoridad sobre CashSession (2026-09-26)

**Estado: COMPLETADO (sin commit).** Regla SSOT `CashSession.IsControlledBy(userId)`: sesión `Open` y `UserId == ICurrentUser.UserId` (nunca por rol). El permiso decide QUÉ acción; la propiedad decide SOBRE QUÉ sesión — se exigen ambas.

- **Aplicada (fail-closed, sin bypass)** en: `RegisterSupplierPayment` (+ sucursal activa), `ReverseSupplierPayment`, `RegisterSupplierCreditRefund`, `ReverseSupplierCreditRefund`, `RecordCashMovement`, `CloseCashSession`. Mensaje: "La caja seleccionada está siendo operada por otro usuario." (`CashSessionOwnership`, Application). Sin solicitud automática (CashFundingRequest pendiente).
- **Auditoría `caja.manage`**: solo protege catálogos (cajas registradoras, motivos de movimiento); sin semántica sobre sesiones → no es excepción. Ninguna excepción documentada para operar sesiones ajenas.
- **Apertura** sin cambios (1 sesión Open por caja y por usuario).
- **Gate** `CashSessionOwnershipPolicyTests`: los 6 endpoints que mueven una sesión conservan su política de permiso (propiedad sin permiso no basta).
- **Test API ajustado**: `CajaVentasEndToEndTests` abría el turno con Admin y registraba con otro usuario (el comportamiento que 02B cierra) — ahora opera el dueño y se prueba por HTTP el rechazo de otro usuario con `caja.record` y del Admin cerrando un turno ajeno.
- **Impacto de negocio a decidir**: una reversa de pago/reembolso en efectivo solo la puede ejecutar quien opera hoy la sesión abierta de esa caja (Contabilidad/Cartera sin caja propia no puede reversar pagos en efectivo); el pago en efectivo desde una caja ajena queda bloqueado hasta CashFundingRequest.
- **02B-CLOSE**: `ReverseSupplierPayment` con efecto en caja exige sucursal activa validada por `IBranchAccessGuard` (el comando sigue company-scoped: la reversa bancaria no la exige) y `CashSession.BranchId == sucursal activa`. Auditoría: la reversa registra automáticamente un ingreso físico (`SupplierPaymentReversal`) en la sesión abierta ACTUAL de la caja — presupone que el efectivo volvió al cajón; la reversa bancaria presupone fondos devueltos (asiento Debe Banco sin evidencia bancaria). Separar reversa documental vs devolución real queda recomendado (ver entrega 02B-CLOSE), no implementado.
- **02B-FINAL — semántica de reversa**: `ReverseSupplierPayment` = corrección documental de una operación NO ejecutada (dinero que salió y regresó → futuro `SupplierPaymentRefund`, no implementado). Reversa siempre total; cada fuente debe calificar o se rechaza completa. Caja: `cashNotDeliveredConfirmed = true` + sesión ORIGINAL de la línea (`CashSessionId`, nuevo `ICashSessionRepository.GetByIdForUpdateAsync`, mismo lock/orden) abierta, controlada por el usuario y en la sucursal activa; compensación solo en esa sesión; sesión cerrada → "El efectivo salió de una sesión que ya está cerrada. Si el proveedor devolvió el dinero, registre una devolución de fondos." Banco: motivo estructurado `SupplierPaymentBankReversalReason` (NotExecuted/RejectedByBank/RegistrationError). Auditoría persistida en `supplier_payments` (`reversal_bank_reason`, `reversal_cash_not_delivered_confirmed`; migración `SupplierPaymentReversalSemantics`, 2 columnas nullable). Frontend: modal de reversa con checkbox de efectivo / motivo bancario y aviso de devolución de fondos.
- **Evidencia**: Domain 1180 · Application 2230 · Infrastructure focalizadas 53/53 (concurrencia 02A verde, sin doble posting) · API 484/485 (mismo fallo preexistente) · Architecture 116 · `architecture:check` 243 = `HEAD`.

---

## ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — Caja real y datos bancarios en pagos a proveedor (2026-09-26)

**Estado: COMPLETADO (sin commit).** Corrige los 2 defectos de ZH-SUPPLIER-PAYMENT-FUNDING-AUDIT-01. SSOT intacto: `SupplierPayment` = único documento/asiento; `SupplierPaymentMethodLine` = fuente del dinero; `CashMovement` = efecto operativo (nunca postea).

- **Caja**: fuente con medio `AffectsPhysicalCash` exige la `CashSession` Open de su caja (FOR SHARE) y registra `CashMovementType.SupplierPayment` (egreso, `ReferenceType=SupplierPayment`, `ReferenceId=SupplierPayment.Id`); la línea guarda `CashSessionId`/`CashMovementId`. Reversa → `SupplierPaymentReversal` (ingreso compensatorio en la sesión Open de la caja; el original no se borra). Pagos previos a 02A (sin movimiento) no compensan.
- **Refund SupplierCredit en efectivo**: signo invertido corregido (demostrado primero con test) — reembolso recibido = `ManualIncome`, su reversa = `ManualExpense`.
- **Medio ↔ destino** (PaymentMethod SSOT, fail-closed): efectivo ⇒ caja; resto ⇒ cuenta bancaria; `IsCreditAllowed` prohibido; `RequiresReference` ⇒ número de operación (cheque ⇒ número de cheque).
- **Datos bancarios**: nueva `TransactionDate` por línea — obligatoria y explícita en fuentes bancarias (validator + handler + dominio + check constraint; el backend nunca la completa con `PaymentDate`; el frontend la precarga visible y siempre la envía); sin backfill — nunca se guardan fechas bancarias inferidas; `ReferenceNumber` = único número de operación. Índice `ix_supplier_payment_methods_bank_reconciliation` (cuenta+fecha+referencia) para conciliación futura. Sin `BankMovement`.
- **Migración** `SupplierPaymentCashTransferHardening` (3 columnas nullable, FKs a `cash_sessions`/`cash_movements`, índice único filtrado por movimiento, check constraint; sin SQL de datos). BD dev: 0 filas en `supplier_payment_methods` → aplica sin bloqueo.
- **Frontend**: destinos filtrados por `affectsPhysicalCash` (expuesto en `PaymentMethodDto`), crédito excluido, fecha de transacción + número de operación en fuentes bancarias, columna "Fecha transacción" en el detalle, etiquetas de los nuevos tipos de movimiento de caja.
- **Gate**: `CashMovementPostingBoundaryTests` — Posting no puede depender de `CashMovement`/`CashSession`.
- **Evidencia**: Domain 1180/1180 · Application 2212/2212 · Infrastructure focalizadas 46/46 (Postgres real, migración aplicada) · API 484/485 (1 fallo preexistente en `HEAD`: `PG_unique_business_partner_identification_enforced`) · Architecture.Tests 110/110 · vitest 238 archivos/2450 · `tsc -b`, lint (0 errores), build OK · `architecture:check` 243 = `HEAD` (0 nuevas).
- **02A-CLOSE**: (1) refund en efectivo → `CashMovement` con `CashReferenceType.SupplierCreditRefund` + `ReferenceId` = transacción de reembolso ORIGINAL (también en la reversa) + `ReferenceNumber` = `ExternalReference` si cabe; nunca `None`. (2) Sin sobregiro de caja: las líneas de efectivo de un pago se acumulan por sesión y no pueden superar `CashSession.CurrentBalance` ("La caja seleccionada dispone de $80.00 y se intenta registrar un pago de $120.00."), sin override. Evidencia: Domain 1180 · Application 2218 · Infrastructure focalizadas 48/48 · API 484/485 (mismo fallo preexistente) · Architecture 110 · `architecture:check` 243 = `HEAD`.
- **02A-FINAL**: (1) pago y reversa bloquean la `CashSession` con `FOR UPDATE` (nuevo `ICashSessionRepository.GetOpenByCashRegisterForUpdateAsync`, mismo patrón que la variante FOR SHARE) antes de leer el saldo, en orden determinista por `CashRegisterId` — dos pagos concurrentes quedan serializados; el segundo recibe la validación normal de saldo. Test concurrente real PostgreSQL (saldo 100, 2×70 → 1 confirmado, saldo 30, 1 movimiento, 1 asiento); con FOR SHARE el mismo test reproduce `40P01 deadlock detected`. (2) `TransactionDate` bancaria sin fallback. Evidencia: Domain 1180 · Application 2219 · Infrastructure focalizadas 49/49 · API 484/485 (mismo fallo preexistente) · Architecture 110 · vitest 238/2451 · `tsc -b`, lint 0 errores · `architecture:check` 243 = `HEAD`.
- **02A-FINAL-CLOSE**: (1) backfill `PaymentDate → TransactionDate` eliminado de la migración. (2) `RegisterSupplierCreditRefund`/`ReverseSupplierCreditRefund` migrados a `GetOpenByCashRegisterForUpdateAsync`; `GetOpenByCashRegisterForShareAsync` eliminado (FOR UPDATE = único lock oficial de `CashSession`). Los tests concurrentes destaparon un defecto real: si el llamador ya tenía la sesión trackeada antes del lock (la reversa de reembolso la lee por Id), EF devolvía la instancia con `xmin` viejo → `DbUpdateConcurrencyException` intermitente; corregido en el repositorio recargando la entidad tras el lock (test determinista dedicado). Evidencia: Domain 1180 · Application 2219 · Infrastructure focalizadas 52/52 (+5 corridas de estrés de los 3 tests concurrentes) · Architecture 110 · `architecture:check` 243 = `HEAD`; con FOR SHARE los 3 tests concurrentes reproducen `40P01`.
- **Preexistente detectado**: `ERP.Infrastructure.Tests` no compila en `HEAD` — `PurchaseExpenseExclusivityTests` referencia la migración `ExpensesFromPurchaseReception` eliminada al compactar el historial (`4cbc4b12`).

---

## ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — Contrato temporal único (2026-09-25)

**Estado: COMPLETADO (sin commit).** Regla: [`data-standards.md § Contrato temporal`](docs/architecture/data-standards.md) · [ADR-034](docs/decisions/ADR-034-temporal-contract-single-source.md). Fecha de negocio = `DateOnly`/`date`/`"YYYY-MM-DD"`; instante = UTC `"...Z"` presentado en `Company.Timezone`.

- **02A Gastos**: `authorizationDate` ya no acumula +5 h por edición (conversión única hora empresa ⇄ UTC).
- **02B "Hoy"**: 9 usos de `toISOString().slice(0,10)` → `todayIso()` (hoy de `Company.Timezone`); vencimientos con `addDaysIso`.
- **02C AuthorizationDate**: TXT SRI y `datetime-local` convierten hora Ecuador → UTC vía `ICompanyClock.CompanyLocalToUtcAsync`; `UtcDateTime.EnsureUtc` rechaza horas sin zona. Datos históricos **no** autocorregidos → [plan de remediación](docs/operations/AUTHORIZATION-DATE-REMEDIATION-PLAN-02.md) (66 recepciones solo-TXT en dev).
- **02D Filtros**: Kardex, movimientos, Monitor SRI y ajustes filtran por `DateOnly`; día de empresa → `[inicioUtc, finUtc)` con `ICompanyClock.DayUtcRangeAsync`. Instantes por query exigen zona (`UtcInstantModelBinder`); JSON exige/emite `Z` (`UtcInstantJsonConverter`).
- **02E Inventario**: `TransferDate`/`AdjustmentDate` → `DateOnly`/`date` (migración `TemporalContractInventoryBusinessDates02`, con guard de medianoche y reversible). `KardexSnapshot` eliminado (sin consumidores).
- **02F**: `ElectronicDocumentData.IssueDate`, `Dashboard.AsOf` → `DateOnly`; `SriCatalogClock` unificado en `CompanyTimeZone`; `fechaEmision` de la NC de venta desde el día de empresa.
- **02G Frontend**: `formatDateTime` presenta en `Company.Timezone` (`session.tenant.timezone`), no UTC crudo.
- **02I Segundos**: `formatDateTime` es la única salida visual de instantes — `dd/MM/yyyy HH:mm:ss` siempre; `formatDateTimeSeconds` eliminado; 13 consumidores migrados (incl. 4 "Última carga" que usaban `toTimeString()` del navegador — auditoría 03); `datetime-local` conserva segundos reales; guard `F-DT-instant-as-date`.
- **02J Cierre estricto**: Ventas `TransferDate`/`CashDate` → `DateOnly` (sin parseo por cultura); `ApiResponse.Meta.Timestamp` → `DateTime` UTC con `Z` (antes `DateTimeOffset` `+00:00`). Solo quedan dos representaciones temporales productivas.
- **Guards**: backend `DateTimeCompanyClockGuardrailTests` extendido (+7 reglas); frontend nuevo check `frontend-datetime` en `run-all`.
- **Evidencia**: Domain 1175/1175 · Application 2198/2198 · Infrastructure 813/813 (Postgres real, migración aplicada) · API 477/477 · Architecture.Tests 107/107 · vitest 237 archivos/2411 · `tsc -b`, lint (0 errores), build OK · `architecture:check` sin nuevas violaciones (5 checks rojos preexistentes, idénticos a `HEAD`).

---

## DESTINOS-CONTABLES-COBROS-VENTAS-01 — Cuenta contable por forma de pago (2026-09-15)

**Estado: COMPLETADO (Fase 1).** `PaymentMethod → AccountingAccount` por `TenantId + CompanyId` (entidad `PaymentMethodAccount`), consumido por la contabilización de Ventas: una venta con pago no-crédito (ej. Transferencia) resuelve su cuenta contable configurada en vez de caer siempre en "Caja general"; si la Company tiene al menos una cuenta configurada, cualquier forma de pago no-crédito sin cuenta asignada bloquea la autorización (fail-closed) con mensaje explícito. Compañías sin ninguna fila configurada mantienen el comportamiento histórico (retrocompatible).

- **Backend** (construido en `47cff20a`, `SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01`): entidad `PaymentMethodAccount` (`ERP.Domain/Modules/Sales/Entities`), endpoint `PUT /api/v1/payment-methods/{id}/account` (`SetPaymentMethodAccountCommand`, valida cuenta activa/imputable y de la Company activa), gate de bloqueo en `AuthorizeSalesUseCases`, ruteo de allocations en `SalesInvoiceAuthorizedPostingTranslator`.
- **Frontend**: `PaymentMethodsPage.tsx` — tabla Forma de cobro/Código/Código SRI/Requiere referencia/Cuenta contable/Estado, selector de cuenta imputable activa, guardado independiente del CRUD del catálogo de formas de pago.
- **Navegación** (este ticket): único punto de acceso reubicado a **Contabilidad → Configuración → Destinos contables → Cobros de ventas** (antes en Configuración → Condiciones comerciales, `8ab5c121`) — mismo `NavItem` Id reutilizado, sin duplicar entrada de menú (criterio ya establecido: dos accesos al mismo destino confunde). Cambio de navegación puro, sin tocar backend/lógica de posting.

---

## API TEST SUITE CLEANUP — `ERP.API.Tests` 483/483 (2026-09-14)

**Estado: COMPLETADO.** Las 25 fallas preexistentes de `ERP.API.Tests` (detectadas durante COMPANY-PRECISION-POLICY, confirmadas no atribuibles a esa iniciativa) quedaron resueltas. `ERP.API.Tests` pasó de 458/483 a **483/483** — sin fallas pendientes.

- **Causa raíz agrupada**: fixtures de `ERP.API.Tests` incompletos — creaban Company "a mano" sin correr el bootstrap contable oficial (`PostingRule` faltante → `RULE_NOT_FOUND`); `PaymentTerm` de contado sembrado con código no canónico ("CONT" en vez de "CONTADO"); índice único de `BusinessPartner` (`uq_mbp_identification`) perdido al recomprimir el historial de migraciones dos veces.
- **Commits**: `257ff134` (SalesReturn fixture: bootstrap contable oficial) · `e91e9bc9` (CajaVentas fixture: PaymentTerm canónico + bootstrap contable) · `1b94cb54` (migración: restaura índice único de BusinessPartner).

---

## COMPANY-PRECISION-POLICY — Precisión decimal operativa por empresa (2026-09-14)

**Estado: COMPLETADO.** `company_precision_policy` queda como única SSOT de precisión decimal operativa configurable por empresa (precio unitario venta/compra, cantidad, porcentaje, costo unitario/promedio, factor de conversión, tolerancia de cuadre). `FiscalPrecision` sigue fijo para impuestos/totales/caja/CxC/CxP/contabilidad — sin cambios.

- **Backend**: entidad + provider fail-closed por tenant/company, perfiles Estándar comercial/Alta precisión/Personalizado, bloqueo operativo automático (409) al detectar la primera operación real (venta autorizada, compra confirmada, movimiento de inventario, pago, asiento posted) — sin endpoint de desbloqueo.
- **Frontend**: migrado módulo por módulo (Sales → Purchases → Items/Pricing → Inventory → Expenses → Payables/Supplier Payments → consumidores compartidos) a `precisionPolicy.config.ts`. `decimal.config.ts` y la pantalla legacy de decimales fueron eliminados del frontend.
- **Legacy backend**: endpoint `GET/PUT /api/v1/config/decimals` (`DecimalConfigController`) eliminado — cero consumidores confirmados por auditoría. Las keys `org_settings` `presentation.decimal.*` fueron eliminadas (código, catálogo y filas de BD vía migración `PrecisionPolicyBackfillAndLegacyCleanup04`); `CompanyPrecisionPolicy` es la única funcionalidad de precisión decimal. Sin defaults en frontend: valores, rangos y perfiles salen de `GET /precision-policy` y `/precision-policy/metadata` (definición única backend `PrecisionPolicyDefinitions`); si la API falla, la app muestra error.

**Commits**: `b907502a` (base backend) · `ffbfa763` (Sales) · `5269406f` (Purchases) · `845fc701` (Items/Pricing) · `d988fc71` (Inventory) · `8ff8ae6c` (Expenses) · `c82dba96` (Payables/Supplier Payments) · `97ce8e36` (retiro legacy frontend) · `71f29b9d` (retiro legacy backend).

### ERP-PRECISION-CAPACITY-05A / OPERATIONAL-05B (2026-09-21)

- **05A (capacidad)**: máximos de `PrecisionPolicyDefinitions` alineados con la BD — venta 6; compra/costo/promedio/factor 10; cantidad 6; porcentaje 6 (migración `PrecisionCapacityAlignment05A`: columnas `numeric(22,10)`/`(18,10)`/`(20,6)`/`(16,6)`/`(9,6)`, normaliza `sales_unit_price_decimals > 6` a 6 y regenera los CHECK). `FiscalPrecision` y totales fiscales sin cambios.
- **05B (operativo)**: Application resuelve `CompanyPrecisionPolicy` y pasa los dígitos a Domain (Domain no consulta configuración). Migrado: Pricing (precio unitario resuelto/simulación), Sales (cantidad base, factor, costo unitario del snapshot, precio lista), Purchases (cantidad base, `LandedUnitCost`, factor, devoluciones/NC), Inventory (ajustes: cantidad/costo/factor; costo promedio corrido del Kardex en `StockRepository`). Sin argumento, Domain conserva el comportamiento histórico. Con el perfil Estándar el precio unitario de venta pasa a 2 decimales (antes 6 fijo en Pricing).

### Pendientes no bloqueantes

- `PRICING-LAB-COST-DECIMAL-SEMANTICS-01`: 3 métricas de costo en `PricingTab.tsx` (Items) mantienen la escala legacy (`purchaseUnitPriceDecimals`) en vez de `unitCostDecimals`/`averageCostDecimals` por falta de tests de formato — decisión de negocio pendiente.
- `INVENTORY-DECIMAL-SEMANTICS-01`: mismo criterio aplicado a 3 métricas de costo en Kardex (`KardexPage.tsx`/`KardexMovementDetailModal.tsx`).
- `purchaseLinePresentation.ts` — `inventory.baseUnitCost` sigue en `purchaseUnitPriceDecimals` en vez de `unitCostDecimals`; mismo criterio, pendiente de decisión de negocio + tests dedicados.
- `ERP.API.Tests`: 25 fallas `RULE_NOT_FOUND` preexistentes (Sales Return/Caja Ventas E2E + 1 test de unicidad PostgreSQL) — confirmadas no atribuibles a esta iniciativa (reproducidas idénticas contra el baseline previo a estos cambios).

---

## CLOSE-PURCHASES-EXPENSES-PAYABLES-READY-01 — Cierre funcional (2026-09-12)

**Estado: LISTO / CERRADO** para Compras, Gastos, CxP y Pagos proveedor en el alcance siguiente. Consolida la validación funcional comunicada por el responsable del proyecto, las validaciones históricas de este documento y la cobertura existente identificada en la [matriz de QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No equivale a una nueva ejecución de todas las suites ni a certificar el despliegue de cada ambiente.

| Bloque | Alcance funcional cerrado |
|---|---|
| Compras | Compra normal; recepción XML; vinculación de producto/presentación/inventario; costos/Kardex; notas de crédito de compra; devolución a proveedor; NC por descuento; presentación e inventario con nombres legibles y factor separado. |
| Gastos | Gasto manual y desde recepción XML; IVA real de catálogo SRI; tipo de documento de catálogo SRI; generación de CxP; anulación controlada. Sin hardcodes peligrosos pendientes detectados en los puntos revisados de IVA/tipo documental; no es una auditoría exhaustiva nueva del frontend. |
| CxP / Pagos proveedor | CxP desde compras y gastos; pago parcial y total; reversa; bloqueo de anulación con pagos activos; anulación luego de reversar los pagos, sujeta a las demás validaciones del documento; aislamiento por empresa. |
| Contabilidad relacionada | Asientos de compra, gasto, pago, reversa y anulación; reportes que conservan el neteo de asientos Reversed; documento origen legible y descripciones de líneas sin GUID técnico. Cierre limitado a estos flujos, no al módulo contable completo. |

### Guardrails / No romper

- No reintroducir catálogos SRI hardcodeados en frontend: IVA desde `sri-vat-rates` y tipo de documento desde `sri-doc-types`.
- Cuentas contables desde reglas/configuración, nunca decididas por el frontend.
- No mostrar GUIDs/códigos técnicos en pantallas operativas si existe nombre legible. En presentación, no usar códigos UOM como sustituto del nombre cuando falta contexto.
- No permitir anular gastos/compras/CxP con pagos activos. Reversar primero; mantener las demás restricciones de anulación.
- No cambiar posting/Kardex/CxP sin pruebas E2E o de integración del flujo afectado.
- No excluir asientos `Reversed` de reportes financieros si rompe el neteo contable: conservar original y contrapartida según las reglas del reporte.

### Commits relevantes verificados en git

| Commit | Cambio |
|---|---|
| `9a327417` | fix(expenses): show document type names from SRI catalog |
| `f2c508a5` | fix(purchases): show readable presentation and inventory labels |
| `9a46a1ef` | fix(expenses): use SRI document type constant for credit notes |
| `3f7d8298` | fix(expenses): show real VAT percentages and validate XML totals |
| `772be348` | fix(accounting): resolve source documents for expenses and supplier payments |
| `567b4851` | fix(accounting): show readable line descriptions for expenses and supplier payments |
| `901e6551` | fix(supplier-payments): show legible installment info in payment detail |
| `e6e44296` | fix(payables): scope AccountsPayable detail-by-id to the active company |
| `21e1d9a6` | feat(supplier-payments): show provider's pending payables portfolio |
| `cab0ae53` | fix(purchases): release reception when cancelling discount credit note |
| `9a0f3b02` | fix(accounting): post purchase credit note discounts to income |
| `eeac4930` | fix(accounting): include reversed entries in reports |
| `d81cbc7d` | fix(inventory): persist purchase cancellation stock movement type |
| `8f6cf51a` | fix(purchases): enable cancel action for confirmed purchases |

### Pendientes no bloqueantes

- Saldos iniciales banco/caja/capital.
- Cierre contable de utilidad/pérdida a patrimonio.
- Mejoras futuras de UX, incluida la recuperación del nombre de presentaciones históricas ausentes del contexto; actualmente se muestra un mensaje legible de indisponibilidad.
- Liquidación de compra SRI 03 y recepción física sin factura son ampliaciones fuera de este cierre. Conciliación bancaria y flujo de efectivo conservan su alcance futuro.

Estos pendientes no reabren Compras/Gastos/CxP. El registro histórico del 2026-09-10 conserva una migración pendiente de aplicar a la BD de la aplicación: su aplicación no se verificó en este ticket documental y debe comprobarse antes de operar ese ambiente.

**Evidencia reciente ejecutada:** para `f2c508a5`, 252 tests de Compras en 24 archivos aprobados; `tsc --noEmit`, build y `git diff --check` correctos; lint con 0 errores y 30 advertencias. No hubo comprobación visual en navegador de ese ticket. Las demás ejecuciones previas se mantienen con su fecha y alcance original abajo; la matriz QA distingue cobertura existente de una ejecución nueva.

---

## NC de compra por devolución de productos (2026-09-10)

**Estado: IMPLEMENTADO.** Descuento/promoción conserva su comportamiento.

- Captura cantidades sobre las líneas de la factura: comprado, ya devuelto, disponible, precio, base, IVA, ICE, IRBPNR, total y bodega. Sin productos libres.
- NC y borrador de devolución se guardan juntos. Cada línea fiscal conserva `purchase_invoice_detail_id` y cantidad; impuestos históricos prorrateados server-side.
- Autorización revalida disponibilidad bajo bloqueo financiero, aplica inventario/CxP/contabilidad mediante PurchaseReturn y marca la recepción procesada. Edición y cancelación sincronizan la NC sin duplicar efectos.
- Validado: 292 tests de dominio, 316 de aplicación, 56 de API/E2E, 15 PostgreSQL de persistencia/concurrencia/contabilidad y 201 frontend. Builds backend/frontend, tsc, lint (0 errores, 29 advertencias) y `git diff --check` correctos.
- `architecture:check`: 187 incumplimientos preexistentes en cinco categorías; no se amplía el alcance para corregirlos.
- Migración `20260909172144_PurchaseCreditNoteReturnLines` generada y probada en PostgreSQL temporal; pendiente de aplicar a la BD de la aplicación.

---

## ZH-ADMINGLOBALCORE-LOGIN-FLOW-05I — AdminGlobalCore login flow para provisioning (2026-09-02)

**Estado: CERRADA.** Commit `28b6ddbf feat(auth): add global core admin login for company provisioning`.

- Se agregó rol global persistido para `AdminGlobalCore` y endpoint `POST /api/v1/auth/global-login`, emitiendo token global con `tenant_id = Guid.Empty`.
- La policy `CompanyProvisioning` quedó compatible con token global; `POST /api/v1/companies` permite crear empresas bajo el tenant real recibido en body solo desde el contexto global autorizado.
- Admin en modo empresa queda correctamente bloqueado para provisioning: backend devuelve `403` en `POST /api/v1/companies`; frontend 5H-01 oculta "Nueva empresa" y redirige `/companies/new` a `/companies` cuando el usuario no puede provisionar.
- Se ajustó auditoría de `PriceList` para soportar bootstrap ejecutado desde contexto global.
- **Validado**: `dotnet build backend/src/ERP.API/ERP.API.csproj` OK; `POST /api/v1/auth/global-login` OK con `tenant_id = Guid.Empty`; `POST /api/v1/companies` con AdminEmpresa normal devuelve `403`; `POST /api/v1/companies` con AdminGlobalCore crea empresa correctamente; `GET /api/v1/companies` con token global devuelve `403` esperado.

---

## DOCUMENT-FLOW-POLICY-01 — Separación permisos/flujo documental + reemplazo de `doc_workflow_policy` (2026-08-30)

**Estado: COMPLETADO.** Reemplaza por completo la entidad `DocWorkflowPolicy`/tabla `doc_workflow_policy` (DOC-TYPE-SSOT-01, EXPENSES-WORKFLOW-INTEGRATION-01, ambas del mismo día) por un modelo mucho más rico y con separación conceptual explícita permisos vs. flujo documental — ver [docs/architecture/security.md § Permisos vs. política de flujo documental](docs/architecture/security.md#permisos-vs-política-de-flujo-documental-document-flow-policy-01).

- **`DocumentFlowPolicy`** (Domain, reemplaza `DocWorkflowPolicy`; tabla `document_flow_policy`, único índice `(tenant_id, company_id, document_type_code)`): `IsActive` + 9 modos (`CreationMode`, `ConfirmationMode`, `AuthorizationMode`, `PendingDocumentMode`, `CancellationMode`, `PayableGenerationMode`, `AccountingPostingMode`, `InventoryImpactMode`, `NotificationMode`, todos enums nuevos en `Domain/Modules/DocTypes/Enums`) + 4 flags (`RequiresCancellationReason`/`RequiresAttachment`/`RequiresSupplier`/`RequiresDueDate`). Ningún campo `Can*`/`Allow*` — deliberado, para que nunca pueda confundirse con un permiso. Migración `ReplaceDocWorkflowPolicyWithDocumentFlowPolicy` (drop `doc_workflow_policy` + create `document_flow_policy`; aplicada y verificada con `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes).
- **`IDocumentFlowPolicyService`** (Application) / `DocumentFlowPolicyService` (Infrastructure, reemplaza `IDocWorkflowPolicyService`): `GetRequiredAsync` (falla explícito con `DocumentFlowPolicyViolationException.NotConfigured` si no hay fila — a diferencia del servicio anterior, ya **no** asume un default legado silencioso), `EnsureDraftCreationAllowedAsync`/`EnsureDirectCreationAllowedAsync` (equivalentes a los `ValidateCreate*Async` anteriores, sobre `CreationMode`), `EnsureConfirmationFlowAsync` (nuevo — valida `AuthorizationMode`, devuelve la política para que el caller decida efectos), `EnsureCancellationFlowAsync` (nuevo — valida `CancellationMode`/`RequiresCancellationReason`).
- **Política inicial obligatoria de GASDOC** (sembrada por `DocumentFlowPolicyBootstrapStep`, mismo `Order=49`, y backfill para companies existentes vía `DocumentFlowPolicyBackfillService`): `CreationMode.DraftRequired`, `ConfirmationMode.ManualConfirmation`, `AuthorizationMode.None`, `CancellationMode.AllowedAfterConfirmationWithReversal`, `RequiresCancellationReason=true`, `RequiresSupplier=true`, `RequiresDueDate=true`, `PayableGenerationMode.OnConfirmation`, `AccountingPostingMode.OnConfirmation` — nótese que `CreationMode.DraftRequired` es más estricto que el `DraftMode.Optional` anterior: `CreateConfirmedExpenseCommand` (creación directa confirmada) ahora queda bloqueado por política para GASDOC (el endpoint sigue existiendo para otros doc types futuros). Resto de `DocType` quedan con default legado (`DirectCreation`/`AutoConfirmOnCreate`/sin efectos vía política) — sin cambio de comportamiento observable para ellos.
- **Integración a Expenses** (`ConfirmExpenseDocumentHandler`/`CreateConfirmedExpenseHandler`/`CancelExpenseDocumentHandler`/`CreateExpenseDraftHandler`, `ExpenseDocumentConfirmUseCases.cs`/`CancelExpenseDocumentUseCases.cs`/`ExpenseDocumentDraftUseCases.cs`): el permiso de la acción (`expenses.documents.confirm`/`.cancel`) se sigue validando exclusivamente en `ExpensesController` vía `[Authorize(Policy = "perm:...")]`, **sin cambios** — `DocumentFlowPolicy` nunca sustituye ese chequeo. `ConfirmExpenseDocumentHandler` ahora llama `EnsureConfirmationFlowAsync` y solo crea la `AccountsPayable` si `PayableGenerationMode.OnConfirmation`; `CancelExpenseDocumentHandler` llama `EnsureCancellationFlowAsync` (bloquea con mensaje fijo si `CancellationMode.NotAllowed` o motivo vacío con `RequiresCancellationReason`) y solo reversa CxP si `CancellationMode.AllowedAfterConfirmationWithReversal`. El posting contable (asiento) sigue disparándose de forma incondicional vía el evento de dominio existente (`ExpenseDocumentConfirmedPostingTranslator`/`...CancelledPostingTranslator`) — coincide con `AccountingPostingMode.OnConfirmation` de la política inicial de GASDOC, así que no hay cambio de comportamiento observable; gatear el translator por el modo de la política queda fuera de alcance (infraestructura de posting es FROZEN).
- **Pantalla nueva**: Configuración → Documentos y flujos (`/settings/document-flows`), permisos nuevos `settings.documentFlows.view`/`.update` (solo controlan el acceso a esta pantalla, no reemplazan permisos de acción de ningún módulo). Backend: `DocumentFlowPoliciesController` (`GET`/`GET {id}`/`PUT {id}`, `api/v1/settings/document-flows`), `GetDocumentFlowPoliciesQuery`/`GetDocumentFlowPolicyByIdQuery`/`UpdateDocumentFlowPolicyCommand`/`DocumentFlowPolicyDto` (`Application/Modules/DocTypes/UseCases`), `IDocumentFlowPolicyRepository`/`DocumentFlowPolicyRepository` (CRUD de administración, separado del servicio de validación en runtime). Frontend: `DocumentFlowPoliciesPage.tsx` (lista→editor con `ConfigTabsLayout`, mismo patrón que `ItemTypesPage`), nota de separación permisos/flujo visible en ambas tabs, labels en español sin ningún "puede crear/confirmar/anular" (usa "Flujo de creación"/"Confirmación y autorización"/"Anulación"/"Efectos del documento"/"Notificaciones").
- **No tocado**: lógica de negocio de Ventas/Compras/Inventario/Contabilidad (solo el hook mínimo de integración en Expenses descrito arriba); infraestructura de posting/secuencias documentales (FROZEN); `DocType`/`DocTypeSriMap` (sin cambios).
- **Validado**: `dotnet build` completo (0 errores en Domain/Application/Infrastructure/API); `ERP.Domain.Tests` 992/992; `ERP.Application.Tests` filtrado a `Expenses` 61/61 (17 casos nuevos/actualizados: creación directa bloqueada/permitida por `CreationMode`, confirmación bloqueada por `AuthorizationMode`, CxP condicionada a `PayableGenerationMode`, anulación bloqueada por `CancellationMode.NotAllowed`, motivo obligatorio por política, reversa condicionada a `AllowedAfterConfirmationWithReversal`); `ERP.Infrastructure.Tests` completo (531/531, incluye alta a `IgnoreQueryFiltersAuditTests` allowlist para los 2 archivos de seeding nuevos); `ERP.API.Tests` filtrado sin `PurchaseReturnEndToEndTests` (ver hallazgo pre-existente abajo) — incluye nuevo caso `Cancel` en `Cada_endpoint_expone_su_permiso_propio`; frontend `tsc --noEmit` limpio, `npm run lint` sin errores nuevos, `npm run build` exitoso. `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
- **Hallazgo pre-existente, no introducido por este ticket, corregido porque bloqueaba compilar `ERP.API.Tests` entero**: `ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.cs` no compilaba en `main` — `AuthorizePurchaseReturnHandler` ganó un parámetro `IPostingEngine` en un commit anterior sin actualizar este test de integración (`BuildAuthorizeHandler`). Fix mínimo y contenido a ese archivo: se agregó `BuildPostingEngine(db)` (instancia real de `PostingEngine` con sus repos ya usados en el archivo), sin tocar lógica de negocio.
- **Bug real encontrado al correr la API contra Postgres real (reportado por el usuario, no detectado por `dotnet test` porque los tests usan Testcontainers con datos de prueba más cortos)**: `DocumentFlowPolicyBootstrapStep` fallaba en el primer arranque con `Npgsql 22001 value too long for type character varying(32)` — `CancellationMode.AllowedAfterConfirmationWithReversal` (36 caracteres en minúsculas) excedía el `HasMaxLength(32)` puesto a las 9 columnas de modo en `DocumentFlowPolicyConfiguration`. **Fix**: `HasMaxLength(64)` para las 9 columnas de modo + migración `WidenDocumentFlowPolicyModeColumns` (9 `AlterColumn`, aplicada; `has-pending-model-changes` limpio de nuevo). Ninguna fila llegó a persistirse antes del fix (la excepción abortaba el `SaveChangesAsync` completo de cada company, transacción implícita de EF Core) — sin necesidad de limpieza de datos corruptos.

---

## EXPENSES-CANCEL-01 — Anulación de ExpenseDocument confirmado (2026-08-30)

**Estado: COMPLETADO.** Un gasto Confirmed puede anularse — reversa CxP + asiento contable, nunca borra el documento.

- **`ExpenseDocument.Cancel(string reason, Guid cancelledBy)`** (Domain, void + excepciones — sin `Result<T>`, consistente con `PurchaseInvoice.Cancel`): exige `Status == Confirmed` (`InvalidOperationException` si no), motivo obligatorio (`ArgumentException` si vacío/whitespace), guarda `CancelReason`/`CancelledAt`/`CancelledBy`, llama `SetUpdated`, levanta `ExpenseDocumentCancelledEvent`. Draft no se anula (se edita/elimina por su propio flujo); Cancelled es terminal — anular dos veces lanza la misma excepción de estado.
- **Columnas nuevas en `expense_documents`** (migración `AddExpenseDocumentCancelColumns`, nullable, aditiva): `cancel_reason`, `cancelled_at`, `cancelled_by` — misma convención de nombres que `PurchaseInvoice`/`SalesInvoice` (mayoría establecida, no la variante `_utc`/`_user_id` de Purchases credit/return).
- **`CancelExpenseDocumentCommand`/`CancelExpenseDocumentHandler`** (mismo patrón transaccional que `CancelPurchaseHandler`): `IUnitOfWork.BeginTransactionAsync` explícito → carga `ExpenseDocument` → si existe `AccountsPayable` originada por `AccountsPayableOriginType.ExpenseDocument`, llama `AccountsPayable.Cancel(userId)` (ya existente, reutilizado tal cual — bloquea con `InvalidOperationException` si hay pagos aplicados, traducido a 422 con mensaje claro) → `ExpenseDocument.Cancel(reason, userId)` → un único `SaveChangesAsync` (dispara el evento/traductor de posting) → `CommitAsync`; cualquier fallo hace `RollbackAsync`. Validación de motivo con FluentValidation (`CancelExpenseDocumentValidator`).
- **`ExpenseDocumentCancelledPostingTranslator`** (nuevo, `INotificationHandler<ExpenseDocumentCancelledEvent>`, auto-descubierto por MediatR sin registro DI): reversa el `JournalEntry` original vía `IJournalEntryRepository.GetBySourceAsync` + `ReverseJournalEntryCommand` — mismo mecanismo que `PurchaseInvoiceCancelledPostingTranslator` (nunca reversa contabilidad manualmente desde el handler). Difiere de Purchases en que **lanza** `ExpensePostingFailedException` si no hay asiento Posted que reversar o si el reverso falla (mismo criterio estricto que `ExpenseDocumentConfirmedPostingTranslator`, EXPENSES-CONFIRM-07 — un gasto Confirmed siempre tiene asiento real, así que no encontrarlo es una inconsistencia real, no un caso normal a omitir).
- **API**: `POST api/v1/expenses/documents/{id}/cancel` (body `{ reason }`), permiso nuevo `expenses.documents.cancel` (agregado a `ExpensePermissions` y a `RelatedActionPermissionsCsv` de `SuppliersModule.ExpenseDocuments`).
- **Frontend**: botón "Anular gasto" (`ExpenseDocumentFormPage`, variant `destructive`) visible solo si `status === "Confirmed"` y el usuario tiene el permiso — nunca en Draft/Cancelled. Modal reutiliza `ZHConfirmModal` (`variant="danger"`) + `ZHField`/`ZhTextarea` para el motivo obligatorio, mismo patrón ya establecido por `AdjustmentLifecycleModals` (Inventory Adjustments) — sin componente nuevo. Errores 422 mostrados vía `message.error(formatApiRequestError(...))`, igual que Confirmar/Guardar. `ExpenseDocumentDetailDto` (frontend) gana `cancelReason`/`cancelledAt`/`cancelledBy`; recarga desde API tras anular (mismo criterio que Confirmar).
- **Fuera de alcance, no tocado**: `doc_workflow_policy`, `doc_workflow_rule`, aprobaciones, retenciones, XML/RIDE/SRI, ADR-032, ventas/compras de mercadería, SaaS/Platform.
- **Validado**: `dotnet build` de `ERP.API`/`ERP.Domain` (0 errores); `ERP.Domain.Tests` filtrado a `ExpenseDocument` 16/16 (incluye 5 casos nuevos de `Cancel`); `ERP.Application.Tests` filtrado a `Expenses|Accounting|Payables` 293/293 (incluye 8 casos nuevos de `CancelExpenseDocumentHandler`: sin CxP, con CxP sin pagos, con CxP con pagos aplicados → 422 claro + rollback, Draft bloqueado, ya Cancelled bloqueado, no encontrado, validator, fallo de reverso contable → rollback); `ERP.Infrastructure.Tests` filtrado a `Expenses|Accounting|Payables` 102/102; `ERP.Architecture.Tests` 101/101; frontend `tsc --noEmit` limpio, `npm run lint` sin errores (solo warnings preexistentes ajenos a este cambio), `npm run build` exitoso; `git diff --check` limpio. Migración `AddExpenseDocumentCancelColumns` generada y revisada.

---

## EXPENSES-WORKFLOW-INTEGRATION-01 — Primer consumidor de `doc_workflow_policy` (GASDOC) (2026-08-30)

**Estado: COMPLETADO.** Primera integración real de la política de flujo documental (DOC-TYPE-SSOT-01) — Expenses/ExpenseDocument, `doc_type` GASDOC.

- **`CreateExpenseDraftHandler`** ahora llama `IDocWorkflowPolicyService.ValidateCreateDraftAsync(companyId, "GASDOC", ct)` antes de resolver proveedor/líneas — bloqueado (`draft_mode` `Disabled`) devuelve 422 con mensaje fijo: *"La política de la empresa no permite guardar borradores para documentos de gasto."*
- **Nuevo `CreateConfirmedExpenseCommand`/`CreateConfirmedExpenseHandler`** (`ExpenseDocumentConfirmUseCases.cs`, endpoint `POST api/v1/expenses/documents/confirmed`, permiso `expenses.documents.confirm`): crea un gasto directamente en `Confirmed` — mismos datos que `CreateExpenseDraftCommand`, misma resolución de proveedor/condición de pago/líneas (`ExpenseDraftRules`, promovido de `file` a `internal` para reutilizarse entre ambos archivos, mismo criterio que `ExpenseDocumentMapper`), luego `ExpenseDocument.CreateDraft()` + `Confirm()` en la misma operación — mismo posting estricto (`ExpensePostingFailedException` aborta) y creación best-effort de `AccountsPayable` que `ConfirmExpenseDocumentCommand`. Llama `ValidateCreateConfirmedAsync` antes de construir el documento — bloqueado (`draft_mode` `Required`) devuelve 422: *"La política de la empresa requiere guardar el gasto como borrador antes de confirmarlo."*
- **Ajuste menor a DOC-TYPE-SSOT-01** (permitido por alcance): `DocWorkflowPolicyService.ValidateCreateConfirmedAsync` ahora también bloquea cuando `DraftMode.Required` (antes solo validaba `IsEnabled`) — necesario para que "Required" tenga efecto real; nueva excepción `DocWorkflowPolicyViolationException.DraftRequired`. Solo aplica a la creación de un documento ya confirmado — confirmar un borrador existente nunca pasa por este chequeo.
- **`ConfirmExpenseDocumentCommand` sin cambios**: confirmar un Draft ya existente nunca llama al servicio de política (no inyectado en ese handler) — funciona igual sin importar `draft_mode`, incluido `Required`.
- **No obligado visualmente, no eliminado**: ambos caminos (crear borrador / crear confirmado directo) coexisten como comandos independientes; el frontend decide cuál ofrecer.
- **Mensajes específicos de módulo**: `ExpenseWorkflowPolicyMessages.Translate(DocWorkflowPolicyViolationException)` (nuevo, `internal`, en `ExpenseDocumentDraftUseCases.cs`) traduce los códigos SSOT genéricos (`doc_workflow.draft_not_allowed`, `doc_workflow.draft_required`) a los dos mensajes fijos pedidos — el mensaje genérico de la excepción (reutilizable por otros módulos futuros) no es el texto que ve el usuario de Gastos.
- **No tocado**: ADR-032, XML/RIDE/SRI, aprobaciones, `doc_workflow_rule` (no creado), retenciones, Cancel (sin hooks nuevos — no fue necesario), ventas/compras existentes.
- **Validado**: `dotnet build` de `ERP.API`/`ERP.Application`/`ERP.Application.Tests` (0 errores); `ERP.Domain.Tests` 985/985; `ERP.Application.Tests` filtrado a `Expenses` 43/43 (7 casos nuevos: GASDOC Optional/Required permiten borrador, Disabled lo bloquea con mensaje exacto; Optional/Disabled permiten confirmado directo, Required lo bloquea con mensaje exacto; confirmar un Draft existente funciona con `Required`); `ERP.Infrastructure.Tests` filtrado a `Expenses|DocWorkflow|DocType` 37/37; `ERP.Architecture.Tests` 101/101; `git diff --check` limpio. Sin migración nueva (no se tocó el modelo de datos).

---

## DOC-TYPE-SSOT-01 — Fase 1: SSOT documental interno + política base de borrador (2026-08-30)

**Estado: FASE 1 COMPLETADA (lectura/seed).** Nueva infraestructura de catálogo, sin integración a ningún flujo existente todavía.

- **`doc_type`** (global, schema `global`): SSOT interno de tipos de documento/proceso del ERP — deliberadamente simple (código, nombre, activo), sin flags de impacto contable/inventario/AP-AR. Distinto de `sri_doc_type` (catálogo oficial SRI, sin tocar). Seed inicial: `FACVEN`, `NCVDEV`, `FACCOM`, `NCCDEV`, `GASDOC`, `RETGAS`, `PAGPRO`, `COBCLI`, `ASI`, `AJUINV` — códigos fijados también en `DocTypeCodes` (Domain), mismo criterio que `SriDocumentTypeCodes`.
- **`doc_type_sri_map`** (global): mapeo opcional `doc_type → sri_doc_type`; varios `doc_type` pueden apuntar al mismo código SRI. Seed: `FACVEN→01`, `NCVDEV→04`, `NCCDEV→04`, `RETGAS→07`.
- **`doc_workflow_policy`** (por tenant/company/doc_type, único índice `(tenant_id, company_id, doc_type_code)`): habilitado/deshabilitado, `draft_mode` (Disabled/Optional/Required), `default_action` (Confirm/Draft). Sembrado por `DocWorkflowPolicyBootstrapStep` (`ICompanyBootstrapStep`, Order=49, entre ExpensesCatalog y Access) para companies nuevas — una fila por `DocType` activo, `GASDOC` con `DraftMode.Optional`, el resto `Disabled`, todos `DefaultAction.Confirm` (idéntico al comportamiento previo). Backfill para companies existentes vía `DocWorkflowPolicyBackfillService` (mismo patrón que `ExpensesCatalogBackfillService`/`AccountingChartBackfillService`: automático en cada arranque, fuera de Production).
- **`IDocWorkflowPolicyService`** (Application) / `DocWorkflowPolicyService` (Infrastructure): `GetPolicyAsync`, `ValidateCreateDraftAsync`, `ValidateCreateConfirmedAsync`. Sin fila explícita, resuelve el mismo default legado (fail-open a comportamiento actual) — ninguna company queda bloqueada por falta de backfill. Violaciones lanzan `DocWorkflowPolicyViolationException` (422, mismo criterio que `SystemSeededRecordException`).
- **No aplicado todavía** a ningún flujo de ventas/compras/gastos existente — fase 1 es exclusivamente SSOT + seed + servicio de lectura/validación. Integración a Expenses queda para una fase posterior.
- **No tocado**: ADR-032, XML/RIDE/SRI (solo lectura de `sri_doc_type` para el mapping), aprobaciones (`doc_approval_*` no creado), ningún módulo existente.
- **Validado**: `dotnet build` completo (0 errores), `ERP.Domain.Tests` (985/985), `ERP.Application.Tests` build limpio, `ERP.Infrastructure.Tests` filtrado a `DocType|DocWorkflow|BootstrapStepGovernance|CompanyBootstrapOrchestrator` (26/26, incluye nuevo `DocTypeSeedAlignmentTests`), `ERP.Architecture.Tests` (101/101), `git diff --check` limpio, migración `AddDocTypeSsot` generada y revisada.

---

## ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 (+ QA-01 + REPORTS-HIERARCHY-SMOKE-01 + FINAL-CLOSEOUT-01) — CERRADO (2026-08-29)

**Estado: COMPLETADO.** Plan de Cuentas con jerarquía canónica por código, protegida a futuro, y reportes contables alineados. Runbook: [`docs/operations/ACCOUNTING_CHART_HIERARCHY_BACKFILL_RUNBOOK.md`](docs/operations/ACCOUNTING_CHART_HIERARCHY_BACKFILL_RUNBOOK.md).

- **Regla canónica**: el código contable manda la jerarquía — "1.1.01" implica padre "1.1", "1.1.01.001" implica padre "1.1.01". `ParentAccountId` y `Level` (calculado, no persistido) quedan siempre alineados con el código.
- **Blueprint corregido** (`AccountingBootstrapStep.cs`, `RetailChartAccountCount` 92→102): 10 cuentas agrupadoras intermedias agregadas (`3.1.01`/`3.1.02`/`3.1.03`/`4.2.01`/`5.1.01`/`6.1.01`/`6.2.01`/`6.3.01`/`6.4.01`/`6.5.01`, todas `AllowsPosting=false`, ninguna referenciada en `MinimalPostingRules`) + 1 bug de dato corregido (`5.1.02` colgaba de "5" en vez de "5.1"). Ningún código ni nombre de cuenta existente cambió.
- **Backfill para companies existentes**: `AccountingChartBackfillService.BackfillHierarchyAsync` (automático en `EnsureAsync`, fuera de Production, sin cambios en ese guard) + `RunControlledHierarchyMaintenanceAsync` — diagnóstico previo, fix transaccional por company (rollback si falla), diagnóstico posterior — disparado solo vía `dotnet run --project backend/src/ERP.API -- backfill-accounting-chart-hierarchy` (nunca automático, ver runbook).
- **Create/Update Account** ahora valida que `ParentAccountId` coincida con el padre canónico implicado por el código (`Map.ValidateCanonicalParent`) — impide reintroducir manualmente el mismo tipo de inconsistencia que corrigió el backfill.
- **Orden natural**: `AccountCodeComparer` (Domain, `IComparer<string>` por segmentos, numérico cuando corresponde) aplicado en `AccountRepository.GetByCompanyAsync` y en los 4 reportes contables (Balance de Comprobación, Libro Mayor incl. su filtro de rango `AccountCodeFrom`/`AccountCodeTo` — hallazgo P1 corregido en el smoke, Estado de Resultados, Estado de Situación Financiera). Orden macro (Activo→Pasivo→Patrimonio, Ingresos→Costos→Gastos) garantizado estructuralmente por el DTO (propiedades separadas por grupo, no una lista mezclada).
- **`AccountHierarchyDiagnostics`** (Domain, puro): analiza 7 invariantes (padre huérfano/faltante/desalineado, Level≠profundidad, agrupadora posteable, ciclos, PostingRule inválida) — reutilizado por bootstrap, backfill y el comando CLI de mantenimiento controlado.
- **`AccountTreeBuilder`** (Domain, puro): árbol padre/hijo con acumulación de saldos hacia agrupadoras, orden natural — listo para futuros diagramas/reportes jerárquicos, sin endpoint nuevo expuesto todavía.
- **Resultado final**: 0 padres faltantes, 0 cuentas huérfanas, 0 `ParentAccountId` desalineados, 0 diferencias Level vs profundidad, 0 ciclos, 0 agrupadoras con `AllowsPosting=true`, 0 `PostingRule` inválidas — confirmado en `ERP.Domain.Tests`/`ERP.Application.Tests`/`ERP.Infrastructure.Tests` filtrados a `Accounting`/`Seeding` (83+200+65+52, todos en verde) y en `ChartOfAccountsPage.test.tsx` (7/7) + `tsc --noEmit` + `npm run build` limpios.
- **No tocado**: Posting Engine (`JournalFactory`/`PostingRuleResolver`/`PostingPipeline`), códigos contables existentes, nombres de cuentas existentes, lógica de asientos, UI de Plan de Cuentas (el frontend ya calculaba orden/profundidad visual correctamente por código desde antes).

---

## DocumentSequenceExclusivityTests (SEQ_GATE_01/02) — RESUELTO (2026-08-29)

**Estado: COMPLETADO.** Deuda preexistente documentada en el checkpoint de ADR-032 — diagnosticada y cerrada, sin relación con IRBPNR/ICE/ADR-032 (nunca tocó ese ADR ni impuestos/ventas/compras/XML).

- **Causa raíz real**: gate de arquitectura basado en scan de texto (`DocumentSequenceExclusivityTests.cs`) con un allowlist de archivos autorizados a llamar `.CaptureAndIncrement()` / mutar `CurrentSeq`. `SupplierPaymentSequence`/`SupplierPaymentSequenceRepository` (SUPPLIER-PAYMENTS-FOUNDATION-15B) replican, a propósito y documentado en su propio doc comment, el mismo patrón de secuencia independiente ya usado por `PurchaseReturnSequence` (no es una instancia de `DocumentSequence`, infraestructura FROZEN — un pago a proveedor no emite comprobante SRI). `PurchaseReturnSequence` ya tenía su exclusión exacta por path en el allowlist; `SupplierPaymentSequence` nunca la recibió cuando se creó — el gate lo detectaba como violación real, cuando en realidad el código de producción ya seguía el patrón correcto (advisory lock en el repositorio + `CurrentSeq` mutado únicamente dentro de la propia entidad).
- **Verificado antes de corregir**: `grep` confirmó que `SupplierPaymentSequenceRepository.cs` es el único caller de `.CaptureAndIncrement()` fuera de la entidad y de `PurchaseReturnSequenceRepository.cs` (ya permitido), y que `SupplierPaymentSequence.cs` es el único mutador de `CurrentSeq` fuera de `DocumentSequence.cs`/`PurchaseReturnSequence.cs` (ya permitidos) — ninguna otra violación real en el árbol.
- **Fix**: 2 entradas nuevas en los allowlists ya existentes (`AllowedCaptureAndIncrementCallers`, `AllowedCurrentSeqMutators`), mismo criterio exacto que la exclusión ya otorgada a `PurchaseReturnSequence`. **No se relajó ningún guard** — el gate sigue bloqueando cualquier otro caller/mutador no listado; solo se corrigió un allowlist incompleto.
- **No tocado**: ADR-032, impuestos/ICE/IRBPNR, Ventas, Compras, XML/RIDE, menú, permisos, SaaS — confirmado por el diff (1 solo archivo, el propio test de arquitectura).
- **Validaciones**: `dotnet build` (solución completa) sin errores. `ERP.Infrastructure.Tests` filtrado a `DocumentSequenceExclusivityTests` (8 tests) en verde. **`ERP.Infrastructure.Tests` completo: 499/499 en verde** — primera vez en esta sesión que la suite completa pasa sin ningún hallazgo pendiente. `ERP.Domain.Tests` 962/962, `ERP.Application.Tests` 1312/1312 (sin cambios, verificados por no compartir código). `git diff --check` limpio.

---

## ADR-032 / TAX-LINE-SSOT-ICE-IRBPNR-01 — CERRADO hasta Fase 6 (2026-08-29)

**Estado: implementación principal completada.** Ver [ADR-032](docs/decisions/ADR-032-tax-line-ssot-ice-irbpnr.md) para el diseño completo y el detalle de cada fase/subfase (secciones abajo en este mismo archivo). Este bloque es el resumen de cierre — no repite el detalle ya documentado.

- **Fases 1–6: COMPLETADAS.**
  - Fase 1 (tests de comportamiento baseline), Fase 2 (`SalesInvoiceDetailTax` creada), Fase 4 (backfill idempotente) y Fase 5 (migración de consumidores XML/RIDE/devoluciones/NC/posteo, Subfases 5A–5E) — completadas en sesiones previas de este mismo ticket.
  - Fase 3 (invertir autoridad de impuestos de línea a `*DetailTax`) — **cerrada en este checkpoint**: estaba completa para IRBPNR desde el inicio, pero **nunca se había completado para ICE** hasta el ajuste de plan de esta sesión (`Ice*` convertido a legacy compatibility mirror real, computado desde `_taxes`, en las 4 entidades de línea).
  - Fase 6 (marcar campos legacy como no-fuente-de-verdad) — cerrada junto con Fase 3: `Ice*`/`ExciseTaxCode` son ahora legacy compatibility mirror real (documentado en código y en ADR-032), no solo declarado.
- **`ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01` — RESUELTO**, commit `f43d915d`. IRBPNR ahora genera su nodo `<impuesto>` (código SRI "5") en el XML electrónico de venta.
- **`PURCHASE-RETURN-CONCURRENCY-TESTCONTAINERS-01` — RESUELTO**, commit `243e08e6`. Causa raíz: `NewChildEntityTrackingInterceptor` no registrado en 4 archivos de test de concurrencia de `PurchaseReturn` — no relacionado con IRBPNR ni con producción.
- **Fase 7 (eliminación física de columnas legacy) — NO iniciada.** Queda como ticket futuro, explícitamente fuera de esta sesión — requiere confirmación previa de que ningún consumidor externo (reportes/integraciones) depende de las columnas `Ice*`/`ExciseTaxCode` directamente antes de generar cualquier `DROP COLUMN`.
- **`DocumentSequenceExclusivityTests` (`SEQ_GATE_01`/`SEQ_GATE_02`) sobre `SupplierPaymentSequenceRepository`/`SupplierPaymentSequence`** — deuda preexistente **separada**, ajena a ADR-032/IRBPNR/ICE (módulo Payables, nunca tocado por este ticket). Confirmada con `git stash` que ya fallaba antes de cualquier cambio de esta sesión. Sigue sin resolver, requiere su propio ticket.
- **Smoke funcional**: 2 tests de integración contra Postgres real (compra + venta, IVA+ICE+IRBPNR juntos) confirmando asiento contable balanceado — ver bloque "Fase 3 (ICE) completada" abajo para el detalle.

---

## ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01 — IRBPNR en XML electrónico (2026-08-29)

**Estado: COMPLETADO.** Cierra el pendiente registrado en ADR-032 §9, ejecutado antes de Fase 7 tal como se pidió.

- **Causa raíz**: `SriTaxCategoryCodeResolver` (`ERP.Infrastructure/Services/ElectronicDocuments/`) solo reconocía `"VAT"→"2"` e `"ICE"→"3"`. `InvoiceXmlBuilder.Validate()` exige que **todo** `TaxCode` presente en el documento resuelva a un código SRI antes de construir el XML — una factura de venta con IRBPNR fallaba por completo ("código de impuesto que el sistema no reconoce"), aunque `SalesInvoiceElectronicDocumentDataProvider` (Subfase 5C) ya emitía la etiqueta "IRBPNR" correctamente desde `SalesInvoiceDetail.Taxes`.
- **Fix**: una línea nueva en el diccionario del resolver — `["IRBPNR"] = "5"`. `InvoiceXmlBuilder` no se tocó (ya era agnóstico, itera genéricamente sobre las etiquetas que le llegan) — confirmado por `git diff` vacío sobre ese archivo.
- **Tests agregados**: `SriTaxCategoryCodeResolverTests.Resolve_irbpnr_returns_sri_tax_category_code_5`; `InvoiceXmlBuilderTests.Build_factura_con_IRBPNR_genera_nodo_impuesto_con_codigo_SRI_5` (IVA+ICE+IRBPNR juntos, 3 nodos `<impuesto>`, códigos "2"/"3"/"5") y `Build_factura_sin_IRBPNR_no_genera_nodo_de_codigo_5_falso`. La cobertura de `SalesInvoiceElectronicDocumentDataProvider` para IVA+ICE+IRBPNR ya existía de la Subfase 5C (`Factura_con_IVA_ICE_e_IRBPNR_produce_los_3_impuestos`, `Linea_sin_IRBPNR_no_genera_nodo_IRBPNR_falso`) — confirmada en verde, sin necesidad de ampliarla.
- **No tocado** (regla explícita): RIDE, Recepción XML, SaaS/Platform, permisos, menú, catálogos SRI globales, los 3 archivos de `ChartOfAccountsPage` (frontend, cambios preexistentes sin commitear de otra tarea).
- **Validaciones**: `dotnet build` (solución completa) sin errores. `ERP.Application.Tests` 1312/1312 (incluye ElectronicDocuments, 106 tests filtrados). `ERP.Infrastructure.Tests` filtrado a ElectronicDocuments/resolver (13 tests) en verde. `git diff --check` limpio.

---

## TAX-LINE-SSOT-ICE-IRBPNR-01 — Fase 3 (ICE) completada + Fase 6 (auditoría de legacy) (2026-08-29)

**Estado: COMPLETADO.** Ver [ADR-032](docs/decisions/ADR-032-tax-line-ssot-ice-irbpnr.md). Ajuste de plan pedido explícitamente: no dejar `Ice*`/`ExciseTaxCode` como legacy indefinidamente sin primero cerrar la Fase 3 original (invertir autoridad a `*DetailTax`), que nunca se había completado para ICE — solo para IRBPNR y `PurchaseCreditNoteTaxSummary`.

- **Hallazgo previo a la ejecución** (auditoría con agente Explore): `PurchaseInvoiceDetail`/`SalesInvoiceDetail`/`PurchaseReturnDetail`/`SalesReturnDetail` seguían con `Ice*` como campos `private set` escritos directamente por `ApplyTaxes()`/`RecalcTaxes()`/`Freeze()`/`Create()` — nunca convertidos a getters computados como ya lo estaba `IrbpnrAmount`. `TaxInclusiveTotal`/`LineGrandTotal` leían el escalar ICE directamente, no `_taxes`.
- **Fase 3 (ICE) completada ahora**: las 4 entidades convierten `IceCode/IceRate/IceAmount/IceCalculationType/SnapshotIceName` (`ReturnedIceAmount` en `PurchaseReturnDetail`) a propiedades de solo lectura computadas desde `_taxes`/`Taxes` — mismo patrón exacto que `IrbpnrAmount`. `ApplyTaxes()`/`RecalcTaxes()`/`Create()`/`Freeze()` escriben únicamente a la colección (`UpsertTaxRow`/`RemoveTaxRow`), nunca a un escalar paralelo. `PurchaseReturnDetail.Freeze()` pierde los 3 parámetros redundantes `iceCode/iceRate/returnedIceAmount` (ya derivables de `returnedTaxes`) — cambio de firma `internal`, sin impacto externo.
- **EF**: las 5 propiedades Ice* pasan a `builder.Ignore(...)` en las 4 configuraciones (mismo tratamiento que `Irbpnr*`). Las columnas físicas `ice_code/ice_rate/ice_amount/ice_calculation_type/snapshot_ice_name/returned_ice_amount` **no se eliminan** — quedan huérfanas. Migración `IgnoreLegacyIceCompatibilityMirror` sin `DROP COLUMN` (EF lo scaffoldea por defecto; se editó a mano para evitarlo) — único efecto real: `ALTER COLUMN ... SET DEFAULT` en las columnas `NOT NULL` que antes dependían de que EF siempre las escribiera (`ice_rate`/`ice_amount` en `purchase_invoice_details`/`sales_invoice_details`, más `ice_calculation_type` en `sales_invoice_details`/`sales_return_details`) — sin este default, cualquier INSERT nuevo (que ya no incluye estas columnas) violaba la restricción NOT NULL.
- **`ExciseTaxCode` (Item) migrado en los 6 consumidores restantes** que aún resolvían ICE contra `Item.TaxConfig.ExciseTaxCode` en vez de `ItemSpecialTaxConfiguration`: `PurchaseDraftUseCases` (2 sitios), `GetSalesItemPricingQueryHandler` (gana `ICompanySpecialTaxResponsibilityRepository`/`ICurrentCompany`, ahora aplica la regla completa de §3.4/§5.1 — antes solo miraba el ítem), `ItemMappingService`/`GetItemFullReportQuery`/`GetItemByIdQuery` (ficha de ítem — el DTO conserva el nombre `ExciseTaxCode` por contrato público, pero el valor ya viene de `ItemSpecialTaxConfiguration`), `InvoiceItemSearchRepository` (subquery EF traducible a SQL, dropdown de búsqueda POS).
- **No tocado** (ya estaba correctamente migrado, confirmado por la auditoría — los hallazgos originales del ADR §2.3/§2.5 sobre XML/RIDE de venta estaban obsoletos): `SalesInvoiceElectronicDocumentDataProvider`, `SalesReturnCreditNoteDataProvider`, `GetPurchaseItemContextQueryHandler`, la rama principal de `SalesDraftUseCases` — todos ya leían de `Taxes`/`ItemSpecialTaxConfiguration`/`CompanySpecialTaxResponsibility` antes de este ticket.
- **No tocado** (regla explícita): RIDE, ElectronicDocuments core, Recepción XML, SaaS/Platform, permisos, menú, catálogos SRI globales. No se hizo Fase 7 (eliminación física de columnas) — sigue pendiente, requiere confirmación explícita futura de que ningún consumidor externo (reportes/integraciones) depende de las columnas directamente.
- **Validaciones**: `dotnet build` (solución completa) sin errores. `ERP.Domain.Tests` 962/962. `ERP.Application.Tests` 1310/1310. `ERP.Infrastructure.Tests` filtrado a Purchase/Sales Invoice/Return + Item + SriIce (76 tests) en verde. `git diff --check` limpio.
- **Migraciones**: `IgnoreLegacyIceCompatibilityMirror` (aplicada en local, sin DROP COLUMN, solo `SET DEFAULT` en columnas NOT NULL huérfanas).
- **Siguiente bloque**: Fase 6 formalmente cerrada por esta auditoría — `Ice*`/`ExciseTaxCode` son ahora legacy compatibility mirror real (no solo declarado). Fase 7 (decisión de eliminación física) sigue como ticket futuro, sin iniciar.
- **Smoke funcional post-checkpoint (2026-08-29)**: 2 tests nuevos de integración contra Postgres real (Testcontainers) — `PurchaseInvoiceConfirmedPostingIntegrationTests.Smoke_IVA_ICE_IRBPNR_juntos...` y `SalesInvoiceAuthorizedPostingIntegrationTests.Smoke_IVA_ICE_IRBPNR_juntos...` — cada uno crea una línea con IVA 15% + ICE 10% (Percentage) + IRBPNR fijo 0.30, confirma/autoriza el documento, y verifica el asiento contable real (`JournalEntry`/`JournalEntryLine`) generado por el Posting Engine: 5 líneas, debe=haber, y el total del asiento coincide exactamente con `GrandTotal`. Confirma que la conversión de Fase 3 no rompió ni el cálculo de totales ni la contabilización real. Ejecutado además el barrido de regresión completo tras el checkpoint: `ERP.Domain.Tests` 962/962, `ERP.Application.Tests` 1310/1310, `ERP.Infrastructure.Tests` filtrado a Purchase/Sales Invoice/Return/CreditNote + Item + SriIce (83 tests, incluye los 2 smoke nuevos) — todo en verde. Devoluciones/NC no tienen un smoke nuevo dedicado (su lógica de proración no cambió en este checkpoint, solo heredan el mirror ya validado) — cubiertas por la suite de regresión existente, ya en verde.

---

## TAX-LINE-SSOT-ICE-IRBPNR-01 — Fase 5E: posting/contabilidad IRBPNR (2026-08-29)

**Estado: Fase 5E COMPLETADA (dentro de alcance).** Ver [ADR-032](docs/decisions/ADR-032-tax-line-ssot-ice-irbpnr.md). Cierra el punto pendiente dejado explícitamente por 5D: eventos y traductores contables ahora agregan IVA/ICE/IRBPNR desde `*DetailTax`/tax summary lines, nunca desde campos legacy.

- **Eventos** (aditivo puro, parámetro opcional al final, ningún call site existente se rompe): `PurchaseReturnAuthorizedEvent`/`PurchaseReturnCancelledEvent` ganan `AuthorizedIrbpnrTotal`; `PurchaseCreditNoteAuthorizedEvent` gana `IrbpnrAmount` (mismo criterio ya usado con `IceAmount` en ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B); `SalesReturnAuthorizedEvent` gana `TotalIrbpnr`. **Ampliación de alcance confirmada con el usuario**: `SalesInvoiceAuthorizedEvent` (factura de venta normal) también gana `TotalIrbpnr` — tenía el mismo gap que las devoluciones/NC (ya soportaba IRBPNR en `SalesInvoiceDetail` desde 5B pero nunca lo posteaba), a pesar de no estar en la lista literal de alcance original de 5E.
- **`SalesInvoice.TotalIrbpnr`** nueva propiedad computada (`_lines.Sum(l => l.IrbpnrAmount)`), mismo criterio que `TotalIce`/`TotalVat` (siempre en vivo, sin snapshot propio).
- **Traductores contables** (`PurchaseReturnAuthorizedPostingTranslator`, `PurchaseReturnCancelledPostingTranslator`, `PurchaseCreditNoteAuthorizedPostingTranslator`, `SalesReturnAuthorizedPostingTranslator`, `SalesInvoiceAuthorizedPostingTranslator`) ahora setean `PostingFact.TotalIrbpnr` — `PostingFact`/`PostingAmountKind.TaxIrbpnr`/`JournalFactory` ya estaban listos desde FLOW-READY-02F.2 (Compras), sin cambios necesarios ahí.
- **Guards IRBPNR** (mismo patrón exacto que `ConfirmPurchaseUseCases` STEP 0, vía `IPostingEngine.IsAmountKindConfiguredAsync`) agregados a los 4 Authorize use-case handlers que faltaban: `AuthorizePurchaseReturnHandler`, `AuthorizePurchaseCreditNoteHandler`, `AuthorizeSalesReturnHandler`, `AuthorizeSalesInvoiceHandler` — bloquean con mensaje claro si hay IRBPNR sin `PostingRuleLine` configurada, antes de consumir secuencial/persistir efectos.
- **`PurchaseCreditNoteCancelledEvent`/`SalesReturnCancelledEvent`** deliberadamente NO tocados: nunca transportaron snapshot de impuestos (el primero solo reversa `AppliedToPayableAmount`; el segundo solo es legal desde `Draft`, sin traductor) — no había nada que propagar.
- **Gap preexistente descubierto y corregido en el camino** (aprobado explícitamente, alcance acotado): `AuthorizePurchaseCreditNoteHandlerTests` tenía un test arquitectónico (`Handler_no_depende_de_...`) que prohibía cualquier dependencia con "Posting"/"Accounting" en el constructor del handler — desactualizado por el guard nuevo (legítimo y de solo lectura); test corregido para exigir que el handler siga sin crear `JournalEntry` ni tocar `IStockRepository`, permitiendo `IPostingEngine`.
- **No tocado** (regla explícita de 5E, cumplida): XML/RIDE/`InvoiceXmlBuilder`/ElectronicDocuments core/Recepción XML/SaaS-Platform/permisos/menú/catálogos SRI globales/UI. No se inició Fase 6 ni Fase 7.
- **Validaciones**: `dotnet build` (Domain/Application/Infrastructure/API) sin errores. `ERP.Domain.Tests` 962/962, `ERP.Application.Tests` 1310/1310 (incluye los 6 tests mínimos pedidos: IRBPNR en devolución/NC/venta genera `PostingFact.TotalIrbpnr`, documento sin IRBPNR no genera IRBPNR falso, guard bloquea sin `PostingRuleLine`, IVA/ICE sin regresión). `git diff --check` limpio.
- **Deuda preexistente descubierta y corregida en el camino (fuera del código de producción de 5E, aprobada explícitamente)**: la suite `ERP.Infrastructure.Tests` no compilaba en `HEAD` desde 5D-1 — 5 archivos construían `PurchaseReturn.OriginalLineSnapshot` con el constructor anterior a la Subfase 5D-1 (sin el parámetro `Taxes`). Corregido con `Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()` en los 5 fixtures (sin efecto en `VatAmount`/`IceAmount`, que se calculan desde campos separados, no desde `Taxes`). Al compilar por primera vez, 2 de esos fixtures (`AuthorizePurchaseReturnConcurrencyTests`, `AuthorizePurchaseReturnSequenceConcurrencyTests`) además nunca llamaban `line.ApplyTaxes(...)` — corregido con el mismo patrón ya usado en `AuthorizePurchaseReturnLockAConcurrencyTests`.
- **`PURCHASE-RETURN-CONCURRENCY-TESTCONTAINERS-01` — RESUELTO (2026-08-29)**, ver bloque siguiente.
- **Siguiente bloque**: Fase 6 (marcar legacy fields como non-source-of-truth) y Fase 7 (decisión de eliminación física, ticket futuro) — ninguna de las dos iniciada.

---

## PURCHASE-RETURN-CONCURRENCY-TESTCONTAINERS-01 — Corrige concurrencia en devoluciones de compra (2026-08-29)

**Estado: RESUELTO.** Causa raíz diagnosticada (no era un problema de IRBPNR ni introducido por 5E) — la suite Docker/Testcontainers de concurrencia de `PurchaseReturn` pasa limpia.

- **Causa raíz real**: `NewChildEntityTrackingInterceptor` (corrige la clasificación errónea de EF Core de entidades hijas nuevas con clave generada por el dominio como `Modified` en vez de `Added` — ver su propio doc comment) está registrado en producción (`ERP.Infrastructure/DependencyInjection.cs`) pero los 4 archivos de test de concurrencia de `PurchaseReturn` construían su `ErpDbContext` manualmente (`new DbContextOptionsBuilder<ErpDbContext>()...`) sin `.AddInterceptors(...)`. Sin el interceptor, cada `PurchaseReturnDetailTax` nueva (fila de impuesto creada por `Authorize()`, con Guid generado por el dominio) quedaba mal clasificada como `Modified` — el UPDATE resultante afectaba 0 filas → `DbUpdateConcurrencyException` real de EF/Postgres, con hasta 3 reintentos agotados por `StockRepository.IsSequenceConflict` (que trata cualquier `DbUpdateConcurrencyException` como conflicto de secuencia retriable, aunque `RecoverFromConflictAndRetrackAsync` solo sabe recuperar `CurrentStock`/`StockMovement`, nunca `PurchaseReturnDetailTax`).
- **Diagnóstico**: reproducido con un test temporal (solo en memoria de este ticket, nunca commiteado) que capturó `DbUpdateConcurrencyException.Entries` — confirmó `PurchaseReturnDetailTax State=Modified` con **todas** las propiedades `Original == Current` (la firma inequívoca que el propio `NewChildEntityTrackingInterceptor` documenta como el caso que corrige). Otros 4 archivos de test hermanos (`ApplySupplierCreditConcurrencyTests`, `PurchaseReturnCrossInvariantTests`, `RegisterSupplierCreditNoteIntegrationTests`, `SupplierCreditRefundConcurrencyTests`) ya registraban el interceptor correctamente — sirvieron de referencia para el fix.
- **Fix aplicado** (solo fixtures de test, sin tocar `AuthorizePurchaseReturnHandler` ni `StockRepository`): agregado `.AddInterceptors(new NewChildEntityTrackingInterceptor())` en `CreateContext()` de los 4 archivos (`AuthorizePurchaseReturnConcurrencyTests`, `AuthorizePurchaseReturnSequenceConcurrencyTests`, `AuthorizePurchaseReturnLockAConcurrencyTests`, `AuthorizePurchaseReturnStockMovementSequenceTests`) — mismo patrón exacto ya usado en los 4 archivos hermanos. Adicionalmente, `AuthorizePurchaseReturnStockMovementSequenceTests.SeedAuthorizableDraftOnSharedItemAsync` tenía el mismo gap de `ApplyTaxes` ya cerrado en Fase 5E para otros 2 archivos — cerrado aquí también (mismo patrón, sin relación con el interceptor).
- **No se relajó ningún guard, no se desactivó ningún test, no se eliminó concurrencia real** — el fix corrige la clasificación EF de la entidad, no el comportamiento de negocio.
- **Hallazgo nuevo, separado y NO tocado** (fuera de alcance de este ticket): `DocumentSequenceExclusivityTests` (`SEQ_GATE_01`/`SEQ_GATE_02`) falla en `HEAD` de forma completamente independiente — referencia `SupplierPaymentSequenceRepository.cs`/`SupplierPaymentSequence.cs` (módulo Payables, nunca tocado por ADR-032/Fase 5E ni por este ticket). Confirmado que también falla con `git stash` (sin ninguno de los cambios de este ticket) — deuda preexistente ajena a `PurchaseReturn`, requiere su propio ticket.
- **Validaciones**: `dotnet build` (solución completa) sin errores. `ERP.Domain.Tests` 962/962. `ERP.Application.Tests` 1310/1310. `ERP.Infrastructure.Tests` filtrado a `PurchaseReturn` (32 tests, incluye las 4 clases de concurrencia) 32/32 en verde. `git diff --check` limpio. Diff final: 4 archivos de test modificados, cero cambios de producción.

---

## TAX-LINE-SSOT-ICE-IRBPNR-01 — Fase 5D: propagación de impuestos de línea en devoluciones y notas de crédito (2026-08-29)

**Estado: Fase 5D COMPLETADA (checkpoint).** Ver [ADR-032](docs/decisions/ADR-032-tax-line-ssot-ice-irbpnr.md) para el diseño completo y el detalle de cada subfase. Continúa el trabajo de las Fases 1-5C (SSOT de IVA/ICE/IRBPNR en `PurchaseInvoiceDetailTax`/`SalesInvoiceDetailTax`, `ItemSpecialTaxConfiguration`, `CompanySpecialTaxResponsibility`, migración de consumidores de resolución de impuestos y de los data providers de factura).

- **5D-1 — Devolución de Compra**: `PurchaseReturnDetailTax` (nueva) — filas de impuesto prorrateadas desde `PurchaseInvoiceDetailTax` de la línea original por la fracción `Quantity/OriginalQuantity`. `PurchaseReturn.AuthorizedIrbpnrTotal`/`AuthorizedGrandTotal` ya incluyen IRBPNR (afecta correctamente `AppliedToPayableAmount`/`SupplierCreditAmount`).
- **5D-2 — Nota de Crédito de Compra**: primera implementación agregó columnas fijas `Irbpnr*` a `PurchaseCreditNoteTaxSummary` — **corregido tras revisión** antes de continuar a 5D-3: reemplazado por `PurchaseCreditNoteTaxSummaryLine`, colección genérica (`TaxCode, TaxRateCode, TaxName, Rate, CalculationType, TaxAmount`) que modela IVA/ICE/IRBPNR de forma uniforme, sin columna por impuesto. `VatCode/.../IceAmount/Irbpnr*` quedan como propiedades derivadas de solo lectura (legacy compatibility mirror), sin romper `CreditNoteMap.ToDto`. Migración de la primera implementación revertida antes de aplicar la definitiva — sin rastro de columnas agregadas-y-eliminadas.
- **5D-3 — Devolución de Venta**: `SalesReturnDetailTax` (nueva) + `IceCalculationType` (soporte ICE Specific, gap preexistente cerrado) + IRBPNR prorrateado por fracción de cantidad. `SalesReturnDraftUseCases` nunca consulta configuración tributaria actual del ítem.
- **5D-4 — Nota de Crédito de Venta**: `SalesReturnCreditNoteDataProvider.BuildDetailLine` migrado a leer `SalesReturnDetail.Taxes` (mismo patrón que la Subfase 5C sobre la factura), `SalesReturn.TotalIrbpnr` nuevo, incluido en `Totals.TotalTax` del documento electrónico.
- **Deliberadamente NO tocado en 5D** (queda para **Fase 5E**): `PurchaseReturnAuthorizedEvent`/`PurchaseReturnCancelledEvent`, `PurchaseCreditNoteAuthorizedEvent`, `SalesReturnAuthorizedEvent` — ninguno gana campo `Irbpnr*` todavía; alimentan los traductores contables (posting), que siguen sin cambios. Tampoco se tocó `InvoiceXmlBuilder`, RIDE, ElectronicDocuments core (fuera de los 2 data providers de NC ya explícitamente en alcance), Recepción XML, SaaS/Platform, permisos, menú ni catálogos SRI globales.
- **Pendiente registrado** (`ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01`, ver ADR-032 §9): `SriTaxCategoryCodeResolver` aún no traduce IRBPNR→"5" para el XML SRI real — los data providers ya entregan la fila completa, falta la traducción final en infraestructura FROZEN de ElectronicDocuments (ticket propio).
- **Migraciones** (todas aditivas, aplicadas en local): `AddTaxLineSsotIceIrbpnr`, `BackfillTaxLineSsotIceIrbpnr`, `BackfillItemSpecialTaxConfigurationFromExciseTaxCode`, `AddPurchaseReturnDetailTaxAndIrbpnrTotal`, `AddPurchaseCreditNoteTaxSummaryLines`, `AddSalesReturnDetailTaxAndIceSpecific`, `AddSalesReturnTotalIrbpnr`.
- **Validaciones**: `dotnet build` (solución completa) sin errores. `ERP.Domain.Tests` 962/962, `ERP.Application.Tests` 1301/1301, `ERP.Infrastructure.Tests` (incluye gates CI-bloqueantes) en verde. `git diff --check` limpio en cada subfase.
- **Siguiente bloque**: ver Fase 5E arriba (ya completada).

---

## SECURITY-PERMISSION-SCOPE-01 — Anti-escalamiento en asignación de permisos + punto de extensión SaaS externo (2026-08-29)

**Estado: COMPLETADO (alcance acotado).** Cierra la deuda de seguridad interna documentada en NAV-PERMISSION-HIERARCHY-SSOT-01 sin introducir ningún concepto de planes/billing/SuperAdmin — la futura plataforma SaaS será externa, conectada por API.

- **Decisión de alcance**: no existe (ni existía) un rol "SuperAdmin"/scope "global" en el Kernel Registry (`SecurityRoles` solo define `Admin`/`User`, y `Admin` es por membresía empresa↔usuario, no tenant-wide) — el equipo confirmó no inventar esa jerarquía ahora y diferirla a cuando la plataforma SaaS externa defina sus propios scopes.
- **`UpsertProfilePermissionsHandler`**: split del rechazo atómico en dos pasos distinguibles — inexistente (no está en `KernelRegistry.Permissions`) vs. no asignable (existe pero fuera de `KernelRegistry.AssignablePermissionKeys`). Nuevo chequeo anti-escalamiento: un asignador sin rol `SecurityRoles.Admin` nunca puede otorgar (`IsAllowed = true`) un permiso que él mismo no tenga efectivo en su propio contexto operativo (`ICompanyContextProvider` + `IEffectivePermissionKeysProvider`, mismo patrón que `GetMyPermissionsHandler`/`RuntimePermissionAuthorizer`); revocar no pasa por este chequeo (nunca escala privilegios). `Admin` sigue con bypass total, igual que en `RuntimePermissionAuthorizer`.
- **Preparación SaaS externo (NoOp, sin acoplar)**: `IExternalEntitlementService`/`NoOpExternalEntitlementService` nuevos en `ERP.Application.Modules.Integration` (siempre permisivo, registrado en DI) — puerto/adaptador explícitamente documentado como NO modelo de planes internos, NO billing, NO suscripciones, NO bloqueo por plan; único seam para cuando exista la plataforma SaaS externa, sin tocar menú/permisos/handlers al reemplazar la implementación. `NavItemAttribute`/`NavigationItemDefinition`/`PermissionCatalogItemDto` ganan metadata opcional `FeatureKey`/`RequiresExternalEntitlement` (default `null`/`false`) — declarativa, no gatea nada todavía. Verificado por grep: no existen `RequiredPlan`/`PlanKey`/`SubscriptionId`/`BillingCycle`/`CommercialPlan`/`SaasBilling` en `backend/src` (ninguna lógica comercial SaaS dentro del ERP Core).
- **No implementado a propósito** (fuera de alcance de este ticket, explícitamente diferido): jerarquía SuperAdmin/global, tablas de planes, billing, enforcement real de plan en `GetProfilePermissionAuditHandler` (`BlockedByPlan` sigue sin calcularse) — mismo gap documentado, ahora con el punto de extensión ya preparado para cuando se resuelva.
- **Tests**: `UpsertProfilePermissionsHandlerTests` pasa de 2 a 8 casos (inexistente, no asignable, Admin bypass, escalamiento bloqueado/permitido, revocar sin restricción, contexto operativo no resoluble). Domain 936/936, Application 1266/1266, Architecture 101/101 (incluye `PlatformControlPlaneGuardTests` — sin fugas SaaS/billing en `ERP.Domain`), API 24/24 (subset filtrado por Permission/ProfilePermission — no se re-ejecutó el suite API completo en esta sesión), Infrastructure 494/496 (2 fallas preexistentes no relacionadas con este ticket — `DocumentSequenceExclusivityTests` × 2, `SupplierPaymentSequence`; no confundir con la falla preexistente de `InventoryAdjustmentsEndToEndTests.Escenario3` (Kardex/costeo), que es del suite API y no se tocó en esta sesión).

---

## NAV-PERMISSION-HIERARCHY-SSOT-01 — Fuente única backend para menú y catálogo de permisos (2026-08-29)

**Estado: COMPLETADO.** El backend queda como fuente única de la jerarquía Módulo → Categoría → Pantalla → Permiso, consumida tal cual tanto por el launcher como por la pantalla de Asignación de permisos.

- **Diagnóstico**: `GetPermissionCatalogHandler` ya leía `KernelRegistry.Navigation` 100% en memoria (mismo origen que el menú, sin catálogo paralelo) — no había dos sistemas que unificar, solo faltaba el nivel Categoría entre Módulo y Pantalla. Una sesión previa había parchado esto con `LAUNCHER_REGROUP_RULES`/`regroupModuleItems` puramente en frontend (`navConfig.ts`), duplicando conocimiento de categorización que le corresponde al backend.
- **Diseño**: Categoría = mismo patrón "ítem contenedor" ya usado por "Compras"/"Configuración"/"Reportes" (`[NavItem]` sin `Permission` propio, con `PermissionsAnyCsv` y children vía `ParentId`) — sin atributo nuevo, sin columna nueva en `ui_nav_items`/`ui_nav_groups`, sin cambios en `NavigationSyncService`/`NavigationBuilder` (ya recursivo sin límite de profundidad).
- **Backend**: 17 categorías nuevas agregadas en `SuppliersModule.cs`/`CustomersModule.cs`/`ProductsModule.cs`/`AccountingModule.cs`/`SettingsModule.cs`/`AdminModule.cs`, reparentando las pantallas que quedaban sueltas bajo el módulo. `sales`/`inventory` sin tocar (ya 100% categorizados). `PermissionCatalogDto`/`GetPermissionCatalogHandler` ganan el nivel `Categories`, derivado de los mismos `ParentItemId` que ya usa `NavigationBuilder` para el árbol del menú — una sola fuente, dos consumidores.
- **Frontend**: `LAUNCHER_REGROUP_RULES`/`regroupModuleItems` eliminados por completo de `navConfig.ts` — el launcher renderiza el árbol que entrega el backend, sin categorización propia. `PermissionsAssignmentPage.tsx`/`adminPermissionsService.ts` extendidos con el nivel Categoría (selección masiva por categoría, búsqueda que matchea también categoría).
- **No se cambiaron**: rutas existentes, permisos existentes, contratos públicos críticos (solo el shape interno de `PermissionCatalogDto`, endpoint SPA-only desplegado junto con su único consumidor), colores/Design System, ni enforcement de seguridad efectivo.
- **SECURITY-PERMISSION-SCOPE-01** queda documentado como deuda separada (comentarios en `UpsertProfilePermissionsHandler.cs`/`GetProfilePermissionAuditHandler.cs`): validación real de alcance del asignador, límite Admin de empresa vs. SuperAdmin, y enforcement real de plan SaaS en guardado/auditoría — gaps preexistentes, no introducidos ni resueltos por este ticket.
- **Tests**: Domain 936/936, Application 1261/1261, API 390/391 (1 preexistente no relacionado — `InventoryAdjustmentsEndToEndTests.Escenario3`, Kardex/costeo), Infrastructure 494/496 (2 preexistentes no relacionados — `DocumentSequenceExclusivityTests`/`SupplierPaymentSequence`). Frontend Vitest 1047/1047.
- **Validaciones**: `dotnet build` limpio. Frontend `npx tsc --noEmit`/`npm run lint`/`npm run build`/`check-i18n-keys.mjs`/`git diff --check` limpios. `npm run architecture:check`: solo violaciones repo-wide preexistentes (module-boundaries/css-prefixes/design-system/backend-subscriber-rules), ninguna en archivo tocado por este ticket.
- Verificado visualmente (harness Playwright aislado, sin backend) que launcher y pantalla de permisos muestran la misma ubicación funcional para cada pantalla.

---

## ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — Cuenta contable dinámica por destino financiero (2026-08-26)

**Estado: COMPLETADO.** Los asientos de cobros/pagos ya pueden usar una cuenta distinta de Caja General/Bancos según el destino financiero (caja/banco específico) elegido en el cobro o pago — antes esas dos reglas Finance apuntaban siempre a una cuenta fija de la `PostingRule`.

- **Diagnóstico (revisado en código real, no asumido)**: `PaymentMethod` es tenant-global (compartido por todas las companies del tenant), mientras `Account` es company-scoped — poner `AccountId` directo en `PaymentMethod` habría roto multi-company. `CompanyFinancialDestination` (Finance) ya es company-scoped y ya exige una `AccountingAccountId` activa/postable desde su creación (`CreateCompanyFinancialDestinationHandler`) — infraestructura ya construida, sin usar en el flujo de Cobros/Pagos. `Payment`/`CollectionAppliedEvent`/`SupplierPaymentAppliedEvent` no transportaban ningún destino; `PostingFact`/`JournalFactory` no tenían forma de sustituir una cuenta de `PostingRule.Lines`.
- **Decisión técnica**: se agregó `FinancialDestinationId` opcional a `Payment`/`RegisterCollectionCommand`/`RegisterPaymentCommand`/eventos — si se especifica, valida existencia/empresa/activo en el handler (bloquea el comando con `ValidationFailure` si es inválido, igual que cualquier otra referencia mal formada). `PostingFact` gana 3 campos opcionales al final (`OverrideAmountKind`/`OverrideAccountNature`/`OverrideAccountId`, mismo patrón aditivo ya usado por P0-02 Fase 6/FLOW-READY-02F.2) resueltos por los traductores Finance ANTES de construir el `PostingFact` (nunca se tocó `PostingEngine.cs`) — cuenta inválida/inactiva en el destino → log-and-continue, cae al comportamiento actual. Nuevo `PostingLineAccountResolver` (compartido por `JournalFactory` y `PostingAccountGuard`, sin condicionales por SourceModule/FactType — ADR-026 §6.2) sustituye la cuenta de la línea que matchee `(AmountKind, Nature)`; `PostingAccountGuard` valida la cuenta EFECTIVA (ya con el override aplicado), así que un destino cuya cuenta se desactivó después de configurarse sigue bloqueando solo el posting, nunca la operación.
- **No se tocó `PaymentMethod` ni `CompanyFinancialDestinationController`** (su doc ya declara "únicamente los 4 casos de uso aprobados, sin CRUD genérico" — extenderlo sin ADR quedó fuera de alcance).
- **Migración**: `20260826005327_AddPaymentFinancialDestinationId` — columna `financial_destination_id` nullable + FK Restrict en `payments`, aplicada en local. Aditiva, sin backfill, sin tocar asientos históricos.
- **Frontend**: `RegisterCollectionModal.tsx`/`RegisterPaymentModal.tsx` — nuevo selector opcional "Destino financiero" (reutiliza `financialDestinationService.list(true)`, mismo patrón `ZhSelect` ya usado para forma de pago) con el texto de ayuda contextual pedido. Sin componentes nuevos, sin CSS nuevo, sin entradas de menú nuevas.
- **Hallazgos pre-existentes corregidos** (no introducidos por este ticket, detectados al correr la suite completa por primera vez sobre el trabajo de ACCOUNTING-BASE-CHART-TEMPLATE-13): `AccountingBootstrapStep.cs` nunca se agregó a la allowlist de `IgnoreQueryFiltersAuditTests` (C#) pese a estar correctamente registrado en DI vía factory delegate — el regex del test de gobierno de bootstrap steps no reconocía ese patrón de registro; ambos corregidos (allowlist + regex ampliado), sin tocar `DependencyInjection.cs` ni el seed.
- **Tests**: 12 nuevos/actualizados — `PaymentTests` (Domain, evento incluye destino), `CollectionAppliedPostingTranslatorTests`/`SupplierPaymentAppliedPostingTranslatorTests` (override válido, destino inactivo/inexistente → fallback), `CollectionAndSupplierPaymentPostingPipelineTests` (pipeline real: transferencia postea a Banco en vez de Caja; cuenta del destino ya no postable bloquea solo el posting), `RegisterCollectionCommandHandlerTests` (propaga destino válido, rechaza destino inactivo, no consulta el repo si no se especifica). Suite completa backend: 2958/2959 verde (único rojo: `InventoryAdjustmentsEndToEndTests.Escenario3`, Kardex/costeo, sin relación con este ticket, no tocado).
- **Validaciones**: `dotnet build ERP.slnx` limpio. `dotnet test ERP.slnx` completo. Frontend: `npx tsc --noEmit` limpio, `npm run lint` sin errores nuevos, `npm run build` verde. `npm run architecture:check`: 25 violaciones preexistentes (module-boundaries/css-prefixes/design-system/backend-subscriber-rules) — ninguna en archivo tocado por este ticket, mismo baseline de drift ya documentado en entregas anteriores.
- **Brecha restante**: sin UI para configurar "destino financiero por defecto según forma de pago" (habría requerido tocar la superficie deliberadamente cerrada de `CompanyFinancialDestinationController`) — hoy el destino se elige manualmente por transacción; queda como mejora futura si el negocio lo pide explícitamente.

---

## ACCOUNTING-REPORT-ENDPOINTS-SWAGGER-AUDIT-11D — Auditoría de reportes contables en Swagger (2026-08-25)

**Estado: CERRADO — sin bug, sin cambios de código.** Auditó por qué Mayor (`general-ledger`) y Balance de Comprobación (`trial-balance`) "no aparecieron en Swagger" durante ACCOUNTING-POSTING-SMOKE-11C.

- **Revisado**: `AccountingReportsController.cs` (los 5 endpoints — `general-journal`, `general-ledger`, `trial-balance`, `income-statement`, `balance-sheet` — todos con `[HttpGet]`/`[Route("api/v1/accounting/reports")]`/`[Authorize(Policy = "perm:AccountingPermissions.View")]` correctos, sin `[ApiExplorerSettings(IgnoreApi = true)]` ni condicional de registro), `AccountingController.cs` (sin colisión de rutas), `SwaggerExtensions.cs` (un solo `SwaggerDoc("v1", ...)`, sin `DocInclusionPredicate` ni filtro que excluya acciones específicas — los únicos 2 controllers con `IgnoreApi = true` en todo el proyecto son `DevCacheController`/`SpaMenuCatalogController`, no relacionados), y `accountingApi.ts`/`*ReportTab.tsx` del frontend (los 5 endpoints sí están consumidos por UI real).
- **Verificación en vivo**: `dotnet build` (0 errores) → API levantada localmente → `GET /swagger/v1/swagger.json` → los 5 paths de `api/v1/accounting/reports/*` están presentes, incluidos `general-ledger` y `trial-balance`.
- **Causa exacta**: no hay ningún bug ni endpoint faltante — los 5 reportes están correctamente expuestos, autorizados y documentados en Swagger. La ausencia observada en el smoke 11C fue una omisión de verificación de ese smoke (no llegó a revisarlos), no una condición real del sistema.
- **Sin cambios de código** — ni en `PostingEngine`, ni en rutas, ni en reportes, ni en frontend.

---

## ACCOUNTING-POSTING-SMOKE-11C — Retail Posting Smoke (2026-08-25)

**Estado: Validated / Approved.**

- **Empresa usada**: E2E Company (empresa/usuario de prueba oficial `E2ESeedService`) — no ZH TECH, para evitar tocar credenciales reales de administrador.
- **Documentos validados**: factura de compra confirmada, factura de venta autorizada, cobro de cliente aplicado, pago a proveedor aplicado.
- **Asientos generados**: Purchases/InvoiceReceived, Sales/InvoiceIssued, Sales/CostOfGoodsSold, Finance/CollectionApplied, Finance/SupplierPaymentApplied.
- **Resultado**:
  - `journal_entries` 0 → 5.
  - Todos `Posted`.
  - Todos balanceados, Debit = Credit.
  - Libro Diario (API) con trazabilidad hacia los documentos fuente.
- **Notas**:
  - Sin posting retroactivo.
  - Sin modificación de documentos operativos existentes.
  - SRI no disponible en dev local — no bloquea contabilidad.
  - Mayor y Balance de Comprobación quedaron en auditoría Swagger — **cerrado sin hallazgos en ACCOUNTING-REPORT-ENDPOINTS-SWAGGER-AUDIT-11D** (ver arriba): ambos endpoints existen, están autorizados y expuestos correctamente.

---

## MENU-FINAL-STRUCTURE-VERIFY-01 — Verificación post-reorganización del menú (2026-08-22)

**Estado: COMPLETADO — verificación pasó sin hallazgos, cero cambios de código.** Auditoría de todo el trabajo de menú de la sesión (MENU-P0-FIX-01 → MENU-MODULE-REORG-01 → MENU-UX-RENAME-01 → MENU-FINAL-STRUCTURE-01) contra la jerarquía final exacta pedida.

- **Estructura**: se leyó el contenido actual de los 8 archivos de módulo Kernel (`SalesModule.cs`, `PurchasesModule.cs`, `InventoryModule.cs`, `CajaModule.cs`, `ProductsModule.cs`, `MasterDataModule.cs`, `SettingsModule.cs`, `AdminModule.cs`) y se comparó ítem por ítem contra la jerarquía exacta solicitada — coincide en su totalidad (agrupación, orden, labels, Reportes de Caja ausente, Transportistas ausente).
- **Rutas**: se verificaron las 32 rutas reales de NavItems (excluyendo contenedores `*-group`, que nunca son clickeables — `LauncherCategoryGroup`/`LauncherModuleGroup` los renderizan como acordeón, no como `Link`) contra `frontend/src/routes/*.tsx` — las 32 están montadas a un componente real. Cero NavItems huérfanos.
- **Permisos**: se extrajo programáticamente el mapeo (ruta → permiso) del Kernel en el commit previo a todo el trabajo de menú (`941d9a16`) y se comparó contra el estado actual. Único hallazgo: los 3 cambios de permiso ya conocidos y ya validados en **MENU-P0-FIX-01** (`/cash/registers` Manage→View, `/finance/payables` Finance→Purchase, `/finance/receivables` Finance→Sales) — ninguno nuevo. Se confirmó además, como hallazgo positivo (no una regresión), que el contenedor "Ventas" ahora incluye `ElectronicDocumentsPermissions.View` en su `PermissionsAnyCsv` — el contenedor original ("Facturación Electrónica") solo listaba `ElectronicInvoicingPermissions.View`, dejando el Monitor de Documentos Electrónicos con un permiso propio que el contenedor padre no cubría (un usuario con ese permiso pero sin el de facturación electrónica nunca habría podido expandir el contenedor para llegar a él). Corregido como efecto colateral correcto de MENU-MODULE-REORG-01, sin ampliar el acceso de nadie (el permiso del ítem hijo no cambió, la API ya lo exigía igual).
- **APIs y lógica de negocio**: `git diff 941d9a16..HEAD --stat` sobre `ERP.Application/`, `ERP.Infrastructure/` (no-test) y `ERP.API/` completos → **sin cambios, cero archivos**. Todo el trabajo de menú vivió exclusivamente en Kernel Domain (metadata de navegación), tests y i18n/frontend de presentación.
- **Formularios no eliminados**: `git diff 941d9a16..HEAD --diff-filter=D` sobre `frontend/src/` → **cero archivos borrados**. Sobre `backend/` → solo `FinanceModule.cs`/`ReportsModule.cs` (metadata de navegación relocada íntegramente a Ventas/Compras/Inventario, no lógica de negocio).
- **Empresas**: confirmado que preserva ambas funciones — `/companies` (multiempresa, `CompanyManagementHubPage` con `CurrentCompanyCard`) y `/settings/company` (empresa activa, `CompanySettingsHubPage` con tabs perfil/marca/fiscal/decimales) — ambas montadas, ambas con su permiso original intacto (`settings.companies.view`/`settings.company.view`), agrupadas bajo el contenedor "Empresas" sin fusionar ni perder pantalla.
- **Validaciones**: `dotnet build` 0 errores. `ERP.Domain.Tests` filtro Navigation|Kernel 22/22. `ERP.API.Tests` filtro Navigation|Menu|Permissions 22/22. Sin migración EF pendiente. Frontend: `npm run lint` (0 errores), `npx tsc --noEmit` (limpio), `npm run build` (verde), `npm run architecture:check` → `permissions-authorization-rules`/`frontend-permissions-rules`/`i18n-keys` PASS (mismo baseline preexistente en `module-boundaries`/`css-prefixes`/`design-system`/`backend-subscriber-rules`, no relacionado con menú). `git diff --check` limpio.
- **No se requirió ningún cambio de código** — la reorganización de menú de la sesión quedó verificada como correcta sin regresiones.

---

## MENU-FINAL-STRUCTURE-01 — Aplicar jerarquía final del menú ERP (2026-08-22)

**Estado: COMPLETADO.** Ajustes finales de nomenclatura sobre la estructura ya reorganizada en MENU-MODULE-REORG-01 — solo labels/i18n y una reagrupación de contenedor, sin cambios de ruta, permiso ni lógica de negocio.

- **Subgrupo "Operación" → nombre del módulo**: en Ventas/Compras/Inventario/Caja, el primer subgrupo (antes "Operación") ahora se llama igual que el módulo padre (Ventas→Ventas, Compras→Compras, Inventario→Inventario, Caja→Caja), tal como pide la jerarquía exacta solicitada. Mismos Ids/rutas/permisos de sus hijos, solo cambió el texto del contenedor.
- **"Kardex / Movimientos de Inventario" → "Historial de Existencias"**: mismo Id/ruta (`/inventory/kardex`)/permiso; también renombrado el título de la pantalla (`KardexPage.tsx`, clave `kardex.title`) para que menú y pantalla coincidan.
- **"Preferencias operativas" → "Parámetros Generales"**: mismo Id/ruta (`/settings/operations`)/permiso; también renombrado el título de la pantalla (`OperationalPreferencesPage.tsx`, clave `settings.operations.pageTitle`).
- **"Delegación de administración" → "Delegar Funciones"**: mismo Id/ruta (`/admin/security`)/permiso; también renombrado el título de la pantalla (`SecuritySettingsPage.tsx`, clave `security.title`).
- **Empresas — decisión reportada**: se evaluó fusionar "Mis empresas" (`/companies`, multiempresa del suscriptor, permiso `settings.companies.view`) y "Datos de la empresa" (`/settings/company`, empresa activa, permiso `settings.company.view`) en una sola entrada. **No se fusionaron**: son pantallas reales con alcance y permisos distintos (lista/gestión de todas las empresas del tenant vs. hub de configuración operativa de la empresa activa — perfil/marca/fiscal/decimales); fusionarlas habría requerido rediseño de frontend fuera de alcance y arriesgado mezclar permisos o perder funcionalidad. Se implementó la solución segura explícitamente prevista por la tarea: un contenedor "Empresas" nuevo en `SettingsModule.cs` (mismo patrón que Ventas/Compras/Inventario/Caja) agrupa ambas pantallas — el nivel superior de Configuración muestra una sola línea "Empresas" que se expande a "Mis empresas" y "Datos de la empresa", cada una con su Id/ruta/permiso intactos.
- **Tests**: `KernelRegistryTests.cs` — nuevo test verifica explícitamente que ambas pantallas de empresa siguen existiendo con su Id/permiso original bajo el contenedor nuevo. Suites verdes: Domain.Tests filtro Navigation|Kernel 22/22, completa 851/851; API.Tests filtro Navigation|Menu|Permissions 22/22. `dotnet build` sin errores. Sin migración EF. Frontend: `npm run lint` (0 errores), `npx tsc --noEmit` (limpio), `npm run build` (verde), `npm run architecture:check` → `permissions-authorization-rules`/`frontend-permissions-rules`/`i18n-keys` PASS. `git diff --check` sin errores de espacios en blanco.
- **No tocado**: rutas frontend, permisos, APIs, print-agent, `SystemProviderSettingsController`, lógica de negocio. Transportistas sigue oculto (MENU-P0-FIX-01). Reportes sintéticos no reintroducidos — todo sigue viniendo de `GET /api/v1/me/menu`.

---

## MENU-MODULE-REORG-01 — Reorganizar menú por módulos de negocio (2026-08-22)

**Estado: COMPLETADO.** Reagrupó el menú por dominio de negocio (Ventas/Compras/Inventario/Caja agrupan ahora su propia Operación/Configuración/Reportes) sin cambiar rutas, permisos ni lógica de negocio — solo `[Module]`/`[NavItem]` del Kernel, SortOrder y labels/i18n.

- **Soporte de anidamiento verificado antes de implementar**: `NavigationBuilder.BuildItemTree` (backend) y `LauncherCategoryGroup`/`LauncherModuleGroup` (frontend, `zh/header/launcher/`) recursan sin límite de profundidad — se confirmó que el modelo Módulo → Operación/Configuración/Reportes → pantalla (3 niveles) es soportado nativamente, sin inventar arquitectura nueva. Restricción real encontrada: `ParentId` solo resuelve dentro del mismo `[Module]` (mismo `GroupId`) — cada contenedor y sus hijos deben vivir en el mismo archivo de módulo.
- **Productos y servicios**: promovido a módulo propio (`ProductsModule.cs`, antes un contenedor dentro de `inventory`) — Productos, Tipos de Producto, Categorías de Productos, Marcas, Atributos de Productos, Definiciones de Atributos (este último no está en el modelo de negocio explícito pero se mantuvo visible para no perder la pantalla real). Mismos Ids/rutas/permisos.
- **Inventario**: Operación (Bodegas, Kardex, Transferencias), Configuración (Preferencias de Inventario — deep-link), Reportes (Reporte de Inventario, movido desde el módulo `reports` retirado).
- **Ventas**: Operación (Facturas de venta/POS, Devoluciones, Cuentas por Cobrar — movida desde `finance`, Monitor de Documentos Electrónicos), Configuración (Métodos de Pago, Preferencias de Ventas/POS — deep-link), Reportes (Reporte de Ventas, movido desde `reports`). "Facturación Electrónica" (config del certificado/ambiente SRI) se movió a Configuración general por ser transversal, no exclusiva de Ventas.
- **Compras**: Operación (Compras, Recepción electrónica (TXT), Devoluciones, Cuentas por Pagar y Créditos de Proveedor — movidas desde `finance`), Configuración (Preferencias de Compras — deep-link), Reportes (Reporte de Compras, movido desde `reports`).
- **Caja**: Operación (Turno de Caja), Configuración (Cajas registradoras, Preferencias de Caja — deep-link). **Sin Reportes**: no existe pantalla de "Reporte de Caja" — no se creó entrada falsa (regla explícita de la tarea).
- **Preferencias operativas sin duplicar pantalla**: la única pantalla real (`/settings/operations`, tabs por query param `?tab=salesPos|purchases|inventory|cash`) ya soportaba deep-links por tab (`OperationalPreferencesPage.tsx` + `useSearchParams`) — se agregaron NavItems que enlazan a cada tab desde su módulo, sin crear pantallas nuevas ni duplicar la existente.
- **Clientes y proveedores**: `MasterDataModule` aplanado — los contenedores internos "Clientes"/"Proveedores" se retiraron (el módulo completo ya representa ese dominio); ahora Clientes, Proveedores, Condiciones de Pago, Condiciones de Crédito son ítems planos. "Listas de Precios" no está en el modelo de negocio explícito pero se mantuvo (aplica a clientes y proveedores por igual, sin destino más claro). Transportistas sigue oculto (MENU-P0-FIX-01, sin backend).
- **Configuración general**: quedó solo con configuraciones transversales — se insertó "Facturación Electrónica" (antes en Ventas) y se reordenó siguiendo la secuencia pedida (Mis empresas, Datos de la empresa, Sucursales, Establecimientos, Puntos de emisión, Destinos financieros, Facturación Electrónica, Correo SMTP, Preferencias operativas, Geografía — Destinos financieros no está en la lista explícita de la tarea pero es transversal, se mantuvo).
- **Administración**: sin cambios, tal como pedía la tarea.
- **Módulos `finance` y `reports` retirados** (`FinanceModule.cs`/`ReportsModule.cs` eliminados): todos sus ítems se reubicaron dentro de Ventas/Compras/Inventario con el mismo Id explícito que ya tenían — `NavigationSyncService` los reconoce como UPDATE (mismo Id, nuevo `GroupId`/`ParentId`), no como filas nuevas; los grupos vacíos se desactivan solos en el próximo sync (soft, sin borrado físico). Único caso sin Id explícito previo que cambiaba de módulo (`ElectronicInvoicing`, antes en `sales` sin Id fijo) recibió un Id explícito nuevo para evitar que el cambio de módulo generara una fila huérfana.
- **Orden de grupos en el menú**: `MAIN_NAV_GROUP_ORDER` (`frontend/src/nav/navConfig.ts`) — único punto que realmente ordena los grupos de nivel superior en el launcher, porque `NavMenuGroupDto` no viaja con `sortOrder` (confirmado leyendo `types/access.ts`, `NavigationBuilder.cs` y `mapSessionMenuToNavGroups`) — se agregó `products`/`caja` y no se tocó el resto (los backends module SortOrder solo ordenan ítems *dentro* de cada grupo, ya correcto).
- **Tests**: `KernelRegistryTests.cs` reescrito extensamente (nuevos: contenedores Operación/Config/Reportes por módulo, Products module, deep-links de preferencias, finance/reports ya no existen como módulos) — 21/21 (filtro Navigation|Kernel), suite completa Domain.Tests 850/850. `ERP.API.Tests` 22/22 (filtro) + 339/339 completa (controllers de Finance/Purchases siguen validando sus propias políticas `[Authorize]`, sin tocar). `dotnet build` sin errores. Sin migración EF (navegación se sincroniza en runtime desde `KernelRegistry`, nunca vía modelo EF). Frontend: `npm run lint` (0 errores), `npx tsc --noEmit` (limpio), `npm run build` (verde), `npm run architecture:check` → `permissions-authorization-rules`/`frontend-permissions-rules`/`i18n-keys` PASS (confirma que no se tocaron permisos). `git diff --check` sin errores de espacios en blanco.
- **No tocado**: rutas frontend, permisos/APIs, print-agent, `SystemProviderSettingsController`, nombres de pantallas ya fijados en MENU-UX-RENAME-01 (salvo los explícitamente pedidos en el modelo de esta tarea: Facturas de venta/POS, Compras, Recepción electrónica (TXT), Reporte de Ventas/Compras/Inventario, Tipos de Producto), Notas de Crédito de Compra ni Ajustes de Inventario (no expuestos, sin aprobación separada), Transportistas (sigue oculto).

---

## MENU-UX-RENAME-01 — Renombrar menú y pantallas con lenguaje de negocio (2026-08-22)

**Estado: COMPLETADO.** Renombres de labels/títulos/subtítulos/kickers/breadcrumbs detectados en MENU-UX-AUDIT, aplicados sobre el trabajo de permisos de MENU-P0-FIX-01. Solo texto visible — sin cambios de rutas, permisos, APIs ni lógica funcional.

- **Productos y servicios**: grupo contenedor `ProductsGroup` (`InventoryModule.cs`, LabelKey `app.nav.item.inventory.catalog`) renombrado de "Catálogo" a "Productos y servicios" — mismo texto aplicado al `kicker` compartido (`catalog.kicker`) de Marcas/Atributos/Categorías. Ítem "Ítems" → "Productos" (`app.nav.item.inventory.items` + `items.title` de `ItemsPage.tsx`). "Árbol de catálogo" → "Categorías de productos" (`app.nav.item.catalog.tree` + `catalog.tree.title` de `TreeEditor.tsx`). "Grupos de Atributos" → "Atributos de Productos" (`app.nav.item.catalog.attributeGroups` + título/entidad de `AttributeGroupsPage.tsx`) — la pantalla gestiona definiciones de atributos reutilizables (Color, Talla), no genera variantes por sí misma.
- **Clientes y proveedores**: kicker hardcoded `"MasterData"` (string literal sin i18n) reemplazado por `t("masterdata.kicker", "Clientes y proveedores")` en `MasterDataCustomersPage.tsx`, `MasterDataSuppliersPage.tsx` y `MasterDataBusinessPartnerDetailPage.tsx` (este último no tenía `useI18n` importado — se agregó).
- **Empresa / Empresas**: NavItem "Empresas" (`app.nav.item.erp.companies`, ruta `/companies`, multiempresa del suscriptor) → "Mis empresas". NavItem "Company" (`app.nav.item.settings.company`, ruta `/settings/company`, empresa activa) → "Datos de la empresa"; título hardcoded "Configuración Empresarial" de `CompanySettingsHubPage.tsx` migrado a i18n con el mismo texto (`settings.company.pageTitle`/`pageSubtitle` — nombres nuevos para no colisionar con la clave preexistente `settings.company.title` que ya usan los `NoAccessPage` de las pestañas internas).
- **Caja**: NavItem "Sesiones de Caja" (`app.nav.item.caja.sessions`, ruta `/cash`) → "Turno de Caja", alineado con el título ya usado en `CajaPage.tsx` ("Caja" → "Turno de caja", ahora migrado a i18n, `useI18n` agregado — el archivo no lo tenía). NavItem "Administración de Cajas" (`app.nav.item.caja.registers`, ruta `/cash/registers`) → "Cajas registradoras", igual en `CashRegistersPage.tsx` (también sin i18n previamente, agregado).
- **Kardex**: título de menú y de pantalla unificados en "Kardex / Movimientos de Inventario" (antes el menú decía "Kardex" y la pantalla "Centro de Investigación de Inventario", sin relación visible entre sí). Se eliminaron 3 botones deshabilitados "Próximamente" (Excel/PDF/Imprimir) en `KardexPage.tsx` sin handler real — placeholders inertes sin función, no lógica de negocio.
- **Compras — Recepción electrónica**: confirmado que la pantalla solo importa TXT (`accept=".txt"`, método `importTxt`; XML es solo una consulta puntual por fila, no un formato de carga alterno) — título actualizado a "Recepción electrónica (TXT)" para no insinuar soporte XML de carga masiva que no existe.
- **i18n**: todos los strings hardcoded tocados migrados a claves i18n en es/en/qu (sin duplicar claves existentes — se detectaron y corrigieron 2 colisiones de nombre con claves preexistentes de otro propósito antes de cerrar: `settings.company.title/subtitle` ya usada por los `NoAccessPage` de las pestañas de empresa). **qu es best-effort**: las traducciones Kichwa nuevas/editadas en este bloque son aproximaciones compuestas a partir del vocabulario ya presente en `qu.json`, sin revisión de hablante nativo — pendiente de validación si el piloto lo requiere.
- **Tests/build**: `dotnet build` del solution completo sin errores. `ERP.Domain.Tests` 20/20 y `ERP.API.Tests` 22/22 (filtros Navigation|Kernel / Navigation|Menu|Permissions) verdes — ningún test depende del texto de `Label`/i18n (`KernelRegistry` no expone `Label`, solo `PermissionKey`/`LabelKey` como claves, no como texto traducido). Sin migración EF. Frontend: `npm run lint` (0 errores), `npx tsc --noEmit` (limpio), `npm run build` (verde), `npm run architecture:check` → `i18n-keys` PASS (se corrigió una clave qu faltante detectada por el propio guardrail antes de cerrar). `git diff --check` sin errores de espacios en blanco.
- **No tocado**: rutas frontend, permisos, APIs, print-agent, `SystemProviderSettingsController`, reorganización del menú por módulos, terminología SRI/RUC/IVA/Retenciones/Facturación electrónica.

---

## MENU-P0-FIX-01 — Corrección de hallazgos P0 del menú general (2026-08-22)

**Estado: COMPLETADO.** Cierra los hallazgos P0 de la auditoría del menú general (navegación server-driven vs permisos reales de API), sin reorganizar el menú ni tocar lógica de negocio.

- **Reportes**: el frontend inyectaba un grupo "Reportes" sintético (`ensureReportsGroup` en `frontend/src/nav/navConfig.ts`) sin ningún filtro de permisos — cualquier usuario autenticado veía los 3 reportes aunque no tuviera `sales.view`/`purchases.view`/`inventory.stock.view`. El backend ya tenía `NavItem`s reales y correctamente permission-gated para `/reportes/ventas|stock|compras` (`ReportsModule.cs`, sincronizados por `NavigationSyncService` con el mismo `LabelKey` que ya usaba el frontend), por lo que el fallback era puramente redundante. Se eliminó `ensureReportsGroup` y su call site en `useAppLayoutNavigation.ts`; el menú de Reportes ahora viene 100% del backend.
- **Cuentas por Cobrar / Cuentas por Pagar**: el `NavItem` (`FinanceModule.cs`) exigía `finance.view`, pero `SalesReceivablesController`/`PurchasePayablesController` exigen `sales.view`/`purchases.view` respectivamente — un usuario con `finance.view` pero sin el permiso real veía el ítem en el menú y recibía 403 en cada llamada. Se alineó el permiso del `NavItem` al permiso real de cada API.
- **Cajas registradoras**: el `NavItem` (`CajaModule.cs`) exigía `caja.manage`, pero el GET/listado real de `CashRegisterController` solo exige `caja.view` — un usuario con permiso de solo lectura no veía la entrada de menú. Se cambió el `NavItem` a `caja.view`; create/update/enable/disable siguen protegidos por `caja.manage` a nivel de API (sin cambios ahí).
- **Transportistas**: `carrierService.ts` (frontend) llama a `api/v1/logistics/carriers*`, pero no existe ningún controller backend para esa ruta — toda operación devuelve 404. Se retiró el `NavItem` de Transportistas (`MasterDataModule.cs`) para que la pantalla rota no sea alcanzable desde el menú; no se implementó backend de Transportistas (fuera de alcance de este bloque).
- **Tests**: `KernelRegistryTests.cs` actualizado para reflejar los permisos corregidos de Receivables/Payables. Suites verdes: `ERP.Domain.Tests` (20/20 filtro Navigation|Kernel|Permissions), `ERP.API.Tests` (22/22 filtro Navigation|Menu|Permissions|Reports). `dotnet build` del solution completo sin errores. Sin migración EF (`has-pending-model-changes` → ninguno; la navegación se sincroniza en runtime desde `KernelRegistry`, no vía modelo EF). Frontend: `npm run lint` (0 errores), `npx tsc --noEmit` (limpio), `npm run build` (verde) — warnings preexistentes no relacionados.
- **No tocado**: `print-agent/`, `SystemProviderSettingsController` (sigue sin exponerse), reorganización del menú por módulos, nombres de ítems.

---

## COMMUNICATIONS-SETTINGS-UI-01 — Configuración SMTP por empresa (2026-08-21)

Cierra el hallazgo P0 de CONFIG-AUDIT-01: `communications.email.*` ya tenía SSOT en `OrgSettings` (scope=Company) pero ningún endpoint/pantalla lo escribía — el piloto dependía por completo del fallback `Communications:Email:*` por variable de entorno (ver línea "SMTP documentado" del cierre ERP-CORE-CLOSEOUT-10-FINALIZE arriba, ahora superada).

- **Backend**: `GetCompanyEmailSettingsQuery`/`UpdateCompanyEmailSettingsCommand`/`SendTestEmailCommand` (`ERP.Application/Modules/Communications/UseCases/`) + `CommunicationsEmailSettingsController` (`GET`/`PUT /api/v1/communications/email-settings`, `POST .../test`), permisos `communications.view`/`communications.configure`. Password nunca se devuelve en `GET` (solo `passwordConfigured: bool`); se persiste cifrado con `ISecretProtector` (mismo mecanismo que la contraseña del certificado SRI); un `PUT` sin password nueva conserva la existente.
- **Bug real corregido en el camino** (`CommunicationSettingsResolver`, `ERP.Infrastructure/Communications/`): (1) consultaba `OrgSettings` con `scopeId=Guid.Empty` en vez del `companyId` real — la capa OrgSettings nunca se leía en la práctica; (2) `IOrgConfigResolver.GetValueAsync<T>` no puede distinguir "no configurado" de "configurado como `false`/`0`" para tipos valor (no tiene `where T : struct`) — rompía el fallback a env var para `Enabled`/`UseSsl`/`SmtpPort`/`MaxRetries`. Ambos corregidos localmente en el resolver de Communications (lectura de string crudo + parseo propio), sin tocar el resolver genérico compartido.
- **Frontend**: `/settings/communications/email` (`modules/configuracion/comunicaciones/`), patrón idéntico a `/settings/electronic-invoicing` (useForm+zodResolver+`applyServerErrors`, componentes ZH, password con toggle mostrar/ocultar tipo SRI). Muestra `Contraseña configurada: Sí/No` y la fuente actual (`OrgSettings` vs `EnvironmentFallback`). Incluye envío de correo de prueba sin tocar Sales/POS.
- **Tests**: 9 tests nuevos en `ERP.Application.Tests/Communications/` (GET nunca expone password, aislamiento multi-tenant, password nueva se cifra, `PUT` sin password conserva la existente, `Enabled` sin campos requeridos → 422, `SendTestEmail` usa la config de la empresa actual) + 3 en `ERP.Infrastructure.Tests/Communications/` (regresión de ambos bugs del resolver). Suites completas verdes: 1013 Application, 451 Infrastructure. `dotnet build` (0 errores), `npx tsc --noEmit`, `npm run lint`, `npm run build` (incluye `run-platform-guard`, allowlist actualizado con `/api/communications`) todos verdes. Sin cambios de esquema (`has-pending-model-changes` → ninguno).
- **Variables de entorno `Communications:Email:*`**: siguen funcionando como fallback de infraestructura (no se removieron) — quedan como respaldo, ya no como único camino.

### COMMUNICATIONS-SETTINGS-UI-01B — Menú (2026-08-21)

La pantalla renderizaba bien por URL directa pero no aparecía en el menú. Causa raíz: el `[AppFeature(...)]` puesto sobre `CommunicationsEmailSettingsController` (mismo patrón que `ElectronicInvoicingController`) alimenta la tabla `app_features` — que **no es lo que arma el menú real**. La navegación server-driven (`GET /api/v1/me/menu`) la construye `NavigationBuilder` leyendo `ui_nav_groups`/`ui_nav_items`, sincronizados en cada arranque por `NavigationSyncService` a partir de los atributos `[NavItem]` declarados en `ERP.Domain.Kernel` (nunca por migración/seed). `SystemProviderSettingsController` tiene el mismo problema latente (mismo `[AppFeature]` sin `[NavItem]` correspondiente) — fuera de alcance de esta tarea, no se tocó.

- **Fix**: un `[NavItem]` nuevo en `SettingsModule.cs` (`ERP.Domain/Kernel/Modules/`), `Permission = CommunicationsPermissions.View`, `SortOrder = 70`, ruta `/settings/communications/email` — mismo nivel que Company/Branches/Establishments/Geography/FinancialDestinations bajo "Configuración" (no se creó submenú "Comunicaciones" nuevo, para no rediseñar el menú). `NavigationSyncService` lo sincroniza a `ui_nav_items` automáticamente en el próximo arranque de `ERP.API`, sin migración EF.
- **Permisos**: `communications.view` (ver) / `communications.configure` (guardar/probar correo) ya existían del cierre anterior — el rol Admin los recibe automáticamente (`NavigationBuilder.IsItemVisible`/`usePermissionsUi` conceden wildcard a Admin, sin necesidad de fila de grant materializada); un usuario no-admin sin el permiso no ve el ítem en el menú y, si entra por URL directa, la pantalla bloquea con `NoAccessPage` (ya implementado en el cierre anterior).
- **Tests**: nuevo `Navigation_contains_settings_communications_email_with_communications_view_permission` en `KernelRegistryTests.cs` (Id único, `(GroupId, RoutePath)` único, `PermissionKey` existe en `KernelRegistry.Permissions`, `SortOrder`) — suite completa verde: 15/15. `npx tsc --noEmit`, `npm run build` (incluye `run-platform-guard`) verdes. Sin cambios de esquema.

---

## ERP-CORE-CLOSEOUT-10-FINALIZE — Cierre final sin pendientes técnicos accionables (2026-08-21)

### Veredicto final

**ERP Core queda listo para piloto técnico.** Pendientes técnicos accionables en el repositorio: **ninguno**. Bloques cerrados: **01 a 10**. Solo quedan pendientes externos inevitables, listados abajo con procedimiento ya preparado para cuando estén disponibles.

- **Tirilla (Print Agent)**: preparada — cola persistente, reintentos, `Driver: "windows-raw"`, instalación como servicio Windows, 21 tests verdes. Pendiente prueba física real por falta de impresora térmica disponible.
- **Correo (SMTP)**: preparado — outbox desacoplado (nunca bloquea una venta), resolución en dos capas (`OrgSettings` → fallback `Communications:Email:*`/env vars) ya implementada y probada. Pendiente credencial SMTP real (Zoho u otro) para el smoke de envío end-to-end.
- **SRI proveedor de sistema (XML)**: configuración dinámica lista (`SystemProviderSettings`, singleton de instancia). Pendiente el texto de la Resolución NAC-DGERCGC26-00000027 o ficha técnica SRI que confirme el campo/elemento exacto antes de tocar los XML builders — normativa, no técnica.

### Corrección a un hallazgo del cierre anterior (ERP-CORE-CLOSEOUT-10)

El cierre anterior afirmó que el backup de PostgreSQL/FileStorage era "procedimiento manual, no programado" — **eso era impreciso**: `scripts/backup-localprod.ps1` (dump + FileStorage + checksums + manifest) y `scripts/restore-check-localprod.ps1` (drill de restore completo en un entorno descartable, sin tocar el stack real) ya existían y están documentados en detalle en `docs/BACKUP_RESTORE_LOCALPROD.md` — no se habían revisado en el cierre anterior. Lo único que realmente falta es agendar la ejecución periódica (no existe cron/scheduler todavía) — eso sí queda clasificado como externo/operativo, no como código faltante.

### Qué se revisó y su clasificación

| Punto | Clasificación | Detalle |
|---|---|---|
| Health checks API/Postgres/Redis, `depends_on: service_healthy` | **Cerrado** | Corregido en el cierre anterior (`docker-compose.localprod.yml`), reverificado con `docker compose config`. |
| Volúmenes persistentes: Postgres, FileStorage, logs API | **Cerrado** | `erp_saas_pgdata`, `erp-api-files`, `erp-api-logs` (este último agregado en el cierre anterior) — documentados en `docs/DOCKER_LOCAL_PROD.md`. |
| Secretos en `docker-compose*.yml`/`.env*.example` | **Cerrado** | Sin secretos reales; `POSTGRES_PASSWORD`/`JWT_SECRET_KEY` sin default en `compose.base.yml` (falla si no se exportan) — ningún compose de prod puede heredar un password débil. |
| Migraciones EF aplicables desde cero | **Cerrado** | Re-verificado en este cierre: 27 migraciones aplicadas sin error contra un Postgres 16 real (contenedor temporal, no Testcontainers). `has-pending-model-changes` → sin cambios. Comando documentado en `docs/DOCKER_LOCAL_PROD.md` §5 y `docs/DEVELOPMENT.md`. |
| Backup/restore/rollback PostgreSQL + FileStorage | **Cerrado** | Scripts ya existentes (`backup-localprod.ps1`, `restore-check-localprod.ps1`) + `docs/DOCKER_LOCAL_PROD.md` § Rollback de la aplicación (nuevo en este cierre: rollback de contenedores por commit + advertencia sobre downgrade de esquema). `docs/deployment/README.md` corregido para referenciar los scripts reales en vez de comandos genéricos inventados. |
| Backend config (appsettings, guardas de arranque, CORS, Swagger, Hangfire, JWT) | **Cerrado** | Reverificado directamente en `Program.cs`: guard fail-fast en Production para `Jwt:SecretKey`/`ConnectionStrings:DefaultConnection`/`Cors:AllowedOrigins` con placeholder o vacíos; CORS sin `AllowAnyOrigin`; Swagger solo Development/Testing; Hangfire deshabilitado por defecto sin bloquear ventas ni Communications. |
| Frontend config (`.env` examples, `VITE_API_URL`, `VITE_PRINT_AGENT_*`) | **Cerrado** | `VITE_API_URL` vacío = proxy relativo `/api` (funciona en dev y en Docker vía nginx); las 4 variables `VITE_PRINT_AGENT_*` documentadas en `.env.development.example`, comentadas, sin clave real; Vite no embebe ningún secreto por defecto en el bundle. |
| SMTP documentado (OrgSettings + fallback env vars) | **Cerrado** | Nueva sección en `docs/deployment/README.md`: tabla de variables `Communications__Email__*` (ejemplo Zoho), confirmación de que no bloquea ventas, y nota honesta de que el endpoint de administración de `communications.email.*` vía OrgSettings **no existe todavía** (se usa el fallback por variables de entorno para el piloto) — no se inventó ni se implementó esa pantalla en este cierre (sería alcance funcional nuevo). |
| Print Agent — versionado, README, prueba física | **Cerrado** | `print-agent/` ya está versionado (43 archivos trackeados, no `?? print-agent/`) — no hacía falta un commit separado. Build (`ZH.PrintAgent.sln`) y tests (21/21) verdes. README ya cubría instalación/ApiKey/DataDirectory/`windows-raw`/nombre de cola; se agregó la advertencia explícita de prueba física pendiente. |
| SRI/certificado — documentación de dependencia física vs. electrónica | **Cerrado** | Nueva sección en `docs/deployment/README.md`: factura física sin dependencia del certificado (garantía estructural), venta electrónica con error claro (nunca 500) si falta certificado/settings, endpoint de readiness, y el punto normativo pendiente del proveedor de sistema. |
| `Deployment:SuperAdminPanelEnabled` | **No aplicable por arquitectura** | Esa clave de configuración no existe en `backend/src` — ERP Core no tiene panel SuperAdmin/Platform por diseño (`ERP_CORE_FREEZE.md`). Discrepancia de alcance de la tarea, no un defecto de este repo. |
| Dominio `.com.ec` + SSL real | **Externo inevitable** | Requiere dominio registrado y decisión de proveedor de certificado — documentado en `docs/deployment/README.md`, sin inventar configuración TLS sin el dominio real. |
| Credenciales SMTP reales (Zoho) | **Externo inevitable** | El código/config ya está listo (ver tabla de variables); falta la cuenta real. |
| Impresora térmica física | **Externo inevitable** | El agente y su README ya están listos; falta el hardware para la prueba end-to-end. |
| Certificado `.p12` SRI real por empresa piloto | **Externo inevitable** | El flujo de subida/validación ya está implementado (ERP-CORE-CLOSEOUT-06/07); falta el certificado real de la empresa piloto. |
| Texto/ficha técnica de la Resolución NAC-DGERCGC26-00000027 | **Externo inevitable** | Ver ERP-CORE-CLOSEOUT-09 — no se puede confirmar la estructura XML sin la fuente normativa oficial; no se inventó. |
| Backups productivos con periodicidad automatizada | **Externo inevitable (operativo)** | Los scripts de backup/restore ya funcionan (ver arriba); falta solo agendarlos (cron/Task Scheduler) en el entorno real del piloto — decisión operativa, no de código. |

### Validado en este cierre

- `dotnet build backend/src/ERP.slnx --no-restore` → 0 errores.
- `npm run build` (frontend) → build correcto.
- Migraciones EF aplicadas **desde cero** contra Postgres 16 real (segunda verificación independiente, contenedor temporal nuevo) → sin errores. `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
- `dotnet build print-agent/ZH.PrintAgent.sln --no-restore` → 0 errores. `dotnet test print-agent/ZH.PrintAgent.sln --no-build` → 21/21 verdes.
- `git diff --check` limpio. `git status` revisado antes de cualquier commit propuesto — sin `bin/`, `obj/`, `data/`, `TestResults/` en los cambios.
- Efecto colateral detectado y revertido dos veces en este cierre: `npm run build`/`dotnet build` regeneran automáticamente `docs/ci/PLATFORM_GUARD_REPORT.md` y `docs/future-platform/API_USAGE_GRAPH.json` con timestamp nuevo (contenido idéntico, `PASS`/0 violaciones) — revertidos por no ser cambios semánticos reales.

### Archivos modificados en este cierre (pendientes de commit — no se commiteó nada todavía)

`STATUS.md`, `docker-compose.localprod.yml` (ya modificado en el cierre anterior, sin cambios adicionales en este), `docs/DOCKER_LOCAL_PROD.md`, `docs/deployment/README.md`, `print-agent/README.md`. Ninguno mezcla código de backend/frontend con print-agent en el mismo cambio — todo es documentación/configuración Docker.

---

## ERP-CORE-CLOSEOUT-10 — Preparación despliegue piloto (2026-08-21)

**Estado: COMPLETADO.** Auditoría de entorno/Docker/variables/migraciones/seeds/health/logs/seguridad mínima para el piloto. Se validó de punta a punta (build backend/frontend, migraciones EF aplicadas desde cero contra Postgres real, `docker compose config`) y se corrigieron **2 gaps reales de infraestructura**; el resto del entorno ya estaba listo.

**Corregido**:
- `docker-compose.localprod.yml`: `erp-frontend` dependía de `erp-api` con `condition: service_started` (arranque del contenedor), no `service_healthy` — nginx podía empezar a proxyear `/api/*` mientras la API todavía aplicaba migraciones/bootstrap. Corregido a `service_healthy`.
- `docker-compose.localprod.yml`: los logs de Serilog (`logs/erp-.txt`, resuelve a `/app/logs` por el `WORKDIR` del Dockerfile) no tenían volumen — se perdían en cada recreación del contenedor. Se agregó el volumen nombrado `erp-api-logs`.
- `docs/deployment/README.md` (antes un placeholder de una línea): se agregaron procedimientos concretos de **backup** (`pg_dump`/`pg_restore` contra el contenedor `postgreszh`, con nota explícita de que hoy es manual, no programado), **rollback** (rebuild desde commit anterior — no hay registry de imágenes versionado todavía; downgrade de EF requiere revisar el `Down()` de la migración) y **dominio/SSL** (documentado honestamente como pendiente externo no resuelto, sin inventar una configuración TLS sin dominio real).

**Validado end-to-end (no solo revisado)**:
- `dotnet build backend/src/ERP.slnx --no-restore` → 0 errores.
- `npm run build` (frontend) → build correcto (solo warnings preexistentes de tamaño de chunk).
- Migraciones EF aplicadas **desde cero** contra un Postgres 16 real (contenedor temporal, no Testcontainers) — las 27 migraciones corrieron sin error hasta `AddSystemProviderSettings`. `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
- `docker compose -f docker-compose.yml -f docker-compose.localprod.yml config` → renderiza correctamente (validación estática, sin levantar contenedores).
- `git diff --check` limpio.

**Confirmado sin defectos** (auditado, no corregido): guard de arranque que falla rápido en Production si `Jwt:SecretKey`/`ConnectionStrings:DefaultConnection`/`Cors:AllowedOrigins` quedan con el placeholder o vacíos (`Program.cs`) — ningún secreto real en el repo, solo placeholders `CHANGE_ME_*`. CORS sin `AllowAnyOrigin`, con fallback a `localhost` inalcanzable en Production por el guard anterior. Swagger habilitado solo en Development/Testing. Hangfire deshabilitado por defecto (`Hangfire:Enabled=false`) sin romper el arranque ni las colas de Communications — los jobs simplemente no se programan; una venta/factura nunca depende de que Hangfire esté activo. Migraciones y bootstrap global se auto-aplican en cada arranque de la API (`db.Database.MigrateAsync()` + `GlobalBootstrapOrchestrator`); una empresa piloto llega a estado operativo solo con `POST /api/v1/setup/admin` (sin intervención manual en BD, coherente con ERP-CORE-CLOSEOUT-06). Volumen `erp-api-files` ya persistía certificados P12/XML/RIDE correctamente. Dockerfile backend ya en Alpine/musl con el fix de SkiaSharp Linux confirmado (ERP-CORE-CLOSEOUT-07); Dockerfile frontend sirve build estático vía nginx, no dev server. Variables `VITE_*` no embeben ningún secreto por defecto en el bundle. `.env.docker.local.example`/`compose.base.yml` fuerzan `POSTGRES_PASSWORD`/`JWT_SECRET_KEY` sin default real — un compose de prod no puede heredar silenciosamente una contraseña débil.
- Nota menor no bloqueante: `.env.example` (solo para dev local, nunca alcanzable por prod por el guard de arranque) trae un password de conveniencia no vacío — documentado en el propio archivo como dev-only, no accionado.

**Discrepancia de alcance detectada**: el punto "SuperAdmin panel controlado por `Deployment:SuperAdminPanelEnabled`" no aplica — esa clave de configuración no existe en ningún lugar de `backend/src`, consistente con hallazgos de auditorías previas (ERP-CORE-CLOSEOUT-05): este repo de ERP Core no contiene ningún panel de SuperAdmin/Platform por diseño arquitectónico (`ERP_CORE_FREEZE.md`, "ERP never depends on Platform"). No es un defecto de este repo — probablemente una referencia cruzada a un flag de otro producto (ZH Platform).

### Checklist operativo del piloto

1. **Crear empresa**: `POST /api/v1/setup/admin` con el token de instalación impreso en consola al primer arranque → crea Tenant + Company + admin + `CompanyUserMembership` + `CompanyUserBranch` a la sucursal principal (fix de ERP-CORE-CLOSEOUT-06). Bootstrap automático crea sucursal, bodega, establecimiento, punto de emisión, caja, secuencias, métodos de pago, cliente "Consumidor Final" y lista de precios por defecto — sin pasos manuales adicionales.
2. **Sucursal/bodega/caja adicionales** (si el piloto necesita más de una sucursal): crear vía `/settings/branches`, `/inventory/warehouses`, `/settings/cash-registers` — cada uno valida pertenencia a la empresa activa (ERP-CORE-CLOSEOUT-05-FIX01).
3. **Abrir caja**: requiere `CashRegister` con `EmissionPointId` asignado — sin caja abierta, Ventas bloquea con mensaje claro ("No existe una caja abierta para realizar ventas.").
4. **Compra**: requiere bodega válida en la sucursal activa — bloquea con mensaje claro si falta.
5. **Venta física**: funciona sin ningún dato de facturación electrónica configurado (aislamiento estructural confirmado en ERP-CORE-CLOSEOUT-07).
6. **Venta electrónica**: requiere `SriSettings` (ambiente + WSDL) y certificado `.p12` subido vía `/settings/electronic-invoicing` — sin eso, bloquea con mensaje claro, nunca un 500. El endpoint `GET /api/companies/operational-readiness` muestra exactamente qué falta antes de intentar vender.
7. **RIDE**: disponible solo tras factura Authorized con XML autorizado persistido — `GET /api/v1/ride/content`. Funciona en Docker/Linux (QuestPDF Community + SkiaSharp Linux, confirmado).
8. **Correo SMTP pendiente**: la venta/factura electrónica nunca se bloquea por falta de SMTP — el correo simplemente no se envía hasta que se configure SMTP real por empresa vía OrgSettings.
9. **Print Agent pendiente de impresora física**: `SalesIssueModal` ofrece imprimir tirilla vía el agente local; si no hay impresora física conectada, el agente reporta el error de forma aislada sin afectar la venta ya emitida (ver `print-agent/README.md`).

### Pendientes externos (no resolubles en este repo)

SMTP real (Zoho u otro) · impresora térmica física + Print Agent instalado por caja · dominio `.com.ec` + SSL · certificado `.p12` SRI real por empresa piloto · confirmación normativa de la Resolución NAC-DGERCGC26-00000027 (ver ERP-CORE-CLOSEOUT-09) · backups productivos automatizados (hoy manual, ver `docs/deployment/README.md`).

---

## ERP-CORE-CLOSEOUT-09 — Cumplimiento SRI proveedor de sistema (2026-08-21)

**Estado: PARCIAL — infraestructura de configuración dinámica lista; integración XML queda como precondición normativa explícita.** Preparación del ERP para obligaciones de proveedor de sistema de facturación electrónica (Resolución NAC-DGERCGC26-00000027).

**Restricción reconocida al iniciar este cierre**: no es posible verificar de forma confiable, desde el conocimiento de este agente, el contenido técnico exacto de la resolución (qué campo/elemento del XML —si alguno— debe llevar el dato del proveedor de sistema). Inventar esa estructura habría violado la propia instrucción del cierre ("No modificar XML SRI sin confirmar estructura/campo aplicable"). Se confirmó el alcance con el usuario antes de implementar: preparar la infraestructura de configuración dinámica sin tocar XML, dejando la integración documentada como precondición.

- **Sin hardcodes previos**: se auditó el código de runtime (Application/Domain/Infrastructure/API, excluyendo tests y `E2ESeedService.cs` ya gateado como no-producción) buscando RUC/razón social/CIIU/"ZH Technologies" — no se encontró ningún hardcode fuera de tests y de un string descriptivo de Swagger. Este punto ya estaba limpio.
- **Configuración dinámica implementada**: nueva entidad singleton `SystemProviderSettings` (RUC, razón social, CIIU, habilitado, fecha de vigencia) — **a nivel de instancia del ERP, no por tenant/empresa** (decisión confirmada con el usuario: el proveedor de sistema es quien construyó el software, un hecho fijo del despliegue, no algo que cada empresa cliente configura). Deliberadamente separada de `Company`/`SriSettings` (el emisor de cada comprobante) — mismo patrón singleton que `SystemSetupState` (Id=1, sin TenantId/CompanyId). Fail-closed: no puede quedar `Enabled=true` con RUC/razón social/CIIU incompletos (validado en el dominio y en el validador de FluentValidation).
- **API**: `GET`/`PUT /api/v1/system/provider-settings`, controlador nuevo y separado (`SystemProviderSettingsController`) — acceso solo Admin del tenant (`[Authorize(Roles = SecurityRoles.Admin)]`, mismo patrón que `SecurityController`), sin requerir contexto de empresa, para no mezclar con la configuración del emisor. Sin pantalla de frontend nueva (no había una existente que lo requiriera, fuera del alcance de este cierre).
- **PRECONDICIÓN NORMATIVA PENDIENTE (bloqueante para cerrar el punto 3 del alcance)**: el dato del proveedor de sistema **todavía no se inyecta en ningún XML de comprobante electrónico**. Antes de tocar `InvoiceXmlBuilder`/`CreditNoteXmlBuilder` o el `infoTributaria`/`infoAdicional` del XML, se necesita el texto de la Resolución NAC-DGERCGC26-00000027 o la ficha técnica SRI correspondiente que confirme el campo/elemento exacto. Documentado también como comentario en `SystemProviderSettings.cs`.
- **Checklist de facturación electrónica**: deliberadamente NO se agregó un ítem de readiness para "proveedor de sistema" en `CompanyOperationalReadinessResolver` en este cierre — el `Code` de cada ítem requiere una traducción i18n correspondiente en frontend (fuera de alcance: "frontend solo si existe pantalla de configuración necesaria", y no hay pantalla de proveedor de sistema todavía). Queda como seguimiento explícito para cuando se implemente esa pantalla.
- **Precondición legal/administrativa externa (no es un bug técnico)**: si ZH Technologies comercializa este ERP como proveedor de sistema, debe revisar/actualizar su propio RUC ante el SRI con el código CIIU J62021002 (actividad de desarrollo de software) antes de operar bajo esa obligación regulatoria — trámite administrativo externo al código, no una tarea de este repositorio.
- Sin cambios en `SalesPage`/POS, Print Agent, ni XML SRI existente — la emisión electrónica actual no se modificó ni se rompió.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests nuevos de dominio y de los handlers Get/Upsert (10 tests, todos verdes), tests filtrados ElectronicDocument/ElectronicInvoicing/Sri/Configuration/CompanyProfile/Security en Application/Infrastructure/API.Tests (119+90+4, todo verde), guardrails de `ERP.Architecture.Tests` (101/101 verde), migración EF nueva (`AddSystemProviderSettings`) sin cambios de modelo pendientes, y `git diff --check`.

**Resultado real vs. esperado**: la configuración dinámica queda lista y sin hardcodes (cumple items 1, 2, 5 —como precondición documentada—, 6 y 7 del alcance). El item 3 (integración XML) y el item 4 (checklist UI) quedan explícitamente abiertos, no cerrados por decisión deliberada ante la falta de confirmación normativa/de alcance de frontend — no se debe interpretar este cierre como "cumplimiento SRI completo".

---

## ERP-CORE-CLOSEOUT-08 — Reportes mínimos finales (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 8 reportes mínimos (Ventas, Compras, Inventario/stock, Kardex, Caja, Cuentas por Cobrar, Cuentas por Pagar, Monitor de documentos electrónicos). Se encontraron y corrigieron **2 defectos reales**; el resto de los reportes ya tenía aislamiento y cálculos correctos.

- **Totales de Ventas/Compras inflados por Draft/Cancelled corregido**: `GetDailySalesReportQueryHandler` y `GetPurchasesBySupplierReportQueryHandler` sumaban `Totals` sobre **todas** las facturas/compras del rango sin filtrar por estado — una factura Draft (aún no emitida) o Cancelled (anulada), o una compra Draft (aún no confirmada), inflaba el "ingreso"/"gasto" del período. Corregido: `Totals` ahora se calcula solo sobre facturas `Authorized` (Ventas) / compras `Confirmed` (Compras); las filas individuales del reporte siguen mostrando **todos** los documentos del rango con su estado real, para auditoría/trazabilidad — no se ocultó nada, solo se corrigió qué entra en el agregado. 4 tests nuevos.
- **Filtro "Pagadas" de Cuentas por Pagar corregido (bug real, no semántica documentada)**: `PurchasePayableRepository.GetPagedAsync` filtraba `Status == "paid"` literalmente, pero `PurchasePayable.Status` nunca transiciona a `"paid"` (`RegisterPayment` solo acumula `PaidAmount`) — el filtro "Pagadas" del listado de CxP siempre devolvía cero filas, incluso con cuentas completamente saldadas. Corregido con el mismo patrón que ya existía (y funcionaba) en `SalesReceivableRepository.GetPagedAsync` desde `FINANCE-RECEIVABLES-LIST-ENTERPRISE-01`: `"pending"`/`"paid"` se traducen a la condición real de saldo (`BalanceDue`), `"cancelled"` sigue siendo comparación literal. El caso equivalente de Cuentas por Cobrar (saldo cero con `Status` persistido en `"pending"`) ya estaba correctamente resuelto — documentado como semántica intencional (el saldo es la única señal real de "pagada"; `StatusLabel` deriva el estado visible correcto) — y no es un bug. 2 tests nuevos (Postgres real vía Testcontainers, necesario porque el bug era de traducción de la query EF, no verificable con un mock).
- **Confirmado sin defectos** (auditado, no corregido): aislamiento por empresa correcto en los 8 reportes (`ForOperationalScope`/`TenantId`+`CompanyId`, sin excepciones); alcance company-wide (no filtrado por sucursal) es una decisión de negocio ya documentada y consistente en Ventas/Compras/Inventario/Kardex/Caja, no un defecto; fix P0 de `StockRepository` (ERP-CORE-CLOSEOUT-05-FIX01) sigue intacto y correctamente heredado por los reportes de stock/Kardex; fix crítico de RIDE/XML cross-empresa (ERP-CORE-CLOSEOUT-07) sigue intacto y sin reversión; dashboard/estadísticas del monitor de documentos electrónicos correctamente scopeado por empresa, con conteos `Authorized`/`Failed`/reintentables coherentes con los estados reales del documento; fechas comparadas en `DateOnly`/UTC sin ambigüedad de zona horaria; validación de rango de fechas (`DateFrom <= DateTo`) presente en Ventas/Compras. Compras importadas vía recepción XML aparecen en el reporte igual que las manuales (mismo tipo de entidad, sin exclusión especial).
- **Notas no bloqueantes (no corregidas, reportadas)**: `PendingRetries` del dashboard de documentos electrónicos cuenta `RetryCount > 0` (histórico) en vez de estados actualmente reintentables — semánticamente impreciso, no una fuga; `GetPreviousMovementAsync`/`GetNextMovementAsync` en `StockRepository` filtran manualmente por `CompanyId` en vez de usar `ForOperationalScope` — funcionalmente seguro (filtro explícito + filtro global EF de respaldo) pero inconsistente con el resto del archivo; faltan tests directos de company-scoping para `GetForReportAsync`/`GetPreviousMovementAsync`/`GetNextMovementAsync` y para el dashboard/list de documentos electrónicos (cubiertos indirectamente por el filtro global EF, no por un test que lo pruebe explícitamente).
- Sin cambios en `frontend/`, `SalesPage`/POS, Print Agent, ni reglas de negocio cerradas.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Reports/Sales/Purchases/Inventory/Kardex/Cash/Receivables/Payables/ElectronicDocument en Application/Infrastructure/API.Tests (513+87+108, todo verde tras descartar un fallo transitorio de Testcontainers/Docker no relacionado), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-07 — Documentos electrónicos, monitor y reintentos (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 8 flujos de documentos electrónicos (configuración incompleta, emisión/firma/SRI, documento autorizado, documento fallido/rechazado, reintentos, monitor, RIDE/XML, integración con Communications). Se encontró y corrigió **1 fuga crítica cross-empresa**; el resto del pipeline (estados, firma, reintentos, idempotencia, RIDE en Docker/Linux) ya estaba sólido.

- **Fuga crítica corregida (cross-empresa, mismo tenant)**: `GetElectronicDocumentQueryHandler` y `GetElectronicDocumentXmlQueryHandler` resolvían el documento vía `GetBySourceAsync`, que solo filtra por `TenantId` — sin comparar `document.CompanyId` contra la empresa activa. Cualquier usuario autenticado del tenant podía leer el XML comercial completo (borrador/firmado/autorizado: cliente, ítems, totales, RUC) de otra empresa, y esa misma fuga se propagaba al RIDE (`GET /api/v1/ride/content` devolvía el PDF de la factura de otra empresa) porque `ElectronicDocumentRideSourceXmlProvider` consume exactamente esas dos queries. Contradecía además el propio comentario de `RideController` que afirmaba que un documento de otra empresa "nunca es distinguible de 'no aplica'" — en la práctica sí se distinguía, devolviendo datos reales. Ambos handlers ya no existían como huecos aislados: `GetElectronicDocumentDetailQueryHandler`/`Timeline`/`RetryElectronicDocument` ya tenían el chequeo correcto (`document.CompanyId != _currentCompany.CompanyId → NotFound`); se aplicó el mismo patrón exacto a los dos handlers que quedaban sin él. 4 tests nuevos (`GetElectronicDocumentQueryHandlerTests`, `GetElectronicDocumentXmlQueryHandlerTests`).
- **Confirmado sin defectos** (auditado, no corregido): modelo de estados (`Draft/XmlGenerated/Signed/Sent/Received/Authorized/Rejected/DeadLetter/Cancelled/Failed`) con transiciones estrictamente guardadas, sin retroceso posible desde estados avanzados. Pipeline de emisión con try/catch en cada etapa, nunca un 500 sin manejar (dos capas: `ElectronicDocumentIssuer` y `ElectronicSalesInvoiceEmissionStrategy`). Clave de acceso persistida antes del envío a SRI; XML firmado/autorizado escrito antes de la transición de estado que lo reclama. Índices únicos `(TenantId, SourceModule, SourceEntityId)` y `(TenantId, AccessKey)` impiden doble sometimiento a SRI incluso bajo carrera. Ambiente/WSDL SRI 100% dinámico por empresa, sin URLs hardcodeadas. Nota de crédito reutiliza el mismo pipeline con las mismas garantías. Rechazo SRI persiste el mensaje real y queda auditado (`ElectronicDocumentSriMessage`); logs con contexto completo (documentId, clave de acceso, texto real del error SRI). La transacción comercial (venta/kardex/CxC) se commitea **antes** de intentar la emisión electrónica — un fallo SRI nunca revierte la venta. Reintentos: Draft/Failed regeneran el XML pero con clave de acceso determinística (hash de RUC+establecimiento+PE+secuencial+tipo — mismo documento, misma clave siempre); Signed/Received solo reenvían el XML ya firmado sin volver a capturar secuencia; documentos Authorized estructuralmente excluidos de la cola de reintento; concurrencia optimista (`xmin`) más `[DisableConcurrentExecution]` evitan doble procesamiento; reintento manual usa el mismo servicio que el job automático, mismas garantías; error en un documento no detiene el batch de los demás. Monitor (lista/detalle/timeline) ya scopeaba correctamente por empresa. QuestPDF con licencia Community configurada y SkiaSharp con paquete Linux/musl explícito para Alpine — RIDE funciona en Docker. Communications: sin email no falla, sin SMTP no bloquea la venta, índice único de `IdempotencyKey` en BD impide duplicados reales (no solo un check de aplicación).
- Sin cambios en `frontend/`, `SalesPage`/POS, Print Agent, ni reglas de negocio.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados ElectronicDocument/Sales/Ride/Communications en Application/Infrastructure/API.Tests (285+94+63, todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-06 — Configuración inicial obligatoria para empresa piloto (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 11 flujos de configuración inicial (Empresa, Sucursal, Establecimiento, Punto de Emisión, Bodega, Caja, Secuencias, Facturación electrónica, Communications/correo, Usuarios/permisos, smoke de empresa recién configurada). Se encontró y corrigió **1 bloqueante crítico**; el resto de los flujos ya funcionaba end-to-end vía la app sin intervención manual en base de datos.

- **Bloqueante crítico corregido**: `POST /api/v1/setup/admin` (`CreateInitialAdminHandler`) creaba el `CompanyUserMembership` del admin inicial pero **ninguna `CompanyUserBranch`**. Resultado real: el admin podía iniciar sesión, pero `BranchAccessGuard` rechazaba toda operación branch-scoped (venta, compra, caja) con "No tiene autorización para operar en esta sucursal.", y el modal de selección de sucursal del frontend (`BranchSelectorModal`, no descartable) quedaba bloqueado en "No tiene sucursales asignadas. Contacte a un administrador." — sin otro admin a quien contactar, la empresa piloto quedaba atrapada sin salida posible desde la propia app. Corregido: `CreateInitialAdminHandler` ahora ubica la sucursal principal ya creada por el bootstrap (`EnsureDefaultCompanyAsync`/`CompanyBootstrapOrchestrator`) y autoriza al admin en ella dentro de la misma transacción — mismo patrón ya usado por `E2ESeedService` para su admin de pruebas. 5 tests en `CreateInitialAdminHandlerTests` (1 nuevo).
- **Confirmado sin defectos** (auditado, no corregido — no hacía falta): bootstrap automático de Sucursal/Bodega/Establecimiento/Punto de Emisión/Caja/Secuencias documentales/Métodos de pago/Cliente "Consumidor Final"/Lista de precios por defecto al crear la empresa (`CompanyBootstrapOrchestrator`, 7 steps) — un admin no necesita crear nada de eso manualmente. `DocumentSequence.CaptureNextAsync` es find-or-create con advisory lock, sin duplicación posible (índice único `(TenantId, CompanyId, EmissionPointId, DocTypeCode)`). Facturación electrónica: certificado .p12 se sube vía endpoint real (`POST /api/v1/electronic-invoicing/sri-configuration/certificate`), ambiente SRI es dinámico por empresa (nunca hardcodeado), falta de certificado al emitir devuelve `ValidationFailure` claro (nunca 500), factura física nunca toca certificado/ElectronicDocument (garantía estructural — estrategias separadas). Existe un endpoint de "readiness" (`GET /api/companies/operational-readiness`) que le dice al admin exactamente qué falta antes de vender/facturar/usar inventario/caja. SMTP nunca bloquea una venta — el encolado de correo está desacoplado (domain event handler post-commit con try/catch) y el outbox processor aísla fallas por fila sin detener el batch ni otras empresas.
- **Sin hardcodes nuevos**: se buscó "ZH Tech(nologies)"/"Sumak"/RUC de ejemplo fuera de tests/seeders explícitamente gateados — ningún hit en código de runtime real.
- **Nota no bloqueante (no corregida, reportada)**: en el endpoint de readiness, la ausencia de caja/bodega principal es `Warning` para `CanSell`, pero el bloqueo real en runtime (`HasOpenSession`) sigue funcionando correctamente — es solo una posible discrepancia de UX entre "listo para vender" y "puede abrir caja", no un defecto de seguridad ni de datos.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`/POS, ni reglas de negocio cerradas.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Company/Branch/Establishment/EmissionPoint/Warehouse/Cash/DocumentSequence/Electronic/Auth/Setup en Application/Infrastructure/API.Tests (449+156+88, todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX03 — Cierre de gobernanza IgnoreQueryFilters (2026-08-21)

**Estado: COMPLETADO.** Cierra el último pendiente de gobernanza dejado abierto por FIX02: `ConfigurationChangeLogQueryRepository.cs` usaba `.IgnoreQueryFilters()` fuera del allowlist permitido, sin necesitarlo.

- **Causa raíz**: `query.TenantId`/`query.CompanyId` (los únicos filtros aplicados) siempre provienen de `ICurrentTenant`/`ICurrentCompany` — documentado explícitamente en `ConfigurationChangeLogQuery` ("nunca del query string"). Eso es exactamente lo mismo que ya exige el filtro global de EF para `ConfigurationChangeLog` (`ITenantScopedEntity` + `ICompanyScopedEntity`), así que el bypass no tenía ninguna razón real — no era un caso de "necesita cruzar tenant/empresa" como el resto del allowlist (login, bootstrap, seeding).
- **Fix**: se eliminó el bypass del filtro global; se mantiene el `Where` explícito por `TenantId`/`CompanyId` como defensa en profundidad, sin ningún bypass. No se usó `PlatformQueryAccessor` aquí porque no aplicaba — el requisito era "si no necesita ignorar filtros, eliminarlo", no envolverlo.
- **`IgnoreQueryFiltersAuditTests` queda en verde** — no quedan usos de `.IgnoreQueryFilters()` fuera del allowlist documentado en todo `backend/src`. ERP-CORE-CLOSEOUT-05 cierra sin pendientes de gobernanza multi-tenant.
- Sin cambios en Sales, Purchases, Inventory, Cash, Communications, Print Agent ni lógica funcional de auditoría/configuración — solo se retiró un bypass innecesario.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), `dotnet test backend/src/ERP.Infrastructure.Tests --filter IgnoreQueryFiltersAuditTests` (verde), `dotnet test backend/src/ERP.Application.Tests --filter Configuration|Settings|Branch|Tenant` (139 tests verdes), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX02 — P1 de aislamiento y gobernanza (2026-08-21)

**Estado: COMPLETADO.** Se corrigieron los 6 hallazgos P1 de ERP-CORE-CLOSEOUT-05 sin reabrir los P0 de FIX01 ni tocar reglas de negocio de venta/compra/inventario/caja.

- **Compras por id (P1-1)**: `GetPurchaseByIdHandler` ahora valida `inv.BranchId == ICurrentBranch.BranchId` (mismo patrón que `GetSalesInvoiceByIdHandler`, FIX01). `GetPurchaseListQuery` queda sin cambios — su alcance company-wide es una decisión de negocio ya documentada en el propio código (mismo criterio que `GetSalesInvoiceListQuery`), no un defecto.
- **CashMovement (P1-2)**: decisión documentada — se mantiene `IMustHaveTenant` (sin Company/Branch) porque nunca se consulta directamente, solo como hijo de `CashSession` (ya scopeado). Se agregó `CashMovementDirectQueryAuditTests` (guardrail de gobernanza) para que una futura consulta directa no pueda introducirse sin scope explícito.
- **SwitchBranchHandler / UserSession.BranchId (P1-3)**: `UserSession.BranchId` quedaba congelado en la sucursal del login tras un switch, pudiendo ser consultado como fallback por `GetSessionContextHandler` cuando el cliente aún no envía `X-Branch-Id`. Se agregó `UserSession.UpdateBranch()` y `SwitchBranchHandler` ahora actualiza la sesión activa tras un switch exitoso — best-effort, nunca fuente de autorización (eso sigue siendo `ICurrentBranch` + `BranchScopeBehavior` por request).
- **IgnoreQueryFilters en StockAdjustmentRepository (P1-4)**: reemplazado por el wrapper sancionado `PlatformQueryAccessor.AsPlatformQuery()` (ya pre-registrado en el allowlist de `IgnoreQueryFiltersAuditTests`), manteniendo el filtro explícito por TenantId.
- **StockAdjustment sin guard de sucursal (P1-5 — hallazgo real, no duplicado)**: se confirmó que `CreateStockAdjustmentCommandHandler`/`ExecuteStockAdjustmentCommandHandler` **nunca tuvieron** validación de bodega/sucursal en el código commiteado (el reporte de auditoría previo que decía "ya protegido" citaba líneas que no correspondían a código real). Se agregaron los guards (`warehouse.BranchId == ICurrentBranch.BranchId`) con 5 tests nuevos.
- **CommunicationOutboxProcessor (P1-6/gobernanza)**: `IgnoreQueryFiltersAuditTests` fallaba en el código commiteado (previo a este fix, no causado por esta sesión) porque este archivo usaba `.IgnoreQueryFilters()` crudo, fuera del allowlist. Cambio de una línea a `.AsPlatformQuery()`, sin tocar SMTP, outbox ni lógica de envío — confirmado con el usuario antes de tocar Communications.
- **Hallazgo nuevo fuera de alcance, reportado sin corregir**: `ConfigurationChangeLogQueryRepository.cs` (módulo Configuration/Settings) también usa `.IgnoreQueryFilters()` fuera del allowlist — no relacionado a Communications/Sales/Purchases/Inventory/Caja y fuera de la lista de P1 de este cierre. `IgnoreQueryFiltersAuditTests` sigue en rojo por este motivo (no cubierto por los filtros de test exigidos en este fix). Candidato a un FIX03 futuro.
- Tests nuevos: `GetPurchaseByIdHandlerTests`, `StockAdjustmentBranchOwnershipTests` (5 casos), `CashMovementDirectQueryAuditTests`, 2 tests nuevos en `SwitchBranchHandlerTests`.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`, reglas de negocio, ni infraestructura FROZEN.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Purchases/Cash/Inventory/Branch/StockAdjustment en Application/Infrastructure/API.Tests (todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX01 — Corrección de P0 de aislamiento multiempresa/multisucursal (2026-08-21)

**Estado: COMPLETADO.** Se corrigieron los 4 P0 detectados en la auditoría ERP-CORE-CLOSEOUT-05, sin tocar reglas de negocio, `print-agent/` ni Communications.

- **Caja**: `CloseCashSessionHandler` y `RecordCashMovementHandler` ahora validan que la `CashSession` cargada por id pertenezca a la sucursal activa (`ICurrentBranch`) antes de cerrarla o registrar un movimiento — antes solo filtraban por Tenant+Company, permitiendo cerrar/mutar la caja de otra sucursal por GUID.
- **Ventas/Caja (lectura)**: `GetSalesInvoiceByIdHandler` y `GetCashSessionByIdHandler` agregan el mismo chequeo de `BranchId` (mismo patrón ya usado por el endpoint `receipt-print-payload`) antes de devolver el detalle — antes exponían facturas/cajas de otra sucursal de la misma empresa por GUID.
- **Inventario**: `StockRepository.GetStockAsync/GetStockByWarehouseAsync/GetStockByProductAsync/GetMovementsAsync/GetMovementsByProductAsync/GetMovementByIdAsync/GetMovementsByDocumentAsync` ahora scopean explícitamente por `CompanyId` vía `ForOperationalScope` (defensa en profundidad — `CurrentStock`/`StockMovement` ya tenían filtro global EF por `CompanyId`, pero los métodos del repositorio no lo reforzaban explícitamente).
- **Warehouse/CashRegister**: `CreateWarehouseCommandHandler`, `UpdateWarehouseCommandHandler` y `CreateCashRegisterHandler` ahora resuelven la sucursal recibida en el body vía `IBranchRepository` y rechazan el comando si no existe o `branch.CompanyId` no coincide con la empresa activa — antes confiaban en el `BranchId` del cliente sin validar pertenencia a la empresa.
- Tests nuevos: `CashSessionBranchScopeTests`, `CreateCashRegisterBranchOwnershipTests`, `WarehouseBranchOwnershipTests`, `GetSalesInvoiceByIdHandlerTests` (Application.Tests) y `StockRepositoryCompanyScopeIntegrationTests` (Infrastructure.Tests, Postgres real vía Testcontainers, dos empresas del mismo tenant).
- Sin cambios en `frontend/`, `print-agent/`, Communications, ni en la infraestructura FROZEN (Secuencias Documentales, Entity Tracking, Configuración Tributaria).
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), `dotnet test` filtrado por Sales/Cash/Inventory/Warehouse en Application/Infrastructure/API.Tests (todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.
- Nota: durante la auditoría previa, un subagente de investigación introdujo cambios no solicitados fuera de alcance (guard de sucursal en StockAdjustment, refactor de `CommunicationOutboxProcessor`) pese a instrucciones explícitas de solo lectura; se detectaron vía `git status` antes de commitear y se revirtieron. Quedan pendientes como posible FIX02 si el usuario decide retomarlos formalmente.

---

## ZH-PRINT-AGENT-02B — SalesIssueModal integrado con Print Agent local (2026-08-21)

**Estado: COMPLETADO.** Se integró el modal post-facturación de Ventas/POS con el ZH Print Agent local usando el payload oficial del backend, sin imprimir desde backend y sin recalcular datos fiscales en frontend.

- Frontend: `SalesIssueModal` ahora ofrece `Imprimir tirilla` / `Reimprimir tirilla` en el estado de éxito de emisión, manteniendo `Nueva venta` como salida normal para omitir impresión.
- Datos: antes de imprimir consulta `GET /api/v1/sales/invoices/{invoiceId}/receipt-print-payload`; el request al agente se arma solo con esos snapshots oficiales, sin recalcular totales, IVA, pagos ni vuelto.
- Print Agent: cliente local configurable con `VITE_PRINT_AGENT_BASE_URL`, `VITE_PRINT_AGENT_RECEIPT_ENDPOINT`, `VITE_PRINT_AGENT_API_KEY`, `VITE_PRINT_AGENT_PRINTER_NAME` y overrides por `localStorage` (`zh.printAgent.*`). El endpoint real del agente actual es `/print-jobs`.
- Idempotencia: `jobId = invoice-{invoiceId}-receipt`; reenviar el mismo job no duplica una tirilla ya `Printed` según semántica del agente. Si el job queda `Failed`/`NeedsReview`, el reintento usa `POST /print-jobs/{jobId}/retry`.
- UX: mensajes visibles para `Imprimiendo...`, `Tirilla enviada a impresión.`, agente apagado, API key inválida/no configurada, impresora no disponible y error de impresión reintentable.
- Sin cambios en `SalesPage`, reglas de venta, caja, kardex, stock, pagos, SRI, RIDE, Communications ni `print-agent/`.
- Validado con `npx vitest run src/modules/sales/api/printAgentClient.test.ts`, eslint específico de archivos tocados, `npm run build` y guardas de plataforma OK.

---

## ZH-PRINT-AGENT-02A — Backend payload oficial de tirilla POS (2026-08-21)

**Estado: COMPLETADO.** Se agregó un contrato backend oficial, estable y solo lectura para que el POS pueda obtener el payload de tirilla de una factura ya emitida sin acoplar el ERP al Print Agent ni ejecutar impresión física desde backend.

- Application/API: agregado `GetSalesReceiptPrintPayloadQuery` y endpoint `GET /api/v1/sales/invoices/{invoiceId}/receipt-print-payload`, expuesto desde `SalesController` con controller delgado y permiso `Sales.View`.
- Payload: devuelve tenant, empresa, sucursal, RUC, nombre comercial, caja/sesión, número de factura, cliente, estado electrónico/SRI cuando aplica, líneas, totales, pagos y pie de documento resuelto por `ICompanyBrandingResolver`.
- Scope: la consulta falla cerrado con `NotFound` si la factura no existe, no está autorizada/emitida o pertenece a otra sucursal activa del contexto. No crea ni modifica factura, pagos, caja, stock, kardex, RIDE, Communications ni outbox.
- Fallbacks documentados: `cashReceived` y `cashChange` se devuelven `null` porque hoy no están persistidos en `SalesInvoicePayment` ni `CashMovement`; `establishmentCode`/`emissionPointCode` usan configuración actual si existe y fallback histórico desde `InvoiceNumber`/snapshot de `CashSession`.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`, `SalesIssueModal`, reglas de venta, SRI, RIDE ni Communications.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests nuevos de payload, tests Application relevantes de Sales/Caja/ElectronicDocuments, y `dotnet ef migrations has-pending-model-changes` sin cambios pendientes.

---

## ERP-CORE-CLOSEOUT-02B — Correo automático de factura autorizada SRI (2026-08-21)

**Estado: COMPLETADO.** Se conectó la autorización electrónica SRI con el módulo transversal Communications para encolar automáticamente el correo de factura autorizada al cliente, sin acoplar Ventas/SRI/POS a SMTP.

- Integración: agregado `SalesInvoiceAuthorizedCommunicationHandler`, suscrito a `ElectronicDocumentAuthorizedEvent`. Solo actúa cuando el documento electrónico está `Authorized`, es `Invoice` y su origen es `Sales`.
- Communications: agregado propósito canónico `SALES_INVOICE_AUTHORIZED`; `ICommunicationQueue` ahora permite pasar `BranchId` explícito y diferir `SaveChanges` para integrarse correctamente con handlers de domain events.
- Email: si el snapshot del cliente tiene email válido, se encola `CommunicationOutbox` con asunto/cuerpo que incluyen número de factura, clave de acceso, cliente, total y empresa emisora. Si el cliente no tiene email válido, no se encola y la factura no falla.
- Adjuntos: se referencia el XML autorizado (`AuthorizedXmlPath`) y se solicita RIDE por el caso de uso público `GetOrGenerateRideQuery`; si RIDE no está disponible o falla, se registra y el correo se encola con los adjuntos disponibles sin revertir la autorización.
- Idempotencia: la comunicación usa una `IdempotencyKey` determinística por tenant, empresa, factura, propósito y destinatario para evitar duplicados ante reprocesos.
- Sin cambios de UI: no se modificó `SalesPage`, POS ni se agregó botón manual de correo. No hubo migración nueva en 02B; se reutiliza la migración `AddCommunicationsOutbox` de 02A.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests Application relevantes de Communications/ElectronicDocuments/Sales y tests Domain relevantes de Communications/ElectronicDocuments/Sales.

---

## ERP-CORE-CLOSEOUT-02A — Communications transversal reutilizable (2026-08-21)

**Estado: COMPLETADO.** Se implementó la arquitectura base de Communications como módulo transversal desacoplado de Ventas/SRI/POS y reutilizable por otros módulos.

- Domain: agregado `Communications` con `CommunicationOutbox`, `CommunicationOutboxAttachment`, `CommunicationTemplate`, enums de canal/estado/prioridad/tipo de adjunto e interfaces de repositorio. Domain no depende de SMTP, Hangfire, EF ni ASP.NET.
- Application: agregado contrato reutilizable `ICommunicationQueue`, `QueueEmailCommand` CQRS/MediatR con FluentValidation, DTOs de encolado y contratos técnicos `IEmailSender`, `ICommunicationSettingsResolver`, `ICommunicationOutboxProcessor`.
- Infrastructure/API: agregado mapeo EF y repositorios, resolvedor SMTP desde `OrgSettings` (`communications.email.*`) con fallback a `Communications:Email:*`, `SmtpEmailSender`, processor de outbox multi-tenant con `JobExecutionContext`, y job Hangfire `process-communications` cada minuto.
- Persistencia: migración `20260821020826_AddCommunicationsOutbox` crea `communication_outbox`, `communication_outbox_attachments` y `communication_templates`, con índices para pendientes, correlación e idempotencia por `(tenant_id, company_id, idempotency_key)`.
- No se modificó `SalesPage` y no se conectó SRI/POS a SMTP; el disparo post-autorización de factura se implementó después en `ERP-CORE-CLOSEOUT-02B`.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests puntuales de Domain/Application para Communications/configuración, y generación de SQL idempotente de la migración EF.

---

## ERP-CORE-CLOSEOUT-01 — Cierre POS Retail / SalesPage para piloto (2026-08-20)

**Estado: COMPLETADO.** Auditoría de `SalesPage` contra el checklist funcional de cierre para piloto retail/POS — la implementación existente ya cumplía la mayoría de los requisitos; se corrigió el único vacío real encontrado.

- Confirmado ya implementado (sin cambios): búsqueda por SKU/nombre/código de barras (`InvoiceItemSearchRepository`, rankeo barcode exacto → SKU exacto → parcial → nombre), tarjeta de resultado con stock/precio sin IVA/IVA/precio final sin costo, fusión de línea al reescanear/duplicar producto (`findMergeableLineIndex`) con actualización visual de cantidad/subtotal/IVA/total, diseño de línea aprobado (`ZHLineCard` con rail numerado + basurero), bloqueo de emisión sin caja abierta (`canEmit`/`hasCashSession`) y sin cobro completo (`paymentOk`), y modal post-facturación sin envío de correo manual.
- Corregido: el modal de éxito de emisión (`SalesIssueModal`) no mostraba **dinero entregado** ni **vuelto** en ventas con cobro en efectivo — se agregaron ambos campos (visibles solo cuando `cashDue > 0`), reutilizando el estado ya existente en `useSalesPage` (`cashReceived`/`cashChange`), sin nuevos componentes ni cálculos duplicados.
- Sin cambios de backend, sin componentes nuevos del Design System, sin estilos inline.
- Validado con `npx eslint` (sin errores nuevos), `npx tsc --noEmit` (sin errores), `npm run build` y `npx vitest run` sobre los tests existentes de `SalesPage` (bottombar/emitButton/paymentMethod, 20/20 passed).

---

## FLOW-READY-02F.11-FIX01 — Compras: proveedor inactivo y reactivación visible (2026-08-13)

**Estado: COMPLETADO.** Corrección acotada del bloqueo de compras con proveedor inactivo y de la visibilidad operativa en Administración de proveedores.

- Compras muestra el mensaje específico de `data.errors` antes que el genérico de `VALIDATION_ERROR`, con resumen visible de errores en `ZHPageNotice`.
- Backend mantiene el bloqueo fail-closed de proveedor inactivo y ahora devuelve el nombre legal del proveedor en el mensaje específico cuando está disponible.
- Administración de proveedores expone filtro explícito `Todos / Activos / Inactivos`, muestra el estado real `BusinessPartner.IsActive` y reutiliza `PATCH /activate` / `DELETE` soft-disable existentes.
- No se tocaron PricingResolver, Kardex, IStockRepository, posting/accounting, PurchaseCreditNote, PurchaseReturn ni SupplierCredit.
- Validado con `npx tsc --noEmit`, `npm run lint`, `npm run build`, `npx vitest run src/modules/purchases src/modules/masterData src/modules/items`, `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter BusinessPartner`, `dotnet test backend/src/ERP.slnx --filter Items` y `git diff --check`.

---

## FLOW-READY-02F.10-CLEAN01 — Items Admin SSOT cleanup (2026-08-12)

**Estado: COMPLETADO.** Auditoría y limpieza acotada del Admin de Ítems sin cambios de backend.

- Códigos de barras y códigos proveedor quedan gestionados solo en Principal; se eliminó la sección duplicada no consumida de códigos proveedor en detalle.
- Presentaciones/empaques quedan en Inventario y presentaciones; se conserva `ItemPackagingLevel` como SSOT y no se infieren factores desde nombres.
- Precio/costo/rentabilidad queda en una sola sección de Principal; se eliminó el componente antiguo de simulación “Nuevo PVP / Simular” y su cliente frontend.
- Textos visibles tocados en catálogo/listado/árbol se pasaron a i18n `es/en/qu`; se removió CSS huérfano del simulador viejo y se mantuvo cero `style=`.
- Validado con `npx vitest run src/modules/items`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `git diff --check`.

---

## FLOW-READY-02F.7 — Controles preventivos empaques / XML (2026-08-11)

**Estado: COMPLETADO.** Controles fail-closed para evitar configuraciones peligrosas de presentación, código proveedor y compra XML.

- Ítems inventariables requieren exactamente una presentación base con `BaseQuantity = 1`; servicios/no inventariables no quedan bloqueados por ausencia de base.
- Empaques validan cantidad positiva, no duplican `UOM + BaseQuantity`, no permiten base con factor distinto de 1 y advierten nombres tipo `PACA x12` con factor 1 sin inferir automáticamente.
- Códigos proveedor muestran estado “sin presentación” y permiten guardar la presentación asociada.
- Confirmación de compra XML muestra checklist de ítems, presentaciones, líneas sin presentación, impuestos y diferencia total; backend bloquea líneas XML inventariables sin presentación.
- Empaques usados por códigos proveedor o documentos confirmados no pueden eliminarse ni cambiar su factor; se debe crear una nueva presentación.
- Alerta de costo base extremo contra último costo/promedio sugiere revisar presentación/factor.
- Validado con `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter Items`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter PurchaseReception`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `npx vitest run src/modules/items src/modules/purchases`.

---

## Purchases — Recepción XML empaques FIX03 (2026-08-11)

**Estado: COMPLETADO.** Corrección de rehidratación de presentación al abrir una compra desde Recepción Electrónica con `fromReceptionId`.

- `CreatePurchaseReceptionDraftHandler` re-resuelve cada línea vinculada usando `SupplierId + SupplierCode` contra `ItemSupplierCode` y toma `PackagingLevelId`, UOM y factor desde `ItemPackagingLevel`.
- El DTO de draft de recepción expone `packagingLevelId`, `uomCode`, `baseUomCode`, `conversionFactor` y `quantityInBaseUom`, evitando que el frontend vuelva a factor 1.
- `/purchases?fromReceptionId=...` hidrata el formulario con la instantánea de presentación y el VM muestra `Ítem + PACA x12` aun si el contexto de bodega todavía no cargó.
- Guardar presentación del proveedor actualiza la línea local con UOM, factor y cantidad base sin requerir recarga manual.
- Validado con `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter PurchaseReception`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter Items`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `npx vitest run src/modules/purchases src/modules/items`.

---

## Items — Empaques FIX02 (2026-08-11)

**Estado: COMPLETADO.** Corrección del flujo de edición de niveles de empaque en maestro de ítems.

- El guardado de empaques muestra errores reales de validación backend y conserva la fila en edición si falla.
- La UI impide guardar conjuntos sin exactamente una presentación base y facilita crear `UNIDAD X1`.
- `replacePackagingLevels` preserva IDs existentes al editar, evitando romper asociaciones de códigos de proveedor.
- El selector de presentación de códigos proveedor usa los empaques refrescados y muestra el factor contra la unidad base del ítem.
- Validado con `npx tsc --noEmit`, `npm run lint`, `npm run build`, `npx vitest run src/modules/items`, `dotnet build backend/src/ERP.slnx` y `dotnet test backend/src/ERP.slnx --filter Items`.

---

## Design System Form Controls SSOT — fase cerrada (2026-08-07)

**Estado: CERRADO CON DEUDA DOCUMENTADA.** Migración de controles HTML crudos (`<input>`/`<select>`/`<textarea>`) hacia los componentes ZH oficiales (`ZhTextInput`, `ZhNumberInput`, `ZhDecimalInput`, `ZhDateInput`, `ZhPhoneInput`, `ZhSelect`, `ZhTextarea`), ejecutada en bloques 14B-4 a 14B-12.

**Resultado:**
- Controles HTML crudos reducidos de 314 a 149 (`frontend/src/modules/**/*.tsx`).
- 165 controles migrados a componentes ZH oficiales, sin cambios en schemas, handlers, payloads ni servicios.
- No quedan clusters grandes de Categoría A (pendiente real simple); el mayor residuo es de 3 controles en un mismo archivo.
- 12 controles A dispersos en 7 archivos quedan documentados como deuda menor (candidatos a cierre puntual futuro, cada uno ≤3 controles/mismo archivo).
- Residuos restantes (137 controles) están justificados por tipo HTML (email/password/checkbox/radio/file/color) o por dominio especializado: SRI crítico, IAM/permisos, pickers con teclado, tablas editables, stock/logística crítica, ItemTypes FROZEN, min/max nativo crítico.

**Validado:** `npx tsc -b`, `npm run build`, `git diff --check` en verde en cada bloque de migración.

---

## Piloto operativo Sumak — uso supervisado (2026-08-03)

**Estado: READY_FOR_PILOT / uso supervisado.** No implica producción estable ni cierre de módulo — es habilitación para operar con supervisión directa mientras se completan las limitaciones aceptadas abajo.

`SUMAK_E2E_01_STATUS: PASSED`. Commits relacionados: `da1a2381` (reporte de stock actual por bodega), `cef699d6` (reporte de compras por proveedor), `c49da503` (reportes mínimos en el menú).

**Capacidades validadas (E2E manual):**
- Compra manual y creación de Item desde línea de compra
- IVA compra/venta + precio de venta resuelto correctamente
- Confirmación de compra
- Stock actual y Kardex
- Venta POS con cobro en efectivo y cálculo de vuelto
- Factura electrónica autorizada
- Caja actualizada tras la venta
- Reportes de Ventas, Compras y Stock funcionando
- Devolución de compra bloqueada correctamente por stock insuficiente
- 0 errores HTTP 5xx y 0 errores de consola durante la prueba E2E

**Limitaciones aceptadas (no bloquean el piloto, sí producción):**
- SRI producción no validado (solo ambiente de pruebas)
- Recepción física sin factura previa: pendiente
- Reportes sin exportación a Excel/PDF
- Reportes de ventas/compras alcance company-scoped (no consolidado multi-sucursal)
- Caja consolidada diaria: pendiente
- CxP/CxC avanzado: pendiente
- Limpieza global de lint/architecture/e2e: fuera de este cierre

---

## Backlog futuro UX

### MEJORA_FUTURA_UX_01 — Command Palette / Buscador rápido de navegación

- **Estado:** BACKLOG / FUTURE
- **Prioridad:** P2
- **Tipo:** UX / Navegación / Productividad
- **Dependencia:** App Drawer estabilizado y `navigation.config.ts` como SSOT.
- **Objetivo:** Permitir buscar y abrir formularios con `Ctrl+K` / `Cmd+K` usando la misma fuente de verdad del menú.
- **Fuera de alcance actual:** No implementar código, no tocar backend, no cambiar rutas, no cambiar permisos ni modificar el App Drawer.
- **Motivo:** Mejora no bloqueante para usuarios avanzados cuando existan más pantallas.

---

## Estado actual (2026-06-24)

**Completado**
- Arquitectura base terminada (Clean Architecture + CQRS)
- Autenticación JWT + Refresh Token
- Multi-tenant por `tenant_id` + `company_id`
- Cambio de empresa (multi-company)
- Dashboard unificado
- ERP Core congelado
- Items Module FROZEN v1.0 (2026-06-17)
- **Items Module — Rediseño flujo de creación: FROZEN v2.0 (2026-07-02)** — reemplaza v1.0: código de barras obligatorio (mínimo 1, exactamente 1 principal), códigos de proveedor opcionales (`ItemSupplierCode`, 0..N, FK a `BusinessPartner`), categoría y marca obligatorias en creación, eliminación de flags booleanos de impuesto (`AppliesVatOnSale/Purchase/ExciseTax` — el código tributario es la única fuente de verdad, alineado con la Infraestructura Tributaria CLOSED), precio inicial creado atómicamente junto con el ítem (`ItemPrice` contra lista DEFAULT/PVP)
- **Items Module — Auditoría por fases, Fase 1 (Información Base del Item): COMPLETADA (2026-07-02)** — SKU editable y único por tenant (índice BD), marca/categoría con FK real e integridad activa validada, breadcrumb de categoría, profundidad máxima del árbol de categorías configurable por empresa (`OrgSettings`, default 3). Detalle completo: [`docs/items/PHASE1-ITEM-IDENTITY.md`](items/PHASE1-ITEM-IDENTITY.md)
- **Items Module — Auditoría por fases, Fase 2 (Identificación del Item): COMPLETADA (2026-07-02)** — código de barras único globalmente por tenant (antes solo por ítem), código de proveedor único por `(tenant_id, supplier_id, code)`, proveedor obligatorio por cada entrada de código de proveedor. Detalle completo: [`docs/items/PHASE2-ITEM-IDENTIFICATION.md`](items/PHASE2-ITEM-IDENTIFICATION.md)
- **Items Module — Auditoría por fases, Fase 3 (Tributación del Item): COMPLETADA (2026-07-02)** — códigos SRI (`SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode`) confirmados como única fuente de verdad, sin cambios; campos de cuenta contable (`VatAccountId`/`PurchaseVatAccountId`/`ExciseAccountId`) retirados del contrato público del módulo Items por no tener módulo de Contabilidad que los respalde (quedan reservados internamente); `SriServiceCode` retirado del formulario por no tener catálogo SRI de respaldo. Sin impacto en Ventas/Compras/Facturación (siguen resolviendo impuestos vía `ISriTaxResolver`, Infraestructura Tributaria CLOSED intacta). Detalle completo: [`docs/items/PHASE3-ITEM-TAXATION.md`](items/PHASE3-ITEM-TAXATION.md)
- **Items Module — Auditoría por fases, Fase 4 (Comercial del Item): COMPLETADA (2026-07-02)** — confirmado: precio inicial siempre a la lista de precios predeterminada, sin selector en el formulario; corregido símbolo de moneda hardcodeado (`$`) en `PricingTab.tsx`, ahora refleja `PriceList.CurrencyCode` real. Sin cambios de backend. Detalle completo: [`docs/items/PHASE4-ITEM-COMMERCIAL.md`](items/PHASE4-ITEM-COMMERCIAL.md)
- **Items Module — Auditoría por fases, Fase 5 (Inventario y Venta del Item): COMPLETADA (2026-07-02)** — confirmado: la configuración de Inventario/Venta (`TracksStock`, lotes, series, decimales, disponibilidad POS/Web/Mobile) es intencionalmente independiente del `ItemType`, sin restricciones ni defaults condicionados por tipo. Sin cambios de código. Detalle completo: [`docs/items/PHASE5-ITEM-INVENTORY-SALE.md`](items/PHASE5-ITEM-INVENTORY-SALE.md)
- **Items Module — Auditoría por fases, Fase 6 (Variantes del Item): COMPLETADA (2026-07-02)** — SKU de variante único globalmente por tenant (antes solo por ítem), consistente con SKU de ítem (Fase 1) y barcode/código de proveedor (Fase 2). Detalle completo: [`docs/items/PHASE6-ITEM-VARIANTS.md`](items/PHASE6-ITEM-VARIANTS.md)
- **Items Module — Auditoría por fases, Fase 7 (Pricing del Item): COMPLETADA (2026-07-02)** — corregida violación de la regla "no eliminar registros": `RemoveItemPriceCommand` ahora deshabilita el precio en vez de hacer `DELETE` físico; historial de cambios de precio registrado en `UserActivity` (auditoría existente, append-only), no en tabla propia. Detalle completo: [`docs/items/PHASE7-ITEM-PRICING.md`](items/PHASE7-ITEM-PRICING.md)
- **Motor de Pricing v2 — Dominio Items+Pricing: CLOSED (2026-07-05)** — reemplaza el modelo de Fase 7: `Item.BaseSalePrice` es el SSOT del precio base; `ItemPrice` fue eliminado y reemplazado por `PricingRule` (regla de ajuste, no precio absoluto, sin quiebres de cantidad — eso pertenece al futuro módulo Promotions); `PriceList` gana una regla general opcional; `IPricingResolver` centraliza la resolución de precio (antes duplicada en 4 lugares) como única API pública que el resto del ERP debe consumir. Reabre parcialmente el freeze de Items v1.0 y de Fase 7 solo en lo referente a precios — ambos quedan reemplazados por este ADR en ese punto. Integración con Ventas/Compras/POS/Facturación (consumo real de `IPricingResolver`) queda pendiente como trabajo de esos módulos, sin reabrir este dominio. Detalle completo: [`docs/decisions/ADR-021-pricing-engine-ssot.md`](adr/ADR-021-pricing-engine-ssot.md)
- **Items Module — Auditoría por fases, Fase 8 (Compras): COMPLETADA (2026-07-02)** — Compras migrado para resolver el código de proveedor vía `ItemSupplierCode` (Fase 2) según el proveedor real de la factura, con fallback al campo legacy `Item.Code.PurchaseCode`; corregido defecto preexistente que impedía cargar `Item.SupplierCodes` en cualquier lectura del agregado (`.Include()` faltante). Detalle completo: [`docs/items/PHASE8-ITEM-PURCHASES.md`](items/PHASE8-ITEM-PURCHASES.md)
- **Items Module — Auditoría por fases, Fase 9 (Arquitectura — revisión transversal): COMPLETADA (2026-07-02)** — revisión de duplicación/acoplamientos/cumplimiento de infraestructuras FROZEN en las Fases 1-8; único hallazgo (duplicación menor de resolución de código de proveedor introducida en Fase 8) corregido con un helper compartido en `PurchaseDraftUseCases.cs`. **Cierra la auditoría completa del módulo Items (Fases 1-9).** Detalle completo: [`docs/items/PHASE9-ARCHITECTURE.md`](items/PHASE9-ARCHITECTURE.md)
- Customer Module FROZEN (2026-06-17)
- Compras: auditoría UX + SSOT completada (2026-06-24)
- Sales Invoice + Detail: módulo cerrado (2026-06-24)
- Payment Methods + Formas de Cobro Multi-Pago: CERRADO (2026-06-24)
- Sales Receivable (CxC deuda, sin cobros): CERRADO (2026-06-25)
- Estándar de Precisión Numérica: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Estándar de Fechas y Horas: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Infraestructura de Mensajes Visuales: CLOSED (2026-06-29) — ADR-018
- Infraestructura de Secuencias Documentales: CLOSED (2026-06-29) — ADR-019
- **Infraestructura de Entity Tracking (EF Core Change Tracking): CLOSED (2026-06-30) — ADR-020**
- **Infraestructura Tributaria (Tax Infrastructure): CLOSED (2026-07-01)**
- **Infraestructura de Valores por Defecto de Facturación: CLOSED (2026-07-01) — migrado a org_settings (Phase 8, 2026-07-01)**
- **Infraestructura Org Config Jerárquica (OrgSetting / 5 scopes): CLOSED (2026-07-01)** — `org_settings`, `IOrgSettingsRepository`, `OrgSettingKeys`; 10 endpoints GET/PUT por scope; UI en Company Settings Hub
- **Infraestructura Master Configuration UI: CLOSED (2026-07-02)** — Patrón oficial de tabs para módulos de configuración; `ConfigTabsLayout` + `items-catalog.css`; implementado en Branches, Establishments, Emission Points, Warehouses; prohibido crear variantes sin decisión arquitectónica global
- **Infraestructura de Auditoría por Dominio (Entity Audit): CLOSED (2026-07-07) — ADR-022** — contratos comunes (`AuditRecordBase`/`IAuditWriter`/`IAuditReader`/`IAuditService`/`IAuditContext`) reutilizables por todo dominio futuro; pilotos `PricingRuleAudit`/`PriceListItemAudit`; Process Audit (procesos sin `EntityId` único) queda diseñado en `docs/architecture/audit-infrastructure.md`, sin implementar
- **Contexto Operativo del Usuario (UserSession): implementado y estabilizado (2026-07-17)** — registro de sesión operativa (empresa/sucursal/terminal) integrado a Login/SwitchCompany, expiración automática vía Hangfire, dashboard administrativo en `/admin/access/sessions` (`AdminUserSessionController`, única API pública del dominio). Detalle: [`docs/IDENTITY.md#usersession-contexto-operativo-del-usuario`](IDENTITY.md#usersession-contexto-operativo-del-usuario). Hardening Fase 12: eliminado `UserSessionController` self-service (IDOR + cero consumidores reales) en vez de endurecerlo
- **CompanyUserPreferences (preferencias de login: sucursal por defecto + modo de ingreso): ciclo cerrado (2026-07-17)** — única fuente de verdad de `DefaultBranchId`/`LoginMode`; escritura vía `UpsertCompanyUserMembershipHandler` (alta/edición de membresía) y `PUT /api/v1/admin/iam/company-users/{companyUserId}/preferences`; lectura centralizada en `CompanyUserPreferencesLoginResolver` (Login/SwitchCompany) y `GET` del mismo endpoint; `CompanyUserBranch` sigue siendo la única fuente de sucursales autorizadas (nunca se le agregó comportamiento). Auditoría de cierre (Fase H) corrigió que una sucursal desactivada podía aceptarse como `DefaultBranchId`. UI en `SecuritySettingsPage` (`/admin/security`), sin CRUD propio. Sin cambios de JWT en todo el ciclo. Detalle: [`docs/IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login`](IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login)
- **Access/IAM — Fase I-A (wiring administrativo de CompanyUserMembership): backend completado (2026-07-17)** — expone `POST /api/v1/admin/iam/memberships` (alta/edición de rol, perfil y sucursales autorizadas) y `POST /api/v1/admin/iam/memberships/revoke` (`CompanyUserMembershipsController`), que hasta esta fase no existían pese a que `UpsertCompanyUserMembershipHandler`/`RevokeCompanyUserMembershipHandler` (Fase D) estaban implementados y probados sin ningún consumidor de producción. TenantId/CompanyId nunca viajan en el request — cada Admin command (`UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand`) los resuelve del contexto autenticado (`ICurrentTenant`/`ICurrentCompany`) y delega íntegramente vía MediatR en los handlers de Fase D, sin reimplementar su lógica. `CompanyUserMembership` sigue siendo la única fuente de verdad de la relación usuario-empresa, `Role`, `ProfileId` e `IsActive` de membresía; `CompanyUserBranch` sigue siendo la única fuente de autorización de sucursal; `CompanyUserPreferences` no se modificó. Reutiliza el permiso `access.company_user_memberships.view` (mismo criterio que `AccessProfilesController`/`CompanyUserPreferencesController`) — no se introdujo un permiso `.manage` nuevo en esta fase. Sin frontend, sin invitaciones, sin cambios a `IdentityUser` ni a su `IsActive` global.
- **Access/IAM — Fase I-B (administración de CompanyUserBranch): backend completado (2026-07-17)** — expone `GET`/`PUT /api/v1/admin/iam/memberships/{membershipId}/branches` (`CompanyUserBranchesController`). `GetCompanyUserBranchesAdminQuery` proyecta las sucursales activas de la empresa de la membresía marcando cuáles están autorizadas (`{branchId, branchName, authorized}`), pensado para que un futuro selector de frontend lo consuma directamente. `UpdateCompanyUserBranchesAdminCommand` reemplaza la autorización completa todo-o-nada (ninguna escritura ocurre si cualquier `BranchId` es inválido): reactiva/crea las solicitadas, desactiva el resto — `CompanyUserBranch` sigue siendo la única fuente de verdad de sucursales autorizadas, nunca se copia a `Membership`/`Preferences`/`IdentityUser`. Hallazgo de auditoría: `IBranchRepository.GetAsync`/`GetByIdAsync` solo filtran por `TenantId` (no por `CompanyId`, a diferencia de entidades con `ForOperationalScope`) — ambos handlers filtran/comparan `Branch.CompanyId` manualmente contra la empresa de la membresía antes de aceptar cualquier sucursal, y usan el mismo mensaje para "no existe" y "pertenece a otra empresa" (mismo criterio anti-enumeración que `GetCompanyUserPreferencesAdminHandler`). Decisión documentada: lista vacía es un valor válido (revoca todas las sucursales sin desactivar la membresía) — es seguro porque `CompanyUserPreferencesLoginResolver` (Fase E) ya revalida `DefaultBranchId` en cada login y falla con `ValidationFailure` controlado si dejó de estar autorizado, nunca asumió que hubiera siempre al menos una sucursal activa. Reutiliza `access.company_user_memberships.view` — sin permiso nuevo. Sin frontend, sin cambios a `CompanyUserPreferences`/`IdentityUser`/JWT.
- **Access/IAM — Fase I-C (pantalla administrativa de usuarios empresariales): completado (2026-07-17)** — reemplaza el placeholder `/admin/users` (antes un `<Navigate>` a `/admin/roles`) por `UsersPage` (`frontend/src/modules/access/users/`), que administra `CompanyUserMembership` end-to-end: tabla principal (Usuario/Email/Perfil/Role/Estado/Sucursales autorizadas/Modo de ingreso/Acciones), modal de alta/edición de membership (`membershipService.upsertMembership`, nunca crea `IdentityUser`), modal de sucursales autorizadas (`branchAssignmentService`, Fase I-B — el frontend nunca valida pertenencia/activa/autorización previa, solo envía los `BranchId` marcados), modal de preferencias de login (reutiliza 100% el schema/servicio de Fase G, sin extraer un componente compartido con `SecuritySettingsPage` para no tocar ese ciclo ya cerrado) y revocación con confirmación vía `message.confirm` (`lib/messages`, API pública oficial). Bloqueo real detectado y resuelto: no existía ningún endpoint que listara `CompanyUserMembership` con inactivas + `ProfileName` (`GET /api/v1/security/admin-matrix`, Fase B, solo devuelve `IdentityUser` activos sin perfil) — se agregó `GET /api/v1/admin/iam/memberships` (`GetCompanyUserMembershipsAdminQuery`, solo lectura, junta `CompanyUserMembership`+`IdentityUser`+`AccessProfile`, todos ya expuestos individualmente) reutilizando `access.company_user_memberships.view`, sin permiso nuevo. Limitación conocida y documentada en código: "Sucursales autorizadas"/"Modo de ingreso" por fila se resuelven con `Promise.allSettled` por membership (sin endpoint de resumen agregado) — aceptable al volumen típico de usuarios por empresa, candidato a un endpoint agregado en una fase futura si escala. Sin invitaciones, sin cambios a `IdentityUser`/JWT/`CompanyUserPreferences`.
- **Access/IAM — Fase S1 (Security Hardening): completado (2026-07-17)** — corrige los 3 hallazgos críticos/altos de la auditoría de cierre de Access/IAM, sin agregar funcionalidad ni tocar JWT/frontend/otros módulos:
  - **5A** — `POST /api/v1/auth/register` **eliminado**. Permitía crear un usuario (con `Role` arbitrario, incl. `Admin`) en cualquier tenant existente indicando `TenantId` en el body, sin ningún control de identidad. El alta del primer usuario/tenant ya tenía un flujo seguro y dedicado (`SetupController` → `CreateInitialAdminCommand`, token de instalación de un solo uso generado por consola, nunca acepta `TenantId`/`Role` del cliente) — confirmado sin consumidor alguno en frontend antes de eliminar. `RegisterCommand`/`RegisterHandler`/`RegisterCommandValidator`/`RegisterDto` eliminados.
  - **5B** — `POST /api/v1/auth/password-reset` **eliminado**. Cambiaba la contraseña de cualquier usuario solo con `TenantId`+`Email`, sin contraseña actual, token ni OTP. El flujo oficial (`ForgotPassword` + `ResetPasswordWithToken`, token de un solo uso por email) queda como único camino. `DirectPasswordResetCommand`/`Handler`/`Validator` eliminados. Su único consumidor frontend (`PasswordResetPage.tsx`, página pública en `/password-reset`) se eliminó en el cierre final del módulo (ver entrada siguiente) — no quedan referencias vivas al flujo eliminado.
  - **5C** — `GetCompanyUserMembershipsAdminQuery`, `GetCompanyUserPreferencesAdminQuery`, `UpdateCompanyUserPreferencesAdminCommand`, `GetCompanyUserBranchesAdminQuery`, `UpdateCompanyUserBranchesAdminCommand` ahora implementan `IRequiresCompanyContext` — mismo marker que `UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand` (Fase I-A), sin inventar un mecanismo nuevo. Antes, su única defensa era comparar manualmente contra `ICurrentCompany.CompanyId` (header `X-Company-Id`, no un claim firmado), sin pasar por `ICompanyAccessGuard` — un caller con rol Admin de su propio tenant podía leer/escribir memberships, sucursales y preferencias de una empresa ajena manipulando el header, porque el bypass de rol Admin (`RuntimePermissionAuthorizer`) nunca revalidaba tenant/membership real. El marker fuerza `CompanyScopeBehavior` → `ICompanyAccessGuard.RequireCurrentCompanyAsync` antes del handler; el chequeo manual original se mantiene como defensa adicional.
  - Tests nuevos: `ERP.Architecture.Tests/AuthAttackSurfaceGuardTests.cs` (CI-bloqueante, impide reintroducir 5A/5B), `ERP.API.Tests/Auth/AuthControllerTests.cs`, `ERP.Application.Tests/Setup/CreateInitialAdminHandlerTests.cs` (prueba que el flujo alternativo seguro sigue funcionando), `ERP.Application.Tests/Behaviors/CompanyScopeBehaviorTests.cs` + `ERP.Application.Tests/Access/CompanyScopeMarkerConsistencyTests.cs` (prueban el mecanismo de 5C y que los 5 handlers corregidos usan el mismo patrón que Fase I-A).
  - **Módulo Access/IAM: apto para producción** en lo referente a estos 3 hallazgos. Deuda no crítica restante documentada en la auditoría de cierre (naming, duplicación de UI en modal de preferencias, etc.) — ver entrada de cierre final más abajo para lo que sí se resolvió en la limpieza posterior.
- **Access/IAM — Cierre final del módulo (limpieza de deuda técnica menor): completado (2026-07-17)** — módulo declarado terminado y cerrado a mantenimiento únicamente. Sin funcionalidad nueva, sin endpoints nuevos, sin cambios de comportamiento ni de contrato HTTP/BD. Alcance:
  - **Código muerto eliminado**: `PasswordResetPage.tsx`/`.css` y `passwordResetSchema.ts` (frontend, único consumidor de `POST /auth/password-reset`, eliminado en Fase S1 — la página había quedado sin backend detrás); ruta `/password-reset` retirada de `publicRoutes.tsx`; entradas `/api/v1/auth/register` y `/api/v1/auth/password-reset` retiradas de `PUBLIC_AUTH_PATHS` (`authRefreshPolicy.ts`, rutas ya inexistentes); 7 claves i18n huérfanas (`reset.title`, `reset.subtitle`, `reset.directSubtitle`, `reset.error.disabled`, `reset.error.mismatch`, `reset.subscriberCheck.enabled/unavailable`) retiradas de `es/en/qu.json`; `RegisterDto` (backend, ya sin uso desde antes de Fase S1) eliminado.
  - **Naming corregido (solo archivos, sin tocar clases/namespaces/contratos)**: `Entities/Membership.cs` → `CompanyUserMembership.cs` (la clase ya se llamaba así); carpetas `UseCases/UpsertMembership`/`RevokeMembership` → `UpsertCompanyUserMembership`/`RevokeCompanyUserMembership` (ya coincidían con el namespace, no con el nombre de carpeta); los 6 archivos `Upsert/RevokeMembership{Command,CommandValidator,Handler}.cs` dentro renombrados a `Upsert/RevokeCompanyUserMembership{Command,CommandValidator,Handler}.cs` (las clases ya tenían el nombre completo).
  - **No se encontró** ningún Command/Query/DTO/validator/servicio registrado sin consumidor en Access/IAM más allá de lo ya listado — confirmado por auditoría previa y revalidado en esta fase.
- **ADR-026 (Accounting Core Architecture): ACCEPTED (2026-07-24)** — diseño arquitectónico aprobado por Architecture Review Board (`docs/decisions/ADR-026-accounting-core.md`): bounded context (`Account`/`AccountingPeriod`/`JournalEntry`/`PostingRule`), `CompanyId`-scoped obligatorio en los 4 aggregates, integración exclusivamente vía Domain Events (sin dependencias directas hacia Sales/Purchases), `JournalEntrySequence` independiente de `IDocumentSequenceRepository` (ADR-019), alcance v1 limitado a Sales/Purchases/Caja/Inventory.
  - **Fase 0 (housekeeping, 2026-07-24)**: eliminado `ERP.Application/Common/Interfaces/IAccountingService.cs` (dead code confirmado — cero implementaciones, cero consumidores).
  - **Fase 1 — Fundamentos de dominio (2026-07-24)**: `Account`/`AccountingPeriod`/`PostingRule` con comportamiento completo (`Create`, `Rename`, `Activate`/`Disable`/`Enable`, `Close`, `Lock`, `UpdateMapping`); `JournalEntry` como esqueleto de identidad únicamente (sin líneas, sin `Post()`/`Reverse()` — explícitamente fuera de esta fase). VO `AccountCode`, enums `AccountType`/`AccountNature`/`PeriodStatus`/`JournalEntryStatus`. 7 domain events (`AccountCreatedEvent`/`AccountActivatedEvent`/`AccountDisabledEvent`/`AccountingPeriodCreatedEvent`/`AccountingPeriodClosedEvent`/`AccountingPeriodLockedEvent`/`PostingRuleCreatedEvent`).
  - **Fase 1.2/1.3/1.4 — Persistencia (2026-07-25)**: 4 configuraciones EF Core, 4 tablas (`accounts`, `accounting_periods`, `journal_entries`, `posting_rules`), 3 índices únicos (`uq_accounts_company_code`, `uq_accounting_periods_company_year_period`, `uq_posting_rules_company_source_fact`) + 1 FK (`journal_entries.accounting_period_id → accounting_periods.id`, `RESTRICT`). Migración `20260725000917_AddAccountingCoreFoundations` **aplicada** en desarrollo, auditada por Database Migration Review Board — `ACCEPTED`.
  - **Fase 2.0/2.1/2.2 — Application + API (2026-07-25)**: 4 repositorios (`IAccountRepository`/`IAccountingPeriodRepository`/`IJournalEntryRepository`/`IPostingRuleRepository`) con filtrado `TenantId`+`CompanyId` en toda consulta; 11 Commands + 6 Queries + 11 Validators FluentValidation + 17 Handlers (patrón CQRS/MediatR, sin ningún `AccountingService`/`AccountService`/`PostingRuleService`); concurrencia con patrón pre-check → `SaveChanges` → `IDatabaseExceptionTranslator` en los 3 Commands de creación; permisos `accounting.view/create/update/delete`; `AccountingController` (`api/v1/accounting`) con 14 endpoints REST (6 GET, 3 POST, 5 PATCH, sin `DELETE` — baja lógica vía `PATCH .../disable`). Auditado por Architecture Review Board (Auditoría Final de Implementación) — `APPROVED WITH MINOR CHANGES` (hallazgo de documentación ya resuelto con esta entrada; longitudes de validación duplicadas entre `Validator`/EF `Configuration` sin constante compartida queda como deuda menor no bloqueante).
  - **Explícitamente NO implementado hasta Fase 2.2**: Posting Engine (ADR-026 §8), `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence` (ADR-026 §7), integración vía eventos con Sales/Purchases/Caja/Inventory, reportes financieros. `JournalEntry` no tenía ningún endpoint ni caso de uso — solo existía como tabla y aggregate de identidad.
  - **Fase 3.1 — Posting Engine inicial (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/` — `IPostingEngine.PostAsync(PostingFact, ct)` como único contrato público (`PostingFact`: `TenantId`/`CompanyId`/`SourceModule`/`FactType`/`SourceEventId`/`EntryDate`, sin Currency/Amount/Lines/impuestos — fuera de esta fase). Pipeline interno fijo (Idempotency → PostingRuleResolver → PostingPeriodResolver → PostingPeriodGuard → JournalFactory → JournalValidator → Persistencia), componentes `internal` sin registro propio en DI — solo `IPostingEngine → PostingEngine` se registra. `PostingOutcomeDto`/`PostingOutcomeStatus` (`Created`/`AlreadyProcessed` — reintento del mismo hecho **es éxito**, nunca `Conflict`). Códigos de error: `RULE_NOT_FOUND`, `PERIOD_NOT_OPEN`, `VALIDATION_FAILED`. `JournalFactory` construye vía `JournalEntry.Create()` (sin DTO intermedio) con `SystemActor = Guid.Empty` (mismo patrón que `ExpireUserSessionsHandler`) y descripción determinística `"{SourceModule} — {FactType} — {SourceEventId}"`. `JournalValidator` es NO-OP documentado (partida doble aún no existe). Idempotencia real: `IJournalEntryRepository.FindByKeyAsync` + índice único `uq_journal_entries_company_source_event_fact` (`company_id`, `source_module`, `source_event_id`, `source_event_type`) — reemplaza el índice no-único anterior (migración `20260725013347_AddJournalEntryIdempotencyKey`); en carrera, `IDatabaseExceptionTranslator` traduce la violación UNIQUE y la segunda ejecución re-consulta y retorna `AlreadyProcessed`. `IAccountingPeriodRepository.FindContainingDateAsync` agregado para resolución de período por fecha. Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PostingEngineTests.cs` — RuleNotFound/PeriodNotOpen/Created/AlreadyProcessed, mocks) + 2 de integración PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/PostingEngineIntegrationTests.cs` — doble ejecución secuencial idempotente, concurrencia real con dos tareas paralelas verificando un único `JournalEntry`). **Pendiente al cierre de Fase 3.1**: `PostingRule.IsActive == false` no se validaba — resuelto en Fase 3.3 (ver abajo). `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence`, endpoints HTTP del Posting Engine y reportes financieros siguen sin implementar.
  - **Fase 3.3 — Primer consumidor real: SalesInvoiceAuthorizedPostingTranslator (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/Translators/SalesInvoiceAuthorizedPostingTranslator.cs` — `INotificationHandler<SalesInvoiceAuthorizedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>` (sin `DbContext`, sin repositorios de Sales), construye `PostingFact{ SourceModule="Sales", FactType="InvoiceIssued" }` y llama `PostAsync`; si falla, `LogWarning` con `InvoiceId`/`InvoiceNumber`/`Code`/`Error` y **no lanza excepción** — la autorización de la venta nunca se revierte por un problema de configuración contable. `SalesInvoiceAuthorizedEvent` enriquecido con `CompanyId`/`IssueDate` y `TenantId` ahora fijado en el constructor (antes quedaba siempre `null` — defecto real detectado y corregido, no solo teórico); los 3 datos se toman del propio agregado `SalesInvoice` en `Authorize()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente. `PostingRuleResolver` ahora trata `PostingRule` inactiva igual que regla inexistente (`RULE_NOT_FOUND`, sin código nuevo) — el filtro vive en el Resolver (Application), no en `IPostingRuleRepository.FindByKeyAsync` (compartido con `CreatePostingRuleHandler`, que sigue necesitando ver reglas inactivas para su pre-check de duplicados). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/SalesInvoiceAuthorizedPostingTranslatorTests.cs`, mocks) + 3 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/SalesInvoiceAuthorizedPostingIntegrationTests.cs`).
  - **✅ Hallazgo crítico de Fase 3.3 — RESUELTO (Fase 3.3.5, 2026-07-25)**: la re-entrancia de `SaveChangesAsync` detectada al conectar el primer Translator (`PostingPipeline` llamaba a `IJournalEntryRepository.SaveChangesAsync()` desde dentro de `ErpDbContext.SaveChangesAsync`, produciendo `DbUpdateConcurrencyException` real cuando coexistía con el handler de Caja sobre el mismo evento) quedó corregida con dos cambios: (1) `PostingPipeline` ya no comitea — solo hace `AddAsync` (staging) y retorna; la persistencia física pertenece exclusivamente al ciclo externo de `ErpDbContext.SaveChangesAsync`, misma convención que ya seguían `SalesInvoiceAuthorizedHandler` (Caja) y los `*AuditHandler`. (2) `IJournalEntryRepository.AcquireIdempotencyLockAsync(companyId, sourceModule, sourceEventId, factType, ct)` — nuevo método, implementado en `JournalEntryRepository` con `pg_advisory_xact_lock(int4, int4)` (mismo mecanismo que `DocumentSequenceRepository`/ADR-019, `StableHash` duplicado deliberadamente sin helper compartido), invocado por `PostingIdempotencyGuard` **antes** de `FindByKeyAsync`, sobre la transacción ambiente (nunca abre ni comitea transacción propia). Con el lock, dos ejecuciones concurrentes para la misma clave se serializan antes de competir por el mismo `INSERT` — la violación UNIQUE deja de ocurrir en el camino normal (el índice `uq_journal_entries_company_source_event_fact` queda como protección final, no como mecanismo primario). El stub de `ICashSessionRepository` en los tests de integración fue retirado — la suite corre con el repositorio real. Se agregó además un test de doble publicación concurrente del mismo `SalesInvoiceAuthorizedEvent` (Caja + Accounting reaccionando simultáneamente en dos transacciones distintas) que confirma ausencia de excepción y un único `JournalEntry`. Detalle completo del proceso de diseño: revisiones ARB Fase 3.3.1 (SaveChanges ownership) a 3.3.4 (readiness review). Habilitado conectar un segundo Translator (Purchases) con este mismo patrón.
  - **Fase 3.4 — Segundo consumidor real: PurchaseInvoiceConfirmedPostingTranslator (2026-07-25)**: replica exactamente el patrón de Fase 3.3 sobre `PurchaseInvoice.Confirm()`. `PurchaseInvoiceConfirmedEvent` enriquecido de forma aditiva con `CompanyId`/`IssueDate` (tomados del propio agregado en `Confirm()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente) — único consumidor preexistente del evento (`PurchaseInvoiceAuditHandler`, Entity Audit ADR-022) no requirió cambios, es aditivo. `ERP.Application/Modules/Accounting/Posting/Translators/PurchaseInvoiceConfirmedPostingTranslator.cs` — `INotificationHandler<PurchaseInvoiceConfirmedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>`, construye `PostingFact{ SourceModule="Purchases", FactType="InvoiceReceived" }` y llama `PostAsync`; si falla, `LogWarning` y **no lanza excepción** — la confirmación de la compra nunca se revierte por un problema de configuración contable. `PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository` — sin ningún cambio (mismo Posting Engine, ningún `SaveChangesAsync`/transacción/lock nuevo). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PurchaseInvoiceConfirmedPostingTranslatorTests.cs`, mocks) + 4 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/PurchaseInvoiceConfirmedPostingIntegrationTests.cs` — JournalEntry Draft, fallo sin revertir, idempotencia, concurrencia con advisory lock). Retenciones (`IssuedWithholding`) quedan explícitamente fuera de alcance — hecho contable distinto, Translator futuro si se requiere.
  - **Fase 3.5.2 — PostingFact Enrichment, cierre de ADR-026 §4 (2026-07-25)**: prerrequisito para el futuro motor de partida doble (`JournalEntryLine`, diseñado en Fase 3.5.1, aún no implementado). `SalesInvoiceAuthorizedEvent` y `PurchaseInvoiceConfirmedEvent` enriquecidos de forma aditiva con `Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount` — tomados de las propiedades ya computadas del propio agregado (`SalesInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Authorize()`, `PurchaseInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Confirm()`), sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany`. `PostingFact` extendido con los mismos 4 campos más `GrandTotal` — deliberadamente **sin** `Currency`/`ExchangeRate`/`Branch`/`CostCenter`/`Metadata` (fuera de alcance v1 por ADR-026 §10 y por ausencia de módulo `CostCenter`, ver Fase 3.5.1). `SalesInvoiceAuthorizedPostingTranslator`/`PurchaseInvoiceConfirmedPostingTranslator` actualizados únicamente en la construcción de `PostingFact` (una línea cada uno) — sin cambio de patrón, dependencias ni manejo de errores. Posting Engine (`PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository`) sin ningún cambio — los montos nuevos viajan en `PostingFact` pero `JournalFactory` todavía no los consume (eso pertenece a la fase de `JournalEntryLine`). Compatibilidad: 10 call sites de construcción de `SalesInvoiceAuthorizedEvent`/`PurchaseInvoiceConfirmedEvent`/`PostingFact` en código productivo y tests actualizados; regresión completa en verde (452 `ERP.Application.Tests`, 254 `ERP.Domain.Tests`, 97 `ERP.Architecture.Tests`, 10 de integración PostgreSQL real en `ERP.Infrastructure.Tests/Accounting`). ADR-026 §4 queda implementado en su parte de montos (`Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount`, alcance exacto de esta fase); **pendiente** el otro requisito original de §4 para `SalesInvoiceAuthorizedEvent` — *"información de pago necesaria para la contabilización (forma de pago / referencia de cobro)"* — no incluido en el alcance aprobado de Fase 3.5.2, queda para una fase posterior o para reevaluación explícita si el motor de partida doble no lo necesita.
  - **Fase 3.5.3 — Modelo de dominio de partida doble (2026-07-25)**: implementa únicamente el modelo de dominio aprobado en Fase 3.5.1 — sin persistencia EF Core, sin migración, sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`. `JournalEntryLine` (nueva entidad hija de `JournalEntry`, `ERP.Domain/Modules/Accounting/Entities/`) con invariante propio: exactamente uno de `Debit`/`Credit` mayor a cero, nunca ambos con valor ni ambos en cero (`JournalEntryLine.Create`, `IMustHaveTenant`, sin `CompanyId` propio — igual patrón que `PurchaseInvoiceDetail`/`SalesInvoiceDetail`). `JournalEntry` incorpora `Lines` (`IReadOnlyCollection<JournalEntryLine>`), `AddLine(accountId, description, debit, credit)` (construye la línea internamente, asigna `SortOrder` incremental) y `EnsureBalanced()` (Σ Debit == Σ Credit) — ninguno con consumidor todavía: `JournalFactory` sigue construyendo solo el encabezado (0 líneas), por lo que `EnsureBalanced()` se cumple trivialmente (0 == 0) sin invocarse desde ningún flujo real. `PostingRuleLine` (nueva entidad hija de `PostingRule`) con `AccountId`/`Nature` (`AccountNature`, reutilizado)/`AmountKind` (`PostingAmountKind`, enum nuevo)/`SortOrder`. `PostingRule` incorpora `Lines` + `AddLine(...)` — coexiste con `DebitAccountId`/`CreditAccountId` planos sin retirarlos (transición, ningún consumidor migra todavía). `PostingAmountKind` (`Subtotal`/`TaxVat`/`TaxIce`/`Discount`/`Retention`/`GrandTotal`) — únicos 6 valores aprobados en Fase 3.5.1, ninguno adicional. Hallazgo de compatibilidad EF Core resuelto: `JournalEntry.Lines`/`PostingRule.Lines` son navegaciones nuevas que `RelationshipDiscoveryConvention` detecta y registra como entidades independientes con tabla propia aunque se las ignore a nivel de propiedad (`builder.Ignore(x => x.Lines)` en cada `IEntityTypeConfiguration` no basta) — requiere además `modelBuilder.Ignore<JournalEntryLine>()`/`Ignore<PostingRuleLine>()` a nivel de `ErpDbContext.OnModelCreating()` para que el modelo runtime siga coincidiendo exactamente con la migración ya aplicada (`dotnet ef migrations has-pending-model-changes` verificado en `No changes`). Tests: 24 nuevos en `ERP.Domain.Tests/Accounting/` (`JournalEntryLineTests`, `JournalEntryTests`, `PostingRuleLineTests`) — Debit/Credit válidos, ambos con valor, ambos en cero, montos negativos, cuenta vacía, creación con líneas, `SortOrder` incremental, colección de solo lectura, `EnsureBalanced()` con/sin líneas balanceadas y desbalanceadas, naturaleza y `AmountKind` correctos. Regresión completa en verde: 278 `ERP.Domain.Tests` (254+24), 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 219 `ERP.Infrastructure.Tests` (incluye las 10 suites de integración PostgreSQL de Accounting ya existentes, sin cambios de comportamiento).
  - **Fase 3.5.4 — Persistencia de JournalEntryLine y PostingRuleLine (2026-07-25)**: única y exclusivamente la capa de persistencia del modelo aprobado en Fase 3.5.3 — sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`, sin generación automática de líneas, sin consumo de `PostingAmountKind`. `JournalEntryLineConfiguration`/`PostingRuleLineConfiguration` (`ERP.Infrastructure/Accounting/Persistence/Configurations/`) nuevas — `journal_entry_lines`/`posting_rule_lines`, `Debit`/`Credit` en `numeric(18,2)` (Estándar de Precisión Numérica INMUTABLE, CLAUDE.md). `JournalEntryLine.AccountId` con FK real a `accounts` (`Restrict`) — a diferencia de `PostingRuleLine.AccountId`, columna plana sin FK (mismo criterio ya vigente para `PostingRule.DebitAccountId`/`CreditAccountId`: configuración de datos, existencia se valida en Application al resolver, no en la base de datos). `JournalEntryConfiguration`/`PostingRuleConfiguration`: `Ignore(x => x.Lines)` reemplazado por `HasMany(x => x.Lines).WithOne().HasForeignKey(...).OnDelete(Cascade)` (mismo patrón que `PurchaseInvoice`→`PurchaseInvoiceDetail`) — cascade porque ninguna línea tiene sentido de existir sin su encabezado. `ErpDbContext`: retirados los dos `modelBuilder.Ignore<T>()` de Fase 3.5.3 (ya no aplican, las líneas ahora se mapean), agregados `DbSet<JournalEntryLine>`/`DbSet<PostingRuleLine>`. Migración `20260725165737_AddJournalEntryLineAndPostingRuleLine` — crea ambas tablas, 2 FKs (`journal_entry_lines→accounts` Restrict, `journal_entry_lines→journal_entries` Cascade, `posting_rule_lines→posting_rules` Cascade), 4 índices; no toca ninguna columna existente de `posting_rules` (`DebitAccountId`/`CreditAccountId` intactos, coexistencia deliberada durante la transición). Verificado `dotnet ef migrations has-pending-model-changes` → `No changes`. Tests: 8 nuevos de persistencia PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/JournalEntryLinePersistenceTests.cs`, `PostingRuleLinePersistenceTests.cs`) — guardar con líneas, recuperar navegación (`Include(x => x.Lines)`), integridad referencial (FK real en `JournalEntryLine` vs. ausencia deliberada de FK en `PostingRuleLine`), cascade delete de líneas al eliminar el encabezado. Regresión completa en verde: 278 `ERP.Domain.Tests`, 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 227 `ERP.Infrastructure.Tests` (18 en `Accounting/`, incluye las 10 suites de Sales/Purchases/PostingEngine ya existentes sin cambio de comportamiento).
  - **Fase 3.5.5 — JournalFactory & JournalValidator: motor de partida doble real (2026-07-25)**: `JournalFactory` deja de construir solo el encabezado — ahora itera `PostingRule.Lines` (`PostingRuleLine`, persistido en Fase 3.5.4), resuelve el monto de cada línea exclusivamente por `PostingAmountKind` (`Subtotal→fact.Subtotal`, `TaxVat→fact.TotalVat`, `TaxIce→fact.TotalIce`, `Discount→fact.TotalDiscount`, `GrandTotal→fact.GrandTotal`, `Retention→0m` — no disponible en `PostingFact` todavía, fuera de alcance de esta fase) y llama `JournalEntry.AddLine(...)` por cada línea con monto distinto de cero (líneas en cero se omiten, nunca se contabilizan). `JournalValidator` deja de ser NO-OP: valida mínimo 2 líneas, `AccountId` requerido, exactamente un monto (Débito o Crédito) por línea, ninguna cuenta simultáneamente en Débito y Crédito del mismo asiento, totales distintos de cero, y balance (`entry.EnsureBalanced()`, código `VALIDATION_FAILED` en cualquier fallo). **2 excepciones mínimas y necesarias, declaradas explícitamente**: (1) `PostingPipeline.ExecuteAsync` — una línea agrega el parámetro `PostingRule` ya resuelto a la llamada de `JournalFactory.Create(...)` (el orden de las 7 etapas no cambia, solo se propaga un dato ya calculado); (2) `PostingRuleRepository.FindByKeyAsync` — agrega `.Include(x => x.Lines)`, sin el cual `PostingRule.Lines` llegaría siempre vacío a `PostingRuleResolver` (`PostingRule` es `sealed` sin navegación `virtual`, no hay lazy loading posible). `PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalEntryRepository`/`Translators`/`PostingFact`/Domain Events sin ningún otro cambio. Compatibilidad: las 3 suites de integración PostgreSQL ya existentes (`PostingEngineIntegrationTests`, `SalesInvoiceAuthorizedPostingIntegrationTests`, `PurchaseInvoiceConfirmedPostingIntegrationTests`) actualizaron su `SeedRuleAndPeriodAsync` para sembrar `Account`s reales + `PostingRuleLine`s balanceadas (antes sembraban solo `DebitAccountId`/`CreditAccountId` legacy, sin `Lines` — habrían producido asientos de 0 líneas, rechazados por el nuevo `JournalValidator`). Tests: 12 unitarios nuevos (`ERP.Application.Tests/Accounting/JournalFactoryTests.cs`, `JournalValidatorTests.cs` — ejercidos indirectamente vía `PostingEngine.PostAsync` con repositorios mockeados, ya que `JournalFactory`/`JournalValidator` son `internal` sin `InternalsVisibleTo`, sin precedente de ese patrón en el proyecto) + 2 de integración PostgreSQL real nuevos en `PostingEngineIntegrationTests.cs` (persistencia de `JournalEntry` con `JournalEntryLine`, recuperación completa del agregado con balance verificado). Riesgo documentado: "cuentas existentes"/"cuentas activas" no se validan en `JournalValidator` (fuera del alcance aprobado para esta fase) — hoy solo protegidas por la FK real de `JournalEntryLine.AccountId` a nivel de base de datos, que falla como `DbUpdateException` no como `Result` limpio. Regresión completa en verde: 278 `ERP.Domain.Tests`, 464 `ERP.Application.Tests` (452+12), 97 `ERP.Architecture.Tests`, 229 `ERP.Infrastructure.Tests` (20 en `Accounting/`).
- **P0-01 — Devolución de Venta (SalesReturn) + Nota de Crédito SRI: COMPLETED / CLOSED (2026-07-31)** — módulo cerrado formalmente de punta a punta, sin código productivo pendiente. Diseño: [`P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md`](docs/archive/designs/P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md). Plan de ejecución por fases (1-15, todas cerradas) y backlog técnico no bloqueante: [`P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`](docs/archive/plans/P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md). Activación de Nota de Crédito v1.1.0: [`docs/decisions/ADR-031-credit-note-v1-activation.md`](docs/decisions/ADR-031-credit-note-v1-activation.md) (Accepted).
  - **Capacidades entregadas:** `SalesReturn`/`SalesReturnDetail`/`SalesReturnRefundAllocation` (Domain); devolución parcial y total sobre una `SalesInvoice` `Authorized`; ciclo Draft → Update → Cancel → Authorize; control de remanente devolvible bajo concurrencia real (advisory lock por factura + revalidación bajo lock, cierre de la ventana de condición de carrera que el chequeo preventivo del Draft no podía cerrar por sí solo); reversión de inventario (Kardex, `StockMovementType.SaleReturn`) al autorizar; reembolso explícito sin prorrateo automático — Efectivo / Crédito CxC / mixto (`SalesReturnRefundAllocation`, `Σ Amount == GrandTotal` como invariante de dominio); asiento contable automático vía `SalesReturnAuthorizedPostingTranslator` (mismo Posting Engine que Factura/Compra, ADR-026); Entity Audit (`SalesReturnAudit`, ADR-022); Nota de Crédito electrónica SRI V1.1.0 (XML, validación XSD, firma XAdES-BES, secuencial "04" vía `IDocumentSequenceRepository`, envío/autorización) activada por ADR-031; RIDE de Nota de Crédito; API REST documentada (`SalesReturnController`, `api/v1/sales/returns`); frontend completo (listado, formulario Draft/Authorize, sección de Nota de Crédito Electrónica); suite E2E de 23/23 escenarios contra PostgreSQL real (`SalesReturnEndToEndTests`).
  - **Mejora de infraestructura registrada junto con el cierre:** `DocumentSequenceRepository.CaptureNextAsync` corregido para participar de una transacción ambiente ya abierta por el caller (defecto real detectado durante el cierre de P0-01) — sin cambio de API pública ni de estrategia de locking de la infraestructura FROZEN de Secuencias Documentales (ADR-019).
  - **Pendiente operativo (no bloqueante para el cierre técnico):** prueba real de emisión de Nota de Crédito contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con certificado `.p12` configurado — no ejecutada en esta fase por no existir certificado de prueba disponible en este entorno (ver ADR-031, sección "Validación de la activación"). Mismo protocolo ya usado para cerrar ADR-023 con Factura (comprobantes reales, rechazo real confirmado) queda pendiente de repetirse para Nota de Crédito cuando haya certificado disponible.
  - **Backlog técnico no bloqueante** (detalle completo en la sección homónima de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`): wiring de React Hook Form + Zod en el formulario Draft de `SalesReturnFormPage`; unificación de `formatApiError`/`formatApiRequestError` en `SalesReturnCreditNoteSection`; evaluación de la ubicación REST de `GET .../returnable-lines`; consolidación de fixtures de test repetidas en `ERP.Application.Tests/Sales`; constante propia (no heredada de `SalesInvoice`) para la longitud de `CreditNoteDocumentNumber`. Ninguno bloquea el cierre — todos fueron evaluados y descartados de corrección inmediata en la auditoría de hardening previa por implicar refactor o riesgo de cambio de comportamiento fuera de ese alcance.

**Futuro (no implementado, fuera del ERP actual)**
- Plataforma externa — ver [`docs/future-platform/`](./future-platform/)

---

## FASE 1 — ERP Kernel Cleanup — COMPLETE 2026-06-05

> Branch `feat/platform-kernel-refactor`. Todos los componentes SaaS eliminados. Build: **0 errores**.
> Eliminado: Billing domain, Subscriptions domain, Platform entities, Commercial plans, Entitlements,
> SaaS controllers/middleware/jobs/services/behaviors. Tests SaaS eliminados. ERP puro compila limpio.
>
> **FASE 2 — Subscriber → Tenant rename: COMPLETADA (2026-07-23).** JWT claim (`tenant_id`), columna BD (`tenant_id`), DbContext (`ITenantScopedEntity`), frontend (componentes, i18n, navegación) y documentación normativa (`docs/architecture/`) consolidados en `Tenant`.
>
> Deuda cosmética conocida y no bloqueante:
> - nombres de variable/parámetro `subscriber` en código backend.
> - nombres históricos de índices SQL con `_subscriber_`.
>
> La columna física y el aislamiento real usan `tenant_id`. Esta deuda queda pendiente para una limpieza mecánica futura.

---

## ERP CORE FREEZE — GOVERNANCE LOCK ACTIVE (2026-06-08)

> **ERP Core está oficialmente congelado como producto independiente.** Acta completa, módulos incluidos/excluidos, frontera de integración (`/api/integration/v1/*`, [ADR-ERP-002](adr/ADR-ERP-002-platform-separation.md)) y reglas obligatorias (*ERP never depends on Platform* / *Platform may consume ERP APIs only*) en [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md).

## ERP CORE BASELINE v1.0 — FROZEN 2026-06-05

> Architecture frozen. Changes to any module below require an Architecture Review before implementation.

| Module | Closed | Evidence |
|--------|:------:|----------|
| BusinessPartner V2 (Customer + Supplier roles) | ✅ | `docs/decisions/ADR-017-business-partner-scope.md` |
| Customer Module | ✅ | BP V2 Customer closed 2026-06-04 |
| Supplier Module | ✅ | BP V2 Supplier closed 2026-06-04 |
| Company Isolation (ICompanyOperationalEntity + EF filters) | ✅ | `docs/security/MULTI-TENANT-HARDENING.md` |
| Security Hardening (CompanyScopeBehavior, namespaced fallback removed) | ✅ | Migration `20260605113654_AddCompanyIdToOperationalEntities` |
| Multi-Tenant Boundaries (all scopes explicit, fail-closed dual filter) | ✅ | `FINAL HARDENING REPORT 2026-06-05` — 0 CRITICAL/HIGH/MEDIUM/LOW issues |

**Test baseline at freeze:** ERP.Application.Tests 190/190 · ERP.API.Tests SecurityTests 33/33 · Build 0 errors.

---

## Documentation map (canonical — `docs/architecture/` + `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md` + docs/ + índices)

| Topic | File |
|-------|------|
| **Implementation rules (canonical)** | `docs/architecture/README.md` |
| Index | `CONTEXT.md` |
| Repo structure (2026-05) | `README.md`, `infrastructure/`, `scripts/`, `tools/` |
| Product summary | `README.md` |
| Agent adapters | `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `.cursor/rules/` → `docs/architecture/*` |
| Delivery state | `STATUS.md` (this file) |
| Priorities | `docs/ROADMAP.md` |
| Architecture | `docs/ARCHITECTURE.md` |
| Architecture rules (PR blocking) | `docs/architecture/pr-rules-catalog.md` (entry: `docs/ARCHITECTURE-RULES.md`) |
| ADRs (architectural rationale) | `docs/decisions/README.md` |
| Development + stack | `docs/DEVELOPMENT.md` |
| Identity + security | `docs/IDENTITY.md` |
| SaaS plans + billing (histórico) | `docs/archive/SAAS-COMMERCIAL.md` |
| Database | `docs/DATABASE.md` |

Consolidated 2026-05-21: former `MULTITENANCY`, `SCOPES`, `SECURITY`, `BILLING`, `DATABASE/*`, etc. merged into the files above. **2026-05-21:** `AI-RULES/` centralized implementation rules for Cursor, Claude and future agents. **2026-08-07 (Bloque 16B):** `AI-RULES/` reorganizado a `docs/architecture/` (SSOT único) + `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md`; contenido original archivado en `docs/decisions/archive-ai-rules/`.

## Módulos FROZEN (arquitectura cerrada)

Los siguientes módulos tienen su arquitectura y modelo de datos cerrados definitivamente.
No se aceptan cambios estructurales sin una ADR aprobada.

| Módulo | Fecha cierre | ADR | Notas |
|--------|:------------:|-----|-------|
| **Business Partners V2** (Clientes / Proveedores) | 2026-06-05 | `docs/decisions/ADR-017-business-partner-scope.md` | subscriber-scoped, Roles (Customer/Supplier), CompanySettings, LegalRepresentativeName, unique index DB |
| **Customer Module** | 2026-06-05 | BP V2 ADR | FROZEN + FREEZE GATE PASS (2026-06-17); 5 ARs, 31+ endpoints, 20 domain events, 38 [Authorize]; UI completa: listado + wizard + detalle + ubicaciones CRUD + contactos CRUD + roles + trading settings; RUC/CI SRI; consumidores: Sales, Quotations, Orders, E-Invoicing, CRM, AR |
| **Supplier Module** | 2026-06-05 | BP V2 ADR | Fiscal + classification, full FROZEN |
| **Company Isolation** | 2026-06-05 | Security Hardening Report | ICompanyOperationalEntity, fail-closed EF filters, PaymentApplication, ArAp/AccountingPeriod scopes |
| **Security Hardening** | 2026-06-05 | Security Hardening Report | CompanyScopeBehavior explicit only, 0 namespace fallback, all APIs fail-closed |
| **Multi-Tenant Boundaries** | 2026-06-05 | Security Hardening Report | 223/223 tests, migration 20260605120243_FinalHardening |
| **SaaS Commercial Flow** | 2026-05-28 | `docs/archive/historical-decisions/SAAS-FREEZE.md` | Plans, Entitlements, Subscription lifecycle |
| **Sucursales** | 2026-06-16 | — | Entidad organizativa (no fiscal); CRUD + soft-disable; ruta `/settings/branches` |
| **Establecimientos SRI** | 2026-06-16 | — | Código SRI único por empresa; BranchId opcional; disable bloqueado si tiene PEs activos; ruta `/settings/establishments` |
| **Puntos de Emisión** | 2026-06-16 | — | Código único por Establecimiento; DocumentSequence automático; ruta `/settings/emission-points` |
| **Items / Catálogo v1.0** | 2026-06-17 | A1 CLOSED 2026-10-05 | 14 entidades, 56 endpoints, 20 validators; catálogo Tenant + Company, compartido entre sucursales de su empresa; ItemNature Product/Service independiente de clasificación y control de existencias (A1 / ADR-040); 6 catálogos CRUD (Brand, Family, Category, Subcategory, AttributeGroup, AttributeDefinition); Detail page con Variants, Images, Conversions, Substitutes, Packaging; SRI lookups (UOM, VAT, ICE); [informe A1](docs/items/A1-COMPANY-SCOPE-INVENTORY-NATURE.md) |
| **Sales Invoice + Detail** | 2026-06-24 | — | Aggregate root SalesInvoice + SalesInvoiceDetail; lifecycle Draft→Authorized→Cancelled; freeze contract irreversible (IsFrozen + EnsureDraft); snapshot fiscal (VAT/ICE rates + amounts + names); computed totals no persistidos (LineSubtotal, TaxableBase, TaxInclusiveTotal); AuthorizedSubtotal/GrandTotal congelados al autorizar; ReplaceLines único mutator; DocumentSequence SRI; facturación electrónica (AccessKey, AuthorizationNumber); frontend preview-only (salesCalc.ts); 4 use cases (Draft CRUD, Authorize, Discount, Cancel); FluentValidation; company-scoped + tenant-scoped |
| **Payment Methods + Formas de Cobro** | 2026-06-24 | — | PaymentMethod catálogo dinámico (CRUD+Toggle, multi-tenant, seed 5 métodos). SalesInvoicePayment entidad hija (N pagos por factura, snapshot Code+Name, Amount>0, Reference condicional). Authorize() valida ≥1 pago + Sum==GrandTotal. Sin enums, sin JSONB, sin auto-default. Base definitiva para CxC/Cobros/Caja/Contabilidad |
| **Sales Receivable (CxC deuda)** | 2026-06-25 | — | SalesReceivable + SalesReceivableInstallment. Solo crédito (CreditTermDays>0 o Installments>1). PaidAmount=0 (sin cobros). Cancel cascada desde factura. 2 tablas, 6 índices, 2 endpoints GET. Módulo pasivo: registra deuda, no cobra |
| **Estándar de Precisión Numérica** | 2026-06-25 | — | CLOSED. 73/73 columnas auditadas, 100% compliance. Reglas: [`docs/architecture/data-standards.md`](architecture/data-standards.md) |
| **Estándar de Fechas y Horas** | 2026-06-25 | — | CLOSED. Reglas: [`docs/architecture/data-standards.md`](architecture/data-standards.md) |
| **Infraestructura de Mensajes Visuales** | 2026-06-29 | `docs/decisions/ADR-018-message-infrastructure.md` | API pública `message.*` congelada. Store interno encapsulado. Cola FIFO + deduplicación. 22 tests. ESLint gate activo. |
| **Infraestructura de Secuencias Documentales** | 2026-06-29 | `docs/decisions/ADR-019-document-sequence-infrastructure.md` | CLOSED. 4 gates CI-bloqueantes, suite concurrente 8/8 passing (PostgreSQL 16 real, 500 req simultáneas, 0 duplicados). Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura de Entity Tracking (EF Core Change Tracking)** | 2026-06-30 | `docs/decisions/ADR-020-entity-tracking-infrastructure.md` | CLOSED. `ATT-GATE-01` gate CI-bloqueante, 6/6 tests de integración passing (PostgreSQL 16 real, Testcontainers). Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura de Valores por Defecto de Facturación** | 2026-07-01 | — | CLOSED. Migrado a `org_settings` (Phase 8, 2026-07-01) — ya no `SriSettings`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura Tributaria (Tax Infrastructure)** | 2026-07-01 | — | CLOSED. Motor único `ISriTaxResolver`/`sriLookupService.*Rates()`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Tipos de Ítem (Item Types)** | 2026-07-04 | — | CLOSED. `ItemTypeDefinition` catálogo tenant-editable, reemplaza el enum fijo `Physical/Service/Digital/Kit/Bundle`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Items Administration** | 2026-07-07 | — | Item CRUD (14 entidades hijas: variantes, códigos de proveedor, barcodes, imágenes, conversiones, sustitutos, packaging), pricing base (`Item.BaseSalePrice` SSOT), catálogo de Tipos de Ítem tenant-editable, `ItemAudit` (Entity Audit) sobre `ItemCreatedEvent`/`ItemUpdatedEvent`/`ItemPriceChangedEvent`/`ItemEnabledEvent`/`ItemDisabledEvent`. Deuda técnica documentada (no bloqueante): `ItemVariantAddedEvent`/`ItemVariantDisabledEvent` no implementan `IAuditEvent` — cubrirlos requiere modificar las clases de evento, decisión explícita futura |
| **Pricing Administration** | 2026-07-07 | — | `PriceList` (contenedor + regla general opcional), `PriceListItem` (asignación administrativa ítem↔lista, sin reglas ni precios), `PricingRule` (excepción por ítem, override de la regla general). `PricingResolver`/`PricingCalculation` como única API de resolución de precio neto. Auditoría de dominio completa vía Domain Events: `PriceListAudit` (creación/actualización/activación/desactivación), `PriceListItemAudit` (asignación/activación/desactivación), `PricingRuleAudit` (creación/actualización/activación/desactivación, con old/new tipados). Invariante `PricingRule` requiere `PriceListItem` activa (validado en `SetPricingRuleHandler`/`EnablePricingRuleHandler`) — no existen reglas huérfanas. Pricing no calcula impuestos (frontera con `ISriTaxResolver`/`sriLookupService`). Pricing no soporta `ItemVariantId` (retirado deliberadamente 2026-07-07, ver `PricingRule.cs`). Endpoint legacy `/api/v1/pricing/item-prices` queda explícitamente fuera de este freeze — pendiente del cierre de Compras |
| **Infraestructura de Auditoría por Dominio (Entity Audit)** | 2026-07-07 | `docs/decisions/ADR-022-audit-infrastructure-entity-vs-process.md` | Contratos comunes `AuditRecordBase`/`AuditActor`/`AuditSource`/`IAuditEvent` (Domain) + `IAuditWriter<T>`/`IAuditReader<T>`/`IAuditContext`/`IAuditService` (Application) + `EfAuditWriter<T>`/`EfAuditReader<T>`/`HttpAuditContext`/`AuditService` genéricos (Infrastructure, open-generic en DI). Dispatcher reutiliza domain events + Outbox ya FROZEN (ADR-007/008). Pilotos: `PricingRuleAudit`, `PriceListItemAudit`, `PriceListAudit` (tablas `pricing_rule_audit`, `price_list_item_audit`, `price_list_audit`). Cada dominio nuevo agrega solo su entidad + eventos + handler, sin tocar la infraestructura común. `UserActivity` queda reservada al feed liviano, no a auditoría de negocio tipada. Process Audit (auditoría de procesos sin `EntityId` único — recálculos masivos, cierres, ETL, jobs) queda diseñado y documentado en `docs/architecture/audit-infrastructure.md`, sin implementar: reutilizará el `EntityId` como `ProcessRunId` sintético, sin modificar ningún contrato FROZEN. `UserName` resuelto 2026-07-07: snapshot histórico obligatorio en `AuditActor` (no-nullable, fallback `"Unknown"`), poblado desde claims JWT (`ClaimTypes.Email`/`ClaimTypes.Name`) embebidas al emitir el token en `AccessTokenService` — no de una consulta en vivo. Corregido el mismo día un error de claim (`GivenName` representa solo el nombre, no el nombre completo; se corrigió a `ClaimTypes.Name`, con fallback transitorio de compatibilidad en `CurrentUserService`). `AuditActor` confirmado como único modelo oficial del actor (ampliado additive con `FullName`/`Email`/`RoleName` opcionales) — regla Open/Closed nueva: prohibido agregar columnas de identidad del usuario en las entidades de auditoría de cada dominio. Columna `user_name` migrada a `NOT NULL` (`MakeAuditUserNameRequired`). Deuda técnica restante (no bloquea el freeze del contrato): `Source` hardcodeado a `UserAction` en `HttpAuditContext` (falta contexto para jobs/sistema), `CorrelationId`/`RequestId` sin truncado antes de persistir en `varchar(100)`. |
| **ElectronicDocuments v1.0 (Facturación Electrónica SRI)** — **CIERRE OFICIAL** | 2026-07-11 | `docs/decisions/ADR-023-electronic-documents-v1-closure.md` | Núcleo FROZEN: generación XML, validación XSD, firma XAdES-BES, recepción/autorización SRI (esquema offline), reintentos con backoff (`ElectronicDocumentRetryPolicy`, 5 intentos), Monitor de consulta. Cerrado tras 3 rondas: auditoría de robustez (2 críticos + 3 altos corregidos con evidencia/reproducción — TIMEOUT deadletering prematuro, pipeline sin try/catch, Hangfire sin guard de concurrencia, IDOR Company Scope en retry, 503→409 en carrera de registro), cumplimiento del Anexo Técnico SRI verificado texto por texto contra el PDF oficial (clave de acceso módulo 11 reproducido bit a bit, catálogo `sri_error_code` reescrito con 33 códigos reales), y pruebas reales contra `celcer.sri.gob.ec` (8 comprobantes reales, incluido un rechazo real confirmado con código `[65]`). **Addendum RESP-01 (2026-07-11, causa 2 — bug demostrado)**: reenvío de Recepción ahora trata también los códigos `[43]`/`[45]` (no solo `[70]`) como "ya existe, consultar autorización" en vez de rechazo automático — 2 tests de regresión agregados, ningún contrato modificado. Solo `Invoice` tiene builder/provider/validador activo — CreditNote/DebitNote/ShippingGuide/Retention/PurchaseSettlement tienen XSD/catálogo pero sin implementación (`activeVersion: null`), documentado como límite explícito. Deuda técnica aceptada y no bloqueante (ver ADR-023, sección "Cierre oficial"): búsqueda del Monitor acoplada a Sales, contraseñas de certificado legacy en texto plano, `AVG` en memoria, `GetRetryCandidatesAsync` sin paginación. Cambios futuros al núcleo solo por: cambio obligatorio SRI, bug demostrado, vulnerabilidad de seguridad, o rendimiento crítico. |
| **Infraestructura de Diagnóstico SRI reutilizable** | 2026-07-11 | `docs/decisions/ADR-024-electronic-document-diagnostic-infrastructure.md` | Extensión aditiva y controlada de ADR-023 (causa 1: campo real de la Ficha Técnica, `<mensaje>/<tipo>`, descartado silenciosamente). `SriMessage` (Domain value object) capturado por `SriSoapClient` en paralelo al texto aplanado existente — corrigió en el camino un bug real de parsing (mensaje fantasma por reutilización del tag `<mensaje>` en el esquema SRI). Solo `ElectronicDocument.MarkRejected` gana un parámetro opcional; `MarkFailed`/`MarkDeadLetter` sin cambios. Segundo suscriptor de `ElectronicDocumentRejectedEvent` (`ElectronicDocumentSriMessageAuditHandler`, tabla nueva `electronic_document_sri_message`) — mismo patrón `PricingRuleAudit`/`ElectronicDocumentAudit`, sin tocar `IAuditReader<T>`/`IAuditWriter<T>` genéricos. `ElectronicDocumentDiagnosticDto` único contrato reutilizable (retira `ElectronicDocumentErrorInfoDto`), ensamblado por `ElectronicDocumentDiagnosticAssembler` y consumido por Monitor, el reintento manual (cierra un bug real de contrato: `RetryElectronicDocumentCommandHandler` devolvía `ElectronicDocumentDto` en vez del detalle completo) y el nuevo `GET /api/v1/electronic-documents/by-source` agnóstico de módulo. Frontend: `ElectronicDocumentDiagnosticPanel` (`components/zh/electronicDocuments/`) integrado en Monitor y en Ventas (`SalesElectronicDiagnosticDrawer`, segundo consumidor real). Retenciones/Notas/Guías quedan explícitamente fuera (sin emisión activa, ver límites de ADR-023). |
| **Recepción XML de Compras → Compra** — **CIERRE OFICIAL** | 2026-07-28 | `docs/decisions/ADR-028-purchase-reception-to-purchase-flow-freeze.md` | Flujo congelado: Recepción XML → Descargar XML → Crear Compra → Formulario precargado → Guardar Compra. `PurchaseReceptionDocument.XmlContent` es evidencia fiscal inmutable; `PurchaseReceptionLine` es el único snapshot operativo (nunca se elimina una línea por ausencia de Item o fallo de matching); `IPurchaseReceptionDetailProcessor` es la única interpretación de XML→snapshot+Item Matching, reutilizada por la descarga inicial y por la reconstrucción transparente e interna de `CreatePurchaseReceptionDraftHandler` (dispara solo si `ProcessingStatus.Failed`, persiste de inmediato, nunca reconstruye dos veces — verificado por tests dedicados). Un único botón "Crear Compra", sin endpoints ni acciones de "reprocesar" expuestos al usuario. Deuda aceptada y documentada (no bloqueante, ver ADR-028 "Consecuencias"/"Riesgos"): `PurchaseReceptionDocument.MarkProcessed(...)` existe pero no tiene invocador real — `CreatePurchaseDraftCommand` (creación de `PurchaseInvoice`) no recibe todavía un `PurchaseReceptionDocumentId`. Evolución futura (workflow de aprobación de Compras, no implementado) documentada en `docs/decisions/ADR-029-purchase-approval-workflow-future-evolution.md`. |

### Items Administration
Estado: FROZEN

Contrato cerrado:
- Item master data
- Item pricing base
- Item child entities
- Item audit

### Pricing Administration
Estado: FROZEN

Contrato cerrado:
- Price Lists
- Price List assignments
- Pricing Rules
- Pricing resolution rules
- Pricing audit

Restricciones:
- Pricing no calcula impuestos.
- Pricing no soporta ItemVariantId.
- PricingRule requiere PriceListItem activo.
- Auditoría mediante Domain Events.

### Items — PVP fix (2026-06-24)

Fix de actualización de PVP en formulario de edición de ítems:
- Schema de validación correcto (`updateItemSchema` sin `sku`) al editar
- Precio se carga desde `itemPriceService.list()` al abrir edición
- Precio se persiste via `itemPriceService.setInitial()` al guardar

### Compras — Auditoría UX + SSOT (2026-06-24)

Auditoría completa del formulario de Compras. Build: **0 errores frontend + backend**. Tests: **47/47 PASS**.

| Mejora | Detalle |
|--------|---------|
| Código muerto eliminado | `ItemContextPanel`, `creditDays`, `profileLoading`, `expandedLines`/`toggleExpand` (−184 líneas neto) |
| Duplicidad visual eliminada | SKU en select bodega, nombre producto en panel contexto |
| Descuento por línea | Input editable 0-100% (backend ya lo soportaba, UI no lo exponía) |
| Cálculo local IVA/ICE | Estimación en borrador nuevo usando `ctx.vatPercent`/`ctx.icePercent` — elimina totales engañosos $0 |
| Alerta costo fuera de rango | Warning visual cuando costo difiere >20% del promedio SSOT |
| Selector condición de pago | Backend: `Guid? PaymentTermId` opcional en commands (backwards compatible). Frontend: select en cabecera con regeneración automática de cuotas |
| Secciones colapsables | Info Electrónica y Observaciones colapsables, auto-expand si tienen datos |
| Lógica extraída + testeable | `purchaseCalc.ts` con funciones puras; 27 tests unitarios (Vitest) |
| Import huérfano eliminado | `UpdatePurchasePayload` |
| CSS huérfano eliminado | `.pdl-line__disc-badge*`, `.pf-mini-card--obs` |

---

## Architecture (current)

| Area | State |
|------|--------|
| Modular monolith (Clean + CQRS) | ✅ |
| EF baseline `20260606040144_ErpBaselineClean` | ✅ |
| Tenant / Company / Membership model (`SubscriberId → TenantId` consolidado FASE 4) | ✅ |
| `CompanyScopeBehavior` (pipeline MediatR) | ✅ |
| Wave 1 `company_id` (inventory core) | ✅ (in baseline) |
| PostgreSQL RLS (enterprise tables) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Architecture guardrails CI (scripts + NetArchTest) | ✅ (2026-05-21) |
| **Frontend architecture checks (Node ESM)** | ✅ 12/12, score 100/100 (2026-05-24) — controllers backend ≤150 líneas |
| **Architecture governance v2** (ADRs, backend Node checks, score, PR annotations) | ✅ (2026-05-21) |
| Architecture baseline v1.0 remediation (lint, E2E smoke, legacy platform controller, SYSTEM_TRUTH) | ✅ (2026-05-21) |
| Post-audit remediation (session SEC, Sales unify, Kardex CQRS, Cash validators) | ✅ (2026-05-21) |
| Post-audit wave 2 (menu builder split, services→modules, access/security pages) | ✅ (2026-05-21) |
| Post-audit wave 3 (menu builder modular split, test sessionStorage) | ✅ (2026-05-21) |
| Enterprise monorepo root (`infrastructure/`, `scripts/`, `tools/`, docs stubs) | ✅ (2026-05-21) |
| Post-reorg stabilization (paths, CI green, company-scoped inventory movements) | ✅ (2026-05-21) |
| Post-audit P2 + wave 4 (services eliminados, AppLayout/Companies split) | ✅ (2026-05-21) |
| Post-audit wave 5 (PR-7 TSX: catálogo, clientes, contabilidad, menu builder, platform shell) | ✅ (2026-05-21) |
| Post-audit wave 6 (handlers C-03, lazy routes, grandfather vacío) | ✅ (2026-05-21) |
| **docs/architecture/ multi-agent governance** (`docs/architecture/*` canonical; `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md` + `.mdc` adapters) | ✅ (2026-05-21, reorganizado 2026-08-07) |

Details: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE.md](./DATABASE.md).

### Post-audit remediation (2026-05-21)

| Item | Estado |
|------|--------|
| Frontend: tokens en memoria + perfil/bootstrap/permisos en `sessionStorage`; `SessionBootstrap` + cookie refresh | ✅ |
| Backend: `ERP.Application/Sales` consolidado bajo `Modules/Sales` + validators Notas/Retenciones | ✅ |
| Backend: `EnqueueKardexReportCommand` (controller sin `SaveChangesAsync`) | ✅ |
| Backend: validators Cash (caja/bancos/conciliación) | ✅ |
| Pendiente PR-7 TSX >500 | ✅ (grandfather `tsxMaxLines500` vacío 2026-05-21) |

### Post-audit wave 5 (2026-05-21)

| Item | Estado |
|------|--------|
| `MenuBuilder` + `NavigationMenuEditorPanel` modularizados (controller + subpaneles) | ✅ |
| `PlatformPanelPage` + `PlatformPlansSection` en hook + tabs/modales | ✅ |
| `AccountingPage`, `BranchesPage`, `CustomersPage`, `SriConfigPage`, `BodegasPage` | ✅ |
| `CatalogPages`, `CatalogStructurePage`, categorías/subcategorías | ✅ |
| `architecture-grandfather.json`: `tsxMaxLines500` vacío | ✅ (`tools/architecture/`) |

### Post-audit wave 6 (2026-05-21)

| Item | Estado |
|------|--------|
| Handlers C-03: `CrearVenta`, `CreateProduct`, `UpdateProduct`, `EmitirFactura`, `EnviarNotaSri` (Handle ≤150) | ✅ |
| `ProductCommandMutationHelper` compartido create/update | ✅ |
| Rutas lazy: `accessRoutes`, `companiesRoutes`, `companyManagementRoutes`, `publicRoutes`, `mainRoutes` (placeholder) | ✅ |
| Grandfather vacío (`handlerHandleMaxLines150`, `tsxMaxLines500`, `tsxPageWrapperMaxLines15`) | ✅ |
| Chunk `index-*.js` ~362 KB (límite 650 KB) | ✅ |

### Post-audit P2 (2026-05-21)

| Item | Estado |
|------|--------|
| Carpeta `frontend/src/services/` eliminada (cero consumidores; API solo en `modules/*/api`) | ✅ |
| `SalesReportPage` → `modules/reportes/pages/` + wrapper 1 línea | ✅ |
| Placeholders → `modules/shared/pages/` + wrappers delgados | ✅ |
| `components/ui` sustituido por ZH en company-management, access, security, companies | ✅ |

### Post-audit wave 4 (2026-05-21)

| Item | Estado |
|------|--------|
| `AppLayout.tsx` (~634 → ~216) + `AppLayoutMainMenu`, `useAppLayoutNavigation`, banner | ✅ |
| `CompaniesPage.tsx` (~820 → ~252) + `useCompaniesPage`, `CompaniesPageDataTab` | ✅ |
| Grandfather: retirados `AppLayout`, `CompaniesPage`, `SalesReportPage` | ✅ |

### Post-audit wave 3 (2026-05-21)

| Item | Estado |
|------|--------|
| `usePlatformGateMenuBuilder` (~844 → ~371 líneas) + effects/actions/persist extraídos | ✅ |
| `PlatformMenuBuilderCrmWorkspace` (~934 → ~259 líneas) + panels/preview/audit/modals | ✅ |
| Test `syncSessionEntitlements` con stub `sessionStorage`/`localStorage` | ✅ |
| Grandfather: `PlatformMenuBuilderCrmWorkspace` retirado de PR-7 | ✅ |

### Post-audit wave 2 (2026-05-21)

| Item | Estado |
|------|--------|
| `PlatformMenuBuilderSection` dividido en entry + hook + CRM/legacy panels | ✅ |
| Imports `services/` → `modules/*/api` (cero consumidores directos en `src/`) | ✅ |
| `ProfilesPage`, `SubscriberAccessPage`, `SecuritySettingsPage` en `modules/` + wrappers delgados | ✅ |
| Re-exports `@deprecated` en `frontend/src/services/` para compatibilidad | ✅ (carpeta eliminada 2026-05-21) |
| Grandfather JSON actualizado (CRM workspace, sin legacy service imports) | ✅ |

## SaaS platform y ERP backend (snapshot histórico — pre FASE 1)

> ⚠️ **Snapshot pre-refactor (2026-05-23/24).** Las dos tablas siguientes describen el estado **anterior** a "FASE 1 — ERP Kernel Cleanup" (2026-06-05, ver banner al inicio de este documento), que eliminó por completo Billing domain, Subscriptions domain, Platform entities, Commercial plans y Entitlements, y a "FASE 4" (consolidación `SubscriberId → TenantId` + BP V2). Items como *Billing governance*, *Entitlements snapshot*, *Commercial limits*, *Sales/Accounting/Cash* descritos abajo **ya no existen** como módulos activos del backend — ver el inventario real de módulos en [`docs/ARCHITECTURE.md`](./ARCHITECTURE.md#bounded-contexts) y el estado vigente en "ERP CORE BASELINE v1.0" arriba. Se conservan como registro histórico de delivery, no como estado actual.

| Component (histórico) | Status (al 2026-05-23) |
|-----------|--------|
| Subscribers / plans / features | ✅ |
| Platform UI naming + API JSON aliases + middleware rename | ✅ (2026-05-23) |
| Subscriber ficha unificada + impersonación con retorno | ✅ (2026-05-23) |
| Company management API + UI (`/companies`) | ✅ |
| Switch company + JWT claims | ✅ |
| Commercial limits (companies, users, branches, warehouses) | ✅ |
| Entitlements snapshot API | ✅ |
| Billing governance + API | ✅ backend |
| Billing UI | ⏳ not built |
| Stripe / real payment provider | ⏳ `NullPaymentProviderAdapter` |

| Module (histórico) | Status (al 2026-05-24) |
|--------|--------|
| **Business Partners (Clientes/Proveedores) — FROZEN** | ✅ FROZEN 2026-06-02 — ver `docs/decisions/ADR-017-business-partner-scope.md` (sigue vigente como BP V2) |
| Products, catalogs, customers, suppliers | ✅ |
| Inventory, transfers, adjustments, kardex | ✅ |
| Purchases (OC, bills, expenses) | ✅ (UX/SSOT audit 2026-06-24) |
| Sales + electronic invoice (SRI code) | ✅ code / 🟡 real SRI validation pending |
| **Sales commercial pipeline** (quote → order → invoice, `DocumentRelation`) | ✅ API + UI + E2E (2026-05-24) |
| Accounting, cash | ✅ |
| Retenciones / guía remisión | 🟡 partial / placeholder UI |

### Backend architecture hardening (audit 2026-05-21)

| Item | Status |
|------|--------|
| SRI post-auth atomic transactions (`IUnitOfWork` ambient + journal entry nested) | ✅ |
| `SriSettings.CertPassword` encrypted at rest (Data Protection, legacy plaintext fallback) | ✅ |
| `Company` → `ISubscriberScopedEntity` + global EF subscriber filter | ✅ |
| `AccountingService` orchestration in Application layer | ✅ |
| API DbContext leakage → CQRS (`GetAppFeatureTree`, `ListPendingSriRetry`, `IAppFeatureRepository`) | ✅ |

## Frontend

| Area | Status |
|------|--------|
| Auth, subscriber select, company select | ✅ |
| Core ERP modules (sales, purchases, inventory, settings) | ✅ |
| **Ventas pipeline UI** (`/sales/quotes`, `/sales/orders`, `/sales/invoices`, credit notes) | ✅ (2026-05-24) |
| **`fullLogout()` centralizado** (stores + localStorage + `erp.saas.*`) | ✅ |
| **Products/customers — fuente única en `modules/*`** (`apiEnvelope`, adapters `@deprecated`) | ✅ |
| **Consolidación modular P3** (auth, branches, accounting, dashboard, platform API + pages) | ✅ |
| **Catálogo + bodegas + auth UI** en `modules/catalog`, `modules/inventario/warehouses`, `modules/auth/pages` | ✅ |
| **Lazy routes P4** (`routes/lazyPage.tsx`, main/catalog/platform split) | ✅ |
| **Platform naming cleanup** (`/platform/*`, `platformAuth.ts`, sin `isPlatformOperator`) | ✅ (2026-05-23) |
| **ZH UI estándar** (`components/ui` delega clases ZH; catálogo usa `ZHCard`/`ZHSearchBar`) | ✅ |
| Company management module | ✅ |
| SaaS billing pages | ⏳ |
| Kardex / stock dedicated UI | ⏳ placeholder routes |
| Legacy `tenant` i18n aliases | 🟡 rename deferred |

## PostgreSQL

| Item | Status |
|------|--------|
| Schema from single baseline | ✅ |
| Naming `_subscriber_` on indexes/FK | ✅ |
| RLS enabled (inventory, sales core) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Session vars via interceptor | ✅ |
| Company scope on operational entities | ✅ (baseline + query filters) |

## Security

| Item | Status |
|------|--------|
| JWT + refresh rotation (FamilyId, grace configurable, revocación por familia, rate limit IP/user/family, audit logs) | ✅ |
| Multi-tab SPA (Web Locks + BroadcastChannel + bootstrap retry) | ✅ |
| Permission policies | ✅ |
| Company isolation (app layer) | ✅ |
| SRI certificate password encryption (Data Protection) | ✅ |
| RLS (DB layer) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Platform operator bypass (JWT global) | ✅ controlled |
| Permissions cache in handler hot path | ⏳ service exists, wiring partial |
| SPA session cleanup (`fullLogout`) | ✅ frontend |

## Cache

| Cache | Status |
|-------|--------|
| Entitlements snapshot (Redis-ready) | ✅ |
| Permissions (distributed impl) | ✅ registered |
| Dedicated `commercial-limits:{id}` cache | ⏳ optional future |

## Tests

| Project | Status (2026-05-21) |
|---------|---------------------|
| `ERP.Infrastructure.Tests` (limits/entitlements + optional Postgres unified-doc) | ✅ 23/23 |
| `ERP.Domain.Tests` | ✅ 24/24 |
| `ERP.Application.Tests` | ✅ 190/190 (2026-06-05) |
| `ERP.API.Tests` | ✅ 33/33 SecurityTests (2026-06-05); integration suite stable |
| `ERP.Architecture.Tests` (NetArchTest + controller guardrails) | ✅ 30/32 — 2 pre-existing failures (Items module permissions pending plan catalog registration) |
| Frontend ESLint (`npm run lint`) | ✅ 0 errors (2026-05-21 remediation) |
| Frontend Vitest | ✅ 47/47 (27 purchase calc tests added 2026-06-24) |
| Frontend build | ✅ |
| Playwright smoke | ✅ PASS |
| Playwright enterprise E2E | 🟡 requiere API local; skip controlado sin backend |

### Sales commercial pipeline greenfield (2026-05-24)

| Item | Estado |
|------|--------|
| API: quotes (list/detail/create/approve/cancel), orders (list/detail/create/confirm/cancel/invoice) | ✅ |
| API: invoices (list/detail/validar/emitir/reintentar/anular) + permisos `sales.invoices.*` | ✅ |
| API: `DocumentRelation` (`QUOTE_TO_ORDER`, `ORDER_TO_INVOICE`) en detalle | ✅ |
| UI: `/sales/quotes`, `/sales/orders`, `/sales/invoices` + legacy redirects | ✅ |
| UI: trazabilidad cotización↔pedido↔factura; factura directa walk-in | ✅ |
| UI: filtros servidor en listado facturas; permiso `sales.credit-notes.send` | ✅ |
| E2E: `SalesCommercialPipelineEndToEndTests`, `SalesOrderInvoiceEndToEndTests`, `SalesCommercialCancelEndToEndTests` | ✅ |
| Tenants con perfil Facturador anterior al seed | 🟡 re-seed o migración manual de permisos `sales.quotes.*`, `sales.orders.*` |

Flujo canónico: **Cotización → Aprobar → Pedido → Confirmar → Factura → Validar/Emitir SRI**.

## MVP commercial (~85–90%)

**Done:** Core ERP operational flows, platform control plane, plans, multi-company foundation.

**Blocking / high priority:**

1. Validate SRI in `celcer.sri.gob.ec` with test certificate
2. Billing + retenciones UI gaps
3. Playwright enterprise E2E con API en CI (smoke ya verde)

See [ROADMAP.md](./ROADMAP.md) for prioritized backlog.

### Enterprise hardening — MasterData + security (2026-05-23)

| Item | Estado |
|------|--------|
| Explicit scope markers (`ICompanyScopedRequest` / CI AR-SEC-4) | ✅ |
| PostgreSQL unique violation → `Result.Conflict` (409) | ✅ |
| Testcontainers concurrency tests | ✅ (`Category=PostgreSql`) |
| Security metrics wired (refresh, 403, dual-write, namespace fallback) | ✅ |
| MasterData reconciliation (READ-ONLY) + health + Hangfire job | ✅ |
| SRI foundation (`SupplierProfile` retention defaults) | ✅ |
| Docs: [security/MULTI-TENANT-HARDENING.md](./security/MULTI-TENANT-HARDENING.md), [observability/METRICS.md](./observability/METRICS.md) | ✅ |

## Risks

| Risk | Mitigation |
|------|------------|
| Cross-company data leak | `CompanyScopeBehavior` + EF query filters |
| Production migration from old chain | Use baseline + planned data migration — never `DROP SCHEMA` in prod |
| Billing suspend without UI visibility | Entitlements snapshot exposes status; build `/saas/billing` |
| Test drift | Fix controller/DTO names before release gate |

## Quick start

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
```

First-run admin: banner en consola al arrancar API (`GET /api/setup/status` + `POST /api/setup/admin`, token-gated).

## Related

- [ROADMAP.md](./ROADMAP.md) — what’s next
- [DEVELOPMENT.md](./DEVELOPMENT.md) — how to contribute safely
