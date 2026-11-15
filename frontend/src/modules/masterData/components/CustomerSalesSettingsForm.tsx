import { useEffect, useState, type MutableRefObject } from "react";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { paymentTermService, type PaymentTermDto } from "../api/paymentTermService";
import type { CompanyBpSalesSettingsDto } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

type Props = {
  initialSettings: CompanyBpSalesSettingsDto | null;
  saving: boolean;
  error: string | null;
  /** Bandera compartida con el modal: bloquea el cierre mientras hay un guardado en curso. */
  submittingRef: MutableRefObject<boolean>;
  /** Cancelar/cerrar manual (el modal lo protege mientras hay un guardado en curso). */
  onClose: () => void;
  /** Cierre tras un guardado exitoso (sin la guarda de guardado en curso). */
  onSaved: () => void;
  onSave: (body: { paymentTermId: string | null }) => Promise<void>;
};

/**
 * Formulario de condición de pago predeterminada para ventas (por empresa activa).
 * Extraído de MasterDataSalesSettingsModal sin cambio de comportamiento.
 */
export function CustomerSalesSettingsForm({
  initialSettings,
  saving,
  error,
  submittingRef,
  onClose,
  onSaved,
  onSave,
}: Props) {
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
        setFormError(formatApiRequestError(err, { generic: "No se pudo cargar el catálogo de condiciones." }));
      }
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);
  const invalidSelection = !!paymentTermId && !terms.some((pt) => pt.id === paymentTermId && pt.isActive);
  return (
    <form onSubmit={async (event) => {
      event.preventDefault();
      if (loading || loadFailed || invalidSelection || saving || submittingRef.current) return;
      submittingRef.current = true;
      setFormError(null);
      try {
        await onSave({ paymentTermId: paymentTermId || null });
        message.success("Condición de ventas guardada correctamente.");
        onSaved();
      } catch (err) {
        setFormError(formatApiRequestError(err, { generic: "No se pudo guardar la condición de ventas." }));
      } finally {
        submittingRef.current = false;
      }
    }}>
      {(error || formError) && <ZHPageNotice variant="error" message={error ?? formError!} />}
      {invalidSelection && !loading && <ZHPageNotice variant="warning" message="La condición configurada no está disponible. Seleccione una condición activa o quite el valor predeterminado." />}
      <ZHField label="Condición predeterminada para ventas" hint="Aplica a la empresa activa. Sin valor predeterminado, deberá seleccionar una condición al crear la venta.">
        <ZhSelect aria-label="Condición predeterminada para ventas" value={paymentTermId} disabled={loading || saving || loadFailed} onChange={(event) => setPaymentTermId(event.target.value)}>
          <option value="">Sin condición predeterminada</option>
          {invalidSelection && <option value={paymentTermId} disabled>Condición no disponible</option>}
          {terms.filter((pt) => pt.isActive).map((pt) => <option key={pt.id} value={pt.id}>{pt.code} — {pt.name} ({pt.summary})</option>)}
        </ZhSelect>
      </ZHField>
      <ZHFormActions onCancel={onClose} saveButtonType="submit" disableSave={loading || saving || loadFailed || invalidSelection} labels={{ cancel: "Cerrar", save: saving ? "Guardando..." : "Guardar" }} />
    </form>
  );
}
