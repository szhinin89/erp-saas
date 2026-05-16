import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useI18n } from '../../../../i18n/i18n';
import { useSuppliers } from '../hooks/useSuppliers';
import {
  supplierSchema,
  defaultSupplierValues,
  type SupplierFormValues,
} from '../schemas/supplierSchema';
import type { Supplier } from '../api/supplierService';
import './suppliers-page.css';

type TabId = 'list' | 'form';

const CATEGORIES = ['Supplies', 'Technology', 'Services', 'Manufacturing', 'Logistics', 'Other'];

function statusClass(status: Supplier['status']) {
  if (status === 'active')  return 'zh-status zh-status--active';
  if (status === 'pending') return 'zh-status zh-status--pending';
  return 'zh-status zh-status--inactive';
}

function statusLabel(status: Supplier['status']) {
  if (status === 'active')  return 'Active';
  if (status === 'pending') return 'Pending';
  return 'Inactive';
}

export function SuppliersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canView   = isAdmin || hasPerm('compras.suppliers.view');
  const canCreate = isAdmin || hasPerm('compras.suppliers.create');
  const canEdit   = isAdmin || hasPerm('compras.suppliers.edit');

  const { suppliers, loading, error, saving, saveError, createSupplier, updateSupplier, setSupplierStatus } =
    useSuppliers();

  /* ── UI state ── */
  const [activeTab,     setActiveTab]     = useState<TabId>('list');
  const [searchQuery,   setSearchQuery]   = useState('');
  const [statusFilter,  setStatusFilter]  = useState<'all' | 'active' | 'pending' | 'inactive'>('all');
  const [modalOpen,     setModalOpen]     = useState(false);
  const [editingId,     setEditingId]     = useState<string | null>(null);

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

  if (!canView) return <NoAccessPage title="Suppliers" />;

  const anyError = error || saveError;

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Breadcrumb">
            <span className="pg-breadcrumb-item">Purchases</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Suppliers</span>
          </nav>
          <h1 className="pg-title">Supplier Management</h1>
          <p className="pg-subtitle">Manage the supplier directory, statuses and payment terms.</p>
        </div>
        <div className="pg-header-right">
          <ZHBtn variant="ghost" size="md" type="button">
            <span className="material-symbols-outlined">download</span>
            Export
          </ZHBtn>
          {canCreate && (
            <ZHBtn variant="primary" size="md" type="button" onClick={openCreateModal}>
              <span className="material-symbols-outlined">add</span>
              New Supplier
            </ZHBtn>
          )}
        </div>
      </div>

      {/* ── Errors ── */}
      {anyError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={anyError} /> : null}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">storefront</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Total Suppliers</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">verified</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Active</p>
            <p className="pg-kpi-value">{totals.active}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--warning">
            <span className="material-symbols-outlined">pending</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Pending</p>
            <p className="pg-kpi-value">{totals.pending}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">block</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Inactive</p>
            <p className="pg-kpi-value">{totals.inactive}</p>
          </div>
        </div>
      </div>

      {/* ── Main section ── */}
      <div className="pg-section">

        {/* Tabs */}
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label="Supplier sections">
            <button
              type="button" role="tab"
              aria-selected={activeTab === 'list'}
              className={activeTab === 'list' ? 'is-active' : ''}
              onClick={() => setActiveTab('list')}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>view_list</span>
              List
            </button>
            {canCreate && (
              <button
                type="button" role="tab"
                aria-selected={activeTab === 'form'}
                className={activeTab === 'form' ? 'is-active' : ''}
                onClick={handleNewInTab}
              >
                <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>add_box</span>
                New Supplier
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
                    placeholder="Search by tax ID or name…"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    aria-label="Search supplier"
                  />
                </div>
                <select
                  className="zh-input"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
                  aria-label="Filter by status"
                >
                  <option value="all">All</option>
                  <option value="active">Active</option>
                  <option value="pending">Pending</option>
                  <option value="inactive">Inactive</option>
                </select>
              </div>
              <div className="pg-table-controls-right">
                <span>{filtered.length} of {suppliers.length} suppliers</span>
              </div>
            </div>

            {loading ? (
              <LoadingState />
            ) : filtered.length === 0 ? (
              <EmptyState message={suppliers.length === 0 ? 'No suppliers registered yet.' : 'No results for the applied filters.'} />
            ) : (
              <>
                <div className="prv-table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Tax ID</th>
                        <th>Legal Name</th>
                        <th>Primary Contact</th>
                        <th>Phone</th>
                        <th>Status</th>
                        {canEdit ? <th style={{ textAlign: 'right' }}>Actions</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {filtered.map((supplier) => (
                        <tr key={supplier.id} style={{ opacity: supplier.status === 'inactive' ? 0.65 : 1 }}>
                          <td className="mono subtle" style={{ whiteSpace: 'nowrap' }}>{supplier.taxId}</td>
                          <td>
                            <div className="prv-supplier-name">{supplier.legalName}</div>
                            {supplier.category && (
                              <div className="prv-contact-sub">{supplier.category}</div>
                            )}
                          </td>
                          <td>{supplier.primaryContact ?? <span className="subtle">—</span>}</td>
                          <td className="mono">{supplier.phone ?? <span className="subtle">—</span>}</td>
                          <td>
                            <span className={statusClass(supplier.status)}>
                              {statusLabel(supplier.status)}
                            </span>
                          </td>
                          {canEdit ? (
                            <td style={{ textAlign: 'right' }}>
                              <div className="prv-actions-cell" style={{ justifyContent: 'flex-end' }}>
                                <button
                                  type="button"
                                  className="zh-btn zh-btn--ghost zh-btn--sm"
                                  title="Edit"
                                  onClick={() => openEditModal(supplier)}
                                  aria-label="Edit supplier"
                                >
                                  <span className="material-symbols-outlined">edit</span>
                                </button>
                                <button
                                  type="button"
                                  className={`zh-btn zh-btn--ghost zh-btn--sm ${supplier.status === 'active' ? 'prv-btn-block' : 'prv-btn-activate'}`}
                                  title={supplier.status === 'active' ? 'Deactivate' : 'Activate'}
                                  disabled={saving}
                                  onClick={() => void handleToggleStatus(supplier)}
                                  aria-label={supplier.status === 'active' ? 'Deactivate supplier' : 'Activate supplier'}
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
                  <p className="subtle" style={{ fontSize: 12, margin: 0 }}>{filtered.length} results</p>
                  <p className="pg-table-timestamp">Updated this session</p>
                </div>
              </>
            )}

            {/* Secondary panels */}
            <div className="prv-panels-grid">
              <div className="pg-kpi" style={{ flexDirection: 'column', gap: 'var(--space-3)', height: 'auto' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <p className="pg-kpi-label" style={{ margin: 0 }}>Management Notes</p>
                  <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" aria-label="Add note">
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
                <p className="prv-payment-title">Payment Summary</p>
                <div>
                  <p className="prv-payment-amount">$0.00</p>
                  <p className="prv-payment-sub">Total accumulated this session</p>
                </div>
                <div style={{ display: 'flex', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
                  <ZHBtn variant="primary" size="md" type="button">
                    <span className="material-symbols-outlined">receipt_long</span>
                    View Payments
                  </ZHBtn>
                  <ZHBtn variant="ghost" size="md" type="button">Generate Report</ZHBtn>
                </div>
              </div>
            </div>
          </>
        )}

        {/* ── Form tab ── */}
        {activeTab === 'form' && canCreate && (
          <div style={{ padding: 'var(--space-4) var(--space-5)' }}>
            <form onSubmit={onSubmit}>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Tax ID" required error={errors.taxId?.message}>
                  <input className="zh-input" placeholder="10 or 13 digits" disabled={saving} {...register('taxId')} />
                </ZHField>
                <ZHField label="Legal Name" required error={errors.legalName?.message}>
                  <input className="zh-input" placeholder="Supplier's registered legal name" disabled={saving} {...register('legalName')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Trade Name">
                  <input className="zh-input" placeholder="Brand or trade name" disabled={saving} {...register('tradeName')} />
                </ZHField>
                <ZHField label="Primary Contact">
                  <input className="zh-input" placeholder="Representative's name" disabled={saving} {...register('primaryContact')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Email" error={errors.email?.message}>
                  <input className="zh-input" type="email" placeholder="supplier@company.com" disabled={saving} {...register('email')} />
                </ZHField>
                <ZHField label="Phone">
                  <input className="zh-input" placeholder="0999123456" disabled={saving} {...register('phone')} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Category">
                  <select className="zh-input" disabled={saving} {...register('category')}>
                    <option value="">— select —</option>
                    {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                  </select>
                </ZHField>
                <ZHField label="Status">
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="active">Active</option>
                    <option value="pending">Pending</option>
                    <option value="inactive">Inactive</option>
                  </select>
                </ZHField>
              </div>
              <ZHField label="Address">
                <textarea className="zh-input" rows={2} disabled={saving} {...register('address')} />
              </ZHField>
              <ZHField label="Website">
                <input className="zh-input" type="url" placeholder="https://supplier.com" disabled={saving} {...register('website')} />
              </ZHField>
              <div className="prv-modal-actions">
                <ZHBtn variant="ghost" size="md" type="button" onClick={() => setActiveTab('list')}>Cancel</ZHBtn>
                <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                  {saving ? t('common.saving') : 'Save Supplier'}
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
          aria-label={editingId ? 'Edit supplier' : 'New supplier'}
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">{editingId ? 'Edit Supplier' : 'New Supplier'}</h2>
              <button type="button" className="zh-modal-close" onClick={closeModal} aria-label="Close">✕</button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Tax ID" required error={errors.taxId?.message}>
                    <input className="zh-input" placeholder="10 or 13 digits" disabled={saving} {...register('taxId')} />
                  </ZHField>
                  <ZHField label="Legal Name" required error={errors.legalName?.message}>
                    <input className="zh-input" placeholder="Legal name" disabled={saving} {...register('legalName')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Primary Contact">
                    <input className="zh-input" disabled={saving} {...register('primaryContact')} />
                  </ZHField>
                  <ZHField label="Phone">
                    <input className="zh-input" placeholder="0999123456" disabled={saving} {...register('phone')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Email" error={errors.email?.message}>
                    <input className="zh-input" type="email" disabled={saving} {...register('email')} />
                  </ZHField>
                  <ZHField label="Category">
                    <select className="zh-input" disabled={saving} {...register('category')}>
                      <option value="">— select —</option>
                      {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                    </select>
                  </ZHField>
                </div>
                <ZHField label="Status">
                  <select className="zh-input" disabled={saving} {...register('status')}>
                    <option value="active">Active</option>
                    <option value="pending">Pending</option>
                    <option value="inactive">Inactive</option>
                  </select>
                </ZHField>
                <div className="prv-modal-actions">
                  <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal}>Cancel</ZHBtn>
                  <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                    {saving ? t('common.saving') : 'Save'}
                  </ZHBtn>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
