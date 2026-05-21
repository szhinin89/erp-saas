import { EmptyState, LoadingState } from '../../../components/PageShell';
import type { BranchesPageContext } from '../hooks/useBranchesPage';

type Props = Pick<
  BranchesPageContext,
  | 't'
  | 'loading'
  | 'items'
  | 'totals'
  | 'search'
  | 'setSearch'
  | 'filtered'
  | 'canUpdate'
  | 'canDelete'
  | 'openEditModal'
  | 'toggleDisable'
>;

export function BranchesListSection({
  t,
  loading,
  items,
  totals,
  search,
  setSearch,
  filtered,
  canUpdate,
  canDelete,
  openEditModal,
  toggleDisable,
}: Props) {
  return (
    <>
      {!loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">warehouse</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Sucursales</p>
              <p className="pg-kpi-value">{totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--success">
              <span className="material-symbols-outlined">task_alt</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Activas</p>
              <p className="pg-kpi-value">{totals.active}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">star</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Sucursal Principal</p>
              <p className="pg-kpi-value">{totals.main}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivas</p>
              <p className="pg-kpi-value">{totals.inactive}</p>
            </div>
          </div>
        </div>
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">business</span>
            <span className="pg-section-label">Sucursales Registradas</span>
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
                  <th>Sucursal</th>
                  <th>Encargado</th>
                  <th>Contacto</th>
                  <th>Principal</th>
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
                      {row.branchType && (
                        <div style={{ fontSize: 'var(--text-label-sm-size)', color: 'var(--color-text-secondary)' }}>
                          {row.branchType}
                        </div>
                      )}
                    </td>
                    <td>{row.managerName ?? <span className="subtle">—</span>}</td>
                    <td>
                      <div style={{ fontSize: 'var(--text-body-sm-size)', color: 'var(--color-text-secondary)' }}>
                        {row.phones ?? ''}
                        {row.email && row.phones ? ' · ' : ''}
                        {row.email ?? ''}
                        {!row.phones && !row.email && <span className="subtle">—</span>}
                      </div>
                    </td>
                    <td>
                      {row.isMainBranch ? (
                        <span className="badge badge--blue badge--md">Principal</span>
                      ) : (
                        <span className="subtle" style={{ fontSize: 'var(--text-body-sm-size)' }}>
                          —
                        </span>
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
                              onClick={() => void openEditModal(row.id)}
                            >
                              <span className="material-symbols-outlined">edit</span>
                            </button>
                          )}
                          {(row.isActive ? canDelete : canUpdate) && (
                            <button
                              type="button"
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              title={row.isActive ? 'Desactivar' : 'Activar'}
                              onClick={() => void toggleDisable(row)}
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
            {filtered.length} sucursales
          </p>
          {items.length > 0 && (
            <p className="pg-table-timestamp">Última carga: {new Date().toLocaleTimeString('es')}</p>
          )}
        </div>
      </div>
    </>
  );
}
