import { CatalogSimplePage, type CatalogRow } from './CatalogSimplePage';
import { catalogService } from '../api/catalogService';

export { CatalogStructurePage } from './CatalogStructurePage';
export { CategoriesCatalogPage } from './CategoriesCatalogPage';
export { SubcategoriesCatalogPage } from './SubcategoriesCatalogPage';

function mapBasic(items: { id: string; code: string; name: string; isActive: boolean }[]): CatalogRow[] {
  return (items ?? []).map((x) => ({ id: x.id, code: x.code, name: x.name, isActive: x.isActive }));
}

export function BrandsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.brands.title"
      listTabLabelKey="catalog.brands.tabList"
      primaryCreateKey="catalog.brands.primaryCreate"
      viewPermissionKey="inventory.brands.view"
      createPermissionKey="inventory.brands.create"
      auditEntityType="Brand"
      load={async () => mapBasic(await catalogService.brands(false))}
      create={async (p) => catalogService.createBrand({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductTypesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productTypes.title"
      listTabLabelKey="catalog.productTypes.tabList"
      primaryCreateKey="catalog.productTypes.primaryCreate"
      viewPermissionKey="inventory.product-types.view"
      createPermissionKey="inventory.product-types.create"
      load={async () => mapBasic(await catalogService.productTypes(false))}
      create={async (p) => catalogService.createProductType({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function UnitsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.units.title"
      listTabLabelKey="catalog.units.tabList"
      primaryCreateKey="catalog.units.primaryCreate"
      viewPermissionKey="inventory.units.view"
      createPermissionKey="inventory.units.create"
      load={async () => mapBasic(await catalogService.units(false))}
      create={async (p) => catalogService.createUnit({ code: String(p.code ?? ''), name: String(p.name ?? ''), symbol: undefined })}
    />
  );
}

export function TariffsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.tariffs.title"
      listTabLabelKey="catalog.tariffs.tabList"
      primaryCreateKey="catalog.tariffs.primaryCreate"
      viewPermissionKey="inventory.tariffs.view"
      createPermissionKey="inventory.tariffs.create"
      load={async () => mapBasic(await catalogService.tariffs(false))}
      create={async (p) => catalogService.createTariff({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductLinesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productLines.title"
      listTabLabelKey="catalog.productLines.tabList"
      primaryCreateKey="catalog.productLines.primaryCreate"
      viewPermissionKey="inventory.product-lines.view"
      createPermissionKey="inventory.product-lines.create"
      load={async () => mapBasic(await catalogService.productLines({ activeStatus: 'all' }))}
      create={async (p) => catalogService.createProductLine({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

// TaxRatesCatalogPage eliminada: las tarifas SRI vienen de sri_vat_rate (datos oficiales pre-cargados,
// no editables por el subscriber). Se configuran en el producto al momento de crearlo/editarlo.
