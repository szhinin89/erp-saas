import { useState } from "react";
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
  const [confirmRow, setConfirmRow] = useState<CashMovementReasonDto | null>(null);

  if (loading) return <LoadingState />;
  if (reasons.length === 0)
    return <EmptyState message="No hay motivos de movimientos registrados aún." />;

  const sorted = [...reasons].sort((a, b) => a.sortOrder - b.sortOrder);

  const columns: ZHDataTableColumn<CashMovementReasonDto>[] = [
    {
      key: "code",
      header: "Código",
      render: (row) => <Badge label={row.code} variant="neutral" size="md" code />,
    },
    { key: "name", header: "Nombre", render: (row) => row.name },
    {
      key: "movementType",
      header: "Tipo",
      render: (row) => cashMovementTypeLabel(row.movementType),
    },
    {
      key: "sortOrder",
      header: "Orden",
      render: (row) => <span className="mono">{row.sortOrder}</span>,
    },
    {
      key: "status",
      header: "Estado",
      render: (row) => (
        <Badge
          label={row.isActive ? "Activo" : "Inactivo"}
          variant={row.isActive ? "green" : "gray"}
          size="md"
        />
      ),
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: "Acciones",
            align: "right" as const,
            render: (row: CashMovementReasonDto) => (
              <div className="prd-actions-cell">
                <ZHBtn
                  type="button"
                  variant="ghost"
                  size="sm"
                  title="Editar"
                  aria-label={`Editar ${row.name}`}
                  disabled={toggling}
                  onClick={() => onEdit(row)}
                >
                  <span className="material-symbols-outlined">edit</span>
                </ZHBtn>
                <ZHIconButton
                  icon={row.isActive ? "block" : "check_circle"}
                  variant={row.isActive ? "danger" : "success"}
                  title={row.isActive ? "Desactivar" : "Activar"}
                  ariaLabel={
                    row.isActive ? `Desactivar ${row.name}` : `Activar ${row.name}`
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
        title={confirmRow?.isActive ? "Desactivar motivo" : "Activar motivo"}
        message={
          <p className="zh-confirm-message">
            {confirmRow?.isActive
              ? "Dejará de estar disponible para nuevos movimientos manuales de caja. Los movimientos existentes no cambian."
              : "Volverá a estar disponible para nuevos movimientos manuales de caja."}{" "}
            <strong>{confirmRow?.name}</strong>
          </p>
        }
        confirmLabel={confirmRow?.isActive ? "Desactivar" : "Activar"}
        cancelLabel="Cancelar"
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
