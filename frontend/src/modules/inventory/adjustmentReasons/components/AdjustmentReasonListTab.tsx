import { useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { Badge, EmptyState, LoadingState } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { ZHConfirmModal } from "../../../../components/zh/ZHConfirmModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import type { InventoryAdjustmentReasonDto } from "../types";

type Props = {
  reasons: InventoryAdjustmentReasonDto[];
  loading: boolean;
  toggling: boolean;
  canManage: boolean;
  onEdit: (row: InventoryAdjustmentReasonDto) => void;
  onToggle: (row: InventoryAdjustmentReasonDto) => Promise<void>;
};

/**
 * Lista del catálogo de motivos de ajuste — mismo armazón que `WarehouseListadoTab`
 * (tabla `.table` global, `Badge`, `ZHIconButton`, confirmación con `ZHConfirmModal`),
 * sin buscador porque el catálogo es corto y ya viene ordenado por `sortOrder`.
 */
export function AdjustmentReasonListTab({
  reasons,
  loading,
  toggling,
  canManage,
  onEdit,
  onToggle,
}: Props) {
  const { t } = useI18n();
  const [confirmRow, setConfirmRow] =
    useState<InventoryAdjustmentReasonDto | null>(null);

  if (loading) return <LoadingState />;
  if (reasons.length === 0)
    return (
      <EmptyState
        message={t(
          "inventory.adjustmentReasons.messages.empty",
          "No hay motivos de ajuste registrados aún.",
        )}
      />
    );

  const sorted = [...reasons].sort((a, b) => a.sortOrder - b.sortOrder);

  const columns: ZHDataTableColumn<InventoryAdjustmentReasonDto>[] = [
    {
      key: "code",
      header: t("inventory.adjustmentReasons.table.code", "Código"),
      render: (row) => <Badge label={row.code} variant="neutral" size="md" code />,
    },
    { key: "name", header: t("inventory.adjustmentReasons.table.name", "Nombre"), render: (row) => row.name },
    {
      key: "allowedMovementType",
      header: t("inventory.adjustmentReasons.table.allowedMovementType", "Movimiento permitido"),
      render: (row) => row.allowedMovementType,
    },
    {
      key: "requiresNotes",
      header: t("inventory.adjustmentReasons.table.requiresNotes", "Exige observación"),
      render: (row) => (row.requiresNotes ? t("common.yes", "Sí") : t("common.no", "No")),
    },
    {
      key: "status",
      header: t("inventory.adjustmentReasons.table.status", "Estado"),
      render: (row) => (
        <Badge
          label={row.isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
          variant={row.isActive ? "green" : "gray"}
          size="md"
        />
      ),
    },
    {
      key: "sortOrder",
      header: t("inventory.adjustmentReasons.table.sortOrder", "Orden"),
      render: (row) => <span className="mono">{row.sortOrder}</span>,
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: t("inventory.adjustmentReasons.table.actions", "Acciones"),
            align: "right" as const,
            render: (row: InventoryAdjustmentReasonDto) => (
              <div className="prd-actions-cell">
                <ZHBtn
                  type="button"
                  variant="ghost"
                  size="sm"
                  title={t("common.edit", "Editar")}
                  aria-label={`${t("common.edit", "Editar")} ${row.name}`}
                  disabled={toggling}
                  onClick={() => onEdit(row)}
                >
                  <span className="material-symbols-outlined">edit</span>
                </ZHBtn>
                <ZHIconButton
                  icon={row.isActive ? "block" : "check_circle"}
                  variant={row.isActive ? "danger" : "success"}
                  title={row.isActive ? t("common.disable", "Desactivar") : t("common.enable", "Activar")}
                  ariaLabel={
                    row.isActive
                      ? `${t("common.disable", "Desactivar")} ${row.name}`
                      : `${t("common.enable", "Activar")} ${row.name}`
                  }
                  disabled={toggling}
                  onClick={() => setConfirmRow(row)}
                />
              </div>
            ),
          },
        ]
      : []),
  ];

  return (
    <div className="adjr-list prd-fadein">
      <ZHDataTable
        columns={columns}
        rows={sorted}
        rowKey={(row) => row.id}
        showRowNumber
        rowClassName={(row) => (row.isActive ? undefined : "prd-row--inactive")}
      />

      <ZHConfirmModal
        open={!!confirmRow}
        title={
          confirmRow?.isActive
            ? t(
                "inventory.adjustmentReasons.toggle.disable.title",
                "Desactivar motivo",
              )
            : t(
                "inventory.adjustmentReasons.toggle.activate.title",
                "Activar motivo",
              )
        }
        message={
          <p className="zh-confirm-message">
            {confirmRow?.isActive
              ? t(
                  "inventory.adjustmentReasons.toggle.disable.warning",
                  "Dejará de estar disponible para nuevos ajustes. Los ajustes existentes no cambian.",
                )
              : t(
                  "inventory.adjustmentReasons.toggle.activate.warning",
                  "Volverá a estar disponible para nuevos ajustes.",
                )}{" "}
            <strong>{confirmRow?.name}</strong>
          </p>
        }
        confirmLabel={
          confirmRow?.isActive
            ? t("common.disable", "Desactivar")
            : t("common.enable", "Activar")
        }
        cancelLabel={t("common.cancel", "Cancelar")}
        variant={confirmRow?.isActive ? "danger" : "default"}
        onConfirm={() => {
          const row = confirmRow;
          setConfirmRow(null);
          if (row) void onToggle(row);
        }}
        onCancel={() => setConfirmRow(null)}
      />
    </div>
  );
}
