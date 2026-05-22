import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useOrdenesCompraList } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra, OrdenesCompraFilter } from '../api/ordenCompraService';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

function estadoBadgeClass(estado: EstadoOrdenCompra): string {
  const map: Record<string, string> = {
    borrador: 'badge--gray', enviada: 'badge--orange', aprobada: 'badge--green',
    recibidaparcial: 'badge--blue', cerrada: 'badge--gray', cancelada: 'badge--red',
  };
  return `badge badge--md ${map[estado.toLowerCase()] ?? 'badge--gray'}`;
}

export function OrdenesCompraListPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canView   = canShow('purchases.orders.view');
  const canCreate = canShow('purchases.orders.create');

  const [filter, setFilter] = useState<OrdenesCompraFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useOrdenesCompraList(filter);

  if (!canView) return <NoAccessPage title="Órdenes de Compra" />;

  const items    = result?.items ?? [];
  const total    = result?.totalCount ?? 0;
  const page     = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize   ?? 20;

  return (
    <ErpPageTemplate
      kicker="Compras"
      title="Órdenes de Compra"
      subtitle="Gestión y seguimiento de órdenes de compra a proveedores."
      action={
        canCreate ? (
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/compras/ordenes/nueva')}
          >
            <span className="material-symbols-outlined">add</span>
            Nueva Orden
          </button>
        ) : undefined
      }
    >
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
          <div className="pg-pad-40"><LoadingState /></div>
        ) : items.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="No hay órdenes de compra que coincidan con los filtros." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Proveedor</th>
                  <th>Fecha emisión</th>
                  <th>Fecha requerida</th>
                  <th className="pg-th-right">Total</th>
                  <th>Estado</th>
                  <th className="pg-th-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((oc) => (
                  <tr key={oc.id} className="pg-row-clickable"
                    onClick={() => navigate(`/compras/ordenes/${oc.id}`)}>
                    <td><strong className="mono">{oc.numeroOrden}</strong></td>
                    <td>{oc.proveedorNombre}</td>
                    <td>{new Date(oc.fechaEmision).toLocaleDateString('es')}</td>
                    <td>{new Date(oc.fechaRequerida).toLocaleDateString('es')}</td>
                    <td className="mono pg-td-right">${oc.total.toFixed(2)}</td>
                    <td><span className={estadoBadgeClass(oc.estado)}>{oc.estado}</span></td>
                    <td className="pg-td-right">
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
            <span className="subtle pg-text-muted-sm">Pág. {page} · {total} registros</span>
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
    </ErpPageTemplate>
  );
}
