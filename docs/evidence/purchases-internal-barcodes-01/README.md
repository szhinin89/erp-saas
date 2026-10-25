# Compras: códigos Internos provisionales automáticos

Fecha: 2026-10-24. Clasificación: EXTEND. Alcance: Resolver productos pendientes y dependencias necesarias de Ítems.

Reglas vigentes: [Compras](../../architecture/purchases.md#ausencia-de-barcode-en-productos-pendientes) e [Ítems](../../architecture/items.md#2-identidad-y-unicidad).

## Validación

| Comprobación | Resultado |
|---|---|
| Regresión contra el handler original | 5 FAIL / 3 PASS; reproduce ausencia de generación automática |
| Application focal: resolución, equivalencias, validadores y actualización de Ítems | PASS 63/63 |
| Frontend: utilidades, formulario compacto, creación/vinculación, masivo y lote de 23 | PASS 86/86 |
| PostgreSQL desechable: códigos y scope | PASS 11/11: 10 en lote + colisión corregida y repetida aisladamente 1/1 |
| TypeScript `tsc -b` | PASS |
| ESLint completo | PASS; 0 errores, 35 advertencias existentes |
| Build frontend | PASS |
| Architecture.Tests backend | PASS 143/143 |
| `architecture:check` | PASS; 140 pruebas de guardrails, 0 nuevas infracciones |
| `git diff --check` | PASS |

Actualización 2026-10-24: la lista fija de barcodes descartados se sustituyó por la regla GTIN
única (frontend `isRealBarcode`, backend `IsValidGtin`), y los barcodes explícitos ya no se descartan.
Tras el cambio: Application focal 447/447, PostgreSQL desechable 22/22
(`ResolvePurchaseReceptionLinesIntegrationTests` + `ItemRepositorySupplierCodeMatchTests`) y
frontend focal 767/767 PASS.

PostgreSQL cubre códigos del proveedor no GTIN en el XML (`0`, vacío, `07`, `02`, `300067162`), creación de 23 productos, EAN-8/EAN-13 desde XML sin hint del cliente, EAN duplicado con solicitud de vinculación, colisión real insertada después del precheck, rollback/regeneración, dos lotes coordinados en paralelo y unicidad por tenant/empresa entre variantes y empaques. Añadir posteriormente un EAN comprueba conservación de Id, Interno, presentación, equivalencia y vínculo documental.

La primera pasada de la clase de integración dio 14 PASS y 2 FAIL por fixtures nuevos (orden de líneas y variante ausente en el producto sembrado). Se corrigieron los fixtures; la preparación de la colisión se ajustó una vez más para reutilizar el seed de Item completo y evitar una modificación de clave de tracking en el fixture. Resultado definitivo: 10 casos PASS en lote y colisión PASS 1/1 al repetirla aisladamente. Los casos existentes de factura de 100 líneas, vinculación y corrección, presentaciones, auto-matching, precios/confirmación, stock/Kardex, rollback y concurrencia de SKU/proveedor pasaron. Un intento de recompilar mientras esa ejecución seguía activa encontró DLL bloqueadas; se serializó la pasada final. No quedan fallos pendientes.

CI: las pruebas nuevas amplían la clase parcial `ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception.ResolvePurchaseReceptionLinesIntegrationTests`, ya registrada en `tools/ci/backend-test-groups.json`; no se agregó otra clase ejecutable.

## Archivos

Backend:

- `backend/src/ERP.Application/Modules/Inventory/ItemMatching/UseCases/ResolveLines/ResolvePurchaseReceptionLinesHandler.cs`
- `backend/src/ERP.Application/Modules/Inventory/ItemMatching/Services/ItemMatchConfirmationService.cs`
- `backend/src/ERP.Application/Modules/Items/UseCases/CreateItem/CreateItemCommandHandler.cs`
- `backend/src/ERP.Application.Tests/Inventory/ItemMatching/ResolvePurchaseReceptionLinesHandlerTests.cs`
- `backend/src/ERP.Application.Tests/Inventory/ItemMatching/ItemMatchConfirmationServiceTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseReception/ResolvePurchaseReceptionLinesIntegrationTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseReception/ResolvePurchaseReceptionLinesIntegrationTests.InternalBarcodes.cs` (nuevo)

Frontend:

- `frontend/src/modules/purchases/utils/pendingProductsResolution.ts`
- `frontend/src/modules/purchases/utils/pendingProductsResolution.test.ts`
- `frontend/src/modules/purchases/schemas/pendingProductsSchema.ts`
- `frontend/src/modules/purchases/components/PendingProductRow.tsx`
- `frontend/src/modules/purchases/components/ResolvePendingProductsModal.tsx`
- `frontend/src/modules/purchases/components/ResolvePendingProductsModal.test.tsx`
- `frontend/src/i18n/locales/es.json`
- `frontend/src/i18n/locales/en.json`

Documentación:

- `docs/architecture/purchases.md`
- `docs/architecture/items.md`
- `STATUS.md`
- Este informe de evidencia (nuevo).

Sin operaciones sobre datos reales, migraciones, cambios fiscales, commit, push ni deploy. No hay decisiones de negocio pendientes.
