import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SuperAdminCrudTemplate } from '../../templates/SuperAdminCrudTemplate';
import { SuperAdminPlansSection } from '../../components/superadmin/SuperAdminPlansSection';
import { SuperAdminMenuBuilderSection } from '../../components/superadmin/SuperAdminMenuBuilderSection';
import { useI18n } from '../../i18n/i18n';

type PlansTab = 'plans' | 'menu';

function parsePlansTab(raw: string | null): PlansTab {
  const v = (raw ?? '').trim().toLowerCase();
  if (v === 'menu' || v === 'menubuilder' || v === 'menu-builder') return 'menu';
  return 'plans';
}

export function SuperAdminPlansPage() {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = useMemo(() => parsePlansTab(searchParams.get('tab')), [searchParams]);
  const [auditLines, setAuditLines] = useState<string[]>([]);

  const loadAudit = useCallback(() => {
    try {
      const raw = window.localStorage.getItem('crmAuditLog');
      setAuditLines(raw ? (JSON.parse(raw) as string[]) : []);
    } catch {
      setAuditLines([]);
    }
  }, []);

  useEffect(() => {
    if (tab === 'menu') loadAudit();
  }, [tab, loadAudit]);

  const setTab = useCallback(
    (next: PlansTab) => {
      setSearchParams(next === 'plans' ? {} : { tab: next }, { replace: true });
    },
    [setSearchParams],
  );

  return (
    <SuperAdminCrudTemplate
      title={t('superadmin.shell.plans')}
      subtitle={t('superadmin.menuPlansHub.subtitle')}
    >
      <div className="zh-form-tabs" role="tablist" aria-label={t('superadmin.menuPlansHub.tabListLabel')}>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'plans'}
          className={tab === 'plans' ? 'is-active' : ''}
          onClick={() => setTab('plans')}
        >
          <span className="material-symbols-outlined pg-icon-16">loyalty</span>
          {t('superadmin.shell.plans')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'menu'}
          className={tab === 'menu' ? 'is-active' : ''}
          onClick={() => setTab('menu')}
        >
          <span className="material-symbols-outlined pg-icon-16">folder_managed</span>
          {t('superadmin.menuPlansHub.tabMenuBuilder')}
        </button>
      </div>

      {tab === 'plans' ? <SuperAdminPlansSection /> : null}
      {tab === 'menu' ? (
        <>
          <SuperAdminMenuBuilderSection crmWorkspace />
          {auditLines.length > 0 && (
            <p className="subtle pg-mt-md">
              Auditoría local del builder ({auditLines.length} entradas). Log canónico:{' '}
              <a className="zh-link" href="/superadmin/audit">/superadmin/audit</a>.
            </p>
          )}
        </>
      ) : null}
    </SuperAdminCrudTemplate>
  );
}
