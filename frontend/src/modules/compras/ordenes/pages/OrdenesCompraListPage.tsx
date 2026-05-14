import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenesCompraList } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra, OrdenesCompraFilter } from '../api/ordenCompraService';
import './ordenes-compra-pages.css';

function estadoClass(estado: EstadoOrdenCompra) {
  return `oc-status-badge--${estado.toLowerCase()}`;
}

function EstadoBadge({ estado }: { estado: EstadoOrdenCompra }) {
  return (
    <span className={`oc-status-badge oc-status-badge--sm ${estadoClass(estado)}`}>
      {estado}
    </span>
  );
}

export function OrdenesCompraListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('compras.ordenes.view');
  const canCreate = hasPerm('compras.ordenes.create');

  const [filter, setFilter] = useState<OrdenesCompraFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useOrdenesCompraList(filter);

  if (!canView) return <NoAccessPage title="Órdenes de Compra" />;

  const items = result?.items ?? [];
  const total = result?.totalCount ?? 0;

  return (
    <PageShell
      kicker="Compras"
      title="Órdenes de compra"
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" onClick={() => navigate('/compras/ordenes/nueva')}>
            Nueva orden
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

        {/* Filtros */}
        <div className="oc-list-filters">
          <select
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
            type="date"
            value={filter.fechaDesde ?? ''}
            onChange={(e) => setFilter((f) => ({ ...f, fechaDesde: e.target.value || undefined, pageNumber: 1 }))}
            placeholder="Desde"
          />
          <input
            type="date"
            value={filter.fechaHasta ?? ''}
            onChange={(e) => setFilter((f) => ({ ...f, fechaHasta: e.target.value || undefined, pageNumber: 1 }))}
            placeholder="Hasta"
          />
        </div>

        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message="No hay órdenes de compra que coincidan con los filtros." />
        ) : (
          <>
            <table className="table oc-responsive-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Proveedor</th>
                  <th>Fecha emisión</th>
                  <th>Fecha requerida</th>
                  <th>Total</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((oc) => (
                  <tr key={oc.id} className="oc-row-clickable" onClick={() => navigate(`/compras/ordenes/${oc.id}`)}>
                    <td data-label="Número"><strong>{oc.numeroOrden}</strong></td>
                    <td data-label="Proveedor">{oc.proveedorNombre}</td>
                    <td data-label="Fecha emisión">{new Date(oc.fechaEmision).toLocaleDateString()}</td>
                    <td data-label="Fecha requerida">{new Date(oc.fechaRequerida).toLocaleDateString()}</td>
                    <td data-label="Total">${oc.total.toFixed(2)}</td>
                    <td data-label="Estado"><EstadoBadge estado={oc.estado} /></td>
                    <td data-label="Acciones" className="oc-cell-actions">
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/compras/ordenes/${oc.id}`);
                      }}>Ver</ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {total > (filter.pageSize ?? 20) && (
              <div className="oc-pagination">
                <ZHBtn variant="ghost" size="sm" disabled={(filter.pageNumber ?? 1) <= 1}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}>
                  Anterior
                </ZHBtn>
                <span className="oc-pagination-label">
                  Pág. {filter.pageNumber ?? 1} · {total} registros
                </span>
                <ZHBtn variant="ghost" size="sm"
                  disabled={(filter.pageNumber ?? 1) * (filter.pageSize ?? 20) >= total}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) + 1 }))}>
                  Siguiente
                </ZHBtn>
              </div>
            )}
          </>
        )}
      </Card>
    </PageShell>
  );
}
