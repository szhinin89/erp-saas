import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { Customer } from '../api/customerService';
import { categoryBadgeClass, type CustomerCategory } from './customersPageUtils';

type CategorizationRow = {
  customerId: string;
  customerName: string;
  category: CustomerCategory;
  tags: string;
};

type CustomersPageCategoriesPanelProps = {
  t: (key: string) => string;
  customers: Customer[];
  categorizationRows: CategorizationRow[];
  selectedCategoryCustomerId: string;
  categoryValue: CustomerCategory;
  setCategoryValue: (value: CustomerCategory) => void;
  categoryTags: string;
  setCategoryTags: (value: string) => void;
  onSelectCustomer: (customerId: string) => void;
  onSave: () => void;
};

export function CustomersPageCategoriesPanel({
  t,
  customers,
  categorizationRows,
  selectedCategoryCustomerId,
  categoryValue,
  setCategoryValue,
  categoryTags,
  setCategoryTags,
  onSelectCustomer,
  onSave,
}: CustomersPageCategoriesPanelProps) {
  return (
    <div className="pg-section-body">
      <div className="pg-form-grid pg-form-grid--2">
        <ZHField label={t('customers.categories.select')}>
          <select
            className="zh-input"
            value={selectedCategoryCustomerId}
            onChange={(e) => onSelectCustomer(e.target.value)}
          >
            <option value="">— {t('customers.categories.select')} —</option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.fullName}
              </option>
            ))}
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
      <div className="cls-panel-actions-mb">
        <ZHBtn variant="primary" size="md" type="button" onClick={onSave} disabled={!selectedCategoryCustomerId}>
          {t('customers.categories.save')}
        </ZHBtn>
      </div>
      <div className="pg-overflow-x">
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
  );
}
