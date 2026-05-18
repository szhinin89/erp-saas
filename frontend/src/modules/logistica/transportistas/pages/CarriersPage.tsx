import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import type { Carrier } from '../api/carrierService';
import { useCarriers } from '../hooks/useCarriers';
import { carrierSchema, defaultCarrierValues, type CarrierFormValues } from '../schemas/carrierSchema';
import './carriers-page.css';

const ID_TYPES = ['RUC', 'CI', 'PASSPORT'] as const;

export function CarriersPage() {
  const { t } = useI18n();
  const hasPerm  = usePermissionsStore(s => s.has);
  const canView   = hasPerm('logistics.carriers.view');
  const canCreate = hasPerm('logistics.carriers.create');
  const canEdit   = hasPerm('logistics.carriers.update') || canCreate;

  /* ── State ── */
  const [searchQuery, setSearchQuery]   = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [modalOpen, setModalOpen]       = useState(false);
  const [editingId, setEditingId]       = useState<string | null>(null);

  /* ── Data ── */
  const { carriers, loading, error, saving, saveError, createCarrier, updateCarrier, toggleCarrierStatus } = useCarriers();

  const { register, handleSubmit, reset, setValue, formState: { errors } } = useForm<CarrierFormValues>({
    resolver: zodResolver(carrierSchema),
    defaultValues: defaultCarrierValues,
  });

  /* ── Derived ── */
  const filtered = useMemo(() => {
    let list = carriers;
    if (statusFilter === 'active')   list = list.filter(c => c.isActive);
    if (statusFilter === 'inactive') list = list.filter(c => !c.isActive);
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter(c =>
      c.legalName.toLowerCase().includes(term)            ||
      c.identificationNumber.toLowerCase().includes(term) ||
      c.licensePlate.toLowerCase().includes(term)         ||
      (c.email ?? '').toLowerCase().includes(term)
    );
  }, [carriers, searchQuery, statusFilter]);

  const totals = useMemo(() => ({
    total:    carriers.length,
    active:   carriers.filter(c => c.isActive).length,
    inactive: carriers.filter(c => !c.isActive).length,
  }), [carriers]);

  /* ── Modal helpers ── */
  const openCreate = () => {
    setEditingId(null);
    reset(defaultCarrierValues);
    setModalOpen(true);
  };

  const openEdit = (carrier: Carrier) => {
    setEditingId(carrier.id);
    setValue('identificationType',   carrier.identificationType);
    setValue('identificationNumber', carrier.identificationNumber);
    setValue('legalName',            carrier.legalName);
    setValue('licensePlate',         carrier.licensePlate);
    setValue('phone',                carrier.phone ?? '');
    setValue('email',                carrier.email ?? '');
    setModalOpen(true);
  };

  const closeModal = () => { setModalOpen(false); setEditingId(null); reset(defaultCarrierValues); };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      identificationType:   values.identificationType,
      identificationNumber: values.identificationNumber,
      legalName:            values.legalName,
      licensePlate:         values.licensePlate,
      phone:                values.phone || null,
      email:                values.email || null,
    };
    const result = editingId
      ? await updateCarrier(editingId, payload)
      : await createCarrier(payload);
    if (result) closeModal();
  });

  const handleToggle = async (carrier: Carrier) => {
    if (!canEdit) return;
    await toggleCarrierStatus(carrier.id, !carrier.isActive);
  };

  if (!canView) return <NoAccessPage title={t('carriers.title')} />;

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <p className="pg-kicker">{t('carriers.kicker')}</p>
          <h1 className="pg-title">{t('carriers.title')}</h1>
          <p className="pg-subtitle">{t('carriers.subtitle')}</p>
        </div>
        {canCreate && (
          <div className="pg-header-right">
            <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={openCreate}>
              <span className="material-symbols-outlined">add</span>
              {t('carriers.new')}
            </ZHBtn>
          </div>
        )}
      </div>

      {/* ── Errors ── */}
      {error     && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
      {saveError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">local_shipping</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('carriers.kpi.total')}</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">check_circle</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('carriers.kpi.active')}</p>
            <p className="pg-kpi-value">{totals.active}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">do_not_disturb</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('carriers.kpi.inactive')}</p>
            <p className="pg-kpi-value">{totals.inactive}</p>
          </div>
        </div>
      </div>

      {/* ── Table section ── */}
      <div className="pg-section">
        {/* Filter bar */}
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                className="zh-input"
                type="search"
                placeholder={t('carriers.search.placeholder')}
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value as typeof statusFilter)}
            >
              <option value="all">{t('carriers.filter.all')}</option>
              <option value="active">{t('carriers.filter.active')}</option>
              <option value="inactive">{t('carriers.filter.inactive')}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">
              {filtered.length} / {carriers.length} {t('carriers.kpi.total').toLowerCase()}
            </span>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <LoadingState />
        ) : filtered.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>{t('carriers.table.idType')}</th>
                  <th>{t('carriers.table.identification')}</th>
                  <th>{t('carriers.table.legalName')}</th>
                  <th>{t('carriers.table.licensePlate')}</th>
                  <th>{t('carriers.table.phone')}</th>
                  <th>{t('carriers.table.email')}</th>
                  <th>{t('carriers.table.status')}</th>
                  <th>{t('carriers.table.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(carrier => (
                  <tr key={carrier.id}>
                    <td>
                      <span className="badge badge--gray badge--upper">
                        {t(`carriers.idType.${carrier.identificationType}`)}
                      </span>
                    </td>
                    <td className="mono">{carrier.identificationNumber}</td>
                    <td style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>
                      {carrier.legalName}
                    </td>
                    <td>
                      <span className="badge badge--blue mono">{carrier.licensePlate}</span>
                    </td>
                    <td className="subtle">{carrier.phone ?? '—'}</td>
                    <td className="subtle">{carrier.email ?? '—'}</td>
                    <td>
                      <span className={carrier.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                        {carrier.isActive ? t('carriers.status.active') : t('carriers.status.inactive')}
                      </span>
                    </td>
                    <td>
                      <div className="crt-actions-cell">
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEdit(carrier)}
                          disabled={!canEdit}
                          aria-label="Edit"
                        >
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button
                          type="button"
                          className={`zh-btn zh-btn--ghost zh-btn--sm ${carrier.isActive ? 'crt-btn-danger' : 'crt-btn-success'}`}
                          onClick={() => void handleToggle(carrier)}
                          disabled={!canEdit || saving}
                          aria-label={carrier.isActive ? 'Disable' : 'Enable'}
                        >
                          <span className="material-symbols-outlined">
                            {carrier.isActive ? 'block' : 'check_circle'}
                          </span>
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── Modal ── */}
      {modalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingId ? t('carriers.modal.editTitle') : t('carriers.modal.createTitle')}
          onClick={e => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingId ? t('carriers.modal.editTitle') : t('carriers.modal.createTitle')}
              </h2>
              <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Close">✕</button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField
                    label={t('carriers.modal.idType')}
                    required
                    error={errors.identificationType?.message ? t(errors.identificationType.message) : undefined}
                  >
                    <select className="zh-input" disabled={saving} {...register('identificationType')}>
                      {ID_TYPES.map(type => (
                        <option key={type} value={type}>{t(`carriers.idType.${type}`)}</option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField
                    label={t('carriers.modal.idNumber')}
                    required
                    error={errors.identificationNumber?.message ? t(errors.identificationNumber.message) : undefined}
                  >
                    <input
                      className="zh-input"
                      placeholder={t('carriers.modal.idNumberPlaceholder')}
                      disabled={saving}
                      {...register('identificationNumber')}
                    />
                  </ZHField>
                </div>

                <ZHField
                  label={t('carriers.modal.legalName')}
                  required
                  error={errors.legalName?.message ? t(errors.legalName.message) : undefined}
                >
                  <input
                    className="zh-input"
                    placeholder={t('carriers.modal.legalNamePlaceholder')}
                    disabled={saving}
                    {...register('legalName')}
                  />
                </ZHField>

                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField
                    label={t('carriers.modal.licensePlate')}
                    required
                    error={errors.licensePlate?.message ? t(errors.licensePlate.message) : undefined}
                  >
                    <input
                      className="zh-input"
                      placeholder={t('carriers.modal.licensePlatePlaceholder')}
                      disabled={saving}
                      {...register('licensePlate')}
                    />
                  </ZHField>
                  <ZHField label={t('carriers.modal.phone')}>
                    <input
                      className="zh-input"
                      type="tel"
                      placeholder={t('carriers.modal.phonePlaceholder')}
                      disabled={saving}
                      {...register('phone')}
                    />
                  </ZHField>
                </div>

                <ZHField
                  label={t('carriers.modal.email')}
                  error={errors.email?.message ? t(errors.email.message) : undefined}
                >
                  <input
                    className="zh-input"
                    type="email"
                    placeholder={t('carriers.modal.emailPlaceholder')}
                    disabled={saving}
                    {...register('email')}
                  />
                </ZHField>

                <div className="pg-actions-bar">
                  <div className="pg-actions-buttons">
                    <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal}>
                      {t('common.cancel')}
                    </ZHBtn>
                    <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                      {saving ? t('common.saving') : t('common.save')}
                    </ZHBtn>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
