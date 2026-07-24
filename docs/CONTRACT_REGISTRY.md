# ERP Backend Contract Registry

**Status:** Authoritative — generated from controller scan.  
**Rule:** The frontend MUST NOT call any endpoint not listed here. Ghost calls must be guarded or removed.

---

## Base URL

Configured via `VITE_API_URL` environment variable (e.g. `http://localhost:5000`).  
All routes below are relative to that base, prefixed by `/api/v1` — except `/api/integration/v1/*` (Platform integration boundary) and `/api/dev/*` (dev-only diagnostics).  
Single centralized HTTP client: `frontend/src/modules/lib/api.ts`.

---

## Auth — `api/v1/auth`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| POST | `/api/v1/auth/login` | Anonymous | `LoginCommand` | `AuthResponseDto` |
| POST | `/api/v1/auth/refresh` | Anonymous | `RefreshRequest?` | `AuthResponseDto` |
| POST | `/api/v1/auth/logout` | Anonymous | `LogoutRequest?` | `string` |
| POST | `/api/v1/auth/forgot-password` | Anonymous | `ForgotPasswordCommand` | `object` |
| POST | `/api/v1/auth/reset-password` | Anonymous | `ResetPasswordWithTokenCommand` | `object` |
| GET | `/api/v1/auth/my-companies` | Bearer | — | `AccessibleCompanyDto[]` |
| POST | `/api/v1/auth/switch-company` | Bearer | `SwitchCompanyRequest` | `AuthResponseDto` |

---

## Session — `api/v1/me`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/me/menu` | Session | — | `NavMenuGroupDto[]` |

---

## Setup — `api/v1/setup`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/setup/status` | Anonymous | — | `SetupStatusDto` |
| POST | `/api/v1/setup/admin` | Anonymous | `CreateInitialAdminCommand` | `string` |

---

## Public — `api/v1/public`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/public/deployment` | Anonymous | — | deployment config object |

---

## Dashboard — `api/v1/dashboard`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/dashboard/kpis` | Bearer | `asOf?` (query, DateTime) | `DashboardKpisDto` |

---

## IAM Profiles — `api/v1/admin/iam`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/admin/iam/profiles` | Session | `onlyActive?` (query, bool) | `ProfileDto[]` |
| POST | `/api/v1/admin/iam/profiles` | Session + `perm:access.profiles.view` | `CreateProfileCommand` | `ProfileDto` |
| PUT | `/api/v1/admin/iam/profiles/{profileId}` | Session + `perm:access.profiles.view` | `UpdateProfileCommand` | `ProfileDto` |
| PUT | `/api/v1/admin/iam/profiles/{profileId}/permissions` | Session + `perm:access.profiles.view` | `UpsertProfilePermissionsCommand` | `PermissionUpsertResultDto` |
| GET | `/api/v1/admin/iam/profiles/{profileId}/permissions` | Session + `perm:access.profiles.view` | — | `ProfilePermissionsDto` |
| GET | `/api/v1/admin/iam/profiles/{profileId}/permission-audit` | Session + `perm:access.profiles.view` | — | `ProfilePermissionAuditDto` |
| GET | `/api/v1/admin/iam/me/permissions` | Session | — | `MyPermissionsDto` |

---

## Activity — `api/v1/admin/activity`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/admin/activity/my` | `perm:admin.activity.view` | `module?`, `page`, `pageSize` | `UserActivityDto[]` |
| GET | `/api/v1/admin/activity/entity` | `perm:admin.activity.view` | `entityType`, `entityId`, `take` | `UserActivityDto[]` |

---

## Security — `api/v1/security`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/security/admin-matrix` | Session + Admin role | — | `object` |
| PUT | `/api/v1/security/admin-scopes` | Session + Admin role | `UpsertSecurityAdminScopesCommand` | `object` |

---

