import { useMemo } from "react";
import { useConfig } from "./ConfigContext";

export function useFeatureFlag(
  featureKey: string,
  module?: string | null,
  feature?: string | null,
): boolean {
  const { getValue } = useConfig();
  return useMemo(() => {
    return Boolean(getValue<boolean>(featureKey, module, feature, false));
  }, [feature, featureKey, getValue, module]);
}
