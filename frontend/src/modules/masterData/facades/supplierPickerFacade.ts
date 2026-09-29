/**
 * supplierPickerFacade — superficie pública del buscador ÚNICO de proveedores registrados
 * (ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02) para formularios y filtros de otros módulos
 * (purchases, expenses, payables, finance, items, reportes, supplier-payments).
 *
 * Los módulos externos deben importar el componente y el tipo de fila desde aquí, nunca
 * directamente de masterData/components o masterData/types.
 */
import type { SupplierPickerRow } from "../types/businessPartner.types";

export { SupplierSearchSelect } from "../components/SupplierSearchSelect";
export type { SupplierSearchSelectProps } from "../components/SupplierSearchSelect";
export type { SupplierPickerRow };
