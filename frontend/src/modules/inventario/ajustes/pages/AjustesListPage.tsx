import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { AjusteEstadoBadge, AjusteTipoBadge } from '../components/AjusteEstadoBadge';
import { useAjustesList } from '../hooks/useAjustes';
import type { AjustesFilter, EstadoAjuste } from '../api/ajusteService';
import './ajustes-pages.css';

export function AjustesListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('inventario.ajustes.view');
  const canCreate = hasPerm('inventario.ajustes.create');

  const [filter, setFilter] = useState<AjustesFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useAjustesList(filter);

  if (!canView) return <NoAccessPage title="Ajustes de Inventario" />;

  const items = result?.items ?? [];
  const total = result?.totalCount ?? 0;

  return (
    <PageShell
      kicker="Inventario"
      title="Ajustes de inventario"
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" onClick={() => navigate('/inventario/ajustes/nuevo')}>
            Nuevo ajuste
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

        {/* Filtros */}
        <div className="aj-list-filters">
          <select
            value={filter.estado ?? ''}
            onChange={(e) =>
              setFilter((f) => ({ ...f, estado: (e.target.value as EstadoAjuste) || undefined, pageNumber: 1 }))
            }
          >
            <option value="">Todos los estados</option>
            <option value="Borrador">Borrador</option>
            <option value="Ejecutado">Ejecutado</option>
            <option value="Cancelado">Cancelado</option>
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
          <EmptyState message="No hay ajustes que coincidan con los filtros." />
        ) : (
          <>
            <table className="table aj-responsive-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Producto</th>
                  <th>Bodega</th>
                  <th>Cantidad</th>
                  <th>Motivo</th>
                  <th>Fecha</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((a) => (
                  <tr key={a.id} className="aj-row-clickable" onClick={() => navigate(`/inventario/ajustes/${a.id}`)}>
                    <td data-label="Número"><strong>{a.numeroAjuste}</strong></td>
                    <td data-label="Producto">{a.productoNombre}</td>
                    <td data-label="Bodega">{a.bodegaNombre}</td>
                    <td data-label="Cantidad"><AjusteTipoBadge tipo={a.tipoAjuste} cantidad={Math.abs(a.cantidadAjuste)} /></td>
                    <td data-label="Motivo">{a.motivo}</td>
                    <td data-label="Fecha">{new Date(a.fechaAjuste).toLocaleDateString()}</td>
                    <td data-label="Estado"><AjusteEstadoBadge estado={a.estado} /></td>
                    <td data-label="Acciones" className="aj-cell-actions">
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/inventario/ajustes/${a.id}`);
                      }}>Ver</ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {total > (filter.pageSize ?? 20) && (
              <div className="aj-pagination">
                <ZHBtn variant="ghost" size="sm" disabled={(filter.pageNumber ?? 1) <= 1}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}>
                  Anterior
                </ZHBtn>
                <span className="aj-pagination-label">
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
