import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ZHCardSection } from '../../../components/zh/ZHLayout';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { RuntimeModeBadge } from '../../../components/RuntimeModeBadge';
import { saasBillingService, type SaasBillingInvoiceDto, type SubscriberBillingAccountDto } from '../billing/api/saasBillingService';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useI18n } from '../../../i18n/i18n';

export function SaasBillingPage() {
  const { t } = useI18n();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [account, setAccount] = useState<SubscriberBillingAccountDto | null>(null);
  const [invoices, setInvoices] = useState<SaasBillingInvoiceDto[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [acc, inv] = await Promise.all([
        saasBillingService.getAccount(),
        saasBillingService.listInvoices(10),
      ]);
      setAccount(acc);
      setInvoices(inv);
    } catch (e) {
      setError(formatApiRequestError(e, {
        offline: t('common.apiUnreachable'),
        generic: t('common.errorGeneric'),
      }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { void load(); }, [load]);

  return (
    <ErpPageTemplate
      kicker={t('saas.billing.kicker')}
      title={t('saas.billing.title')}
      subtitle={t('saas.billing.subtitle')}
      action={
        <div className="pg-flex-row-8">
          <RuntimeModeBadge />
          <button className="zh-btn zh-btn--secondary" type="button" onClick={() => navigate('/saas/overview')}>
            {t('saas.billing.backOverview')}
          </button>
        </div>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      {loading ? (
        <LoadingState />
      ) : (
        <>
          <div className="card card--xl">
            <ZHCardSection title={t('saas.billing.accountTitle')}>
              {!account ? (
                <EmptyState message={t('saas.billing.accountEmpty')} />
              ) : (
                <table className="table">
                  <tbody>
                    <tr><th>{t('saas.billing.field.status')}</th><td>{account.status}</td></tr>
                    <tr><th>{t('saas.billing.field.renewal')}</th><td>{account.renewalState}</td></tr>
                    <tr><th>{t('saas.billing.field.email')}</th><td>{account.billingEmail}</td></tr>
                    <tr><th>{t('saas.billing.field.currency')}</th><td>{account.currencyCode}</td></tr>
                  </tbody>
                </table>
              )}
            </ZHCardSection>
          </div>

          <div className="card card--xl">
            <ZHCardSection title={t('saas.billing.invoicesTitle')}>
              {invoices.length === 0 ? (
                <EmptyState message={t('saas.billing.invoicesEmpty')} />
              ) : (
                <table className="table">
                  <thead>
                    <tr>
                      <th>{t('saas.billing.col.number')}</th>
                      <th>{t('saas.billing.col.status')}</th>
                      <th>{t('saas.billing.col.amount')}</th>
                      <th>{t('saas.billing.col.date')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invoices.map((inv) => (
                      <tr key={inv.id}>
                        <td>{inv.invoiceNumber}</td>
                        <td>{inv.status}</td>
                        <td>{inv.totalAmount} {inv.currencyCode}</td>
                        <td>{new Date(inv.issuedAtUtc).toLocaleDateString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </ZHCardSection>
          </div>
        </>
      )}
    </ErpPageTemplate>
  );
}
