import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { catalogService, type BrandItem } from '../api/catalogService';
import { ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { formatApiRequestError } from '../../lib/apiError';
import './catalog-list-page.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

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
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();

  const canView   = canShow('inventory.brands.view');
  const canCreate = canShow('inventory.brands.create');
  const canUpdate = canShow('inventory.brands.update');
  const canDelete = canShow('inventory.brands.delete');

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
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario')}
      title={t('brands.title')}
      subtitle={t('brands.subtitle')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
            <span className="material-symbols-outlined">add</span>
            {t('brands.new')}
          </ZHBtn>
        ) : undefined
      }
    >
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
              className="zh-input cat-list-filter-select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
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
          <p className="subtle pg-state-pad-24">{t('common.loading')}</p>
        ) : filtered.length === 0 ? (
          <p className="subtle pg-state-pad-24-center">{t('common.noData')}</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('brands.table.brand')}</th>
                <th>{t('brands.table.manufacturer')}</th>
                <th>{t('brands.table.country')}</th>
                <th className="cat-list-th-center">{t('common.status')}</th>
                <th className="cat-list-th-right">{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((brand) => (
                <tr key={brand.id} className={brand.isActive ? undefined : 'pg-row-inactive'}>
                  <td>
                    <div className="pg-actions-inline-10">
                      <div className="zh-avatar zh-avatar--square pg-avatar-sm" aria-hidden>
                        <span className="material-symbols-outlined pg-icon-18 pg-icon-primary">sell</span>
                      </div>
                      <div>
                        <p className="cat-list-name">{brand.name}</p>
                        <p className="subtle mono cat-list-code">{brand.code}</p>
                      </div>
                    </div>
                  </td>
                  <td>
                    {brand.manufacturer ? (
                      <span className="pg-text-13">{brand.manufacturer}</span>
                    ) : (
                      <span className="subtle pg-text-12">—</span>
                    )}
                  </td>
                  <td>
                    {brand.countryOfOrigin ? (
                      <div className="cat-list-country-row">
                        <span className="material-symbols-outlined cat-list-icon-country">public</span>
                        <span className="pg-text-13">{brand.countryOfOrigin}</span>
                      </div>
                    ) : (
                      <span className="subtle pg-text-12">—</span>
                    )}
                  </td>
                  <td className="cat-list-td-center">
                    <span className={`zh-status zh-status--${brand.isActive ? 'active' : 'inactive'}`}>
                      {brand.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div className="pg-actions-inline">
                      {canUpdate && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEdit(brand)}
                          title={t('common.edit')}
                        >
                          <span className="material-symbols-outlined pg-icon-17">edit</span>
                        </button>
                      )}
                      {(canDelete || canUpdate) && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => void toggleBrand(brand)}
                          title={brand.isActive ? t('common.disable') : t('common.enable')}
                        >
                          <span className="material-symbols-outlined pg-icon-17">
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
          <div className="zh-modal pg-modal--md">

            <div className="zh-modal-header">
              <div className="pg-modal-title-row">
                <span className="material-symbols-outlined pg-icon-20 pg-icon-primary">sell</span>
                <h2 className="pg-modal-title-text">{modalTitle}</h2>
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
    </ErpPageTemplate>
  );
}
