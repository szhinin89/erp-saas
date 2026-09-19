import { useState } from "react";
import { useI18n } from "../../../i18n/i18n";
import { Badge, EmptyState, LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { cashMovementTypeLabel } from "../constants/cashMovementTypes";
import type { CashMovementReasonDto } from "../api/cajaService";

type Props = {
  reasons: CashMovementReasonDto[];
  loading: boolean;
  toggling: boolean;
  canManage: boolean;
  onEdit: (row: CashMovementReasonDto) => void;
  onToggle: (row: CashMovementReasonDto) => Promise<void>;
};

/**
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03 — mismo armazón que
 * `AdjustmentReasonListTab` (tabla `ZHDataTable`, `Badge`, `ZHIconButton`, confirmación con
 * `ZHConfirmModal`), sin buscador porque el catálogo es corto y ya viene ordenado por
 * `sortOrder`. `cashMovementTypeLabel` es la misma fuente SSOT que usa el historial de
 * Movimientos en /treasury/cash — nunca se reimplementa el mapeo Tipo aquí.
 */
export function CashMovementReasonListTab({
  reasons,
  loading,
  toggling,
  canManage,
  onEdit,
  onToggle,
}: Props) {
  const { t } = useI18n();
  const [confirmRow, setConfirmRow] = useState<CashMovementReasonDto | null>(null);

  if (loading) return <LoadingState />;
  if (reasons.length === 0)
    return (
      <EmptyState
        message={t(
          "caja.movementReasons.messages.empty",
          "No hay motivos de movimientos registrados aún.",
        )}
      />
    );

  const sorted = [...reasons].sort((a, b) => a.sortOrder - b.sortOrder);

  const columns: ZHDataTableColumn<CashMovementReasonDto>[] = [
    {
      key: "code",
      header: t("caja.movementReasons.table.code", "Código"),
      render: (row) => <Badge label={row.code} variant="neutral" size="md" code />,
    },
    {
      key: "name",
      header: t("caja.movementReasons.table.name", "Nombre"),
      render: (row) => row.name,
    },
    {
      key: "movementType",
      header: t("caja.movementReasons.table.type", "Tipo"),
      render: (row) => cashMovementTypeLabel(t, row.movementType),
    },
    {
      key: "sortOrder",
      header: t("caja.movementReasons.table.sortOrder", "Orden"),
      render: (row) => <span className="mono">{row.sortOrder}</span>,
    },
    {
      key: "status",
      header: t("caja.movementReasons.table.status", "Estado"),
      render: (row) => (
        <Badge
          label={row.isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
          variant={row.isActive ? "green" : "gray"}
          size="md"
        />
      ),
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: t("caja.movementReasons.table.actions", "Acciones"),
            align: "right" as const,
            render: (row: CashMovementReasonDto) => (
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
                  title={row.isActive ? t("common.deactivate", "Desactivar") : t("common.activate", "Activar")}
                  ariaLabel={
                    row.isActive
                      ? `${t("common.deactivate", "Desactivar")} ${row.name}`
                      : `${t("common.activate", "Activar")} ${row.name}`
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
    <div className="cmr-list prd-fadein">
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
            ? t("caja.movementReasons.toggle.disable.title", "Desactivar motivo")
            : t("caja.movementReasons.toggle.activate.title", "Activar motivo")
        }
        message={
          <p className="zh-confirm-message">
            {confirmRow?.isActive
              ? t(
                  "caja.movementReasons.toggle.disable.warning",
                  "Dejará de estar disponible para nuevos movimientos manuales de caja. Los movimientos existentes no cambian.",
                )
              : t(
                  "caja.movementReasons.toggle.activate.warning",
                  "Volverá a estar disponible para nuevos movimientos manuales de caja.",
                )}{" "}
            <strong>{confirmRow?.name}</strong>
          </p>
        }
        confirmLabel={
          confirmRow?.isActive
            ? t("common.deactivate", "Desactivar")
            : t("common.activate", "Activar")
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
