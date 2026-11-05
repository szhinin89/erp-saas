import { useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import type { CustomerConfigBody, CustomerRoleConfigDto } from "../types/businessPartner.types";
import {
  useCustomerCategories,
  useCustomerClassifications,
  useCustomerCreditRatings,
  useCustomerInvoiceFormats,
  useCustomerSegments,
  useLoyaltyTiers,
} from "../api/useClassificationCatalogs";

/**
 * BUG-CUSTOMERS-CLASSIFICATION-01: conserva como opción seleccionable un valor persistido que ya no
 * figura en el catálogo activo; sin ella el select lo mostraría vacío y el guardado (reemplazo
 * completo) lo borraría sin intervención del usuario.
 */
function PersistedValueOption({
  value,
  loading,
  options,
}: Readonly<{
  value: string;
  loading: boolean;
  options: readonly { code: string }[];
}>) {
  if (!value || loading || options.some((o) => o.code === value)) return null;
  return <option value={value}>{`${value} (fuera del catálogo activo)`}</option>;
}

// ── MasterDataCustomerConfigModal — CRM-ready classification modal ───────────
export function MasterDataCustomerConfigModal({
  bpName,
  initialConfig,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpName: string;
  initialConfig: CustomerRoleConfigDto | null;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: CustomerConfigBody) => void;
}) {
  const [category, setCategory] = useState(initialConfig?.customerCategory ?? "");
  const [segment, setSegment] = useState(initialConfig?.customerSegment ?? "");
  const [zone, setZone] = useState(initialConfig?.salesZone ?? "");
  const [rating, setRating] = useState(initialConfig?.creditRating ?? "");
  const [loyalty, setLoyalty] = useState(initialConfig?.loyaltyTier ?? "");
  const [invoiceFormat, setInvoiceFormat] = useState(initialConfig?.preferredInvoiceFormat ?? "");
  const [classification, setClassification] = useState(initialConfig?.customerClassification ?? "");

  const categories = useCustomerCategories();
  const segments = useCustomerSegments();
  const creditRatings = useCustomerCreditRatings();
  const loyaltyTiers = useLoyaltyTiers();
  const invoiceFormats = useCustomerInvoiceFormats();
  const classifications = useCustomerClassifications();
  const catalogsError =
    categories.error ||
    segments.error ||
    creditRatings.error ||
    loyaltyTiers.error ||
    invoiceFormats.error ||
    classifications.error;
  const catalogsLoading =
    categories.loading ||
    segments.loading ||
    creditRatings.loading ||
    loyaltyTiers.loading ||
    invoiceFormats.loading ||
    classifications.loading;
  // No se guarda hasta tener catálogos cargados: el payload reemplaza la configuración completa.
  const saveBlocked = saving || catalogsLoading || Boolean(catalogsError);

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    if (saveBlocked) return;
    onSave({
      customerCategory: category || null,
      customerSegment: segment || null,
      salesZone: zone.trim() || null,
      creditRating: rating || null,
      loyaltyTier: loyalty || null,
      preferredInvoiceFormat: invoiceFormat || null,
      customerClassification: classification || null,
    });
  };

  return (
    <ZHModal
      open
      onClose={onClose}
      title="Perfil Cliente — CRM"
      subtitle={bpName}
    >
      <form onSubmit={handleSave}>
        {error && <ZHPageNotice variant="error" message={error} />}
        {catalogsError && <ZHPageNotice variant="error" message={catalogsError} />}
        <ZHGrid cols={2}>
          <ZHField label="Categoría">
            <ZhSelect
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              disabled={saving || categories.loading}
            >
              <option value="">
                {categories.loading ? "Cargando…" : "— Sin asignar —"}
              </option>
              <PersistedValueOption value={category} loading={categories.loading} options={categories.options} />
              {categories.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Segmento">
            <ZhSelect
              value={segment}
              onChange={(e) => setSegment(e.target.value)}
              disabled={saving || segments.loading}
            >
              <option value="">
                {segments.loading ? "Cargando…" : "— Sin asignar —"}
              </option>
              <PersistedValueOption value={segment} loading={segments.loading} options={segments.options} />
              {segments.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Zona de ventas">
            <ZhTextInput
              className="zh-input"
              value={zone}
              onChange={(e) => setZone(e.target.value)}
              placeholder="Norte, Sur, Centro…"
              maxLength={100}
              disabled={saving}
            />
          </ZHField>
          <ZHField label="Rating crediticio">
            <ZhSelect
              value={rating}
              onChange={(e) => setRating(e.target.value)}
              disabled={saving || creditRatings.loading}
            >
              <option value="">
                {creditRatings.loading ? "Cargando…" : "— Sin calificar —"}
              </option>
              <PersistedValueOption value={rating} loading={creditRatings.loading} options={creditRatings.options} />
              {creditRatings.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Nivel de fidelización">
            <ZhSelect
              value={loyalty}
              onChange={(e) => setLoyalty(e.target.value)}
              disabled={saving || loyaltyTiers.loading}
            >
              <option value="">{loyaltyTiers.loading ? "Cargando…" : "— Sin nivel —"}</option>
              <PersistedValueOption value={loyalty} loading={loyaltyTiers.loading} options={loyaltyTiers.options} />
              {loyaltyTiers.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Formato de factura">
            <ZhSelect
              value={invoiceFormat}
              onChange={(e) => setInvoiceFormat(e.target.value)}
              disabled={saving || invoiceFormats.loading}
            >
              <option value="">
                {invoiceFormats.loading ? "Cargando…" : "— Sin preferencia —"}
              </option>
              <PersistedValueOption value={invoiceFormat} loading={invoiceFormats.loading} options={invoiceFormats.options} />
              {invoiceFormats.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label="Clasificación comercial">
            <ZhSelect
              value={classification}
              onChange={(e) => setClassification(e.target.value)}
              disabled={saving || classifications.loading}
            >
              <option value="">
                {classifications.loading ? "Cargando…" : "— Sin clasificar —"}
              </option>
              <PersistedValueOption value={classification} loading={classifications.loading} options={classifications.options} />
              {classifications.options.map((v) => (
                <option key={v.id} value={v.code}>
                  {v.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </ZHGrid>
        <ZHFormActions
          onCancel={onClose}
          hideDraft
          saveButtonType="submit"
          disableSave={saveBlocked}
          labels={{
            cancel: "Cerrar",
            save: saving ? "Guardando..." : "Guardar",
          }}
        />
      </form>
    </ZHModal>
  );
}
