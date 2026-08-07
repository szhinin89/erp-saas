import { Link } from "react-router-dom";
import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../components/zh/inputs";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
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
          <ReportKpiCard
            layout="horizontal"
            icon="warehouse"
            tone="primary"
            label="Total Sucursales"
            value={String(totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="task_alt"
            tone="success"
            label="Activas"
            value={String(totals.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="star"
            tone="secondary"
            label="Principal"
            value={String(totals.main)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label="Inactivas"
            value={String(totals.inactive)}
          />
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
              <ZhTextInput
                placeholder="Buscar por nombre, cÃ³digo o encargado..."
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
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>CÃ³digo</th>
                  <th>Nombre</th>
                  <th>DirecciÃ³n</th>
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
                        label={row.code ?? "â€”"}
                        variant="neutral"
                        size="md"
                        className="mono"
                      />
                    </td>
                    <td>
                      <div className="br-list-name">{row.name}</div>
                      {row.isMainBranch && (
                        <div className="br-list-sub">
                          <Badge label="Principal" variant="info" size="md" />
                        </div>
                      )}
                    </td>
                    <td>
                      <div className="br-list-contact">
                        {row.address || <span className="subtle">â€”</span>}
                      </div>
                    </td>
                    <td>
                      {row.managerName ?? <span className="subtle">â€”</span>}
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
              Ãšltima carga: {new Date().toTimeString().slice(0, 8)}
            </p>
          )}
        </div>
      </div>
    </>
  );
}

