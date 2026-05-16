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

type TabId = 'clientes' | 'contactos' | 'categorias' | 'auditoria';

type ContactItem = {
  id: string;
  customerId: string;
  name: string;
  role: string;
  email: string;
  phone: string;
};

type Categorization = {
  category: 'Premium' | 'Regular' | 'Ocasional';
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

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('ventas.customers.view');
  const canCreate = hasPerm('ventas.customers.create');
  const canEdit   = hasPerm('ventas.customers.update') || canCreate;

  /* ── State ── */
  const [activeTab, setActiveTab]       = useState<TabId>('clientes');
  const [searchQuery, setSearchQuery]   = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'activo' | 'inactivo'>('all');

  const [customerModalOpen, setCustomerModalOpen]   = useState(false);
  const [editingCustomerId, setEditingCustomerId]   = useState<string | null>(null);
  const [customerStatus, setCustomerStatus]         = useState<'activo' | 'inactivo'>('activo');

  const [contacts, setContacts]                     = useState<ContactItem[]>([]);
  const [contactModalOpen, setContactModalOpen]     = useState(false);
  const [editingContactId, setEditingContactId]     = useState<string | null>(null);
  const [contactForm, setContactForm]               = useState<Omit<ContactItem, 'id'>>({
    customerId: '', name: '', role: '', email: '', phone: '',
  });

  const [categorizationByCustomer, setCategorizationByCustomer] = useState<Record<string, Categorization>>({});
  const [selectedCategoryCustomerId, setSelectedCategoryCustomerId] = useState('');
  const [categoryValue, setCategoryValue]           = useState<Categorization['category']>('Regular');
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
    if (statusFilter === 'activo')   list = list.filter((c) => c.isActive);
    if (statusFilter === 'inactivo') list = list.filter((c) => !c.isActive);
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter((c) =>
      c.fullName.toLowerCase().includes(term) ||
      c.identificationNumber.toLowerCase().includes(term) ||
      (c.email ?? '').toLowerCase().includes(term)
    );
  }, [customers, searchQuery, statusFilter]);

  const contactRows = useMemo(() =>
    contacts.map((item) => ({
      ...item,
      customerName: customers.find((c) => c.id === item.customerId)?.fullName ?? '-',
    })), [contacts, customers]);

  const categorizationRows = useMemo(() =>
    customers.map((c) => ({
      customerId: c.id,
      customerName: c.fullName,
      category: categorizationByCustomer[c.id]?.category ?? 'Regular',
      tags: categorizationByCustomer[c.id]?.tags ?? '',
    })), [categorizationByCustomer, customers]);

  const totals = useMemo(() => ({
    total:      customers.length,
    activos:    customers.filter((c) => c.isActive).length,
    inactivos:  customers.filter((c) => !c.isActive).length,
    sinEmail:   customers.filter((c) => !c.email).length,
  }), [customers]);

  /* ── Helpers ── */
  const pushAudit = (action: string, customerName: string, details: string) =>
    setAuditItems((prev) => [{ id: buildId(), at: new Date().toISOString(), user: 'admin@zhtechnologies.com', action, customerName, details }, ...prev].slice(0, 50));

  /* ── Customer modal ── */
  const openCreateModal = () => {
    setEditingCustomerId(null);
    setCustomerStatus('activo');
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
    setCustomerStatus(customer.isActive ? 'activo' : 'inactivo');
    setCustomerModalOpen(true);
  };

  const closeCustomerModal = () => {
    setCustomerModalOpen(false);
    setEditingCustomerId(null);
    setCustomerStatus('activo');
    reset(defaultCustomerValues);
  };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      identification: values.identification,
      fullName: values.fullName,
      email: values.email ?? null,
      phone: values.phone ?? null,
      address: values.address ?? null,
      isActive: customerStatus === 'activo',
    };
    if (editingCustomerId) {
      if (!canEdit) return;
      const updated = await updateCustomer(editingCustomerId, payload);
      if (!updated) return;
      pushAudit('Editar cliente', updated.fullName, `Actualizado: ${updated.identificationNumber}`);
      closeCustomerModal();
    } else {
      if (!canCreate) return;
      const created = await createCustomer(payload);
      if (!created) return;
      pushAudit('Crear cliente', created.fullName, `Creado: ${created.identificationNumber}`);
      closeCustomerModal();
    }
  });

  const handleToggleStatus = async (customer: Customer) => {
    if (!canEdit) return;
    const updated = await toggleCustomerStatus(customer.id, !customer.isActive);
    if (!updated) return;
    pushAudit(updated.isActive ? 'Activar' : 'Inactivar', updated.fullName, `Estado → ${updated.isActive ? 'activo' : 'inactivo'}`);
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
      pushAudit('Editar contacto', customer.fullName, contactForm.name.trim());
    } else {
      setContacts((prev) => [...prev, { id: buildId(), ...contactForm }]);
      pushAudit('Crear contacto', customer.fullName, contactForm.name.trim());
    }
    closeContactModal();
  };

  const deleteContact = (contactId: string) => {
    const contact = contacts.find((c) => c.id === contactId);
    if (!contact) return;
    const customer = customers.find((c) => c.id === contact.customerId);
    setContacts((prev) => prev.filter((c) => c.id !== contactId));
    pushAudit('Eliminar contacto', customer?.fullName ?? '-', contact.name);
    closeContactModal();
  };

  /* ── Categorization ── */
  const onSelectCategoryCustomer = (customerId: string) => {
    setSelectedCategoryCustomerId(customerId);
    const current = categorizationByCustomer[customerId];
    setCategoryValue(current?.category ?? 'Regular');
    setCategoryTags(current?.tags ?? '');
  };

  const saveCategorization = () => {
    if (!selectedCategoryCustomerId) return;
    const customer = customers.find((c) => c.id === selectedCategoryCustomerId);
    if (!customer) return;
    setCategorizationByCustomer((prev) => ({ ...prev, [selectedCategoryCustomerId]: { category: categoryValue, tags: categoryTags.trim() } }));
    pushAudit('Categorización', customer.fullName, `Categoría: ${categoryValue}`);
  };

  if (!canView) return <NoAccessPage title={t('customers.title')} />;

  /* ── Tab content ── */
  const TABS: { id: TabId; label: string }[] = [
    { id: 'clientes',    label: t('customers.tabs.list')     || 'Clientes'       },
    { id: 'contactos',   label: t('customers.tabs.contacts') || 'Contactos'      },
    { id: 'categorias',  label: 'Categorización'                                 },
    { id: 'auditoria',   label: 'Auditoría'                                      },
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
                placeholder={t('customers.searchPlaceholder') || 'Filtrar por nombre o RUC…'}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                aria-label="Buscar cliente"
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
              aria-label="Filtrar por estado"
            >
              <option value="all">Todos</option>
              <option value="activo">Activos</option>
              <option value="inactivo">Inactivos</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span>{filteredCustomers.length} de {customers.length} clientes</span>
          </div>
        </div>

        {/* Table */}
        {loading ? <LoadingState /> : filteredCustomers.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <div className="cls-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Nombre / Razón Social</th>
                  <th>RUC / CI</th>
                  <th>Email</th>
                  <th>Categoría</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filteredCustomers.map((customer) => (
                  <tr key={customer.id}>
                    <td className="subtle mono" style={{ whiteSpace: 'nowrap' }}>#{customer.identificationNumber.slice(0, 6)}</td>
                    <td>
                      <div className="cls-customer-name">{customer.fullName}</div>
                    </td>
                    <td className="mono">{customer.identificationNumber}</td>
                    <td>
                      {customer.email
                        ? <a href={`mailto:${customer.email}`} className="cls-email-link">{customer.email}</a>
                        : <span className="subtle">—</span>}
                    </td>
                    <td>
                      <span className="badge badge--gray">
                        {categorizationByCustomer[customer.id]?.category ?? 'Regular'}
                      </span>
                    </td>
                    <td>
                      <span className={customer.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                        {customer.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td>
                      <div className="cls-actions-cell">
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => openEditModal(customer)}
                          disabled={!canEdit}
                          title="Editar"
                          aria-label="Editar cliente"
                        >
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button
                          type="button"
                          className={`zh-btn zh-btn--ghost zh-btn--sm ${customer.isActive ? 'cls-btn-danger' : 'cls-btn-success'}`}
                          onClick={() => void handleToggleStatus(customer)}
                          disabled={!canEdit || creating}
                          title={customer.isActive ? 'Inactivar' : 'Activar'}
                          aria-label={customer.isActive ? 'Inactivar cliente' : 'Activar cliente'}
                        >
                          <span className="material-symbols-outlined">{customer.isActive ? 'block' : 'check_circle'}</span>
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

    contactos: (
      <>
        <div className="pg-table-controls">
          <div className="pg-table-controls-left" />
          <div className="pg-table-controls-right">
            <ZHBtn variant="primary" size="md" type="button" onClick={() => openContactModal()}>
              <span className="material-symbols-outlined">add</span>
              Nuevo Contacto
            </ZHBtn>
          </div>
        </div>
        {contactRows.length === 0 ? (
          <EmptyState message="No hay contactos registrados todavía." />
        ) : (
          <div className="cls-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Cliente</th>
                  <th>Nombre</th>
                  <th>Cargo</th>
                  <th>Email</th>
                  <th>Teléfono</th>
                  <th>Acciones</th>
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
                        <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => { const c = contacts.find((x) => x.id === contact.id); if (c) openContactModal(c); }}>
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm cls-btn-danger"
                          onClick={() => deleteContact(contact.id)}>
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
          <ZHField label="Seleccionar cliente">
            <select className="zh-input" value={selectedCategoryCustomerId}
              onChange={(e) => onSelectCategoryCustomer(e.target.value)}>
              <option value="">— seleccionar —</option>
              {customers.map((c) => <option key={c.id} value={c.id}>{c.fullName}</option>)}
            </select>
          </ZHField>
          <ZHField label="Categoría">
            <select className="zh-input" value={categoryValue}
              onChange={(e) => setCategoryValue(e.target.value as Categorization['category'])}
              disabled={!selectedCategoryCustomerId}>
              <option value="Premium">Premium</option>
              <option value="Regular">Regular</option>
              <option value="Ocasional">Ocasional</option>
            </select>
          </ZHField>
        </div>
        <ZHField label="Etiquetas (separadas por coma)">
          <input className="zh-input" type="text" value={categoryTags}
            onChange={(e) => setCategoryTags(e.target.value)}
            placeholder="Ej: VIP, Descuento, Mayorista"
            disabled={!selectedCategoryCustomerId} />
        </ZHField>
        <div style={{ marginBottom: 'var(--space-6)' }}>
          <ZHBtn variant="primary" size="md" type="button"
            onClick={saveCategorization} disabled={!selectedCategoryCustomerId}>
            Guardar categorización
          </ZHBtn>
        </div>
        <div className="cls-table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Cliente</th>
                <th>Categoría</th>
                <th>Etiquetas</th>
              </tr>
            </thead>
            <tbody>
              {categorizationRows.map((row) => (
                <tr key={row.customerId}>
                  <td>{row.customerName}</td>
                  <td><span className="badge badge--blue">{row.category}</span></td>
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
        <EmptyState message="No hay eventos de auditoría en esta sesión." />
      ) : (
        <div className="cls-table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Fecha / Hora</th>
                <th>Usuario</th>
                <th>Acción</th>
                <th>Cliente</th>
                <th>Detalles</th>
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
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.sales') || 'Ventas'}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('customers.title') || 'Clientes'}</span>
          </nav>
          <h1 className="pg-title">{t('customers.title') || 'Administración de Clientes'}</h1>
          <p className="pg-subtitle">Gestione la base de datos de clientes corporativos y sus estados.</p>
        </div>
        {canCreate && (
          <div className="pg-header-right">
            <ZHBtn variant="primary" size="md" type="button" disabled={creating} onClick={openCreateModal}>
              <span className="material-symbols-outlined">person_add</span>
              Nuevo Cliente
            </ZHBtn>
          </div>
        )}
      </div>

      {/* ── Errors ── */}
      {error      ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error}       /> : null}
      {createError? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}

      {/* ── Main section ── */}
      <div className="pg-section">
        {/* Tabs */}
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label="Secciones de clientes">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={activeTab === tab.id}
                className={activeTab === tab.id ? 'is-active' : ''}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </div>
        </div>

        {/* Tab panel */}
        <div role="tabpanel">
          {tabContent[activeTab]}
        </div>
      </div>

      {/* ── KPI summary ── */}
      <div className="pg-kpis">
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--primary">
            <span className="material-symbols-outlined">group</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Total Clientes</p>
            <p className="pg-kpi-value">{totals.total}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--success">
            <span className="material-symbols-outlined">how_to_reg</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Activos</p>
            <p className="pg-kpi-value">{totals.activos}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--warning">
            <span className="material-symbols-outlined">pending</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Sin Email</p>
            <p className="pg-kpi-value">{totals.sinEmail}</p>
          </div>
        </div>
        <div className="pg-kpi pg-kpi--h">
          <div className="pg-kpi-icon pg-kpi-icon--error">
            <span className="material-symbols-outlined">person_off</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Inactivos</p>
            <p className="pg-kpi-value">{totals.inactivos}</p>
          </div>
        </div>
      </div>

      {/* ── Customer modal ── */}
      {customerModalOpen && (
        <div className="zh-modal-overlay" role="dialog" aria-modal="true"
          aria-label={editingCustomerId ? 'Editar cliente' : 'Nuevo cliente'}
          onClick={(e) => { if (e.target === e.currentTarget) closeCustomerModal(); }}>
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">
                {editingCustomerId ? 'Editar Cliente' : 'Nuevo Cliente'}
              </h2>
              <button type="button" className="zh-modal-close" onClick={closeCustomerModal} aria-label="Cerrar">✕</button>
            </div>
            <div className="zh-modal-body">
              <form onSubmit={onSubmit}>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="RUC / Cédula" required
                    error={errors.identification?.message ? t(String(errors.identification.message)) : undefined}>
                    <input className="zh-input" placeholder="10 o 13 dígitos" disabled={creating} {...register('identification')} />
                  </ZHField>
                  <ZHField label="Razón Social / Nombre" required
                    error={errors.fullName?.message ? t(String(errors.fullName.message)) : undefined}>
                    <input className="zh-input" placeholder="Nombre completo" disabled={creating} {...register('fullName')} />
                  </ZHField>
                </div>
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label="Email" required
                    error={errors.email?.message ? t(String(errors.email.message)) : undefined}>
                    <input className="zh-input" type="email" placeholder="cliente@ejemplo.com" disabled={creating} {...register('email')} />
                  </ZHField>
                  <ZHField label="Teléfono">
                    <input className="zh-input" placeholder="0999123456" disabled={creating} {...register('phone')} />
                  </ZHField>
                </div>
                <ZHField label="Dirección">
                  <textarea className="zh-input" rows={2} disabled={creating} {...register('address')} />
                </ZHField>
                <ZHField label="Estado">
                  <select className="zh-input" value={customerStatus}
                    onChange={(e) => setCustomerStatus(e.target.value as 'activo' | 'inactivo')}>
                    <option value="activo">Activo</option>
                    <option value="inactivo">Inactivo</option>
                  </select>
                </ZHField>
                <div className="cls-modal-actions">
                  <ZHBtn variant="ghost" size="md" type="button" onClick={closeCustomerModal}>Cancelar</ZHBtn>
                  <ZHBtn variant="primary" size="md" type="submit" disabled={creating || (!canCreate && !editingCustomerId)}>
                    {creating ? t('common.saving') : 'Guardar'}
                  </ZHBtn>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* ── Contact modal ── */}
      {contactModalOpen && (
        <div className="zh-modal-overlay" role="dialog" aria-modal="true"
          aria-label={editingContactId ? 'Editar contacto' : 'Nuevo contacto'}
          onClick={(e) => { if (e.target === e.currentTarget) closeContactModal(); }}>
          <div className="zh-modal">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">{editingContactId ? 'Editar Contacto' : 'Nuevo Contacto'}</h2>
              <button type="button" className="zh-modal-close" onClick={closeContactModal} aria-label="Cerrar">✕</button>
            </div>
            <div className="zh-modal-body">
              <ZHField label="Cliente" required>
                <select className="zh-input" value={contactForm.customerId}
                  onChange={(e) => setContactForm((p) => ({ ...p, customerId: e.target.value }))}>
                  <option value="">— seleccionar cliente —</option>
                  {customers.map((c) => <option key={c.id} value={c.id}>{c.fullName}</option>)}
                </select>
              </ZHField>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Nombre" required>
                  <input className="zh-input" value={contactForm.name}
                    onChange={(e) => setContactForm((p) => ({ ...p, name: e.target.value }))} />
                </ZHField>
                <ZHField label="Cargo">
                  <input className="zh-input" value={contactForm.role}
                    onChange={(e) => setContactForm((p) => ({ ...p, role: e.target.value }))} />
                </ZHField>
              </div>
              <div className="pg-form-grid pg-form-grid--2">
                <ZHField label="Email" required>
                  <input className="zh-input" type="email" value={contactForm.email}
                    onChange={(e) => setContactForm((p) => ({ ...p, email: e.target.value }))} />
                </ZHField>
                <ZHField label="Teléfono">
                  <input className="zh-input" value={contactForm.phone}
                    onChange={(e) => setContactForm((p) => ({ ...p, phone: e.target.value }))} />
                </ZHField>
              </div>
              <div className="cls-modal-actions">
                {editingContactId && (
                  <ZHBtn variant="destructive" size="md" type="button" onClick={() => deleteContact(editingContactId)}>
                    Eliminar
                  </ZHBtn>
                )}
                <ZHBtn variant="ghost" size="md" type="button" onClick={closeContactModal}>Cancelar</ZHBtn>
                <ZHBtn variant="primary" size="md" type="button" onClick={saveContact}>Guardar</ZHBtn>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
