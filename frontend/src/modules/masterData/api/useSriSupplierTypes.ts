import { useAsync } from "../../../hooks/useAsync";
import { apiGet } from "../../lib/apiEnvelope";

export type SriSupplierTypeOption = {
  code: string;
  name: string;
};

const FALLBACK: SriSupplierTypeOption[] = [
  { code: "01", name: "Persona Natural" },
  { code: "02", name: "Sociedad" },
];

export function useSriSupplierTypes() {
  const state = useAsync(() =>
    apiGet<SriSupplierTypeOption[]>("/api/v1/catalog/sri-supplier-types").catch(
      () => FALLBACK,
    ),
  );
  return { options: state.data ?? FALLBACK, loading: state.loading };
}
