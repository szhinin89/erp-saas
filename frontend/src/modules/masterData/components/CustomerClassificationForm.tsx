import { useEffect, useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
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
  const { t } = useI18n();
  if (!value || loading || options.some((o) => o.code === value)) return null;
  return <option value={value}>{t("masterdata.bp.detail.cls.outOfCatalog", { value })}</option>;
}

/**
 * Formulario de clasificación comercial del cliente (CRM), usado en la pestaña Cliente de la ficha
 * unificada (`/masterdata/business-partners/:id`). El PATCH del rol reemplaza la configuración completa:
 * parte siempre de la persistida y no guarda mientras los catálogos cargan o fallan.
 */
export function CustomerClassificationForm({
  initialConfig,
  saving,
  error,
  readOnly = false,
  onSave,
  onDirtyChange,
}: {
  initialConfig: CustomerRoleConfigDto | null;
  saving: boolean;
  error?: string | null;
  /** Ficha sin permiso de edición: campos deshabilitados y sin acciones. */
  readOnly?: boolean;
  onSave: (body: CustomerConfigBody) => void;
  /** Avisa de cambios sin guardar (protección de la ficha). */
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const { t } = useI18n();
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
  const busy = saving || readOnly;
  const dirty =
    category !== (initialConfig?.customerCategory ?? "") ||
    segment !== (initialConfig?.customerSegment ?? "") ||
    zone !== (initialConfig?.salesZone ?? "") ||
    rating !== (initialConfig?.creditRating ?? "") ||
    loyalty !== (initialConfig?.loyaltyTier ?? "") ||
    invoiceFormat !== (initialConfig?.preferredInvoiceFormat ?? "") ||
    classification !== (initialConfig?.customerClassification ?? "");
  useEffect(() => onDirtyChange?.(dirty), [dirty, onDirtyChange]);

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
    <form onSubmit={handleSave}>
      {error && <ZHPageNotice variant="error" message={error} />}
      {catalogsError && <ZHPageNotice variant="error" message={catalogsError} />}
      <ZHGrid cols={2}>
        <ZHField label={t("masterdata.bp.detail.cls.category", "Categoría")}>
          <ZhSelect
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            disabled={busy || categories.loading}
          >
            <option value="">
              {categories.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.cls.unassigned", "— Sin asignar —")}
            </option>
            <PersistedValueOption value={category} loading={categories.loading} options={categories.options} />
            {categories.options.map((v) => (
              <option key={v.id} value={v.code}>
                {v.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.segment", "Segmento")}>
          <ZhSelect
            value={segment}
            onChange={(e) => setSegment(e.target.value)}
            disabled={busy || segments.loading}
          >
            <option value="">
              {segments.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.cls.unassigned", "— Sin asignar —")}
            </option>
            <PersistedValueOption value={segment} loading={segments.loading} options={segments.options} />
            {segments.options.map((v) => (
              <option key={v.id} value={v.code}>
                {v.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.zone", "Zona de ventas")}>
          <ZhTextInput
            className="zh-input"
            value={zone}
            onChange={(e) => setZone(e.target.value)}
            placeholder={t("masterdata.bp.detail.cls.zonePlaceholder", "Norte, Sur, Centro…")}
            maxLength={100}
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.rating", "Rating crediticio")}>
          <ZhSelect
            value={rating}
            onChange={(e) => setRating(e.target.value)}
            disabled={busy || creditRatings.loading}
          >
            <option value="">
              {creditRatings.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.cls.unrated", "— Sin calificar —")}
            </option>
            <PersistedValueOption value={rating} loading={creditRatings.loading} options={creditRatings.options} />
            {creditRatings.options.map((v) => (
              <option key={v.id} value={v.code}>
                {v.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.loyalty", "Nivel de fidelización")}>
          <ZhSelect
            value={loyalty}
            onChange={(e) => setLoyalty(e.target.value)}
            disabled={busy || loyaltyTiers.loading}
          >
            <option value="">{loyaltyTiers.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.cls.noTier", "— Sin nivel —")}</option>
            <PersistedValueOption value={loyalty} loading={loyaltyTiers.loading} options={loyaltyTiers.options} />
            {loyaltyTiers.options.map((v) => (
              <option key={v.id} value={v.code}>
                {v.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.invoiceFormat", "Formato de factura")}>
          <ZhSelect
            value={invoiceFormat}
            onChange={(e) => setInvoiceFormat(e.target.value)}
            disabled={busy || invoiceFormats.loading}
          >
            <option value="">
              {invoiceFormats.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.sri.noPreference", "— Sin preferencia —")}
            </option>
            <PersistedValueOption value={invoiceFormat} loading={invoiceFormats.loading} options={invoiceFormats.options} />
            {invoiceFormats.options.map((v) => (
              <option key={v.id} value={v.code}>
                {v.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.cls.classification", "Clasificación comercial")}>
          <ZhSelect
            value={classification}
            onChange={(e) => setClassification(e.target.value)}
            disabled={busy || classifications.loading}
          >
            <option value="">
              {classifications.loading ? t("masterdata.bp.detail.loadingShort", "Cargando…") : t("masterdata.bp.detail.sri.unclassified", "— Sin clasificar —")}
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
      {!readOnly && (
        <ZHFormActions
          hideCancel
          hideDraft
          saveButtonType="submit"
          disableSave={saveBlocked}
          labels={{
            save: saving ? t("masterdata.bp.detail.saving", "Guardando...") : t("masterdata.bp.detail.save", "Guardar"),
          }}
        />
      )}
    </form>
  );
}