## Companies — `api/v1/companies`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/companies` | Bearer + `perm:companies.view` | `activeOnly?` (bool) | `CompanyListItemDto[]` |
| GET | `/api/v1/companies/current` | Bearer + `perm:companies.view` | — | `CompanyDetailDto` |
| GET | `/api/v1/companies/{id}` | Bearer + `perm:companies.view` | — | `CompanyDetailDto` |
| POST | `/api/v1/companies` | Bearer + `perm:companies.create` | `CreateCompanyCommand` | `CompanyDetailDto` |
| PUT | `/api/v1/companies/{id}` | Bearer + `perm:companies.update` | `UpdateCompanyCommand` | `CompanyDetailDto` |

---

## Branches — `api/v1/settings/branches`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/settings/branches` | Bearer + `perm:settings.branches.view` | `active?`, `search?` | `BranchDto[]` |
| GET | `/api/v1/settings/branches/{id}` | Bearer + `perm:settings.branches.view` | — | `BranchDetailDto` |
| POST | `/api/v1/settings/branches` | Bearer + `perm:settings.branches.create` | `CreateBranchCommand` | `BranchDto` |
| PUT | `/api/v1/settings/branches/{id}` | Bearer + `perm:settings.branches.update` | `UpdateBranchCommand` | `BranchDto` |
| PATCH | `/api/v1/settings/branches/{id}/disable` | Bearer + `perm:settings.branches.delete` | — | — |
| PATCH | `/api/v1/settings/branches/{id}/enable` | Bearer + `perm:settings.branches.update` | — | — |

> ⚠️ Establishments (`/api/v1/settings/branches/{id}/establishments`) — **NOT implemented**.  
> ⚠️ Emission points (`/api/v1/settings/establishments/{id}/emission-points`) — **NOT implemented**.

---

## Geography — `api/v1/settings/geography`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/settings/geography/countries` | `perm:settings.geography.view` | — | `GeographyItemDto[]` |
| GET | `/api/v1/settings/geography/provinces` | `perm:settings.geography.view` | `countryId` (query) | `GeographyItemDto[]` |
| GET | `/api/v1/settings/geography/cantons` | `perm:settings.geography.view` | `provinceId` (query) | `GeographyItemDto[]` |
| GET | `/api/v1/settings/geography/parishes` | `perm:settings.geography.view` | `cantonId` (query) | `GeographyItemDto[]` |

---

## Items — `api/v1/items`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/items` | Bearer + `perm:items.view` | search, sku, isActive, isForSale, … (query) | `GetItemsResponse` |
| GET | `/api/v1/items/report` | Bearer + `perm:items.view` | (same as list) | `GetItemsResponse` |
| GET | `/api/v1/items/{id}` | Bearer + `perm:items.view` | — | `ItemDetailDto` |
| GET | `/api/v1/items/{id}/full-report` | Bearer + `perm:items.view` | — | `ItemFullReportDto` |
| POST | `/api/v1/items` | Bearer + `perm:items.create` | `CreateItemCommand` | `ItemDto` |
| PUT | `/api/v1/items/{id}` | Bearer + `perm:items.edit` | `UpdateItemCommand` | — |
| PATCH | `/api/v1/items/{id}/disable` | Bearer + `perm:items.edit` | — | — |
| PATCH | `/api/v1/items/{id}/enable` | Bearer + `perm:items.edit` | — | — |
| POST | `/api/v1/items/{id}/variants` | Bearer + `perm:items.edit` | `AddVariantRequest` | `ItemVariantDto` |
| PUT | `/api/v1/items/{id}/variants/{variantId}` | Bearer + `perm:items.edit` | `UpdateVariantRequest` | — |
| PATCH | `/api/v1/items/{id}/variants/{variantId}/disable` | Bearer + `perm:items.edit` | — | — |
| PATCH | `/api/v1/items/{id}/variants/{variantId}/enable` | Bearer + `perm:items.edit` | — | — |
| PUT | `/api/v1/items/{id}/images` | Bearer + `perm:items.edit` | `ReplaceImagesRequest` | `ItemDetailDto` |
| PATCH | `/api/v1/items/{id}/images/{imageId}/disable` | Bearer + `perm:items.edit` | — | — |
| PUT | `/api/v1/items/{id}/unit-conversions` | Bearer + `perm:items.edit` | `ReplaceConversionsRequest` | `ItemDetailDto` |
| PUT | `/api/v1/items/{id}/substitutes` | Bearer + `perm:items.edit` | `ReplaceSubstitutesRequest` | `ItemDetailDto` |
| PUT | `/api/v1/items/{id}/packaging-levels` | Bearer + `perm:items.edit` | `ReplacePackagingRequest` | `ItemDetailDto` |
| GET | `/api/v1/items/{id}/stock` | Bearer + `perm:items.view` | `warehouseId?` (query) | `StockByItemDto[]` |
| GET | `/api/v1/items/{id}/kardex` | Bearer + `perm:items.view` | variantId, warehouseId, from, to, page, size | `KardexResponse` |

