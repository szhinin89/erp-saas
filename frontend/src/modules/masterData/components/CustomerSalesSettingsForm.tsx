import { useEffect, useState, type MutableRefObject } from "react";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { paymentTermService, type PaymentTermDto } from "../api/paymentTermService";
import type { CompanyBpSalesSettingsDto } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";

type Props = {
  initialSettings: CompanyBpSalesSettingsDto | null;
  saving: boolean;
  /** Bandera de guardado en curso: evita envíos duplicados. */
  submittingRef: MutableRefObject<boolean>;
  /** Ficha sin permiso de edición: campo deshabilitado y sin acciones. */
  readOnly?: boolean;
  /** Avisa de cambios sin guardar (protección de la ficha). */
  onDirtyChange?: (dirty: boolean) => void;
  /** Se invoca tras un guardado exitoso. */
  onSaved: () => void;
  onSave: (body: { paymentTermId: string | null }) => Promise<void>;
};

/**
 * Formulario de condición de pago predeterminada para ventas (por empresa activa).
 * Se usa en la pestaña Cliente de la ficha unificada.
 */
export function CustomerSalesSettingsForm({
  initialSettings,
  saving,
  submittingRef,
  readOnly = false,
  onDirtyChange,
  onSaved,
  onSave,
}: Props) {
  const { t } = useI18n();
  const [paymentTermId, setPaymentTermId] = useState(initialSettings?.paymentTermId ?? "");
  const [terms, setTerms] = useState<PaymentTermDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  useEffect(() => {
    let cancelled = false;
    paymentTermService.list().then((data) => {
      if (!cancelled) setTerms(data);
    }).catch((err) => {
      if (!cancelled) {
        setLoadFailed(true);
        setFormError(formatApiRequestError(err, { generic: t("masterdata.bp.detail.sales.loadError", "No se pudo cargar el catálogo de condiciones.") }));
      }
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [t]);
  const dirty = paymentTermId !== (initialSettings?.paymentTermId ?? "");
  useEffect(() => onDirtyChange?.(dirty), [dirty, onDirtyChange]);
  const invalidSelection = !!paymentTermId && !terms.some((pt) => pt.id === paymentTermId && pt.isActive);
  return (
    <form onSubmit={async (event) => {
      event.preventDefault();
      if (loading || loadFailed || invalidSelection || saving || submittingRef.current) return;
      submittingRef.current = true;
      setFormError(null);
      try {
        await onSave({ paymentTermId: paymentTermId || null });
        message.success(t("masterdata.bp.detail.sales.saved", "Condición de ventas guardada correctamente."));
        onSaved();
      } catch (err) {
        setFormError(formatApiRequestError(err, { generic: t("masterdata.bp.detail.sales.saveError", "No se pudo guardar la condición de ventas.") }));
      } finally {
        submittingRef.current = false;
      }
    }}>
      {formError && <ZHPageNotice variant="error" message={formError} />}
      {invalidSelection && !loading && <ZHPageNotice variant="warning" message={t("masterdata.bp.detail.sales.unavailable", "La condición configurada no está disponible. Seleccione una condición activa o quite el valor predeterminado.")} />}
      <ZHField label={t("masterdata.bp.detail.sales.label", "Condición predeterminada para ventas")} hint={t("masterdata.bp.detail.sales.hint", "Aplica a la empresa activa. Sin valor predeterminado, deberá seleccionar una condición al crear la venta.")}>
        <ZhSelect aria-label={t("masterdata.bp.detail.sales.label", "Condición predeterminada para ventas")} value={paymentTermId} disabled={loading || saving || loadFailed || readOnly} onChange={(event) => setPaymentTermId(event.target.value)}>
          <option value="">{t("masterdata.bp.detail.sales.none", "Sin condición predeterminada")}</option>
          {invalidSelection && <option value={paymentTermId} disabled>{t("masterdata.bp.detail.sales.notAvailable", "Condición no disponible")}</option>}
          {terms.filter((pt) => pt.isActive).map((pt) => <option key={pt.id} value={pt.id}>{pt.code} — {pt.name} ({pt.summary})</option>)}
        </ZhSelect>
      </ZHField>
      {!readOnly && <ZHFormActions hideCancel saveButtonType="submit" disableSave={loading || saving || loadFailed || invalidSelection} labels={{ save: saving ? t("masterdata.bp.detail.saving", "Guardando...") : t("masterdata.bp.detail.save", "Guardar") }} />}
    </form>
  );
}
