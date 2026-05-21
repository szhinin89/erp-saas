import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { catalogService, type UnitItem } from '../api/catalogService';
import { ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { formatApiRequestError } from '../../lib/apiError';
import './catalog-list-page.css';

/* ── Form schema ────────────────────────────────────────────── */
const unitSchema = z.object({
  code:   z.string().min(1, 'Required').max(20),
  name:   z.string().min(1, 'Required').max(120),
  symbol: z.string().max(20).optional(),
});
type UnitFormValues = z.infer<typeof unitSchema>;

/* ── Main page ──────────────────────────────────────────────── */
export function UnitsPage() {
  const { t } = useI18n();
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canView   = isAdmin || hasPerm('inventory.units.view');
  const canCreate = isAdmin || hasPerm('inventory.units.create');
  const canUpdate = isAdmin || hasPerm('inventory.units.update');
  const canDelete = isAdmin || hasPerm('inventory.units.delete');

  /* ── Data state ───────────────────────────────────────────── */
  const [units,   setUnits]   = useState<UnitItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState('');
  const [search,  setSearch]  = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');

  /* ── Modal state ──────────────────────────────────────────── */
  type ModalMode = { kind: 'create' } | { kind: 'edit'; item: UnitItem };
  const [modal,      setModal]      = useState<ModalMode | null>(null);
  const [saving,     setSaving]     = useState(false);
  const [modalError, setModalError] = useState('');

  const { register, handleSubmit, reset, formState: { errors } } = useForm<UnitFormValues>({
    resolver: zodResolver(unitSchema),
    defaultValues: { code: '', name: '', symbol: '' },
  });

  /* ── Load ─────────────────────────────────────────────────── */
  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setUnits(await catalogService.units(false) ?? []);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('units.error.load') }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  /* ── Filtered list ────────────────────────────────────────── */
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return units.filter((u) => {
      const matchStatus =
        statusFilter === 'all' ||
        (statusFilter === 'active'   && u.isActive) ||
        (statusFilter === 'inactive' && !u.isActive);
      const matchSearch = !q ||
        `${u.code} ${u.name} ${u.symbol ?? ''}`.toLowerCase().includes(q);
      return matchStatus && matchSearch;
    });
  }, [units, search, statusFilter]);

  const activeCount   = useMemo(() => units.filter((u) => u.isActive).length,  [units]);
  const inactiveCount = useMemo(() => units.filter((u) => !u.isActive).length, [units]);

  /* ── Modal helpers ────────────────────────────────────────── */
  const openCreate = () => {
    setModalError('');
    reset({ code: '', name: '', symbol: '' });
    setModal({ kind: 'create' });
  };

  const openEdit = (item: UnitItem) => {
    setModalError('');
    reset({ code: item.code, name: item.name, symbol: item.symbol ?? '' });
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
        code:   values.code.trim(),
        name:   values.name.trim(),
        symbol: values.symbol?.trim() || null,
      };
      if (modal.kind === 'create') {
        await catalogService.createUnit(payload);
      } else {
        await catalogService.updateUnit(modal.item.id, payload);
      }
      await load();
      closeModal();
    } catch (e) {
      setModalError(formatApiRequestError(e, { generic: t('units.error.save') }));
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle active ────────────────────────────────────────── */
  const toggleUnit = async (unit: UnitItem) => {
    setError('');
    try {
      if (unit.isActive) await catalogService.disableUnit(unit.id);
      else               await catalogService.enableUnit(unit.id);
      await load();
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('units.error.toggle') }));
    }
  };

  if (!canView) return <NoAccessPage title={t('units.title')} />;

  const modalTitle = modal?.kind === 'create' ? t('units.modal.create') : t('units.modal.edit');

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario')}
      title={t('units.title')}
      subtitle={t('units.subtitle')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
            <span className="material-symbols-outlined">add</span>
            {t('units.new')}
          </ZHBtn>
        ) : undefined
      }
    >
      {/* ── KPIs ───────────────────────────────────────────── */}
      <div className="pg-kpis">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">straighten</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('units.kpi.total')}</p>
            <p className="pg-kpi-value">{units.length}</p>
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
                placeholder={t('units.search.placeholder')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <select
              className="zh-input cat-list-filter-select cat-list-filter-select--wide"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
            >
              <option value="all">{t('units.filter.all')}</option>
              <option value="active">{t('common.active')}</option>
              <option value="inactive">{t('common.inactive')}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">
              {filtered.length} {t('units.kpi.total').toLowerCase()}
            </span>
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
                <th>{t('common.code')}</th>
                <th>{t('common.name')}</th>
                <th>{t('units.table.symbol')}</th>
                <th className="cat-list-th-center">{t('common.status')}</th>
                <th className="cat-list-th-right">{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((unit) => (
                <tr key={unit.id} className={unit.isActive ? undefined : 'pg-row-inactive'}>
                  <td>
                    <div className="pg-actions-inline-10">
                      <div className="zh-avatar zh-avatar--square pg-avatar-sm-32" aria-hidden>
                        <span className="material-symbols-outlined pg-icon-18 pg-icon-primary">straighten</span>
                      </div>
                      <span className="mono cat-list-code-primary">
                        {unit.code}
                      </span>
                    </div>
                  </td>
                  <td className="pg-text-13">{unit.name}</td>
                  <td>
                    {unit.symbol ? (
                      <span className="badge badge--blue">{unit.symbol}</span>
                    ) : (
                      <span className="subtle pg-text-12">—</span>
                    )}
                  </td>
                  <td className="cat-list-td-center">
                    <span className={`zh-status zh-status--${unit.isActive ? 'active' : 'inactive'}`}>
                      {unit.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  <td>
                    <div className="pg-actions-inline">
                      {canUpdate && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEdit(unit)}
                          title={t('common.edit')}
                        >
                          <span className="material-symbols-outlined pg-icon-17">edit</span>
                        </button>
                      )}
                      {(canDelete || canUpdate) && (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => void toggleUnit(unit)}
                          title={unit.isActive ? t('common.disable') : t('common.enable')}
                        >
                          <span className="material-symbols-outlined pg-icon-17">
                            {unit.isActive ? 'visibility_off' : 'visibility'}
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
          <div className="zh-modal pg-modal--480">

            <div className="zh-modal-header">
              <div className="pg-modal-title-row">
                <span className="material-symbols-outlined pg-icon-20 pg-icon-primary">straighten</span>
                <h2 className="pg-modal-title-text">{modalTitle}</h2>
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
                      placeholder="KG"
                    />
                  </ZHField>
                  <ZHField label={t('units.form.symbol')} error={errors.symbol?.message}>
                    <input
                      className="zh-input"
                      {...register('symbol')}
                      disabled={saving}
                      placeholder={t('units.form.symbolPlaceholder')}
                    />
                  </ZHField>
                </div>
                <ZHField label={t('common.name')} required error={errors.name?.message}>
                  <input
                    className="zh-input"
                    {...register('name')}
                    disabled={saving}
                    placeholder={t('units.form.namePlaceholder')}
                  />
                </ZHField>
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
    </ErpPageTemplate>
  );
}
