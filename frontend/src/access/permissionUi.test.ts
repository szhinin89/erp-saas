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
        role: 'Admin',
      }),
    ).toBe(true);
  });

  it('canShowPermissionKey uses explicit permission', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: ['sales.invoices.view'],
        has: has(['sales.invoices.view']),
        role: 'User',
      }),
    ).toBe(true);
  });

  it('canShowPermissionKey empty snapshot tenant admin fallback (UI parity)', () => {
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: [],
        has: has([]),
        role: 'Admin',
      }),
    ).toBe(true);
    expect(
      canShowPermissionKey('sales.invoices.view', {
        permissions: [],
        has: has([]),
        role: 'User',
      }),
    ).toBe(false);
  });

  it('canShowPermissionKey admin-only fallback option', () => {
    expect(
      canShowPermissionKey('saas.companies.create', {
        permissions: [],
        has: has([]),
        role: 'SuperAdmin',
        adminFallbackRoles: ['Admin'],
      }),
    ).toBe(false);
    expect(
      canShowPermissionKey('saas.companies.create', {
        permissions: [],
        has: has([]),
        role: 'Admin',
        adminFallbackRoles: ['Admin'],
      }),
    ).toBe(true);
  });
});
