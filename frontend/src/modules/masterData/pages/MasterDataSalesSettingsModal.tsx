import { useEffect, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { paymentTermService, type PaymentTermDto } from "../api/paymentTermService";
import type { BusinessPartnerSummaryDto, CompanyBpSalesSettingsDto } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";

type Props = {
  partner: BusinessPartnerSummaryDto;
  initialSettings: CompanyBpSalesSettingsDto | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (body: { paymentTermId: string | null }) => Promise<void>;
};

export function MasterDataSalesSettingsModal({ partner, initialSettings, saving, error, onClose, onSave }: Props) {
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
    <ZHModal open onClose={onClose} title="Condición predeterminada para ventas" subtitle={partner.legalName} closeOnBackdrop={false}>
      <form onSubmit={async (event) => {
        event.preventDefault();
        if (loading || loadFailed || invalidSelection || saving) return;
        setFormError(null);
        try {
          await onSave({ paymentTermId: paymentTermId || null });
          onClose();
        } catch (err) {
          setFormError(formatApiRequestError(err, { generic: "No se pudo guardar la condición de ventas." }));
        }
      }}>
        {(error || formError) && <ZHPageNotice variant="error" message={error ?? formError!} />}
        {invalidSelection && !loading && <ZHPageNotice variant="warning" message="La condición configurada no está disponible. Seleccione una condición activa o quite el valor predeterminado." />}
        <ZHField label="Condición predeterminada para ventas" hint="Aplica a la empresa activa. Sin valor predeterminado, deberá seleccionar una condición al crear la venta.">
          <select aria-label="Condición predeterminada para ventas" value={paymentTermId} disabled={loading || saving || loadFailed} onChange={(event) => setPaymentTermId(event.target.value)}>
            <option value="">Sin condición predeterminada</option>
            {invalidSelection && <option value={paymentTermId} disabled>Condición no disponible</option>}
            {terms.filter((pt) => pt.isActive).map((pt) => <option key={pt.id} value={pt.id}>{pt.code} — {pt.name} ({pt.summary})</option>)}
          </select>
        </ZHField>
        <ZHFormActions onCancel={onClose} saveButtonType="submit" disableSave={loading || saving || loadFailed || invalidSelection} labels={{ cancel: "Cerrar", save: saving ? "Guardando..." : "Guardar" }} />
      </form>
    </ZHModal>
  );
}
