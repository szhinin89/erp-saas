# QA — Cierre de Compras, Gastos, CxP y Pagos proveedor

Fecha: 2026-09-12. Ticket: **CLOSE-PURCHASES-EXPENSES-PAYABLES-READY-01**.

Estado funcional: **LISTO / CERRADO**, según el [SSOT de entrega](../../STATUS.md#close-purchases-expenses-payables-ready-01--cierre-funcional-2026-09-12). Esta matriz identifica pruebas existentes y el protocolo de regresión; no declara que se hayan vuelto a ejecutar en este cambio documental.

## Cobertura que protege el cierre

Rutas relativas a la raíz del repositorio. Los nombres son suites existentes, no resultados nuevos.

| Flujo / riesgo | Pruebas existentes |
|---|---|
| Compra normal, confirmación y anulación | `backend/src/ERP.Application.Tests/Purchases/ConfirmPurchaseHandlerTests.cs`, `CancelPurchaseHandlerTests.cs` en la misma carpeta |
| Recepción XML y asociación | `backend/src/ERP.Application.Tests/Purchases/PurchaseReception/`; `frontend/src/modules/purchases/utils/purchaseLineReadiness.test.ts` |
| Presentación unidad/empaque, vínculo manual y línea guardada | `frontend/src/modules/purchases/utils/purchaseLinePresentation.test.ts`: nombre sin UOM técnico, factor separado, cantidades/costos preservados y fallback sin contexto |
| NC, devolución, inventario y contabilidad | `backend/src/ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.cs`; `backend/src/ERP.Infrastructure.Tests/Accounting/PurchaseReturnAuthorizedPostingIntegrationTests.cs`, `PurchaseReturnCancelledPostingIntegrationTests.cs`, `PurchaseCreditNoteDiscountPostingIntegrationTests.cs` |
| Gasto manual/XML, IVA y tipo documental | `frontend/src/modules/expenses/pages/ExpenseDocumentFormPage.reception.test.tsx`, `ExpenseDocumentFormPage.vatMismatch.test.tsx`; `frontend/src/modules/expenses/components/ExpenseDocumentHeader.test.tsx`, `ExpenseDocumentLinesEditor.test.tsx` |
| Confirmación de gasto y anulación controlada | `backend/src/ERP.Application.Tests/Expenses/ExpenseDocumentConfirmUseCasesTests.cs`, `CancelExpenseDocumentUseCasesTests.cs` |
| CxP de ambos orígenes y restricciones | `backend/src/ERP.Application.Tests/Payables/AccountsPayableQueryUseCasesTests.cs`, `AccountsPayableServiceTests.cs`; `backend/src/ERP.Domain.Tests/Payables/AccountsPayableTests.cs` |
| Pago parcial/total, reversa, rollback y aislamiento | `backend/src/ERP.Infrastructure.Tests/Payables/SupplierPaymentEndToEndTests.cs`: cuotas de compras/gastos, posting balanceado, reversa total/parcial y bloqueo entre empresas |
| Reportes y reversas | `backend/src/ERP.Infrastructure.Tests/Accounting/AccountingReportsRepositoryTests.cs`, `ReverseJournalEntryIntegrationTests.cs` |
| Origen contable y líneas legibles | `backend/src/ERP.Application.Tests/Accounting/JournalEntrySourceResolverTests.cs`, `ExpenseDocumentConfirmedPostingTranslatorTests.cs`, `SupplierPaymentConfirmedPostingTranslatorTests.cs`, `SupplierPaymentReversedPostingTranslatorTests.cs` |

La existencia de suites no sustituye su ejecución. Validaciones históricas: ver STATUS, entradas «NC de compra por devolución de productos» (2026-09-10), «EXPENSES-CANCEL-01» y «DOCUMENT-FLOW-POLICY-01» (2026-08-30). La ejecución más reciente comprobada en esta conversación corresponde a `f2c508a5`: 252 tests frontend de Compras aprobados, TypeScript/build correctos y lint sin errores.

## Checklist obligatorio ante cambios del flujo

Casillas sin marcar a propósito: corresponden a la **próxima regresión**, no a defectos ni a tareas pendientes del cierre aprobado.

- [ ] Compra normal y XML: vincular manualmente unidad x1 y caja; guardar y reabrir detalle/edición; comprobar nombre y factor sin IDs, cantidades base y costo correctos.
- [ ] NC por devolución y descuento: comprobar impuestos históricos, efecto de inventario según tipo, CxP, asiento y anulación sin duplicación.
- [ ] Gasto manual/XML: comprobar IVA desde `sri-vat-rates`, tipo documental desde `sri-doc-types`, totales XML y generación de CxP.
- [ ] Por cada origen (compra/gasto), pagar parcialmente y totalmente; bloquear anulación con pagos activos; reversar todos los pagos y anular, respetando otras restricciones documentales.
- [ ] Comprobar asientos balanceados y rollback si falla posting; verificar que reportes netean original `Reversed` y reverso sin perder movimientos.
- [ ] Cambiar de empresa: impedir consulta/aplicación/reversa de CxP y pagos ajenos.
- [ ] Verificar origen y líneas contables legibles; cuentas resueltas por reglas/configuración.
- [ ] Ejecutar E2E/integración del flujo afectado, registrar comando, fecha y resultado. No declarar aprobado por inspección de código solamente.

Los [guardrails y pendientes no bloqueantes](../../STATUS.md#guardrails--no-romper) se mantienen en STATUS para evitar fuentes de estado divergentes. No se certifica aquí la aplicación de migraciones a ambientes ni un cierre contable global.
