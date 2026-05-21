import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { BranchFormModal } from '../components/BranchFormModal';
import { BranchesListSection } from '../components/BranchesListSection';
import { useBranchesPage } from '../hooks/useBranchesPage';
import './branches-page.css';

export function BranchesPage() {
  const ctx = useBranchesPage();
  const { t, canView, canCreate, loading, error, fetchList, openCreateModal } = ctx;

  if (!canView) return <NoAccessPage title={t('branches.title')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.configuracion')}
      title={t('branches.title')}
      subtitle={t('branches.subtitle')}
      action={
        <>
          <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void fetchList()}>
            <span className="material-symbols-outlined">refresh</span>
            {t('common.refresh')}
          </button>
          {canCreate ? (
            <button className="zh-btn zh-btn--primary" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              {t('branches.list.newAction')}
            </button>
          ) : null}
        </>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      <BranchesListSection {...ctx} />
      <BranchFormModal {...ctx} />
    </ErpPageTemplate>
  );
}
