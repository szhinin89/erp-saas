import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { CustomersPageAuditPanel } from './CustomersPageAuditPanel';
import { CustomersPageCategoriesPanel } from './CustomersPageCategoriesPanel';
import { CustomersPageContactModal } from './CustomersPageContactModal';
import { CustomersPageContactsPanel } from './CustomersPageContactsPanel';
import { CustomersPageCustomerModal } from './CustomersPageCustomerModal';
import { CustomersPageListPanel } from './CustomersPageListPanel';
import { useCustomersPage } from './useCustomersPage';
import './customers-page.css';

export function CustomersPage() {
  const page = useCustomersPage();

  if (!page.canView) return <NoAccessPage title={page.t('customers.title')} />;

  const {
    t,
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
  } = page;

  return (
    <div className="pg-page">
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

      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
      {createError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} />}

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
          {activeTab === 'clientes' && (
            <CustomersPageListPanel
              t={t}
              searchQuery={searchQuery}
              setSearchQuery={setSearchQuery}
              statusFilter={statusFilter}
              setStatusFilter={setStatusFilter}
              categoryFilter={categoryFilter}
              setCategoryFilter={setCategoryFilter}
              filteredCustomers={filteredCustomers}
              customers={customers}
              loading={loading}
              creating={creating}
              canEdit={canEdit}
              categorizationByCustomer={categorizationByCustomer}
              contactCountByCustomer={contactCountByCustomer}
              onEdit={openEditModal}
              onToggleStatus={handleToggleStatus}
            />
          )}
          {activeTab === 'contactos' && (
            <CustomersPageContactsPanel
              t={t}
              contactRows={contactRows}
              contacts={contacts}
              onEditContact={openContactModal}
              onDeleteContact={deleteContact}
            />
          )}
          {activeTab === 'categorias' && (
            <CustomersPageCategoriesPanel
              t={t}
              customers={customers}
              categorizationRows={categorizationRows}
              selectedCategoryCustomerId={selectedCategoryCustomerId}
              categoryValue={categoryValue}
              setCategoryValue={setCategoryValue}
              categoryTags={categoryTags}
              setCategoryTags={setCategoryTags}
              onSelectCustomer={onSelectCategoryCustomer}
              onSave={saveCategorization}
            />
          )}
          {activeTab === 'auditoria' && <CustomersPageAuditPanel t={t} auditItems={auditItems} />}
        </div>
      </div>

      <CustomersPageCustomerModal
        t={t}
        open={customerModalOpen}
        editingCustomerId={editingCustomerId}
        creating={creating}
        canCreate={canCreate}
        customerStatus={customerStatus}
        setCustomerStatus={setCustomerStatus}
        register={register}
        errors={errors}
        onClose={closeCustomerModal}
        onSubmit={onSubmit}
      />

      <CustomersPageContactModal
        t={t}
        open={contactModalOpen}
        editingContactId={editingContactId}
        contactForm={contactForm}
        setContactForm={setContactForm}
        customers={customers}
        onClose={closeContactModal}
        onSave={saveContact}
        onDelete={deleteContact}
      />
    </div>
  );
}
