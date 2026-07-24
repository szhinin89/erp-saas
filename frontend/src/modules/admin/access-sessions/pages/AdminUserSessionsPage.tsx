import { useCallback, useEffect, useState } from 'react';
import { EmptyState, ErrorState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHCard } from '../../../../components/zh/ZHCard';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { formatDateTime } from '../../../../lib/formatters/dateFormatters';
import {
  userSessionAdminService,
  type SessionStatisticsDto,
  type UserSessionAdminDto,
} from '../../api/userSessionAdminService';
import { formatApiError } from '../../../lib/formatApiError';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

const STATUS_LABEL: Record<UserSessionAdminDto['status'], string> = {
  Active: 'Activa',
  ClosedManually: 'Cerrada manualmente',
  ClosedByNewLogin: 'Cerrada por nuevo login',
  Expired: 'Expirada',
};

const PAGE_SIZE = 25;

export function AdminUserSessionsPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow('access.sessions.view');
  const canClose = canShow('access.sessions.close');

  const [identityUserId, setIdentityUserId] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [status, setStatus] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [pageNumber, setPageNumber] = useState(1);

  const [rows, setRows] = useState<UserSessionAdminDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [stats, setStats] = useState<SessionStatisticsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [closingId, setClosingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [paged, statistics] = await Promise.all([
        userSessionAdminService.getPaged({
          identityUserId: identityUserId || undefined,
          companyId: companyId || undefined,
          status: status || undefined,
          fromUtc: fromDate ? `${fromDate}T00:00:00Z` : undefined,
          toUtc: toDate ? `${toDate}T23:59:59Z` : undefined,
          pageNumber,
          pageSize: PAGE_SIZE,
        }),
        userSessionAdminService.getStatistics(companyId || undefined),
      ]);
      setRows(paged.items);
      setTotalCount(paged.totalCount);
      setStats(statistics);
    } catch (e) {
      setError(formatApiError(e));
      setRows([]);
      setStats(null);
    } finally {
      setLoading(false);
    }
  }, [identityUserId, companyId, status, fromDate, toDate, pageNumber]);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView) return <NoAccessPage title="Sesiones de Usuario" />;

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  function applyFilters() {
    setPageNumber(1);
  }

  async function confirmClose() {
    if (!closingId) return;
    const id = closingId;
    setClosingId(null);
    try {
      await userSessionAdminService.close(id);
      setNotice('Sesión cerrada correctamente.');
      await load();
    } catch (e) {
      setError(formatApiError(e));
    }
  }

  return (
    <ErpPageTemplate
      kicker="Administración"
      title="Sesiones de Usuario"
      subtitle="Monitoreo y cierre administrativo de sesiones activas (UserSession)."
      action={
        <ZHBtn variant="secondary" type="button" disabled={loading} onClick={() => void load()}>
          <span className="material-symbols-outlined">refresh</span>
          Actualizar
        </ZHBtn>
      }
    >
      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}
      {notice && <ZHPageNotice variant="success" message={notice} />}

      <div className="pg-section pg-stat-cards">
        <ZHCard title="Activas"><strong>{stats?.active ?? '—'}</strong></ZHCard>
        <ZHCard title="Cerradas manualmente"><strong>{stats?.closedManually ?? '—'}</strong></ZHCard>
        <ZHCard title="Cerradas por nuevo login"><strong>{stats?.closedByNewLogin ?? '—'}</strong></ZHCard>
        <ZHCard title="Expiradas"><strong>{stats?.expired ?? '—'}</strong></ZHCard>
        <ZHCard title="Total"><strong>{stats?.total ?? '—'}</strong></ZHCard>
      </div>

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <input
              className="zh-input"
              type="text"
              placeholder="Id. de usuario (GUID)…"
              value={identityUserId}
              onChange={(e) => setIdentityUserId(e.target.value)}
            />
            <input
              className="zh-input"
              type="text"
              placeholder="Id. de empresa (GUID)…"
              value={companyId}
              onChange={(e) => setCompanyId(e.target.value)}
            />
            <select className="zh-input" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">Todos los estados</option>
              <option value="Active">Activa</option>
              <option value="ClosedManually">Cerrada manualmente</option>
              <option value="ClosedByNewLogin">Cerrada por nuevo login</option>
              <option value="Expired">Expirada</option>
            </select>
            <input
              className="zh-input"
              type="date"
              aria-label="Fecha desde"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
            <input
              className="zh-input"
              type="date"
              aria-label="Fecha hasta"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
            <ZHBtn variant="secondary" type="button" onClick={applyFilters}>
              Filtrar
            </ZHBtn>
          </div>
          <div className="pg-table-controls-right">
            <span>Pág. {pageNumber} de {totalPages}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : error ? (
          <div className="pg-pad-40">
            <ErrorState message={error} />
            <ZHBtn variant="secondary" type="button" onClick={() => void load()}>Reintentar</ZHBtn>
          </div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="No se encontraron sesiones con los filtros indicados." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Empresa</th>
                  <th>Sucursal</th>
                  <th>Terminal</th>
                  <th>Estado</th>
                  <th>Creada</th>
                  <th>Última actualización</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.sessionId}>
                    <td title={row.identityUserId}>{row.identityUserId.slice(0, 8)}…</td>
                    <td title={row.companyId}>{row.companyId.slice(0, 8)}…</td>
                    <td title={row.branchId}>{row.branchId.slice(0, 8)}…</td>
                    <td>{row.terminalId}</td>
                    <td>{STATUS_LABEL[row.status]}</td>
                    <td>{formatDateTime(row.createdAt)}</td>
                    <td>{row.updatedAt ? formatDateTime(row.updatedAt) : '—'}</td>
                    <td>
                      {canClose && row.status === 'Active' && (
                        <ZHBtn
                          variant="destructive"
                          size="sm"
                          type="button"
                          onClick={() => setClosingId(row.sessionId)}
                        >
                          Cerrar sesión
                        </ZHBtn>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pg-table-footer">
          <div className="pg-pagination-controls">
            <button
              className="pg-pagination-btn" type="button"
              disabled={pageNumber <= 1 || loading}
              onClick={() => setPageNumber((p) => p - 1)}
            >
              <span className="material-symbols-outlined">chevron_left</span>
            </button>
            <button
              className="pg-pagination-btn" type="button"
              disabled={pageNumber >= totalPages || loading}
              onClick={() => setPageNumber((p) => p + 1)}
            >
              <span className="material-symbols-outlined">chevron_right</span>
            </button>
          </div>
        </div>
      </div>

      <ZHConfirmModal
        open={closingId !== null}
        title="Cerrar sesión"
        message="¿Confirma cerrar esta sesión? El usuario deberá iniciar sesión nuevamente en esta empresa."
        variant="danger"
        confirmLabel="Cerrar sesión"
        onConfirm={() => void confirmClose()}
        onCancel={() => setClosingId(null)}
      />
    </ErpPageTemplate>
  );
}
