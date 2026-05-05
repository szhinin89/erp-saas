import { type ReactNode } from 'react';
import { useFeatureFlag } from './useFeatureFlag';

type FeatureGateProps = {
  feature: string;
  module?: string;
  featureScope?: string;
  fallback?: ReactNode;
  children: ReactNode;
};

export function FeatureGate(props: FeatureGateProps) {
  const { feature, module, featureScope, fallback = null, children } = props;
  const enabled = useFeatureFlag(feature, module, featureScope);
  if (!enabled) return <>{fallback}</>;
  return <>{children}</>;
}

