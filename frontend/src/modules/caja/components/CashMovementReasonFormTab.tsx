import type { FieldErrors, UseFormRegister } from "react-hook-form";
import { useI18n } from "../../../i18n/i18n";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { manualCashMovementTypeOptions } from "../constants/cashMovementTypes";
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
 * `manualCashMovementTypeOptions(t)` — la misma fuente SSOT del formulario "Registrar movimiento
 * manual de efectivo" (constants/cashMovementTypes.ts) — nunca se hardcodea una segunda lista aquí.
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
  const { t } = useI18n();
  const typeOptions = manualCashMovementTypeOptions(t);

  return (
    <div className="cmr-form prd-fadein">
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix", "Error:")}
          detail={saveError}
        />
      )}

      <ZHGrid cols={2}>
        <ZHField
          label={t("caja.movementReasons.form.code", "Código")}
          required
          error={errors.code?.message}
          hint={
            editingId
              ? t(
                  "caja.movementReasons.form.codeImmutable",
                  "El código no se puede modificar después de crear el motivo.",
                )
              : undefined
          }
        >
          <ZhTextInput
            className="zh-input mono"
            disabled={saving || !!editingId}
            readOnly={!!editingId}
            aria-required="true"
            aria-label={t("caja.movementReasons.form.code", "Código")}
            placeholder={t("caja.movementReasons.form.codePlaceholder", "Ej: CAMBIO_CAJA")}
            {...register("code")}
          />
        </ZHField>

        <ZHField label={t("caja.movementReasons.form.name", "Nombre")} required error={errors.name?.message}>
          <ZhTextInput
            className="zh-input"
            disabled={saving}
            aria-required="true"
            aria-label={t("caja.movementReasons.form.name", "Nombre")}
            placeholder={t(
              "caja.movementReasons.form.namePlaceholder",
              "Ej: Cambio de caja chica",
            )}
            {...register("name")}
          />
        </ZHField>

        <ZHField
          label={t("caja.movementReasons.form.type", "Tipo")}
          required
          error={errors.movementType?.message}
          hint={
            editingId
              ? t(
                  "caja.movementReasons.form.typeImmutable",
                  "El tipo no se puede modificar después de crear el motivo.",
                )
              : undefined
          }
        >
          <select
            className="zh-input"
            disabled={saving || !!editingId}
            aria-required="true"
            aria-label={t("caja.movementReasons.form.type", "Tipo")}
            {...register("movementType")}
          >
            <option value="">{t("caja.movements.form.selectPlaceholder", "Seleccione...")}</option>
            {typeOptions.map((mt) => (
              <option key={mt.value} value={mt.value}>
                {mt.label}
              </option>
            ))}
          </select>
        </ZHField>

        <ZHField label={t("caja.movementReasons.form.sortOrder", "Orden")} error={errors.sortOrder?.message}>
          <ZhNumberInput positiveOnly disabled={saving} placeholder="0" {...register("sortOrder")} />
        </ZHField>
      </ZHGrid>

      <div className="zh-form-actions-row zh-form-actions-row--end">
        <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={onCancel}>
          {t("common.cancel", "Cancelar")}
        </ZHBtn>
        <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={onSave}>
          <span className="material-symbols-outlined">save</span>
          {saving
            ? t("common.saving", "Guardando...")
            : editingId
              ? t("caja.movementReasons.form.update", "Actualizar motivo")
              : t("caja.movementReasons.form.save", "Guardar motivo")}
        </ZHBtn>
      </div>
    </div>
  );
}
