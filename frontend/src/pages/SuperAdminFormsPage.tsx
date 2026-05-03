import { useMemo, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { PageShell, TableCard, EmptyState } from '../components/PageShell';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { ZHField } from '../components/zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHStack } from '../components/zh/ZHLayout';
import { buildNavGroups } from '../nav/navConfig';

export function SuperAdminFormsPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const [q, setQ] = useState('');

  const isSuperAdmin = (user?.role ?? '') === 'SuperAdmin';
  const groups = useMemo(() => buildNavGroups(t), [t]);
  const items = useMemo(() => {
    const query = q.trim().toLowerCase();
    const rows = groups
      .filter((g) => g.items.length > 0)
      .flatMap((g) => g.items.map((it) => ({ group: g, item: it })));
    if (!query) return rows;
    return rows.filter((r) =>
      r.group.label.toLowerCase().includes(query) ||
      r.item.label.toLowerCase().includes(query) ||
      r.item.to.toLowerCase().includes(query)
    );
  }, [groups, q]);

  if (!isSuperAdmin) {
    return (
      <PageShell kicker={t('app.nav.group.home')} title={t('superadmin.forms.title')} subtitle={t('common.noAccess')}>
        <TableCard>
          <EmptyState message={t('common.noAccess')} />
        </TableCard>
      </PageShell>
    );
  }

  return (
    <PageShell
      kicker={t('app.nav.group.home')}
      title={t('superadmin.forms.title')}
      subtitle={t('superadmin.forms.subtitle')}
    >
      <TableCard>
        <ZHCardSection title={t('superadmin.forms.searchTitle')}>
          <ZHGridRow cols={1}>
            <ZHField label={t('superadmin.forms.searchLabel')}>
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t('superadmin.forms.searchPlaceholder')}
              />
            </ZHField>
          </ZHGridRow>

          {items.length === 0 ? (
            <EmptyState message={t('common.noData')} />
          ) : (
            <ZHStack gap={8}>
              {items.map(({ group, item }) => (
                <NavLink key={`${group.id}:${item.to}`} to={item.to} className="sa-formRow">
                  <div className="sa-formRow-title">{item.label}</div>
                  <div className="sa-formRow-meta">
                    <span className="badge badge--gray">{group.label}</span>
                    <span className="mono">{item.to}</span>
                    {item.permissionKey ? <span className="mono">{item.permissionKey}</span> : null}
                  </div>
                </NavLink>
              ))}
            </ZHStack>
          )}
        </ZHCardSection>
      </TableCard>
    </PageShell>
  );
}

