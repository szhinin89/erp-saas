# Sustentos tributarios — BUG REAL P1 (2026-10-20)

> **HISTÓRICO (archivado 2026-10-21).** No es fuente vigente. La definición vigente de GLOBAL está en
> [docs/architecture/architecture.md § GLOBAL IMMUTABLE](../../architecture/architecture.md#global-immutable-schema-global).

Implementado para revisión, sin commit/push ni aplicación a desarrollo o producción.

## Fuente y alcance

Fuente: [SRI, Anexos y guías](https://www.sri.gob.ec/web/intersri/formularios-e-instructivos1), Catálogo ATS actualizado 06/08/2026.
Archivo: [Catalogo_ATS.xls](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/e6a826af-b22c-40bb-8752-d711f293b8f9/Catalogo_ATS.xls), hoja `TABLAS REFERENCIALES`, Tabla 5.
SHA-256 verificado en el diagnóstico: `bd3f7834f2cd31187af39cd2f4c685a646d7e49316e92ee493da5f2dd9776e3e`.
Se conserva la descripción literal, eliminando únicamente espacios exteriores de las celdas.
06 = Inventario - Crédito Tributario para declaración de IVA.
07 = Inventario - Costo o Gasto para declaración de IR.

- BUG REAL: seed global incorrecto, replicado por el baseline y por PostgreSQL local.
- KEEP: claves, códigos, API, repositorio y consumidores existentes. `IsActive` solo gobierna selección; no se añade filtro global.
- BUG REAL: corregidos nombres 01–15 y selección de 00/16–20. Se preservan nombres históricos 16–20.
- DO NOT TOUCH: documentos, defaults y snapshots existentes; módulos funcionales CLOSED/FROZEN.

## Migración y compatibilidad

`20261021031200_SriTaxSupportAtsTable5Correction` es incremental, sin cambios a `InitialProductionBaseline`.
Actualiza únicamente `name` de 01–15 e `is_active` de 16–20. Incorpora 00 inactivo si falta; si existe conserva su nombre y solo lo desactiva (upsert por clave).
`ValueGeneratedNever` aplica el patrón de ADR-042 para conservar `false` con DEFAULT true.
No borra ni renumera filas, ni modifica tablas funcionales. `Down` rechaza el rollback automático: una reversión fiscal requiere otra migración revisada, sin borrar 00 ni reintroducir silenciosamente el catálogo incorrecto.

## Inventario antes de migrar

Destino consultado: contenedor local `postgreszh`, PostgreSQL 16, `erp_zh_dev`, transacción READ ONLY.
Compra: código 02, estado 1 (Draft), una fila. Gastos y Retenciones: cero filas. Proveedores: tres defaults nulos.
No se migró esta base. Piloto/producción: no consultados ni modificados.
Antes de desplegar en cualquier destino se debe repetir el inventario READ ONLY por código/estado sobre:
`purchase_invoices.tax_support_code`, `expense_documents.tax_support_code`, `master_bp_supplier_configs.default_tax_support_code`, `retention_documents.source_document_tax_support_code`, y revisar defaults/borradores afectados sin remapearlos.
Corregir etiquetas no permite inferir la intención tributaria de quien seleccionó una etiqueta errónea.

## Verificación focal

- PASS 5/5 Infrastructure: exactitud completa Tabla 5; instalación nueva con todas las migraciones; upgrades con y sin 00 preexistente; conservación de referencias FK PostgreSQL para todos los códigos/consumidores/estados de prueba; guard de operaciones de migración limitado al catálogo; persistencia real de false por EF.
- Las referencias del upgrade se prueban mediante una tabla de sondeo FK aislada, no creando documentos comerciales. El guard de operaciones y diff verifican que la migración no escribe tablas comerciales.
- PASS HTTP 1/1: endpoint autenticado real sobre PostgreSQL aislado devuelve solo 01–15 y las etiquetas correctas de 06/07.
- PASS frontend: Gastos y Proveedores (26 pruebas), Compras (20 pruebas); prueban consumo de etiquetas globales y conservación de un código guardado en Compras. Hubo un fallo inicial por import faltante en la prueba nueva, corregido.
- Sin suites completas. Migraciones ejecutadas exclusivamente en contenedores efímeros de pruebas.

- PASS TypeScript `tsc -b`; lint focal y `git diff --check` sin errores.

## Archivos de la entrega

- `backend/src/ERP.Infrastructure/Persistence/Configurations/SriCatalogs/SriTaxSupportConfiguration.cs`.
- `backend/src/ERP.Infrastructure/Migrations/20261021031200_SriTaxSupportAtsTable5Correction.cs`, su `.Designer.cs` y `ErpDbContextModelSnapshot.cs`.
- `backend/src/ERP.Infrastructure.Tests/Persistence/SriCatalogs/SriTaxSupportSeedTests.cs` y `SriTaxSupportPostgreSqlTests.cs`.
- `backend/src/ERP.API.Tests/Integration/BusinessPartnerRoleConfigHttpContractTests.cs`.
- `frontend/src/modules/purchases/hooks/usePurchasesPage.retention.test.ts`.
- `frontend/src/modules/expenses/pages/ExpenseDocumentFormPage.retention.test.tsx`.
- `frontend/src/modules/masterData/pages/MasterDataSuppliersPage.test.tsx`.
- `STATUS.md`, `docs/agent/WORK_QUEUE.md` y este informe.

EF confirma que no hay cambios de modelo pendientes. La fecha UTC del identificador generado de migración es 2026-10-21; fecha local de entrega: 2026-10-20.
