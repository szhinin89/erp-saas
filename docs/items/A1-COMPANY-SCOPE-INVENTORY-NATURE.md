# A1 — Item Company Scope + Inventory Semantics

Estado: CLOSED. Fecha: 2026-10-05. Decisión aprobada: [ADR-040](../decisions/ADR-040-item-company-scope-inventory-nature.md).

## Resultado

Item pertenece a Tenant + Company, sin BranchId. Su empresa se obtiene del contexto autenticado, es inmutable y las lecturas quedan cerradas sin contexto válido. SKU, SKU de variante y barcode permiten repetición entre empresas y la impiden dentro de una empresa. Código de proveedor es único por Tenant + Company + Supplier. Los hijos y la auditoría heredan el aislamiento de su Item; los sustitutos requieren ambos Items dentro del scope. Pricing tiene FK compuesta al Item de su empresa.

ItemTypeDefinition conserva su contrato CLOSED de clasificación pura. ItemNature es independiente: Product siempre participa en Inventory; Service nunca utiliza Warehouse, StockMovement, CurrentStock, kardex ni costo promedio de Inventory. Digital permanece FUTURE.

StockControlEnabled reemplaza TracksStock como control individual de disponibilidad. La nueva preferencia Company `inventory.stock_control_enabled` tiene default true. OFF en cualquiera conserva los movimientos de Product sin bloqueo de disponibilidad. Ambos ON mantienen la validación existente y la excepción AllowSellWithoutStock. Sales, Purchases, ajustes, transferencias, disponibilidad, matching, búsquedas y reportes utilizan el Item del scope actual. Se conserva Warehouse por línea y multiwarehouse.

La UI muestra naturaleza y control individual, oculta configuración Inventory/packaging para Service y permite configurar el control general de la empresa con componentes ZH. El picker de Inventory filtra por participación, incluida la de Products con control OFF. Las advertencias de disponibilidad de venta respetan el control efectivo.

## Migración y datos

Creada y aplicada `20261006002212_ItemCompanyScopeAndInventoryNature` con Designer y snapshot EF. No hubo reset. Se conserva el dato existente y se resuelve ownership a partir de referencias reales o de la única empresa del tenant, abortando ante ambigüedad. Se añaden restricciones compuestas y triggers PostgreSQL para naturaleza/scope de Inventory y unicidad de barcode entre variantes y packaging.

Verificación en `dberpsaas` (PostgreSQL de desarrollo, puerto 5435): `10001204 — BIS MAN REG 12X10` quedó `Nature = Product`, `CompanyId = e686e280-592f-4fef-b24f-356f7b2fa3d8`, control individual ON. La migración completa desde BD vacía también fue ejercitada por Testcontainers.

## Verificaciones

| Verificación | Resultado |
|---|---|
| `dotnet build backend/src/ERP.slnx --no-restore` | PASS, 0 errores; warnings existentes |
| Domain: Items + ConfigurationDefinitionCatalog | 107/107 PASS |
| Application: Items, Sales, Purchases, Inventory/matching y preferencias | 279/279 PASS |
| Infrastructure con PostgreSQL real: A1, búsquedas, supplier matching, backfills/scope y objetos SQL | 31 casos únicos PASS: 29 de la suite ampliada y 2 rechecks tras corregir fixtures |
| API HTTP + PostgreSQL: ItemLookupStockFilter | 7/7 PASS |
| `ERP.Architecture.Tests` | 143/143 PASS |
| Frontend: módulos afectados y ItemEditorModal | 820/820 PASS, 69 archivos |
| Typecheck (`tsc -b`) | PASS |
| Frontend lint | PASS, 0 errores; 35 warnings existentes |
| Frontend build + Platform guard | PASS |
| `npm run architecture:check` | PASS, 81 tests de scanners; 0 nuevas infracciones |
| `git diff --check` y revisión de paths/fixtures | PASS |

