import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import type { EstablishmentsPageContext } from "../hooks/useEstablishmentsPage";

type Props = Pick<
  EstablishmentsPageContext,
  | "loading"
  | "items"
  | "totals"
  | "search"
  | "setSearch"
  | "filtered"
  | "canUpdate"
  | "canDisable"
  | "canCreate"
  | "selectedId"
  | "openEdit"
  | "toggleDisable"
  | "openCreate"
  | "fetchList"
  | "activeStatus"
  | "setActiveStatus"
>;

export function EstablishmentsListSection({
  loading,
  items,
  totals,
  search,
  setSearch,
  filtered,
  canUpdate,
  canDisable,
  canCreate,
  selectedId,
  openEdit,
  toggleDisable,
  openCreate,
  fetchList,
  activeStatus,
  setActiveStatus,
}: Props) {
  return (
    <>
      {!loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">receipt_long</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total</p>
              <p className="pg-kpi-value">{totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">check_circle</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Activos</p>
              <p className="pg-kpi-value">{totals.active}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">star</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Principal</p>
              <p className="pg-kpi-value">{totals.main}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivos</p>
              <p className="pg-kpi-value">{totals.inactive}</p>
            </div>
          </div>
        </div>
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              receipt_long
            </span>
            <span className="pg-section-label">Establecimientos SRI</span>
          </div>
          <div className="br-actions-tight">
            <ZHBtn
              variant="secondary"
              size="sm"
              type="button"
              disabled={loading}
              onClick={() => void fetchList()}
            >
              <span className="material-symbols-outlined">refresh</span>
              Actualizar
            </ZHBtn>
            {canCreate && (
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={() => void openCreate()}
              >
                <span className="material-symbols-outlined">add</span>
                Nuevo Establecimiento
              </ZHBtn>
            )}
          </div>
        </div>

        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Buscar por código, nombre, dirección o sucursal..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                disabled={loading}
              />
            </div>
            <select
              className="zh-input"
              value={activeStatus}
              onChange={(e) =>
                setActiveStatus(e.target.value as "all" | "active" | "inactive")
              }
              disabled={loading}
            >
              <option value="active">Solo activos</option>
              <option value="inactive">Solo inactivos</option>
              <option value="all">Todos</option>
            </select>
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
            <EmptyState message="No hay establecimientos registrados." />
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
                  <th>Código SRI</th>
                  <th>Nombre</th>
                  <th>Dirección fiscal</th>
                  <th>Sucursal</th>
                  <th>P. Emisión</th>
                  <th>Estado</th>
                  {(canUpdate || canDisable) && (
                    <th className="pg-th-right">Acciones</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr
                    key={row.id}
                    className={
                      [
                        row.isActive ? undefined : "pg-row-inactive",
                        row.id === selectedId ? "cfg-row--selected" : undefined,
                      ]
                        .filter(Boolean)
                        .join(" ") || undefined
                    }
                  >
                    <td>
                      <Badge
                        label={row.code}
                        variant="gray"
                        size="md"
                        className="mono"
                      />
                      {row.isMain && (
                        <Badge
                          label="Principal"
                          variant="blue"
                          size="md"
                          style={{ marginLeft: "var(--space-1)" }}
                        />
                      )}
                    </td>
                    <td>
                      <div className="br-list-name">{row.name}</div>
                      {row.phone && (
                        <div className="br-list-sub">{row.phone}</div>
                      )}
                    </td>
                    <td>
                      <div className="br-list-contact">{row.address}</div>
                    </td>
                    <td>
                      {row.branchName ? (
                        <div className="br-list-name">{row.branchName}</div>
                      ) : (
                        <span className="subtle">—</span>
                      )}
                    </td>
                    <td>
                      <Badge
                        label={row.emissionPointCount}
                        variant="gray"
                        size="md"
                      />
                    </td>
                    <td>
                      <span
                        className={
                          row.isActive
                            ? "zh-status zh-status--active"
                            : "zh-status zh-status--inactive"
                        }
                      >
                        {row.isActive ? "Activo" : "Inactivo"}
                      </span>
                    </td>
                    {(canUpdate || canDisable) && (
                      <td className="pg-td-right">
                        <div className="br-actions-tight">
                          {row.isActive && canUpdate && (
                            <ZHBtn
                              type="button"
                              variant="ghost"
                              size="sm"
                              title="Editar"
                              onClick={() => void openEdit(row)}
                            >
                              <span className="material-symbols-outlined">
                                edit
                              </span>
                            </ZHBtn>
                          )}
                          {(row.isActive ? canDisable : canUpdate) && (
                            <ZHBtn
                              type="button"
                              variant="ghost"
                              size="sm"
                              title={row.isActive ? "Desactivar" : "Activar"}
                              onClick={() => void toggleDisable(row)}
                            >
                              <span className="material-symbols-outlined">
                                {row.isActive ? "block" : "check_circle"}
                              </span>
                            </ZHBtn>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pg-table-footer">
          <p className="subtle br-list-footer-note">
            {filtered.length} establecimientos
          </p>
        </div>
      </div>
    </>
  );
}
