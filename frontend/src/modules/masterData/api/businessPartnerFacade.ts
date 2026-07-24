/**
 * businessPartnerFacade — capa de orquestación para componentes UI.
 *
 * Centraliza todas las operaciones de BusinessPartner V2.
 * Los componentes importan desde aquí, nunca directamente de los servicios.
 *
 * ELIMINADO (V1 legacy):
 *   - IBusinessPartnerOperationalLinkEnricher
 *   - searchCustomersForPicker con legacyId
 *   - addRole con flags booleanos (asCustomer/asSupplier)
 *   - updateCustomerNotes / updateSupplierProfile (reemplazados por updateRoleNotes / updateSupplierConfig)
 *   - getCompanySettings / upsertCompanySettings (endpoint renombrado a trading-settings)
 */

import {
  bpContactService,
  bpLocationService,
  bpRoleService,
  bpTradingSettingsService,
  businessPartnerService,
} from './businessPartnerService';
import {
  mapBusinessPartnerToCustomerPickerRow,
  mapBusinessPartnerToSupplierPickerRow,
} from '../adapters/businessPartnerCustomerAdapter';
import type {
  AssignRoleBody,
  BlockBody,
  BpContactDto,
  BpLocationDto,
  BusinessPartnerDetailDto,
  BusinessPartnerPagedResult,
  BusinessPartnerRoleDto,
  BusinessPartnerSummaryDto,
  CarrierConfigBody,
  CompanyBpTradingSettingsDto,
  CreateBusinessPartnerBody,
  CreateContactBody,
  CreateLocationBody,
  CustomerConfigBody,
  CustomerPickerRow,
  SearchBusinessPartnersParams,
  SupplierClassificationBody,
  SupplierConfigBody,
  SupplierPickerRow,
  UpdateBusinessPartnerBody,
  UpdateContactBody,
  UpdateIdentificationBody,
  UpdateLocationBody,
  UpdateRoleNotesBody,
  UpsertTradingSettingsBody,
} from '../types/businessPartner.types';
import { RoleTypeEnum } from '../types/businessPartner.types';

// ── Identity ──────────────────────────────────────────────────────────────────

