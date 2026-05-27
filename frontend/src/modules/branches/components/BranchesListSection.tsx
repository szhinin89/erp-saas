import { Link } from 'react-router-dom';
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
  | 'canCreate'
  | 'openEditModal'
  | 'toggleDisable'
  | 'openCreateModal'
  | 'fetchList'
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
  canCreate,
  openEditModal,
  toggleDisable,
  openCreateModal,
  fetchList,
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
          <div className="br-actions-tight">
            <button
              className="zh-btn zh-btn--secondary zh-btn--sm"
              type="button"
              disabled={loading}
              onClick={() => void fetchList()}
            >
              <span className="material-symbols-outlined">refresh</span>
              {t('common.refresh')}
            </button>
            {canCreate && (
              <button className="zh-btn zh-btn--primary zh-btn--sm" type="button" onClick={openCreateModal}>
                <span className="material-symbols-outlined">add</span>
                {t('branches.list.newAction')}
              </button>
            )}
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
                  <th>Sucursal</th>
                  <th>Encargado</th>
                  <th>Contacto</th>
                  <th>Principal</th>
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
                      <div className="br-list-name">{row.name}</div>
                      {row.branchType && (
                        <div className="br-list-sub">
                          {row.branchType}
                        </div>
                      )}
                    </td>
                    <td>{row.managerName ?? <span className="subtle">—</span>}</td>
                    <td>
                      <div className="br-list-contact">
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
                        <span className="subtle br-list-contact">
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
                      <td className="pg-td-right">
                        <div className="br-actions-tight">
                          <Link
                            to={`/settings/branches/${row.id}`}
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title="Ver detalle"
                          >
                            <span className="material-symbols-outlined">open_in_new</span>
                          </Link>
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
          <p className="subtle br-list-footer-note">
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
