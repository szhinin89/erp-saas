import { NoAccessPage, PageShell } from '../../../components/PageShell';
import { ZHCard } from '../../../components/zh/ZHCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { SubcategoriesCatalogPageDataTab } from './SubcategoriesCatalogPageDataTab';
import { SubcategoriesCatalogPageListPanel } from './SubcategoriesCatalogPageListPanel';
import { useSubcategoriesCatalogPage } from './useSubcategoriesCatalogPage';

export function SubcategoriesCatalogPage() {
  const page = useSubcategoriesCatalogPage();

  if (!page.canView) {
    return <NoAccessPage title={page.t('catalog.subcategories.title')} />;
  }

  return (
    <PageShell
      kicker={page.t('app.nav.group.inventario')}
      title={page.t('catalog.subcategories.title')}
      action={
        page.canCreate && page.tab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => void page.onCreate()}
            disabled={
              page.saving ||
              page.loading ||
              !page.formWatch.code.trim() ||
              !page.formWatch.name.trim() ||
              !page.formWatch.lineId ||
              !page.formWatch.categoryId
            }
          >
            {page.saving ? page.t('common.saving') : page.t('catalog.subcategories.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      <ZHCard>
        {page.error ? <ZHPageNotice variant="error" message={page.t('common.errorPrefix')} detail={page.error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={page.tab === 'data' ? 'is-active' : ''} onClick={() => page.setTab('data')}>
            {page.t('common.formTab.data')}
          </button>
          <button type="button" className={page.tab === 'list' ? 'is-active' : ''} onClick={() => page.setTab('list')}>
            {page.t('catalog.subcategories.tabList')}
          </button>
        </div>

        {page.tab === 'data' && (
          <SubcategoriesCatalogPageDataTab
            t={page.t}
            canCreate={page.canCreate}
            saving={page.saving}
            loading={page.loading}
            lines={page.lines}
            formCategories={page.formCategories}
            formWatch={page.formWatch}
            lineIdField={page.lineIdField}
            setValue={page.setValue}
            register={page.register}
            errors={page.errors}
          />
        )}

        {page.tab === 'list' && (
          <SubcategoriesCatalogPageListPanel
            t={page.t}
            canCreate={page.canCreate}
            loading={page.loading}
            items={page.items}
            listFiltered={page.listFiltered}
            lines={page.lines}
            categories={page.categories}
            filterLineId={page.filterLineId}
            filterCategoryId={page.filterCategoryId}
            onFilterLineChange={page.onFilterLineChange}
            setFilterCategoryId={page.setFilterCategoryId}
            listQuery={page.listQuery}
            setListQuery={page.setListQuery}
            onNew={() => page.setTab('data')}
          />
        )}
      </ZHCard>
    </PageShell>
  );
}
