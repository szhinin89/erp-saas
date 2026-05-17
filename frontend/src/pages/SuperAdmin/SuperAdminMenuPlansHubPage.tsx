import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SuperAdminPageTemplate } from '../../components/superadmin/SuperAdminPageTemplate';
import { SuperAdminMenuBuilderSection } from '../../components/superadmin/SuperAdminMenuBuilderSection';
import { useI18n } from '../../i18n/i18n';

const AUDIT_STORAGE_KEY = 'crmAuditLog';

type HubTab = 'menuBuilder' | 'auditoriaGlobal';

function parseHubTab(raw: string | null): HubTab {
  const v = (raw ?? '').trim().toLowerCase();
  if (v === 'auditoriaglobal' || v === 'auditoria-global' || v === 'audit') return 'auditoriaGlobal';
  return 'menuBuilder';
}

export function SuperAdminMenuPlansHubPage() {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();

  const tab = useMemo(() => parseHubTab(searchParams.get('tab')), [searchParams]);
  const [auditLines, setAuditLines] = useState<string[]>([]);

  const loadAudit = useCallback(() => {
    try {
      const raw = window.localStorage.getItem(AUDIT_STORAGE_KEY);
      setAuditLines(raw ? (JSON.parse(raw) as string[]) : []);
    } catch {
      setAuditLines([]);
    }
  }, []);

  useEffect(() => {
    if (tab === 'auditoriaGlobal') loadAudit();
  }, [tab, loadAudit]);

  const setTab = useCallback(
    (next: HubTab) => {
      setSearchParams(next === 'menuBuilder' ? {} : { tab: next }, { replace: true });
    },
    [setSearchParams],
  );

  return (
    <SuperAdminPageTemplate
      title={t('superadmin.menuPlansHub.title')}
      subtitle={t('superadmin.menuPlansHub.subtitle')}
      hideHeader
    >
      <div className="pg-page">

        {/* ── Header ─────────────────────────────────────────── */}
        <div className="pg-header-row">
          <div>
            <p className="pg-kicker">{t('app.nav.group.superadmin')}</p>
            <h1 className="pg-title">{t('superadmin.menuPlansHub.title')}</h1>
            <p className="pg-subtitle">{t('superadmin.menuPlansHub.subtitle')}</p>
          </div>
        </div>

        {/* ── Tab bar ────────────────────────────────────────── */}
        <div className="zh-form-tabs" role="tablist" aria-label={t('superadmin.menuPlansHub.tabListLabel')}>
          <button
            type="button"
            role="tab"
            id="hub-tab-menu-builder"
            aria-selected={tab === 'menuBuilder'}
            className={tab === 'menuBuilder' ? 'is-active' : ''}
            onClick={() => setTab('menuBuilder')}
          >
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>folder_managed</span>
            {t('superadmin.menuPlansHub.tabMenuBuilder')}
          </button>
          <button
            type="button"
            role="tab"
            id="hub-tab-auditoria"
            aria-selected={tab === 'auditoriaGlobal'}
            className={tab === 'auditoriaGlobal' ? 'is-active' : ''}
            onClick={() => setTab('auditoriaGlobal')}
          >
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>history</span>
            {t('superadmin.menuPlansHub.tabAuditoria')}
          </button>
        </div>

        {/* ── Tab panel ──────────────────────────────────────── */}
        <div
          role="tabpanel"
          aria-labelledby={tab === 'menuBuilder' ? 'hub-tab-menu-builder' : 'hub-tab-auditoria'}
        >

          {tab === 'menuBuilder' ? (
            <SuperAdminMenuBuilderSection crmWorkspace />
          ) : null}

          {tab === 'auditoriaGlobal' ? (
            <div className="pg-section">
              <div className="pg-section-header">
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">history</span>
                  <h2 className="pg-section-label">{t('superadmin.menuPlansHub.auditTitle')}</h2>
                </div>
                <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={loadAudit}>
                  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>refresh</span>
                  {t('common.refresh')}
                </button>
              </div>

              {auditLines.length === 0 ? (
                <p className="subtle" style={{ padding: 'var(--space-8)', textAlign: 'center' }}>
                  {t('superadmin.menuPlansHub.auditEmpty')}
                </p>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th style={{ width: 56 }}>#</th>
                        <th>{t('common.actions')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {[...auditLines].reverse().map((line, i) => (
                        <tr key={auditLines.length - i}>
                          <td>
                            <span className="subtle mono" style={{ fontSize: 11 }}>
                              {auditLines.length - i}
                            </span>
                          </td>
                          <td style={{ fontSize: 13 }}>{line}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          ) : null}

        </div>
      </div>
    </SuperAdminPageTemplate>
  );
}
