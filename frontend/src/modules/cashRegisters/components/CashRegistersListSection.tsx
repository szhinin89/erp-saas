import { EmptyState, LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../components/zh/inputs";
import { Badge } from "../../../components/PageShell";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import type { CashRegistersPageContext } from "../hooks/useCashRegistersPage";

type Props = Pick<
  CashRegistersPageContext,
  | "loading"
  | "items"
  | "totals"
  | "search"
  | "setSearch"
  | "filtered"
  | "canManage"
  | "selectedId"
  | "openEdit"
  | "toggleDisable"
  | "openCreate"
  | "fetchList"
>;

export function CashRegistersListSection({
  loading,
  items,
  totals,
  search,
  setSearch,
  filtered,
  canManage,
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
          <ReportKpiCard
            layout="horizontal"
            icon="point_of_sale"
            tone="primary"
            label="Total Cajas"
            value={String(totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label="Activas"
            value={String(totals.active)}
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
              point_of_sale
            </span>
            <span className="pg-section-label">Cajas Registradas</span>
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
            {canManage && (
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={() => void openCreate()}
              >
                <span className="material-symbols-outlined">add</span>
                Nueva Caja
              </ZHBtn>
            )}
          </div>
        </div>

        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <ZhTextInput
                placeholder="Buscar por código, nombre o sucursal..."
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
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Sucursal</th>
                  <th>Establecimiento / Pto. Emisión</th>
                  <th>Estado</th>
                  <th>Fecha</th>
                  {canManage ? <th className="pg-th-right">Acciones</th> : null}
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
                      <div className="br-list-name">{row.name}</div>
                      {row.hasHistory && (
                        <div className="br-list-sub">
                          <Badge
                            label="Con historial"
                            variant="info"
                            size="md"
                          />
                        </div>
                      )}
                    </td>
                    <td>
                      <div className="br-list-name">{row.branchName}</div>
                      {row.branchCode && (
                        <div className="br-list-sub mono">{row.branchCode}</div>
                      )}
                    </td>
                    <td>
                      {row.emissionPointCode ? (
                        <>
                          <div className="br-list-name mono">
                            {row.establishmentCode}-{row.emissionPointCode}
                          </div>
                          <div className="br-list-sub">
                            {row.emissionPointName ?? "—"}
                          </div>
                        </>
                      ) : (
                        <span className="subtle">Sin configurar</span>
                      )}
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
                    {canManage ? (
                      <td className="pg-td-right">
                        <div className="br-actions-tight">
                          {row.isActive && (
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
          <p className="subtle br-list-footer-note">{filtered.length} cajas</p>
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

