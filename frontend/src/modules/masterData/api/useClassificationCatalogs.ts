import { useAsync } from "../../../hooks/useAsync";
import { classificationCatalogFacade } from "../facades/classificationCatalogFacade";
import type { ClassificationCatalogItem } from "../facades/classificationCatalogFacade";

export type ClassificationCatalogOption = ClassificationCatalogItem;

// Sin fallback hardcodeado a propósito (CLASS-BP-CATALOGS-01): los 12 catálogos de clasificación
// de BusinessPartner viven en backend/BD (tenant+company-scoped). Duplicar sus códigos aquí y
// sustituirlos silenciosamente ante error de red mostraría opciones desincronizadas de la empresa
// activa sin que el usuario lo note. Mismo patrón que useSriPaymentMethods.ts.

export function useCustomerCategories() {
  const state = useAsync(() => classificationCatalogFacade.customerCategories());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useCustomerSegments() {
  const state = useAsync(() => classificationCatalogFacade.customerSegments());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useCustomerCreditRatings() {
  const state = useAsync(() => classificationCatalogFacade.customerCreditRatings());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useLoyaltyTiers() {
  const state = useAsync(() => classificationCatalogFacade.loyaltyTiers());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useCustomerInvoiceFormats() {
  const state = useAsync(() => classificationCatalogFacade.customerInvoiceFormats());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useCustomerClassifications() {
  const state = useAsync(() => classificationCatalogFacade.customerClassifications());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useSupplierCategories() {
  const state = useAsync(() => classificationCatalogFacade.supplierCategories());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useSupplierTypes() {
  const state = useAsync(() => classificationCatalogFacade.supplierTypes());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useSupplierRisks() {
  const state = useAsync(() => classificationCatalogFacade.supplierRisks());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useSupplierRatings() {
  const state = useAsync(() => classificationCatalogFacade.supplierRatings());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function usePrimaryGoodTypes() {
  const state = useAsync(() => classificationCatalogFacade.primaryGoodTypes());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}

export function useSupplierSegments() {
  const state = useAsync(() => classificationCatalogFacade.supplierSegments());
  return { options: state.data ?? [], loading: state.loading, error: state.error };
}
