import { logDevApiRequest } from '../../../lib/observability/devApiLog';
import { mapBusinessPartnerToCustomerPickerRow } from '../adapters/businessPartnerCustomerAdapter';
import {
  mapBusinessPartnerToSupplierPickerRow,
  type SupplierPickerOption,
} from '../adapters/businessPartnerSupplierAdapter';
import type { CustomerPickerRow, SupplierPickerRow } from '../types/pickerRow.types';
import { businessPartnerService } from './businessPartnerService';
import type {
  BusinessPartnerDto,
  BusinessPartnerPagedResult,
  CompanyBpSettingsDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
  UpdateSupplierProfileBody,
} from '../types/businessPartner.types';

export const businessPartnerFacade = {
  async searchCustomersForPicker(search?: string): Promise<CustomerPickerRow[]> {
    logDevApiRequest({ endpoint: '/api/master/business-partners', mode: 'masterdata', method: 'GET' });
    const bps = await businessPartnerService.search({ q: search, isActive: true, isCustomer: true, take: 100 });
    return bps.map(mapBusinessPartnerToCustomerPickerRow);
  },

  searchCustomers: (search?: string) => businessPartnerFacade.searchCustomersForPicker(search),

  async searchSuppliersForPicker(search?: string): Promise<SupplierPickerRow[]> {
    logDevApiRequest({ endpoint: '/api/master/business-partners', mode: 'masterdata', method: 'GET' });
    const bps = await businessPartnerService.search({ q: search, isActive: true, isSupplier: true, take: 100 });
    return bps.map(mapBusinessPartnerToSupplierPickerRow);
  },

  searchSuppliers: (search?: string) => businessPartnerFacade.searchSuppliersForPicker(search),

  async searchSupplierPickerOptions(search?: string): Promise<SupplierPickerOption[]> {
    const suppliers = await businessPartnerFacade.searchSuppliersForPicker(search);
    return suppliers.map((s) => ({
      id: s.pickerMeta.legacyOperationalId ?? s.id,
      razonSocial: s.tradeName?.trim() || s.legalName,
      selectable: s.pickerMeta.selectable,
      missingOperationalLink: s.pickerMeta.missingOperationalLink,
      warningMessage: s.pickerMeta.warningMessage,
    }));
  },

  searchBusinessPartners: (params?: Parameters<typeof businessPartnerService.search>[0]) =>
    businessPartnerService.search(params),

  searchBusinessPartnersPaged: (params?: Parameters<typeof businessPartnerService.searchPaged>[0]): Promise<BusinessPartnerPagedResult> =>
    businessPartnerService.searchPaged(params),

  getBusinessPartner: (id: string) => businessPartnerService.getById(id),

  createBusinessPartner: (body: CreateBusinessPartnerBody) => businessPartnerService.create(body),

  disableBusinessPartner: (id: string) => businessPartnerService.disable(id),

  updateBusinessPartner: (id: string, body: UpdateBusinessPartnerBody) =>
    businessPartnerService.update(id, body),

  activateBusinessPartner: (id: string) => businessPartnerService.activate(id),

  getCompanySettings: (id: string): Promise<CompanyBpSettingsDto | null> =>
    businessPartnerService.getCompanySettings(id),

  upsertCompanySettings: (
    id: string,
    body: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean },
  ) => businessPartnerService.upsertCompanySettings(id, body),

  addRole: (id: string, asCustomer: boolean, asSupplier: boolean) =>
    businessPartnerService.addRole(id, asCustomer, asSupplier),

  updateCustomerNotes: (id: string, notes: string | null) =>
    businessPartnerService.updateCustomerNotes(id, notes),

  updateSupplierProfile: (id: string, body: UpdateSupplierProfileBody) =>
    businessPartnerService.updateSupplierProfile(id, body),
};

export type {
  BusinessPartnerDto,
  CompanyBpSettingsDto,
  UpdateBusinessPartnerBody,
  UpdateSupplierProfileBody,
  SupplierPickerOption,
  CustomerPickerRow,
  SupplierPickerRow,
};
