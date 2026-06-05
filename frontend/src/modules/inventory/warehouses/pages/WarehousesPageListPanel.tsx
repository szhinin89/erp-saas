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
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      ) : items.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message={t('common.noData')} />
        </div>
      ) : filtered.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message="No se encontraron resultados." />
        </div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Código</th>
                <th>Bodega</th>
                <th>Sucursal</th>
                <th>Encargado</th>
                <th>Capacidad</th>
                <th>Estado</th>
                {canUpdate || canDelete ? <th className="pg-th-right">Acciones</th> : null}
              </tr>
            </thead>
            <tbody>
              {filtered.map((row) => (
                <tr key={row.id} className={row.isActive ? undefined : 'pg-row-inactive'}>
                  <td>
                    <span className="badge badge--gray badge--md mono">{row.code ?? '—'}</span>
                  </td>
                  <td>
                    <div className="bod-list-name">{row.name}</div>
                    {row.storageType && (
                      <div className="bod-list-sub">
                        {row.storageType}
                      </div>
                    )}
                  </td>
                  <td className="bod-list-branch">
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
                    <td className="pg-td-right">
                      <div className="bod-actions-tight">
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
        <p className="subtle bod-list-footer-note">
          {filtered.length} bodegas
        </p>
        {items.length > 0 && (
          <p className="pg-table-timestamp">Última carga: {new Date().toLocaleTimeString('es')}</p>
        )}
      </div>
    </div>
  );
}
