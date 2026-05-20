/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { fetchDeploymentConfig, type DeploymentInfo } from './deploymentConfig';

type DeploymentState = DeploymentInfo & { loaded: boolean };

const DeploymentContext = createContext<DeploymentState | null>(null);

export function DeploymentProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<DeploymentState>({
    loaded: false,
    superAdminPanelEnabled: true,
    maxActiveTenants: null,
    maxIdentityUsers: null,
    dedicatedSingleClientInstance: false,
    maxUsersPerTenant: null,
  });

  useEffect(() => {
    let cancelled = false;
    void fetchDeploymentConfig().then((info) => {
      if (!cancelled) {
        setState({
          loaded: true,
          superAdminPanelEnabled: info.superAdminPanelEnabled,
          maxActiveTenants: info.maxActiveTenants ?? null,
          maxIdentityUsers: info.maxIdentityUsers ?? null,
          dedicatedSingleClientInstance: info.dedicatedSingleClientInstance ?? false,
          maxUsersPerTenant: info.maxUsersPerSubscriber ?? null,
        });
      }
    });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!state.loaded) return null;

  return <DeploymentContext.Provider value={state}>{children}</DeploymentContext.Provider>;
}

export function useDeployment(): DeploymentState {
  const ctx = useContext(DeploymentContext);
  if (!ctx) throw new Error('useDeployment debe usarse dentro de DeploymentProvider');
  return ctx;
}
