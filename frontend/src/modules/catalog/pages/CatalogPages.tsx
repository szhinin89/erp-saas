import { CatalogSimplePage, type CatalogRow } from './CatalogSimplePage';
import { catalogService } from '../api/catalogService';

export { CatalogStructurePage } from './CatalogStructurePage';

function mapBasic(items: { id: string; code: string; name: string; isActive: boolean }[]): CatalogRow[] {
  return (items ?? []).map((x) => ({ id: x.id, code: x.code, name: x.name, isActive: x.isActive }));
}

export function TariffsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.tariffs.title"
      listTabLabelKey="catalog.tariffs.tabList"
      primaryCreateKey="catalog.tariffs.primaryCreate"
      viewPermissionKey="inventory.tariffs.view"
      createPermissionKey="inventory.tariffs.create"
      auditEntityType="Tariff"
      load={async () => mapBasic(await catalogService.tariffs(false))}
      create={async (p) => catalogService.createTariff({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}
