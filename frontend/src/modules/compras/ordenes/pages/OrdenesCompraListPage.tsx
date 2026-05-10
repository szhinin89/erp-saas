import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenesCompraList } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra, OrdenesCompraFilter } from '../api/ordenCompraService';

const ESTADO_COLORS: Record<EstadoOrdenCompra, string> = {
  Borrador:         'var(--zh-text-muted)',
  Enviada:          '#2563eb',
  Aprobada:         '#16a34a',
  RecibidaParcial:  '#ca8a04',
  Cerrada:          '#6d28d9',
  Cancelada:        '#dc2626',
};

function EstadoBadge({ estado }: { estado: EstadoOrdenCompra }) {
  return (
    <span style={{
      fontSize: '0.75rem',
      fontWeight: 600,
      padding: '2px 8px',
      borderRadius: '99px',
      background: ESTADO_COLORS[estado] + '20',
      color: ESTADO_COLORS[estado],
    }}>
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
      <TableCard>
        {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

        {/* Filtros */}
        <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', marginBottom: '16px' }}>
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
            <table className="table">
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
                  <tr
                    key={oc.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/compras/ordenes/${oc.id}`)}
                  >
                    <td><strong>{oc.numeroOrden}</strong></td>
                    <td>{oc.proveedorNombre}</td>
                    <td>{new Date(oc.fechaEmision).toLocaleDateString()}</td>
                    <td>{new Date(oc.fechaRequerida).toLocaleDateString()}</td>
                    <td>${oc.total.toFixed(2)}</td>
                    <td><EstadoBadge estado={oc.estado} /></td>
                    <td>
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
              <div style={{ display: 'flex', gap: '8px', marginTop: '16px', justifyContent: 'flex-end' }}>
                <ZHBtn variant="ghost" size="sm" disabled={(filter.pageNumber ?? 1) <= 1}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}>
                  Anterior
                </ZHBtn>
                <span style={{ lineHeight: '32px', color: 'var(--zh-text-muted)' }}>
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
      </TableCard>
    </PageShell>
  );
}
