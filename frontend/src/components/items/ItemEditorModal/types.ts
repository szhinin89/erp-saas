/** 'create' cuando la línea aún no tiene Item; 'update' cuando ya hay uno vinculado (se resuelve por la presencia de `itemId` en `ItemEditorModalProps`, nunca se declara por separado para que no pueda contradecirse). */
export type ItemEditorMode = "create" | "update";

/**
 * Datos de precarga opcionales — el componente no sabe de dónde vienen (Compras, Inventario,
 * Ventas, Importaciones...). Quien lo invoca decide qué prellenar. Se usan en ambos modos.
 */
export interface CreateItemInitialData {
  name?: string;
  /** Valor del código de barras a prellenar (el llamador decide su origen — p. ej. código auxiliar de un XML). Solo aplica en modo crear — Update no gestiona códigos de barras (recurso independiente del Item). */
  barcode?: string;
  /** Código de proveedor, solo informativo salvo que también se envíe `supplierId`. */
  supplierCode?: string;
  /** Nombre del proveedor — solo para mostrar; no viaja al backend. */
  supplierName?: string;
  /** Proveedor real (BusinessPartner.Id) — si viene junto con `supplierCode`, se crea el ItemSupplierCode en el mismo alta del Item (solo modo crear). */
  supplierId?: string;
  /** Código SRI de unidad de medida por defecto (p. ej. "UNIT") — el sistema no usa un Id, usa código. */
  defaultUomCode?: string;
  source?: "PurchaseReception" | "Manual";
  /**
   * Contexto de costo de la línea de compra de origen — habilita, de forma opcional y modular,
   * la sección "Información de Compra" + el simulador de precio de venta / margen, en ambos
   * modos (crear y actualizar). Si se omite, el formulario se comporta exactamente igual que sin
   * esta sección. Ninguno de estos valores se envía al backend — son puramente informativos.
   */
  purchaseContext?: {
    /** Costo unitario de la factura (antes de descuento). */
    unitCost: number;
    quantity: number;
    /** Porcentaje de descuento de la línea — `undefined` = dato desconocido, nunca se asume 0. */
    discountPct?: number;
    /** Código IVA de la línea de compra (catálogo SRI) — resuelve el bloque "IVA XML" de referencia. `undefined` = desconocido, se muestra "—". */
    vatCode?: string;
  };
}

export interface ItemCreatedResult {
  id: string;
  sku: string;
  shortName: string;
  /** Precio base con el que quedó el Item — `null` si no se activó "Actualizar precio". */
  baseSalePrice?: number | null;
  /** IVA de compra con el que quedó el Item — permite al llamador (p. ej. una línea de compra) reflejarlo sin volver a consultar el Item. */
  purchaseVatCode?: string | null;
}

export interface ItemEditorModalProps {
  open: boolean;
  /** Presente → modo Actualizar (precarga el Item existente); ausente → modo Crear. */
  itemId?: string;
  initialData?: CreateItemInitialData;
  onClose: () => void;
  /** Se dispara tanto al crear como al actualizar correctamente. */
  onSaved: (item: ItemCreatedResult) => void;
}
