import { EmptyState, LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { EMISSION_TYPE_ELECTRONIC } from "../api/emissionPointsService";
import type { EmissionPointsPageContext } from "../hooks/useEmissionPointsPage";

type Props = Pick<
  EmissionPointsPageContext,
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

export function EmissionPointsListSection({
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
  const { t } = useI18n();

  return (
    <>
      {!loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">point_of_sale</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Puntos</p>
              <p className="pg-kpi-value">{totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">bolt</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">ElectrÃ³nicos</p>
              <p className="pg-kpi-value">{totals.electronic}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">print</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">FÃ­sicos</p>
              <p className="pg-kpi-value">{totals.physical}</p>
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
              point_of_sale
            </span>
            <span className="pg-section-label">
              Puntos de EmisiÃ³n Registrados
            </span>
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
                onClick={() => void openCreate()}
              >
                <span className="material-symbols-outlined">add</span>
                Nuevo Punto
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
                placeholder="Buscar por cÃ³digo, nombre o establecimiento..."
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
                  <th>CÃ³digo</th>
                  <th>Nombre</th>
                  <th>Sucursal</th>
                  <th>Tipo de emisiÃ³n</th>
                  <th>Estado</th>
                  <th>Fecha</th>
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
                        label={row.code}
                        variant="neutral"
                        size="md"
                        className="mono"
                      />
                    </td>
                    <td>
                      <div className="br-list-name">
                        {row.name ?? <span className="subtle">â€”</span>}
                      </div>
                      {row.isDefault && (
                        <div className="br-list-sub">
                          <Badge label="Por defecto" variant="info" size="md" />
                        </div>
                      )}
                    </td>
                    <td>
                      <div className="br-list-name">
                        {row.establishmentName}
                      </div>
                      <div className="br-list-sub mono">
                        {row.establishmentCode}
                      </div>
                    </td>
                    <td>
                      <Badge
                        variant={
                          row.emissionType === EMISSION_TYPE_ELECTRONIC
                            ? "blue"
                            : "gray"
                        }
                        size="md"
                        label={
                          row.emissionType === EMISSION_TYPE_ELECTRONIC
                            ? "ElectrÃ³nico"
                            : "FÃ­sico"
                        }
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
                        {row.isActive
                          ? t("common.active")
                          : t("common.inactive")}
                      </span>
                    </td>
                    <td>
                      <span className="br-list-contact">
                        {formatDate(row.createdAt)}
                      </span>
                    </td>
                    {canUpdate || canDelete ? (
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
            {filtered.length} puntos de emisiÃ³n
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

