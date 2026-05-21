import { EmptyState, LoadingState } from '../../../../components/PageShell';
import type { WarehouseDto } from '../api/warehouseService';

type BodegasPageListPanelProps = {
  t: (key: string) => string;
  loading: boolean;
  items: WarehouseDto[];
  filtered: WarehouseDto[];
  search: string;
  setSearch: (value: string) => void;
  canUpdate: boolean;
  canDelete: boolean;
  branchName: (id: string) => string;
  onEdit: (id: string) => void;
  onToggleStatus: (row: WarehouseDto) => void;
};

export function BodegasPageListPanel({
  t,
  loading,
  items,
  filtered,
  search,
  setSearch,
  canUpdate,
  canDelete,
  branchName,
  onEdit,
  onToggleStatus,
}: BodegasPageListPanelProps) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">warehouse</span>
          <span className="pg-section-label">Bodegas Registradas</span>
        </div>
      </div>

      <div className="pg-table-controls">
        <div className="pg-table-controls-left">
          <div className="pg-search">
            <span className="material-symbols-outlined">search</span>
            <input
              type="text"
              placeholder="Buscar por nombre, código o encargado..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              disabled={loading}
            />
          </div>
        </div>
        <div className="pg-table-controls-right">
          <span>
            Mostrando {filtered.length} de {items.length}
          </span>
        </div>
      </div>

      {loading ? (
        <div style={{ padding: '40px' }}>
          <LoadingState />
        </div>
      ) : items.length === 0 ? (
        <div style={{ padding: '40px' }}>
          <EmptyState message={t('common.noData')} />
        </div>
      ) : filtered.length === 0 ? (
        <div style={{ padding: '40px' }}>
          <EmptyState message="No se encontraron resultados." />
        </div>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th>Código</th>
                <th>Bodega</th>
                <th>Sucursal</th>
                <th>Encargado</th>
                <th>Capacidad</th>
                <th>Estado</th>
                {canUpdate || canDelete ? <th style={{ textAlign: 'right' }}>Acciones</th> : null}
              </tr>
            </thead>
            <tbody>
              {filtered.map((row) => (
                <tr key={row.id} style={{ opacity: row.isActive ? 1 : 0.65 }}>
                  <td>
                    <span className="badge badge--gray badge--md mono">{row.code ?? '—'}</span>
                  </td>
                  <td>
                    <div style={{ fontWeight: 500, color: 'var(--color-text-primary)' }}>{row.name}</div>
                    {row.storageType && (
                      <div style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)' }}>
                        {row.storageType}
                      </div>
                    )}
                  </td>
                  <td style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-body-sm-size)' }}>
                    {branchName(row.branchId)}
                  </td>
                  <td>{row.manager ?? <span className="subtle">—</span>}</td>
                  <td>
                    {row.capacity ? (
                      <span className="mono subtle">{row.capacity.toLocaleString('es')} m³</span>
                    ) : (
                      <span className="subtle">—</span>
                    )}
                  </td>
                  <td>
                    <span className={row.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                      {row.isActive ? t('common.active') : t('common.inactive')}
                    </span>
                  </td>
                  {canUpdate || canDelete ? (
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'flex', gap: 'var(--space-1)', justifyContent: 'flex-end' }}>
                        {canUpdate && (
                          <button
                            type="button"
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title="Editar"
                            onClick={() => void onEdit(row.id)}
                          >
                            <span className="material-symbols-outlined">edit</span>
                          </button>
                        )}
                        {(row.isActive ? canDelete : canUpdate) && (
                          <button
                            type="button"
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title={row.isActive ? 'Desactivar' : 'Activar'}
                            onClick={() => void onToggleStatus(row)}
                          >
                            <span className="material-symbols-outlined">
                              {row.isActive ? 'block' : 'check_circle'}
                            </span>
                          </button>
                        )}
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="pg-table-footer">
        <p className="subtle" style={{ fontSize: 12, margin: 0 }}>
          {filtered.length} bodegas
        </p>
        {items.length > 0 && (
          <p className="pg-table-timestamp">Última carga: {new Date().toLocaleTimeString('es')}</p>
        )}
      </div>
    </div>
  );
}
