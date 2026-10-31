# Project Status

## P0 instalación limpia — READY FOR REVIEW (2026-10-31)

Base `main@d462cddde19a0b8196cf75089f388a4316dae2ad`, CI SUCCESS confirmado por el
propietario. Esta confirmación actualiza los pendientes de commit/CI de las entradas
históricas incluidas en ese commit (F-1..F-5 y corrección PR-7 `2e0977ec5`, entre ellas);
no acredita aceptación visual, E2E SRI ni nuevos cierres CLOSED. Se conservan abajo
las entradas originales como historial de la evidencia disponible en cada entrega.

Decisión del propietario para P0: conservar `PhysicalDocumentAuthorization` incremental,
sin consolidar la baseline ni modificar bases persistentes. Evidencia de ejecución:
[P0 instalación limpia](docs/evidence/p0-clean-install-20261031/README.md).

PASS agente: PostgreSQL 16 desechable vacío → ambas migraciones aplicadas, sin cambios
pendientes del modelo EF; 28 tablas GLOBAL y política física bloqueada preservadas.
Infrastructure 84/84, API 23/23 (arranque First Run y consulta operativa HTTP real),
Application 5/5, frontend público 4/4 y cuatro scripts oficiales del launcher PASS.
Sin error reproducido de instalación. Sin cambios de código ni bases persistentes,
emisión SRI, staging, commit o push. Pendiente gate/revisión del propietario; no CLOSED.

## F-1..F-5 — corrección final: captura acotada y Régimen General — READY FOR REVIEW (2026-10-30)

