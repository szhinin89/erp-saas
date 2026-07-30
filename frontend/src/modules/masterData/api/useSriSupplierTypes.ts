import { useAsync } from "../../../hooks/useAsync";
import { apiGet } from "../../lib/apiEnvelope";

export type SriSupplierTypeOption = {
  code: string;
  name: string;
};

// Sin fallback hardcodeado a propósito: "01"/"02" son el catálogo oficial SRI (Tabla 26,
// Tipo Proveedor de Reembolso — sri_supplier_type), fuente única en SriSupplierType/backend.
// Duplicar sus nombres aquí y sustituirlos silenciosamente ante error de red mostraría datos
// fiscales potencialmente desincronizados sin que el usuario lo note. Ver useSriIdTypes.ts.
export function useSriSupplierTypes() {
  const state = useAsync(() =>
    apiGet<SriSupplierTypeOption[]>("/api/v1/catalog/sri-supplier-types"),
  );
  return { options: state.data ?? [], loading: state.loading };
}
