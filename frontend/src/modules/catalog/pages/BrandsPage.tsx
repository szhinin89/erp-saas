import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { catalogService, type BrandItem } from '../api/catalogService';
import { ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NoAccessPage } from '../../../components/PageShell';
import { formatApiRequestError } from '../../lib/apiError';

/* ── Form schema ────────────────────────────────────────────── */
const brandSchema = z.object({
  code:            z.string().min(1, 'Required').max(20),
  name:            z.string().min(1, 'Required').max(120),
  manufacturer:    z.string().max(120).optional(),
  countryOfOrigin: z.string().max(80).optional(),
});
type BrandFormValues = z.infer<typeof brandSchema>;

/* ── Main page ──────────────────────────────────────────────── */
export function BrandsPage() {
  const { t } = useI18n();
  const role     = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm  = usePermissionsStore((s) => s.has);

  const canView   = isAdmin || hasPerm('inventory.brands.view');
  const canCreate = isAdmin || hasPerm('inventory.brands.create');
  const canUpdate = isAdmin || hasPerm('inventory.brands.update');
  const canDelete = isAdmin || hasPerm('inventory.brands.delete');

  /* ── Data state ───────────────────────────────────────────── */
  const [brands,  setBrands]  = useState<BrandItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');
  const [search,  setSearch]  = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');

  /* ── Modal state ──────────────────────────────────────────── */
  type ModalMode = { kind: 'create' } | { kind: 'edit'; item: BrandItem };
  const [modal,  setModal]  = useState<ModalMode | null>(null);
  const [saving, setSaving] = useState(false);
  const [modalError, setModalError] = useState('');

  const { register, handleSubmit, reset, formState: { errors } } = useForm<BrandFormValues>({
    resolver: zodResolver(brandSchema),
    defaultValues: { code: '', name: '', manufacturer: '', countryOfOrigin: '' },
  });

  /* ── Load ─────────────────────────────────────────────────── */
  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setBrands(await catalogService.brands(false) ?? []);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('brands.error.load') }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  /* ── Filtered list ────────────────────────────────────────── */
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return brands.filter((b) => {
      const matchStatus =
        statusFilter === 'all' ||
        (statusFilter === 'active' && b.isActive) ||
        (statusFilter === 'inactive' && !b.isActive);
      const matchSearch = !q ||
        `${b.code} ${b.name} ${b.manufacturer ?? ''} ${b.countryOfOrigin ?? ''}`.toLowerCase().includes(q);
      return matchStatus && matchSearch;
    });
  }, [brands, search, statusFilter]);

  const activeCount   = useMemo(() => brands.filter((b) => b.isActive).length, [brands]);
  const inactiveCount = useMemo(() => brands.filter((b) => !b.isActive).length, [brands]);

  /* ── Modal helpers ────────────────────────────────────────── */
  const openCreate = () => {
    setModalError('');
    reset({ code: '', name: '', manufacturer: '', countryOfOrigin: '' });
    setModal({ kind: 'create' });
  };

  const openEdit = (item: BrandItem) => {
    setModalError('');
    reset({
      code:            item.code,
      name:            item.name,
      manufacturer:    item.manufacturer ?? '',
      countryOfOrigin: item.countryOfOrigin ?? '',
    });
    setModal({ kind: 'edit', item });
  };

  const closeModal = () => { setModal(null); setModalError(''); };

  /* ── Submit ───────────────────────────────────────────────── */
  const onSubmit = handleSubmit(async (values) => {
    if (!modal) return;
    setSaving(true);
    setModalError('');
    try {
      const payload = {
        code:            values.code.trim(),
        name:            values.name.trim(),
        manufacturer:    values.manufacturer?.trim() || null,
        countryOfOrigin: values.countryOfOrigin?.trim() || null,
      };
      if (modal.kind === 'create') {
        await catalogService.createBrand(payload);
      } else {
        await catalogService.updateBrand(modal.item.id, payload);
      }
      await load();
      closeModal();
    } catch (e) {
      setModalError(formatApiRequestError(e, { generic: t('brands.error.save') }));
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle active ────────────────────────────────────────── */
  const toggleBrand = async (item: BrandItem) => {
    setError('');
    try {
      if (item.isActive) await catalogService.disableBrand(item.id);
      else               await catalogService.enableBrand(item.id);
      await load();
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('brands.error.toggle') }));
    }
  };

  if (!canView) return <NoAccessPage title={t('brands.title')} />;

  const modalTitle = modal?.kind === 'create' ? t('brands.modal.create') : t('brands.modal.edit');

  return (
    <div className="pg-page">

      {/* ── Header ─────────────────────────────────────────── */}
      <div className="pg-header-row">
        <div>
          <p className="pg-kicker">{t('app.nav.group.inventario')}</p>
          <h1 className="pg-title">{t('brands.title')}</h1>
          <p className="pg-subtitle">{t('brands.subtitle')}</p>
        </div>
        {canCreate && (
          <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>add</span>
            {t('brands.new')}
          </ZHBtn>
        )}
      </div>

      {/* ── KPIs ───────────────────────────────────────────── */}
      <div className="pg-kpis">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">sell</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('brands.kpi.total')}</p>
            <p className="pg-kpi-value">{brands.length}</p>
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
                placeholder={t('brands.search.placeholder')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
              style={{ width: 'auto', minWidth: 130 }}
            >
              <option value="all">{t('brands.filter.all')}</option>
              <option value="active">{t('common.active')}</option>
              <option value="inactive">{t('common.inactive')}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">{filtered.length} {t('brands.kpi.total').toLowerCase()}</span>
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
                <th>{t('brands.table.brand')}</th>
                <th>{t('brands.table.manufacturer')}</th>
                <th>{t('brands.table.country')}</th>
                <th style={{ textAlign: 'center' }}>{t('common.status')}</th>
                <th style={{ textAlign: 'right' }}>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((brand) => (
                <tr key={brand.id} style={{ opacity: brand.isActive ? 1 : 0.65 }}>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <div
                        className="zh-avatar zh-avatar--square"
                        style={{ width: 36, height: 36, flexShrink: 0, background: 'var(--color-surface-container-high)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}
                        aria-hidden
                      >
                        <span className="material-symbols-outlined" style={{ fontSize: 18, color: 'var(--color-primary)' }}>sell</span>
                      </div>
                      <div>
                        <p style={{ margin: 0, fontWeight: 500, fontSize: 13 }}>{brand.name}</p>
                        <p className="subtle mono" style={{ margin: 0, fontSize: 11 }}>{brand.code}</p>
                      </div>
                    </div>
                  </td>
                  <td>
                    {brand.manufacturer ? (
                      <span style={{ fontSize: 13 }}>{brand.manufacturer}</span>
                    ) : (
                      <span className="subtle" style={{ fontSize: 12 }}>—</span>
                    )}
                  </td>
                  <td>
                    {brand.countryOfOrigin ? (
                      <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                        <span className="material-symbols-outlined" style={{ fontSize: 14, color: 'var(--color-text-secondary)' }}>public</span>
                        <span style={{ fontSize: 13 }}>{brand.countryOfOrigin}</span>
                      </div>
                    ) : (
                      <span className="subtle" style={{ fontSize: 12 }}>—</span>
                    )}
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <span className={`zh-status zh-status--${brand.isActive ? 'active' : 'inactive'}`}>
                      {brand.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                      {canUpdate && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEdit(brand)}
                          title={t('common.edit')}
                        >
                          <span className="material-symbols-outlined" style={{ fontSize: 17 }}>edit</span>
                        </button>
                      )}
                      {(canDelete || canUpdate) && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => void toggleBrand(brand)}
                          title={brand.isActive ? t('common.disable') : t('common.enable')}
                        >
                          <span className="material-symbols-outlined" style={{ fontSize: 17 }}>
                            {brand.isActive ? 'visibility_off' : 'visibility'}
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
          <div className="zh-modal" style={{ maxWidth: 520 }}>

            <div className="zh-modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span className="material-symbols-outlined" style={{ fontSize: 20, color: 'var(--color-primary)' }}>sell</span>
                <h2 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{modalTitle}</h2>
              </div>
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={closeModal} aria-label={t('common.close')}>
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
                    <input className="zh-input" {...register('code')} disabled={saving} placeholder="MRC-001" />
                  </ZHField>
                  <ZHField label={t('common.name')} required error={errors.name?.message}>
                    <input className="zh-input" {...register('name')} disabled={saving} placeholder={t('brands.form.namePlaceholder')} />
                  </ZHField>
                  <ZHField label={t('brands.form.manufacturer')} error={errors.manufacturer?.message}>
                    <input className="zh-input" {...register('manufacturer')} disabled={saving} placeholder={t('brands.form.manufacturerPlaceholder')} />
                  </ZHField>
                  <ZHField label={t('brands.form.countryOfOrigin')} error={errors.countryOfOrigin?.message}>
                    <input className="zh-input" {...register('countryOfOrigin')} disabled={saving} placeholder={t('brands.form.countryPlaceholder')} />
                  </ZHField>
                </div>
              </div>

              <div className="pg-actions-bar">
                <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal} disabled={saving}>
                  {t('common.cancel')}
                </ZHBtn>
                <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                  {saving ? t('common.saving') : t('common.saveChanges')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
