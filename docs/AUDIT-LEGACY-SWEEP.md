# Auditoría y barrido legacy — ERP SaaS

**Fecha:** 2026-05-25  
**Alcance:** Full sweep (frontend + backend)  
**Convención canónica:** `Subscriber` / `subscriber_id` (no coexistencia `Tenant` en código nuevo)

## Resumen ejecutivo

Se eliminó código legacy, duplicado y sin referencias que coexistía con la arquitectura oficial (Clean Architecture, CQRS/MediatR, modular monolith, React+TS). Los checks de arquitectura pasan **13/13** (`npm run architecture:check`).

---

## Archivos eliminados

### Backend (18 archivos)

| Área | Archivos |
|------|----------|
| CQRS huérfano | `RegisterSubscriberWithAdmin/*` (3), `CreateSubscriber/*` (3), `UpdateSubscriberSubscription/*` (3) |
| Tests muertos | `UpdateSubscriberSubscriptionHandlerTests.cs` |
| Aliases globales | `ERP.Application/EnglishAliases.cs`, `ERP.Infrastructure/EnglishAliases.cs` |
| API legacy | `ERP.API/Filters/DeprecatedApiAttribute.cs` |

**Motivo:** Duplicaban `PlatformCreateSubscriberWithAdminHandler` / `ChangePlatformSubscriberPlanCommand` ya expuestos en controllers platform. Ningún controller enviaba los commands eliminados.

### Frontend (47 archivos)

| Área | Archivos |
|------|----------|
| Platform shell legacy | `PlatformPanelPage*`, `usePlatformPanelPage`, tabs/modals panel, `PlatformMenuPlansHubPage`, `PlatformPlaceholderPage`, re-exports en `pages/` |
| Catálogo duplicado | `CategoriesCatalogPage*` (4), `SubcategoriesCatalogPage*` (4), `pages/CatalogPages.tsx` |
| UI kit muerto | `components/ui/*` (12) — reemplazado por `components/Card.tsx` único (único uso real era `Card`) |
| Hooks muertos | `hooks/useZHSearch.ts`, `useVentasFacturasList` (función removida del hook) |
| Pages huérfanas | `FeaturePlaceholderPage` (módulo + re-export) |

---

## Archivos fusionados / refactorizados

| Antes | Después |
|-------|---------|
| `PlatformPanelCreateSubscriberModal` + `PlatformPanelPageState` | `PlatformCreateSubscriberModal` con props explícitas |
| `CatalogPages.tsx` (8 exports duplicados) | Solo `TariffsCatalogPage` + re-export `CatalogStructurePage` |
| `bodegaService` alias | Uso directo de `warehouseService` |
| `platformService.updateSubscriberSubscription` | Eliminado; canónico: `changePlan` |
| `platformService.SubscriberDetailDto` | Eliminado; canónico: `PlatformSubscriberDetailDto` |
| `routes/index.ts` `getAppRoutes()` | Eliminado; `App.tsx` importa arrays de rutas directamente |
| `components/ui/*` (12 archivos) | `components/Card.tsx` (único primitivo usado) |
| `navConfig.mergeMissingStaticNavGroups()` | Eliminado (sin callers) |

---

## Violaciones corregidas

1. **Doble onboarding CQRS:** `RegisterSubscriberWithAdmin` vs `PlatformCreateSubscriberWithAdmin` → un solo handler platform.
2. **Doble cambio de plan:** `UpdateSubscriberSubscriptionCommand` vs `ChangePlatformSubscriberPlanCommand` → solo lifecycle controller.
3. **Shell platform sin ruta:** `PlatformPanelPage` conservado “por compatibilidad” pero sin router → eliminado; rutas canónicas: `PlatformOverviewPage`, `PlatformSubscribersPage`, `PlatformSubscriberDetailPage`.
4. **Catálogo triplicado:** páginas ZH Form de categorías/subcategorías vs `CatalogStructurePage` en cascada → eliminadas las duplicadas.
5. **Nav drift:** item menú apuntaba a `/products` → corregido a `/inventory/products` (redirect legacy se mantiene en rutas).
6. **Import roto post-eliminación:** `AuthController` importaba `ERP.API.Filters` → limpiado.

---

## Dependencias / código muerto eliminado

- Global usings `SalesInvoiceDetail`, `Expense` (sin uso real).
- Clases alias de dominio `[Obsolete]`: `PurchInvDetail`, `WithholdingCert` (tipos concretos; entidades canónicas intactas).
- Commands AR/AP write (`CreateArEntry`, `ApplyArPayment`, etc.) sin endpoint ni caller interno — eliminados; **queries aging conservadas** (`GetArAgingReportQuery`, `GetApAgingReportQuery`) usadas por `DashboardController`.

---

## Verificación

| Check | Resultado |
|-------|-----------|
| `npm run architecture:check` | 13/13 PASS |
| `dotnet build ERP.Application` | OK |
| `dotnet build ERP.Domain` | OK |
| `npm run build` (frontend) | OK |

---

## Riesgos / pendientes

Todos los ítems identificados en el barrido inicial fueron resueltos en el mismo ciclo (2026-05-25):

- **AR/AP writes:** restaurados handlers CQRS + `ArApController` (`POST /api/accounting/ar-ap/*`).
- **i18n platform detail:** claves `platform.subscriberDetail.*` en es/en/qu.
- **EF naming:** archivos renombrados a `PurchaseInvoiceDetail*`, `WithholdingCertificate*`.
- **Docs drift:** `PAGE-AUDIT.md` actualizado.
- **`_tenant` en Application:** renombrado a `_subscriber` en toda la capa Application.

---

## Source of truth consolidado

| Dominio | Canónico |
|---------|----------|
| Alta suscriptor + admin | `PlatformCreateSubscriberWithAdminCommand` → `POST /api/platform/subscribers` |
| Cambio de plan | `ChangePlatformSubscriberPlanCommand` → `PATCH /api/platform/subscribers/{id}/plan` |
| Estructura catálogo | `CatalogStructurePage` → `/inventory/catalog-structure` |
| Tarifas inventario | `TariffsCatalogPage` → `/inventory/tariffs` |
| Marcas/tipos/unidades | `BrandsPage`, `ProductTypesPage`, `UnitsPage` en `modules/catalog/pages/` |
| Bodegas | `warehouseService` |
| Detalle suscriptor platform | `PlatformSubscriberDetailDto` + `platformService` |
