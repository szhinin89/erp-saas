import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { TransferenciaEstadoBadge } from '../components/TransferenciaEstadoBadge';
import { useBodegas, useTransferenciasList } from '../hooks/useTransferencias';
import type { EstadoTransferencia, TransferenciasFilter } from '../api/transferenciaService';

export function TransferenciasListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('inventory.transfers.view');
  const canCreate = hasPerm('inventory.transfers.create');

  const [filter, setFilter] = useState<TransferenciasFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useTransferenciasList(filter);
  const { data: bodegas }          = useBodegas();

  if (!canView) return <NoAccessPage title="Transferencias" />;

  const items    = result?.items ?? [];
  const total    = result?.totalCount ?? 0;
  const page     = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize   ?? 20;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Transferencias</span>
          </nav>
          <h1 className="pg-title">Transferencias entre Bodegas</h1>
          <p className="pg-subtitle">Movimientos internos de stock entre almacenes.</p>
        </div>
        <div className="pg-header-right">
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button"
              onClick={() => navigate('/inventario/transferencias/nueva')}>
              <span className="material-symbols-outlined">add</span>
              Nueva Transferencia
            </button>
          )}
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <select
              className="zh-input"
              value={filter.estado ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, estado: (e.target.value as EstadoTransferencia) || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todos los estados</option>
              <option value="Borrador">Borrador</option>
              <option value="Confirmado">Confirmado</option>
              <option value="Cancelado">Cancelado</option>
            </select>
            <select
              className="zh-input"
              value={filter.bodegaOrigenId ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, bodegaOrigenId: e.target.value || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todas las bodegas origen</option>
              {(bodegas ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
            <select
              className="zh-input"
              value={filter.bodegaDestinoId ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, bodegaDestinoId: e.target.value || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todas las bodegas destino</option>
              {(bodegas ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
            <input className="zh-input" type="date" value={filter.fechaDesde ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, fechaDesde: e.target.value || undefined, pageNumber: 1 }))} />
            <input className="zh-input" type="date" value={filter.fechaHasta ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, fechaHasta: e.target.value || undefined, pageNumber: 1 }))} />
          </div>
          <div className="pg-table-controls-right">
            <span>{total} registros</span>
          </div>
        </div>

        {loading ? (
          <div style={{ padding: '40px' }}><LoadingState /></div>
        ) : items.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message="No hay transferencias que coincidan con los filtros." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Origen</th>
                  <th>Destino</th>
                  <th>Fecha</th>
                  <th>Estado</th>
                  <th style={{ textAlign: 'right' }}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => (
                  <tr key={t.id} style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/inventario/transferencias/${t.id}`)}>
                    <td><strong className="mono">{t.transferNumber}</strong></td>
                    <td>{t.sourceWarehouseName}</td>
                    <td>{t.destinationWarehouseName}</td>
                    <td>{new Date(t.transferDate).toLocaleDateString('es')}</td>
                    <td><TransferenciaEstadoBadge estado={t.status} /></td>
                    <td style={{ textAlign: 'right' }}>
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/inventario/transferencias/${t.id}`);
                      }}>Ver</ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {total > pageSize && (
          <div className="pg-table-footer">
            <span className="subtle" style={{ fontSize: 12 }}>Pág. {page} · {total} registros</span>
            <div className="pg-pagination-controls">
              <button className="pg-pagination-btn" disabled={page <= 1}
                onClick={() => setFilter((f) => ({ ...f, pageNumber: page - 1 }))}>
                <span className="material-symbols-outlined">chevron_left</span>
              </button>
              <button className="pg-pagination-btn" disabled={page * pageSize >= total}
                onClick={() => setFilter((f) => ({ ...f, pageNumber: page + 1 }))}>
                <span className="material-symbols-outlined">chevron_right</span>
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
