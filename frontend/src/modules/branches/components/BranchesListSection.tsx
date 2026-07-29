import { Link } from "react-router-dom";
import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import type { BranchesPageContext } from "../hooks/useBranchesPage";

type Props = Pick<
  BranchesPageContext,
  | "t"
  | "loading"
  | "items"
  | "totals"
  | "search"
  | "setSearch"
  | "filtered"
  | "canUpdate"
  | "canDelete"
  | "canCreate"
  | "selectedId"
  | "openEdit"
  | "toggleDisable"
  | "openCreate"
  | "fetchList"
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
  selectedId,
  openEdit,
  toggleDisable,
  openCreate,
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
              <p className="pg-kpi-label">Principal</p>
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
            <span className="material-symbols-outlined pg-section-icon">
              business
            </span>
            <span className="pg-section-label">Sucursales Registradas</span>
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
              {t("common.refresh")}
            </ZHBtn>
            {canCreate && (
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={openCreate}
              >
                <span className="material-symbols-outlined">add</span>
                {t("branches.list.newAction")}
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
            <EmptyState message={t("common.noData")} />
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
                  <th>Nombre</th>
                  <th>Dirección</th>
                  <th>Responsable</th>
                  <th>Estado</th>
                  {canUpdate || canDelete ? (
                    <th className="pg-th-right">Acciones</th>
                  ) : null}
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
                        label={row.code ?? "—"}
                        variant="gray"
                        size="md"
                        className="mono"
                      />
                    </td>
                    <td>
                      <div className="br-list-name">{row.name}</div>
                      {row.isMainBranch && (
                        <div className="br-list-sub">
                          <Badge label="Principal" variant="blue" size="md" />
                        </div>
                      )}
                    </td>
                    <td>
                      <div className="br-list-contact">
                        {row.address || <span className="subtle">—</span>}
                      </div>
                    </td>
                    <td>
                      {row.managerName ?? <span className="subtle">—</span>}
                    </td>
                    <td>
                      <span
                        className={
                          row.isActive
                            ? "zh-status zh-status--active"
                            : "zh-status zh-status--inactive"
                        }
                      >
                        {row.isActive
                          ? t("common.active")
                          : t("common.inactive")}
                      </span>
                    </td>
                    {canUpdate || canDelete ? (
                      <td className="pg-td-right">
                        <div className="br-actions-tight">
                          <Link
                            to={`/settings/branches/${row.id}`}
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title="Ver detalle completo"
                          >
                            <span className="material-symbols-outlined">
                              open_in_new
                            </span>
                          </Link>
                          {canUpdate && (
                            <ZHBtn
                              type="button"
                              variant="ghost"
                              size="sm"
                              title="Editar"
                              onClick={() => void openEdit(row.id)}
                            >
                              <span className="material-symbols-outlined">
                                edit
                              </span>
                            </ZHBtn>
                          )}
                          {(row.isActive ? canDelete : canUpdate) && (
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
            <p className="pg-table-timestamp">
              Última carga: {new Date().toTimeString().slice(0, 8)}
            </p>
          )}
        </div>
      </div>
    </>
  );
}
