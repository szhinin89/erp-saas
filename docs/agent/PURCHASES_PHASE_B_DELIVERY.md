# Purchases Legacy / Domain — Fase B

Fecha: 2026-10-15. Implementado, validación focal PASS; sin commit/push.

## BUG REAL — orden de migración con claves reutilizadas

La validación inicial no cubría una compra activa creada ANTES de migrar con una
clave de un descarte histórico. El backfill se ejecutaba bajo los índices legacy:
al cambiar 3 → 4, el histórico volvía al índice y colisionaba con la compra activa.
El fallo reportado en piloto requiere este fix antes de reintentar el despliegue.

Orden corregido en `20261015202527_PurchaseDiscardedState.Up`, en la misma transacción:

1. Eliminar ambos índices legacy: tenant/access_key y tenant/company/supplier/número.
2. Recrear ambos con predicado explícito de activos `status IN (1, 2)`.
3. Actualizar la función del trigger de exclusividad compra/gasto a activos 1/2.
4. Convertir únicamente `Cancelled AND confirmed_grand_total IS NULL` a Discarded.

También Down conserva predicados explícitos 1/2; esta migración ya no contiene
`status <> 3`. No existen lookups adicionales en ella. No cambian scopes ni locks.

La regresión PostgreSQL cubre cada clave por separado con activa Draft y Confirmed,
histórica convertida, coexistencia, lookups y rechazo 23505 de nuevos duplicados
activos por el índice exacto de cada caso. El esquema previo se reconstruye desde
las migraciones anteriores reales en el contenedor aislado del test, sin depender
de que Down reproduzca índices legacy.

- Reproducción previa: FAIL 4/4, todos con 23505; dos por cada índice único.
- Trigger PostgreSQL: PASS 2/2 (Discarded libera la clave; Draft activo bloquea).
- Migración corregida y reproceso: PASS 6/6 en PostgreSQL 16 real (cuatro casos de
  claves reutilizadas + backfill/evidencia + liberación/reproceso de recepción).
- Diff review/check PASS. Fix no reaplicado al piloto; sin suites completas ni commit/push.

## SSOT final

- Recepción: `PurchaseState` (`NONE/DRAFT/CONFIRMED/CANCELLED/DISCARDED`) y
  `PurchaseId` para la compra activa. La referencia histórica existente conserva
  el ID de la última Cancelled/Discarded cuando no existe una activa.
- Compra: `PurchaseStatus` persistido, sin renumeración: Draft=1, Confirmed=2,
  Cancelled=3, Discarded=4. Descartar exige Draft y conserva evidencia, evento
  `PurchaseDraftDiscardedEvent`, liberación de recepción y ausencia de reversos.
- Eliminados `PurchaseExists` e `IsDiscardedDraft` de contratos, código y tests
  backend/frontend, sin alias. Verifier y creación de proveedor deciden por estado.
  Mappers, importación/descargas, badges, modo lectura y filtro usan el contrato nuevo.
- `AffectedPurchaseExists` de NC permanece intacto. Scope tenant/company/branch,
  claves de unicidad y lock del trigger conservan sus dimensiones actuales.

## Migración

`20261015202527_PurchaseDiscardedState`, generada con EF; Designer y snapshot incluidos.
Backfill: solo `status=3 AND confirmed_grand_total IS NULL` cambia a `status=4`.
Los dos índices únicos de compras y la consulta del trigger compartido consideran
compras activas exclusivamente en estados 1/2. Runtime no usa totales para resolver
estados. Los predicados legacy permanecen solo en migraciones históricas anteriores.
Este fix no se reaplicó al piloto; la validación local usa PostgreSQL 16 Testcontainers.

## Validación focal

| Comprobación | Resultado |
|---|---|
| Application: descarte, resolver, importación, descargas individual/lote, detalle/listado | PASS 51/51 |
| Infrastructure: verifier, refresh, recepción liberada/recreación, constraints/reproceso y factura afectada | PASS 41/41 |
| Migración histórica desde esquema anterior, estados intactos, evidencia, filtros, aislamiento y recreación | PASS 1/1 PostgreSQL real |
| Frontend: estado documental, estado de recepción, refresh, acciones, hooks y creación de proveedor en los cinco estados | PASS 90/90 |
| TypeScript `tsc -b`, build frontend | PASS |
| ESLint focal productivo | PASS, 0 errores; aviso de tamaño existente de PurchasesPage |
| Guard `tools/architecture/run-all.mjs` | PASS, 0 nuevas violaciones |
| Búsqueda exacta de los dos campos eliminados en backend/frontend | PASS, 0 coincidencias; campo de NC conservado |
| Revisión del diff / whitespace | PASS |

Los 41 casos iniciales de Infrastructure y el caso nuevo de migración se ejecutaron
por separado. Incluyen confirmada que no puede descartarse, evento sin reversos,
reproceso después de Cancelled/Discarded, bloqueo de duplicados activos y exclusividad
compra/gasto. No se ejecutaron suites completas ni se arrancó la aplicación del piloto.

