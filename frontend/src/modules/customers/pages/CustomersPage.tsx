import { useMemo, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../components/PageShell';
import { Alert, Badge, Button, Card, Input, Modal, Tabs } from '../../../components/ui';
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

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = hasPerm('ventas.customers.view');
  const canCreate = hasPerm('ventas.customers.create');
  const canEdit = hasPerm('ventas.customers.update') || canCreate;

  const [searchQuery, setSearchQuery] = useState('');
  const [customerModalOpen, setCustomerModalOpen] = useState(false);
  const [editingCustomerId, setEditingCustomerId] = useState<string | null>(null);
  const [customerStatus, setCustomerStatus] = useState<'activo' | 'inactivo'>('activo');
  const [contacts, setContacts] = useState<ContactItem[]>([]);
  const [contactModalOpen, setContactModalOpen] = useState(false);
  const [editingContactId, setEditingContactId] = useState<string | null>(null);
  const [contactForm, setContactForm] = useState<Omit<ContactItem, 'id'>>({
    customerId: '',
    name: '',
    role: '',
    email: '',
    phone: '',
  });
  const [categorizationByCustomer, setCategorizationByCustomer] = useState<Record<string, Categorization>>({});
  const [selectedCategoryCustomerId, setSelectedCategoryCustomerId] = useState('');
  const [categoryValue, setCategoryValue] = useState<Categorization['category']>('Regular');
  const [categoryTags, setCategoryTags] = useState('');
  const [auditItems, setAuditItems] = useState<AuditItem[]>([]);
  const {
    customers,
    loading,
    error,
    creating,
    createError,
    createCustomer,
    updateCustomer,
    toggleCustomerStatus,
  } = useCustomers();

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: defaultCustomerValues,
  });

  const filteredCustomers = useMemo(() => {
    const term = searchQuery.trim().toLowerCase();
    if (!term) return customers;
    return customers.filter((customer) => {
      return (
        customer.fullName.toLowerCase().includes(term) ||
        customer.identificationNumber.toLowerCase().includes(term) ||
        (customer.email ?? '').toLowerCase().includes(term) ||
        (customer.phone ?? '').toLowerCase().includes(term)
      );
    });
  }, [customers, searchQuery]);

  const contactRows = useMemo(() => {
    return contacts.map((item) => {
      const customer = customers.find((c) => c.id === item.customerId);
      return {
        ...item,
        customerName: customer?.fullName ?? '-',
      };
    });
  }, [contacts, customers]);

  const categorizationRows = useMemo(() => {
    return customers.map((customer) => {
      const item = categorizationByCustomer[customer.id];
      return {
        customerId: customer.id,
        customerName: customer.fullName,
        category: item?.category ?? 'Regular',
        tags: item?.tags ?? '',
      };
    });
  }, [categorizationByCustomer, customers]);

  const pushAudit = (action: string, customerName: string, details: string) => {
    setAuditItems((prev) => [
      {
        id: buildId(),
        at: new Date().toISOString(),
        user: 'admin@zhtechnologies.com',
        action,
        customerName,
        details,
      },
      ...prev,
    ].slice(0, 50));
  };

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
      pushAudit('Editar cliente', updated.fullName, `Se actualizó ${updated.identificationNumber}`);
      closeCustomerModal();
      return;
    }

    if (!canCreate) return;
    const created = await createCustomer(payload);
    if (!created) return;
    pushAudit('Crear cliente', created.fullName, `Se creó ${created.identificationNumber}`);
    closeCustomerModal();
  });

  const handleToggleStatus = async (customer: Customer) => {
    if (!canEdit) return;
    const updated = await toggleCustomerStatus(customer.id, !customer.isActive);
    if (!updated) return;
    pushAudit(
      updated.isActive ? 'Activar cliente' : 'Inactivar cliente',
      updated.fullName,
      `Estado actualizado a ${updated.isActive ? 'activo' : 'inactivo'}`
    );
  };

  const openContactModal = (contact?: ContactItem) => {
    if (contact) {
      setEditingContactId(contact.id);
      setContactForm({
        customerId: contact.customerId,
        name: contact.name,
        role: contact.role,
        email: contact.email,
        phone: contact.phone,
      });
    } else {
      setEditingContactId(null);
      setContactForm({
        customerId: '',
        name: '',
        role: '',
        email: '',
        phone: '',
      });
    }
    setContactModalOpen(true);
  };

  const closeContactModal = () => {
    setContactModalOpen(false);
    setEditingContactId(null);
  };

  const saveContact = () => {
    const customer = customers.find((item) => item.id === contactForm.customerId);
    if (!customer || !contactForm.name.trim() || !contactForm.email.trim()) return;

    if (editingContactId) {
      setContacts((prev) =>
        prev.map((item) =>
          item.id === editingContactId
            ? { ...item, ...contactForm, name: contactForm.name.trim(), email: contactForm.email.trim() }
            : item
        )
      );
      pushAudit('Editar contacto', customer.fullName, `Contacto: ${contactForm.name.trim()}`);
    } else {
      setContacts((prev) => [
        ...prev,
        {
          id: buildId(),
          ...contactForm,
          name: contactForm.name.trim(),
          email: contactForm.email.trim(),
        },
      ]);
      pushAudit('Crear contacto', customer.fullName, `Contacto: ${contactForm.name.trim()}`);
    }
    closeContactModal();
  };

  const deleteContact = (contactId: string) => {
    const contact = contacts.find((item) => item.id === contactId);
    if (!contact) return;
    const customer = customers.find((item) => item.id === contact.customerId);
    setContacts((prev) => prev.filter((item) => item.id !== contactId));
    pushAudit('Eliminar contacto', customer?.fullName ?? '-', `Contacto: ${contact.name}`);
    closeContactModal();
  };

  const onSelectCategoryCustomer = (customerId: string) => {
    setSelectedCategoryCustomerId(customerId);
    const current = categorizationByCustomer[customerId];
    setCategoryValue(current?.category ?? 'Regular');
    setCategoryTags(current?.tags ?? '');
  };

  const saveCategorization = () => {
    if (!selectedCategoryCustomerId) return;
    const customer = customers.find((item) => item.id === selectedCategoryCustomerId);
    if (!customer) return;
    setCategorizationByCustomer((prev) => ({
      ...prev,
      [selectedCategoryCustomerId]: {
        category: categoryValue,
        tags: categoryTags.trim(),
      },
    }));
    pushAudit('Categorización', customer.fullName, `Categoría: ${categoryValue}`);
  };

  if (!canView) {
    return <NoAccessPage title={t('customers.title')} />;
  }

  const tabs = [
    {
      id: 'clientes',
      label: 'Clientes',
      content: (
        <>
          <div className="customers-search-row">
            <Input
              label="Buscar cliente"
              placeholder="Nombre, RUC o email"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
            />
          </div>

          {loading ? (
            <LoadingState />
          ) : filteredCustomers.length === 0 ? (
            <EmptyState message={t('common.noData')} />
          ) : (
            <div className="table-wrapper">
              <table className="customers-responsive-table">
                <thead>
                  <tr>
                    <th>RUC / CI</th>
                    <th>Razón Social</th>
                    <th>Email</th>
                    <th>Teléfono</th>
                    <th>Categoría</th>
                    <th>Estado</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCustomers.map((customer) => (
                    <tr key={customer.id}>
                      <td data-label="RUC / CI">{customer.identificationNumber}</td>
                      <td data-label="Razón Social">{customer.fullName}</td>
                      <td data-label="Email">{customer.email ?? '-'}</td>
                      <td data-label="Teléfono">{customer.phone ?? '-'}</td>
                      <td data-label="Categoría">{categorizationByCustomer[customer.id]?.category ?? 'Regular'}</td>
                      <td data-label="Estado">
                        <Badge variant={customer.isActive ? 'success' : 'danger'}>
                          {customer.isActive ? 'Activo' : 'Inactivo'}
                        </Badge>
                      </td>
                      <td data-label="Acciones">
                        <div className="customers-actions-cell">
                          <Button variant="secondary" size="sm" onClick={() => openEditModal(customer)} disabled={!canEdit}>
                            Editar
                          </Button>
                          <Button
                            variant={customer.isActive ? 'danger' : 'success'}
                            size="sm"
                            onClick={() => void handleToggleStatus(customer)}
                            disabled={!canEdit || creating}
                          >
                            {customer.isActive ? 'Inactivar' : 'Activar'}
                          </Button>
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
    },
    {
      id: 'contactos',
      label: 'Contactos',
      content: (
        <>
          <div className="customers-pane-head">
            <h3 className="customers-pane-title">Contactos por cliente</h3>
            <Button variant="primary" size="sm" onClick={() => openContactModal()}>
              Nuevo Contacto
            </Button>
          </div>
          {contactRows.length === 0 ? (
            <EmptyState message="No hay contactos registrados todavía." />
          ) : (
            <div className="table-wrapper">
              <table className="customers-responsive-table">
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
                      <td data-label="Cliente">{contact.customerName}</td>
                      <td data-label="Nombre">{contact.name}</td>
                      <td data-label="Cargo">{contact.role || '-'}</td>
                      <td data-label="Email">{contact.email}</td>
                      <td data-label="Teléfono">{contact.phone || '-'}</td>
                      <td data-label="Acciones">
                        <div className="customers-actions-cell">
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => {
                              const current = contacts.find((item) => item.id === contact.id);
                              if (current) openContactModal(current);
                            }}
                          >
                            Editar
                          </Button>
                          <Button variant="danger" size="sm" onClick={() => deleteContact(contact.id)}>
                            Eliminar
                          </Button>
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
    },
    {
      id: 'categorias',
      label: 'Categorización',
      content: (
        <>
          <div className="customers-category-grid">
            <div className="form-group">
              <label>Seleccionar cliente</label>
              <select
                value={selectedCategoryCustomerId}
                onChange={(event) => onSelectCategoryCustomer(event.target.value)}
              >
                <option value="">Seleccionar cliente</option>
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.fullName}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Categoría</label>
              <select
                value={categoryValue}
                onChange={(event) => setCategoryValue(event.target.value as Categorization['category'])}
              >
                <option value="Premium">Premium</option>
                <option value="Regular">Regular</option>
                <option value="Ocasional">Ocasional</option>
              </select>
            </div>
          </div>
          <div className="form-group">
            <label>Etiquetas (separadas por coma)</label>
            <input
              type="text"
              value={categoryTags}
              onChange={(event) => setCategoryTags(event.target.value)}
              placeholder="Ej: VIP, Descuento, Mayorista"
            />
          </div>
          <Button variant="primary" onClick={saveCategorization} disabled={!selectedCategoryCustomerId}>
            Guardar Categorización
          </Button>

          <div className="table-wrapper customers-table-space">
            <table className="customers-responsive-table">
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
                    <td data-label="Cliente">{row.customerName}</td>
                    <td data-label="Categoría">{row.category}</td>
                    <td data-label="Etiquetas">{row.tags || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ),
    },
    {
      id: 'auditoria',
      label: 'Auditoría',
      content: (
        <>
          {auditItems.length === 0 ? (
            <EmptyState message="No hay eventos de auditoría en esta sesión." />
          ) : (
            <div className="table-wrapper">
              <table className="customers-responsive-table">
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
                      <td data-label="Fecha / Hora">{new Date(item.at).toLocaleString()}</td>
                      <td data-label="Usuario">{item.user}</td>
                      <td data-label="Acción">{item.action}</td>
                      <td data-label="Cliente">{item.customerName}</td>
                      <td data-label="Detalles">{item.details}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      ),
    },
  ] satisfies { id: TabId; label: string; content: ReactNode }[];

  return (
    <PageShell kicker={t('app.nav.group.sales')} title={t('customers.title')}>
      <div className="customers-shell">
        <Card
          title={
            <span className="customers-title">
              <span>👥</span>
              <span>Gestión de Clientes</span>
            </span>
          }
          actions={
            canCreate ? (
              <Button variant="primary" size="sm" disabled={creating} onClick={openCreateModal}>
                Nuevo Cliente
              </Button>
            ) : null
          }
        >
          {error ? <Alert type="error" message={error} /> : null}
          {createError ? <Alert type="error" message={createError} /> : null}

          <Tabs tabs={tabs} defaultActiveId="clientes" />
        </Card>
      </div>

      <Modal
        isOpen={customerModalOpen}
        onClose={closeCustomerModal}
        title={editingCustomerId ? 'Editar Cliente' : 'Nuevo Cliente'}
      >
        <form onSubmit={onSubmit}>
          <Input
            label="RUC / Cédula"
            required
            placeholder="10 o 13 dígitos"
            error={errors.identification?.message ? t(String(errors.identification.message)) : undefined}
            disabled={creating}
            {...register('identification')}
          />
          <Input
            label="Razón social / Nombre"
            required
            placeholder="Nombre completo"
            error={errors.fullName?.message ? t(String(errors.fullName.message)) : undefined}
            disabled={creating}
            {...register('fullName')}
          />
          <Input
            label="Email"
            required
            placeholder="cliente@ejemplo.com"
            error={errors.email?.message ? t(String(errors.email.message)) : undefined}
            disabled={creating}
            {...register('email')}
          />
          <Input
            label="Teléfono"
            placeholder="0999123456"
            disabled={creating}
            {...register('phone')}
          />
          <div className="form-group">
            <label>Dirección</label>
            <textarea rows={2} disabled={creating} {...register('address')} />
          </div>
          <div className="form-group">
            <label>Estado</label>
            <select value={customerStatus} onChange={(event) => setCustomerStatus(event.target.value as 'activo' | 'inactivo')}>
              <option value="activo">Activo</option>
              <option value="inactivo">Inactivo</option>
            </select>
          </div>

          <div className="customers-form-actions">
            <Button variant="secondary" type="button" onClick={closeCustomerModal}>
              Cancelar
            </Button>
            <Button variant="primary" type="submit" disabled={creating || (!canCreate && !editingCustomerId)}>
              {creating ? t('common.saving') : 'Guardar'}
            </Button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={contactModalOpen} onClose={closeContactModal} title={editingContactId ? 'Editar Contacto' : 'Nuevo Contacto'}>
        <div className="form-group">
          <label className="label-required">Cliente</label>
          <select
            value={contactForm.customerId}
            onChange={(event) => setContactForm((prev) => ({ ...prev, customerId: event.target.value }))}
          >
            <option value="">Seleccionar cliente</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.fullName}
              </option>
            ))}
          </select>
        </div>
        <Input
          label="Nombre"
          required
          value={contactForm.name}
          onChange={(event) => setContactForm((prev) => ({ ...prev, name: event.target.value }))}
        />
        <Input
          label="Cargo"
          value={contactForm.role}
          onChange={(event) => setContactForm((prev) => ({ ...prev, role: event.target.value }))}
        />
        <Input
          label="Email"
          required
          value={contactForm.email}
          onChange={(event) => setContactForm((prev) => ({ ...prev, email: event.target.value }))}
        />
        <Input
          label="Teléfono"
          value={contactForm.phone}
          onChange={(event) => setContactForm((prev) => ({ ...prev, phone: event.target.value }))}
        />

        <div className="customers-form-actions">
          {editingContactId ? (
            <Button variant="danger" type="button" onClick={() => deleteContact(editingContactId)}>
              Eliminar
            </Button>
          ) : null}
          <Button variant="secondary" type="button" onClick={closeContactModal}>
            Cancelar
          </Button>
          <Button variant="primary" type="button" onClick={saveContact}>
            Guardar
          </Button>
        </div>
      </Modal>
    </PageShell>
  );
}

function buildId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}
