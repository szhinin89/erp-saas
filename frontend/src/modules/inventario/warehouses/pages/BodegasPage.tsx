import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { BodegasPageFormModal } from './BodegasPageFormModal';
import { BodegasPageListPanel } from './BodegasPageListPanel';
import { useBodegasPage } from './useBodegasPage';
import './BodegasPage.css';

export function BodegasPage() {
  const page = useBodegasPage();

  if (!page.canView) return <NoAccessPage title="Bodegas" />;

  return (
    <ErpPageTemplate
      kicker="Inventario"
      title="Gestión de Bodegas"
      subtitle="Administre sus almacenes, encargados y parámetros logísticos."
      action={
        <>
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={page.loading}
            onClick={() => void page.fetchList()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
          {page.canCreate && (
            <button className="zh-btn zh-btn--primary" type="button" onClick={page.openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              Nueva Bodega
            </button>
          )}
        </>
      }
    >
      {page.error && <ZHPageNotice variant="error" message={page.t('common.errorPrefix')} detail={page.error} />}

      {!page.loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">warehouse</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Bodegas</p>
              <p className="pg-kpi-value">{page.totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--success">
              <span className="material-symbols-outlined">task_alt</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Activas</p>
              <p className="pg-kpi-value">{page.totals.active}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivas</p>
              <p className="pg-kpi-value">{page.totals.inactive}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">business</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Sucursales con Bodega</p>
              <p className="pg-kpi-value">{page.totals.branches}</p>
            </div>
          </div>
        </div>
      )}

      <BodegasPageListPanel
        t={page.t}
        loading={page.loading}
        items={page.items}
        filtered={page.filtered}
        search={page.search}
        setSearch={page.setSearch}
        canUpdate={page.canUpdate}
        canDelete={page.canDelete}
        branchName={page.branchName}
        onEdit={page.openEditModal}
        onToggleStatus={page.toggleStatus}
      />

      <BodegasPageFormModal
        t={page.t}
        open={page.modalOpen}
        editingId={page.editingId}
        editCode={page.editCode}
        saving={page.saving}
        saveError={page.saveError}
        branches={page.branches}
        register={page.form.register}
        errors={page.errors}
        onClose={page.closeModal}
        onSave={() => void page.save()}
      />
    </ErpPageTemplate>
  );
}