---

## Item Catalog — `api/v1/catalog`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/catalog/item-families` | Bearer + `perm:catalog.manage` | `isActive?` | `ItemFamilyDto[]` |
| GET | `/api/v1/catalog/item-families/{id}` | Bearer + `perm:catalog.manage` | — | `ItemFamilyDto` |
| POST | `/api/v1/catalog/item-families` | Bearer + `perm:catalog.manage` | `CreateItemFamilyCommand` | `ItemFamilyDto` |
| PUT | `/api/v1/catalog/item-families/{id}` | Bearer + `perm:catalog.manage` | `UpdateItemFamilyCommand` | `ItemFamilyDto` |
| PATCH | `/api/v1/catalog/item-families/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/item-families/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |
| GET | `/api/v1/catalog/item-categories` | Bearer + `perm:catalog.manage` | `familyId?`, `isActive?` | `ItemCategoryDto[]` |
| GET | `/api/v1/catalog/item-categories/{id}` | Bearer + `perm:catalog.manage` | — | — |
| POST | `/api/v1/catalog/item-categories` | Bearer + `perm:catalog.manage` | `CreateItemCategoryCommand` | — |
| PUT | `/api/v1/catalog/item-categories/{id}` | Bearer + `perm:catalog.manage` | `UpdateItemCategoryCommand` | — |
| PATCH | `/api/v1/catalog/item-categories/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/item-categories/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |
| GET | `/api/v1/catalog/item-subcategories` | Bearer + `perm:catalog.manage` | `categoryId?`, `isActive?` | `ItemSubcategoryDto[]` |
| GET | `/api/v1/catalog/item-subcategories/{id}` | Bearer + `perm:catalog.manage` | — | — |
| POST | `/api/v1/catalog/item-subcategories` | Bearer + `perm:catalog.manage` | `CreateItemSubcategoryCommand` | — |
| PUT | `/api/v1/catalog/item-subcategories/{id}` | Bearer + `perm:catalog.manage` | `UpdateItemSubcategoryCommand` | — |
| PATCH | `/api/v1/catalog/item-subcategories/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/item-subcategories/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |
| GET | `/api/v1/catalog/brands` | Bearer + `perm:catalog.manage` | `isActive?` | `BrandDto[]` |
| GET | `/api/v1/catalog/brands/{id}` | Bearer + `perm:catalog.manage` | — | — |
| POST | `/api/v1/catalog/brands` | Bearer + `perm:catalog.manage` | `CreateBrandCommand` | — |
| PUT | `/api/v1/catalog/brands/{id}` | Bearer + `perm:catalog.manage` | `UpdateBrandCommand` | — |
| PATCH | `/api/v1/catalog/brands/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/brands/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |
| GET | `/api/v1/catalog/attribute-groups` | Bearer + `perm:catalog.manage` | `isActive?` | `AttributeGroupDto[]` |
| GET | `/api/v1/catalog/attribute-groups/{id}` | Bearer + `perm:catalog.manage` | — | — |
| POST | `/api/v1/catalog/attribute-groups` | Bearer + `perm:catalog.manage` | `CreateAttributeGroupCommand` | — |
| PUT | `/api/v1/catalog/attribute-groups/{id}` | Bearer + `perm:catalog.manage` | `UpdateAttributeGroupCommand` | — |
| PATCH | `/api/v1/catalog/attribute-groups/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/attribute-groups/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |
| GET | `/api/v1/catalog/attribute-definitions` | Bearer + `perm:catalog.manage` | `groupId?`, `isActive?` | `AttributeDefinitionDto[]` |
| GET | `/api/v1/catalog/attribute-definitions/{id}` | Bearer + `perm:catalog.manage` | — | — |
| POST | `/api/v1/catalog/attribute-definitions` | Bearer + `perm:catalog.manage` | `CreateAttributeDefinitionCommand` | — |
| PUT | `/api/v1/catalog/attribute-definitions/{id}` | Bearer + `perm:catalog.manage` | `UpdateAttributeDefinitionCommand` | — |
| PATCH | `/api/v1/catalog/attribute-definitions/{id}/enable` | Bearer + `perm:catalog.manage` | — | — |
| PATCH | `/api/v1/catalog/attribute-definitions/{id}/disable` | Bearer + `perm:catalog.manage` | — | — |

> ⚠️ `/api/v1/catalog/sri-uom` — **NOT implemented** (no SRI UOM catalog endpoint exists).  
> ⚠️ `/api/v1/catalog/sri-vat-rates` — **NOT implemented**.  
> ⚠️ `/api/v1/catalogs/sri/id-types` — **NOT implemented** (note: wrong prefix `catalogs` vs `catalog`).

---

## Master Data — Business Partners — `api/v1/master/business-partners`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/v1/master/business-partners` | Session + `perm:masterdata.business_partner.view` | q, isActive, roles[], skip, take | `PagedResult<BusinessPartnerSummaryDto>` |
| GET | `/api/v1/master/business-partners/{id}` | Session + `perm:masterdata.business_partner.view` | — | `BusinessPartnerDetailDto` |
| POST | `/api/v1/master/business-partners` | Session + `perm:masterdata.business_partner.create` | `CreateBusinessPartnerRequest` | `BusinessPartnerSummaryDto` |
| PUT | `/api/v1/master/business-partners/{id}` | Session + `perm:masterdata.business_partner.update` | `UpdateBusinessPartnerRequest` | `BusinessPartnerSummaryDto` |
| PATCH | `/api/v1/master/business-partners/{id}/identification` | Session + `perm:masterdata.business_partner.update` | `UpdateIdentificationRequest` | `BusinessPartnerSummaryDto` |
| PATCH | `/api/v1/master/business-partners/{id}/activate` | Session + `perm:masterdata.business_partner.update` | — | `bool` |
| DELETE | `/api/v1/master/business-partners/{id}` | Session + `perm:masterdata.business_partner.disable` | — | `bool` |
| GET | `/api/v1/master/business-partners/{bpId}/roles` | Session + view | `onlyActive?` | `BusinessPartnerRoleDto[]` |
| POST | `/api/v1/master/business-partners/{bpId}/roles` | Session + update | `AssignRoleRequest` | `BusinessPartnerRoleDto` |
| DELETE | `/api/v1/master/business-partners/{bpId}/roles/{roleId}` | Session + update | — | `bool` |
| PATCH | `/api/v1/master/business-partners/{bpId}/roles/{roleId}/supplier-config` | Session + update | `SupplierConfigRequest` | `BusinessPartnerRoleDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/roles/{roleId}/supplier-classification` | Session + update | `SupplierClassificationRequest` | `BusinessPartnerRoleDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/roles/{roleId}/carrier-config` | Session + update | `CarrierConfigRequest` | `BusinessPartnerRoleDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/roles/{roleId}/customer-config` | Session + update | `CustomerConfigRequest` | `BusinessPartnerRoleDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/roles/{roleId}/notes` | Session + update | `UpdateRoleNotesRequest` | `bool` |
| GET | `/api/v1/master/business-partners/{bpId}/locations` | Session + view | `onlyActive?` | `BpLocationDto[]` |
| GET | `/api/v1/master/business-partners/{bpId}/locations/{locationId}` | Session + view | — | `BpLocationDto` |
| POST | `/api/v1/master/business-partners/{bpId}/locations` | Session + update | `CreateLocationRequest` | `BpLocationDto` |
| PUT | `/api/v1/master/business-partners/{bpId}/locations/{locationId}` | Session + update | `UpdateLocationRequest` | `BpLocationDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/locations/{locationId}/set-primary` | Session + update | — | `bool` |
| PATCH | `/api/v1/master/business-partners/{bpId}/locations/{locationId}/activate` | Session + update | — | `bool` |
| DELETE | `/api/v1/master/business-partners/{bpId}/locations/{locationId}` | Session + update | — | `bool` |
| GET | `/api/v1/master/business-partners/{bpId}/contacts` | Session + view | `onlyActive?` | `BpContactDto[]` |
| GET | `/api/v1/master/business-partners/{bpId}/contacts/{contactId}` | Session + view | — | `BpContactDto` |
| POST | `/api/v1/master/business-partners/{bpId}/contacts` | Session + update | `CreateContactRequest` | `BpContactDto` |
| PUT | `/api/v1/master/business-partners/{bpId}/contacts/{contactId}` | Session + update | `UpdateContactRequest` | `BpContactDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/contacts/{contactId}/set-primary` | Session + update | — | `bool` |
| PATCH | `/api/v1/master/business-partners/{bpId}/contacts/{contactId}/activate` | Session + update | — | `bool` |
| DELETE | `/api/v1/master/business-partners/{bpId}/contacts/{contactId}` | Session + update | — | `bool` |
| GET | `/api/v1/master/business-partners/{bpId}/trading-settings` | Session + view | — | `CompanyBpTradingSettingsDto` |
| PUT | `/api/v1/master/business-partners/{bpId}/trading-settings` | Session + `configure_company` | `UpsertTradingSettingsRequest` | `CompanyBpTradingSettingsDto` |
| PATCH | `/api/v1/master/business-partners/{bpId}/trading-settings/block` | Session + `configure_company` | `BlockRequest` | `bool` |
| PATCH | `/api/v1/master/business-partners/{bpId}/trading-settings/unblock` | Session + `configure_company` | — | `bool` |

