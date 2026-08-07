import type { FieldValues } from "react-hook-form";
import { EmptyState, LoadingState } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import type { CatalogCrudContext } from "../hooks/useCatalogCrud";

interface Props<
  TDto extends { isActive: boolean; name: string },
  TForm extends FieldValues,
> {
  ctx: CatalogCrudContext<TDto, TForm>;
  title: string;
  icon: string;
  createLabel: string;
  searchPlaceholder: string;
  columns: {
    key: string;
    label: string;
    render: (item: Record<string, unknown>) => React.ReactNode;
  }[];
}

export function CatalogListSection<
  TDto extends { isActive: boolean; name: string },
  TForm extends FieldValues,
>({
  ctx,
  title,
  icon,
  createLabel,
  searchPlaceholder,
  columns,
}: Props<TDto, TForm>) {
  const { t } = useI18n();
  const {
    loading,
    items,
    filtered,
    totals,
    search,
    setSearch,
    canCreate,
    canUpdate,
    canDelete,
    openCreateModal,
    openEditModal,
    toggleDisable,
    fetchList,
  } = ctx;

  return (
    <>
      {!loading && (
        <div className="pg-kpis">
          <ReportKpiCard
            layout="horizontal"
            icon={icon}
            tone="primary"
            label="Total"
            value={String(totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label={t("common.active", "Activos")}
            value={String(totals.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label={t("common.inactive", "Inactivos")}
            value={String(totals.inactive)}
          />
        </div>
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              {icon}
            </span>
            <span className="pg-section-label">{title}</span>
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
              {t("common.refresh", "Actualizar")}
            </ZHBtn>
            {canCreate && (
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={openCreateModal}
              >
                <span className="material-symbols-outlined">add</span>
                {createLabel}
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
                placeholder={searchPlaceholder}
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
            <EmptyState message={t("common.noData", "No hay datos.")} />
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
                  {columns.map((col) => (
                    <th key={col.key}>{col.label}</th>
                  ))}
                  <th>{t("common.status", "Estado")}</th>
                  {(canUpdate || canDelete) && (
                    <th className="pg-th-right">
                      {t("common.actions", "Acciones")}
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {filtered.map((row: Record<string, unknown>) => {
                  const isActive = row.isActive as boolean;
                  return (
                    <tr
                      key={row.id as string}
                      className={isActive ? undefined : "pg-row-inactive"}
                    >
                      {columns.map((col) => (
                        <td key={col.key}>{col.render(row)}</td>
                      ))}
                      <td>
                        <span
                          className={
                            isActive
                              ? "zh-status zh-status--active"
                              : "zh-status zh-status--inactive"
                          }
                        >
                          {isActive
                            ? t("common.active", "Activo")
                            : t("common.inactive", "Inactivo")}
                        </span>
                      </td>
                      {(canUpdate || canDelete) && (
                        <td className="pg-td-right">
                          <div className="br-actions-tight">
                            {isActive && canUpdate && (
                              <ZHBtn
                                type="button"
                                variant="ghost"
                                size="sm"
                                title="Editar"
                                onClick={() => openEditModal(row as never)}
                              >
                                <span className="material-symbols-outlined">
                                  edit
                                </span>
                              </ZHBtn>
                            )}
                            {(isActive ? canDelete : canUpdate) && (
                              <ZHBtn
                                type="button"
                                variant="ghost"
                                size="sm"
                                title={isActive ? "Desactivar" : "Activar"}
                                onClick={() => void toggleDisable(row as never)}
                              >
                                <span className="material-symbols-outlined">
                                  {isActive ? "block" : "check_circle"}
                                </span>
                              </ZHBtn>
                            )}
                          </div>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <div className="pg-table-footer">
          <p className="subtle br-list-footer-note">
            {filtered.length} registros
          </p>
        </div>
      </div>
    </>
  );
}
