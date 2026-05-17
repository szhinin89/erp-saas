import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { catalogService, type CatalogItem } from '../services/catalogService';
import { ZHField, ZHBtn } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { NoAccessPage } from '../components/PageShell';
import { formatApiRequestError } from '../modules/lib/apiError';

/* ── Form schema ────────────────────────────────────────────── */
const productTypeSchema = z.object({
  code: z.string().min(1, 'Required').max(20),
  name: z.string().min(1, 'Required').max(120),
});
type ProductTypeFormValues = z.infer<typeof productTypeSchema>;

/* ── Main page ──────────────────────────────────────────────── */
export function ProductTypesPage() {
  const { t } = useI18n();
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canView   = isAdmin || hasPerm('inventario.productTypes.view');
  const canCreate = isAdmin || hasPerm('inventario.productTypes.create');
  const canUpdate = isAdmin || hasPerm('inventario.productTypes.update');
  const canDelete = isAdmin || hasPerm('inventario.productTypes.delete');

  /* ── Data state ───────────────────────────────────────────── */
  const [items,   setItems]   = useState<CatalogItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');
  const [search,  setSearch]  = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');

  /* ── Modal state ──────────────────────────────────────────── */
  type ModalMode = { kind: 'create' } | { kind: 'edit'; item: CatalogItem };
  const [modal,      setModal]      = useState<ModalMode | null>(null);
  const [saving,     setSaving]     = useState(false);
  const [modalError, setModalError] = useState('');

  const { register, handleSubmit, reset, formState: { errors } } = useForm<ProductTypeFormValues>({
    resolver: zodResolver(productTypeSchema),
    defaultValues: { code: '', name: '' },
  });

  /* ── Load ─────────────────────────────────────────────────── */
  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setItems(await catalogService.productTypes(false) ?? []);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('productTypes.error.load') }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  /* ── Filtered list ────────────────────────────────────────── */
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((item) => {
      const matchStatus =
        statusFilter === 'all' ||
        (statusFilter === 'active'   && item.isActive) ||
        (statusFilter === 'inactive' && !item.isActive);
      const matchSearch = !q || `${item.code} ${item.name}`.toLowerCase().includes(q);
      return matchStatus && matchSearch;
    });
  }, [items, search, statusFilter]);

  const activeCount   = useMemo(() => items.filter((i) => i.isActive).length,  [items]);
  const inactiveCount = useMemo(() => items.filter((i) => !i.isActive).length, [items]);

  /* ── Modal helpers ────────────────────────────────────────── */
  const openCreate = () => {
    setModalError('');
    reset({ code: '', name: '' });
    setModal({ kind: 'create' });
  };

  const openEdit = (item: CatalogItem) => {
    setModalError('');
    reset({ code: item.code, name: item.name });
    setModal({ kind: 'edit', item });
  };

  const closeModal = () => { setModal(null); setModalError(''); };

  /* ── Submit ───────────────────────────────────────────────── */
  const onSubmit = handleSubmit(async (values) => {
    if (!modal) return;
    setSaving(true);
    setModalError('');
    try {
      const payload = { code: values.code.trim(), name: values.name.trim() };
      if (modal.kind === 'create') {
        await catalogService.createProductType(payload);
      } else {
        await catalogService.updateProductType(modal.item.id, payload);
      }
      await load();
      closeModal();
    } catch (e) {
      setModalError(formatApiRequestError(e, { generic: t('productTypes.error.save') }));
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle active ────────────────────────────────────────── */
  const toggleItem = async (item: CatalogItem) => {
    setError('');
    try {
      if (item.isActive) await catalogService.disableProductType(item.id);
      else               await catalogService.enableProductType(item.id);
      await load();
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('productTypes.error.toggle') }));
    }
  };

  if (!canView) return <NoAccessPage title={t('productTypes.title')} />;

  const modalTitle = modal?.kind === 'create' ? t('productTypes.modal.create') : t('productTypes.modal.edit');

  return (
    <div className="pg-page">

      {/* ── Header ─────────────────────────────────────────── */}
      <div className="pg-header-row">
        <div>
          <p className="pg-kicker">{t('app.nav.group.inventario')}</p>
          <h1 className="pg-title">{t('productTypes.title')}</h1>
          <p className="pg-subtitle">{t('productTypes.subtitle')}</p>
        </div>
        {canCreate && (
          <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>add</span>
            {t('productTypes.new')}
          </ZHBtn>
        )}
      </div>

      {/* ── KPIs ───────────────────────────────────────────── */}
      <div className="pg-kpis">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">inventory_2</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('productTypes.kpi.total')}</p>
            <p className="pg-kpi-value">{items.length}</p>
          </div>
        </div>
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--success">
              <span className="material-symbols-outlined">check_circle</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('common.active')}</p>
            <p className="pg-kpi-value">{activeCount}</p>
          </div>
        </div>
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--warning">
              <span className="material-symbols-outlined">pause_circle</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('common.inactive')}</p>
            <p className="pg-kpi-value">{inactiveCount}</p>
          </div>
        </div>
      </div>

      {/* ── Error ──────────────────────────────────────────── */}
      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      {/* ── Table section ──────────────────────────────────── */}
      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                className="zh-input"
                type="search"
                placeholder={t('productTypes.search.placeholder')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
              style={{ width: 'auto', minWidth: 140 }}
            >
              <option value="all">{t('productTypes.filter.all')}</option>
              <option value="active">{t('common.active')}</option>
              <option value="inactive">{t('common.inactive')}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">
              {filtered.length} {t('productTypes.kpi.total').toLowerCase()}
            </span>
          </div>
        </div>

        {loading ? (
          <p className="subtle" style={{ padding: 24 }}>{t('common.loading')}</p>
        ) : filtered.length === 0 ? (
          <p className="subtle" style={{ padding: 24, textAlign: 'center' }}>{t('common.noData')}</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('common.code')}</th>
                <th>{t('productTypes.table.type')}</th>
                <th style={{ textAlign: 'center' }}>{t('common.status')}</th>
                <th style={{ textAlign: 'right' }}>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((item) => (
                <tr key={item.id} style={{ opacity: item.isActive ? 1 : 0.65 }}>
                  <td>
                    <span className="mono" style={{ fontSize: 12, fontWeight: 600, color: 'var(--color-primary)' }}>
                      {item.code}
                    </span>
                  </td>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <div
                        className="zh-avatar zh-avatar--square"
                        style={{ width: 32, height: 32, flexShrink: 0, background: 'var(--color-surface-container-high)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}
                        aria-hidden
                      >
                        <span className="material-symbols-outlined" style={{ fontSize: 16, color: 'var(--color-primary)' }}>inventory_2</span>
                      </div>
                      <span style={{ fontSize: 13, fontWeight: 500 }}>{item.name}</span>
                    </div>
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <span className={`zh-status zh-status--${item.isActive ? 'active' : 'inactive'}`}>
                      {item.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                      {canUpdate && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEdit(item)}
                          title={t('common.edit')}
                        >
                          <span className="material-symbols-outlined" style={{ fontSize: 17 }}>edit</span>
                        </button>
                      )}
                      {(canDelete || canUpdate) && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => void toggleItem(item)}
                          title={item.isActive ? t('common.disable') : t('common.enable')}
                        >
                          <span className="material-symbols-outlined" style={{ fontSize: 17 }}>
                            {item.isActive ? 'visibility_off' : 'visibility'}
                          </span>
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* ── Create / Edit modal ─────────────────────────────── */}
      {modal && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal" style={{ maxWidth: 440 }}>

            <div className="zh-modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span className="material-symbols-outlined" style={{ fontSize: 20, color: 'var(--color-primary)' }}>inventory_2</span>
                <h2 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{modalTitle}</h2>
              </div>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--sm"
                onClick={closeModal}
                aria-label={t('common.close')}
              >
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>

            <form onSubmit={onSubmit}>
              <div className="zh-modal-body">
                {modalError && (
                  <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={modalError} />
                )}
                <div className="pg-form-grid--2">
                  <ZHField label={t('common.code')} required error={errors.code?.message}>
                    <input
                      className="zh-input"
                      {...register('code')}
                      disabled={saving}
                      placeholder="MRC"
                    />
                  </ZHField>
                  <ZHField label={t('common.name')} required error={errors.name?.message}>
                    <input
                      className="zh-input"
                      {...register('name')}
                      disabled={saving}
                      placeholder={t('productTypes.form.namePlaceholder')}
                    />
                  </ZHField>
                </div>
              </div>

              <div className="pg-actions-bar">
                <div />
                <div className="pg-actions-buttons">
                  <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal} disabled={saving}>
                    {t('common.cancel')}
                  </ZHBtn>
                  <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                    {saving ? t('common.saving') : t('common.saveChanges')}
                  </ZHBtn>
                </div>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
