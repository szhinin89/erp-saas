import { useAsync } from "../../../hooks/useAsync";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type { SriIdTypeLookup } from "../../items/facades/sriLookupFacade";

export type SriIdTypeOption = SriIdTypeLookup;

// Sin fallback hardcodeado a propósito: es el catálogo oficial SRI (sri_id_type), fuente
// única en TaxIdentification/backend. Un fallback local duplicaría esos datos y, ante una
// falla de red o un catálogo actualizado en BD, mostraría opciones desincronizadas o
// inválidas para el filtro por uso (customer/supplier) sin que el usuario lo note. Ante
// error o carga, se expone lista vacía — igual que useLegalEntityTypes — y el consumidor
// deshabilita/muestra "Cargando…" mientras tanto.
export function useSriIdTypes() {
  const state = useAsync(() => sriLookupFacade.idTypes());
  return { options: state.data ?? [], loading: state.loading };
}

export function useSriIdTypesByUsage(usage: string) {
  const state = useAsync(() => sriLookupFacade.idTypes(usage), true, [usage]);
  return { options: state.data ?? [], loading: state.loading };
}

export function getSriIdTypeName(
  code: string,
  options: SriIdTypeOption[] = [],
): string {
  return options.find((o) => o.code === code)?.name ?? code;
}
