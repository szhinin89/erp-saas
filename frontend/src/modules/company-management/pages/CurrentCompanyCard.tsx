import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '../../../i18n/i18n';
import { companyManagementService } from '../api/companyManagementService';
import type { CompanyDetail } from '../../../types/companyManagement';
import { ZHCard } from '../../../components/zh/ZHCard';
import { LoadingState } from '../../../components/PageShell';
import { useAuthStore } from '../../../store/authStore';

export function CurrentCompanyCard() {
  const { t } = useI18n();
  const companyId = useAuthStore((s) => s.user?.companyId);
  const [detail, setDetail] = useState<CompanyDetail | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!companyId) {
      setDetail(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const d = await companyManagementService.getCurrent();
        if (!cancelled) setDetail(d);
      } catch {
        if (!cancelled) setDetail(null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [companyId]);

  if (!companyId) return null;

  return (
    <ZHCard title={t('companyManagement.currentTitle')}>
      {loading ? (
        <LoadingState />
      ) : detail ? (
        <div className="company-current-card">
          <p className="company-current-name">{detail.tradeName?.trim() || detail.legalName}</p>
          <p className="mono subtle">{detail.taxId}</p>
          <p className="subtle">{detail.mainAddress}</p>
          <div className="company-current-meta">
            <span className={`badge badge--md ${detail.isActive ? 'badge--green' : 'badge--gray'}`}>
              {detail.isActive ? t('common.active') : t('common.inactive')}
            </span>
            <span className="subtle">{detail.timezone} · {detail.currencyCode}</span>
          </div>
          <Link to={`/saas/companies/${detail.id}/edit`} className="zh-link">
            {t('companyManagement.editCurrent')}
          </Link>
        </div>
      ) : (
        <p className="subtle">{t('companyManagement.currentUnavailable')}</p>
      )}
    </ZHCard>
  );
}
