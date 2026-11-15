import { useRef, useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useCompanyScopedAsync } from "../../../hooks/useCompanyScopedAsync";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BusinessPartnerRoleDto,
  CustomerConfigBody,
} from "../types/businessPartner.types";
import { CustomerClassificationForm } from "./CustomerClassificationForm";
import { CustomerSalesSettingsForm } from "./CustomerSalesSettingsForm";

/**
 * Pestañas de configuración de la ficha de Cliente (`part`: clasificación o condiciones de ventas;
 * solo con rol Customer activo): montan los mismos formularios que los modales del listado, con
 * guardado independiente. Permisos reales: `businesspartners.update` →
 * clasificación; `…configure-company` → condición de ventas de la empresa activa (se consulta con
 * `useCompanyScopedAsync`, así que sigue el cambio de empresa). Sin permiso, solo lectura.
 */
export type BpCustomerPart = "classification" | "sales";

export function BpCustomerSection({
  part,
  bpId,
  role,
  canUpdate,
  canConfigure,
  onReload,
  onDirtyChange,
}: {
  part: BpCustomerPart;
  bpId: string;
  role: BusinessPartnerRoleDto;
  canUpdate: boolean;
  canConfigure: boolean;
  onReload: () => Promise<void>;
  onDirtyChange: (key: string, dirty: boolean) => void;
}) {
  const { t } = useI18n();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const salesSubmitting = useRef(false);
  // Cada guardado exitoso remonta el formulario con lo persistido (base limpia).
  const [classificationVersion, setClassificationVersion] = useState(0);
  const [salesVersion, setSalesVersion] = useState(0);
  const sales = useCompanyScopedAsync(
    () => businessPartnerFacade.getSalesSettings(bpId),
    part === "sales",
    [bpId],
  );

  // El PATCH reemplaza la configuración completa del rol: el formulario parte de la persistida.
  const handleSaveClassification = async (body: CustomerConfigBody) => {
    setSaving(true);
    setError(null);
    try {
      await businessPartnerFacade.updateCustomerConfig(bpId, role.id, body);
      message.success(
        t("masterdata.bp.detail.customer.saved", "Clasificación del cliente guardada correctamente."),
      );
      await onReload();
      setClassificationVersion((v) => v + 1);
    } catch (err) {
      setError(
        formatApiRequestError(err, { generic: "Error al procesar la operación." }),
      );
    } finally {
      setSaving(false);
    }
  };

  const handleSaveSales = async (body: { paymentTermId: string | null }) => {
    setSaving(true);
    try {
      await businessPartnerFacade.upsertSalesSettings(bpId, body);
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="md-detail-section">
      {(part === "sales" ? !canConfigure : !canUpdate) && (
        <ZHPageNotice
          variant="info"
          message={t(
            "masterdata.bp.detail.readOnly",
            "Solo lectura: no tienes permiso para modificar esta sección.",
          )}
        />
      )}
      {part === "classification" && (
        <div className="md-bp-config-section">
          <h4>{t("masterdata.bp.detail.customer.classification", "Clasificación comercial")}</h4>
          {/* La clave remonta el formulario con la configuración persistida tras guardar (base limpia). */}
          <CustomerClassificationForm
            key={`${JSON.stringify(role.customerConfig)}-${classificationVersion}`}
            initialConfig={role.customerConfig}
            saving={saving}
            error={error}
            readOnly={!canUpdate}
            onSave={(body) => void handleSaveClassification(body)}
            onDirtyChange={(d) => onDirtyChange("customer-classification", d)}
          />
        </div>
      )}
      {part === "sales" && (
        <div className="md-bp-config-section">
          <h4>{t("masterdata.bp.detail.customer.sales", "Condición predeterminada para ventas")}</h4>
          {sales.error ? (
            <ZHPageNotice variant="error" message={sales.error} />
          ) : sales.loading && !sales.data ? (
            <p className="md-supplier-section-hint">
              {t("masterdata.bp.detail.loading", "Cargando…")}
            </p>
          ) : (
            <CustomerSalesSettingsForm
              key={`${sales.data?.paymentTermId ?? ""}-${salesVersion}`}
              initialSettings={sales.data}
              saving={saving}
              submittingRef={salesSubmitting}
              readOnly={!canConfigure}
              onSaved={() => {
                setSalesVersion((v) => v + 1);
                sales.refetch();
              }}
              onSave={handleSaveSales}
              onDirtyChange={(d) => onDirtyChange("customer-sales", d)}
            />
          )}
        </div>
      )}
    </section>
  );
}