Las pruebas incluyen todas las colecciones hijas pobladas, reads/búsquedas/matching cross-company, reemplazo rechazado, falta de Company, unicidad SKU/variante/proveedor/barcode, barcode entre tablas, FK de Pricing, Service excluido de Inventory, Product control OFF con movimientos, bloqueo de existencia con ambos ON, control general OFF, rechazo Sales/Purchases de Item ajeno, Warehouse por línea y múltiples bodegas. Se registran los objetos SQL de A1 en el guardrail de supervivencia a un squash.

## Hallazgos y límites

Se corrigió un defecto en borradores de compra que aceptaba silenciosamente un ItemId no resuelto. Los fixtures existentes se adaptaron para crear Items con empresa real; un fixture de backfills que compartía Item entre empresas ahora crea un Item propio para cada empresa. La prueba de variantes utiliza la ruta canónica TrackVariantAsync y atributos distintos, respetando las invariantes del agregado. Las comprobaciones de colecciones se siembran como un agregado nuevo completo.

Permanece deuda previa de warnings de compilación/lint y avisos de bundles grandes/import dinámico inefectivo en Vite. No se decidió ni modificó el algoritmo de costo con stock negativo, AllowSellWithoutStock ni las preferencias futuras AllowNegativeStock: esa evolución queda en A2. No se ejecutó una regresión total ajena al alcance.

No se activó ninguna STOP CONDITION sobre datos reales. No se modificaron BusinessPartner, ADR-017, ItemTypeDefinition, ElectronicDocuments/SRI ni los interceptores CLOSED. A1 fue aprobado CLOSED; se autorizó un único commit después de la higiene final, sin push.

## Git y diff

La revisión de los 179 archivos modificados clasificó 170 como cambios reales A1 (incluidas las adaptaciones de tests), 1 como ModelSnapshot generado, 6 como documentación A1 y 2 como cambios accidentales de timestamp en reportes generados. Los 10 archivos nuevos pertenecen a A1: 6 de implementación/tests, 2 de migración/Designer y 2 de documentación.

Resultado final antes del commit: 177 archivos modificados y 10 nuevos, 187 en total.

| Clasificación final | Archivos | Alcance |
|---|---:|---|
| Cambio real A1 | 176 | Backend/frontend y tests permanentes; incluye A1ItemTestSupport, ItemNatureA1Tests e ItemCompanyScopeA1Tests |
| Migración / ModelSnapshot | 3 | ItemCompanyScopeAndInventoryNature.cs, su Designer y ErpDbContextModelSnapshot.cs |
| Documentación | 8 | FEATURES, STATUS, arquitectura, seguridad, índice ADR, ADR-040, histórico Fase 5 e informe A1 |
| Accidental / temporal pendiente | 0 | Limpieza completada |

Se revirtieron únicamente los timestamps de `docs/ci/PLATFORM_GUARD_REPORT.md` y `docs/future-platform/API_USAGE_GRAPH.json`. Se retiró el reformateo incidental de `salesCalc.test.ts`, conservando el cambio de contrato y la prueba de control OFF; se restauró el BOM original de tres archivos frontend. No cambió comportamiento funcional.

Se eliminaron los 37 logs `.tmp-a1-*.log` restantes; los scripts temporales A1 ya habían sido eliminados. No queda ningún temporal A1 rastreado, nuevo o ignorado. Los logs `.tmp-01b-*` preexistentes pertenecen a otra entrega y se conservaron. Los auxiliares de tests A1 bajo `src/*.Tests` son código permanente de la suite.

Checks rápidos de higiene: 87/87 tests de `salesCalc.test.ts` PASS y `git diff --check` limpio. No se repitió auditoría arquitectónica ni se reabrió A1.

Commit autorizado: `feat(items): scope items by company and separate inventory nature`. Incluye el alcance Company de Item, naturaleza operativa independiente, control general/individual, migración, tests y documentación. Sin push.
