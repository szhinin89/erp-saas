import { useEffect, useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZHField,
  ZHGrid,
  ZHToggle,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type {
  SupplierConfigBody,
  SupplierRoleConfigDto,
} from "../types/businessPartner.types";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import { useSriPaymentMethods } from "../api/useSriPaymentMethods";
import { useSriTaxSupportCodes } from "../api/useSriTaxSupportCodes";

/**
 * Un valor guardado que ya no exista o esté inactivo en el catálogo no se descarta ni se
 * reemplaza silenciosamente (perdería el dato real sin que el usuario lo note) — se detecta
 * aquí para mostrar una opción sintética deshabilitada + fieldError, exigiendo reselección.
 */
function isOrphanCatalogValue(
  value: string,
  options: readonly { code: string }[],
  loading: boolean,
): boolean {
  return value !== "" && !loading && !options.some((o) => o.code === value);
}

/**
 * Formulario de Config SRI operativa del proveedor (S3-A: incluye método de pago + exención).
 * Extraído de MasterDataSuppliersPage sin cambio de comportamiento; el guardado sigue
 * delegado en `onSave` (reemplazo completo de la configuración SRI).
 * `readOnly` (sin permiso de edición) y `onDirtyChange` (avisa de cambios sin guardar) son de la ficha.
 */
export function SupplierSriConfigForm({
  initialConfig,
  saving,
  error,
  readOnly = false,
  onSave,
  onDirtyChange,
}: {
  initialConfig: SupplierRoleConfigDto | null;
  saving: boolean;
  error?: string | null;
  readOnly?: boolean;
  onSave: (body: SupplierConfigBody) => void;
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const { t } = useI18n();
  const [taxSupportCode, setTaxSupportCode] = useState(
    initialConfig?.defaultTaxSupportCode ?? "",
  );
  const [paymentMethodCode, setPaymentMethodCode] = useState(
    initialConfig?.defaultPaymentMethodCode ?? "",
  );
  const [refundProviderType, setRefundProviderType] = useState(
    initialConfig?.refundProviderTypeCode ?? "",
  );
  const [isRetentionExempt, setIsRetentionExempt] = useState(
    initialConfig?.isRetentionExempt ?? false,
  );
  const [isRequiredToKeepAccounting, setIsRequiredToKeepAccounting] =
    useState(initialConfig?.isRequiredToKeepAccounting ?? false);
  const { options: supplierTypeOptions } = useSriSupplierTypes();
  const {
    options: paymentMethodOptions,
    loading: loadingPaymentMethods,
    error: paymentMethodsError,
  } = useSriPaymentMethods();
  const {
    options: taxSupportOptions,
    loading: loadingTaxSupport,
    error: taxSupportFetchError,
  } = useSriTaxSupportCodes();

  const busy = saving || readOnly;
  const dirty =
    taxSupportCode !== (initialConfig?.defaultTaxSupportCode ?? "") ||
    paymentMethodCode !== (initialConfig?.defaultPaymentMethodCode ?? "") ||
    refundProviderType !== (initialConfig?.refundProviderTypeCode ?? "") ||
    isRetentionExempt !== (initialConfig?.isRetentionExempt ?? false) ||
    isRequiredToKeepAccounting !==
      (initialConfig?.isRequiredToKeepAccounting ?? false);
  useEffect(() => onDirtyChange?.(dirty), [dirty, onDirtyChange]);

  const taxSupportOrphan = isOrphanCatalogValue(
    taxSupportCode,
    taxSupportOptions,
    loadingTaxSupport,
  );

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSave({
          defaultTaxSupportCode: taxSupportCode || null,
          defaultPaymentMethodCode: paymentMethodCode || null,
          refundProviderTypeCode: refundProviderType || null,
          isRetentionExempt,
          isRequiredToKeepAccounting,
        });
      }}
    >
      {error && <ZHPageNotice variant="error" message={error} />}
      <ZHGrid cols={1}>
        <ZHField
          label={t("masterdata.bp.detail.sri.taxSupport", "Sustento tributario predeterminado")}
          fieldError={
            taxSupportFetchError ??
            (taxSupportOrphan
              ? t("masterdata.bp.detail.sri.orphanMessage", "Este código ya no está activo en el catálogo. Selecciona una opción válida.")
              : undefined)
          }
        >
          <ZhSelect
            value={taxSupportCode}
            onChange={(e) => setTaxSupportCode(e.target.value)}
            disabled={busy || loadingTaxSupport}
          >
            <option value="">{t("masterdata.bp.detail.sri.undefined", "— Sin definir —")}</option>
            {loadingTaxSupport ? (
              <option value="">{t("masterdata.bp.detail.loadingShort", "Cargando…")}</option>
            ) : (
              <>
                {taxSupportOrphan && (
                  <option value={taxSupportCode} disabled>
                    {t("masterdata.bp.detail.sri.orphanOption", { code: taxSupportCode })}
                  </option>
                )}
                {taxSupportOptions.map((o) => (
                  <option key={o.code} value={o.code}>
                    {o.code} — {o.name}
                  </option>
                ))}
              </>
            )}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("masterdata.bp.detail.sri.paymentMethod", "Método de pago SRI")}
          fieldError={paymentMethodsError ?? undefined}
        >
          <ZhSelect
            value={paymentMethodCode}
            onChange={(e) => setPaymentMethodCode(e.target.value)}
            disabled={busy || loadingPaymentMethods}
          >
            <option value="">{t("masterdata.bp.detail.sri.noPreference", "— Sin preferencia —")}</option>
            {loadingPaymentMethods ? (
              <option value="">{t("masterdata.bp.detail.loadingShort", "Cargando…")}</option>
            ) : (
              paymentMethodOptions.map((o) => (
                <option key={o.code} value={o.code}>
                  {o.code} — {o.name}
                </option>
              ))
            )}
          </ZhSelect>
        </ZHField>
        <ZHField label={t("masterdata.bp.detail.sri.providerType", "Tipo de Proveedor")}>
          <ZhSelect
            value={refundProviderType}
            onChange={(e) => setRefundProviderType(e.target.value)}
            disabled={busy}
          >
            <option value="">{t("masterdata.bp.detail.sri.unclassified", "— Sin clasificar —")}</option>
            {supplierTypeOptions.map((o) => (
              <option key={o.code} value={o.code}>
                {o.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHToggle
          label={t("masterdata.bp.detail.sri.retentionExempt", "Exento de retención")}
          description={t("masterdata.bp.detail.sri.retentionExemptHint", "RISE / Microempresa / Sector publico")}
          value={isRetentionExempt}
          onChange={setIsRetentionExempt}
          disabled={busy}
        />
        <ZHToggle
          label={t("masterdata.bp.detail.sri.keepAccounting", "Obligado a llevar contabilidad")}
          description={t("masterdata.bp.detail.sri.keepAccountingHint", "Dato tributario del proveedor. Por ahora se usa como información para evaluación de retenciones.")}
          value={isRequiredToKeepAccounting}
          onChange={setIsRequiredToKeepAccounting}
          disabled={busy}
        />
      </ZHGrid>
      {!readOnly && (
        <ZHFormActions
          hideCancel
          hideDraft
          saveButtonType="submit"
          disableSave={saving}
          labels={{
            save: saving ? t("masterdata.bp.detail.saving", "Guardando...") : t("masterdata.bp.detail.save", "Guardar"),
          }}
        />
      )}
    </form>
  );
}
