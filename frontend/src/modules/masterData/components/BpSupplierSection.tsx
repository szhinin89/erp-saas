import { useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BusinessPartnerRoleDto,
  SupplierConfigBody,
} from "../types/businessPartner.types";
import { SupplierSriConfigForm } from "./SupplierSriConfigForm";
import { SupplierPurchaseDefaultSection } from "./SupplierPurchaseDefaultSection";
import { SupplierRetentionDefaultsSection } from "./SupplierRetentionDefaultsSection";
import { SupplierExpenseSubcategoriesSection } from "./SupplierExpenseSubcategoriesSection";

/**
 * Pestañas de configuración de la ficha de Proveedor (`part`: SRI, compras, retenciones o subcategorías
 * de gasto; solo con rol Supplier activo): cada pestaña monta su componente con guardado independiente.
 * Permisos reales:
 * `businesspartners.update` → configuración SRI; `…configure-company` → condición de compras y
 * retenciones predeterminadas (las subcategorías de gasto aplican su propia lectura/edición).
 * Sin permiso la sección se muestra en solo lectura.
 */
export type BpSupplierPart = "sri" | "purchases" | "retentions" | "expenseSubcategories";

export function BpSupplierSection({
  part,
  bpId,
  role,
  canUpdate,
  canConfigure,
  onReload,
  onDirtyChange,
}: {
  part: BpSupplierPart;
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
  // Cada guardado exitoso remonta el formulario con lo persistido (base limpia).
  const [saveVersion, setSaveVersion] = useState(0);

  const handleSave = async (body: SupplierConfigBody) => {
    setSaving(true);
    setError(null);
    try {
      await businessPartnerFacade.updateSupplierConfig(bpId, role.id, body);
      message.success(
        t("masterdata.bp.detail.supplier.saved", "Configuración del proveedor guardada correctamente."),
      );
      await onReload();
      setSaveVersion((v) => v + 1);
    } catch (err) {
      setError(
        formatApiRequestError(err, { generic: "Error al procesar la operación." }),
      );
    } finally {
      setSaving(false);
    }
  };

  const readOnlyPart =
    part === "sri" ? !canUpdate : part === "expenseSubcategories" ? false : !canConfigure;

  return (
    <section className="md-detail-section">
      {readOnlyPart && (
        <ZHPageNotice
          variant="info"
          message={t(
            "masterdata.bp.detail.readOnly",
            "Solo lectura: no tienes permiso para modificar esta sección.",
          )}
        />
      )}
      {part === "sri" && (
        <div className="md-bp-config-section">
          <h4>{t("masterdata.bp.detail.supplier.sri", "Configuración SRI")}</h4>
          {/* La clave remonta el formulario con la configuración persistida tras guardar (base limpia). */}
          <SupplierSriConfigForm
            key={`${JSON.stringify(role.supplierConfig)}-${saveVersion}`}
            initialConfig={role.supplierConfig}
            saving={saving}
            error={error}
            readOnly={!canUpdate}
            onSave={(body) => void handleSave(body)}
            onDirtyChange={(d) => onDirtyChange("supplier-sri", d)}
          />
        </div>
      )}
      {part === "purchases" && (
        <div className="md-bp-config-section">
          <SupplierPurchaseDefaultSection
            bpId={bpId}
            readOnly={!canConfigure}
            onDirtyChange={(d) => onDirtyChange("supplier-purchase", d)}
          />
        </div>
      )}
      {part === "retentions" && (
        <div className="md-bp-config-section">
          <SupplierRetentionDefaultsSection
            bpId={bpId}
            disabled={saving}
            readOnly={!canConfigure}
          />
        </div>
      )}
      {part === "expenseSubcategories" && (
        <div className="md-bp-config-section">
          <SupplierExpenseSubcategoriesSection bpId={bpId} disabled={saving} />
        </div>
      )}
    </section>
  );
}
