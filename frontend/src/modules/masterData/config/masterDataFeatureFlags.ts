import { usePermissionsStore } from '../../../store/permissionsStore';

/** Claves en `enabledFeatures` (entitlements / plan SaaS). */
export const MASTERDATA_CUSTOMERS_FLAG = 'masterdata.customers.enabled';
export const MASTERDATA_SUPPLIERS_FLAG = 'masterdata.suppliers.enabled';

export type MasterDataFeatureFlags = {
  customers: boolean;
  suppliers: boolean;
};

function normalizeFeatureSet(enabledFeatures: string[]): Set<string> {
  return new Set(enabledFeatures.map((f) => f.trim().toLowerCase()));
}

export function readMasterDataFeatureFlags(enabledFeatures: string[]): MasterDataFeatureFlags {
  const set = normalizeFeatureSet(enabledFeatures);
  return {
    customers: set.has(MASTERDATA_CUSTOMERS_FLAG.toLowerCase()),
    suppliers: set.has(MASTERDATA_SUPPLIERS_FLAG.toLowerCase()),
  };
}

/** Lectura fuera de React (facade, servicios). */
export function getMasterDataFeatureFlags(): MasterDataFeatureFlags {
  const { enabledFeatures } = usePermissionsStore.getState();
  return readMasterDataFeatureFlags(enabledFeatures);
}

export function isMasterDataCustomersEnabled(): boolean {
  return getMasterDataFeatureFlags().customers;
}

export function isMasterDataSuppliersEnabled(): boolean {
  return getMasterDataFeatureFlags().suppliers;
}
