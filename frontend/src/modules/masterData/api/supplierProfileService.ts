import { businessPartnerService } from './businessPartnerService';
import type { BusinessPartnerDto, SearchBusinessPartnersParams } from '../types/businessPartner.types';

/**
 * Perfiles proveedor — strangler sobre BusinessPartner hasta endpoints dedicados.
 */
export const supplierProfileService = {
  search: (params: Omit<SearchBusinessPartnersParams, 'isSupplier'> = {}) =>
    businessPartnerService.search({ ...params, isSupplier: true }),

  getByBusinessPartnerId: (businessPartnerId: string) =>
    businessPartnerService.getById(businessPartnerId),
};

export type { BusinessPartnerDto };
