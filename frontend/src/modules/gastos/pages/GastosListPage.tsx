import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { supplierService, type Supplier } from '../../compras/suppliers/api/supplierService';
import { gastosService, type GastoDto, type EstadoGasto } from '../api/gastosService';
import './gastos-page.css';

type BadgeInfo = { cls: string; label: string };

function estadoBadge(e: EstadoGasto): BadgeInfo {
  const base = 'badge badge--md badge--upper';
  if (e === 2) return { cls: `${base} badge--green`,  label: 'Aprobado'  };
  if (e === 1) return { cls: `${base} badge--blue`,   label: 'Validado'  };
  if (e === 3) return { cls: `${base} badge--red`,    label: 'Rechazado' };
  return             { cls: `${base} badge--orange`,  label: 'Borrador'  };
}

export function GastosListPage() {
  const navigate = useNavigate();
  const hasPerm  = usePermissionsStore((s) => s.has);
  const role     = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const canView  = isAdmin || hasPerm('gastos.facturas.view');
  const canCreate = isAdmin || hasPerm('gastos.facturas.create');

  const [rows,       setRows]       = useState<GastoDto[]>([]);
  const [suppliers,  setSuppliers]  = useState<Supplier[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [msg,        setMsg]        = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [q,          setQ]          = useState('');
  const [actionId,   setActionId]   = useState<string | null>(null);
  const [rejectId,   setRejectId]   = useState<string | null>(null);
  const [rejectText, setRejectText] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [data, provs] = await Promise.all([
        gastosService.list(),
        supplierService.getAll(),
      ]);
      setRows(data);
      setSuppliers(provs);
    } catch (e) {
      setMsg({ type: 'error', text: e instanceof Error ? e.message : 'Error al cargar gastos' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const supplierMap = useMemo(() => {
    const m = new Map<string, string>();
    suppliers.forEach((s) => m.set(s.id, s.legalName));
    return m;
  }, [suppliers]);

  const filtered = useMemo(() => {
    const qn = q.trim().toLowerCase();
    if (!qn) return rows;
    return rows.filter((r) =>
      r.description.toLowerCase().includes(qn) ||
      r.expenseCategory.toLowerCase().includes(qn) ||
      (r.supplierId ? supplierMap.get(r.supplierId) ?? '' : '').toLowerCase().includes(qn),
    );
  }, [rows, q, supplierMap]);

  const stats = useMemo(() => ({
    total:      rows.length,
    aprobados:  rows.filter((r) => r.status === 2).length,
    pendientes: rows.filter((r) => r.status === 0 || r.status === 1).length,
    valor:      rows.reduce((s, r) => s + r.total, 0),
  }), [rows]);

  const doAction = async (fn: () => Promise<void>, successMsg: string) => {
    setMsg(null);
    try {
      await fn();
      setMsg({ type: 'success', text: successMsg });
      await load();
    } catch (e) {
      setMsg({ type: 'error', text: e instanceof Error ? e.message : 'Error' });
    } finally {
      setActionId(null);
    }
  };

  const handleValidar = (id: string) => {
    setActionId(id);
    void doAction(() => gastosService.validar(id), 'Gasto validado.');
  };
  const handleAprobar = (id: string) => {
    setActionId(id);
    void doAction(() => gastosService.aprobar(id), 'Gasto aprobado. Se generó el asiento contable.');
  };
  const handleRechazar = async () => {
    if (!rejectId || !rejectText.trim()) return;
    setActionId(rejectId);
    await doAction(() => gastosService.rechazar(rejectId, rejectText.trim()), 'Gasto rechazado.');
    setRejectId(null);
    setRejectText('');
  };

  if (!canView) return <NoAccessPage title="Gastos" />;

  return (
    <div className="pg-page">

      {/* Header */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Gastos</span>
          </nav>
          <h1 className="pg-title">Registro de Gastos</h1>
          <p className="pg-subtitle">Facturas de gasto y erogaciones del período.</p>
        </div>
        <div className="pg-header-right">
          <button className="zh-btn zh-btn--secondary" type="button" onClick={() => void load()} disabled={loading}>
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button" onClick={() => navigate('/gastos/nuevo')}>
              <span className="material-symbols-outlined">add</span>
              Nuevo Gasto
            </button>
          )}
        </div>
      </div>

      {msg && <ZHPageNotice variant={msg.type} message={msg.text} />}

      {/* KPIs */}
      {!loading && (
        <div className="pg-kpis">
          <div className="pg-kpi">
            <div className="pg-kpi-top"><div className="pg-kpi-icon pg-kpi-icon--primary"><span className="material-symbols-outlined">receipt_long</span></div></div>
            <div className="pg-kpi-bottom"><p className="pg-kpi-label">Total Gastos</p><p className="pg-kpi-value">{stats.total}<span className="pg-kpi-unit">docs</span></p></div>
          </div>
          <div className="pg-kpi">
            <div className="pg-kpi-top"><div className="pg-kpi-icon pg-kpi-icon--success"><span className="material-symbols-outlined">task_alt</span></div></div>
            <div className="pg-kpi-bottom"><p className="pg-kpi-label">Aprobados</p><p className="pg-kpi-value">{stats.aprobados}<span className="pg-kpi-unit">gastos</span></p></div>
          </div>
          <div className="pg-kpi">
            <div className="pg-kpi-top">
              <div className="pg-kpi-icon pg-kpi-icon--warning"><span className="material-symbols-outlined">pending</span></div>
              {stats.pendientes > 0 && <span className="pg-kpi-badge pg-kpi-badge--warning">Revisar</span>}
            </div>
            <div className="pg-kpi-bottom"><p className="pg-kpi-label">Pendientes</p><p className="pg-kpi-value">{stats.pendientes}<span className="pg-kpi-unit">gastos</span></p></div>
          </div>
          <div className="pg-kpi">
            <div className="pg-kpi-top"><div className="pg-kpi-icon pg-kpi-icon--error"><span className="material-symbols-outlined">payments</span></div></div>
            <div className="pg-kpi-bottom"><p className="pg-kpi-label">Total Erogado</p><p className="pg-kpi-value">${stats.valor.toLocaleString('es', { minimumFractionDigits: 2 })}<span className="pg-kpi-unit">USD</span></p></div>
          </div>
        </div>
      )}

      {/* Tabla */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">money_off</span>
            <span className="pg-section-label">Gastos del Período</span>
          </div>
        </div>

        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Filtrar por concepto, categoría o proveedor..."
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>
          <div className="pg-table-controls-right">
            <span>Mostrando {filtered.length} de {rows.length}</span>
          </div>
        </div>

        {loading ? (
          <div style={{ padding: 40 }}><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div style={{ padding: 40 }}><EmptyState message="Sin gastos registrados. Agrega el primero." /></div>
        ) : filtered.length === 0 ? (
          <div style={{ padding: 40 }}><EmptyState message="Sin resultados." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Categoría</th>
                  <th>Concepto</th>
                  <th>Proveedor</th>
                  <th>Subtotal</th>
                  <th className="gst-col-right">Total</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const badge  = estadoBadge(row.status);
                  const isBusy = actionId === row.id;
                  const prov   = row.supplierId ? (supplierMap.get(row.supplierId) ?? '—') : '—';
                  return (
                    <tr key={row.id}>
                      <td className="gst-col-date">{new Date(row.issueDate).toLocaleDateString('es')}</td>
                      <td><span className="gst-cat-badge">{row.expenseCategory}</span></td>
                      <td>{row.description}</td>
                      <td style={{ color: 'var(--color-text-secondary)' }}>{prov}</td>
                      <td>${row.subtotal.toFixed(2)}</td>
                      <td className="gst-cell-right" style={{ fontWeight: 600 }}>${row.total.toFixed(2)}</td>
                      <td><span className={badge.cls}>{badge.label}</span></td>
                      <td className="gst-cell-actions">
                        {row.status === 0 && (
                          <>
                            <button className="zh-btn zh-btn--ghost zh-btn--sm" disabled={isBusy} onClick={() => handleValidar(row.id)}>
                              <span className="material-symbols-outlined">check</span>
                              {isBusy ? '...' : 'Validar'}
                            </button>
                            <button className="zh-btn zh-btn--ghost zh-btn--sm" style={{ color: 'var(--color-error)' }} disabled={isBusy} onClick={() => { setRejectId(row.id); setRejectText(''); }}>
                              Rechazar
                            </button>
                          </>
                        )}
                        {row.status === 1 && (
                          <>
                            <button className="zh-btn zh-btn--primary zh-btn--sm" disabled={isBusy} onClick={() => handleAprobar(row.id)}>
                              <span className="material-symbols-outlined">done_all</span>
                              {isBusy ? '...' : 'Aprobar'}
                            </button>
                            <button className="zh-btn zh-btn--ghost zh-btn--sm" style={{ color: 'var(--color-error)' }} disabled={isBusy} onClick={() => { setRejectId(row.id); setRejectText(''); }}>
                              Rechazar
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modal rechazo */}
      {rejectId && (
        <div className="zh-modal-overlay" onClick={() => setRejectId(null)}>
          <div className="zh-modal" style={{ maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">Rechazar Gasto</h2>
              <button className="zh-modal-close" onClick={() => setRejectId(null)}>
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>
            <div className="zh-modal-body" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
              <p style={{ color: 'var(--color-text-secondary)' }}>Indica el motivo del rechazo:</p>
              <textarea className="zh-input" rows={3} value={rejectText} onChange={(e) => setRejectText(e.target.value)} placeholder="Motivo obligatorio..." style={{ resize: 'vertical' }} />
              <div style={{ display: 'flex', gap: 'var(--space-2)', justifyContent: 'flex-end' }}>
                <button className="zh-btn zh-btn--ghost zh-btn--md" onClick={() => setRejectId(null)}>Cancelar</button>
                <button className="zh-btn zh-btn--destructive zh-btn--md" disabled={!rejectText.trim()} onClick={() => void handleRechazar()}>
                  Confirmar rechazo
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
