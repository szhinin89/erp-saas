import { businessPartnerService } from './businessPartnerService';
import type { BusinessPartnerDto, SearchBusinessPartnersParams } from '../types/businessPartner.types';

/**
 * Perfiles cliente — strangler sobre BusinessPartner hasta endpoints dedicados.
 * No expone rutas `/customer-profiles`; filtra `isCustomer` en búsqueda MasterData.
 */
export const customerProfileService = {
  search: (params: Omit<SearchBusinessPartnersParams, 'isCustomer'> = {}) =>
    businessPartnerService.search({ ...params, isCustomer: true }),

  getByBusinessPartnerId: (businessPartnerId: string) =>
    businessPartnerService.getById(businessPartnerId),
};

export type { BusinessPartnerDto };
