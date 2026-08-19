import { apiGet } from "../../lib/apiEnvelope";

const BASE = "/api/v1/sales";

export type DefaultWarehouseSource =
  | "BranchSetting"
  | "BranchMainWarehouse"
  | "None";

export interface SalesInvoiceDefaultsDto {
  defaultDocTypeCode: string | null;
  defaultSriPaymentMethodCode: string | null;
  defaultEmissionPointId: string | null;
  /**
   * CONFIG-FOUNDATION-P0-01: resuelto server-side (Branch OrgSetting → Warehouse.IsMain →
   * null). Si es null, NUNCA sustituir por la primera bodega de un listado — ver
   * requiresManualWarehouseSelection.
   */
  defaultWarehouseId: string | null;
  defaultPaymentTermId: string | null;
  fallbackDocTypeCode: string;
  fallbackSriPaymentMethodCode: string;
  defaultWarehouseSource: DefaultWarehouseSource;
  requiresManualWarehouseSelection: boolean;
  configurationWarnings: string[];
}

export const salesDefaultsService = {
  /** Precargar defaults al abrir una nueva factura de venta. */
  get: () => apiGet<SalesInvoiceDefaultsDto>(`${BASE}/invoice-defaults`),
};
