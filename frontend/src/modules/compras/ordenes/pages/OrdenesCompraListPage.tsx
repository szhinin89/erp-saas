import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenesCompraList } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra, OrdenesCompraFilter } from '../api/ordenCompraService';

function estadoBadgeClass(estado: EstadoOrdenCompra): string {
  const map: Record<string, string> = {
    borrador: 'badge--gray', enviada: 'badge--orange', aprobada: 'badge--green',
    recibidaparcial: 'badge--blue', cerrada: 'badge--gray', cancelada: 'badge--red',
  };
  return `badge badge--md ${map[estado.toLowerCase()] ?? 'badge--gray'}`;
}

export function OrdenesCompraListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('compras.ordenes.view');
  const canCreate = hasPerm('compras.ordenes.create');

  const [filter, setFilter] = useState<OrdenesCompraFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useOrdenesCompraList(filter);

  if (!canView) return <NoAccessPage title="Órdenes de Compra" />;

  const items    = result?.items ?? [];
  const total    = result?.totalCount ?? 0;
  const page     = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize   ?? 20;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Compras</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Órdenes de Compra</span>
          </nav>
          <h1 className="pg-title">Órdenes de Compra</h1>
          <p className="pg-subtitle">Gestión y seguimiento de órdenes de compra a proveedores.</p>
        </div>
        <div className="pg-header-right">
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button"
              onClick={() => navigate('/compras/ordenes/nueva')}>
              <span className="material-symbols-outlined">add</span>
              Nueva Orden
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
                setFilter((f) => ({ ...f, estado: (e.target.value as EstadoOrdenCompra) || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todos los estados</option>
              <option value="Borrador">Borrador</option>
              <option value="Enviada">Enviada</option>
              <option value="Aprobada">Aprobada</option>
              <option value="RecibidaParcial">Recibida parcial</option>
              <option value="Cerrada">Cerrada</option>
              <option value="Cancelada">Cancelada</option>
            </select>
            <input
              className="zh-input"
              type="date"
              value={filter.fechaDesde ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, fechaDesde: e.target.value || undefined, pageNumber: 1 }))}
            />
            <input
              className="zh-input"
              type="date"
              value={filter.fechaHasta ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, fechaHasta: e.target.value || undefined, pageNumber: 1 }))}
            />
          </div>
          <div className="pg-table-controls-right">
            <span>{total} registros</span>
          </div>
        </div>

        {loading ? (
          <div style={{ padding: '40px' }}><LoadingState /></div>
        ) : items.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message="No hay órdenes de compra que coincidan con los filtros." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Proveedor</th>
                  <th>Fecha emisión</th>
                  <th>Fecha requerida</th>
                  <th style={{ textAlign: 'right' }}>Total</th>
                  <th>Estado</th>
                  <th style={{ textAlign: 'right' }}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((oc) => (
                  <tr key={oc.id} style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/compras/ordenes/${oc.id}`)}>
                    <td><strong className="mono">{oc.numeroOrden}</strong></td>
                    <td>{oc.proveedorNombre}</td>
                    <td>{new Date(oc.fechaEmision).toLocaleDateString('es')}</td>
                    <td>{new Date(oc.fechaRequerida).toLocaleDateString('es')}</td>
                    <td style={{ textAlign: 'right' }} className="mono">${oc.total.toFixed(2)}</td>
                    <td><span className={estadoBadgeClass(oc.estado)}>{oc.estado}</span></td>
                    <td style={{ textAlign: 'right' }}>
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/compras/ordenes/${oc.id}`);
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
