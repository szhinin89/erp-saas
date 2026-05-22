import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import type { Customer } from '../api/customerService';
import { useCustomers } from '../hooks/useCustomers';
import { customerSchema, defaultCustomerValues, type CustomerFormValues } from '../schemas/customerSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import {
  buildId,
  type AuditItem,
  type Categorization,
  type ContactItem,
  type CustomerCategory,
  type CustomerTabId,
} from './customersPageUtils';

export function useCustomersPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const companyId = useAuthStore((s) => s.user?.companyId);
  const canView = canShow('sales.customers.view');
  const canCreate = canShow('sales.customers.create');
  const canEdit = canShow('sales.customers.update') || canCreate;
  const canLoadCustomers = canView && !!companyId;

  const [activeTab, setActiveTab] = useState<CustomerTabId>('clientes');
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [categoryFilter, setCategoryFilter] = useState<'all' | CustomerCategory>('all');

  const [customerModalOpen, setCustomerModalOpen] = useState(false);
  const [editingCustomerId, setEditingCustomerId] = useState<string | null>(null);
  const [customerStatus, setCustomerStatus] = useState<'active' | 'inactive'>('active');

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
  const [categoryValue, setCategoryValue] = useState<CustomerCategory>('Minorista');
  const [categoryTags, setCategoryTags] = useState('');

  const [auditItems, setAuditItems] = useState<AuditItem[]>([]);

  const { customers, loading, error, creating, createError, createCustomer, updateCustomer, toggleCustomerStatus } =
    useCustomers(canLoadCustomers);

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
    let list = customers;
    if (statusFilter === 'active') list = list.filter((c) => c.isActive);
    if (statusFilter === 'inactive') list = list.filter((c) => !c.isActive);
    if (categoryFilter !== 'all') {
      list = list.filter((c) => categorizationByCustomer[c.id]?.category === categoryFilter);
    }
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (c) =>
        c.fullName.toLowerCase().includes(term) ||
        c.identificationNumber.toLowerCase().includes(term) ||
        (c.email ?? '').toLowerCase().includes(term),
    );
  }, [customers, searchQuery, statusFilter, categoryFilter, categorizationByCustomer]);

  const contactRows = useMemo(
    () =>
      contacts.map((item) => ({
        ...item,
        customerName: customers.find((c) => c.id === item.customerId)?.fullName ?? '-',
      })),
    [contacts, customers],
  );

  const categorizationRows = useMemo(
    () =>
      customers.map((c) => ({
        customerId: c.id,
        customerName: c.fullName,
        category: (categorizationByCustomer[c.id]?.category ?? 'Minorista') as CustomerCategory,
        tags: categorizationByCustomer[c.id]?.tags ?? '',
      })),
    [categorizationByCustomer, customers],
  );

  const totals = useMemo(
    () => ({
      total: customers.length,
      active: customers.filter((c) => c.isActive).length,
      inactive: customers.filter((c) => !c.isActive).length,
      noEmail: customers.filter((c) => !c.email).length,
    }),
    [customers],
  );

  const contactCountByCustomer = useMemo(() => {
    const counts: Record<string, number> = {};
    contacts.forEach((c) => {
      counts[c.customerId] = (counts[c.customerId] ?? 0) + 1;
    });
    return counts;
  }, [contacts]);

  const pushAudit = (action: string, customerName: string, details: string) =>
    setAuditItems((prev) =>
      [
        {
          id: buildId(),
          at: new Date().toISOString(),
          user: 'admin@zhtechnologies.com',
          action,
          customerName,
          details,
        },
        ...prev,
      ].slice(0, 50),
    );

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
      `Status → ${updated.isActive ? 'active' : 'inactive'}`,
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
      setContactForm({ customerId: '', name: '', role: '', email: '', phone: '' });
    }
    setContactModalOpen(true);
  };

  const closeContactModal = () => {
    setContactModalOpen(false);
    setEditingContactId(null);
  };

  const saveContact = () => {
    const customer = customers.find((c) => c.id === contactForm.customerId);
    if (!customer || !contactForm.name.trim() || !contactForm.email.trim()) return;
    if (editingContactId) {
      setContacts((prev) => prev.map((c) => (c.id === editingContactId ? { ...c, ...contactForm } : c)));
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

  const TABS: { id: CustomerTabId; label: string; icon: string }[] = [
    { id: 'clientes', label: t('customers.tabs.list'), icon: 'person' },
    { id: 'categorias', label: t('customers.tabs.categories'), icon: 'category' },
    { id: 'contactos', label: t('customers.tabs.contacts'), icon: 'contacts' },
    { id: 'auditoria', label: t('customers.tabs.audit'), icon: 'history' },
  ];

  return {
    t,
    canView,
    canCreate,
    canEdit,
    activeTab,
    setActiveTab,
    TABS,
    searchQuery,
    setSearchQuery,
    statusFilter,
    setStatusFilter,
    categoryFilter,
    setCategoryFilter,
    filteredCustomers,
    customers,
    loading,
    error,
    createError,
    creating,
    categorizationByCustomer,
    contactCountByCustomer,
    contactRows,
    contacts,
    categorizationRows,
    selectedCategoryCustomerId,
    categoryValue,
    setCategoryValue,
    categoryTags,
    setCategoryTags,
    auditItems,
    totals,
    customerModalOpen,
    editingCustomerId,
    customerStatus,
    setCustomerStatus,
    contactModalOpen,
    editingContactId,
    contactForm,
    setContactForm,
    register,
    errors,
    openCreateModal,
    openEditModal,
    closeCustomerModal,
    onSubmit,
    handleToggleStatus,
    openContactModal,
    closeContactModal,
    saveContact,
    deleteContact,
    onSelectCategoryCustomer,
    saveCategorization,
  };
}
