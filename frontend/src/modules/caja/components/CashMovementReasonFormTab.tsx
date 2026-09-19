import type { FieldErrors, UseFormRegister } from "react-hook-form";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { MANUAL_CASH_MOVEMENT_TYPES } from "../constants/cashMovementTypes";
import type { CashMovementReasonAdminFormValues } from "../schemas/cashMovementReasonAdminSchema";

type Props = {
  editingId: string | null;
  saving: boolean;
  saveError: string;
  register: UseFormRegister<CashMovementReasonAdminFormValues>;
  errors: FieldErrors<CashMovementReasonAdminFormValues>;
  onSave: () => void;
  onCancel: () => void;
};

/**
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03 — editor del motivo de movimiento de caja, solo
 * componentes del DS (`ZHField`, `ZhTextInput`, `ZhNumberInput`, `ZHBtn`). El <select> de Tipo usa
 * `MANUAL_CASH_MOVEMENT_TYPES` — la misma fuente SSOT del formulario "Registrar movimiento manual
 * de efectivo" (constants/cashMovementTypes.ts) — nunca se hardcodea una segunda lista aquí.
 *
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A — `code` Y `movementType` se deshabilitan al editar
 * porque ambos son inmutables en el backend (`UpdateCashMovementReasonCommand` no los incluye) —
 * se muestran en vez de ocultarse para que el usuario siga viendo qué registro está editando y a
 * qué tipo pertenece; cambiar el tipo de un motivo ya usado reclasificaría movimientos históricos
 * que lo referencian.
 */
export function CashMovementReasonFormTab({
  editingId,
  saving,
  saveError,
  register,
  errors,
  onSave,
  onCancel,
}: Props) {
  return (
    <div className="cmr-form prd-fadein">
      {saveError && (
        <ZHPageNotice variant="error" message="Error:" detail={saveError} />
      )}

      <ZHGrid cols={2}>
        <ZHField
          label="Código"
          required
          error={errors.code?.message}
          hint={
            editingId
              ? "El código no se puede modificar después de crear el motivo."
              : undefined
          }
        >
          <ZhTextInput
            className="zh-input mono"
            disabled={saving || !!editingId}
            readOnly={!!editingId}
            aria-required="true"
            aria-label="Código"
            placeholder="Ej: CAMBIO_CAJA"
            {...register("code")}
          />
        </ZHField>

        <ZHField label="Nombre" required error={errors.name?.message}>
          <ZhTextInput
            className="zh-input"
            disabled={saving}
            aria-required="true"
            aria-label="Nombre"
            placeholder="Ej: Cambio de caja chica"
            {...register("name")}
          />
        </ZHField>

        <ZHField
          label="Tipo"
          required
          error={errors.movementType?.message}
          hint={
            editingId
              ? "El tipo no se puede modificar después de crear el motivo."
              : undefined
          }
        >
          <select
            className="zh-input"
            disabled={saving || !!editingId}
            aria-required="true"
            aria-label="Tipo"
            {...register("movementType")}
          >
            <option value="">Seleccione...</option>
            {MANUAL_CASH_MOVEMENT_TYPES.map((mt) => (
              <option key={mt.value} value={mt.value}>
                {mt.label}
              </option>
            ))}
          </select>
        </ZHField>

        <ZHField label="Orden" error={errors.sortOrder?.message}>
          <ZhNumberInput positiveOnly disabled={saving} placeholder="0" {...register("sortOrder")} />
        </ZHField>
      </ZHGrid>

      <div className="zh-form-actions-row zh-form-actions-row--end">
        <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onCancel}>
          Cancelar
        </ZHBtn>
        <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSave}>
          <span className="material-symbols-outlined">save</span>
          {saving ? "Guardando…" : editingId ? "Actualizar motivo" : "Guardar motivo"}
        </ZHBtn>
      </div>
    </div>
  );
}
