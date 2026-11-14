/**
 * supplierExpenseSubcategoryLookupFacade — superficie pública read-only de las subcategorías de
 * gasto permitidas por proveedor y empresa activa (EXPENSE-SUPPLIER-SUBCATEGORIES-01) para
 * consumidores externos (expenses: formulario de documentos de gasto, manual y desde XML).
 *
 * Expone únicamente la lectura (`GET`); el modo y las subcategorías disponibles los calcula el
 * servidor. La configuración (`PUT`) vive solo en la ficha del proveedor. Los módulos externos deben
 * importar desde aquí, nunca directamente de masterData/api/businessPartnerFacade.
 */
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  SupplierExpenseSubcategoriesDto,
  SupplierExpenseSubcategoryMode,
} from "../types/businessPartner.types";

export type { SupplierExpenseSubcategoriesDto, SupplierExpenseSubcategoryMode };

export const supplierExpenseSubcategoryLookupFacade = {
  get: businessPartnerFacade.getExpenseSubcategories,
};
