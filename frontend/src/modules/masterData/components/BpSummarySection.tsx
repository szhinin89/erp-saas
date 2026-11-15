import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { describeCatalogCode } from "../api/catalogDescription";
import { useSriIdTypes } from "../api/useSriIdTypes";
import { useSriTaxSupportCodes } from "../api/useSriTaxSupportCodes";
import { useCustomerCategories } from "../api/useClassificationCatalogs";
import type {
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
} from "../types/businessPartner.types";
import { BpDetailRow } from "./BpDetailRow";
import type { BpContext } from "./bpDetailTabs";

/**
 * Pestaña «Resumen» de la ficha: identidad, conteos y datos clave (estado y roles activos van en la cabecera de la ficha). Solo lectura,
 * derivada de los datos ya cargados por la ficha (sin consultas propias ni estado duplicado).
 */
export function BpSummarySection({
  context,
  bp,
  locations,
  contacts,
}: {
  /** Solo se muestran los datos del rol del módulo desde el que se administra. */
  context: BpContext;
  bp: BusinessPartnerDetailDto;
  locations: BpLocationDto[];
  contacts: BpContactDto[];
}) {
  const { t } = useI18n();
  const { options: idTypes, loading: idLoading } = useSriIdTypes();
  const taxSupport = useSriTaxSupportCodes();
  const categories = useCustomerCategories();
  const unknown = (c: string) => t("masterdata.bp.detail.unknownCatalog", { code: c });
  const activeRoles = bp.roles.filter((r) => r.isActive);
  const activeLocations = locations.filter((l) => l.isActive);
  const activeContacts = contacts.filter((c) => c.isActive);
  const primaryLocation = activeLocations.find((l) => l.isPrimary);
  const primaryContact = activeContacts.find((c) => c.isPrimary);
  const supplier = activeRoles.find((r) => r.roleType === "Supplier");
  const customer = activeRoles.find((r) => r.roleType === "Customer");
  const primaryLabel = t("masterdata.bp.detail.summary.primary", "Principal");

  return (
    <section className="md-detail-section">
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.identification", "Identificación")}
        value={[describeCatalogCode(bp.identificationType, idTypes, idLoading, unknown), bp.identificationNumber].filter(Boolean).join(" · ")}
      />
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.legalName", "Razón social")}
        value={bp.legalName}
      />
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.tradeName", "Nombre comercial")}
        value={bp.tradeName}
      />
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.locations", "Ubicaciones activas")}
        value={`${activeLocations.length}${primaryLocation ? ` · ${primaryLabel}: ${primaryLocation.name}` : ""}`}
      />
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.contacts", "Contactos activos")}
        value={`${activeContacts.length}${primaryContact ? ` · ${primaryLabel}: ${primaryContact.fullName}` : ""}`}
      />
      {context === "supplier" && supplier && (
        <BpDetailRow
          label={t("masterdata.bp.detail.summary.supplierTaxSupport", "Proveedor · sustento tributario")}
          value={describeCatalogCode(supplier.supplierConfig?.defaultTaxSupportCode, taxSupport.options, taxSupport.loading, unknown)}
        />
      )}
      {context === "customer" && customer && (
        <BpDetailRow
          label={t("masterdata.bp.detail.summary.customerCategory", "Cliente · categoría")}
          value={describeCatalogCode(customer.customerConfig?.customerCategory, categories.options, categories.loading, unknown)}
        />
      )}
      {(context === "supplier" ? supplier : customer)?.notes && (
        <BpDetailRow
          label={t("masterdata.bp.detail.summary.roleNotes", "Notas del rol")}
          value={(context === "supplier" ? supplier : customer)?.notes ?? null}
        />
      )}
      <BpDetailRow
        label={t("masterdata.bp.detail.summary.createdAt", "Creado")}
        value={formatDateTime(bp.createdAt)}
      />
    </section>
  );
}
