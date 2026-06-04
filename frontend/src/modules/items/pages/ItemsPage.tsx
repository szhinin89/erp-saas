import { useEffect } from 'react';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHPageNotice } from '../../../components/zh/ZHForm';

import { useItems } from '../hooks/useItems';
import { useItemUiStore } from '../store/itemUiStore';
import { ItemFormTabs } from '../components/ItemForm/ItemFormTabs';
import { ItemListTable } from '../components/ItemListTable';
import type { CreateItemFormValues } from '../schemas/createItemSchema';
import type { ItemDto } from '../../../types/items';

type TabId = 'resumen' | 'listado' | 'nuevo';

const TABS: { id: TabId; labelKey: string; labelFb: string; icon: string }[] = [
  { id: 'resumen',  labelKey: 'items.tabs.resumen',  labelFb: 'Resumen',    icon: 'bar_chart_4_bars' },
  { id: 'listado',  labelKey: 'items.tabs.listado',  labelFb: 'Listado',    icon: 'view_list' },
  { id: 'nuevo',    labelKey: 'items.tabs.nuevo',    labelFb: 'Nuevo Ítem', icon: 'add_box' },
];

export function ItemsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView   = canShow('items.view');
  const canCreate = canShow('items.create');
  const canEdit   = canShow('items.edit');

  const searchTerm     = useItemUiStore(s => s.searchTerm);
  const filterIsActive = useItemUiStore(s => s.filterIsActive);
  const filterItemType = useItemUiStore(s => s.filterItemType);
  const setSearchTerm  = useItemUiStore(s => s.setSearchTerm);

  const {
    items, loading, error, refetch,
    creating, createError, createItem,
    updating, updateError, updateItem,
    toggling, toggleError, toggleStatus,
  } = useItems({
    search:     searchTerm || undefined,
    isActive:   filterIsActive,
    itemType:   filterItemType,
    pageNumber: 1,
    pageSize:   50,
  });

  const activeTab    = useItemUiStore(s => s.activeTab);
  const editingItem  = useItemUiStore(s => s.editingItem);
  const setActiveTab = useItemUiStore(s => s.setActiveTab);
  const startEdit    = useItemUiStore(s => s.startEdit);
  const cancelEdit   = useItemUiStore(s => s.cancelEdit);
  const toast        = useItemUiStore(s => s.toast);
  const showToast    = useItemUiStore(s => s.showToast);
  const dismissToast = useItemUiStore(s => s.dismissToast);

  // Auto-dismiss toast after 4s
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(dismissToast, 4000);
    return () => clearTimeout(timer);
  }, [toast, dismissToast]);

  const handleSubmit = async (values: CreateItemFormValues): Promise<boolean> => {
    if (editingItem) {
      const updated = await updateItem({ id: editingItem.id, ...values });
      if (!updated) return false;
      showToast(t('items.updated.success', 'Ítem actualizado correctamente.'), 'success');
      cancelEdit();
      refetch();
      return true;
    }

    const created = await createItem({ ...values });
    if (!created) return false;
    showToast(t('items.created.success', 'Ítem creado correctamente.'), 'success');
    setActiveTab('listado');
    return true;
  };

  const handleToggle = async (item: ItemDto) => {
    const ok = await toggleStatus(item.id, !item.isActive);
    if (ok) {
      showToast(
        item.isActive
          ? t('items.disabled.success', 'Ítem deshabilitado.')
          : t('items.enabled.success', 'Ítem habilitado.'),
        'success'
      );
    }
  };

  if (!canView) return <NoAccessPage title={t('items.title', 'Ítems')} />;

  const anyError = error || createError || updateError || toggleError;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario', 'Inventario')}
      title={t('items.title', 'Ítems')}
      action={
        canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => { cancelEdit(); setActiveTab('nuevo'); }}
          >
            <span className="material-symbols-outlined">add</span>
            {t('items.new', 'Nuevo ítem')}
          </ZHBtn>
        ) : null
      }
    >
      {/* Toast */}
      {toast && (
        <div className={`zh-toast zh-toast--${toast.type}`} role="alert" onClick={dismissToast}>
          {toast.message}
        </div>
      )}

      {anyError && (
        <ZHPageNotice variant="error" message={anyError} style={{ marginBottom: 16 }} />
      )}

      {/* Tab bar */}
      <div className="prd-tabs" role="tablist">
        {TABS.map(tab => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`prd-tab-btn ${activeTab === tab.id ? 'prd-tab-btn--active' : ''}`}
            onClick={() => setActiveTab(tab.id)}
          >
            <span className="material-symbols-outlined">{tab.icon}</span>
            {t(tab.labelKey, tab.labelFb)}
          </button>
        ))}
      </div>

      {/* Tab panels */}
      <div className="prd-tab-content">
        {/* Resumen */}
        {activeTab === 'resumen' && (
          <div style={{ padding: 24 }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
              <SummaryCard label={t('items.summary.total', 'Total ítems')} value={items.length} />
              <SummaryCard label={t('items.summary.active', 'Activos')}    value={items.filter(i => i.isActive).length} />
              <SummaryCard label={t('items.summary.withLot', 'Con lotes')} value={items.filter(i => i.tracksLot).length} />
              <SummaryCard label={t('items.summary.services', 'Servicios')} value={items.filter(i => i.itemType === 'Service').length} />
            </div>
          </div>
        )}

        {/* Listado */}
        {activeTab === 'listado' && (
          <div>
            {/* Filters */}
            <div style={{ display: 'flex', gap: 12, padding: '16px 0', alignItems: 'center', flexWrap: 'wrap' }}>
              <input
                className="zh-input"
                placeholder={t('items.list.search', 'Buscar por SKU o nombre...')}
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
                style={{ minWidth: 240 }}
              />
              <select
                className="zh-select"
                value={filterIsActive === undefined ? '' : String(filterIsActive)}
                onChange={e => useItemUiStore.setState({ filterIsActive: e.target.value === '' ? undefined : e.target.value === 'true' })}
              >
                <option value="">{t('common.allStatuses', 'Todos los estados')}</option>
                <option value="true">{t('common.active', 'Activos')}</option>
                <option value="false">{t('common.inactive', 'Inactivos')}</option>
              </select>
              <select
                className="zh-select"
                value={filterItemType ?? ''}
                onChange={e => useItemUiStore.setState({ filterItemType: e.target.value || undefined })}
              >
                <option value="">{t('common.allTypes', 'Todos los tipos')}</option>
                <option value="Physical">Physical</option>
                <option value="Service">Service</option>
                <option value="Digital">Digital</option>
                <option value="Kit">Kit</option>
                <option value="Bundle">Bundle</option>
              </select>
            </div>

            <ItemListTable
              items={items}
              loading={loading}
              t={t}
              onEdit={(item) => canEdit ? startEdit(item) : undefined}
              onToggle={handleToggle}
              toggling={toggling}
            />
          </div>
        )}

        {/* Nuevo / Editar */}
        {activeTab === 'nuevo' && canCreate && (
          <div style={{ maxWidth: 900 }}>
            <ItemFormTabs
              submitting={creating || updating}
              editingItem={editingItem}
              onSubmit={handleSubmit}
              onCancel={() => cancelEdit()}
            />
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}

function SummaryCard({ label, value }: { label: string; value: number }) {
  return (
    <div style={{ background: 'var(--color-bg-subtle)', borderRadius: 8, padding: '20px 24px', border: '1px solid var(--color-border)' }}>
      <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--color-primary)' }}>{value}</div>
      <div style={{ fontSize: 13, color: 'var(--color-text-secondary)', marginTop: 4 }}>{label}</div>
    </div>
  );
}
