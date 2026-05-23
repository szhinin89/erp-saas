import { describe, expect, it } from 'vitest';
import { resolveCustomerOperationalMeta, resolveSupplierOperationalMeta } from './operationalLinkResolver';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

const baseBp: BusinessPartnerDto = {
  id: 'bp-1',
  legalName: 'ACME',
  identificationType: 'RUC',
  identificationNumber: '1790000000001',
  isCustomer: true,
  isSupplier: false,
  isActive: true,
};

describe('operationalLinkResolver', () => {
  it('customer selectable when legacyCustomerId present', () => {
    const meta = resolveCustomerOperationalMeta({
      ...baseBp,
      legacyCustomerId: 'cust-legacy-1',
    });
    expect(meta.selectable).toBe(true);
    expect(meta.legacyOperationalId).toBe('cust-legacy-1');
    expect(meta.missingOperationalLink).toBe(false);
  });

  it('customer not selectable without legacy link', () => {
    const meta = resolveCustomerOperationalMeta(baseBp);
    expect(meta.selectable).toBe(false);
    expect(meta.warningMessage).toContain('operacional');
  });

  it('supplier selectable when legacySupplierId present', () => {
    const meta = resolveSupplierOperationalMeta({
      ...baseBp,
      isCustomer: false,
      isSupplier: true,
      legacySupplierId: 'sup-legacy-1',
    });
    expect(meta.selectable).toBe(true);
    expect(meta.legacyOperationalId).toBe('sup-legacy-1');
  });
});
