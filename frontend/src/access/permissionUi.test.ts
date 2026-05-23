import { describe, expect, it } from 'vitest';
import {
  canShowPermissionKey,
  hasUnrestrictedPermissionSnapshot,
  isTenantAdminRole,
} from './permissionUi';

describe('permissionUi', () => {
  const has = (keys: string[]) => (key: string) =>
    keys.includes('*') || keys.some((p) => p === key);

  it('isTenantAdminRole recognizes Admin and SuperAdmin', () => {
    expect(isTenantAdminRole('Admin')).toBe(true);
    expect(isTenantAdminRole('SuperAdmin')).toBe(true);
    expect(isTenantAdminRole('User')).toBe(false);
  });

  it('hasUnrestrictedPermissionSnapshot detects wildcard from backend', () => {
    expect(hasUnrestrictedPermissionSnapshot(['*'])).toBe(true);
    expect(hasUnrestrictedPermissionSnapshot(['sales.invoices.view'])).toBe(false);
  });

  it('canShowPermissionKey uses backend wildcard', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: ['*'],
        has: has(['*']),
      }),
    ).toBe(true);
  });

  it('canShowPermissionKey uses explicit permission', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: ['sales.invoices.view'],
        has: has(['sales.invoices.view']),
      }),
    ).toBe(true);
  });

  it('canShowPermissionKey denies empty snapshot (no Admin fallback)', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: [],
        has: has([]),
      }),
    ).toBe(false);
  });

  it('canShowPermissionKey denies while permissions are syncing', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: ['sales.invoices.view'],
        has: has(['sales.invoices.view']),
        permissionsSyncing: true,
      }),
    ).toBe(false);
  });
});
