import { useState } from "react";
import { useI18n } from "../../../i18n/i18n";
import { Badge } from "../../../components/PageShell";
import { ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import type { CatalogStatusFilter } from "../pages/useEmployeeCatalogsPage";
import type { EmployeeCatalogEntry } from "../types/employee.types";

type Props = {
  items: EmployeeCatalogEntry[];
  loading: boolean;
  saving: boolean;
  canManage: boolean;
  search: string;
  status: CatalogStatusFilter;
  page: number;
  pageSize: number;
  totalCount: number;
  onSearch: (value: string) => void;
  onStatus: (value: CatalogStatusFilter) => void;
  onPage: (page: number) => void;
  onEdit: (entry: EmployeeCatalogEntry) => void;
  onSetActive: (entry: EmployeeCatalogEntry, active: boolean) => Promise<void>;
};

export function EmployeeCatalogListTab({
  items,
  loading,
  saving,
  canManage,
  search,
  status,
  page,
  pageSize,
  totalCount,
  onSearch,
  onStatus,
  onPage,
  onEdit,
  onSetActive,
}: Props) {
  const { t } = useI18n();
  const [confirm, setConfirm] = useState<EmployeeCatalogEntry | null>(null);
  const edit = t("common.edit", "Editar");
  const deactivate = t("common.deactivate", "Desactivar");
  const activate = t("common.activate", "Activar");

  const columns: ZHDataTableColumn<EmployeeCatalogEntry>[] = [
    { key: "name", header: t("employees.fields.name", "Nombre"), render: (row) => row.name },
    {
      key: "status",
      header: t("employees.fields.status", "Estado"),
      render: (row) => (
        <Badge
          size="md"
          variant={row.isActive ? "success" : "neutral"}
          label={row.isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
        />
      ),
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: t("employees.fields.actions", "Acciones"),
            align: "right" as const,
            render: (row: EmployeeCatalogEntry) => (
              <div className="prd-actions-cell">
                <ZHIconButton
                  icon="edit"
                  variant="ghost"
                  title={edit}
                  ariaLabel={`${edit} ${row.name}`}
                  disabled={saving}
                  onClick={() => onEdit(row)}
                />
                <ZHIconButton
                  icon={row.isActive ? "block" : "check_circle"}
                  variant={row.isActive ? "danger" : "success"}
                  title={row.isActive ? deactivate : activate}
                  ariaLabel={`${row.isActive ? deactivate : activate} ${row.name}`}
                  disabled={saving}
                  onClick={() => setConfirm(row)}
                />
              </div>
            ),
          },
        ]
      : []),
  ];

  return (
    <div className="zh-stack prd-fadein">
      <ZHFilterBar plain>
        <ZHField label={t("employees.filters.search", "Buscar")} density="compact">
          <ZhTextInput
            aria-label={t("employees.filters.search", "Buscar")}
            placeholder={t("employees.catalogs.searchPlaceholder", "Nombre")}
            maxLength={150}
            value={search}
            onChange={(e) => onSearch(e.target.value)}
          />
        </ZHField>
        <ZHField label={t("employees.fields.status", "Estado")} density="compact">
          <ZhSelect
            aria-label={t("employees.fields.status", "Estado")}
            value={status}
            onChange={(e) => onStatus(e.target.value as CatalogStatusFilter)}
          >
            <option value="">{t("employees.filters.all", "Todos")}</option>
            <option value="active">{t("common.active", "Activo")}</option>
            <option value="inactive">{t("common.inactive", "Inactivo")}</option>
          </ZhSelect>
        </ZHField>
      </ZHFilterBar>

      <ZHDataTable
        columns={columns}
        rows={items}
        rowKey={(row) => row.id}
        loading={loading}
        emptyMessage={t("employees.catalogs.empty", "No hay registros en este catálogo.")}
        rowClassName={(row) => (row.isActive ? undefined : "prd-row--inactive")}
        page={page}
        pageSize={pageSize}
        total={totalCount}
        onPageChange={onPage}
      />

      <ZHConfirmModal
        open={!!confirm}
        variant={confirm?.isActive ? "danger" : "default"}
        title={confirm?.isActive ? deactivate : activate}
        message={
          <p className="zh-confirm-message">
            {confirm?.isActive
              ? t(
                  "employees.catalogs.deactivateWarning",
                  "Dejará de estar disponible para nuevas asignaciones. Las fichas que ya lo usan lo conservan.",
                )
              : t("employees.catalogs.activateWarning", "Volverá a estar disponible para nuevas asignaciones.")}{" "}
            <strong>{confirm?.name}</strong>
          </p>
        }
        confirmLabel={confirm?.isActive ? deactivate : activate}
        cancelLabel={t("common.cancel", "Cancelar")}
        onConfirm={() => {
          const row = confirm;
          setConfirm(null);
          if (row) void onSetActive(row, !row.isActive);
        }}
        onCancel={() => setConfirm(null)}
      />
    </div>
  );
}
