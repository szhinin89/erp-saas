import { describe, expect, it } from 'vitest';
import { mapBusinessPartnerToLegacyCustomer } from '../adapters/businessPartnerCustomerAdapter';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

describe('businessPartnerCustomerAdapter', () => {
  it('uses legacy customer id when provided', () => {
    const bp: BusinessPartnerDto = {
      id: 'bp-1',
      legalName: 'ACME',
      identificationType: 'RUC',
      identificationNumber: '1790000000001',
      isCustomer: true,
      isSupplier: false,
      isActive: true,
    };
    const mapped = mapBusinessPartnerToLegacyCustomer(bp, 'legacy-99');
    expect(mapped.id).toBe('legacy-99');
    expect(mapped.fullName).toBe('ACME');
  });
});
