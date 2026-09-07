import { useAsync } from "../../../hooks/useAsync";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type { SriRetentionCodeLookup } from "../../items/facades/sriLookupFacade";

export type SriRetentionCodeOption = SriRetentionCodeLookup;

// Sin fallback hardcodeado a propósito: el catálogo oficial SRI de códigos de retención
// (sri_retention_code) vive en backend/BD, fuente única en catalogService. Duplicar sus
// códigos aquí y sustituirlos silenciosamente ante error de red mostraría datos fiscales
// potencialmente desincronizados sin que el usuario lo note. Mismo patrón que
// useSriPaymentMethods.ts / useSriSupplierTypes.ts.
export function useSriRetentionCodes(taxType: "IVA" | "RENTA") {
  const state = useAsync(() => sriLookupFacade.retentionCodes(taxType), true, [taxType]);
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}