export const businessPartnerFacade = {
  // ── Búsqueda e identidad ───────────────────────────────────────────────────

  searchBusinessPartners: (params?: SearchBusinessPartnersParams): Promise<BusinessPartnerSummaryDto[]> =>
    businessPartnerService.search(params),

  searchBusinessPartnersPaged: (params?: SearchBusinessPartnersParams): Promise<BusinessPartnerPagedResult> =>
    businessPartnerService.searchPaged(params),

  getBusinessPartner: (id: string): Promise<BusinessPartnerDetailDto> =>
    businessPartnerService.getById(id),

  createBusinessPartner: (body: CreateBusinessPartnerBody): Promise<BusinessPartnerSummaryDto> =>
    businessPartnerService.create(body),

  updateBusinessPartner: (id: string, body: UpdateBusinessPartnerBody): Promise<BusinessPartnerSummaryDto> =>
    businessPartnerService.updateProfile(id, body),

  updateIdentification: (id: string, body: UpdateIdentificationBody): Promise<BusinessPartnerSummaryDto> =>
    businessPartnerService.updateIdentification(id, body),

  activateBusinessPartner: (id: string): Promise<boolean> =>
    businessPartnerService.activate(id),

  deactivateBusinessPartner: (id: string): Promise<boolean> =>
    businessPartnerService.deactivate(id),

  // ── Pickers (Customer / Supplier) ─────────────────────────────────────────
  //
  // V2: businessPartnerId ES el ID operacional — no hay legacyId bridge.
  // Los BP son siempre seleccionables si tienen el rol activo.

  searchCustomersForPicker: async (q?: string): Promise<CustomerPickerRow[]> => {
    const bps = await businessPartnerService.search({
      q,
      isActive: true,
      roles: [RoleTypeEnum.Customer],
      take: 100,
    });
    return bps.map(mapBusinessPartnerToCustomerPickerRow);
  },

  searchCustomers: (q?: string): Promise<CustomerPickerRow[]> =>
    businessPartnerFacade.searchCustomersForPicker(q),

  searchSuppliersForPicker: async (q?: string): Promise<SupplierPickerRow[]> => {
    const bps = await businessPartnerService.search({
      q,
      isActive: true,
      roles: [RoleTypeEnum.Supplier],
      take: 100,
    });
    return bps.map(mapBusinessPartnerToSupplierPickerRow);
  },

  searchSuppliers: (q?: string): Promise<SupplierPickerRow[]> =>
    businessPartnerFacade.searchSuppliersForPicker(q),

  // ── Roles ──────────────────────────────────────────────────────────────────

  getRoles: (bpId: string, onlyActive?: boolean): Promise<BusinessPartnerRoleDto[]> =>
    bpRoleService.list(bpId, onlyActive),

  assignRole: (bpId: string, body: AssignRoleBody): Promise<BusinessPartnerRoleDto> =>
    bpRoleService.assign(bpId, body),

  revokeRole: (bpId: string, roleId: string): Promise<boolean> =>
    bpRoleService.revoke(bpId, roleId),

  updateSupplierConfig: (bpId: string, roleId: string, config: SupplierConfigBody): Promise<BusinessPartnerRoleDto> =>
    bpRoleService.updateSupplierConfig(bpId, roleId, config),

  updateCarrierConfig: (bpId: string, roleId: string, config: CarrierConfigBody): Promise<BusinessPartnerRoleDto> =>
    bpRoleService.updateCarrierConfig(bpId, roleId, config),

  updateRoleNotes: (bpId: string, roleId: string, body: UpdateRoleNotesBody): Promise<boolean> =>
    bpRoleService.updateNotes(bpId, roleId, body),

  updateCustomerConfig: (bpId: string, roleId: string, config: CustomerConfigBody): Promise<BusinessPartnerRoleDto> =>
    bpRoleService.updateCustomerConfig(bpId, roleId, config),

  updateSupplierClassification: (bpId: string, roleId: string, config: SupplierClassificationBody): Promise<BusinessPartnerRoleDto> =>
    bpRoleService.updateSupplierClassification(bpId, roleId, config),

  // ── Locations ──────────────────────────────────────────────────────────────

  getLocations: (bpId: string, onlyActive?: boolean): Promise<BpLocationDto[]> =>
    bpLocationService.list(bpId, onlyActive),

  getLocation: (bpId: string, locationId: string): Promise<BpLocationDto> =>
    bpLocationService.getById(bpId, locationId),

  createLocation: (bpId: string, body: CreateLocationBody): Promise<BpLocationDto> =>
    bpLocationService.create(bpId, body),

  updateLocation: (bpId: string, locationId: string, body: UpdateLocationBody): Promise<BpLocationDto> =>
    bpLocationService.update(bpId, locationId, body),

  setLocationPrimary: (bpId: string, locationId: string): Promise<boolean> =>
    bpLocationService.setPrimary(bpId, locationId),

  activateLocation: (bpId: string, locationId: string): Promise<boolean> =>
    bpLocationService.activate(bpId, locationId),

  deactivateLocation: (bpId: string, locationId: string): Promise<boolean> =>
    bpLocationService.deactivate(bpId, locationId),

  // ── Contacts ───────────────────────────────────────────────────────────────

  getContacts: (bpId: string, onlyActive?: boolean): Promise<BpContactDto[]> =>
    bpContactService.list(bpId, onlyActive),

  getContact: (bpId: string, contactId: string): Promise<BpContactDto> =>
    bpContactService.getById(bpId, contactId),

  createContact: (bpId: string, body: CreateContactBody): Promise<BpContactDto> =>
    bpContactService.create(bpId, body),

  updateContact: (bpId: string, contactId: string, body: UpdateContactBody): Promise<BpContactDto> =>
    bpContactService.update(bpId, contactId, body),

  setContactPrimary: (bpId: string, contactId: string): Promise<boolean> =>
    bpContactService.setPrimary(bpId, contactId),

  activateContact: (bpId: string, contactId: string): Promise<boolean> =>
    bpContactService.activate(bpId, contactId),

  deactivateContact: (bpId: string, contactId: string): Promise<boolean> =>
    bpContactService.deactivate(bpId, contactId),

  // ── Trading Settings ───────────────────────────────────────────────────────

  getTradingSettings: (bpId: string): Promise<CompanyBpTradingSettingsDto> =>
    bpTradingSettingsService.get(bpId),

  upsertTradingSettings: (bpId: string, body: UpsertTradingSettingsBody): Promise<CompanyBpTradingSettingsDto> =>
    bpTradingSettingsService.upsert(bpId, body),

  blockBusinessPartner: (bpId: string, body: BlockBody): Promise<boolean> =>
    bpTradingSettingsService.block(bpId, body),

  unblockBusinessPartner: (bpId: string): Promise<boolean> =>
    bpTradingSettingsService.unblock(bpId),
};

// ── Re-exports para compatibilidad con imports existentes ─────────────────────

export type {
  BusinessPartnerSummaryDto,
  BusinessPartnerDetailDto,
  BusinessPartnerRoleDto,
  BpLocationDto,
  BpContactDto,
  CompanyBpTradingSettingsDto,
  CustomerPickerRow,
  SupplierPickerRow,
  UpdateBusinessPartnerBody,
  SupplierConfigBody,
};
