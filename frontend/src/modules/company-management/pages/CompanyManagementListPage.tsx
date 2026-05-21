import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '../../../i18n/i18n';
import { companyManagementService } from '../api/companyManagementService';
import type { CompanyListItem } from '../../../types/companyManagement';
import { PageShell, LoadingState, EmptyState, Badge } from '../../../components/PageShell';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHCard } from '../../../components/zh/ZHCard';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { CurrentCompanyCard } from './CurrentCompanyCard';

export function CompanyManagementListPage() {
  const { t } = useI18n();
  const isAdmin = (useAuthStore((s) => s.user?.role) ?? '') === 'Admin';
  const has = usePermissionsStore((s) => s.has);
  const canCreate = isAdmin || has('saas.companies.create');
  const [items, setItems] = useState<CompanyListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const refresh = async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await companyManagementService.list(false));
    } catch {
      setError(t('companyManagement.error.load'));
      setItems([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
  }, []);

  return (
    <PageShell
      kicker={t('app.nav.group.settings')}
      title={t('companyManagement.title')}
      subtitle={t('companyManagement.subtitle')}
    >
      <CurrentCompanyCard />
      <ZHCard
        title={t('companyManagement.listTitle')}
        actions={
          <>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void refresh()} disabled={loading}>
              {t('common.refresh')}
            </ZHBtn>
            {canCreate ? (
              <Link to="/saas/companies/new">
                <ZHBtn variant="primary" size="sm" type="button">{t('companyManagement.create')}</ZHBtn>
              </Link>
            ) : null}
          </>
        }
      >
        {error ? <p className="zh-error-text">{error}</p> : null}
        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <table className="companies-table responsive-table companies-responsive-table">
            <thead>
              <tr>
                <th>{t('companyManagement.legalName')}</th>
                <th>{t('companyManagement.taxId')}</th>
                <th>{t('companyManagement.currency')}</th>
                <th>{t('common.status')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {items.map((row) => (
                <tr key={row.id}>
                  <td data-label={t('companyManagement.legalName')}>
                    {row.tradeName?.trim() ? row.tradeName : row.legalName}
                  </td>
                  <td data-label={t('companyManagement.taxId')} className="mono">{row.taxId}</td>
                  <td data-label={t('companyManagement.currency')}>{row.currencyCode}</td>
                  <td data-label={t('common.status')}>
                    <Badge
                      label={row.isActive ? t('common.active') : t('common.inactive')}
                      variant={row.isActive ? 'green' : 'gray'}
                    />
                  </td>
                  <td>
                    <Link to={`/saas/companies/${row.id}/edit`}>{t('common.edit')}</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </ZHCard>
    </PageShell>
  );
}
