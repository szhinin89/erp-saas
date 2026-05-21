import { EmptyState, LoadingState } from '../../../components/PageShell';
import type { Customer } from '../api/customerService';
import { categoryBadgeClass, getInitials, type CustomerCategory, type Categorization } from './customersPageUtils';

type CustomersPageListPanelProps = {
  t: (key: string) => string;
  searchQuery: string;
  setSearchQuery: (value: string) => void;
  statusFilter: 'all' | 'active' | 'inactive';
  setStatusFilter: (value: 'all' | 'active' | 'inactive') => void;
  categoryFilter: 'all' | CustomerCategory;
  setCategoryFilter: (value: 'all' | CustomerCategory) => void;
  filteredCustomers: Customer[];
  customers: Customer[];
  loading: boolean;
  creating: boolean;
  canEdit: boolean;
  categorizationByCustomer: Record<string, Categorization>;
  contactCountByCustomer: Record<string, number>;
  onEdit: (customer: Customer) => void;
  onToggleStatus: (customer: Customer) => void;
};

export function CustomersPageListPanel({
  t,
  searchQuery,
  setSearchQuery,
  statusFilter,
  setStatusFilter,
  categoryFilter,
  setCategoryFilter,
  filteredCustomers,
  customers,
  loading,
  creating,
  canEdit,
  categorizationByCustomer,
  contactCountByCustomer,
  onEdit,
  onToggleStatus,
}: CustomersPageListPanelProps) {
  return (
    <>
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

      {loading ? (
        <LoadingState />
      ) : filteredCustomers.length === 0 ? (
        <EmptyState message={t('common.noData')} />
      ) : (
        <div className="pg-overflow-x">
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
                    <td className="subtle mono cls-cell-nowrap">
                      #{customer.identificationNumber.slice(0, 8)}
                    </td>
                    <td>
                      <div className="cls-customer-row">
                        <div className="zh-avatar zh-avatar--square">{getInitials(customer.fullName)}</div>
                        <div>
                          <div className="cls-customer-name">{customer.fullName}</div>
                          <div className="subtle cls-customer-id-line">
                            {customer.identificationType}: {customer.identificationNumber}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <div className="cls-contact-stack">
                        {customer.email ? (
                          <span className="cls-contact-email">{customer.email}</span>
                        ) : (
                          <span className="subtle">—</span>
                        )}
                        {customer.phone && <span className="subtle">{customer.phone}</span>}
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
                      <div className="cls-contacts-count">
                        <span className="material-symbols-outlined pg-icon-18">
                          groups
                        </span>
                        {contactCountByCustomer[customer.id] ?? 0}
                      </div>
                    </td>
                    <td>
                      <div className="cls-actions-cell">
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => onEdit(customer)}
                          disabled={!canEdit}
                          aria-label="Edit"
                        >
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button
                          type="button"
                          className={`zh-btn zh-btn--ghost zh-btn--sm ${customer.isActive ? 'cls-btn-danger' : 'cls-btn-success'}`}
                          onClick={() => void onToggleStatus(customer)}
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
  );
}