---

## Integration API — `api/integration/v1`

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| POST | `/api/integration/v1/tenants` | IntegrationApi policy | `IntegrationTenantCreateRequest` | — |
| GET | `/api/integration/v1/tenants/{id}/status` | IntegrationApi policy | — | — |
| PUT | `/api/integration/v1/tenants/{id}/activate` | IntegrationApi policy | — | — |
| PUT | `/api/integration/v1/tenants/{id}/suspend` | IntegrationApi policy | — | — |
| POST | `/api/integration/v1/companies` | IntegrationApi policy | `IntegrationCompanyCreateRequest` | — |
| GET | `/api/integration/v1/companies/{id}/status` | IntegrationApi policy | — | — |
| PUT | `/api/integration/v1/companies/{id}/activate` | IntegrationApi policy | — | — |
| PUT | `/api/integration/v1/companies/{id}/suspend` | IntegrationApi policy | — | — |

---

## Dev Only (non-production)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| GET | `/api/dev/redis-health` | Anonymous | Returns 404 outside Development env |
| GET | `/api/dev/cache-metrics` | Anonymous | Returns 404 outside Development env |

---

## Ghost Endpoint Inventory (Frontend calls — Backend NOT implemented)

The following frontend service files call endpoints that have no corresponding backend controller.
They will return 404. Frontend pages that use them will show empty state or error.

