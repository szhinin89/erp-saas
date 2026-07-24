import { apiGet } from '../../lib/apiEnvelope';

const BASE = '/api/v1/sales';

export interface SalesInvoiceDefaultsDto {
  defaultDocTypeCode:           string | null;
  defaultSriPaymentMethodCode:  string | null;
  defaultEmissionPointId:       string | null;
  defaultWarehouseId:           string | null;
  defaultPaymentTermId:         string | null;
  fallbackDocTypeCode:          string;
  fallbackSriPaymentMethodCode: string;
}

export const salesDefaultsService = {
  /** Precargar defaults al abrir una nueva factura de venta. */
  get: () => apiGet<SalesInvoiceDefaultsDto>(`${BASE}/invoice-defaults`),
};
