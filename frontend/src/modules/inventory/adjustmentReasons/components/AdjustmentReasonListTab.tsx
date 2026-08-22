import { useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { Badge, EmptyState, LoadingState } from "../../../../components/PageShell";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { ZHConfirmModal } from "../../../../components/zh/ZHConfirmModal";
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

  return (
    <div className="adjr-list prd-fadein">
      <div className="table-scroll">
        <table className="table">
          <thead>
            <tr>
              <th>{t("inventory.adjustmentReasons.table.code", "Código")}</th>
              <th>{t("inventory.adjustmentReasons.table.name", "Nombre")}</th>
              <th>
                {t(
                  "inventory.adjustmentReasons.table.allowedMovementType",
                  "Movimiento permitido",
                )}
              </th>
              <th>
                {t(
                  "inventory.adjustmentReasons.table.requiresNotes",
                  "Exige observación",
                )}
              </th>
              <th>{t("inventory.adjustmentReasons.table.status", "Estado")}</th>
              <th>{t("inventory.adjustmentReasons.table.sortOrder", "Orden")}</th>
              {canManage && (
                <th className="pg-th-right">
                  {t("inventory.adjustmentReasons.table.actions", "Acciones")}
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {sorted.map((row) => (
              <tr
                key={row.id}
                className={row.isActive ? undefined : "prd-row--inactive"}
              >
                <td>
                  <Badge label={row.code} variant="neutral" size="md" code />
                </td>
                <td>{row.name}</td>
                <td>{row.allowedMovementType}</td>
                <td>
                  {row.requiresNotes
                    ? t("common.yes", "Sí")
                    : t("common.no", "No")}
                </td>
                <td>
                  <Badge
                    label={
                      row.isActive
                        ? t("common.active", "Activo")
                        : t("common.inactive", "Inactivo")
                    }
                    variant={row.isActive ? "green" : "gray"}
                    size="md"
                  />
                </td>
                <td className="mono">{row.sortOrder}</td>
                {canManage && (
                  <td className="pg-th-right">
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
                        title={
                          row.isActive
                            ? t("common.disable", "Desactivar")
                            : t("common.enable", "Activar")
                        }
                        ariaLabel={
                          row.isActive
                            ? `${t("common.disable", "Desactivar")} ${row.name}`
                            : `${t("common.enable", "Activar")} ${row.name}`
                        }
                        disabled={toggling}
                        onClick={() => setConfirmRow(row)}
                      />
                    </div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

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
