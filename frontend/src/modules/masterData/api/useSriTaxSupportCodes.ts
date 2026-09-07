import { useAsync } from "../../../hooks/useAsync";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type { SriTaxSupportLookup } from "../../items/facades/sriLookupFacade";

export type SriTaxSupportOption = SriTaxSupportLookup;

// Sin fallback hardcodeado a propósito: el catálogo oficial SRI de sustento tributario
// (sri_tax_support) vive en backend/BD, fuente única en catalogService. Duplicar sus
// códigos aquí y sustituirlos silenciosamente ante error de red mostraría datos fiscales
// potencialmente desincronizados sin que el usuario lo note. Mismo patrón que
// useSriPaymentMethods.ts / useSriSupplierTypes.ts.
export function useSriTaxSupportCodes() {
  const state = useAsync(() => sriLookupFacade.taxSupportCodes());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}
