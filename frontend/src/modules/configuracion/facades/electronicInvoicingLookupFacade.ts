/**
 * electronicInvoicingLookupFacade — contrato público de tipos de facturación electrónica para
 * consumidores externos (sales: estado de conectividad SRI). Solo re-exporta el tipo; la
 * consulta sigue viviendo en configuracion/facturacionElectronica/api/electronicInvoicingService.
 */
export type { ElectronicInvoicingStatusDto } from "../facturacionElectronica/api/electronicInvoicingService";
