import { Controller } from "react-hook-form";
import type { Control, FieldErrors, UseFormRegister } from "react-hook-form";
import { useI18n } from "../../../../i18n/i18n";
import { ZHBtn, ZHField, ZHGrid, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZhNumberInput } from "../../../../components/zh/inputs/ZhNumberInput";
import type { AdjustmentReasonFormValues } from "../../../../schemas/inventory/adjustmentReasonSchema";

type Props = {
  editingId: string | null;
  saving: boolean;
  saveError: string;
  register: UseFormRegister<AdjustmentReasonFormValues>;
  control: Control<AdjustmentReasonFormValues>;
  errors: FieldErrors<AdjustmentReasonFormValues>;
  onSave: () => void;
  onCancel: () => void;
};

/**
 * Editor del motivo de ajuste — solo componentes del DS (`ZHField`, `ZhTextInput`, `ZhSelect`,
 * `ZhNumberInput`, `ZHToggle`, `ZHBtn`); ningún input/checkbox propio.
 *
 * `code` se deshabilita al editar porque es inmutable en el backend (`UpdateInventoryAdjustment
 * ReasonCommand` no lo incluye) — se muestra en vez de ocultarse para que el usuario siga viendo
 * qué registro está editando.
 */
export function AdjustmentReasonFormTab({
  editingId,
  saving,
  saveError,
  register,
  control,
  errors,
  onSave,
  onCancel,
}: Props) {
  const { t } = useI18n();

  return (
    <div className="adjr-form prd-fadein">
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix", "Error:")}
          detail={saveError}
        />
      )}

      <ZHGrid cols={2}>
        <ZHField
          label={t("inventory.adjustmentReasons.form.code", "Código")}
          required
          error={errors.code?.message}
          hint={
            editingId
              ? t(
                  "inventory.adjustmentReasons.form.codeImmutable",
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
            aria-label={t("inventory.adjustmentReasons.form.code", "Código")}
            placeholder={t(
              "inventory.adjustmentReasons.form.codePlaceholder",
              "Ej: MERMA",
            )}
            {...register("code")}
          />
        </ZHField>

        <ZHField
          label={t("inventory.adjustmentReasons.form.name", "Nombre")}
          required
          error={errors.name?.message}
        >
          <ZhTextInput
            className="zh-input"
            disabled={saving}
            aria-required="true"
            aria-label={t("inventory.adjustmentReasons.form.name", "Nombre")}
            placeholder={t(
              "inventory.adjustmentReasons.form.namePlaceholder",
              "Ej: Merma por caducidad",
            )}
            {...register("name")}
          />
        </ZHField>

        <ZHField
          label={t(
            "inventory.adjustmentReasons.form.allowedMovementType",
            "Movimiento permitido",
          )}
          required
          error={errors.allowedMovementType?.message}
        >
          <ZhSelect
            disabled={saving}
            aria-required="true"
            aria-label={t(
              "inventory.adjustmentReasons.form.allowedMovementType",
              "Movimiento permitido",
            )}
            {...register("allowedMovementType")}
          >
            <option value="Ingreso">
              {t("inventory.adjustments.movementType.ingreso", "Ingreso")}
            </option>
            <option value="Egreso">
              {t("inventory.adjustments.movementType.egreso", "Egreso")}
            </option>
            <option value="Ambos">
              {t("inventory.adjustmentReasons.movementType.both", "Ambos")}
            </option>
          </ZhSelect>
        </ZHField>

        <ZHField
          label={t("inventory.adjustmentReasons.form.sortOrder", "Orden")}
          error={errors.sortOrder?.message}
        >
          <ZhNumberInput
            positiveOnly
            disabled={saving}
            placeholder="0"
            {...register("sortOrder")}
          />
        </ZHField>

        <div className="zh-col-span-2">
          <Controller
            name="requiresNotes"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label={t(
                  "inventory.adjustmentReasons.form.requiresNotes",
                  "Exige observación",
                )}
                description={t(
                  "inventory.adjustmentReasons.form.requiresNotesDesc",
                  "Si se activa, el ajuste que use este motivo no se puede guardar sin observación.",
                )}
                value={field.value}
                onChange={field.onChange}
                disabled={saving}
              />
            )}
          />
        </div>
      </ZHGrid>

      <div className="zh-form-actions-row zh-form-actions-row--end">
        <ZHBtn
          variant="ghost"
          size="md"
          type="button"
          disabled={saving}
          onClick={onCancel}
        >
          {t("common.cancel", "Cancelar")}
        </ZHBtn>
        <ZHBtn
          variant="primary"
          size="md"
          type="button"
          disabled={saving}
          onClick={onSave}
        >
          <span className="material-symbols-outlined">save</span>
          {saving
            ? t("common.saving", "Guardando…")
            : editingId
              ? t("inventory.adjustmentReasons.form.update", "Actualizar motivo")
              : t("inventory.adjustmentReasons.form.save", "Guardar motivo")}
        </ZHBtn>
      </div>
    </div>
  );
}
