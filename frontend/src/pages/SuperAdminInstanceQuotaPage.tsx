import { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import {
  ErrorState,
  LoadingState,
  PageShell,
  TableCard,
} from '../components/PageShell';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { superAdminService, type InstanceQuota } from '../services/superAdminService';
import { formatApiError } from '../modules/lib/formatApiError';
import { invalidateDeploymentConfigCache } from '../deployment/deploymentConfig';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../components/zh/ZHLayout';
import './SuperAdminInstanceQuotaPage.css';

function optionalIntFromInput(raw: string): number | null {
  const t = raw.trim();
  if (t === '') return null;
  const n = Number(t);
  if (!Number.isFinite(n) || !Number.isInteger(n) || n < 0) return null;
  return n;
}

function intToInput(v: number | null | undefined): string {
  if (v == null) return '';
  return String(v);
}

export function SuperAdminInstanceQuotaPage() {
  const { t } = useI18n();
  const { user } = useAuthStore();
  const isSuperAdmin = user?.role === 'SuperAdmin';
  const hasSelectedTenant = !!user?.tenantId && user.tenantId !== '00000000-0000-0000-0000-000000000000';

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  const [dedicated, setDedicated] = useState(false);
  const [maxTenants, setMaxTenants] = useState('');
  const [maxIdentity, setMaxIdentity] = useState('');
  const [maxPerTenant, setMaxPerTenant] = useState('');

  useEffect(() => {
    if (!isSuperAdmin || hasSelectedTenant) return;
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        const q = await superAdminService.getInstanceQuota();
        if (cancelled) return;
        setDedicated(!!q.dedicatedSingleClientInstance);
        setMaxTenants(intToInput(q.maxActiveTenants ?? null));
        setMaxIdentity(intToInput(q.maxIdentityUsers ?? null));
        setMaxPerTenant(intToInput(q.maxUsersPerTenant ?? null));
      } catch (e) {
        if (!cancelled) setError(formatApiError(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isSuperAdmin, hasSelectedTenant]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess(false);

    const nTenants = optionalIntFromInput(maxTenants);
    const nIdentity = optionalIntFromInput(maxIdentity);
    const nPerTenant = optionalIntFromInput(maxPerTenant);

    if (dedicated && (nTenants == null || nTenants <= 0)) {
      setError(t('superadmin.instanceQuota.errorDedicatedTenants'));
      return;
    }

    const body: InstanceQuota = {
      dedicatedSingleClientInstance: dedicated,
      maxActiveTenants: nTenants,
      maxIdentityUsers: nIdentity,
      maxUsersPerTenant: nPerTenant,
    };

    setSaving(true);
    try {
      await superAdminService.saveInstanceQuota(body);
      invalidateDeploymentConfigCache();
      setSuccess(true);
      window.setTimeout(() => window.location.reload(), 600);
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  if (!isSuperAdmin) {
    return (
      <PageShell kicker={t('app.nav.group.home')} title={t('superadmin.instanceQuota.title')}>
        <TableCard>
          <div className="empty-state">{t('superadmin.noAccess')}</div>
        </TableCard>
      </PageShell>
    );
  }

  if (hasSelectedTenant) {
    return (
      <PageShell
        kicker={t('app.nav.group.home')}
        title={t('superadmin.instanceQuota.title')}
        action={
          <NavLink to="/superadmin">{t('superadmin.backToGlobal')}</NavLink>
        }
      >
        <TableCard>
          <div className="empty-state">{t('superadmin.alreadyInTenant')}</div>
        </TableCard>
      </PageShell>
    );
  }

  return (
    <PageShell
      kicker={t('app.nav.group.home')}
      title={t('superadmin.instanceQuota.title')}
      subtitle={t('superadmin.instanceQuota.subtitle')}
      action={
        <NavLink to="/superadmin">← {t('app.nav.superadmin')}</NavLink>
      }
    >
      {error ? <ErrorState message={error} /> : null}
      {success ? (
        <p className="subtle" role="status">
          {t('superadmin.instanceQuota.saved')}
        </p>
      ) : null}

      <TableCard>
        {loading ? (
          <LoadingState />
        ) : (
          <form onSubmit={(ev) => void handleSubmit(ev)}>
            <ZHCardSection title={t('superadmin.instanceQuota.sectionMode')}>
              <p className="subtle">{t('superadmin.instanceQuota.helpDedicated')}</p>
              <ZHGridRow cols={1}>
                <label className="sa-quotaCheck">
                  <input
                    type="checkbox"
                    checked={dedicated}
                    onChange={(e) => setDedicated(e.target.checked)}
                    disabled={saving}
                  />
                  <span>{t('superadmin.instanceQuota.dedicatedLabel')}</span>
                </label>
              </ZHGridRow>
            </ZHCardSection>

            <ZHCardSection title={t('superadmin.instanceQuota.sectionLimits')}>
              <p className="subtle">{t('superadmin.instanceQuota.helpLimits')}</p>
              <ZHGridRow cols={1}>
                <ZHField label={t('superadmin.instanceQuota.maxTenants')}>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    value={maxTenants}
                    onChange={(e) => setMaxTenants(e.target.value)}
                    placeholder={t('superadmin.instanceQuota.placeholderUnlimited')}
                    disabled={saving}
                    required={dedicated}
                  />
                </ZHField>
                <ZHField label={t('superadmin.instanceQuota.maxIdentity')}>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    value={maxIdentity}
                    onChange={(e) => setMaxIdentity(e.target.value)}
                    placeholder={t('superadmin.instanceQuota.placeholderUnlimited')}
                    disabled={saving}
                  />
                </ZHField>
                <ZHField label={t('superadmin.instanceQuota.maxPerTenant')}>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    value={maxPerTenant}
                    onChange={(e) => setMaxPerTenant(e.target.value)}
                    placeholder={t('superadmin.instanceQuota.placeholderUnlimited')}
                    disabled={saving}
                  />
                </ZHField>
              </ZHGridRow>
            </ZHCardSection>

            <ZHInlineRowRight>
              <ZHBtn variant="primary" type="submit" disabled={saving}>
                {saving ? t('common.saving') : t('common.save')}
              </ZHBtn>
            </ZHInlineRowRight>
          </form>
        )}
      </TableCard>
    </PageShell>
  );
}
