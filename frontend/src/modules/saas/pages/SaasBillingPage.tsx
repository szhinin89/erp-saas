import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ZHCardSection } from '../../../components/zh/ZHLayout';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { RuntimeModeBadge } from '../../../components/RuntimeModeBadge';
import { saasBillingService, type SaasBillingInvoiceDto, type SubscriberBillingAccountDto } from '../billing/api/saasBillingService';
import { isReadOnlyBillingStatus, isBlockedBillingStatus } from '../billing/constants/subscriptionStatus';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useI18n } from '../../../i18n/i18n';
import { formatDate } from '../../../lib/formatters/dateFormatters';

// ── ReadOnly / GracePeriod banner ─────────────────────────────────────────────

function AccountStatusBanner({ account }: { account: SubscriberBillingAccountDto }) {
  if (isBlockedBillingStatus(account.status)) {
    return (
      <ZHPageNotice
        variant="error"
        message="Cuenta suspendida o cancelada"
        detail="Tu suscripción está bloqueada. Contacta al soporte para reactivarla."
      />
    );
  }
  if (isReadOnlyBillingStatus(account.status)) {
    return (
      <ZHPageNotice
        variant="warning"
        message="Modo solo lectura activo"
        detail={
          isReadOnlyBillingStatus(account.status) && account.status === 'GracePeriod'
            ? 'Tu cuenta está en período de gracia. Las operaciones de escritura están deshabilitadas hasta regularizar el pago.'
            : 'Hay un pago pendiente en tu cuenta. Las operaciones de escritura están deshabilitadas.'
        }
      />
    );
  }
  return null;
}

function StatusBadge({ status }: { status: string }) {
  const variants: Record<string, string> = {
    Active:      'zh-status--active',
    Trialing:    'zh-status--info',
    GracePeriod: 'zh-status--warning',
    PastDue:     'zh-status--warning',
    Suspended:   'zh-status--inactive',
    Cancelled:   'zh-status--inactive',
  };
  return (
    <span className={`zh-status ${variants[status] ?? 'zh-status--inactive'}`}>
      {status}
    </span>
  );
}

function InvoiceStatusBadge({ status }: { status: string }) {
  const variants: Record<string, string> = {
    Draft:         'badge badge--gray',
    Open:          'badge badge--yellow',
    Paid:          'badge badge--green',
    Void:          'badge badge--gray',
    Uncollectible: 'badge badge--red',
  };
  return <span className={variants[status] ?? 'badge badge--gray'}>{status}</span>;
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function SaasBillingPage() {
  const { t } = useI18n();
  const navigate = useNavigate();

  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState('');
  const [account,  setAccount]  = useState<SubscriberBillingAccountDto | null>(null);
  const [invoices, setInvoices] = useState<SaasBillingInvoiceDto[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [acc, inv] = await Promise.all([
        saasBillingService.getAccount(),
        saasBillingService.listInvoices(20),
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

  const isReadOnly = account ? isReadOnlyBillingStatus(account.status) : false;
  const isBlocked  = account ? isBlockedBillingStatus(account.status) : false;

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

      {/* ReadOnly / Blocked banners */}
      {account && <AccountStatusBanner account={account} />}

      {/* ReadOnly mode notice for operations */}
      {isReadOnly && !isBlocked && (
        <ZHPageNotice
          variant="info"
          message="Acceso de solo lectura"
          detail="Puedes consultar tu información de facturación. Para crear o modificar registros, regulariza el pago de tu suscripción."
        />
      )}

      {loading ? (
        <LoadingState />
      ) : (
        <>
          {/* Account info */}
          <div className="card card--xl">
            <ZHCardSection title={t('saas.billing.accountTitle')}>
              {!account ? (
                <EmptyState message={t('saas.billing.accountEmpty')} />
              ) : (
                <table className="table">
                  <tbody>
                    <tr>
                      <th>{t('saas.billing.field.status')}</th>
                      <td><StatusBadge status={account.status} /></td>
                    </tr>
                    <tr>
                      <th>{t('saas.billing.field.renewal')}</th>
                      <td>{account.renewalState}</td>
                    </tr>
                    <tr>
                      <th>Trial</th>
                      <td>{account.trialState !== 'None' ? account.trialState : '—'}</td>
                    </tr>
                    <tr>
                      <th>{t('saas.billing.field.email')}</th>
                      <td>{account.billingEmail}</td>
                    </tr>
                    <tr>
                      <th>{t('saas.billing.field.currency')}</th>
                      <td>{account.currencyCode}</td>
                    </tr>
                  </tbody>
                </table>
              )}
            </ZHCardSection>
          </div>

          {/* Invoice history */}
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
                      <th className="pg-th-right">{t('saas.billing.col.amount')}</th>
                      <th>{t('saas.billing.col.date')}</th>
                      <th>Vencimiento</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invoices.map((inv) => (
                      <tr key={inv.id}>
                        <td className="mono subtle">{inv.invoiceNumber}</td>
                        <td><InvoiceStatusBadge status={inv.status} /></td>
                        <td className="pg-td-right mono">
                          {inv.totalAmount?.toFixed(2)} {inv.currencyCode}
                        </td>
                        <td>{formatDate(inv.issuedAtUtc)}</td>
                        <td className={inv.status === 'Open' ? 'pg-text-warning' : ''}>
                          {inv.dueAtUtc ? formatDate(inv.dueAtUtc) : '—'}
                        </td>
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
