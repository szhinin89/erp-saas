import { useAsync } from "../../../hooks/useAsync";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type { SriSupplierTypeLookup } from "../../items/facades/sriLookupFacade";

export type SriSupplierTypeOption = SriSupplierTypeLookup;

// Sin fallback hardcodeado a propósito: "01"/"02" son el catálogo oficial SRI (Tabla 26,
// Tipo Proveedor de Reembolso — sri_supplier_type), fuente única en SriSupplierType/backend.
// Duplicar sus nombres aquí y sustituirlos silenciosamente ante error de red mostraría datos
// fiscales potencialmente desincronizados sin que el usuario lo note. Ver useSriIdTypes.ts.
export function useSriSupplierTypes() {
  const state = useAsync(() => sriLookupFacade.supplierTypes());
  return { options: state.data ?? [], loading: state.loading };
}
