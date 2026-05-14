import { useEffect, useState } from 'react';
import { activityService, type UserActivityDto } from '../services/activityService';
import { useI18n } from '../i18n/i18n';
import { Badge, EmptyState, LoadingState } from './PageShell';
import { ZHPageNotice } from './zh/ZHPageNotice';
import { formatApiError } from '../modules/lib/formatApiError';
import './EntityAuditPanel.css';

function actionVerbI18nKey(action: string): string {
  const i = action.lastIndexOf('.');
  const verb = (i >= 0 ? action.slice(i + 1) : action).toLowerCase();
  switch (verb) {
    case 'create':
      return 'audit.action.create';
    case 'update':
      return 'audit.action.update';
    case 'enable':
      return 'audit.action.enable';
    case 'disable':
      return 'audit.action.disable';
    default:
      return 'audit.action.unknown';
  }
}

type Props = {
  entityType: string;
  entityId: string;
  take?: number;
  /** Incrementar tras guardar en el formulario para volver a cargar la lista. */
  refreshKey?: number;
};

/**
 * Lista los últimos eventos de `user_activity` para una entidad (mismo tenant).
 */
export function EntityAuditPanel({ entityType, entityId, take = 10, refreshKey = 0 }: Props) {
  const { t } = useI18n();
  const [rows, setRows] = useState<UserActivityDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
    });
    void activityService
      .forEntity({ entityType, entityId, take })
      .then((data) => {
        if (!cancelled) setRows(data ?? []);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(formatApiError(err));
          setRows([]);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [entityType, entityId, take, refreshKey]);

  if (loading) return <LoadingState />;
  if (error) return <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />;
  if (!rows || rows.length === 0) return <EmptyState message={t('audit.empty')} />;

  return (
    <div className="entity-audit-panel">
      <p className="subtle zh-mb-12">{t('audit.hint')}</p>
      <table className="table entity-audit-responsive-table">
        <thead>
          <tr>
            <th>{t('audit.column.when')}</th>
            <th>{t('audit.column.who')}</th>
            <th>{t('audit.column.what')}</th>
            <th>{t('audit.column.detail')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => {
            const who =
              [r.userFullName?.trim(), r.userEmail?.trim()].filter(Boolean).join(' · ') ||
              r.userEmail ||
              '—';
            const verbKey = actionVerbI18nKey(r.action);
            return (
              <tr key={r.id}>
                <td data-label={t('audit.column.when')}>
                  {new Date(r.createdAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}
                </td>
                <td data-label={t('audit.column.who')}>{who}</td>
                <td data-label={t('audit.column.what')}>
                  <Badge label={t(verbKey, r.action)} variant="gray" />
                </td>
                <td data-label={t('audit.column.detail')} className="subtle">{r.description ?? '—'}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
