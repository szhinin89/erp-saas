import { describe, expect, it, vi, beforeEach } from 'vitest';

function createStorage(): Storage {
  const map = new Map<string, string>();
  return {
    get length() {
      return map.size;
    },
    clear: () => map.clear(),
    getItem: (key: string) => (map.has(key) ? map.get(key)! : null),
    key: (index: number) => Array.from(map.keys())[index] ?? null,
    removeItem: (key: string) => map.delete(key),
    setItem: (key: string, value: string) => map.set(key, value),
  };
}

vi.stubGlobal('localStorage', createStorage());
vi.stubGlobal('sessionStorage', createStorage());

import { syncSessionEntitlements } from './syncSessionEntitlements';import { usePermissionsStore } from '../store/permissionsStore';

vi.mock('../modules/auth/api/entitlementsService', () => ({
  entitlementsService: {
    getMe: vi.fn(),
  },
}));

vi.mock('../modules/auth/api/accessService', () => ({
  accessService: {
    getMyPermissions: vi.fn(),
  },
}));

import { entitlementsService } from '../modules/auth/api/entitlementsService';
import { accessService } from '../modules/auth/api/accessService';

describe('syncSessionEntitlements', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    usePermissionsStore.getState().clearPermissions();
    vi.clearAllMocks();
  });
  it('stores enabledModules from entitlements snapshot only', async () => {
    vi.mocked(entitlementsService.getMe).mockResolvedValue({
      planCode: 'starter',
      planName: 'Starter',
      enabledModules: ['sales', 'inventory'],
      enabledFeatures: ['CUSTOMERS'],
      limits: { CUSTOMERS: 100 },
      hasModuleRestrictions: true,
    });
    vi.mocked(accessService.getMyPermissions).mockResolvedValue({
      permissions: ['sales.invoices.view'],
      planCode: 'legacy',
      enabledModules: ['should-not-use'],
    });

    await syncSessionEntitlements();

    const state = usePermissionsStore.getState();
    expect(state.enabledModules).toEqual(['sales', 'inventory']);
    expect(state.planCode).toBe('starter');
    expect(state.hasModuleRestrictions).toBe(true);
    expect(state.enabledFeatures).toEqual(['CUSTOMERS']);
    expect(state.limits).toEqual({ CUSTOMERS: 100 });
    expect(state.permissions).toEqual(['sales.invoices.view']);
  });
});
