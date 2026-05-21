import { EmptyState } from '../../../components/PageShell';
import type { ContactItem } from './customersPageUtils';

type ContactRow = ContactItem & { customerName: string };

type CustomersPageContactsPanelProps = {
  t: (key: string) => string;
  contactRows: ContactRow[];
  contacts: ContactItem[];
  onEditContact: (contact: ContactItem) => void;
  onDeleteContact: (contactId: string) => void;
};

export function CustomersPageContactsPanel({
  t,
  contactRows,
  contacts,
  onEditContact,
  onDeleteContact,
}: CustomersPageContactsPanelProps) {
  if (contactRows.length === 0) {
    return <EmptyState message={t('customers.contacts.empty')} />;
  }

  return (
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
                    onClick={() => {
                      const c = contacts.find((x) => x.id === contact.id);
                      if (c) onEditContact(c);
                    }}
                    aria-label="Edit contact"
                  >
                    <span className="material-symbols-outlined">edit</span>
                  </button>
                  <button
                    type="button"
                    className="zh-btn zh-btn--ghost zh-btn--sm cls-btn-danger"
                    onClick={() => onDeleteContact(contact.id)}
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
  );
}