| Frontend Service | Ghost Endpoint(s) | Backend Module Needed |
|---|---|---|
| `accountingService.ts` | `GET/POST /api/v1/finance/accounts`, `GET/POST /api/v1/finance/accounts/journal-entries`, etc. | Finance/Accounting |
| `accountingConfigService.ts` | `GET/PUT /api/v1/finance/config`, `GET/POST/DELETE /api/v1/finance/config/gastos` | Finance config |
| `salesInvoicesService.ts` | `GET/POST /api/v1/sales/invoices/*` | Sales module |
| `salesOrderService.ts` | `GET/POST /api/v1/sales/orders/*` | Sales module |
| `quoteService.ts` | `GET/POST /api/v1/sales/quotes/*` | Sales module |
| `creditNotesService.ts` | `GET/POST /api/v1/sales/credit-notes/*` | Sales module |
| `withholdingReceivedService.ts` | `GET/POST /api/v1/sales/withholding-received` | Sales module |
| `purchaseInvoicesService.ts` | `GET/POST /api/v1/purchases/invoices/*` | Purchasing module |
| `purchaseOrderService.ts` | `GET/POST /api/v1/purchases/orders/*` | Purchasing module |
| `purchaseCreditNotesService.ts` | `GET/POST/PUT /api/v1/purchases/credit-notes/*` | Purchasing module |
| `withholdingIssuedService.ts` | `GET/PUT /api/v1/purchases/withholding-issued/*` | Purchasing module |
| `adjustmentService.ts` | `GET/POST /api/v1/inventory/adjustments/*` | Inventory module |
| `kardexService.ts` | `GET /api/v1/inventory/kardex/*` | Inventory module |
| `stockService.ts` | `GET /api/v1/inventory/stock/*` | Inventory module |
| `transferService.ts` | `GET/POST /api/v1/inventory/transfers/*` | Inventory module |
| `warehouseService.ts` | `GET/POST/PUT /api/v1/inventory/warehouses/*` | Inventory module |
| `inventoryItemService.ts` | `GET/POST /api/v1/inventory/lots`, `GET/POST /api/v1/inventory/serials` | Inventory module |
| `establishmentService.ts` | `GET/POST/PUT /api/v1/settings/branches/{id}/establishments/*` | Branches settings |
| `emissionPointService.ts` | `GET/POST/PUT /api/v1/settings/establishments/{id}/emission-points/*` | Branches settings |
| `billingSettingsService.ts` | `GET/PUT /api/v1/settings/ride` | SRI/Billing config |
| `sriService.ts` | `GET/PUT /api/v1/settings/sri` | SRI config |
| `carrierService.ts` | `GET/POST/PUT /api/v1/logistics/carriers/*` | Logistics module |
| `cashBankService.ts` | `GET /api/v1/cash/bank/*` | Cash/Bank module |
| `expensesService.ts` | `GET/POST /api/v1/expenses/*` | Expenses module |
| `digitalItemService.ts` | `GET/POST /api/v1/digital-items/*` | Digital items |
| `kitService.ts` | `GET/POST /api/v1/kits/*` | Kits |
| `pricingService.ts` | `GET/POST /api/v1/pricing/*` | Pricing |
| `supplierCatalogService.ts` | `GET/POST /api/v1/supplier-catalog/*` | Supplier catalog |
| `ItemFormTabs.tsx` (direct fetch) | `/api/v1/catalog/sri-uom`, `/api/v1/catalog/sri-vat-rates` | SRI catalog lookup |
| `useSriIdTypes.ts` (direct fetch) | `/api/v1/catalogs/sri/id-types` | SRI catalog lookup |

---

## Rules

1. **No frontend code may call an endpoint not in this registry.**
2. Endpoints marked ⚠️ are known gaps — use `apiContractValidator` to log warnings in dev.
3. When a new backend controller is added, update this file in the same PR.
4. Route format: `/api/v1/{bounded-context}/{resource}` (or `/api/integration/v1/*`, `/api/dev/*` for the documented exceptions) — no aliases, no duplicates.
