# Entrega — Unificación de NC de Compras (2026-10-21)

EXTEND implementado para revisión. SSOT funcional único: [purchases.md](../architecture/purchases.md).

Cierre 2026-10-21: incluido en el commit local de Compras, sin push/deploy. Las validaciones y la ausencia de
commit descritas abajo corresponden a la entrega previa; la prueba externa de Gastos con bloqueo PR-7 sigue pendiente.

Retiradas las altas y operaciones públicas independientes de devolución, su formulario, sus secciones operativas y sus contratos frontend. KEEP: el motor interno y la consulta del historial. Las dos decisiones del propietario quedan incorporadas en el SSOT; no quedan decisiones de dominio pendientes.

Validación focal:

| Comprobación | Resultado |
|---|---|
| NC, vínculo histórico, concurrencia, reproceso y reversas; PostgreSQL real | 21/21 PASS; SRI simulado |
| Contratos API de NC e historial | 24/24 PASS |
| Application y validación de identidad tributaria | 79/79 PASS |
| Contabilización de devolución y descuento; PostgreSQL real | 6/6 PASS |
| Frontend: formularios, detalle, listas e historial | 35/35 PASS |
| API, TypeScript, lint y build frontend | PASS; lint sin errores, advertencias existentes |
| Gate de arquitectura | PASS, 0 violaciones nuevas |
| Guardrail completo | PR-7 externo: test de Gastos preexistente con 516 líneas frente a techo 514 |
| Diff review / whitespace | PASS |

STRUCTURAL DEBT preexistente y fuera de alcance: `frontend/src/modules/expenses/pages/ExpenseDocumentFormPage.retention.test.tsx` ya tenía cambios al iniciar y excede su techo PR-7 en dos líneas. No se modificó para esta tarea. El guardrail no puede declararse íntegramente PASS mientras ese cambio externo siga así.

No se modificó GLOBAL ni se crearon migraciones. No se ejecutaron escrituras sobre bases de negocio ni pruebas contra SRI real. No se hizo commit, push o deploy. Los cambios previos del proceso de catálogos SRI y sus pruebas se conservaron intactos.

## Archivos de esta entrega

M: modificado; D: retirado; ?: nuevo. En `STATUS.md`, `PROJECT_STATE.md` y `WORK_QUEUE.md` se añadió únicamente la nota de esta entrega, conservando los cambios previos.

- `M` `FEATURES.md`
- `M` `STATUS.md`
- `M` `backend/src/ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.cs`
- `M` `backend/src/ERP.API.Tests/Purchases/PurchaseCreditNoteControllerTests.cs`
- `M` `backend/src/ERP.API.Tests/Purchases/PurchaseReturnControllerTests.cs`
- `M` `backend/src/ERP.API/Controllers/PurchaseCreditNoteController.cs`
- `M` `backend/src/ERP.API/Controllers/PurchaseReturnController.cs`
- `M` `backend/src/ERP.Application/DependencyInjection.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/AuthorizePurchaseCreditNoteUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/AuthorizePurchaseReturnUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/CancelPurchaseCreditNoteUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/CancelPurchaseReturnUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/LinkPurchaseCreditNoteToReturnUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseCreditNoteDraftUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseReturnDraftUseCases.cs`
- `M` `backend/src/ERP.Application/Modules/Purchases/UseCases/RegisterSupplierCreditNoteUseCases.cs`
- `M` `backend/src/ERP.Domain/Kernel/Modules/PurchasesModule.cs`
- `M` `backend/src/ERP.Domain/Modules/Purchases/Entities/PurchaseCreditNote.cs`
- `M` `backend/src/ERP.Domain/Modules/Purchases/Interfaces/IPurchaseCreditNoteRepository.cs`
- `M` `backend/src/ERP.Domain/Modules/Purchases/Interfaces/IPurchaseReturnRepository.cs`
- `M` `backend/src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseCreditNoteRepository.cs`
- `M` `backend/src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseReturnRepository.cs`
- `M` `docs/agent/DECISIONS.md`
- `M` `docs/agent/PROJECT_STATE.md`
- `M` `docs/agent/WORK_QUEUE.md`
- `M` `docs/architecture/purchases.md`
- `M` `frontend/src/i18n/locales/en.json`
- `M` `frontend/src/i18n/locales/es.json`
- `M` `frontend/src/modules/purchases/api/purchaseCreditNoteService.ts`
- `M` `frontend/src/modules/purchases/api/purchaseReturnService.ts`
- `D` `frontend/src/modules/purchases/components/PurchaseReturnCreditNoteSection.test.tsx`
- `D` `frontend/src/modules/purchases/components/PurchaseReturnCreditNoteSection.tsx`
- `D` `frontend/src/modules/purchases/components/PurchaseReturnDraftFormSection.tsx`
- `M` `frontend/src/modules/purchases/components/PurchaseReturnableLinesEditor.test.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseCreditNoteDetailPage.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseCreditNoteFormPage.test.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseCreditNoteFormPage.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseCreditNoteListPage.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseReturnDetailPage.test.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseReturnDetailPage.tsx`
- `D` `frontend/src/modules/purchases/pages/PurchaseReturnFormPage.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchaseReturnListPage.tsx`
- `M` `frontend/src/modules/purchases/pages/PurchasesPage.tsx`
- `M` `frontend/src/modules/purchases/schemas/purchaseCreditNoteSchema.ts`
- `M` `frontend/src/modules/purchases/schemas/purchaseReturnSchema.test.ts`
- `M` `frontend/src/modules/purchases/schemas/purchaseReturnSchema.ts`
- `M` `frontend/src/routes/catalogRoutes.tsx`
- `??` `backend/src/ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.Reprocessing.cs`
- `??` `backend/src/ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.UnifiedCreditNotes.cs`
- `??` `backend/src/ERP.Application.Tests/Purchases/PurchaseCreditNoteFiscalIdentityTests.cs`
- `??` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseCreditNoteFiscalIdentity.cs`
- `??` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseCreditNoteHistoricalReturns.cs`
- `??` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseCreditNoteReprocessingGuard.cs`
- `??` `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchaseCreditNoteReturnMotor.cs`
- `??` `backend/src/ERP.Application/Properties/PurchaseHistoryTestAccess.cs`
- `??` `docs/agent/PURCHASE_CREDIT_NOTE_UNIFICATION_DELIVERY.md`
