import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZhDateInput } from '../../../../components/zh/inputs';
import { AjusteEstadoBadge, AjusteTipoBadge } from ../components/AdjustmentStatusBadge';
import { useAdjustmentsList } from '../hooks/useAdjustments';
import type { AdjustmentsFilter, AdjustmentStatus } from '../api/adjustmentService';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { formatDate } from '../../../../lib/formatters/dateFormatters';

export function AjustesListPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canView   = canShow('inventory.adjustments.view');
  const canCreate = canShow('inventory.adjustments.create');

  const [filter, setFilter] = useState<AdjustmentsFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useAdjustmentsList(filter);

  if (!canView) return <NoAccessPage title="Ajustes de Inventario" />;

  const items    = result?.items ?? [];
  const total    = result?.totalCount ?? 0;
  const page     = filter.pageNumber ?? 1;
  const pageSize = filter.pageSize   ?? 20;

  return (
    <ErpPageTemplate
      kicker="Inventario"
      title="Ajustes de Inventario"
      subtitle="Control de entradas y salidas manuales de stock."
      action={
        canCreate ? (
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/inventario/ajustes/nuevo')}
          >
            <span className="material-symbols-outlined">add</span>
            Nuevo Ajuste
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
              value={filter.status ?? ''}
              onChange={(e) =>
                setFilter((f) => ({ ...f, estado: (e.target.value as AdjustmentStatus) || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todos los estados</option>
              <option value="Borrador">Borrador</option>
              <option value="Ejecutado">Ejecutado</option>
              <option value="Cancelado">Cancelado</option>
            </select>
            <ZhDateInput
              value={filter.dateFrom ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, dateFrom: e.target.value || undefined, pageNumber: 1 }))}
            />
            <ZhDateInput
              value={filter.dateTo ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, dateTo: e.target.value || undefined, pageNumber: 1 }))}
            />
          </div>
          <div className="pg-table-controls-right">
            <span>{total} registros</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : items.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="No hay ajustes que coincidan con los filtros." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Producto</th>
                  <th>Bodega</th>
                  <th>Cantidad</th>
                  <th>Motivo</th>
                  <th>Fecha</th>
                  <th>Estado</th>
                  <th className="pg-th-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((a) => (
                  <tr key={a.id} className="pg-row-clickable"
                    onClick={() => navigate(`/inventario/ajustes/${a.id}`)}>
                    <td><strong className="mono">{a.adjustmentNumber}</strong></td>
                    <td>{a.productName}</td>
                    <td>{a.warehouseName}</td>
                    <td><AjusteTipoBadge tipo={a.adjustmentType} cantidad={Math.abs(a.adjustmentQuantity)} /></td>
                    <td>{a.reason}</td>
                    <td>{formatDate(a.adjustmentDate)}</td>
                    <td><AjusteEstadoBadge estado={a.status} /></td>
                    <td className="pg-td-right">
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/inventario/ajustes/${a.id}`);
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
