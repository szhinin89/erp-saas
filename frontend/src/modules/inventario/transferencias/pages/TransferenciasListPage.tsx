import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { TransferenciaEstadoBadge } from '../components/TransferenciaEstadoBadge';
import { useBodegas, useTransferenciasList } from '../hooks/useTransferencias';
import type { EstadoTransferencia, TransferenciasFilter } from '../api/transferenciaService';
import './transferencias-pages.css';

export function TransferenciasListPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('inventario.transferencias.view');
  const canCreate = hasPerm('inventario.transferencias.create');

  const [filter, setFilter] = useState<TransferenciasFilter>({ pageNumber: 1, pageSize: 20 });
  const { result, loading, error } = useTransferenciasList(filter);
  const { data: bodegas }          = useBodegas();

  if (!canView) return <NoAccessPage title="Transferencias" />;

  const items = result?.items ?? [];
  const total = result?.totalCount ?? 0;

  return (
    <PageShell
      kicker="Inventario"
      title="Transferencias entre bodegas"
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" onClick={() => navigate('/inventario/transferencias/nueva')}>
            Nueva transferencia
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

        {/* Filtros */}
        <div className="trf-list-filters">
          <select
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
            value={filter.bodegaOrigenId ?? ''}
            onChange={(e) =>
              setFilter((f) => ({ ...f, bodegaOrigenId: e.target.value || undefined, pageNumber: 1 }))
            }
          >
            <option value="">Todas las bodegas origen</option>
            {(bodegas ?? []).map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>

          <select
            value={filter.bodegaDestinoId ?? ''}
            onChange={(e) =>
              setFilter((f) => ({ ...f, bodegaDestinoId: e.target.value || undefined, pageNumber: 1 }))
            }
          >
            <option value="">Todas las bodegas destino</option>
            {(bodegas ?? []).map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
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
          <EmptyState message="No hay transferencias que coincidan con los filtros." />
        ) : (
          <>
            <table className="table trf-responsive-table">
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Origen</th>
                  <th>Destino</th>
                  <th>Fecha</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => (
                  <tr key={t.id} className="trf-row-clickable" onClick={() => navigate(`/inventario/transferencias/${t.id}`)}>
                    <td data-label="Número"><strong>{t.numeroTransferencia}</strong></td>
                    <td data-label="Origen">{t.bodegaOrigenNombre}</td>
                    <td data-label="Destino">{t.bodegaDestinoNombre}</td>
                    <td data-label="Fecha">{new Date(t.fechaTransferencia).toLocaleDateString()}</td>
                    <td data-label="Estado"><TransferenciaEstadoBadge estado={t.status} /></td>
                    <td data-label="Acciones" className="trf-cell-actions">
                      <ZHBtn variant="ghost" size="sm" onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/inventario/transferencias/${t.id}`);
                      }}>Ver</ZHBtn>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Paginación simple */}
            {total > (filter.pageSize ?? 20) && (
              <div className="trf-pagination">
                <ZHBtn
                  variant="ghost" size="sm"
                  disabled={(filter.pageNumber ?? 1) <= 1}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}
                >
                  Anterior
                </ZHBtn>
                <span className="trf-pagination-label">
                  Pág. {filter.pageNumber ?? 1} · {total} registros
                </span>
                <ZHBtn
                  variant="ghost" size="sm"
                  disabled={(filter.pageNumber ?? 1) * (filter.pageSize ?? 20) >= total}
                  onClick={() => setFilter((f) => ({ ...f, pageNumber: (f.pageNumber ?? 1) + 1 }))}
                >
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
