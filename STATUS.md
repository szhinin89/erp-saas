# Project Status

**Single source of truth** for delivery state. Updated: **2026-08-30** · Kernel refactor: **2026-06-05**.

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
| **Items / Catálogo v1.0** | 2026-06-17 | — | 14 entidades, 56 endpoints, 20 validators; tenant-scoped catalog compartido entre companies; 6 catálogos CRUD (Brand, Family, Category, Subcategory, AttributeGroup, AttributeDefinition); Detail page con Variants, Images, Conversions, Substitutes, Packaging; SRI lookups (UOM, VAT, ICE); listo para Inventario, Compras, Ventas, Facturación Electrónica |
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
