import { useEffect, useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";

/**
 * Default de compras/gastos por empresa activa (ADR-033). Sección independiente de la Config SRI:
 * fetch/save propios — un fallo en uno no afecta al otro y cada uno muestra su propio error.
 * (Extraída de MasterDataSuppliersPage sin cambio de comportamiento; mismo guardado independiente.)
 */
export function SupplierPurchaseDefaultSection({
  bpId,
  readOnly = false,
  onDirtyChange,
}: {
  bpId: string;
  /** Ficha sin permiso `configure-company`: selector deshabilitado y sin botón de guardado. */
  readOnly?: boolean;
  /** Avisa de cambios sin guardar (protección de la ficha). */
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const { t } = useI18n();
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const [purchaseDefaultPtId, setPurchaseDefaultPtId] = useState("");
  // Último valor persistido (carga o guardado): base para detectar cambios sin guardar.
  const [persistedPtId, setPersistedPtId] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    paymentTermService
      .list()
      .then(setPaymentTermsList)
      .catch(() => {});
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    businessPartnerFacade
      .getPurchaseSettings(bpId)
      .then((dto) => {
        if (!cancelled) {
          setPurchaseDefaultPtId(dto.paymentTermId ?? "");
          setPersistedPtId(dto.paymentTermId ?? "");
        }
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setError(
            formatApiRequestError(err, {
              generic: t("masterdata.bp.detail.purchase.loadError", "No se pudo cargar la condición predeterminada."),
            }),
          );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [bpId, t]);

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      await businessPartnerFacade.upsertPurchaseSettings(bpId, {
        paymentTermId: purchaseDefaultPtId || null,
      });
      setPersistedPtId(purchaseDefaultPtId);
      setSaved(true);
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, {
          generic: t("masterdata.bp.detail.purchase.saveError", "No se pudo guardar la condición predeterminada."),
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const dirty = !loading && purchaseDefaultPtId !== persistedPtId;
  useEffect(() => onDirtyChange?.(dirty), [dirty, onDirtyChange]);

  return (
    <div className="md-supplier-purchase-default-section">
      <h4>{t("masterdata.bp.detail.purchase.title", "Condición predeterminada para compras/gastos")}</h4>
      {error && <ZHPageNotice variant="error" message={error} />}
      <ZHField
        label={t("masterdata.bp.detail.purchase.title", "Condición predeterminada para compras/gastos")}
        hint={t("masterdata.bp.detail.purchase.hint", "Aplica solo a la empresa activa. Si este proveedor trabaja con otra empresa, puede tener una condición distinta.")}
      >
        <ZhSelect
          value={purchaseDefaultPtId}
          onChange={(e) => {
            setPurchaseDefaultPtId(e.target.value);
            setSaved(false);
          }}
          disabled={loading || saving || readOnly}
        >
          <option value="">
            {t("masterdata.bp.detail.purchase.noDefault", "— Sin default (exigir selección al crear documentos) —")}
          </option>
          {paymentTermsList
            .filter((pt) => pt.isActive || pt.id === purchaseDefaultPtId)
            .map((pt) => (
              <option key={pt.id} value={pt.id}>
                {pt.code} — {pt.name} ({pt.summary})
              </option>
            ))}
        </ZhSelect>
      </ZHField>
      {!readOnly && (
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={loading || saving}
          onClick={() => void handleSave()}
        >
          {saving
            ? t("masterdata.bp.detail.purchase.saving", "Guardando...")
            : saved
              ? t("masterdata.bp.detail.purchase.saved", "Guardado ✓")
              : t("masterdata.bp.detail.purchase.save", "Guardar condición de compras/gastos")}
        </ZHBtn>
      )}
    </div>
  );
}
