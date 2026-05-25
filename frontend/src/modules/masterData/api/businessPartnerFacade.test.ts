import { describe, expect, it } from 'vitest';
import { mapBusinessPartnerToCustomerPickerRow } from '../adapters/businessPartnerCustomerAdapter';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

describe('businessPartnerCustomerAdapter', () => {
  it('maps business partner to customer picker row', () => {
    const bp: BusinessPartnerDto = {
      id: 'bp-1',
      legalName: 'ACME',
      identificationType: 'RUC',
      identificationNumber: '1790000000001',
      isCustomer: true,
      isSupplier: false,
      isActive: true,
    };
    const mapped = mapBusinessPartnerToCustomerPickerRow(bp);
    expect(mapped.id).toBe('bp-1');
    expect(mapped.fullName).toBe('ACME');
  });
});
