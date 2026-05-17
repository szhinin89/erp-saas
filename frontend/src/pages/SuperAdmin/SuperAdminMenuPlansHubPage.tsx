import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SuperAdminPageTemplate } from '../../components/superadmin/SuperAdminPageTemplate';
import { SuperAdminMenuBuilderSection } from '../../components/superadmin/SuperAdminMenuBuilderSection';
import { useI18n } from '../../i18n/i18n';

const AUDIT_STORAGE_KEY = 'crmAuditLog';

type HubTab = 'menuBuilder' | 'auditoriaGlobal';

function parseAuditLine(line: string): { timestamp: string; action: string; details: string } {
  const match = /^\[([^\]]+)\]\s+(.+)$/.exec(line);
  const details = match ? match[2] : line;
  const d = details.toLowerCase();
  let action = 'UPDATE';
  if (d.includes('guardado') || d.includes('guardó') || d.includes('snapshot')) action = 'SAVE';
  else if (d.includes('activado') || d.includes('desactivado')) action = 'TOGGLE';
  else if (d.includes('exportad')) action = 'EXPORT';
  else if (d.includes('importad')) action = 'IMPORT';
  else if (d.includes('heredó')) action = 'INHERIT';
  else if (d.includes('reseteado') || d.includes('reset')) action = 'RESET';
  else if (d.includes('sincroniz') || d.includes('sync')) action = 'SYNC';
  else if (d.includes('eliminado')) action = 'DELETE';
  return { timestamp: match ? match[1] : '—', action, details };
}

function auditBadge(action: string): string {
  switch (action) {
    case 'SAVE':    return 'green';
    case 'TOGGLE':  return 'orange';
    case 'RESET':
    case 'DELETE':  return 'red';
    case 'EXPORT':
    case 'IMPORT':
    case 'SYNC':    return 'gray';
    default:        return 'blue';
  }
}

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
                <div style={{ overflowX: 'auto', maxHeight: 400, overflowY: 'auto' }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th style={{ width: 120 }}>Timestamp</th>
                        <th style={{ width: 100 }}>Acción</th>
                        <th>Detalles</th>
                      </tr>
                    </thead>
                    <tbody>
                      {auditLines.map((line, i) => {
                        const { timestamp, action, details } = parseAuditLine(line);
                        return (
                          <tr key={`${i}-${line.slice(0, 16)}`}>
                            <td>
                              <span className="subtle mono" style={{ fontSize: 11 }}>{timestamp}</span>
                            </td>
                            <td>
                              <span className={`badge badge--${auditBadge(action)} badge--upper`} style={{ fontSize: 10 }}>
                                {action}
                              </span>
                            </td>
                            <td style={{ fontSize: 12 }}>{details}</td>
                          </tr>
                        );
                      })}
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
