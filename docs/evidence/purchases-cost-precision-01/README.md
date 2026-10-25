# Compras — Etapa 1: precisión de prorrateo y base cero

Fecha: 2026-10-25. Implementación pendiente de gate del propietario; no CLOSED.

## Auditoría y decisión

- KEEP: `CompanyPrecisionPolicy` en `company_precision_policy`, consultada por Tenant + Company
  mediante `ICompanyPrecisionPolicyProvider.GetEffectiveAsync()`. Frontend recibe
  `EffectivePrecisionPolicyDto` por `GET /api/v1/config/precision-policy`.
- KEEP: `UnitCostDecimals` configurable (2–10) gobierna `LandedUnitCost`. Impuestos y dinero fiscal
  conservan `FiscalPrecision.TaxAmount`; las asignaciones y `TotalLineCost` conservan su escala fija.
- BUG REAL: simulación a centavos frente a `ProrateAmount` backend a seis decimales; modal y
  simulación rechazaban base cero aunque SSOT y Domain disponen reparto equitativo.
- BUG REAL relacionado: sumar un incremento al costo unitario ya redondeado no equivale a dividir
  el nuevo costo total entre la cantidad base. El formulario también descartaba fracciones internas
  de las asignaciones al reconstruir `TotalLineCost`.
- FALTA resuelta con aprobación explícita: exponer la escala interna existente de asignaciones en
  el contrato oficial. `CostAllocationDecimals` deriva de `FiscalPrecision.UnitCost`; no es una
  configuración nueva, columna, default frontend ni parámetro editable.

## Reproducción y corrección

Las seis regresiones originales fallaron antes del cambio:

| Caso | Antes | Después |
|---|---|---|
| Tres bases iguales; costo unitario con 2, 6 y 10 decimales (3 casos) | 0.33 / 0.33 / 0.34 | 0.333333 / 0.333333 / 0.333334 |
| Base cero y descuento del 100 % (2 casos) | 0 / 0 / 0 | Reparto equitativo con residuo |
| Bases 1 / 2 / 4 | 0.14 / 0.29 / 0.57 | 0.142857 / 0.285714 / 0.571429 |

Se reutiliza `FiscalDecimal`/`fiscalRound` de Compras (decimal.js, redondeo HALF_UP). La última
línea recibe la diferencia exacta entre el importe original y las asignaciones anteriores.
El costo nuevo se calcula desde el total de línea y la cantidad base, aplicando al final la escala
empresarial de costo unitario. La base documental XML se conserva explícita en la entrada local
de simulación para evitar reconstruirla desde un precio dividido.

No se cambiaron Domain, persistencia productiva, impuestos, XML, CxP ni movimientos de inventario.
La distribución sigue siendo aditiva: su incremento del total es el comportamiento existente;
esta etapa no convierte un transporte XML en costo distribuido ni añade idempotencia.

## Validación

- Antes: seis regresiones, 6 FAIL.
- Después: esas seis PASS; archivo ampliado, 9/9 PASS.
- Frontend focal, ocho archivos: 178/178 PASS (política, Compras, reapertura/payload, desglose XML y
  fixtures de selección de empresa).
- Domain distribución: 9/9 PASS; Application distribución y política: 30/30 PASS; API: 6/6 PASS.
- PostgreSQL 16 desechable: cuatro casos nuevos de asignación/reapertura y tres regresiones de
  guardado de grupos XML, 7/7 PASS. No se usaron bases existentes. Se aplica la baseline vigente
  exclusivamente al contenedor aislado, sin modificar archivos de migración.
- TypeScript, lint (0 errores; 35 warnings del repositorio), build: PASS.
- Prueba adicional PostgreSQL de conservación del XML e impuestos tras distribuir y reabrir:
  1/1 PASS. Total PostgreSQL focal: 8/8 PASS.
- `architecture:check`: PASS, cero infracciones nuevas; 140 pruebas del gate PASS.

## Archivos

Producción:

- `backend/src/ERP.Application/Modules/Companies/UseCases/PrecisionPolicy/CompanyPrecisionPolicyDtos.cs`
- `frontend/src/lib/config/precisionPolicy.config.ts`
- `frontend/src/modules/purchases/utils/purchaseCalc.ts`
- `frontend/src/modules/purchases/components/DistributeCostModal.tsx`

Pruebas y fixtures:

- `backend/src/ERP.Application.Tests/Companies/CompanyPrecisionPolicyUseCasesTests.cs`
- `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseXmlBreakdownPostgreSqlTests.cs`
- `frontend/src/lib/config/precisionPolicy.config.test.ts`
- `frontend/src/test/precisionPolicyFixture.ts`
- `frontend/src/modules/auth/syncCompanySelection.test.ts`
- `frontend/src/modules/auth/pages/CompanySelectPage.test.tsx`
- `frontend/src/modules/purchases/utils/purchaseCostDistributionPrecision.test.ts`
- `frontend/src/modules/purchases/utils/purchaseCalc.test.ts`
- `frontend/src/modules/purchases/components/DistributeCostModal.test.tsx`

Documentación: `STATUS.md`, `docs/architecture/data-standards.md`,
`docs/architecture/purchases.md` y esta evidencia. `PROGRESS.html` no existe en este checkout.

## Gate focal del propietario (PowerShell)

Desde `C:\ProyectCursor\erp-saas`, con Docker disponible para los contenedores desechables:

```powershell
dotnet test backend/src/ERP.Domain.Tests/ERP.Domain.Tests.csproj --filter FullyQualifiedName~PurchaseInvoiceDistributeAdditionalCostTests
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --filter "FullyQualifiedName~DistributePurchaseCostHandlerTests|FullyQualifiedName~CompanyPrecisionPolicyUseCasesTests"
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --filter FullyQualifiedName~PurchasesControllerDistributeCostTests
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --filter "FullyQualifiedName~PurchaseXmlBreakdownPostgreSqlTests.Cost_distribution_preserves|FullyQualifiedName~PurchaseXmlBreakdownPostgreSqlTests.Update_handler_saves_complete_group_or_preserves_original_draft"
Set-Location frontend
npx.cmd vitest run src/modules/purchases/utils/purchaseCostDistributionPrecision.test.ts src/modules/purchases/utils/purchaseCalc.test.ts src/modules/purchases/components/DistributeCostModal.test.tsx src/lib/config/precisionPolicy.config.test.ts src/modules/purchases/hooks/usePurchasesPage.pilotFix.test.ts src/modules/purchases/utils/purchaseXmlBreakdown.test.ts src/modules/auth/syncCompanySelection.test.ts src/modules/auth/pages/CompanySelectPage.test.tsx
npx.cmd tsc --noEmit
npm.cmd run lint
npm.cmd run build
npm.cmd run architecture:check
```

Pendiente: resultado PASS/FAIL del propietario y posterior revisión final. Sin staging, commit,
push ni deploy. Transporte XML y reintentos permanecen fuera de esta etapa.
