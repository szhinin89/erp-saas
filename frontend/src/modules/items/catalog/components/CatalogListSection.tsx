import type { FieldValues } from "react-hook-form";
import { EmptyState, LoadingState } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
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

  const tableColumns: ZHDataTableColumn<Record<string, unknown>>[] = [
    ...columns.map((col) => ({ key: col.key, header: col.label, render: col.render })),
    {
      key: "status",
      header: t("common.status", "Estado"),
      render: (row) => {
        const isActive = row.isActive as boolean;
        return (
          <span className={isActive ? "zh-status zh-status--active" : "zh-status zh-status--inactive"}>
            {isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
          </span>
        );
      },
    },
    ...(canUpdate || canDelete
      ? [
          {
            key: "actions",
            header: t("common.actions", "Acciones"),
            align: "right" as const,
            render: (row: Record<string, unknown>) => {
              const isActive = row.isActive as boolean;
              return (
                <div className="br-actions-tight">
                  {isActive && canUpdate && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      title={t("common.edit", "Editar")}
                      onClick={() => openEditModal(row as never)}
                    >
                      <span className="material-symbols-outlined">edit</span>
                    </ZHBtn>
                  )}
                  {(isActive ? canDelete : canUpdate) && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      title={isActive ? t("common.deactivate", "Desactivar") : t("common.activate", "Activar")}
                      onClick={() => void toggleDisable(row as never)}
                    >
                      <span className="material-symbols-outlined">
                        {isActive ? "block" : "check_circle"}
                      </span>
                    </ZHBtn>
                  )}
                </div>
              );
            },
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
            icon={icon}
            tone="primary"
            label={t("common.total", "Total")}
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
              <ZhTextInput
                placeholder={searchPlaceholder}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>
          <div className="pg-table-controls-right">
            <span>
              {t("common.showingCount", {
                count: filtered.length,
                total: items.length,
              })}
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
            <EmptyState
              message={t(
                "common.noSearchResults",
                "No se encontraron resultados.",
              )}
            />
          </div>
        ) : (
          <ZHDataTable
            columns={tableColumns}
            rows={filtered as unknown as Record<string, unknown>[]}
            rowKey={(row) => row.id as string}
            showRowNumber
            rowClassName={(row) => ((row.isActive as boolean) ? undefined : "pg-row-inactive")}
          />
        )}

        <div className="pg-table-footer">
          <p className="subtle br-list-footer-note">
            {t("common.recordsCount", { count: filtered.length })}
          </p>
        </div>
      </div>
    </>
  );
}
