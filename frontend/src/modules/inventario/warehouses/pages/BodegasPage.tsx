import { useCallback, useEffect, useRef } from 'react';
import { useI18n } from '../../../../i18n/i18n';
import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';

import { useBodegasPage } from './useBodegasPage';
import { useWarehouseUiStore } from '../store/warehouseUiStore';

import { WarehouseResumenTab }  from '../components/WarehouseResumenTab';
import { WarehouseListadoTab }  from '../components/WarehouseListadoTab';
import { WarehouseFormTab }     from '../components/WarehouseFormTab';
import { WarehouseToastManager } from '../components/WarehouseToastManager';

import '../../../../styles/shared/products-catalog.css'; // prd-* shared tab/dashboard component styles
import './BodegasPage.css';                  // bod-* module-specific overrides

const TABS = [
  { id: 'resumen' as const, labelKey: 'warehouses.tabs.resumen', labelFb: 'Resumen',       icon: 'bar_chart_4_bars' },
  { id: 'listado' as const, labelKey: 'warehouses.tabs.listado', labelFb: 'Listado',        icon: 'view_list'       },
  { id: 'nuevo'   as const, labelKey: 'warehouses.tabs.nuevo',   labelFb: 'Nueva Bodega',   icon: 'add_box'         },
] as const;

export function BodegasPage() {
  const { t } = useI18n();
  const page = useBodegasPage();

  const activeTab        = useWarehouseUiStore((s) => s.activeTab);
  const editingWarehouse = useWarehouseUiStore((s) => s.editingWarehouse);
  const setActiveTab     = useWarehouseUiStore((s) => s.setActiveTab);
  const startEdit        = useWarehouseUiStore((s) => s.startEdit);
  const cancelEdit       = useWarehouseUiStore((s) => s.cancelEdit);
  const showToast        = useWarehouseUiStore((s) => s.showToast);
  const addActivity      = useWarehouseUiStore((s) => s.addActivity);

  const searchRef    = useRef<HTMLInputElement>(null);
  const prevSavingRef = useRef(false);

  // ── Ctrl+K ──────────────────────────────────────────────
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setActiveTab('listado');
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [setActiveTab]);

  // ── Detect successful save via saving transition ─────────
  useEffect(() => {
    const wasSaving = prevSavingRef.current;
    prevSavingRef.current = page.saving;

    if (wasSaving && !page.saving && !page.saveError && activeTab === 'nuevo') {
      const name = editingWarehouse?.name ?? '';
      if (editingWarehouse) {
        addActivity(name, 'updated');
        showToast(t('warehouses.updated.success', 'Bodega actualizada correctamente.'), 'success');
      } else {
        addActivity(page.items[0]?.name ?? name, 'created');
        showToast(t('warehouses.created.success', 'Bodega creada correctamente.'), 'success');
      }
      cancelEdit();
    }
  });


  // ── Toggle status ─────────────────────────────────────────
  const handleToggle = useCallback(async (row: Parameters<typeof page.toggleStatus>[0]) => {
    await page.toggleStatus(row);
    addActivity(row.name, row.isActive ? 'disabled' : 'enabled');
    showToast(
      row.isActive
        ? t('warehouses.toggle.success.disabled', 'Bodega desactivada.')
        : t('warehouses.toggle.success.activated', 'Bodega activada.'),
      'info',
    );
  }, [page, addActivity, showToast, t]);

  // ── Cancel (form tab) ─────────────────────────────────────
  const handleCancel = useCallback(() => {
    cancelEdit();
    page.closeModal();
  }, [cancelEdit, page]);

  if (!page.canView) return <NoAccessPage title={t('warehouses.title', 'Gestión de Bodegas')} />;

  const nuevoLabel = editingWarehouse
    ? t('warehouses.tabs.edit', 'Editar Bodega')
    : t('warehouses.tabs.nuevo', 'Nueva Bodega');
  const nuevoIcon  = editingWarehouse ? 'edit' : 'add_box';

  return (
    <ErpPageTemplate
      kicker={t('warehouses.kicker', 'Inventario')}
      title={t('warehouses.title', 'Gestión de Bodegas')}
      action={
        page.canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => { page.openCreateModal(); setActiveTab('nuevo'); }}
          >
            <span className="material-symbols-outlined">add</span>
            {t('warehouses.new', 'Nueva Bodega')}
          </ZHBtn>
        ) : null
      }
    >
      {page.error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix', 'Error:')} detail={page.error} />
      )}

      {/* Tab bar */}
      <div className="prd-tabs" role="tablist" aria-label={t('warehouses.title', 'Secciones de bodegas')}>
        {TABS.map((tab) => {
          const label  = tab.id === 'nuevo' ? nuevoLabel : t(tab.labelKey, tab.labelFb);
          const icon   = tab.id === 'nuevo' ? nuevoIcon  : tab.icon;
          const active = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={active}
              className={`prd-tab-btn ${active ? 'prd-tab-btn--active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <span className="material-symbols-outlined prd-tab-icon">{icon}</span>
              {label}
              {tab.id === 'nuevo' && editingWarehouse && (
                <span className="prd-tab-edit-badge" aria-hidden>●</span>
              )}
            </button>
          );
        })}
      </div>

      {/* Tab panels */}
      <div className="prd-tab-content">
        {activeTab === 'resumen' && (
          <WarehouseResumenTab warehouses={page.items} />
        )}

        {activeTab === 'listado' && (
          <WarehouseListadoTab
            warehouses={page.filtered}
            loading={page.loading}
            toggling={false}
            canUpdate={page.canUpdate}
            canDelete={page.canDelete}
            branchName={page.branchName}
            onEdit={async (row) => { startEdit(row); await page.openEditModal(row.id); }}
            onToggle={handleToggle}
            searchInputRef={searchRef}
          />
        )}

        {activeTab === 'nuevo' && page.canCreate && (
          <WarehouseFormTab
            editingId={page.editingId}
            editCode={page.editCode}
            saving={page.saving}
            saveError={page.saveError}
            branches={page.branches}
            register={page.form.register}
            errors={page.errors}
            onSave={() => void page.save()}
            onCancel={handleCancel}
          />
        )}
      </div>

      <WarehouseToastManager />
    </ErpPageTemplate>
  );
}
