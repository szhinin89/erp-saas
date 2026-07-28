/**
 * Datos de precarga opcionales — el componente no sabe de dónde vienen (Compras, Inventario,
 * Ventas, Importaciones...). Quien lo invoca decide qué prellenar.
 */
export interface CreateItemInitialData {
  name?: string;
  /** Valor del código de barras a prellenar (el llamador decide su origen — p. ej. código auxiliar de un XML). */
  barcode?: string;
  /** Código de proveedor, solo informativo salvo que también se envíe `supplierId`. */
  supplierCode?: string;
  /** Nombre del proveedor — solo para mostrar; no viaja al backend. */
  supplierName?: string;
  /** Proveedor real (BusinessPartner.Id) — si viene junto con `supplierCode`, se crea el ItemSupplierCode en el mismo alta del Item. */
  supplierId?: string;
  /** Código SRI de unidad de medida por defecto (p. ej. "UNIT") — el sistema no usa un Id, usa código. */
  defaultUomCode?: string;
  source?: 'PurchaseReception' | 'Manual';
}

export interface ItemCreatedResult {
  id: string;
  sku: string;
  shortName: string;
}

export interface CreateItemModalProps {
  open: boolean;
  initialData?: CreateItemInitialData;
  onClose: () => void;
  onCreated: (item: ItemCreatedResult) => void;
}