1. Numeración física: el rango autorizado se comprueba bajo el lock de Secuencias Documentales
   (sobrecarga `CaptureNextAsync(…, authorizedRanges)`, [extensión](docs/architecture/frozen-infrastructure.md#secuencias-documentales));
   prueba de concurrencia real PostgreSQL (12 consumidores por el último número: uno lo obtiene, ninguno lo supera;
   RED confirmado sin la comprobación).
2. Régimen General: una autorización registrada habilitaba la factura física ordinaria. Fila General → 01
   inactiva (BLOCKED por falta de fundamento oficial verificable); hoy ningún régimen emite en físico.
   Nota de venta 02 sigue no implementada. Migración incremental conservada (editada, no aplicada aún).
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push.

## F-1..F-5 — política fiscal de emisión física — READY FOR REVIEW (2026-10-30)

Política propietaria `IPhysicalEmissionRequirementsValidator` (Company), integrada en la apertura de caja,
Readiness y el gate de autorización de ventas antes de `CaptureNextAsync`; SSOT:
[Política fiscal de emisión física](docs/architecture/company-readiness.md#política-fiscal-de-emisión-física-f-1f-5).
Nuevos datos: `physical_document_authorization` y catálogo `global.sri_tax_regime_physical_doc_type`
(migración incremental `PhysicalDocumentAuthorization`; consolidación en la baseline a decisión del propietario).
UI: autorizaciones de impresión en Secuencias documentales. ElectronicDocuments sin cambios.
BLOCKED (fail-closed): factura física ordinaria del Régimen General (ver entrada superior), factura física RIMPE Emprendedor y cupo transitorio de preimpresos 2024.
Nota de venta 02 no implementada (sigue inactiva). La emisión física no queda certificada legalmente:
la autorización y su circunstancia excepcional no se verifican ante el SRI.

Evidencia agente (focal): Application 133, Infrastructure 62 + 151 (PostgreSQL real: ReadinessGap,
catálogos GLOBAL, objetos raw, secuencias), API 86 (E2E de caja/ventas/devoluciones/cobros con la
política física real), Architecture.Tests 143; frontend vitest 31 + 51, `tsc -b`, lint (0 errores), build,
`architecture:check` (140/140, gate PASS, 0 violaciones nuevas). Sin suites completas.
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push.

## CI 2B-3 — corrección focal PR-7 — READY FOR REVIEW (2026-10-30)

Clasificación: BUG REAL del gate en `598941f6f` (CI `41151151161`); KEEP comportamiento
y cobertura de [2B-3](docs/architecture/company-readiness.md#habilitación-preventiva-de-cajas-apertura-de-jornada).
Extracción literal de `SummaryCard` y columnas del detalle de cobros a `caja/components`, y de fixtures a
`useCajaPage.testFixtures.ts`, conservando setup, cleanup y cuerpos de pruebas.
PR-7: `CajaPage.tsx` 834/886 líneas no vacías; `useCajaPage.test.tsx` 1248/1390.
Sin cambios en límites, excepciones, ADR-047, reglas fiscales, backend ni contratos.

Evidencia agente: 72/72 pruebas focales, TypeScript, lint (0 errores; 37 advertencias),
build, architecture:check (140/140 fixtures; cero infracciones nuevas), guardrails PowerShell (árbol completo y chunk) y `git diff --check` PASS.
Gate del propietario desde `frontend`:
`npx.cmd vitest run src/modules/caja/hooks/useCajaPage.test.tsx src/modules/caja/hooks/useCajaPage.closingCounts.test.tsx src/modules/caja/pages/CajaPage.openingReadiness.test.tsx`;
`npx.cmd tsc -b --pretty false`; `npm.cmd run lint`; `npm.cmd run build`;
`npm.cmd run architecture:check`.
Desde raíz: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/architecture/check-architecture-guardrails.ps1`;
`git diff --check`.
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push;
F-1 a F-5 en la entrada superior.

## BUG REAL — diagnóstico de certificado SRI — IMPLEMENTADO — PASS FOCAL — PENDIENTE COMMIT/CI (2026-10-30)

`ValidateSriConfigurationQueryHandler` podía devolver `IsValid=true` con certificado
instalado y contraseña ausente: omitía el control de contraseña. La corrección agrega
el control obligatorio `certificatePassword=false` en ese caso; contrato vigente en
[Backend — diagnóstico de configuración SRI](docs/architecture/backend.md#diagnóstico-de-configuración-sri).

Evidencia del agente: RED de la regresión antes de corregir; GREEN 15/15 pruebas focales
del handler y aislamiento por empresa. `ValidateSriConfigurationQueryHandlerTests`
comprueba contraseña ausente con resolver real y almacenamiento sintético, y conserva
los casos de certificado válido, vencido, contraseña incorrecta y archivo no instalado.
Comando ejecutado desde la raíz:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidateSriConfigurationQueryHandlerTests|FullyQualifiedName~ElectronicInvoicingCompanyScopeTests" --verbosity minimal`.
`git diff --check` PASS. El PASS focal no sustituye el gate del propietario ni acredita E2E.

Sin cambios en firma, XML/XSD, SOAP ni transmisión al SRI; sin acceso a certificados
reales ni secretos, suites generales, migraciones, configuración operativa, staging,
commit ni push. Pendiente commit/CI; no CLOSED.

## BUG REAL — recuperación de notas de crédito — IMPLEMENTADO — PASS FOCAL — PENDIENTE COMMIT/CI (2026-10-30)

Una devolución autorizada con número definitivo de nota de crédito, pero sin
`ElectronicDocument`, no era recuperada automáticamente: `SalesElectronicDocumentRecovery`
descubría únicamente facturas. La regresión PostgreSQL
`Authorized_return_without_electronic_document_is_recovered_once_by_registered_sales_recovery`
reprodujo FAIL con 0 documentos recuperados, cuando se esperaba exactamente un CreditNote.
La corrección amplía el recuperador existente a notas de crédito elegibles mediante
`RegisterMissingAsync` y el mismo job; contrato vigente en
[Backend — ownership de recuperación](docs/architecture/backend.md#lotes-fairness-y-ownership).

PASS focal del agente: 21/21 pruebas PostgreSQL (regresión y casos A9 del recuperador)
y 1/1 prueba del job. La regresión comprueba exactamente un CreditNote en ambas
rondas, estado Draft, número y snapshots comerciales intactos. Se conservan
aislamiento, idempotencia, protección ante carreras y checkpoint; sin nuevos
secuenciales ni repetición de efectos comerciales. Sin cambios en el motor SRI ni
en el esquema de BD. Revisión final: APTO PARA GATE; no sustituye el gate del propietario.

Evidencia local: `artifacts/credit-note-recovery/sales-recovery-focal.trx` y
`artifacts/credit-note-recovery/sales-recovery-job.trx`. Las pruebas no certifican
transmisión real al SRI ni E2E comercial completo; el harness usa contexto fijo y
publisher vacío, y las carreras existentes cubren facturas. Sin suites generales,
staging, commit ni push. Pendiente commit/CI; no CLOSED. No cambia el alcance ni
el estado del caso #32 y no reabre otros módulos CLOSED.

## Compras — detalle compacto y resaltado — IMPLEMENTADO — PENDIENTE VALIDACIÓN VISUAL DEL PROPIETARIO (2026-10-29)

Clasificación: FALTA autorizada en la presentación; KEEP datos, edición, guardado,
reapertura y lógica operativa/fiscal. `PurchasesPage.tsx` reutiliza las tarjetas y
botones ZH existentes, con el patrón de expansión por identificador del modal de
pendientes: inicio contraído, Detalle individual y control alternante Expandir todo /
Contraer todo limitado a las líneas de la compra. La cabecera esencial permanece
visible; contraer solo cambia estado de UI. Estado efímero, reiniciado al abrir otra
compra. `purchases-invoice.css` aplica el token compartido `--color-surface-hover`
a la tarjeta y rail por hover/focus-within, sin sobrescribir el foco de los controles.

PASS agente: `PurchaseLineCard.compact.test.tsx` 18/18 (incluye inicio contraído,
expansión global/individual, aislamiento de otra tarjeta, foco conservado, líneas
sin mutaciones y remontaje contraído); TypeScript y diff --check PASS. ESLint focal
sin errores; advertencia existente de tamaño de `PurchasesPage.tsx` (max-lines).
Gate técnico del propietario desde `frontend`:
`npx.cmd vitest run src/modules/purchases/pages/PurchaseLineCard.compact.test.tsx`;
`npx.cmd tsc -b --pretty false`;
`npx.cmd eslint src/modules/purchases/pages/PurchasesPage.tsx src/modules/purchases/pages/PurchaseLineCard.compact.test.tsx`.
Pendiente revisión visual de hover, foco por teclado y expansión con compra real,
incluida edición/guardado/reapertura; las pruebas de componente no acreditan ese
recorrido persistido. Sin navegador, suites generales, backend, BD, migraciones,
baseline, caso #32, otros módulos, staging, commit ni push; no CLOSED.

## BUG REAL DISURGAL — corregido; validación funcional del propietario PASS DISURGAL y ALPINA (2026-10-29)

El rechazo reproducido provenía de StockMovement.Create: PurchaseEntry exigía costo > 0
sin reconocer las promociones XML legítimas. El repositorio valida evidencia de la compra
confirmada/detalle congelado y del origen fiscal antes de habilitar exclusivamente el cero
documentado; compras ordinarias a cero, costo desconocido y negativo siguen bloqueados.
Regla vigente: [Compras § 4](docs/architecture/purchases.md#4-confirmación).
RED: las dos variantes DISURGAL reprodujeron el mensaje exacto; GREEN Application 74/74,
Domain 13/13 y PostgreSQL desechable 2/2. La prueba real de confirmación conserva 13 líneas,
4 promociones, XML intacto, Kardex 473.77 y CxP/asiento balanceado 544.84; la compra ordinaria
con línea a cero revierte sin efectos persistidos. Compilación API con salida aislada PASS
(la salida habitual estaba bloqueada por la API en ejecución); architecture:check PASS, cero
infracciones nuevas, y git diff --check PASS. Sin cambios en bases existentes,
modelo EF, migraciones, baseline, caso #32, staging, commit ni push; sin E2E/navegador/suites generales.
Gate del propietario desde la raíz:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~ConfirmPurchaseHandlerTests|FullyQualifiedName~PurchaseXmlConfirmationGuardTests"`
`dotnet test backend/src/ERP.Domain.Tests/ERP.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~StockMovementValuationCostTests"`
`dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~Disurgal_promotions_confirm|FullyQualifiedName~Ordinary_zero_cost_purchase"`
El último comando requiere Docker/PostgreSQL desechable.
Validación funcional del propietario (DISURGAL `001-001-000005545`): PASS. Confirmación real
con 13 líneas y promociones del 100 %; inventario 858 unidades en 9 productos; Kardex e
inventario valorado $473.77; CxP pendiente $544.84; asiento N.º 1 Debe Inventario $473.77 +
IVA crédito tributario $71.07, Haber CxP $544.84; Libro Diario y Balance de Comprobación PASS;
diferencia fiscal de $0.01 conciliada al confirmar.
Validación funcional integral del propietario (ALPINA `012-004-004253387`): PASS. XML real con
4 líneas, IVA 0 % y total $16.15; confirmación PASS; inventario y Kardex 20 unidades en 4 productos,
$16.15; CxP una cuota de $16.15, sin pagos, saldo $16.15; asiento N.º 2 Debe Inventario $16.15,
Haber CxP $16.15; Libro Diario, Balance de Comprobación y Balance General PASS. Supera el bloqueo
de carga ALPINA registrado el 2026-10-28 (informe histórico conservado abajo).
Acumulado DISURGAL + ALPINA: inventario 878 unidades en 13 productos, $489.92; Debe/Haber
$560.99 / $560.99. El PASS cubre solo estos dos escenarios: siguen pendientes los demás
escenarios de Compras; Compras V1 no CLOSED.

## CI Infrastructure-01/06 Compras XML — fixtures alineados; pendiente gate del propietario (2026-10-29)

Reproducidos localmente los 3 FAIL del CI `40973768743` (PostgreSQL desechable). Sin BUG REAL de producción;
la corrección de promociones (entrada anterior) se conserva intacta.
- `Xml_service_without_global_warehouse_confirms_without_inventory`: fixture incorrecto; la línea de
  recepción no persistía su snapshot de impuestos y el XML no tenía `totalConImpuestos`.
- `Resolved_XML_saves_and_confirms_with_correct_base_stock_cost_and_Kardex`: fixture incorrecto; el XML
  sintético compartido (`BuildXml`) no tenía `totalConImpuestos`, una cabecera incompleta que
  [Compras § 2](docs/architecture/purchases.md#2-borrador) rechaza.
- `Draft_saved_before_a_correction_…`: expectativa desactualizada; `a55057a9c` retiró a propósito la
  exigencia de volver a guardar (el producto operativo puede diferir del vínculo XML). Decisión del
  propietario: alinear con la SSOT. El test pasa a llamarse
  `Correction_after_saving_keeps_the_saved_operational_product` (borrador sin reescritura, confirma con B y
  la recepción conserva C).
KEEP: guard de snapshot XML, conciliación fiscal, XML original y código productivo; solo cambian pruebas.
RED 0/3 → GREEN 3/3. Regresiones PASS: ambas clases de integración PostgreSQL 112/112 (incluyen promociones
DISURGAL) y Application focal 74/74; `git diff --check` PASS.
Gate del propietario (Docker):
`dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~ResolvePurchaseReceptionLinesIntegrationTests|FullyQualifiedName~PurchaseRetentionConfirmIntegrationTests"`.
Sin BD, migraciones, baseline, caso #32, Ítems, suites generales, E2E, navegador, staging, commit ni push; no CLOSED.

## CI Compras XML V1 — pruebas alineadas; pendiente gate del propietario (2026-10-29)

Reproducidos los 3 FAIL reportados para `217d81cd8` (CI `40969761501`, Backend application).
Clasificación: BUG REAL en expectativas obsoletas de pruebas; KEEP implementación fiscal V1.
La SSOT [Compras §2](docs/architecture/purchases.md#2-borrador) conserva importes fiscales
al editar cantidad/precio operativo y exige conciliación al confirmar. Se retiraron las
expectativas de IVA recalculado y Source=Calculated de esos tres casos, manteniendo
comprobaciones de edición operativa, procedencia XML y recepción original intacta.
Sin cambios productivos, de SSOT, BD ni migraciones. PASS agente: 3/3 corregidas y 76/76
focales (incluyen esas tres, recuperación, precisión/costos, guard de confirmación,
XML reales DISURGAL/ALPINA y desglose). Comando para el gate del propietario:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~PurchaseDraftRecoveryTests|FullyQualifiedName~PurchaseDraftExplicitCostAllocationTests|FullyQualifiedName~PurchaseXmlConfirmationGuardTests|FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~PurchaseXmlBreakdownTests"`.
Sin suites generales, E2E, navegador, staging, commit ni push; no CLOSED.


## Compras XML: identidad de lineas repetidas — pendiente validacion del propietario (2026-10-29)

BUG REAL: el merger emparejaba por posicion grupos de igual codigo/descripcion y conteo,
cruzando normales/promociones cuando cambiaba la enumeracion de la recepcion.
Los grupos repetidos ahora conservan el snapshot de cada PurchaseReceptionLineId y sus
PurchaseReceptionLineTaxId. KEEP: payload, lectura por ID, edicion operativa, XML inmutable
y conciliacion fiscal. Sin cambios de BD/EF/migraciones ni reparacion de borradores existentes.
PASS focal: backend 35/35 (incluye XML DISURGAL invertido y conciliacion 544.84), frontend
25/25 (guardar/reabrir las seis lineas normales/gratuitas en orden inverso).
Compilacion backend incluida en el test focal; TypeScript y diff --check PASS. Gate real PostgreSQL y funcional a cargo del
propietario; no CLOSED, sin staging, commit ni push. Comandos tecnicos:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~CreatePurchaseReceptionDraftHandlerTests"`
y desde frontend: `npx.cmd vitest run src/modules/purchases/hooks/usePurchasesPage.draftRecovery.test.ts`.

## Modal Editar empresa — IMPLEMENTADO — PENDIENTE VALIDACIÓN VISUAL DEL PROPIETARIO (2026-10-29)

BUG REAL de composición: campos sin `ZHGrid` y botones dentro del cuerpo, fuera del footer
oficial. Adaptado a `ZHGrid`, `ZHField`, `ZhTextInput`, checkbox compartido y `footer` de
`ZHModal`, con Guardar asociado al formulario y acciones deshabilitadas durante el envío.
Estilos globales existentes reutilizados, sin CSS nuevo ni cambios de negocio/API/permisos.
PASS focal: componentes 15/15, TypeScript, ESLint de los dos archivos afectados y compilación
frontend. Sin suites generales, E2E ni navegador. Validación visual real pendiente del propietario;
sin commit ni push. Contrato de composición: [modales](docs/architecture/modal-standard.md#formularios-dentro-del-modal).


## Compras XML V1 — IMPLEMENTADO — PENDIENTE VALIDACIÓN FUNCIONAL DEL PROPIETARIO (2026-10-29)

- Regla única vigente: producto, cantidad, presentación, bodega y costo operativo editables
  antes de confirmar, sin igualdad literal con las líneas XML. Se mantienen conversiones reales,
  conciliación fiscal por categoría/tarifa, CxP exacta y persistencia normal de los costos finales
  usados por inventario, Kardex y contabilidad. XML/recepción fiscal intactos;
  desglose reutilizado. Sin EF, baseline, migraciones, escrituras de BD ni cambios al caso #32.
- DISURGAL `001-001-000005545`: regresión con XML real, descuentos del 100 %, cantidades,
  presentaciones y costos editados: costo final 473.77, IVA 71.07, CxP 544.84; ambos snapshots
  originales del XML conservados (detalle base/IVA 473.78/71.06). No hay asiento ficticio.
- PASS técnico focal: Application 98 casos (97 + regresión nueva de documento duplicado),
  Domain 32 y frontend 94; compilación API y frontend, TypeScript, ESLint de archivos afectados,
  guardrails de capas/precisión/imports y diff sin errores. Los tests comprueban también el
  evento contable balanceado, la base retenible y las unidades reales de inventario de DISURGAL.
- Reglas y limitaciones: [SSOT Compras](docs/architecture/purchases.md#2-borrador).
  Pendiente el gate manual del propietario; no CLOSED. No se ejecutan suites generales,
  E2E, navegador, API ni PostgreSQL; no se repiten R1–R4. Sin staging, commit ni push.
- Sigue vigente el bloqueo de esquema ALPINA descrito abajo: esta corrección no modifica bases.


## Compras XML reales — antecedentes DISURGAL y bloqueo de esquema ALPINA (2026-10-28)

Revisión focal de DISURGAL `001-001-000005545` y ALPINA `012-004-004253387`;
no reabre el cierre histórico R2 ni continúa #32.

- **ALPINA — BUG REAL de esquema:** el log del POST de 18:37:49 (respuesta correlacionada
  `77767d8d-0cd0-4cbb-be8b-cf9b4ec33f0a`) muestra PostgreSQL `23502`, no duplicidad:
  `purchase_invoice_details.freight_allocated` recibe NULL. API efectiva en
  `localhost:5435/erp_zh_dev` (Development, user-secrets sin override de conexión).
  Inspección expresamente autorizada, mediante `BEGIN READ ONLY` y `ROLLBACK`: tanto
  `freight_allocated` como `other_costs_allocated` son `numeric(18,6) NOT NULL`, sin default;
  una fila existente, cero filas con importes distintos de cero en ambas columnas, sin
  dependencias de columna registradas en `pg_depend`. EF las ignora y la baseline/snapshot
  vigentes no las contienen. En aquella inspección, el historial EF de la base contenía dos registros;
  la migración nueva no aplicada fue retirada junto con su prueba de contrato por orden
  del propietario. Reparación SQL directa autorizada en `localhost:5435/erp_zh_dev`:
  respaldo completo `.local/alpina-backup/erp_zh_dev-before.dump` (ignorado por Git),
  verificado por lectura/descompresión con `pg_restore`, SHA-256
  `CDCF2F0172B14902B90BADCFC1501AC1F9F48094D0ADCA65738125531941584A`.
  Transacción con locks y guardas: ambas columnas solo ceros, sin NULL ni dependencias;
  eliminadas exclusivamente con `RESTRICT`, COMMIT confirmado. Conteos y huellas de
  226 tablas / 9570 filas idénticos antes/después, excluyendo solo las dos columnas.
  Verificación posterior: columnas ausentes, detalle existente conservado (1 fila),
  historial EF intacto (2 registros). Baseline, snapshot y migraciones anteriores intactos.
  Reintento del propietario: POST 201 a las 03:06:44.760Z; borrador ALPINA
  `739de567-9683-48c2-a499-36546854011e` persistido con 4 líneas. GET por id a las
  03:06:45.016Z falla con PostgreSQL `42P01`: falta `purchase_cost_distributions`
  (`PurchaseInvoiceRepository.cs:29`); el listado también falla (`:185`).
  El correlationId reportado `6d463e49-a677-4074-bbce-71d0ee50f9d2` no aparece
  literalmente en el log de archivo; asociación por hora y endpoint, no por id probado.
  Inspección PostgreSQL READ ONLY: faltan ambas tablas canónicas de distribuciones y
  las claves únicas compuestas de factura/detalle requeridas por sus FK. La baseline
  vigente sí las define; el respaldo previo tampoco contiene las tablas. BUG REAL de
  esquema independiente del DROP anterior. BLOQUEO: pendiente autorización expresa
  para reparación focal de esas tablas, claves e índices; sin nuevas escrituras de BD,
  cambios EF, migraciones ni código. Carga funcional todavía no PASS.
- **DISURGAL:** la nueva regla operativa y su conciliación están implementadas según la
  entrada vigente superior. Se conservan los fixtures firmados, el descuento exacto y la
  evidencia de diferencia cabecera/detalle; no se mantiene el bloqueo del guard anterior.

Gates focales previos: regresiones de XML reales/descuento/contrato de migración 7/7; Domain
`PurchaseInvoiceDetailMultiTaxTests` + `PurchaseInvoiceCostProrationConsistencyTests` 18/18;
build API PASS; `architecture:check` PASS (0 nuevas); `git diff --check` PASS. El test de
migración fue retirado con ella; corrección ALPINA validada en PostgreSQL real según lo
anterior. Sin suites generales, aplicación de migraciones, commit ni push. No CLOSED.
ALPINA continúa bloqueada en la carga por tablas ausentes según lo anterior. DISURGAL queda
pendiente de validación manual del propietario con la regla vigente de conciliación.

## Caso #32 — IMPLEMENTADO — PASS INTEGRACIÓN FOCAL — PENDIENTE E2E SRI (2026-10-30)

Validación integrada focal de recuperación y conciliación de respuestas inciertas del SRI.
Clasificación: **KEEP** implementación existente; cobertura de integración faltante incorporada.
Sin BUG REAL identificado ni cambios de código productivo en esta validación. Contrato:
[backend.md](docs/architecture/backend.md#límites-y-finalización); addendum ADR-023.

**PASS focal del agente:** 1/1 integración PostgreSQL en
`backend/src/ERP.Infrastructure.Tests/ElectronicDocuments/ElectronicDocumentUncertainSriPostgreSqlTests.cs`
y 16/16 pruebas relacionadas de `ElectronicDocumentIssuerRetryTests` y
`ReconcileElectronicDocumentCommandHandlerTests`. Evidencias:
`artifacts/case32/case32-integration.trx` y `artifacts/case32/case32-related.trx`.
La prueba verificó dos consultas de autorización con la misma clave, cero llamadas a Recepción,
conservación de `Signed` y conciliación pendiente; evidencia de conciliación persistida con
usuario, tenant, empresa y fecha, sin autorización ni rechazo manual. Aislamiento comprobado
entre tenants y empresas mediante repositorios y persistencia PostgreSQL reales.

**Validación técnica previa (no reejecutada en esta fase):** Domain ElectronicDocuments 52/52 (incluye Kernel/permisos 50/50 en otra
corrida); Application ElectronicDocuments + Retenciones 368/368 y catálogo de permisos +
conciliación 11/11; Infrastructure ED/Retención electrónica/A9 90/90; API Admin permisos + Monitor
11/11; Architecture 143/143; vitest Monitor/diagnóstico/NC 25/25 e i18n 15/15; `tsc -b`, ESLint
focal, build y `architecture:check` (0 violaciones nuevas). `ERP.API` y las pruebas Architecture/API
se compilaron con salida alternativa porque un `ERP.API.exe` ajeno a la sesión (PID 25680) bloqueaba
`ERP.API/bin`.

Limitación: la integración focal utiliza el SRI simulado; no acredita comunicación real con
los servicios del SRI ni E2E funcional. La E2E se agrupa con #20, #30 y #31; siguen pendientes
permiso efectivo/denegación, evidencia visible en el Monitor y reconsulta real contra el SRI de
Pruebas con ausencia de reenvío en el recorrido completo de Ventas/NC tras un fallo de envío.
El PASS del agente no sustituye el gate del propietario. Sin migraciones, staging, commit ni
push. **No CLOSED.**

## Caso #20 — IMPLEMENTADO — PENDIENTE E2E INTEGRADA (2026-10-28)

Anulación documental de cobros erróneos desde CxC, con permiso específico, selección,
confirmación y motivo obligatorio. **KEEP:** `ReverseCollectionCommand`, restauración de
CxC, locks, auditoría y traducción contable existentes. **FALTA autorizada implementada:**
API, permiso asignable, consulta/UI y precondiciones de seguridad operativa. Sin devolución
de dinero. Los históricos con destino caja/banco sin trazabilidad de origen se bloquean.
Reglas y límites: [receivables.md](docs/architecture/receivables.md#reversa-de-un-cobro).

**PASS técnico del agente:** Application 15/15 (handler de reversa, registro posterior correcto
y traductor existente); controller 7/7 (unitarias, sin servidor HTTP); modal 4/4 (jsdom).
Compilación backend mediante los proyectos de prueba, frontend `tsc -b`/build, lint
(0 errores, 34 warnings), `architecture:check` (0 violaciones nuevas) y `git diff --check` PASS.
Sin BUG REAL funcional acreditado por E2E en esta entrega; los rechazos nuevos se verificaron
unitariamente. Comandos focales desde la raíz:

```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --filter "FullyQualifiedName~ReverseCollectionCommandHandlerTests|FullyQualifiedName~CollectionReversedPostingTranslatorTests" --no-restore
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --filter FullyQualifiedName~FinancePaymentsControllerTests --no-restore
cd frontend
npx.cmd vitest run src/modules/finance/components/ReverseCollectionModal.test.tsx
```

Por ajuste expreso del propietario, **no se ejecutó E2E funcional de navegador, HTTP ni
PostgreSQL**. Pendiente para la cadena integrada: permiso efectivo/denegación, aislamiento
tenant/empresa, duplicados concurrentes, persistencia de motivo/actor/fecha, restauración de
cuotas y nuevo cobro, reversión contable/gate de período y ausencia de cambios operativos
en caja/banco (incluidos turnos cerrados e históricos bloqueados). Estas propiedades no se
acreditan con mocks. Sin modificaciones de bases, migraciones, repetición de R1–R4,
suites funcionales completas, staging, commit ni push. **No CLOSED**; queda pendiente el
gate integrado y la aprobación del propietario conforme al protocolo.

## R4 — Contabilidad — CLOSED (2026-10-28)

Validación UI/API/PostgreSQL en `erp_zh_test`, empresa B: #26 manual 25 y reverso 26
por 7.25; #27 Diario (62 líneas, 1820.15 por lado), Mayor (109 cuentas), Balance
y Resultados conciliados con SQL y el libro R1–R3; #28 fallo controlado de un gasto
de 2.00, configuración restaurada y reproceso UI a Posted (asiento 27, dos intentos),
readiness sin POSTINGS_UNRESOLVED; #42 doce períodos mensuales Open de 2027,
regeneración idempotente. Aislamiento API PASS. Resultado 96.43, activo 780.63,
pasivo 315.70 y patrimonio incluido resultado 464.93. Se conserva la anomalía
histórica de R3. Cierre 2026 bloqueado correctamente por ejercicio no terminado y
cierre anual pendiente; no se cerró ningún período. Acceso desbloqueado mediante
restablecimiento de solo e2e.admin expresamente autorizado. Sin BUG REAL reproducido
ni cambios de código, commit o push. El propietario aprueba **4/4 PASS** y autoriza
el cierre, limitado exclusivamente a #26, #27, #28 y #42 ejecutados.
Evidencia y límites:
[r4-accounting-e2e-01](docs/evidence/r4-accounting-e2e-01/README.md).

## R3 — Caja y ventas de contado — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con los datos de R1 y R2,
aprobado expresamente por el propietario: **6/6 PASS**. Escenarios: #17 venta de contado con vuelto, #19 devolución con reembolso en efectivo, #22
movimiento manual con motivo y asiento, #23 depósito bancario, #21 cierre con arqueo y diferencia, y #39
reporte de ventas conciliado con caja y contabilidad. Aislamiento entre empresas PASS. Evidencia:
[r3-cash-sales-e2e-01](docs/evidence/r3-cash-sales-e2e-01/README.md). Tres **BUG REAL** corregidos con
regresión focal RED → GREEN: (1) un turno abierto en otra empresa del tenant impedía abrir caja; ahora
hay un turno por TenantId + CompanyId (índice `ux_cash_sessions_open_per_company_user`, ahora
consolidado en `20261021162651_InitialProductionBaseline` y su Designer, sin incremental adicional;
EF Core y ModelSnapshot conservan el mismo índice). Comprobaciones técnicas focales de consolidación
PASS; estructuras de Compras preservadas. Instalación limpia pendiente del propietario, sin ejecución
sobre bases ni nueva validación funcional de R3; (2) depósito bancario, egreso manual y retiro permitían sobregirar la
caja; ahora aplican la misma regla que el pago a proveedor; (3) el arqueo mostraba «Total contado» $0.00
en la UI. SSOT: [backend.md § Turnos de caja](docs/architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro)
y [accounting.md §25](docs/architecture/accounting.md#25-caja-contable-accounting-12c). Gates focales:
PostgreSQL 10/10, Application 168/168, API HTTP 36/36, frontend Caja 108/108, lint/tsc/build,
`architecture:check` (0 nuevas) y `git diff --check`. Límites: no se valida la transmisión SRI (B sin
configuración SRI); no se probó con un segundo usuario; anomalía histórica E2E conservada: el depósito de
prueba de 100.00 previo a la corrección (asiento 20) permanece sin reversa bancaria, y el sobrante de 100.00 del
arqueo del turno 1 (asiento 21) solo repone el saldo contable de Caja. No acredita suite completa ni
despliegue. Sin commit ni push.

## R2 — Compras XML, retención, NC de proveedor, ajuste y transferencia — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con datos sintéticos y los
datos de R1, aprobado expresamente por el propietario: **5/5 PASS** en #14 compra desde XML
(vincular, crear pendiente, flete, confirmar y reabrir), #31 retención funcional con CxP neta 24.95,
#16 NC de proveedor con devolución parcial, #10 ajuste de egreso con anulación y #11 transferencia
entre bodegas; aislamiento entre empresas PASS. Evidencia:
[r2-purchases-inventory-e2e-01](docs/evidence/r2-purchases-inventory-e2e-01/README.md).
Dos correcciones con regresión focal RED → GREEN: (1) **BUG REAL** #16,
`AccountsPayable.ApplyReturnCredit` no recalculaba el estado y la CxP quedaba «Pendiente» tras una
devolución autorizada; (2) **H-1** autorizado, `CreateItemCommandHandler` crea la presentación base
de todo Product nuevo (Ítems, IL-1 y Resolver productos pendientes), conforme a
[items.md §5](docs/architecture/items.md#5-variantes-empaques-y-conversiones); los Products
históricos no se migran. Gates focales PASS: Domain 13/13 y 60/60; Application 387/387 (CxP/NC) y
562/562 (Items, InitialLoad, ItemMatching, PurchaseReception, Packaging); Infrastructure PostgreSQL
8/8 (devoluciones), 62/62 (`ConfirmItemsAtomicPostgreSqlTests` + `ResolvePurchaseReceptionLinesIntegrationTests`)
y 31/31 (ajustes, alcance de Items, códigos de proveedor y búsqueda); `architecture:check` (0
nuevas) y `git diff --check`.
Límites: el XML autorizado se sirvió desde un mock local del WS de autorización; la transmisión
electrónica de la retención no se acredita (se detuvo antes del XML por falta de dirección del
emisor) y nada se envió al SRI. La configuración SRI temporal de la empresa B se retiró al cerrar
(estado previo: sin configuración). El arreglo H-1 no se repitió en E2E. Diferidos sin bloquear R2:
H-2 (retención de otra empresa: 200 con `data: null`, sin fuga) y H-3 (transferencias sin listado ni
detalle); las observaciones UX del README siguen sin cambios. No acredita suite completa ni
despliegue. Sin commit ni push.

## R1 — Arranque: carga inicial, apertura contable y usuario limitado — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con datos sintéticos,
aprobado expresamente por el propietario: **5/5 PASS** en #37 IL-1, #6 IL-2/IL-3, #7 IL-4, #8
IL-5/IL-6 con apertura contable, cobro y pago parcial, y #3 usuario con perfil limitado. Evidencia:
[r1-initial-load-e2e-01](docs/evidence/r1-initial-load-e2e-01/README.md).
Dos **BUG REAL** corregidos con regresión focal RED → GREEN: (1) IL-1, el aviso de creación de
catálogo contaba avisos por fila («3 categorías y 3 marcas» cuando eran 1 y 1), en
`ImportWizardPage.tsx`; (2) CxC, «Fecha factura» de una CxC de saldo inicial mostraba la fecha de
carga en lugar de la emisión del documento, en `AccountsReceivablePage.tsx`. La corrección de CxC
fue autorizada expresamente y se limita a esa columna: las CxC con factura conservan la fecha y hora
de la factura. Con aprobación del propietario y conforme a ADR-034 (sufijo `At` solo para instantes),
el campo de fecha de negocio de la respuesta de CxC se renombró de `InvoiceIssuedAt` a
`InvoiceIssueDate` (`invoiceIssueDate` en JSON) en `SalesReceivableDto` y sus consumidores directos;
mismo tipo y valor; sin cambios de guardrails. Gates focales PASS: Vitest initialLoad + finance 82/82,
Application `SalesReceivable*` 9/9, Infrastructure `ValidateInitialReceivablesPostgreSqlTests` 28/28,
compilación de ERP.API, ESLint, `tsc -b`, build, `architecture:check` (0 violaciones nuevas) y
`git diff --check`.
Límites: no se ejecutó el cierre irreversible de la carga inicial (queda abierta y conciliada), ni la
liquidación total de saldos iniciales de CxC/CxP. No acredita suite completa ni despliegue. Sin
commit ni push.

## Ventas — E2E de devolución con crédito a CxC cobrada — CLOSED (2026-10-27)

Resultado E2E **14/14 PASS** en navegador y **23/23 PASS** en rechazos y aislamiento (37/37), con
verificación en PostgreSQL, aprobado expresamente por el propietario; CLOSED solo para la
devolución parcial con crédito a una CxC parcialmente cobrada. Sin BUG REAL ni cambios de código.
Evidencia: [sales-return-receivable-e2e-01](docs/evidence/sales-return-receivable-e2e-01/README.md).
Alcance: factura a crédito de un producto con IVA 15% por 115.00 en 2 cuotas; cobro parcial de
40.00; devolución parcial de 2 u (46.00) desde `/sales/returns` con crédito a CxC: CxC 69.00 /
40.00 / 29.00 con cuotas reconstruidas (14.50 + 14.50), Kardex +2 al costo de la venta, IVA 6.00
revertido, asientos de devolución y de costo balanceados, persistencia tras recargar. Rechazo de
crédito mayor al saldo, de exceso sobre el remanente y de anulación de la factura con devolución
autorizada; aislamiento frente a la empresa B. Límites: la emisión electrónica SRI de la Nota de
Crédito no queda acreditada (la empresa E2E no tiene configuración SRI) y las cuotas se reconstruyen
sobre el saldo conforme a la SSOT (lo cobrado queda en la CxC, no en las cuotas). Observación UX no
bloqueante: el listado de devoluciones muestra el GUID del cliente en vez del nombre; sin ticket ni
cambio de contrato. No acredita reembolso en efectivo o mixto, devolución total, mora, suite
completa ni despliegue. Sin commit ni push.

## Cuentas por cobrar — E2E de cobros a caja y sin destino — CLOSED (2026-10-27)

Resultado E2E **28/28 PASS** en navegador y **14/14 PASS** en rechazos, aislamiento y PostgreSQL,
aprobado expresamente por el propietario; CLOSED solo para el alcance descrito a continuación.
Sin BUG REAL ni cambios de código.
Evidencia: [receivables-e2e-03](docs/evidence/receivables-e2e-03/README.md).
Alcance: factura a crédito por 90.00 (1 cuota); cobro parcial de 40.00 en efectivo a «Caja
Principal» y cobro del saldo de 50.00 sin forma de pago ni destino (permitido por la SSOT), con
recarga y reapertura, asientos balanceados (Debe 1.1.01.001 Caja general en ambos: cuenta de la
caja y cuenta de la regla) y rechazo de caja ajena, de banco y caja a la vez y de cobros desde otra
empresa. No acredita mora, reversa de cobros, devolución con CxC cobrada, saldo inicial IL-5, suite
completa ni despliegue. Sin commit ni push.

## Cuentas por cobrar — E2E de factura con varias cuotas — CLOSED (2026-10-27)

Resultado E2E **58/58 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el propietario;
CLOSED solo para el alcance descrito a continuación.
Sin BUG REAL ni cambios de código de producción; se agregó un test focal de mora con varias cuotas.
Evidencia: [receivables-e2e-02](docs/evidence/receivables-e2e-02/README.md).
Alcance: factura a crédito por 300.00 en 3 cuotas con vencimientos distintos; cobro de 30.00 a la
cuota #2 (tras rechazar un monto mayor al saldo de la cuota), 120.00 sin cuota aplicado por FIFO y
liquidación de 150.00 sin cuota, con recarga y reapertura tras cada paso, «Vence» siguiendo a la
primera cuota no saldada, asientos de venta y cobro balanceados y aislamiento entre empresas. La
mora no se reprodujo en E2E (las fechas de los datos no lo permiten) y solo se acredita por test
focal. No acredita cobro a caja o sin destino, reversa de cobros, devolución con CxC cobrada, saldo
inicial IL-5, suite completa ni despliegue. Sin commit ni push.

## Compras/Gastos → CxP — E2E de reversa de pagos y anulación — CLOSED (2026-10-27)

Resultado E2E **46/46 PASS** (17 comprobaciones en navegador, 20 por API y 9 en PostgreSQL),
aprobado expresamente por el propietario. No se encontró BUG REAL ni hubo cambios de código. Evidencia:
[payables-reversal-cancel-e2e-01](docs/evidence/payables-reversal-cancel-e2e-01/README.md).
Se probaron una compra (115.00) y un gasto (80.00) a crédito, cada uno con un pago parcial por
transferencia. Con pagos activos, la anulación se bloqueó y no hubo cambios en la base. Luego se
reversaron los pagos desde «Reversar pago» del detalle del pago: las CxP recuperaron su saldo y se
generaron asientos inversos. Después se anularon compra y gasto: CxP y cuotas anuladas, kardex
revertido y asientos originales reversados. También se comprobaron las restricciones posteriores
(doble reversa, doble anulación y pago sobre una CxP anulada) y el aislamiento entre empresas. No
acredita pagos en efectivo o cheque, varias cuotas, retenciones, saldo a favor aplicado, suite
completa ni despliegue. Quedan dos DEBT no bloqueantes, documentados en la evidencia: «Reversado
por» muestra el UUID del usuario, y el bloqueo del gasto devuelve un código y un mensaje distintos
de los de la compra. No abren trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Compras → CxP → Pago — E2E de compra a crédito — CLOSED (2026-10-27)

Resultado E2E **40/40 PASS** (flujo, asientos y aislamiento), aprobado expresamente por el
propietario junto con las dos correcciones, tras corregir dos **BUG REAL** de
UI en Compras: la cuota regenerada mostraba y re-confirmaba un monto obsoleto (0.00), lo que dejaba
«Las cuotas no cuadran» y bloqueaba «Confirmar compra»; y la compra confirmada sin retención mostraba
la clave i18n cruda `purchases.retention.noneIssued`. Corrección focal en `PaymentScheduleSection`
(`PurchasesPage.tsx`) y en `es.json`/`en.json`, con tests focales. Evidencia:
[purchases-payables-e2e-01](docs/evidence/purchases-payables-e2e-01/README.md).
Alcance: compra a crédito de una cuota por 115.00, pago parcial 50.00 y del saldo 65.00 por
transferencia, reapertura de compra, CxP y pagos, asientos de compra y pago balanceados y
aislamiento entre empresas. No acredita compras de varias cuotas, retención, reversa de pagos,
bloqueo de anulación, suite completa ni despliegue. Los DEBT observados en la evidencia no abren
trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Cuentas por cobrar — E2E de cobros — CLOSED (2026-10-27)

Resultado E2E **21/21 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el
propietario, tras corregir un **BUG REAL**: el cobro no se acumulaba en la cuota de la CxC (cuota
`pending` con saldo cobrado). Corrección focal en `SalesReceivable`, `SalesReceivableInstallment`
y `RegisterCollectionCommandHandler`, con tests focales. Comportamiento: [SSOT de
CxC](docs/architecture/receivables.md); cobertura: [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md);
evidencia: [receivables-e2e-01](docs/evidence/receivables-e2e-01/README.md).
Alcance acreditado: factura a crédito de una cuota por 100.00, cobro parcial 40.00 y del saldo
60.00 con destino banco, reapertura de factura y CxC, asientos de venta y cobro balanceados y
aislamiento entre empresas. No acredita facturas de varias cuotas, cobro a caja o sin destino,
reversa de cobros (sin endpoint ni UI), devolución con CxC cobrada, saldo inicial IL-5, listado de
cobros (no existe), suite completa ni despliegue. No se reparan cuotas cobradas antes de la
corrección. Sin commit ni push.

## Pagos a proveedores — cierre E2E de gastos — CLOSED (2026-10-27)

Resultado E2E **17/17 PASS**, aprobado expresamente por el propietario. Clasificación:
**KEEP**, sin BUG REAL. Evidencia existente incorporada en
[supplier-payments-e2e-01](docs/evidence/supplier-payments-e2e-01/README.md)
([resultados](docs/evidence/supplier-payments-e2e-01/e2e-results.json) y seis capturas).
Alcance: gasto confirmado por 100.00, pago parcial de 40.00, reapertura del pago y de la CxP
con saldo 60.00, pago total del saldo y cartera sin pendientes. En la
[matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md) se acredita únicamente
el pago parcial/total de gastos.
No acredita pagos de compras, reversas, bloqueo de anulación, aislamiento entre empresas,
suite completa ni despliegue. Se conservan las observaciones y límites de la evidencia,
sin abrir pendientes ni reabrir módulos CLOSED. Cierre documental: sin cambios de código,
sin repetir pruebas y sin tocar el archivo RED histórico. Staging autorizado expresamente
para esta entrega; sin commit ni push.

## GOV-002 — CLOSED

Cierre documental del seguimiento de gobernanza aprobado expresamente por el propietario
(2026-10-27), sobre el contrato central implementado y las validaciones previamente acreditadas.
No certifica cumplimiento exhaustivo de todos los módulos ni acredita nuevas pruebas o despliegues.
Las dos deudas conocidas (códigos de módulo sin catalogar y formateador frontend alternativo)
se conservan como deuda no bloqueante en [error-handling.md](docs/architecture/error-handling.md#deudas-conocidas-y-límite-del-cierre).
No autoriza reauditar ni refactorizar módulos CLOSED. Esta fase solo modifica documentación;
staging autorizado por el propietario, commit y push no realizados.

## SEGURIDAD — SEC-06: revocación de permisos con caché — CLOSED — revisión técnica final PASS (2026-10-27)

La caché de permisos efectivos (TTL 300 s) podía seguir autorizando permisos ya revocados: la
entrada se publicaba con la versión vigente al escribir (una carga anterior a la revocación
quedaba válida), el cambio de perfil solo borraba la entrada (una carga concurrente del perfil
anterior la recreaba), el bump de versión era GET+1+SET (incrementos perdidos), los fallos de
invalidación se ignoraban y `UpsertCompanyUserMembership` podía guardar rol/perfil y salir sin
invalidar. Corregido en el mecanismo existente, sin cachés ni autorizadores nuevos: sello de
versiones (tenant, empresa, usuario) observado antes de cargar la BD, perfil dentro de la entrada,
versiones y marcas atómicas (Lua sobre el `IConnectionMultiplexer` del host; contadores del
proceso sin Redis), protocolo de invalidación en dos fases fail-closed en todos los handlers que
cambian permisos (503 `PERMISSIONS_INVALIDATION_UNAVAILABLE` sin persistir / 503
`PERMISSIONS_INVALIDATION_INCOMPLETE` con el alcance protegido), membership validada antes de
persistir y guardada en transacción, y regla de despliegue verificable `Redis:Required=true` para
más de una instancia de API. TTL sin cambios; sin cambios de base de datos ni migraciones.
Ajustes finales (opción A aprobada): una restauración o failover de Redis podía revivir versiones y
entradas anteriores a una revocación; ahora el Redis de autorización debe ser `noeviction`, sin
RDB/AOF y primario sin réplicas, y API/worker no arrancan si no cumple o no es verificable
(`CONFIG GET` + `INFO`); el Redis local de compose quedó declarado así (el contenedor existente
conserva la configuración anterior hasta recrearlo). Frontend: los dos 503 se presentan en las
pantallas IAM (perfiles, permisos, usuarios, configuración de usuario) sin éxito falso ni reintento
automático; el estado incierto recarga desde el servidor y deja la advertencia. Focales de los
ajustes PASS: Infrastructure 39/39 (worktree aislado; incluye Redis conforme/incompatible/sin
CONFIG/inaccesible, presión de memoria con `noeviction` y reinicio), API 21/21 (registro del
validador, contrato DevCache con Redis conforme e inaccesible), Vitest access 75/75, lint (0
errores), `tsc -b`, build, Arquitectura 143/143 y `architecture:check` PASS. Focales previos PASS: Application 214/214 (Access) + 161/161 (sesión, alta de usuario, scope),
Infrastructure (worktree aislado, sin el archivo RED) `PermissionsCacheRevocationTests` +
`PermissionsCacheRevocationRedisTests` 21/21 con Redis efímero, API 41/41 (regla de despliegue,
host, `CompanyAccessProfilesIsolationHttpTests`, `CrossCompanyAdminPrivilegeHttpTests`, contrato de
errores, sesión), Arquitectura 143/143, `architecture:check` PASS, tamaño de handlers PASS.
Gate independiente del propietario (PowerShell): Infrastructure `Security.PermissionsCache` 39/39
(worktree temporal sin el archivo RED), API Redis/hosts 14/14, Frontend Access 75/75, Architecture
frontend 140/140, `git diff --check` PASS. Revisión técnica final PASS. **SEC-06 CLOSED: commit `bbaa708cd`, CI #1011 SUCCESS.**
Sin despliegue: el Redis de producción (Coolify) no fue verificado contra el contrato. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-05: perfiles de acceso por empresa — CORREGIDO — revisión técnica final PASS (2026-10-26)

Los perfiles de acceso eran TENANT: un Admin de A podía consultar, editar, desactivar o cambiar
permisos de un perfil usado (o usado solo) por B, con efecto inmediato en B; un usuario Admin en A
y User en B podía autoescalarse en B editando el perfil de su membership de B; y un no Admin con
`access.company_user_memberships.view` podía asignarse rol Admin o un perfil más amplio que el suyo.
Decisión del propietario: perfiles independientes por `TenantId + CompanyId`. Corregido en el
mecanismo existente: `AccessProfile` es `ICompanyOperationalEntity` (unicidad por empresa, FK
compuesta membership → perfil de la misma empresa, esquema inicial y snapshot adaptados, sin
migración nueva ni cambios en bases existentes); los casos de uso de perfiles exigen empresa activa
(`IRequiresCompanyContext`) y resuelven el perfil por tenant + empresa; un perfil inactivo no
concede permisos; `UpsertCompanyUserMembership` y la revocación aplican anti-escalamiento con el
mismo bypass de SEC-04; el perfil por defecto "DataEntry" se siembra por empresa. Sin cambios de
contrato en frontend. Focales PASS: Application 213/213 (Access, sesión, `CompanyScopeBehavior`),
Arquitectura 143/143, API HTTP/PostgreSQL `CompanyAccessProfilesIsolationHttpTests` 12/12 +
`CrossCompanyAdminPrivilegeHttpTests` 8/8 y clases con fixtures de perfiles 103/103 + unitarias/
snapshot API 102/102, `architecture:check` PASS, `git diff --check` PASS. Infrastructure.Tests no
compilado en este árbol (archivo RED histórico). Gate independiente del propietario (PowerShell):
Application 22/22, API HTTP 20/20, compilación de ERP.API y coherencia EF/baseline PASS,
`git diff --check` PASS. Revisión técnica final PASS: aislamiento empresarial de perfiles aceptado
técnicamente. **CI del commit SEC-05: aún no ejecutado.** La revocación de permisos frente a la caché se
corrige en SEC-06. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-04: privilegios administrativos entre empresas — CORREGIDO — revisión técnica final PASS (2026-10-26)

`RuntimePermissionAuthorizer` concedía el bypass Admin (`*`) por el rol del JWT, emitido para la
empresa de apertura de sesión: un usuario Admin en A y User limitado en B podía, con el JWT de A y
`X-Company-Id: B`, ejecutar en B cualquier operación `perm:*` (y `GetMyPermissions` devolvía `*`).
Además, `CompanyContextProvider` sustituía en silencio un `X-Company-Id` sin membership por la única
membership del usuario. Corregido en el mecanismo existente: el bypass sale de la membership de la
empresa operativa u operador global (`IOperatorCompanyAccessPolicy`), alineando `GetMyPermissions`,
sesión, navegación y anti-escalamiento de `UpsertProfilePermissions`; un header sin membership activa
del tenant se rechaza con 403 `COMPANY_SCOPE_FORBIDDEN`, sin fallback. Sin cambios en tokens, first-run
ni base de datos. Focales PASS: Application 97/97 (incluye `CompanyScopedAdminAuthorizationTests`),
API HTTP/PostgreSQL 290/290 en las clases con host real (incluye `CrossCompanyAdminPrivilegeHttpTests`
8/8; fixtures de Caja, Ventas y BusinessPartner alineados: su JWT usaba un `sub` sin membership).
Regla funcional aprobada por el propietario: únicamente el Administrador Global de instalación
(modo operador) atraviesa tenants y empresas; los Administradores de Empresa quedan limitados a las
empresas donde su membership tiene rol Admin. Gate independiente del propietario (PowerShell):
Application 37/37, API HTTP 15/15, `architecture:check` 140/140, `git diff --check` PASS; revisión
técnica final PASS sin defectos bloqueantes. **CI del commit SEC-04: aún no ejecutado.** Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-03: aislamiento entre empresas en configuración y retenciones — CORREGIDO (2026-10-26)

13 operaciones COMPANY no declaraban marcador de scope y `CompanyScopeBehavior` no validaba la
membership de la empresa de `X-Company-Id`: un usuario de A podía leer/modificar el SMTP, la política
fiscal de Consumidor Final, las preferencias operativas y los defaults de facturación de B, consultar
el estado de facturación electrónica de B y generar/registrar comprobantes de retención con el
contexto de B. Corregido protegiendo los 13 requests con el marcador existente
`IRequiresCompanyContext` (Communications ×3, Retenciones ×3, OrgConfig ×2, política fiscal ×2,
preferencias operativas ×2, estado FE ×1); `INV_E2` alineado a los namespaces reales (`Purchases`,
`Caja`) y ampliado a esas operaciones. Evidencia: `CompanySettingsIsolationHttpTests` RED 7/7 (fallos
esperados sin la corrección); focales PASS: API HTTP/PostgreSQL 15/15, Application 49/49,
`RetentionsControllerTests` 16/16, Arquitectura 17/17 y `architecture:check`; gate independiente del
propietario en PowerShell 32/32 PASS. Revisión técnica final PASS y alcance de la corrección aceptado
por el propietario (2026-10-26). **Estado: SEC-03 corregido y técnicamente validado.** Sin evidencia
de CI para este cambio hasta publicar su commit. Riesgos conocidos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## INVENTARIO V1 — D-1: aislamiento por empresa de motivos de ajuste — CORREGIDO (2026-10-26)

`InventoryAdjustmentReason` admitía `CompanyId` nulo y se filtraba solo por tenant: una empresa podía
listar, editar, desactivar y usar motivos de otra del mismo tenant. Corregido como dato de empresa
reutilizando el filtro global y `CompanyScopeBehavior` existentes; baseline única actualizada
(`company_id` NOT NULL, índice único Tenant + Company + código), sin migración incremental. Focales PASS:
Application 106/106, API HTTP/PostgreSQL 21/21 (incluye `InventoryAdjustmentReasonCompanyIsolationHttpTests`),
OpenAPI 3/3, Arquitectura backend 31/31, Infrastructure 59/59, frontend 10/10 y `architecture:check`.
Revisión técnica final PASS y aceptación del propietario (2026-10-26).
**Estado: D-1 corregido y técnicamente validado.** Las bases de desarrollo ya migradas conservan el
esquema anterior hasta reinstalarse.
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario).

## INVENTARIO V1 — B4: Cuadre monetario Compras/Kardex/Contabilidad — CLOSED (2026-10-26)

12000 u × 0.1234 con 1.25 % de descuento e IVA 0: Compras y el asiento registraban 1462.29 y el Kardex
1462.30 (cantidad × `LandedUnitCost` redondeado); 3 × 100 + flete 10 prorrateado dejaba el Kardex en
309.99 frente a 310.00. Alcance: compras exclusivamente Product inventariable. El importe de
`TotalLineCost` se conserva entre Compras, Kardex y Contabilidad; la anulación revierte el importe
original persistido y un histórico ambiguo se rechaza en la transacción, sin efectos parciales. Sin
migración ni cambios en `PurchaseCostDistribution`. Compras mixtas Product + Service, ICE e IRBPNR
no forman parte de este cierre. Focales B4 en `PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs`:
6/6 PASS en CI #1001 (`infrastructure-01` 208/208 PASS, `application` SUCCESS). Revisión técnica final
PASS (2026-10-26), sin defectos reproducidos.
**Estado: B4 CLOSED; publicado en `8a2c3692d8fb5c870e2284e06832b60dc014cac5`.**
CI: [#999, #1000 y #1001](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — B3: importe monetario canónico del Kardex — CLOSED (2026-10-26)

3 u por 10.00 y tres ventas dejaban cantidad 0 con valor 0.01 (y la compra siguiente lo heredaba);
una transferencia del saldo dejaba 0.01 en origen; A2 y el reproceso de NC usaban importes sin escala.
Corregidos los residuos y unificado el importe operativo; reglas exclusivamente en la SSOT enlazada.
Sin migración ni revalorización histórica. Implementación alineada con la SSOT. Focales PostgreSQL:
`InventoryValuationPostgreSqlTests` 6/6 y reproceso 2/2 PASS. Gates del propietario PASS (Domain 33/33,
Application 1080/1080, Infrastructure 150/150, API 98/98, Arquitectura backend 143/143, Arquitectura
frontend PASS). Revisión técnica final PASS (2026-10-26), sin defectos reproducidos.
**Estado: B3 CLOSED; publicado en `8dd2f91e2fc1de4cc36e5cc227e3f8c4f29f6ac5`.**
CI acumulativo #999 y #1000 SUCCESS ([referencia](#inventario-v1--evidencia-ci-de-referencia)); no sustituye los gates focales.
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado).

## INVENTARIO V1 — Etapa 1: anulación con servicios y concurrencia de documentos — BUG REAL (2026-10-25)

B1: anular una compra Product + Service con bodega global fallaba ("El ítem no es un producto…");
corregida la selección de líneas inventariables. B2: ejecutar/anular ajuste y confirmar transferencia
concurrentes duplicaban Kardex y stock (5→10, 10→13, 6→2); corregida la concurrencia documental.
Sin migración. Gate del propietario aprobado: Application 137/137, Infrastructure 98/98,
API 71/71 y Arquitectura backend 143/143 PASS; revisión final del código PASS.
**Estado: B1/B2 CLOSED; publicados en `b16a412a0`, con gate y revisión final PASS registrados.**
CI acumulativo: [CI #999](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — Kardex y Reporte de Stock UX/UI (2026-10-25)

Publicados en `50a1af3d9`, sobre la base histórica `8a2c3692d`. Kardex y selector: 32/32 PASS
focales registrados; Reporte de Stock: 17/17 PASS. La excepción obsoleta de tamaño de Kardex
se retiró en `6c5c8c111`. Sin cambios funcionales de inventario.
**Estado: PUBLICADO; Kardex y Reporte de Stock aceptados por el propietario (comunicado 2026-10-26).**
La aceptación se apoya en la evidencia ya registrada; no se ejecutó un nuevo smoke autenticado ni
se obtuvieron evidencias nuevas.
Limitaciones de fixtures, fuentes e hidratación en las evidencias de
[Kardex](docs/evidence/kardex-ux-01/README.md), [selector](docs/evidence/kardex-ux-01/selector/README.md)
y [Stock](docs/evidence/stock-report-ux-01/README.md).
CI: [CI #999](#inventario-v1--evidencia-ci-de-referencia). El workflow frontend no ejecuta las
pruebas Vitest focales; CI no sustituye esos resultados ni el smoke autenticado.

Buscador de documento (2026-10-29): **IMPLEMENTADO — PENDIENTE VALIDACIÓN
VISUAL DEL PROPIETARIO**. FALTA autorizada: alternativa A.
Ajuste visual final: dos lineas agrupadas a la izquierda; numero destacado y badge
juntos arriba, tercero y fecha con separador discreto debajo. Padding compacto,
nombres largos con ajuste de linea; hover/foco y ancho del buscador conservados.
Solo presentacion local sobre ZHPickerResultItem/Badge; logica del hook intacta.
Cada sugerencia muestra numero completo, proveedor/cliente, fecha (formatDate) y
estado en espanol: Compras Confirmed/Cancelled (Confirmada/Anulada), Ventas
Authorized/Cancelled (Autorizada/Anulada). Draft y Discarded quedan excluidos.
KEEP seleccion por ID, busqueda parcial/completa manual (boton/Enter), scope
existente y desplegable ZHPickerResultItem/Badge con estilos compartidos.
Dos consultas mediante las fachadas existentes, filtradas por estado ANTES de
paginar: pagina 1, 10 por estado, maximo 20 sugerencias. Sin N+1, detalles de
facturas ni consultas a Kardex para sugerencias. Respuestas antiguas invalidadas
ante otra busqueda, cambio de texto/tipo/modo o desmontaje.
Limites: lista no exhaustiva; mas de 10 coincidencias por estado requieren precisar
el texto. El estado no acredita movimientos: documentos solo de servicios pueden
aparecer y devolver Kardex vacio. Sin ampliar backend ni alterar inventario.
PASS agente: 14/14 pruebas focales hook/pagina, TypeScript, ESLint focal y diff --check.
Gate del propietario desde frontend:
`npx.cmd vitest run src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.documents.test.ts`;
`npx.cmd tsc -b --pretty false`;
`npx.cmd eslint src/modules/inventory/kardex/pages/KardexPage.tsx src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.ts src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.documents.test.ts`.
Pendiente gate y validacion visual/funcional con datos reales. Mejoras pendientes de
Compras intactas. Sin suites generales, E2E, backend, BD, migraciones, baseline,
caso #32, staging, commit ni push; no CLOSED.

## INVENTARIO V1 — Evidencia CI de referencia

Referencia publicada: `6c5c8c1113f802a0f3ab2a9d795c261d0c8c05ab`.
CI #999, segundo intento: **SUCCESS, 19 PASS, 0 FAIL, deploy SKIPPED**, verificado en GitHub
por el propietario y aportado para esta corrección documental (2026-10-26). Es evidencia
acumulativa de la referencia, no una ejecución individual atribuida a cada commit anterior.
No acredita gates del propietario pendientes ni un despliegue.

Referencia publicada: `ba06799ac99b883bcca289d914c69f4cb261f672`.
[CI #1000](https://github.com/szhinin89/erp-saas/actions/runs/40322826093), intento 1:
**SUCCESS, 19 PASS, 0 FAIL, deploy Azure SKIPPED**. Resultado acumulativo del pipeline;
no sustituye los gates focales ni acredita un despliegue.

Referencia publicada: `fa7b2d842b457c7d2fbe2faef8d065cd01ecf4da`.
[CI #1001](https://github.com/szhinin89/erp-saas/actions/runs/40327986902): **SUCCESS**;
`backend / Backend (infrastructure-01)` 208/208 PASS, con las seis pruebas B4 de
`PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs` identificadas individualmente como PASS
en los logs; `backend / Backend (application)` SUCCESS. Acredita solo las pruebas que ejecutó el
pipeline; no acredita un despliegue.

Referencia publicada: `fce60ea1d52614fee5ce0f84c231df2643b18485`.
[CI #1002](https://github.com/szhinin89/erp-saas/actions/runs/40357020744), intento 1: **SUCCESS**.
Resultado acumulativo del pipeline; no acredita un smoke autenticado, una aceptación del
propietario ni un despliegue.

## Estado de entrega

Preproducción. Actualización documental: 2026-10-26. Este archivo conserva resultados
ya registrados; no acredita nuevas ejecuciones ni despliegues. Las aceptaciones del
propietario se registran solo donde se indican expresamente.
Reglas funcionales únicamente en las [SSOT](docs/architecture/README.md).

| Entrega | Evidencia registrada | Estado / límite pendiente |
|---|---|---|
| Compras: distribución canónica de costos A/B | Publicado en `29a69fcaa`; Backend Domain 34, Application 99, API 8, PostgreSQL 20 + 5 y arquitectura PASS; frontend implementado; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. `PurchaseCostDistribution` es la única autoridad de distribución ([SSOT Compras](docs/architecture/purchases.md)) |
| Compras: precisión de prorrateo/base cero | Publicado en `617253d38`; reproducción 6/6 FAIL; focal 9/9 y PostgreSQL 8/8 PASS en [evidencia](docs/evidence/purchases-cost-precision-01/README.md); revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. El frontend actual no calcula el reparto: usa el resultado del backend. El simulador frontend y su 178/178 pertenecen a una implementación sustituida en `29a69fcaa`; no son pruebas vigentes del cálculo |
| Compras XML: confirmación atómica y Service sin bodega | Publicado en `10afb8425`; PostgreSQL 7/7, Application 66/66, Domain 30/30; guardrails backend PASS; revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. No resuelve la integración operativa futura de ICE mixto ni IRBPNR |
| Frontend: timeouts de Compras/Contabilidad | Publicado en `746a1f1c6`; focales, TypeScript, ESLint, build y arquitectura PASS; suite frontend 3252/3252 PASS según [evidencia](docs/evidence/frontend-timeouts-01/README.md); el CI frontend no ejecuta Vitest | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. Limitación independiente vigente: en la suite, `ZHOverlayGeometry.test.tsx` falló en `afterAll` y pasó 20/20 aislado; no corregida |
| Frontend: cuatro pruebas desactualizadas de Caja/Contabilidad | 3248/3248 PASS, TypeScript, lint, build y arquitectura; [evidencia](docs/evidence/frontend-preexisting-fails-01/README.md) | Corrección verificada en la entrega previa; sin nueva ejecución |
| Ítems: E2E visual Product/Service | Chromium 48/48 PASS sobre `erp_zh_test`; 2 BUG REAL corregidos (precio base perdido al reabrir; 13 claves `items.validation.*` sin traducir); `PricingTab` 17/17, lint, `tsc -b`, build y arquitectura PASS; [evidencia](docs/evidence/items-e2e-visual-01/README.md) | Implementado y verificado 2026-10-27; sin commit; suite completa no ejecutada |
| Ítems: formularios y catálogos asíncronos | Reproducción 4/4 FAIL; editor 32/32, frontend 313/313, Application 446/446 PASS; [evidencia](docs/evidence/items-forms-alignment-01/README.md) | Guardar/reabrir en ERP real verificado en [E2E visual](docs/evidence/items-e2e-visual-01/README.md) |
| Compras: conflictos de códigos | Frontend 767/767 y gates técnicos PASS; [evidencia](docs/evidence/purchases-code-conflicts-sync-01/README.md) | Validación previa, sin acreditar otro gate |
| Compras: avisos compactos de duplicados | Frontend 763/763, focal 37/37 PASS; [evidencia](docs/evidence/purchases-compact-code-conflicts-01/README.md) | Validación previa; capturas con límites documentados |
| Ítems: diagnóstico unificado de códigos | Application 423/423, PostgreSQL 22/22, frontend 755 y arquitectura 143/143 PASS; [evidencia](docs/evidence/items-code-conflicts-01/README.md) | Implementado; sin nueva ejecución |
| Compras: Internos provisionales | Application 63/63, frontend 86/86, PostgreSQL 11/11 PASS; [evidencia](docs/evidence/purchases-internal-barcodes-01/README.md) | Implementado; sin decisiones pendientes en esa entrega |
| Compras: desglose XML | Commits 478f5cc0b / 77f80985f; Domain 59, Application 121, frontend 77 y objetos SQL 2/2 PASS | Implementado; sin acreditar despliegue |
| Compras: vinculación estricta por proveedor | dc643c455; PostgreSQL 6/6, Application 100/100, selección manual 6/6 PASS | Implementado para la conciliación de recepción/código de proveedor; no exige que el producto, costo o cantidad operativos coincidan con el XML. Rige la regla única de Compras V1 indicada al inicio |
| Compras: productos pendientes compactos | 77f80985f; frontend 589/589 y PR-7 PASS; [evidencia](docs/evidence/purchases-pending-products-compact-01/README.md) | Implementado; excepción del modal retirada |
| Compras: modal Distribuir | Focal 16/16 y gates PASS; [evidencia](docs/evidence/purchases-distribute-cost-01/README.md) | Implementado; no amplía cálculo |
| Compras: validaciones comerciales | Focal 368 y gates PASS; [evidencia](docs/evidence/purchases-commercial-risk-01/README.md) | Falta decisión sobre precisión del costo mostrado; no se resuelve en esta consolidación |
| Design System: incidencias de línea | [Evidencia](docs/evidence/zh-ds-line-issues-01/README.md) | Capturas con límites de fuentes/iconos; sin nueva aceptación visual |
| Unificación de NC de compra | PostgreSQL 21/21 con SRI simulado + contabilización 6/6, API 24/24, Application 79/79, frontend 35/35; build/lint/TypeScript/arquitectura PASS | **CLOSED (2026-10-27)**; aceptación funcional expresa del propietario para el alcance implementado de [Compras §§6–7](docs/architecture/purchases.md#6-nota-de-crédito-del-proveedor--entrada-única), publicado en `51678b165`. Recuento frontend conciliado en [evidencia SSOT](docs/architecture/purchases.md#9-evidencia). El límite histórico externo de Gastos (516 > 514) ya no se observa: conteo puntual actual 514, techo 514; no acredita un nuevo gate completo. Sin nuevas pruebas, validación SRI real ni despliegue. |
| Anulación completa de compra | Validación focal PostgreSQL PASS | Implementada; calendario oficial y ventana interna Company siguen fuera del comportamiento operativo |

## Instalación inicial y GLOBAL

GREENFIELD-INSTALL: instalación única validada manualmente por el propietario en
`erp_zh_dev` (25/224/1048 INEC 2026 y SRI cargados). Oráculo PostgreSQL desechable:
212 tablas equivalentes salvo metadatos de procedencia/orden de columnas.
Infrastructure focal 256/256 y Architecture 2/2 PASS; suite previa 7371/7372,
snapshot OpenAPI corregido y focal 3/3 PASS. Regresión de arranque INEC 6/6 PASS.
No implica despliegue ni vuelve a autorizar recrear bases.
Instalación y pendientes normativos: [GLOBAL](docs/architecture/architecture.md#global-immutable-schema-global)
y [First Run](docs/architecture/first-run.md).
GLOBAL OFFICIAL-ALIGNMENT: Infrastructure 190/190, API 9/9, frontend 2/2 PASS;
ICE mixto representado en catálogo (65/65 PASS), sin integración de cálculo operativo.
Corrección Tabla 5: PostgreSQL 5/5, HTTP 1/1, frontend 46/46 PASS.

## Background Processing y despliegue

Implementación validada de catálogo/runner/locks/colas/host separado. Resultados de
los grupos originales solapados: 30/30, 19/19, 1/1, 20/20, 39/39 y 12/12 PASS;
no sumarlos como pruebas únicas. Aceptación posterior: API 20/20 e Infrastructure 4/4
con PostgreSQL 16 y SMTP simulado. La consulta de solo lectura del entorno anterior
observó siete recurring jobs, checkpoint durable y cero workers activos; no prueba
entrega SMTP/SRI ni un host local funcionando.
Fase D: Compose 4/4, focal 21/21 y PostgreSQL 4/4, imagen Linux y smoke Compose aislado PASS.
El arranque local sobre datos existentes no quedó acreditado; la aceptación aislada no lo sustituye.
Transición Coolify real pendiente de inspección, respaldo y autorización de operación.
No se instruye aplicar migraciones sustituidas. [Runbook](docs/deployment/README.md).
Collector del worker y ajuste de carga real pendientes de operación; no existe benchmark de producción.

## Contabilidad y Retenciones

Accounting: capacidades implementadas de continuidad 01–06, períodos 07, manuales 08,
reglas/reportes 11, ajustes 12A, caja 12C y cierre anual 12D documentadas en
[Accounting](docs/architecture/accounting.md). Son capacidades implementadas, no nuevos cierres.
Corrección de finalización SRI por job (2026-10-20): PostgreSQL focal 7 PASS registrados;
contabilización pendiente/fallida no equivale a anulación operativa fallida.
Retenciones: emisión, XML/RIDE, transmisión y anulación asistida implementados en el
alcance de [Backend](docs/architecture/backend.md); no quedan como tareas futuras genéricas.

## A3–A8 — CLOSED (2026-10-27)

Cierre documental y funcional aprobado expresamente por el propietario, con gate
independiente A5 **10/10 PASS en PostgreSQL real** comunicado en esta fase. Se conservan
los PASS ya acreditados de los demás casos, sin repetir pruebas.

| Caso | Evidencia focal acreditada | Estado |
|---|---|---|
| A3 — actualización atómica del borrador de venta | Application 24/24 + PostgreSQL 1/1 PASS | CLOSED |
| A4 — ICE/IRBPNR en devoluciones de venta | Application 7/7 PASS | CLOSED |
| A5 — búsqueda y selección por barcode de presentación | RED 2/2 FAIL esperados → GREEN PostgreSQL 10/10 PASS; gate independiente del propietario 10/10 PASS | CLOSED |
| A6 — guardas de bodega y carreras UI | Application 10/10 + PostgreSQL 6/6 PASS; carreras frontend PASS en el grupo indicado debajo | CLOSED |
| A7 — concurrencia de autorización de ventas | PostgreSQL 6/6 PASS | CLOSED |
| A8 — retry/idempotencia de autorización | PostgreSQL 13/13 + HTTP/PostgreSQL 3/3 PASS | CLOSED |

Frontend focal agrupado previo: 113/113 PASS (incluye autoselección de presentación y
carreras de búsqueda/bodega); no sumarlo como ejecuciones independientes por caso.
A5 conserva el fix de una línea y sus cuatro casos de regresión en
`InvoiceItemSearchRepositoryTests`; evidencias [RED](docs/evidence/a5-barcode-search-01/red.trx)
y [GREEN](docs/evidence/a5-barcode-search-01/green.trx). Contrato funcional únicamente en
[Backend](docs/architecture/backend.md#búsqueda-por-barcode-de-presentación-a5).
Validación del agente en copia temporal sin leer/copiar/compilar el archivo RED histórico;
revisión focal del diff y `git diff --check` PASS. Esta consolidación no ejecuta pruebas,
no acredita despliegue y no modifica otros cierres. Commit y push no realizados;
el propietario conserva su ejecución. CLOSED: no reauditar salvo BUG REAL relacionado.

## Cierres acreditados y límites

| Ámbito | Estado previamente registrado |
|---|---|
| A1 Ítems | CLOSED, 43cce57a9 |
| A2 costo negativo | CLOSED, 64e1b21ca |
| B1/B2 Inventario | CLOSED; evidencia en los bloques precedentes |
| First Run | Cerrado; SSOT en first-run.md |
| QA-INVENTORY-INFRA-SUITES-01 | CLOSED, 509ac6d75; PostgreSQL 39/39 PASS |
| Compras/Gastos/CxP/Pagos, cierre 2026-09-12 | LISTO / CERRADO para ese alcance; [matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No cierra ampliaciones posteriores ni Accounting global |
| CxC y cobros, cierre 2026-10-27 | **CLOSED** para el alcance de la sección precedente; [SSOT](docs/architecture/receivables.md) y [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md) |
| Pricing, BP, ElectronicDocuments e infraestructuras FROZEN | Conservan sus cierres y límites; [infraestructuras](docs/architecture/frozen-infrastructure.md) |
| A9 recovery comercial | **CLOSED (2026-10-27)**; cierre funcional aprobado expresamente por el propietario, exclusivamente para el alcance create-only de [Sales recovery](docs/architecture/backend.md#lotes-fairness-y-ownership). Evidencia PASS existente: PostgreSQL 19/19, guard 1/1, job 1/1, issuer 31/31, arquitectura 143/143 y gates técnicos. No incluye envío, reenvío ni nuevas funcionalidades; no acredita nuevas ejecuciones ni despliegue. |

A3–A8 quedan acreditados por el cierre aprobado en su sección precedente; la discrepancia
documental anterior queda resuelta, sin ampliar el alcance de A9.
AUTH sensitive logging: fix focal 21/21 PASS; sin modificar autenticación ni persistencia.
No usar resultados anteriores como evidencia de que el árbol actual pasó una suite.

## Riesgos y limitaciones vigentes

Registrados sin autorización de implementación; no constituyen trabajo abierto.

- **Aprobación documental:** elegir `SingleStep`/`MultiStep` en Documentos y flujos bloquea
  la confirmación de Gastos, porque no existe flujo de aprobación. Contrato:
  [security.md](docs/architecture/security.md#permisos-vs-política-de-flujo-documental-document-flow-policy-01).
  La aprobación es intención futura en [ROADMAP](docs/ROADMAP.md#etapa-1--completar-erp-core).
- **Comprobantes electrónicos sin respuesta concluyente (Caso #32, PASS integración focal; pendiente E2E SRI):** se
  consultan, nunca se reenvían; la conciliación manual solo registra evidencia y reconsulta.
  Un comprobante que el SRI nunca recibió queda en "Requiere conciliación" sin camino de
  reenvío. Sigue sin decisión el tratamiento de comprobantes de retención anteriores al ciclo
  actual cuyo origen ya fue anulado (antes O-1/O-9): admiten evidencia, no consulta.
  Contrato: [backend.md](docs/architecture/backend.md#límites-y-finalización).
- **Logs de autenticación anteriores a la corrección de AUTH sensitive logging:** pueden
  contener el campo `successorHash` del evento de rotación de refresh token. No se acredita
  exposición ni incidente; su revisión o depuración depende de la operación de cada entorno.
- **Aislamiento entre empresas — límites conocidos tras SEC-03** (contrato:
  [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant)):
  `UpdateCustomerRoleConfigValidator` (request TENANT) comprueba códigos de catálogo contra la
  empresa del contexto; `CreateCompanyUserPreferences` es un consumidor interno sin marcador de
  empresa; `INV_E2` no cubre todos los módulos ni detecta un marcador TENANT puesto por error.
  Tras SEC-04 la resolución de permisos ya no usa la única membership cuando
  el header no tiene membership; la firma `IRuntimePermissionAuthorizer.IsAuthorizedAsync`
  conserva el parámetro `role`, sin efecto. Los perfiles de acceso son por empresa desde SEC-05.
  Impacto acreditado: como máximo, el validador revela si un código existe en el catálogo de otra
  empresa; no se acredita lectura ni escritura de sus datos.
- **Seguridad — revocación de permisos con caché (SEC-06):** garantías y
  contrato en [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).
  Límites: (1) cumplir "Redis obligatorio con varias instancias" depende de que el despliegue declare
  `Redis:Required=true`; el repositorio no conoce la cantidad real de instancias. (2) Si Redis
  desaloja una marca de invalidación en curso (política de memoria `volatile-*`/`allkeys-*`) mientras
  Complete falló, la protección de ese alcance termina antes de los 900 s — excluido por el
  contrato `noeviction` verificado al arrancar. (3) En el alta de usuario
  (transacción ambiente) la invalidación se completa antes del commit externo; el usuario es nuevo y
  no tiene entradas previas. (4) La caché de navegación se invalida solo en el proceso local (no
  autoriza). (5) Escrituras de caché con más de 60 s entre la observación del sello y la escritura se
  descartan (más lecturas a BD, nunca más permisos). (6) Garantías solo operativas: no cambiar la
  configuración de Redis en caliente, no restaurar respaldos ni promover réplicas, monitoreo de
  memoria y alertas de disponibilidad; con `noeviction` y memoria llena los cambios de permisos
  responden 503 sin guardar. (7) Un Redis gestionado que bloquee `CONFIG` impide el arranque hasta
  aprobar una verificación equivalente. (8) El Redis de producción (Coolify) no está inspeccionado:
  si no cumple el contrato, la API no arrancará tras desplegar este cambio.

## Consolidación documental

2026-10-26: edición autorizada de documentos e índice ADR, sin lógica, pruebas funcionales,
base de datos, migraciones, capturas o logs. Publicada en `ba06799ac99b883bcca289d914c69f4cb261f672`;
[CI #1000](#inventario-v1--evidencia-ci-de-referencia) SUCCESS.
Trabajo abierto: [WORK_QUEUE](docs/agent/WORK_QUEUE.md). Futuro: [ROADMAP](docs/ROADMAP.md).
