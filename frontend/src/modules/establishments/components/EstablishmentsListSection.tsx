import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
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
          <ReportKpiCard
            layout="horizontal"
            icon="receipt_long"
            tone="primary"
            label="Total"
            value={String(totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label="Activos"
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
            label="Inactivos"
            value={String(totals.inactive)}
          />
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
              <ZhTextInput
                placeholder="Buscar por código, nombre, dirección o sucursal..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                disabled={loading}
              />
            </div>
            <ZhSelect
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
            </ZhSelect>
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
          <div className="table-scroll">
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
                        variant="neutral"
                        size="md"
                        className="mono"
                      />
                      {row.isMain && (
                        <Badge
                          label="Principal"
                          variant="info"
                          size="md"
                          className="zh-ml-1"
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
                        variant="neutral"
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

