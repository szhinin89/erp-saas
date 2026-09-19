import type { FormEventHandler } from "react";
import type { FieldErrors, UseFormRegister } from "react-hook-form";
import { useI18n } from "../../../i18n/i18n";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput } from "../../../components/zh/inputs";
import type { manualCashMovementTypeOptions } from "../constants/cashMovementTypes";
import type { CashMovementReasonDto } from "../api/cajaService";
import type { RecordMovementFormValues } from "../schemas/cajaSchema";

type Props = {
  open: boolean;
  saving: boolean;
  saveError: string;
  register: UseFormRegister<RecordMovementFormValues>;
  errors: FieldErrors<RecordMovementFormValues>;
  /** Tipo actualmente seleccionado en el formulario — ya resuelto por el hook (misma fuente que
   * filtra `reasons`); el componente nunca llama `watch()` por su cuenta. */
  selectedMovementType: string;
  movementTypes: ReturnType<typeof manualCashMovementTypeOptions>;
  reasons: CashMovementReasonDto[];
  reasonsLoading: boolean;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onClose: () => void;
};

/**
 * TREASURY-CASH-MANUAL-MOVEMENT-SHARED-MODAL-07 — extraído tal cual de CajaPage.tsx (mismo
 * markup, mismos componentes ZH, mismas keys i18n, cero comportamiento nuevo). Puramente
 * presentacional: NO decide si la empresa permite movimientos manuales, si el usuario tiene
 * `caja.record`, si hay turno abierto, ni conoce Tenant/Company — todo eso sigue viviendo en
 * useCajaPage.tsx (o, cuando se use desde otro módulo, en su propio hook/página). Recibe
 * únicamente datos ya resueltos y callbacks — mismo patrón que `CashMovementReasonFormTab`
 * (register/errors del `useForm` del padre, sin estado ni validación propios).
 *
 * Reutiliza sin duplicar: `ZHModal`/`ZHField`/`ZHBtn`/`ZhDecimalInput`/`ZHPageNotice` (Design
 * System), las keys i18n `caja.movements.*`/`common.*` ya existentes, `movementTypes` (única
 * fuente `manualCashMovementTypeOptions`) y `reasons` (catálogo `CashMovementReason` ya resuelto
 * por el hook) — ninguna lista, label ni validación se reimplementa aquí.
 */
export function ManualCashMovementModal({
  open,
  saving,
  saveError,
  register,
  errors,
  selectedMovementType,
  movementTypes,
  reasons,
  reasonsLoading,
  onSubmit,
  onClose,
}: Props) {
  const { t } = useI18n();

  return (
    <ZHModal
      closeLabel={t("common.close")}
      open={open}
      onClose={onClose}
      size="md"
      title={t("caja.movements.modal.title")}
      closeOnBackdrop={!saving}
    >
      <form onSubmit={onSubmit} className="cj-movement-form">
        <ZHField
          density="compact"
          className="cj-movement-field--type"
          label={t("caja.movements.form.type")}
          required
          fieldError={errors.movementType?.message}
        >
          <select {...register("movementType")}>
            <option value="">{t("caja.movements.form.selectPlaceholder")}</option>
            {movementTypes.map((mt) => (
              <option key={mt.value} value={mt.value}>
                {mt.label}
              </option>
            ))}
          </select>
        </ZHField>
        {/* TREASURY-CASH-MANUAL-MOVEMENTS-01 — motivo dinámico desde el backend
            (CashMovementReason), filtrado por Tenant+Company+Tipo — nunca texto libre
            ni una lista hardcodeada en el frontend. */}
        <ZHField
          density="compact"
          className="cj-movement-field--reason"
          label={t("caja.movements.form.reason")}
          required
          fieldError={errors.reasonId?.message}
        >
          <select
            {...register("reasonId")}
            disabled={!selectedMovementType || reasonsLoading || reasons.length === 0}
          >
            <option value="">
              {reasonsLoading
                ? t("caja.movements.form.reasonLoading")
                : selectedMovementType && reasons.length === 0
                  ? t("caja.movements.form.reasonEmpty")
                  : t("caja.movements.form.selectPlaceholder")}
            </option>
            {reasons.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </ZHField>
        <ZHField
          density="compact"
          className="cj-movement-field--amount"
          label={t("caja.movements.form.amount")}
          required
          fieldError={errors.amount?.message}
        >
          <ZhDecimalInput {...register("amount")} decimals={2} positiveOnly />
        </ZHField>
        <ZHField
          density="compact"
          className="cj-movement-field--desc"
          label={t("caja.movements.form.description")}
          required
          fieldError={errors.description?.message}
        >
          <input type="text" {...register("description")} />
        </ZHField>
        {saveError && (
          <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={saveError} />
        )}
        <div className="cj-actions">
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? t("common.saving") : t("caja.movements.form.submit")}
          </ZHBtn>
          <ZHBtn variant="secondary" type="button" disabled={saving} onClick={onClose}>
            {t("common.cancel")}
          </ZHBtn>
        </div>
      </form>
    </ZHModal>
  );
}
