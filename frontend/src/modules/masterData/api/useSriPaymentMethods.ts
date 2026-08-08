import { useAsync } from "../../../hooks/useAsync";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type { SriPaymentMethodLookup } from "../../items/facades/sriLookupFacade";

export type SriPaymentMethodOption = SriPaymentMethodLookup;

// Sin fallback hardcodeado a propósito: el catálogo oficial SRI de formas de pago
// (Tabla 6, sri_payment_method) vive en backend/BD, fuente única en catalogService.
// Duplicar sus códigos aquí y sustituirlos silenciosamente ante error de red mostraría
// datos fiscales potencialmente desincronizados sin que el usuario lo note. Ver
// useSriIdTypes.ts / useSriSupplierTypes.ts para el mismo patrón.
export function useSriPaymentMethods() {
  const state = useAsync(() => sriLookupFacade.paymentMethods());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}
