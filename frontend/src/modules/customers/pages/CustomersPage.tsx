import { useMemo, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import type { Customer } from '../api/customerService';
import { useCustomers } from '../hooks/useCustomers';
import { customerSchema, defaultCustomerValues, type CustomerFormValues } from '../schemas/customerSchema';
import './customers-page.css';

type TabId = 'clientes' | 'categorias' | 'contactos' | 'auditoria';

type ContactItem = {
  id: string;
  customerId: string;
  name: string;
  role: string;
  email: string;
  phone: string;
};

type CustomerCategory = 'Corporativo' | 'Minorista' | 'Gobierno';

type Categorization = {
  category: CustomerCategory;
  tags: string;
};

type AuditItem = {
  id: string;
  at: string;
  user: string;
  action: string;
  customerName: string;
  details: string;
};

function buildId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function getInitials(name: string): string {
  const words = name.trim().split(/\s+/);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

function categoryBadgeClass(category: CustomerCategory): string {
  if (category === 'Corporativo') return 'badge badge--blue';
  if (category === 'Gobierno') return 'badge badge--orange';
  return 'badge badge--gray';
}

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('sales.customers.view');
  const canCreate = hasPerm('sales.customers.create');
  const canEdit   = hasPerm('sales.customers.update') || canCreate;

  /* ── State ── */
  const [activeTab, setActiveTab]       = useState<TabId>('clientes');
  const [searchQuery, setSearchQuery]   = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [categoryFilter, setCategoryFilter] = useState<'all' | CustomerCategory>('all');

  const [customerModalOpen, setCustomerModalOpen]   = useState(false);
  const [editingCustomerId, setEditingCustomerId]   = useState<string | null>(null);
  const [customerStatus, setCustomerStatus]         = useState<'active' | 'inactive'>('active');

  const [contacts, setContacts]                     = useState<ContactItem[]>([]);
  const [contactModalOpen, setContactModalOpen]     = useState(false);
  const [editingContactId, setEditingContactId]     = useState<string | null>(null);
  const [contactForm, setContactForm]               = useState<Omit<ContactItem, 'id'>>({
    customerId: '', name: '', role: '', email: '', phone: '',
  });

  const [categorizationByCustomer, setCategorizationByCustomer] = useState<Record<string, Categorization>>({});
  const [selectedCategoryCustomerId, setSelectedCategoryCustomerId] = useState('');
  const [categoryValue, setCategoryValue]           = useState<CustomerCategory>('Minorista');
  const [categoryTags, setCategoryTags]             = useState('');

  const [auditItems, setAuditItems] = useState<AuditItem[]>([]);

  /* ── Data ── */
  const { customers, loading, error, creating, createError, createCustomer, updateCustomer, toggleCustomerStatus } = useCustomers();

  const { register, handleSubmit, reset, setValue, formState: { errors } } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: defaultCustomerValues,
  });

  /* ── Derived ── */
  const filteredCustomers = useMemo(() => {
    let list = customers;
    if (statusFilter === 'active')   list = list.filter((c) => c.isActive);
    if (statusFilter === 'inactive') list = list.filter((c) => !c.isActive);
    if (categoryFilter !== 'all') {
      list = list.filter((c) => categorizationByCustomer[c.id]?.category === categoryFilter);
    }
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter((c) =>
      c.fullName.toLowerCase().includes(term) ||
      c.identificationNumber.toLowerCase().includes(term) ||
      (c.email ?? '').toLowerCase().includes(term)
    );
  }, [customers, searchQuery, statusFilter, categoryFilter, categorizationByCustomer]);

  const contactRows = useMemo(() =>
    contacts.map((item) => ({
      ...item,
      customerName: customers.find((c) => c.id === item.customerId)?.fullName ?? '-',
    })), [contacts, customers]);

  const categorizationRows = useMemo(() =>
    customers.map((c) => ({
      customerId: c.id,
      customerName: c.fullName,
      category: (categorizationByCustomer[c.id]?.category ?? 'Minorista') as CustomerCategory,
      tags: categorizationByCustomer[c.id]?.tags ?? '',
    })), [categorizationByCustomer, customers]);

  const totals = useMemo(() => ({
    total:    customers.length,
    active:   customers.filter((c) => c.isActive).length,
    inactive: customers.filter((c) => !c.isActive).length,
    noEmail:  customers.filter((c) => !c.email).length,
  }), [customers]);

  const contactCountByCustomer = useMemo(() => {
    const counts: Record<string, number> = {};
    contacts.forEach((c) => { counts[c.customerId] = (counts[c.customerId] ?? 0) + 1; });
    return counts;
  }, [contacts]);

  /* ── Helpers ── */
  const pushAudit = (action: string, customerName: string, details: string) =>
    setAuditItems((prev) => [
      { id: buildId(), at: new Date().toISOString(), user: 'admin@zhtechnologies.com', action, customerName, details },
      ...prev,
    ].slice(0, 50));

  /* ── Customer modal ── */
  const openCreateModal = () => {
    setEditingCustomerId(null);
    setCustomerStatus('active');
    reset(defaultCustomerValues);
    setCustomerModalOpen(true);
  };

  const openEditModal = (customer: Customer) => {
    setEditingCustomerId(customer.id);
    setValue('identification', customer.identificationNumber);
    setValue('fullName', customer.fullName);
    setValue('email', customer.email ?? '');
    setValue('phone', customer.phone ?? '');
    setValue('address', customer.address ?? '');
    setCustomerStatus(customer.isActive ? 'active' : 'inactive');
    setCustomerModalOpen(true);
  };

  const closeCustomerModal = () => {
    setCustomerModalOpen(false);
    setEditingCustomerId(null);
    setCustomerStatus('active');
    reset(defaultCustomerValues);
  };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      identification: values.identification,
      fullName: values.fullName,
      email: values.email ?? null,
      phone: values.phone ?? null,
      address: values.address ?? null,
      isActive: customerStatus === 'active',
    };
    if (editingCustomerId) {
      if (!canEdit) return;
      const updated = await updateCustomer(editingCustomerId, payload);
      if (!updated) return;
      pushAudit('Edit customer', updated.fullName, `Updated: ${updated.identificationNumber}`);
      closeCustomerModal();
    } else {
      if (!canCreate) return;
      const created = await createCustomer(payload);
      if (!created) return;
      pushAudit('Create customer', created.fullName, `Created: ${created.identificationNumber}`);
      closeCustomerModal();
    }
  });

  const handleToggleStatus = async (customer: Customer) => {
    if (!canEdit) return;
    const updated = await toggleCustomerStatus(customer.id, !customer.isActive);
    if (!updated) return;
    pushAudit(
      updated.isActive ? 'Enable customer' : 'Disable customer',
      updated.fullName,
      `Status → ${updated.isActive ? 'active' : 'inactive'}`
    );
  };

  /* ── Contact modal ── */
  const openContactModal = (contact?: ContactItem) => {
    if (contact) {
      setEditingContactId(contact.id);
      setContactForm({ customerId: contact.customerId, name: contact.name, role: contact.role, email: contact.email, phone: contact.phone });
    } else {
      setEditingContactId(null);
      setContactForm({ customerId: '', name: '', role: '', email: '', phone: '' });
    }
    setContactModalOpen(true);
  };

  const closeContactModal = () => { setContactModalOpen(false); setEditingContactId(null); };

  const saveContact = () => {
    const customer = customers.find((c) => c.id === contactForm.customerId);
    if (!customer || !contactForm.name.trim() || !contactForm.email.trim()) return;
    if (editingContactId) {
      setContacts((prev) => prev.map((c) => c.id === editingContactId ? { ...c, ...contactForm } : c));
      pushAudit('Edit contact', customer.fullName, contactForm.name.trim());
    } else {
      setContacts((prev) => [...prev, { id: buildId(), ...contactForm }]);
      pushAudit('Create contact', customer.fullName, contactForm.name.trim());
    }
    closeContactModal();
  };

  const deleteContact = (contactId: string) => {
    const contact = contacts.find((c) => c.id === contactId);
    if (!contact) return;
    const customer = customers.find((c) => c.id === contact.customerId);
    setContacts((prev) => prev.filter((c) => c.id !== contactId));
    pushAudit('Delete contact', customer?.fullName ?? '-', contact.name);
    closeContactModal();
  };

  /* ── Categorization ── */
  const onSelectCategoryCustomer = (customerId: string) => {
    setSelectedCategoryCustomerId(customerId);
    const current = categorizationByCustomer[customerId];
    setCategoryValue(current?.category ?? 'Minorista');
    setCategoryTags(current?.tags ?? '');
  };

  const saveCategorization = () => {
    if (!selectedCategoryCustomerId) return;
    const customer = customers.find((c) => c.id === selectedCategoryCustomerId);
    if (!customer) return;
    setCategorizationByCustomer((prev) => ({
      ...prev,
      [selectedCategoryCustomerId]: { category: categoryValue, tags: categoryTags.trim() },
    }));
    pushAudit('Categorize customer', customer.fullName, `Category: ${categoryValue}`);
  };

  if (!canView) return <NoAccessPage title={t('customers.title')} />;

  /* ── Tab definitions ── */
  const TABS: { id: TabId; label: string; icon: string }[] = [
    { id: 'clientes',   label: t('customers.tabs.list'),       icon: 'person'   },
    { id: 'categorias', label: t('customers.tabs.categories'), icon: 'category' },
    { id: 'contactos',  label: t('customers.tabs.contacts'),   icon: 'contacts' },
    { id: 'auditoria',  label: t('customers.tabs.audit'),      icon: 'history'  },
  ];

  const tabContent: Record<TabId, ReactNode> = {
    clientes: (
      <>
        {/* Filter bar */}
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                className="zh-input"
                type="search"
                placeholder={t('customers.list.searchPlaceholder')}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                aria-label={t('customers.list.searchPlaceholder')}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
              aria-label={t('customers.table.status')}
            >
              <option value="all">{t('customers.filter.all')}</option>
              <option value="active">{t('customers.filter.active')}</option>
              <option value="inactive">{t('customers.filter.inactive')}</option>
            </select>
            <select
              className="zh-input"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value as typeof categoryFilter)}
              aria-label={t('customers.table.category')}
            >
              <option value="all">{t('customers.filter.allCategories')}</option>
              <option value="Corporativo">{t('customers.filter.corporate')}</option>
              <option value="Minorista">{t('customers.filter.retail')}</option>
              <option value="Gobierno">{t('customers.filter.government')}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">
              {filteredCustomers.length} / {customers.length} {t('customers.list.entityLabel')}
            </span>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <LoadingState />
        ) : filteredCustomers.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>{t('customers.table.customerCompany')}</th>
                  <th>{t('customers.table.contact')}</th>
                  <th>{t('customers.table.category')}</th>
                  <th>{t('customers.table.status')}</th>
                  <th>{t('customers.table.contacts')}</th>
                  <th>{t('customers.col.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredCustomers.map((customer) => {
                  const cat = categorizationByCustomer[customer.id]?.category ?? 'Minorista';
                  return (
                    <tr key={customer.id}>
                      <td className="subtle mono" style={{ whiteSpace: 'nowrap' }}>
                        #{customer.identificationNumber.slice(0, 8)}
                      </td>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                          <div className="zh-avatar zh-avatar--square">{getInitials(customer.fullName)}</div>
                          <div>
                            <div style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>{customer.fullName}</div>
                            <div className="subtle" style={{ fontSize: 'var(--text-body-sm-size)' }}>
                              {customer.identificationType}: {customer.identificationNumber}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                          {customer.email
                            ? <span style={{ color: 'var(--color-text-primary)' }}>{customer.email}</span>
                            : <span className="subtle">—</span>}
                          {customer.phone && (
                            <span className="subtle">{customer.phone}</span>
                          )}
                        </div>
                      </td>
                      <td>
                        <span className={categoryBadgeClass(cat as CustomerCategory)}>{cat}</span>
                      </td>
                      <td>
                        <span className={customer.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                          {customer.isActive ? t('customers.status.active') : t('customers.status.inactive')}
                        </span>
                      </td>
                      <td className="subtle">
                        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-1)' }}>
                          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>groups</span>
                          {contactCountByCustomer[customer.id] ?? 0}
                        </div>
                      </td>
                      <td>
                        <div className="cls-actions-cell">
                          <button
                            type="button"
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            onClick={() => openEditModal(customer)}
                            disabled={!canEdit}
                            aria-label="Edit"
                          >
                            <span className="material-symbols-outlined">edit</span>
                          </button>
                          <button
                            type="button"
                            className={`zh-btn zh-btn--ghost zh-btn--sm ${customer.isActive ? 'cls-btn-danger' : 'cls-btn-success'}`}
                            onClick={() => void handleToggleStatus(customer)}
                            disabled={!canEdit || creating}
                            aria-label={customer.isActive ? 'Disable' : 'Enable'}
                          >
                            <span className="material-symbols-outlined">
                              {customer.isActive ? 'block' : 'check_circle'}
                            </span>
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </>
    ),

    contactos: (
      <>
        {contactRows.length === 0 ? (
          <EmptyState message={t('customers.contacts.empty')} />
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>{t('customers.contacts.table.customer')}</th>
                  <th>{t('customers.contacts.table.name')}</th>
                  <th>{t('customers.contacts.table.role')}</th>
                  <th>{t('customers.form.email')}</th>
                  <th>{t('customers.form.phone')}</th>
                  <th>{t('customers.col.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {contactRows.map((contact) => (
                  <tr key={contact.id}>
                    <td>{contact.customerName}</td>
                    <td>{contact.name}</td>
                    <td>{contact.role || <span className="subtle">—</span>}</td>
                    <td>{contact.email}</td>
                    <td>{contact.phone || <span className="subtle">—</span>}</td>
                    <td>
                      <div className="cls-actions-cell">
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => { const c = contacts.find((x) => x.id === contact.id); if (c) openContactModal(c); }}
                          aria-label="Edit contact"
                        >
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm cls-btn-danger"
                          onClick={() => deleteContact(contact.id)}
                          aria-label="Delete contact"
                        >
                          <span className="material-symbols-outlined">delete</span>
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </>
    ),

    categorias: (
      <div className="pg-section-body">
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label={t('customers.categories.select')}>
            <select
              className="zh-input"
              value={selectedCategoryCustomerId}
              onChange={(e) => onSelectCategoryCustomer(e.target.value)}
            >
              <option value="">— {t('customers.categories.select')} —</option>
              {customers.map((c) => <option key={c.id} value={c.id}>{c.fullName}</option>)}
            </select>
          </ZHField>
          <ZHField label={t('customers.categories.category')}>
            <select
              className="zh-input"
              value={categoryValue}
              onChange={(e) => setCategoryValue(e.target.value as CustomerCategory)}
              disabled={!selectedCategoryCustomerId}
            >
              <option value="Corporativo">{t('customers.categories.corporate')}</option>
              <option value="Minorista">{t('customers.categories.retail')}</option>
              <option value="Gobierno">{t('customers.categories.government')}</option>
            </select>
          </ZHField>
        </div>
        <ZHField label={t('customers.categories.tags')}>
          <input
            className="zh-input"
            type="text"
            value={categoryTags}
            onChange={(e) => setCategoryTags(e.target.value)}
            placeholder={t('customers.categories.tagsHint')}
            disabled={!selectedCategoryCustomerId}
          />
        </ZHField>
        <div style={{ marginBottom: 'var(--space-6)' }}>
          <ZHBtn variant="primary" size="md" type="button" onClick={saveCategorization} disabled={!selectedCategoryCustomerId}>
            {t('customers.categories.save')}
          </ZHBtn>
        </div>
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th>{t('customers.categories.table.customer')}</th>
                <th>{t('customers.categories.table.category')}</th>
                <th>{t('customers.categories.table.tags')}</th>
              </tr>
            </thead>
            <tbody>
              {categorizationRows.map((row) => (
                <tr key={row.customerId}>
                  <td>{row.customerName}</td>
                  <td>
                    <span className={categoryBadgeClass(row.category)}>{row.category}</span>
                  </td>
                  <td>{row.tags || <span className="subtle">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    ),

    auditoria: (
      auditItems.length === 0 ? (
        <EmptyState message={t('customers.audit.empty')} />
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th>{t('customers.audit.table.datetime')}</th>
                <th>{t('customers.audit.table.user')}</th>
                <th>{t('customers.audit.table.action')}</th>
                <th>{t('customers.audit.table.customer')}</th>
                <th>{t('customers.audit.table.details')}</th>
              </tr>
            </thead>
            <tbody>
              {auditItems.map((item) => (
                <tr key={item.id}>
                  <td className="mono subtle">{new Date(item.at).toLocaleString()}</td>
                  <td className="subtle">{item.user}</td>
                  <td>{item.action}</td>
                  <td>{item.customerName}</td>
                  <td className="subtle">{item.details}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )
    ),
  };

  return (
    <div className="pg-page">

      {/* ── Page header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <p className="pg-kicker">{t('customers.kicker')}</p>
          <h1 className="pg-title">{t('customers.title')}</h1>
          <p className="pg-subtitle">{t('customers.subtitle')}</p>
        </div>
        <div className="pg-header-right">
          {activeTab === 'contactos' ? (
            <ZHBtn variant="primary" size="md" type="button" onClick={() => openContactModal()}>
              <span className="material-symbols-outlined">contacts</span>
              {t('customers.contacts.new')}
            </ZHBtn>
          ) : canCreate ? (
            <ZHBtn variant="primary" size="md" type="button" disabled={creating} onClick={openCreateModal}>
              <span className="material-symbols-outlined">person_add</span>
              {t('customers.list.newAction')}
            </ZHBtn>
          ) : null}
        </div>
      </div>

      {/* ── Errors ── */}
      {error       && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
      {createError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} />}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">group</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('customers.kpi.total')}</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">how_to_reg</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('customers.kpi.active')}</p>
            <p className="pg-kpi-value">{totals.active}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--warning">
            <span className="material-symbols-outlined">mark_email_unread</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('customers.kpi.noEmail')}</p>
            <p className="pg-kpi-value">{totals.noEmail}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">person_off</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('customers.kpi.inactive')}</p>
            <p className="pg-kpi-value">{totals.inactive}</p>
          </div>
        </div>
      </div>

      {/* ── Main section ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label={t('customers.title')}>
            {TABS.map((tab) => (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={activeTab === tab.id}
                className={activeTab === tab.id ? 'is-active' : ''}
                onClick={() => setActiveTab(tab.id)}
              >
                <span className="material-symbols-outlined">{tab.icon}</span>
                {tab.label}
              </button>
            ))}
          </div>
        </div>
        <div role="tabpanel">
          {tabContent[activeTab]}
        </div>
      </div>

      {/* ── Customer modal ── */}
      {customerModalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingCustomerId ? t('customers.modal.editTitle') : t('customers.modal.createTitle')}
          onClick={(e) => { if (e.target === e.currentTarget) closeCustomerModal(); }}
        >
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingCustomerId ? t('customers.modal.editTitle') : t('customers.modal.createTitle')}
              </h2>
              <button type="button" className="zh-modal-close" onClick={closeCustomerModal} aria-label="Close">✕</button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField
                    label={t('customers.form.identification')}
                    required
                    error={errors.identification?.message ? t(String(errors.identification.message)) : undefined}
                  >
                    <input
                      className="zh-input"
                      placeholder={t('customers.form.identificationPlaceholder')}
                      disabled={creating}
                      {...register('identification')}
                    />
                  </ZHField>
                  <ZHField
                    label={t('customers.form.fullName')}
                    required
                    error={errors.fullName?.message ? t(String(errors.fullName.message)) : undefined}
                  >
                    <input
                      className="zh-input"
                      placeholder={t('customers.form.fullNamePlaceholder')}
                      disabled={creating}
                      {...register('fullName')}
                    />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField
                    label={t('customers.form.email')}
                    required
                    error={errors.email?.message ? t(String(errors.email.message)) : undefined}
                  >
                    <input
                      className="zh-input"
                      type="email"
                      placeholder={t('customers.form.emailPlaceholder')}
                      disabled={creating}
                      {...register('email')}
                    />
                  </ZHField>
                  <ZHField label={t('customers.form.phone')}>
                    <input
                      className="zh-input"
                      placeholder={t('customers.form.phonePlaceholder')}
                      disabled={creating}
                      {...register('phone')}
                    />
                  </ZHField>
                </div>
                <ZHField label={t('customers.form.address')}>
                  <textarea className="zh-input" rows={2} disabled={creating} {...register('address')} />
                </ZHField>
                <ZHField label={t('customers.modal.status')}>
                  <select
                    className="zh-input"
                    value={customerStatus}
                    onChange={(e) => setCustomerStatus(e.target.value as 'active' | 'inactive')}
                  >
                    <option value="active">{t('customers.modal.statusActive')}</option>
                    <option value="inactive">{t('customers.modal.statusInactive')}</option>
                  </select>
                </ZHField>
                <div className="pg-actions-bar">
                  <div className="pg-actions-buttons">
                    <ZHBtn variant="ghost" size="md" type="button" onClick={closeCustomerModal}>
                      {t('common.cancel')}
                    </ZHBtn>
                    <ZHBtn
                      variant="primary"
                      size="md"
                      type="submit"
                      disabled={creating || (!canCreate && !editingCustomerId)}
                    >
                      {creating ? t('common.saving') : t('common.save')}
                    </ZHBtn>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* ── Contact modal ── */}
      {contactModalOpen && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-label={editingContactId ? t('customers.contacts.modal.editTitle') : t('customers.contacts.modal.createTitle')}
          onClick={(e) => { if (e.target === e.currentTarget) closeContactModal(); }}
        >
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingContactId ? t('customers.contacts.modal.editTitle') : t('customers.contacts.modal.createTitle')}
              </h2>
              <button type="button" className="zh-modal-close" onClick={closeContactModal} aria-label="Close">✕</button>
            </div>
            <div className="zh-modal-body">
              <ZHField label={t('customers.contacts.table.customer')} required>
                <select
                  className="zh-input"
                  value={contactForm.customerId}
                  onChange={(e) => setContactForm((p) => ({ ...p, customerId: e.target.value }))}
                >
                  <option value="">{t('customers.contacts.modal.selectCustomer')}</option>
                  {customers.map((c) => <option key={c.id} value={c.id}>{c.fullName}</option>)}
                </select>
              </ZHField>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('customers.contacts.modal.name')} required>
                  <input
                    className="zh-input"
                    value={contactForm.name}
                    onChange={(e) => setContactForm((p) => ({ ...p, name: e.target.value }))}
                  />
                </ZHField>
                <ZHField label={t('customers.contacts.modal.role')}>
                  <input
                    className="zh-input"
                    value={contactForm.role}
                    onChange={(e) => setContactForm((p) => ({ ...p, role: e.target.value }))}
                  />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label={t('customers.form.email')} required>
                  <input
                    className="zh-input"
                    type="email"
                    value={contactForm.email}
                    onChange={(e) => setContactForm((p) => ({ ...p, email: e.target.value }))}
                  />
                </ZHField>
                <ZHField label={t('customers.form.phone')}>
                  <input
                    className="zh-input"
                    value={contactForm.phone}
                    onChange={(e) => setContactForm((p) => ({ ...p, phone: e.target.value }))}
                  />
                </ZHField>
              </div>
              <div className="pg-actions-bar">
                <div className="pg-actions-buttons">
                  {editingContactId && (
                    <ZHBtn variant="destructive" size="md" type="button" onClick={() => deleteContact(editingContactId)}>
                      {t('customers.disable')}
                    </ZHBtn>
                  )}
                  <ZHBtn variant="ghost" size="md" type="button" onClick={closeContactModal}>
                    {t('common.cancel')}
                  </ZHBtn>
                  <ZHBtn variant="primary" size="md" type="button" onClick={saveContact}>
                    {t('common.save')}
                  </ZHBtn>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
