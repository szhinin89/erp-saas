import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { useI18n } from '../../../../i18n/i18n';
import { useSuppliers } from '../hooks/useSuppliers';
import {
  supplierSchema,
  defaultSupplierValues,
  type SupplierFormValues,
} from '../schemas/supplierSchema';
import type { Supplier } from '../api/supplierService';
import './suppliers-page.css';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

type TabId = 'list' | 'form';

function statusClass(status: Supplier['status']) {
  if (status === 'active')  return 'zh-status zh-status--active';
  if (status === 'pending') return 'zh-status zh-status--pending';
  return 'zh-status zh-status--inactive';
}

export function SuppliersPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView   = canShow('purchases.suppliers.view');
  const canCreate = canShow('purchases.suppliers.create');
  const canEdit   = canShow('purchases.suppliers.edit');

  const { suppliers, loading, error, saving, saveError, createSupplier, updateSupplier, setSupplierStatus } =
    useSuppliers();

  /* ── UI state ── */
  const [activeTab,    setActiveTab]    = useState<TabId>('list');
  const [searchQuery,  setSearchQuery]  = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'pending' | 'inactive'>('all');
  const [modalOpen,    setModalOpen]    = useState(false);
  const [editingId,    setEditingId]    = useState<string | null>(null);

  /* ── Form ── */
  const { register, handleSubmit, reset, setValue, formState: { errors } } =
    useForm<SupplierFormValues>({
      resolver: zodResolver(supplierSchema),
      defaultValues: defaultSupplierValues,
    });

  /* ── Derived ── */
  const filtered = useMemo(() => {
    let list = suppliers;
    if (statusFilter !== 'all') list = list.filter((s) => s.status === statusFilter);
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (s) =>
        s.taxId.toLowerCase().includes(term) ||
        s.legalName.toLowerCase().includes(term) ||
        (s.primaryContact ?? '').toLowerCase().includes(term),
    );
  }, [suppliers, searchQuery, statusFilter]);

  const totals = useMemo(() => ({
    total:    suppliers.length,
    active:   suppliers.filter((s) => s.status === 'active').length,
    pending:  suppliers.filter((s) => s.status === 'pending').length,
    inactive: suppliers.filter((s) => s.status === 'inactive').length,
  }), [suppliers]);

  /* ── Modal helpers ── */
  const openCreateModal = () => {
    setEditingId(null);
    reset(defaultSupplierValues);
    setModalOpen(true);
  };

  const openEditModal = (supplier: Supplier) => {
    setEditingId(supplier.id);
    setValue('taxId',          supplier.taxId);
    setValue('legalName',      supplier.legalName);
    setValue('tradeName',      supplier.tradeName      ?? '');
    setValue('primaryContact', supplier.primaryContact ?? '');
    setValue('email',          supplier.email          ?? '');
    setValue('phone',          supplier.phone          ?? '');
    setValue('address',        supplier.address        ?? '');
    setValue('website',        supplier.website        ?? '');
    setValue('category',       supplier.category       ?? '');
    setValue('status',         supplier.status);
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    reset(defaultSupplierValues);
  };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      taxId:          values.taxId,
      legalName:      values.legalName,
      tradeName:      values.tradeName      || null,
      primaryContact: values.primaryContact || null,
      email:          values.email          || null,
      phone:          values.phone          || null,
      address:        values.address        || null,
      website:        values.website        || null,
      category:       values.category       || null,
      status:         values.status,
    };
    if (editingId) {
      const updated = await updateSupplier(editingId, payload);
      if (updated) closeModal();
    } else {
      const created = await createSupplier(payload);
      if (created) closeModal();
    }
  });

  const handleToggleStatus = async (supplier: Supplier) => {
    if (!canEdit) return;
    const next = supplier.status === 'active' ? 'inactive' : 'active';
    await setSupplierStatus(supplier.id, next);
  };

  const handleNewInTab = () => {
    setEditingId(null);
    reset(defaultSupplierValues);
    setActiveTab('form');
  };

  const CATEGORIES = [
    { value: 'Supplies',      label: t('suppliers.category.supplies') },
    { value: 'Technology',    label: t('suppliers.category.technology') },
    { value: 'Services',      label: t('suppliers.category.services') },
    { value: 'Manufacturing', label: t('suppliers.category.manufacturing') },
    { value: 'Logistics',     label: t('suppliers.category.logistics') },
    { value: 'Other',         label: t('suppliers.category.other') },
  ];

  if (!canView) return <NoAccessPage title={t('suppliers.title')} />;

  const anyError = error || saveError;

  return (
    <ErpPageTemplate
      kicker={t('suppliers.breadcrumb.purchases')}
      title={t('suppliers.title')}
      subtitle={t('suppliers.subtitle')}
      action={
        <>
          <ZHBtn variant="ghost" size="md" type="button">
            <span className="material-symbols-outlined">download</span>
            {t('suppliers.export')}
          </ZHBtn>
          {canCreate ? (
            <ZHBtn variant="primary" size="md" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              {t('suppliers.new')}
            </ZHBtn>
          ) : null}
        </>
      }
    >
      {anyError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={anyError} /> : null}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">storefront</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('suppliers.kpi.total')}</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">verified</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('suppliers.kpi.active')}</p>
            <p className="pg-kpi-value">{totals.active}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--warning">
            <span className="material-symbols-outlined">pending</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('suppliers.kpi.pending')}</p>
            <p className="pg-kpi-value">{totals.pending}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">block</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('suppliers.kpi.inactive')}</p>
            <p className="pg-kpi-value">{totals.inactive}</p>
          </div>
        </div>
      </div>

      {/* ── Main section ── */}
      <div className="pg-section">

        {/* Tabs */}
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label={t('suppliers.title')}>
            <button
              type="button" role="tab"
              aria-selected={activeTab === 'list'}
              className={activeTab === 'list' ? 'is-active' : ''}
              onClick={() => setActiveTab('list')}
            >
              <span className="material-symbols-outlined pg-tab-icon">view_list</span>
              {t('suppliers.tab.list')}
            </button>
            {canCreate && (
              <button
                type="button" role="tab"
                aria-selected={activeTab === 'form'}
                className={activeTab === 'form' ? 'is-active' : ''}
                onClick={handleNewInTab}
              >
                <span className="material-symbols-outlined pg-tab-icon">add_box</span>
                {t('suppliers.tab.new')}
              </button>
            )}
          </div>
        </div>

        {/* ── List tab ── */}
        {activeTab === 'list' && (
          <>
            <div className="pg-table-controls">
              <div className="pg-table-controls-left">
                <div className="pg-search">
                  <span className="material-symbols-outlined">search</span>
                  <input
                    className="zh-input"
                    type="search"
                    placeholder={t('suppliers.search.placeholder')}
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    aria-label={t('suppliers.search.placeholder')}
                  />
                </div>
                <select
                  className="zh-input vf-filter-min"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
                  aria-label={t('suppliers.form.status')}
                >
                  <option value="all">{t('suppliers.filter.all')}</option>
                  <option value="active">{t('suppliers.filter.active')}</option>
                  <option value="pending">{t('suppliers.filter.pending')}</option>
                  <option value="inactive">{t('suppliers.filter.inactive')}</option>
                </select>
              </div>
              <div className="pg-table-controls-right">
                <span className="pg-result-count">{filtered.length} / {suppliers.length}</span>
              </div>
            </div>

            {loading ? (
              <LoadingState />
            ) : filtered.length === 0 ? (
              <EmptyState message={suppliers.length === 0 ? t('suppliers.empty.noData') : t('suppliers.empty.noResults')} />
            ) : (
              <>
                <div className="prv-table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>{t('suppliers.table.taxId')}</th>
                        <th>{t('suppliers.table.legalName')}</th>
                        <th>{t('suppliers.table.contact')}</th>
                        <th>{t('suppliers.table.phone')}</th>
                        <th className="pg-th-center">{t('common.status')}</th>
                        {canEdit ? <th className="pg-th-right">{t('common.actions')}</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {filtered.map((supplier) => (
                        <tr key={supplier.id} className={supplier.status === 'inactive' ? 'pg-row-muted' : undefined}>
                          <td className="mono subtle pg-nowrap">{supplier.taxId}</td>
                          <td>
                            <div className="prv-supplier-name">{supplier.legalName}</div>
                            {supplier.category && (
                              <div className="prv-contact-sub">{supplier.category}</div>
                            )}
                          </td>
                          <td>{supplier.primaryContact ?? <span className="subtle">—</span>}</td>
                          <td className="mono">{supplier.phone ?? <span className="subtle">—</span>}</td>
                          <td className="pg-th-center">
                            <span className={statusClass(supplier.status)}>
                              {t(`suppliers.status.${supplier.status}`)}
                            </span>
                          </td>
                          {canEdit ? (
                            <td className="pg-th-right">
                              <div className="prv-actions-cell">
                                <button
                                  type="button"
                                  className="zh-btn zh-btn--ghost zh-btn--sm"
                                  title={t('common.edit')}
                                  onClick={() => openEditModal(supplier)}
                                >
                                  <span className="material-symbols-outlined">edit</span>
                                </button>
                                <button
                                  type="button"
                                  className={`zh-btn zh-btn--ghost zh-btn--sm ${supplier.status === 'active' ? 'prv-btn-block' : 'prv-btn-activate'}`}
                                  title={supplier.status === 'active' ? t('suppliers.action.deactivate') : t('suppliers.action.activate')}
                                  disabled={saving}
                                  onClick={() => void handleToggleStatus(supplier)}
                                >
                                  <span className="material-symbols-outlined">
                                    {supplier.status === 'active' ? 'block' : 'check_circle'}
                                  </span>
                                </button>
                              </div>
                            </td>
                          ) : null}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="pg-table-footer">
                  <p className="subtle prv-results-count">{filtered.length} {t('common.results')}</p>
                  <p className="pg-table-timestamp">{t('suppliers.results.updated')}</p>
                </div>
              </>
            )}

            {/* Secondary panels */}
            <div className="prv-panels-grid">
              <div className="pg-kpi prv-notes-kpi">
                <div className="prv-notes-kpi-head">
                  <p className="pg-kpi-label prv-notes-kpi-label">{t('suppliers.notes.title')}</p>
                  <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" aria-label={t('suppliers.notes.title')}>
                    <span className="material-symbols-outlined">add</span>
                  </button>
                </div>
                <div className="prv-notes-grid">
                  <div className="prv-note-card">
                    <p className="prv-note-title">Contract renewal</p>
                    <p className="prv-note-body">Contact main supplier to renew the framework agreement before end of quarter.</p>
                    <p className="prv-note-meta">2 days ago</p>
                  </div>
                  <div className="prv-note-card prv-note-card--warning">
                    <p className="prv-note-title">Pending payments</p>
                    <p className="prv-note-body">3 unconfirmed invoices from the logistics supplier.</p>
                    <p className="prv-note-meta">5 days ago</p>
                  </div>
                </div>
              </div>
              <div className="prv-payment-card">
                <p className="prv-payment-title">{t('suppliers.payment.title')}</p>
                <div>
                  <p className="prv-payment-amount">$0.00</p>
                  <p className="prv-payment-sub">{t('suppliers.payment.accumulated')}</p>
                </div>
                <div className="prv-notes-tags">
                  <ZHBtn variant="primary" size="md" type="button">
                    <span className="material-symbols-outlined">receipt_long</span>
                    {t('suppliers.payment.view')}
                  </ZHBtn>
                  <ZHBtn variant="ghost" size="md" type="button">{t('suppliers.payment.report')}</ZHBtn>
                </div>
              </div>
            </div>
          </>
        )}

        {/* ── Form tab ── */}
        {activeTab === 'form' && canCreate && (
          <div className="prv-form-pad">
            <form onSubmit={onSubmit}>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('suppliers.form.taxId')} required error={errors.taxId?.message}>
                  <input className="zh-input" placeholder={t('suppliers.form.taxIdPlaceholder')} disabled={saving} {...register('taxId')} />
                </ZHField>
                <ZHField label={t('suppliers.form.legalName')} required error={errors.legalName?.message}>
                  <input className="zh-input" placeholder={t('suppliers.form.legalNamePlaceholder')} disabled={saving} {...register('legalName')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('suppliers.form.tradeName')}>
                  <input className="zh-input" placeholder={t('suppliers.form.tradeNamePlaceholder')} disabled={saving} {...register('tradeName')} />
                </ZHField>
                <ZHField label={t('suppliers.form.primaryContact')}>
                  <input className="zh-input" placeholder={t('suppliers.form.primaryContactPlaceholder')} disabled={saving} {...register('primaryContact')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('suppliers.form.email')} error={errors.email?.message}>
                  <input className="zh-input" type="email" placeholder="supplier@company.com" disabled={saving} {...register('email')} />
                </ZHField>
                <ZHField label={t('suppliers.form.phone')}>
                  <input className="zh-input" placeholder={t('suppliers.form.phonePlaceholder')} disabled={saving} {...register('phone')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('suppliers.form.category')}>
                  <select className="zh-input" disabled={saving} {...register('category')}>
                    <option value="">{t('suppliers.form.categorySelect')}</option>
                    {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                  </select>
                </ZHField>
                <ZHField label={t('suppliers.form.status')}>
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="active">{t('suppliers.status.active')}</option>
                    <option value="pending">{t('suppliers.status.pending')}</option>
                    <option value="inactive">{t('suppliers.status.inactive')}</option>
                  </select>
                </ZHField>
              </div>
              <ZHField label={t('suppliers.form.address')}>
                <textarea className="zh-input" rows={2} disabled={saving} {...register('address')} />
              </ZHField>
              <ZHField label={t('suppliers.form.website')}>
                <input className="zh-input" type="url" placeholder="https://supplier.com" disabled={saving} {...register('website')} />
              </ZHField>
              <div className="prv-modal-actions">
                <ZHBtn variant="ghost" size="md" type="button" onClick={() => setActiveTab('list')}>{t('common.cancel')}</ZHBtn>
                <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                  {saving ? t('common.saving') : t('suppliers.save')}
                </ZHBtn>
              </div>
            </form>
          </div>
        )}
      </div>

      {/* ── Create / Edit modal ── */}
      {modalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingId ? t('suppliers.modal.edit') : t('suppliers.modal.create')}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal prv-modal--md">
            <div className="zh-modal-header">
              <div className="prv-modal-title-row">
                <span className="material-symbols-outlined prv-modal-title-icon">storefront</span>
                <h2 className="prv-modal-title-text">
                  {editingId ? t('suppliers.modal.edit') : t('suppliers.modal.create')}
                </h2>
              </div>
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={closeModal} aria-label={t('common.close')}>
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label={t('suppliers.form.taxId')} required error={errors.taxId?.message}>
                    <input className="zh-input" placeholder={t('suppliers.form.taxIdPlaceholder')} disabled={saving} {...register('taxId')} />
                  </ZHField>
                  <ZHField label={t('suppliers.form.legalName')} required error={errors.legalName?.message}>
                    <input className="zh-input" placeholder={t('suppliers.form.legalNamePlaceholder')} disabled={saving} {...register('legalName')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label={t('suppliers.form.primaryContact')}>
                    <input className="zh-input" disabled={saving} {...register('primaryContact')} />
                  </ZHField>
                  <ZHField label={t('suppliers.form.phone')}>
                    <input className="zh-input" placeholder={t('suppliers.form.phonePlaceholder')} disabled={saving} {...register('phone')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label={t('suppliers.form.email')} error={errors.email?.message}>
                    <input className="zh-input" type="email" disabled={saving} {...register('email')} />
                  </ZHField>
                  <ZHField label={t('suppliers.form.category')}>
                    <select className="zh-input" disabled={saving} {...register('category')}>
                      <option value="">{t('suppliers.form.categorySelect')}</option>
                      {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </select>
                  </ZHField>
                </div>
                <ZHField label={t('suppliers.form.status')}>
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="active">{t('suppliers.status.active')}</option>
                    <option value="pending">{t('suppliers.status.pending')}</option>
                    <option value="inactive">{t('suppliers.status.inactive')}</option>
                  </select>
                </ZHField>
                <div className="pg-actions-bar">
                  <div />
                  <div className="pg-actions-buttons">
                    <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal}>{t('common.cancel')}</ZHBtn>
                    <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                      {saving ? t('common.saving') : t('common.saveChanges')}
                    </ZHBtn>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
