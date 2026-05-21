import { EmptyState } from '../../../components/PageShell';
import type { AuditItem } from './customersPageUtils';

type CustomersPageAuditPanelProps = {
  t: (key: string) => string;
  auditItems: AuditItem[];
};

export function CustomersPageAuditPanel({ t, auditItems }: CustomersPageAuditPanelProps) {
  if (auditItems.length === 0) {
    return <EmptyState message={t('customers.audit.empty')} />;
  }

  return (
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
  );
}
