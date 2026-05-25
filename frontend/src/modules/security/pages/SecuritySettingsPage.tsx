import { useEffect, useMemo, useState } from 'react';
import { EmptyState, LoadingState, PageShell, NoAccessPage } from '../../../components/PageShell';
import { ZHCard } from '../../../components/zh/ZHCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useAuthStore } from '../../../store/authStore';
import { securityService, type SecurityAdminMatrix, type SecurityUser } from '../api/securityService';
import { isJwtPlatformOperatorRole } from '../../../constants/platformAuth';
import { formatApiError } from '../../lib/formatApiError';
import { useI18n } from '../../../i18n/i18n';
import './SecuritySettingsPage.css';

type ScopeKey = 'manageRoles' | 'manageModules' | 'manageScreens' | 'manageProcesses';

const scopeMap: Record<ScopeKey, number> = {
  manageRoles: 1,
  manageModules: 2,
  manageScreens: 3,
  manageProcesses: 4,
};

const scopeColumns: Array<{ key: ScopeKey; labelKey: string }> = [
  { key: 'manageRoles', labelKey: 'security.scopes.manageRoles' },
  { key: 'manageModules', labelKey: 'security.scopes.manageModules' },
  { key: 'manageScreens', labelKey: 'security.scopes.manageScreens' },
  { key: 'manageProcesses', labelKey: 'security.scopes.manageProcesses' },
];

function userAllowedScopes(matrix: SecurityAdminMatrix, user: SecurityUser): Set<number> {
  const allowed = new Set<number>();
  for (const a of matrix.assignments) {
    if (a.subjectType !== 'User') continue;
    if (a.subjectKey !== user.id) continue;
    if (a.isAllowed) allowed.add(a.scope);
  }
  return allowed;
}

export function SecuritySettingsPage() {
  const { user } = useAuthStore();
  const { t } = useI18n();

  const [matrix, setMatrix] = useState<SecurityAdminMatrix | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [error, setError] = useState('');

  const isPlatformOperator = isJwtPlatformOperatorRole(user?.role);

  useEffect(() => {
    if (!isPlatformOperator) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        const data = await securityService.getAdminMatrix();
        if (cancelled) return;
        setMatrix(data);
      } catch (e) {
        if (cancelled) return;
        setError(formatApiError(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isPlatformOperator]);

  const rows = useMemo(() => {
    if (!matrix) return [];
    return matrix.users
      .slice()
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
  }, [matrix]);

  const scopeStateByUserId = useMemo(() => {
    const map = new Map<string, Set<number>>();
    if (!matrix) return map;
    for (const u of matrix.users) map.set(u.id, userAllowedScopes(matrix, u));
    return map;
  }, [matrix]);

  const toggleScope = async (targetUserId: string, scope: number) => {
    if (!matrix) return;
    const current = new Set(scopeStateByUserId.get(targetUserId) ?? []);
    if (current.has(scope)) current.delete(scope);
    else current.add(scope);

    setSavingKey(targetUserId);
    setError('');
    try {
      await securityService.upsertAdminScopes({
        subjectType: 'User',
        subjectKey: targetUserId,
        allowedScopes: [...current.values()],
      });

      // Refresh local matrix assignments for this user (optimistic merge).
      const nextAssignments = matrix.assignments
        .filter((a) => !(a.subjectType === 'User' && a.subjectKey === targetUserId))
        .concat(
          [...current.values()].map((s) => ({
            subjectType: 'User' as const,
            subjectKey: targetUserId,
            scope: s,
            isAllowed: true,
          }))
        );

      setMatrix({ ...matrix, assignments: nextAssignments });
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSavingKey(null);
    }
  };

  if (!isPlatformOperator) {
    return <NoAccessPage title={t('security.title')} />;
  }

  return (
    <PageShell kicker={t('app.nav.group.security')} title={t('security.title')} subtitle={t('security.subtitle')}>
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHCard>
        {loading ? (
          <LoadingState />
        ) : rows.length === 0 ? (
          <EmptyState message={t('security.emptyUsers')} />
        ) : (
          <div className="security-scroll">
            <table className="security-table">
              <thead>
                <tr>
                  <th className="sticky-col">{t('security.users')}</th>
                  {scopeColumns.map((c) => (
                    <th key={c.key}>{t(c.labelKey)}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((u) => {
                  const current = scopeStateByUserId.get(u.id) ?? new Set<number>();
                  const disabled = savingKey === u.id;
                  return (
                    <tr key={u.id} className={!u.isActive ? 'row--inactive' : ''}>
                      <td className="sticky-col">
                        <div className="userCell">
                          <div className="userName">{u.fullName}</div>
                          <div className="userMeta">
                            <span className="mono security-mono">{u.email}</span>
                            <span className="security-badge">{u.role}</span>
                          </div>
                        </div>
                      </td>
                      {scopeColumns.map((c) => {
                        const scope = scopeMap[c.key];
                        const checked = current.has(scope);
                        const ariaLabel = `${t(c.labelKey)} — ${u.fullName}`;
                        return (
                          <td key={c.key} className="cell-center">
                            <label className={`toggle ${disabled ? 'toggle--disabled' : ''}`}>
                              <input
                                type="checkbox"
                                checked={checked}
                                disabled={disabled}
                                aria-label={ariaLabel}
                                onChange={() => toggleScope(u.id, scope)}
                              />
                              <span className="toggle-ui" />
                            </label>
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </ZHCard>
    </PageShell>
  );
}

