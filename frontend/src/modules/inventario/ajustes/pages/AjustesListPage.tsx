import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { AjusteEstadoBadge, AjusteTipoBadge } from '../components/AjusteEstadoBadge';
import { useAjustesList } from '../hooks/useAjustes';
import type { AjustesFilter, EstadoAjuste } from '../api/ajusteService';

export function AjustesListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('inventory.adjustments.view');
  const canCreate = hasPerm('inventory.adjustments.create');

  const [filter, setFilter] = useState<AjustesFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useAjustesList(filter);

  if (!canView) return <NoAccessPage title="Ajustes de Inventario" />;

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
            <span className="pg-breadcrumb-item">Ajustes</span>
          </nav>
          <h1 className="pg-title">Ajustes de Inventario</h1>
          <p className="pg-subtitle">Control de entradas y salidas manuales de stock.</p>
        </div>
        <div className="pg-header-right">
          {canCreate && (
            <button className="zh-btn zh-btn--primary" type="button"
              onClick={() => navigate('/inventario/ajustes/nuevo')}>
              <span className="material-symbols-outlined">add</span>
              Nuevo Ajuste
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
                setFilter((f) => ({ ...f, estado: (e.target.value as EstadoAjuste) || undefined, pageNumber: 1 }))
              }
            >
              <option value="">Todos los estados</option>
              <option value="Borrador">Borrador</option>
              <option value="Ejecutado">Ejecutado</option>
              <option value="Cancelado">Cancelado</option>
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
          <div style={{ padding: '40px' }}><EmptyState message="No hay ajustes que coincidan con los filtros." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
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
                  <th style={{ textAlign: 'right' }}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((a) => (
                  <tr key={a.id} style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/inventario/ajustes/${a.id}`)}>
                    <td><strong className="mono">{a.adjustmentNumber}</strong></td>
                    <td>{a.productName}</td>
                    <td>{a.warehouseName}</td>
                    <td><AjusteTipoBadge tipo={a.adjustmentType} cantidad={Math.abs(a.adjustmentQuantity)} /></td>
                    <td>{a.reason}</td>
                    <td>{new Date(a.adjustmentDate).toLocaleDateString('es')}</td>
                    <td><AjusteEstadoBadge estado={a.status} /></td>
                    <td style={{ textAlign: 'right' }}>
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
