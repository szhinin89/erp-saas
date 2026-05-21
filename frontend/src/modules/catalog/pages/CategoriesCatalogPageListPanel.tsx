import { EmptyState, LoadingState, Badge } from '../../../components/PageShell';
import { ZHSearchBar as SearchBar } from '../../../components/shared/ZHSearchBar';
import { ZHField } from '../../../components/zh/ZHForm';
import { ZHGridRow } from '../../../components/zh/ZHLayout';
import type { ProductCategoryListItem } from '../api/catalogService';

type CategoriesCatalogPageListPanelProps = {
  t: (key: string) => string;
  canCreate: boolean;
  loading: boolean;
  items: ProductCategoryListItem[];
  listFiltered: ProductCategoryListItem[];
  lines: { id: string; code: string; name: string }[];
  filterLineId: string;
  setFilterLineId: (value: string) => void;
  listQuery: string;
  setListQuery: (value: string) => void;
  lineLabel: (row: ProductCategoryListItem) => string;
  onNew: () => void;
};

export function CategoriesCatalogPageListPanel({
  t,
  canCreate,
  loading,
  items,
  listFiltered,
  lines,
  filterLineId,
  setFilterLineId,
  listQuery,
  setListQuery,
  lineLabel,
  onNew,
}: CategoriesCatalogPageListPanelProps) {
  return (
    <>
      <ZHGridRow cols={1} className="zh-mb-12">
        <ZHField label={t('catalog.categories.line')}>
          <select value={filterLineId} onChange={(e) => setFilterLineId(e.target.value)} disabled={loading}>
            <option value="">{t('common.select')}</option>
            {lines.map((x) => (
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
          actionLabel={canCreate ? t('catalog.categories.listNewAction') : undefined}
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
              <th>{t('common.status')}</th>
            </tr>
          </thead>
          <tbody>
            {listFiltered.map((x) => (
              <tr key={x.id}>
                <td>{x.code}</td>
                <td>{x.name}</td>
                <td>{lineLabel(x)}</td>
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
