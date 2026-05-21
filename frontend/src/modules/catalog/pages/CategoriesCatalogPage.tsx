import { NoAccessPage, PageShell } from '../../../components/PageShell';
import { ZHCard } from '../../../components/zh/ZHCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { CategoriesCatalogPageDataTab } from './CategoriesCatalogPageDataTab';
import { CategoriesCatalogPageListPanel } from './CategoriesCatalogPageListPanel';
import { useCategoriesCatalogPage } from './useCategoriesCatalogPage';

export function CategoriesCatalogPage() {
  const page = useCategoriesCatalogPage();

  if (!page.canView) {
    return <NoAccessPage title={page.t('catalog.categories.title')} />;
  }

  return (
    <PageShell
      kicker={page.t('app.nav.group.inventario')}
      title={page.t('catalog.categories.title')}
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
              !page.formWatch.lineId
            }
          >
            {page.saving ? page.t('common.saving') : page.t('catalog.categories.primaryCreate')}
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
            {page.t('catalog.categories.tabList')}
          </button>
        </div>

        {page.tab === 'data' && (
          <CategoriesCatalogPageDataTab
            t={page.t}
            canCreate={page.canCreate}
            saving={page.saving}
            loading={page.loading}
            lines={page.lines}
            register={page.form.register}
            errors={page.errors}
          />
        )}

        {page.tab === 'list' && (
          <CategoriesCatalogPageListPanel
            t={page.t}
            canCreate={page.canCreate}
            loading={page.loading}
            items={page.items}
            listFiltered={page.listFiltered}
            lines={page.lines}
            filterLineId={page.filterLineId}
            setFilterLineId={page.setFilterLineId}
            listQuery={page.listQuery}
            setListQuery={page.setListQuery}
            lineLabel={page.lineLabel}
            onNew={() => page.setTab('data')}
          />
        )}
      </ZHCard>
    </PageShell>
  );
}
