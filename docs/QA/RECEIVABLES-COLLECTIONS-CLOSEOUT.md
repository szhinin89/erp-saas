# QA — Cierre de Cuentas por Cobrar y cobros de cliente

Fecha: 2026-10-27. Comportamiento funcional: [SSOT de CxC](../architecture/receivables.md). Estado
de entrega: [STATUS](../../STATUS.md). Esta matriz identifica pruebas existentes y el protocolo de
regresión; solo las casillas marcadas acreditan una ejecución aprobada.

## Cobertura que protege el cierre

Rutas relativas a `backend/src/` salvo indicación. Los nombres son suites existentes, no resultados
nuevos.

| Flujo / riesgo | Pruebas existentes |
|---|---|
| Saldo, cuotas (FIFO / cuota elegida), reversa, devolución y reconstrucción de cuotas | `ERP.Domain.Tests/Sales/SalesReceivableTests.cs`, `SalesReceivableInitialBalanceTests.cs` |
| Registro de cobro: cliente, cuota, saldo, destino, idempotencia | `ERP.Application.Tests/Finance/RegisterCollectionCommandHandlerTests.cs`; `ERP.API.Tests/Finance/FinancePaymentsControllerTests.cs` |
| Listado, filtros por saldo y estado visible | `ERP.Application.Tests/Sales/SalesReceivableUseCasesTests.cs` |
| Concurrencia, alcance de cliente e idempotencia sobre PostgreSQL | `ERP.API.Tests/Integration/ReceivableCollectionConcurrencyTests.cs`, `ReceivableCollectionCustomerScopeTests.cs`, `FinancialCommandIdempotencyTests.cs`, `SalesCancelCollectionConcurrencyTests.cs` |
| Anulación y devolución con CxC | `ERP.Application.Tests/Sales/CancelSalesInvoiceHandlerTests.cs`, `SalesReturnRefundHandlerTests.cs` |
| Contabilización de cobro y reversa | `ERP.Infrastructure.Tests/Finance/CollectionPostingIntegrationTests.cs` |
| Modal de cobro | `frontend/src/modules/finance/components/RegisterCollectionModal.test.tsx` |

La existencia de suites no sustituye su ejecución. Ejecución focal de este cierre (2026-10-27):
Domain `SalesReceivable*` 47/47, Application cobros/CxC/anulación/devolución 102/102,
Infrastructure 33/33 y API 16/16 (Release); detalle en la
[evidencia](../evidence/receivables-e2e-01/README.md#gates). Suite completa no ejecutada.

## Checklist obligatorio ante cambios del flujo

La casilla marcada acredita únicamente la validación indicada y enlaza su evidencia aprobada. Las
casillas sin marcar corresponden a la **próxima regresión**, no a defectos del cierre aprobado.

- [x] Factura a crédito de una cuota: cobro parcial y del saldo desde `/finance/receivables`, con
  recarga y reapertura de factura y CxC. E2E **21/21 PASS**, aprobado por el propietario
  (2026-10-27); [evidencia y límites](../evidence/receivables-e2e-01/README.md).
- [x] Asientos de venta a crédito y de cobro a banco publicados y balanceados (misma evidencia).
- [x] Cambiar de empresa: impedir consulta y cobro de CxC y facturas ajenas. **7/7 PASS** (misma
  evidencia).
- [x] Factura con varias cuotas (3 cuotas, vencimientos distintos): cobro a cuota elegida (con
  rechazo por exceso del saldo de la cuota), cobro sin cuota por FIFO, liquidación y «Vence» tras
  cada cobro, con recarga, reapertura, asientos y aislamiento. E2E **58/58** y aislamiento **7/7
  PASS**, aprobado por el propietario (2026-10-27);
  [evidencia y límites](../evidence/receivables-e2e-02/README.md).
- [ ] Mora con varias cuotas en E2E visual: **no acreditada en E2E**. Solo tiene cobertura mediante
  test automatizado (`SalesReceivableUseCasesTests.Build_varias_cuotas_mora_sigue_a_la_primera_cuota_no_saldada`);
  los datos E2E no permiten una cuota vencida.
- [ ] Cobro con destino caja y sin destino.
- [ ] Reversa de cobro (sin endpoint HTTP ni UI hoy) y anulación de factura tras reversar.
- [ ] Devolución de venta con crédito a CxC sobre una CxC con cobros.
- [ ] Saldo inicial de CxC (IL-5): cobro parcial y total.
- [ ] Ejecutar E2E/integración del flujo afectado, registrar comando, fecha y resultado. No declarar
  aprobado por inspección de código solamente.

No se certifica aquí la aplicación de migraciones a ambientes, despliegue ni un cierre contable
global.
