import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { BranchFormModal } from '../components/BranchFormModal';
import { BranchesListSection } from '../components/BranchesListSection';
import { useBranchesPage } from '../hooks/useBranchesPage';

export function BranchesPage() {
  const ctx = useBranchesPage();
  const { t, canView, canCreate, loading, error, fetchList, openCreateModal } = ctx;

  if (!canView) return <NoAccessPage title={t('branches.title')} />;

  return (
    <div className="pg-page">
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.security')}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('branches.title')}</span>
          </nav>
          <h1 className="pg-title">{t('branches.title')}</h1>
          <p className="pg-subtitle">
            Gestione sus puntos de venta, encargados y parámetros operativos por sucursal.
          </p>
        </div>
        <div className="pg-header-right">
          <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void fetchList()}>
            <span className="material-symbols-outlined">refresh</span>
            {t('common.refresh') || 'Actualizar'}
          </button>
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              Nueva Sucursal
            </button>
          )}
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      <BranchesListSection {...ctx} />
      <BranchFormModal {...ctx} />
    </div>
  );
}