## Deuda restante

Sin deuda funcional pendiente en estos dos puntos. Pendiente operativo: desplegar
la migración junto al backend/frontend que consumen el contrato nuevo. Persisten
avisos de build/lint ajenos a este alcance; no se modificaron otros módulos CLOSED.

## Archivos

Sin archivos eliminados. Archivos nuevos: migración EF, Designer y este informe.
Inventario completo de archivos modificados/nuevos de esta tarea:

- `STATUS.md`
- `backend/src/ERP.Application.Tests/Purchases/DiscardPurchaseDraftTests.cs`
- `backend/src/ERP.Application.Tests/Purchases/PurchaseReception/BatchDownloadPurchaseReceptionXmlHandlerTests.cs`
- `backend/src/ERP.Application.Tests/Purchases/PurchaseReception/DownloadPurchaseReceptionXmlHandlerTests.cs`
- `backend/src/ERP.Application.Tests/Purchases/PurchaseReception/ImportPurchaseReceptionHandlerTests.cs`
- `backend/src/ERP.Application.Tests/Purchases/PurchaseReception/PurchaseReceptionPurchaseStateResolverTests.cs`
- `backend/src/ERP.Application/Modules/Purchases/DTOs/PurchaseDtos.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseMapper.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/DTOs/PurchaseReceptionDtos.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/Mapping/PurchaseReceptionMapper.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/UseCases/BatchDownloadPurchaseReceptionXml/BatchDownloadPurchaseReceptionXmlDtos.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/UseCases/BatchDownloadPurchaseReceptionXml/BatchDownloadPurchaseReceptionXmlHandler.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/UseCases/DownloadPurchaseReceptionXml/DownloadPurchaseReceptionXmlDtos.cs`
- `backend/src/ERP.Application/Modules/Purchases/PurchaseReception/UseCases/DownloadPurchaseReceptionXml/DownloadPurchaseReceptionXmlHandler.cs`
- `backend/src/ERP.Application/Modules/Purchases/UseCases/DiscardPurchaseDraftUseCases.cs`
- `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseDraftUseCases.cs`
- `backend/src/ERP.Domain/Modules/Purchases/Entities/PurchaseInvoice.cs`
- `backend/src/ERP.Domain/Modules/Purchases/Enums/PurchaseStatus.cs`
- `backend/src/ERP.Domain/Modules/Purchases/Interfaces/IPurchaseInvoiceRepository.cs`
- `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/Enums/PurchaseReceptionPurchaseState.cs`
- `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/Models/PurchaseReceptionVerifiedItem.cs`
- `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/Services/PurchaseReceptionPurchaseStateResolver.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/DiscardPurchaseDraftReleasesReceptionPostgreSqlTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseReception/PurchaseReceptionVerifierTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseReception/RefreshPurchaseReceptionStatusHandlerTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Persistence/PurchaseExpenseReprocessAfterCancelConstraintsTests.cs`
- `backend/src/ERP.Infrastructure/Migrations/20261015202527_PurchaseDiscardedState.Designer.cs`
- `backend/src/ERP.Infrastructure/Migrations/20261015202527_PurchaseDiscardedState.cs`
- `backend/src/ERP.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs`
- `backend/src/ERP.Infrastructure/Modules/Purchases/PurchaseReception/PurchaseReceptionVerifier.cs`
- `backend/src/ERP.Infrastructure/Persistence/Configurations/Purchases/PurchaseInvoiceConfiguration.cs`
- `backend/src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseInvoiceRepository.cs`
- `docs/agent/DECISIONS.md`
- `docs/agent/PROJECT_STATE.md`
- `docs/agent/PURCHASES_PHASE_B_DELIVERY.md`
- `docs/agent/WORK_QUEUE.md`
- `frontend/src/i18n/locales/en.json`
- `frontend/src/i18n/locales/es.json`
- `frontend/src/modules/purchases/api/purchaseReceptionService.ts`
- `frontend/src/modules/purchases/api/purchaseService.ts`
- `frontend/src/modules/purchases/components/PurchaseReceptionActionsCell.test.tsx`
- `frontend/src/modules/purchases/components/PurchaseReceptionActionsCell.tsx`
- `frontend/src/modules/purchases/hooks/usePurchaseReceptionPage.test.ts`
- `frontend/src/modules/purchases/hooks/usePurchaseReceptionPage.ts`
- `frontend/src/modules/purchases/hooks/usePurchasesPage.draftRecovery.test.ts`
- `frontend/src/modules/purchases/hooks/usePurchasesPage.ts`
- `frontend/src/modules/purchases/pages/PurchasesPage.tsx`
- `frontend/src/modules/purchases/utils/purchaseDocumentState.test.ts`
- `frontend/src/modules/purchases/utils/purchaseDocumentState.ts`
- `frontend/src/modules/purchases/utils/receptionPurchaseState.ts`
- `frontend/src/modules/purchases/utils/receptionRefresh.test.ts`
