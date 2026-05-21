import { EmptyState, LoadingState, Badge } from '../../../components/PageShell';
import { ZHSearchBar as SearchBar } from '../../../components/shared/ZHSearchBar';
import { ZHField } from '../../../components/zh/ZHForm';
import { ZHGridRow } from '../../../components/zh/ZHLayout';
import type { ProductCategoryListItem, ProductSubcategoryListItem } from '../api/catalogService';

type SubcategoriesCatalogPageListPanelProps = {
  t: (key: string) => string;
  canCreate: boolean;
  loading: boolean;
  items: ProductSubcategoryListItem[];
  listFiltered: ProductSubcategoryListItem[];
  lines: { id: string; code: string; name: string }[];
  categories: ProductCategoryListItem[];
  filterLineId: string;
  filterCategoryId: string;
  onFilterLineChange: (lineId: string) => void;
  setFilterCategoryId: (value: string) => void;
  listQuery: string;
  setListQuery: (value: string) => void;
  onNew: () => void;
};

export function SubcategoriesCatalogPageListPanel({
  t,
  canCreate,
  loading,
  items,
  listFiltered,
  lines,
  categories,
  filterLineId,
  filterCategoryId,
  onFilterLineChange,
  setFilterCategoryId,
  listQuery,
  setListQuery,
  onNew,
}: SubcategoriesCatalogPageListPanelProps) {
  return (
    <>
      <ZHGridRow cols={2} className="zh-mb-12">
        <ZHField label={t('catalog.categories.line')}>
          <select value={filterLineId} onChange={(e) => onFilterLineChange(e.target.value)} disabled={loading}>
            <option value="">{t('common.select')}</option>
            {lines.map((x) => (
              <option key={x.id} value={x.id}>
                {x.code} — {x.name}
              </option>
            ))}
          </select>
        </ZHField>
        <ZHField label={t('catalog.subcategories.category')}>
          <select value={filterCategoryId} onChange={(e) => setFilterCategoryId(e.target.value)} disabled={loading || !filterLineId}>
            <option value="">{t('common.select')}</option>
            {categories.map((x) => (
              <option key={x.id} value={x.id}>
                {x.code} — {x.name}
              </option>
            ))}
          </select>
        </ZHField>
      </ZHGridRow>
      <div className="zh-mb-12">
        <SearchBar
          searchQuery={listQuery}
          onSearch={setListQuery}
          onClearAll={() => setListQuery('')}
          filterValues={{}}
          placeholder={t('common.zhList.searchPlaceholder')}
          resultCount={listFiltered.length}
          entityLabel={t('common.zhList.entityLabel')}
          loading={loading}
          actionLabel={canCreate ? t('catalog.subcategories.listNewAction') : undefined}
          onAction={canCreate ? onNew : undefined}
        />
      </div>
      {loading ? (
        <LoadingState />
      ) : items.length === 0 ? (
        <EmptyState message={t('common.noData')} />
      ) : listFiltered.length === 0 ? (
        <EmptyState message={t('common.listTab.noMatch')} />
      ) : (
        <table className="table zh-mt-12">
          <thead>
            <tr>
              <th>{t('common.code')}</th>
              <th>{t('common.name')}</th>
              <th>{t('catalog.categories.line')}</th>
              <th>{t('catalog.subcategories.category')}</th>
              <th>{t('common.status')}</th>
            </tr>
          </thead>
          <tbody>
            {listFiltered.map((x) => (
              <tr key={x.id}>
                <td>{x.code}</td>
                <td>{x.name}</td>
                <td>{`${x.lineCode} — ${x.lineName}`}</td>
                <td>{`${x.categoryCode} — ${x.categoryName}`}</td>
                <td>
                  <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
