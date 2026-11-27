import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";

/**
 * Acciones de los modales de Anticipos para `ZHModal.footer` (modal-standard): Cancelar antes de la
 * acción principal, ambas deshabilitadas mientras se envía.
 */
export function CustomerAdvanceModalFooter({
  saving,
  actionLabel,
  onCancel,
  onSubmit,
}: {
  saving: boolean;
  actionLabel: string;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  const { t } = useI18n();
  return (
    <>
      <ZHBtn type="button" variant="ghost" disabled={saving} onClick={onCancel}>
        {t("common.cancel")}
      </ZHBtn>
      <ZHBtn type="button" variant="primary" disabled={saving} onClick={onSubmit}>
        {saving ? t("finance.customerAdvances.saving") : actionLabel}
      </ZHBtn>
    </>
  );
}
