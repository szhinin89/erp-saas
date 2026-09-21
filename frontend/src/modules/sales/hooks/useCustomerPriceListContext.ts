import { useEffect, useState } from "react";
import {
  customerPriceListContextService,
  type CustomerPriceListContextDto,
} from "../api/customerPriceListContextService";

export type CustomerPriceListContextState = {
  status: "idle" | "loading" | "ready" | "error";
  data: CustomerPriceListContextDto | null;
};

const IDLE: CustomerPriceListContextState = { status: "idle", data: null };

/**
 * SALES-PRICING-UX-TRACEABILITY-07C: contexto LIVE de listas del cliente (solo venta nueva/Draft
 * editable). Se refresca cuando cambia `customerId`. Un error deja un estado neutro ("error") —
 * nunca lanza, nunca toca precios ni bloquea la venta. Ignora respuestas de un cliente anterior.
 */
export function useCustomerPriceListContext(
  customerId: string | null | undefined,
  enabled: boolean,
): CustomerPriceListContextState {
  const [state, setState] = useState<CustomerPriceListContextState>(IDLE);

  useEffect(() => {
    if (!enabled || !customerId) {
      setState(IDLE);
      return;
    }
    let cancelled = false;
    setState({ status: "loading", data: null });
    customerPriceListContextService
      .get(customerId)
      .then((data) => {
        if (!cancelled) setState({ status: "ready", data });
      })
      .catch(() => {
        if (!cancelled) setState({ status: "error", data: null });
      });
    return () => {
      cancelled = true;
    };
  }, [customerId, enabled]);

  return state;
}
