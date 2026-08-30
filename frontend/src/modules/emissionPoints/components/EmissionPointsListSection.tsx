import { EmptyState, LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../components/zh/inputs";
import { Badge } from "../../../components/PageShell";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { EMISSION_TYPE_ELECTRONIC } from "../api/emissionPointsService";
import type { EmissionPointsPageContext } from "../hooks/useEmissionPointsPage";

type EmissionPointRow = EmissionPointsPageContext["filtered"][number];

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

  const emissionPointColumns: ZHDataTableColumn<EmissionPointRow>[] = [
    { key: "code", header: "Código", render: (row) => <Badge label={row.code} variant="neutral" size="md" className="mono" /> },
    {
      key: "name",
      header: "Nombre",
      render: (row) => (
        <>
          <div className="br-list-name">{row.name ?? <span className="subtle">—</span>}</div>
          {row.isDefault && (
            <div className="br-list-sub">
              <Badge label="Por defecto" variant="info" size="md" />
            </div>
          )}
        </>
      ),
    },
    {
      key: "establishment",
      header: "Sucursal",
      render: (row) => (
        <>
          <div className="br-list-name">{row.establishmentName}</div>
          <div className="br-list-sub mono">{row.establishmentCode}</div>
        </>
      ),
    },
    {
      key: "emissionType",
      header: "Tipo de emisión",
      render: (row) => (
        <Badge
          variant={row.emissionType === EMISSION_TYPE_ELECTRONIC ? "blue" : "gray"}
          size="md"
          label={row.emissionType === EMISSION_TYPE_ELECTRONIC ? "Electrónico" : "Físico"}
        />
      ),
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <span className={row.isActive ? "zh-status zh-status--active" : "zh-status zh-status--inactive"}>
          {row.isActive ? t("common.active") : t("common.inactive")}
        </span>
      ),
    },
    { key: "createdAt", header: "Fecha", render: (row) => <span className="br-list-contact">{formatDate(row.createdAt)}</span> },
    ...(canUpdate || canDelete
      ? [
          {
            key: "actions",
            header: "Acciones",
            align: "right" as const,
            render: (row: EmissionPointRow) => (
              <div className="br-actions-tight">
                {row.isActive && canUpdate && (
                  <ZHBtn type="button" variant="ghost" size="sm" title="Editar" onClick={() => void openEdit(row)}>
                    <span className="material-symbols-outlined">edit</span>
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
            ),
          },
        ]
      : []),
  ];

  return (
    <>
      {!loading && (
        <div className="pg-kpis">
          <ReportKpiCard
            layout="horizontal"
            icon="point_of_sale"
            tone="primary"
            label="Total Puntos"
            value={String(totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="bolt"
            tone="primary"
            label="Electrónicos"
            value={String(totals.electronic)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="print"
            tone="secondary"
            label="Físicos"
            value={String(totals.physical)}
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
              point_of_sale
            </span>
            <span className="pg-section-label">
              Puntos de Emisión Registrados
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
              <ZhTextInput
                placeholder="Buscar por código, nombre o establecimiento..."
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
          <ZHDataTable
            columns={emissionPointColumns}
            rows={filtered}
            rowKey={(row) => row.id}
            showRowNumber
            rowClassName={(row) =>
              [
                row.isActive ? undefined : "pg-row-inactive",
                row.id === selectedId ? "cfg-row--selected" : undefined,
              ]
                .filter(Boolean)
                .join(" ") || undefined
            }
          />
        )}

        <div className="pg-table-footer">
          <p className="subtle br-list-footer-note">
            {filtered.length} puntos de emisión
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

