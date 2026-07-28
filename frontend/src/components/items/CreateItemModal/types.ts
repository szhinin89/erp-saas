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
  /**
   * Contexto de costo de la línea de compra de origen — habilita, de forma opcional y modular,
   * la sección "Información de Compra" + el simulador de precio de venta / margen. Si se omite,
   * el formulario se comporta exactamente igual que hoy (sin esta sección). Ninguno de estos
   * valores se envía al backend — son puramente informativos para la simulación en pantalla.
   */
  purchaseContext?: {
    /** Costo unitario de la factura (antes de descuento). */
    unitCost: number;
    quantity: number;
    /** Porcentaje de descuento de la línea — `undefined` = dato desconocido, nunca se asume 0. */
    discountPct?: number;
  };
}

export interface ItemCreatedResult {
  id: string;
  sku: string;
  shortName: string;
  /** Precio base con el que quedó el Item — `null` si el usuario no activó "Actualizar precio". */
  baseSalePrice?: number | null;
}

export interface CreateItemModalProps {
  open: boolean;
  initialData?: CreateItemInitialData;
  onClose: () => void;
  onCreated: (item: ItemCreatedResult) => void;
}
