import { describe, expect, it } from 'vitest';
import { mapBusinessPartnerToCustomerPickerRow } from '../adapters/businessPartnerCustomerAdapter';
import type { BusinessPartnerSummaryDto } from '../types/businessPartner.types';

describe('businessPartnerCustomerAdapter V2', () => {
  const bp: BusinessPartnerSummaryDto = {
    id:                   'bp-1',
    identificationType:   '04',
    identificationNumber: '1790000000001',
    legalName:            'ACME Corporation S.A.',
    tradeName:            'ACME',
    personType:           'Legal',
    countryCode:          'EC',
    isActive:             true,
    createdAt:            '2024-01-01T00:00:00Z',
  };

  it('uses businessPartnerId as the operational id (no legacy bridge)', () => {
    const row = mapBusinessPartnerToCustomerPickerRow(bp);
    expect(row.id).toBe('bp-1');
  });

  it('uses tradeName as fullName when available', () => {
    const row = mapBusinessPartnerToCustomerPickerRow(bp);
    expect(row.fullName).toBe('ACME');
  });

  it('falls back to legalName when tradeName is null', () => {
    const row = mapBusinessPartnerToCustomerPickerRow({ ...bp, tradeName: null });
    expect(row.fullName).toBe('ACME Corporation S.A.');
  });

  it('marks hasCustomerRole=true (the search already filtered by role)', () => {
    const row = mapBusinessPartnerToCustomerPickerRow(bp);
    expect(row.hasCustomerRole).toBe(true);
  });

  it('preserves isActive from the BP summary', () => {
    const row = mapBusinessPartnerToCustomerPickerRow({ ...bp, isActive: false });
    expect(row.isActive).toBe(false);
  });
});
