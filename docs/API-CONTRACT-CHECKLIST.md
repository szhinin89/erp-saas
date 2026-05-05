# Checklist de homologación API (ERP.API)

Fecha: 2026-05-05

## Contrato objetivo

- `ApiResponse<T>` uniforme para respuestas exitosas y de error.
- Status esperados por caso: `200/201/400/401/403/404/422`.
- `ValidationException` manejada por middleware como `422`.

## Checklist global

- [x] Controllers usan `ApiResultExtensions` / helpers `Api*`.
- [x] No quedan respuestas manuales `new ApiResponse<...>` en controllers.
- [x] Middleware mapea `ValidationException` a `422`.
- [x] Test de middleware actualizado a `422`.
- [x] Todos los endpoints con body validable documentan `422` en `[ProducesResponseType]`.

## Cobertura por controller (muestreo completo de ERP.API/Controllers)

- [x] `AccessController` homologado (`200/201/400/401/403` según endpoint).
- [x] `AuthController` homologado (`200/400/401/403`).
- [x] `SetupController` homologado (`200/400`).
- [x] `AccountsController` homologado (`200/201/400/401/404/422 runtime`).
- [x] `ProductsController` homologado (`200/201/400/401/404/422 runtime`).
- [x] `BranchesController` homologado (`200/201/400/404`).
- [x] `CustomersController` homologado (`200/201/400/404`).
- [x] `ProductLinesController` homologado (`200/201/400/401`).
- [x] `ProductCategoriesController` homologado (`200/201/400/401`).
- [x] `ProductSubcategoriesController` homologado (`200/201/400/401`).
- [x] `BrandsController` homologado (`200/201/400/401`).
- [x] `ProductTypesController` homologado (`200/201/400/401`).
- [x] `TaxRatesController` homologado (`200/201/400/401`).
- [x] `UnitsOfMeasureController` homologado (`200/201/400/401`).
- [x] `TariffsController` homologado (`200/201/400/401`).
- [x] `TenantsController` homologado (`200/201/400/401/403/404`).
- [x] `SecurityController` homologado (`200/400/401/403`).
- [x] `SaasPlansAdminController` homologado (`200/400/401/403`).
- [x] `SaasFeaturesAdminController` homologado (`200/400/401/403`).
- [x] `SuperAdminController` homologado (`200/400/401/403`).
- [x] `SuperAdminConfigController` homologado (`200/400`).
- [x] `PublicPlansController` homologado (`200`).
- [x] `PublicDeploymentController` homologado (`200`).
- [x] `ActivityController` homologado (`200/400`).
- [x] `GeographyController` homologado (`200/400`).

## Brecha detectada

- Sin brechas abiertas para el contrato objetivo de este checklist.

## Criterio de salida

Se considera cierre al 100% cuando:

1. Se mantenga `ApiResponse<T>` uniforme en todos los controllers.
2. Se conserve `ValidationException => 422` en middleware y tests.
3. Nuevos endpoints cumplan regla automática de `.cursor/rules/backend-api-contracts.mdc`.
