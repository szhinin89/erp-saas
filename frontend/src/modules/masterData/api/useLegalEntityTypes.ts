import { useEffect, useState } from "react";
import { apiGet } from "../../lib/apiEnvelope";

export type LegalEntityTypeDto = {
  code: number;
  name: string;
  sriTaxCategory: string;
  isActive: boolean;
};

export function useLegalEntityTypes() {
  const [options, setOptions] = useState<LegalEntityTypeDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiGet<LegalEntityTypeDto[]>("/api/v1/catalog/legal-entity-types")
      .then((data) => setOptions(data))
      .finally(() => setLoading(false));
  }, []);

  return {
    options,
    loading,
  };
}