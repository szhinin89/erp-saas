import { describe, expect, it, vi, beforeEach } from 'vitest';
import { syncSessionEntitlements } from './syncSessionEntitlements';
import { usePermissionsStore } from '../store/permissionsStore';

vi.mock('../services/entitlementsService', () => ({
  entitlementsService: {
    getMe: vi.fn(),
  },
}));

vi.mock('../services/accessService', () => ({
  accessService: {
    getMyPermissions: vi.fn(),
  },
}));

import { entitlementsService } from '../services/entitlementsService';
import { accessService } from '../services/accessService';

describe('syncSessionEntitlements', () => {
  beforeEach(() => {
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
