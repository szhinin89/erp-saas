import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ZHCardSection, ZHGridRow } from '../../../components/zh/ZHLayout';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { StatCard } from '../../../components/zh/StatCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { RuntimeModeBadge } from '../../../components/RuntimeModeBadge';
import { entitlementsService } from '../../../modules/auth/api/entitlementsService';
import { companyManagementService } from '../../../modules/company-management/api/companyManagementService';
import { saasBillingService } from '../billing/api/saasBillingService';
import type { EntitlementsSnapshot } from '../../../types/entitlements';
import type { CompanyListItem } from '../../../types/companyManagement';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';

const LIMIT_LABELS: Record<string, string> = {
  MAX_COMPANIES: 'saas.overview.limits.maxCompanies',
  MAX_USERS: 'saas.overview.limits.maxUsers',
};

export function SaasOverviewPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [entitlements, setEntitlements] = useState<EntitlementsSnapshot | null>(null);
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [billingStatus, setBillingStatus] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [entResult, companiesResult, billingResult] = await Promise.allSettled([
        entitlementsService.getMe(),
        companyManagementService.list(true),
        saasBillingService.getAccount(),
      ]);

      if (entResult.status === 'fulfilled') {
        setEntitlements(entResult.value);
      } else {
        throw entResult.reason;
      }

      if (companiesResult.status === 'fulfilled') {
        setCompanies(companiesResult.value);
      } else {
        setCompanies([]);
      }

      if (billingResult.status === 'fulfilled') {
        setBillingStatus(billingResult.value?.status ?? null);
      } else {
        setBillingStatus(null);
      }
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

  const limitRows = useMemo(() => {
    if (!entitlements?.limits) return [];
    return Object.entries(entitlements.limits).map(([code, value]) => ({
      code,
      label: LIMIT_LABELS[code] ? t(LIMIT_LABELS[code]) : code,
      value: value == null || value <= 0 ? t('saas.overview.limits.unlimited') : String(value),
      usage:
        code === 'MAX_COMPANIES'
          ? `${companies.length}${value != null && value > 0 ? ` / ${value}` : ''}`
          : '—',
    }));
  }, [companies.length, entitlements?.limits, t]);

  return (
    <ErpPageTemplate
      kicker={t('saas.overview.kicker')}
      title={t('saas.overview.title')}
      subtitle={t('saas.overview.subtitle')}
      action={
        <div className="pg-flex-row-8-wrap">
          <RuntimeModeBadge />
          <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void load()}>
            <span className="material-symbols-outlined">refresh</span>
            {t('common.refresh')}
          </button>
          <button className="zh-btn zh-btn--primary" type="button" onClick={() => navigate('/saas/companies')}>
            <span className="material-symbols-outlined">domain</span>
            {t('saas.overview.manageCompanies')}
          </button>
        </div>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      {/* ReadOnly / GracePeriod banners — shown on overview page too */}
      {billingStatus === 'GracePeriod' && (
        <ZHPageNotice
          variant="warning"
          message="Período de gracia activo"
          detail="Tu suscripción está en período de gracia. Las operaciones de escritura están deshabilitadas. Regulariza el pago para restaurar el acceso completo."
        />
      )}
      {billingStatus === 'PastDue' && (
        <ZHPageNotice
          variant="warning"
          message="Pago pendiente"
          detail="Hay un pago pendiente en tu cuenta. Contacta al soporte o regulariza el pago para restaurar el acceso completo."
        />
      )}
      {billingStatus === 'Suspended' && (
        <ZHPageNotice
          variant="error"
          message="Cuenta suspendida"
          detail="Tu cuenta ha sido suspendida. Contacta al soporte para reactivarla."
        />
      )}

      {loading ? (
        <LoadingState />
      ) : (
        <>
          <div className="pg-kpis">
            <StatCard
              label={t('saas.overview.kpis.plan')}
              value={entitlements?.planName ?? entitlements?.planCode ?? '—'}
              icon="loyalty"
              tone="primary"
              hint={entitlements?.planCode ?? undefined}
            />
            <StatCard
              label={t('saas.overview.kpis.companies')}
              value={String(companies.length)}
              icon="apartment"
              tone="secondary"
              hint={t('saas.overview.kpis.companiesHint')}
            />
            <StatCard
              label={t('saas.overview.kpis.billing')}
              value={billingStatus ?? t('saas.overview.billingUnknown')}
              icon="payments"
              tone="tertiary"
            />
            <StatCard
              label={t('saas.overview.kpis.modules')}
              value={String(entitlements?.enabledModules?.length ?? 0)}
              icon="widgets"
              tone="warning"
              hint={
                entitlements?.hasModuleRestrictions
                  ? t('saas.overview.modulesRestricted')
                  : t('saas.overview.modulesFull')
              }
            />
          </div>

          <ZHGridRow cols={2}>
            <div className="card card--xl">
              <ZHCardSection
                title={t('saas.overview.limitsTitle')}
                right={
                  <button className="zh-btn zh-btn--ghost zh-btn--sm" type="button" onClick={() => navigate('/saas/billing')}>
                    {t('saas.overview.viewBilling')}
                  </button>
                }
              >
                {limitRows.length === 0 ? (
                  <EmptyState message={t('saas.overview.limitsEmpty')} />
                ) : (
                  <table className="table">
                    <thead>
                      <tr>
                        <th>{t('saas.overview.limits.col.limit')}</th>
                        <th>{t('saas.overview.limits.col.max')}</th>
                        <th>{t('saas.overview.limits.col.usage')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {limitRows.map((row) => (
                        <tr key={row.code}>
                          <td>{row.label}</td>
                          <td>{row.value}</td>
                          <td>{row.usage}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </ZHCardSection>
            </div>

            <div className="card card--xl">
              <ZHCardSection
                title={t('saas.overview.companiesTitle')}
                right={
                  user?.companyId ? (
                    <button className="zh-btn zh-btn--ghost zh-btn--sm" type="button" onClick={() => navigate('/dashboard')}>
                      {t('saas.overview.openErpDashboard')}
                    </button>
                  ) : null
                }
              >
                {companies.length === 0 ? (
                  <EmptyState message={t('saas.overview.companiesEmpty')} />
                ) : (
                  <table className="table">
                    <thead>
                      <tr>
                        <th>{t('saas.overview.companies.col.name')}</th>
                        <th>{t('saas.overview.companies.col.taxId')}</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {companies.slice(0, 6).map((c) => (
                        <tr key={c.id}>
                          <td>{c.legalName}</td>
                          <td className="mono">{c.taxId}</td>
                          <td className="pg-table-actions">
                            <button
                              className="zh-btn zh-btn--secondary zh-btn--sm"
                              type="button"
                              onClick={() => navigate('/select-company')}
                            >
                              {t('saas.overview.selectCompany')}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </ZHCardSection>
            </div>
          </ZHGridRow>
        </>
      )}
    </ErpPageTemplate>
  );
}
