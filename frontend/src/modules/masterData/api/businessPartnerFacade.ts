import { logDevApiRequest } from '../../../lib/observability/devApiLog';
import { customerService, type Customer } from '../../customers/api/customerService';
import { supplierService, type Supplier } from '../../compras/suppliers/api/supplierService';
import { mapBusinessPartnerToCustomerPickerRow } from '../adapters/businessPartnerCustomerAdapter';
import {
  mapBusinessPartnerToSupplierPickerRow,
  type SupplierPickerOption,
} from '../adapters/businessPartnerSupplierAdapter';
import {
  isMasterDataCustomersEnabled,
  isMasterDataSuppliersEnabled,
} from '../config/masterDataFeatureFlags';
import type { CustomerPickerRow } from '../types/pickerRow.types';
import type { SupplierPickerRow } from '../types/pickerRow.types';
import { businessPartnerService } from './businessPartnerService';
import { isMasterDataFallbackError } from './masterDataErrors';
import type {
  BusinessPartnerDto,
  CompanyBpSettingsDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
  UpdateSupplierProfileBody,
} from '../types/businessPartner.types';

async function searchCustomersViaMaster(search?: string): Promise<CustomerPickerRow[]> {
  logDevApiRequest({
    endpoint: '/api/master/business-partners',
    mode: 'masterdata',
    method: 'GET',
  });
  const bps = await businessPartnerService.search({
    q: search,
    isActive: true,
    isCustomer: true,
    take: 100,
  });
  return bps.map(mapBusinessPartnerToCustomerPickerRow);
}

async function searchSuppliersViaMaster(search?: string): Promise<SupplierPickerRow[]> {
  logDevApiRequest({
    endpoint: '/api/master/business-partners',
    mode: 'masterdata',
    method: 'GET',
  });
  const bps = await businessPartnerService.search({
    q: search,
    isActive: true,
    isSupplier: true,
    take: 100,
  });
  return bps.map(mapBusinessPartnerToSupplierPickerRow);
}

function legacyCustomersToPickerRows(customers: Customer[]): CustomerPickerRow[] {
  logDevApiRequest({ endpoint: '/api/sales/customers', mode: 'legacy', method: 'GET' });
  return customers.map((c) => ({
    ...c,
    pickerMeta: {
      businessPartnerId: c.id,
      legacyOperationalId: c.id,
      selectable: true,
      missingOperationalLink: false,
    },
  }));
}

function legacySuppliersToPickerRows(suppliers: Supplier[]): SupplierPickerRow[] {
  logDevApiRequest({ endpoint: '/api/purchases/suppliers', mode: 'legacy', method: 'GET' });
  return suppliers.map((s) => ({
    ...s,
    pickerMeta: {
      businessPartnerId: s.id,
      legacyOperationalId: s.id,
      selectable: true,
      missingOperationalLink: false,
    },
  }));
}

/**
 * Fachada strangler MasterData ↔ legacy.
 * Feature flags: `masterdata.customers.enabled` / `masterdata.suppliers.enabled` (entitlements).
 */
export const businessPartnerFacade = {
  /** Búsqueda para pickers de ventas — incluye filas no seleccionables (sin vínculo legacy). */
  async searchCustomersForPicker(search?: string): Promise<CustomerPickerRow[]> {
    if (!isMasterDataCustomersEnabled()) {
      const legacy = await customerService.getAll(search);
      return legacyCustomersToPickerRows(legacy);
    }
    try {
      return await searchCustomersViaMaster(search);
    } catch (err) {
      if (isMasterDataFallbackError(err)) {
        const legacy = await customerService.getAll(search);
        return legacyCustomersToPickerRows(legacy);
      }
      throw err;
    }
  },

  searchCustomers: (search?: string) => businessPartnerFacade.searchCustomersForPicker(search),

  async searchSuppliersForPicker(search?: string): Promise<SupplierPickerRow[]> {
    if (!isMasterDataSuppliersEnabled()) {
      const legacy = await supplierService.getAll(search);
      return legacySuppliersToPickerRows(legacy);
    }
    try {
      return await searchSuppliersViaMaster(search);
    } catch (err) {
      if (isMasterDataFallbackError(err)) {
        const legacy = await supplierService.getAll(search);
        return legacySuppliersToPickerRows(legacy);
      }
      throw err;
    }
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

  /** CRUD legacy — delegación explícita para módulos no migrados. */
  legacy: {
    customers: customerService,
    suppliers: supplierService,
  },
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
