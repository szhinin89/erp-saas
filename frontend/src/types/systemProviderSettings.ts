/**
 * Datos del proveedor del sistema de facturación electrónica — singleton único por instancia
 * del ERP (no por empresa/tenant). Ver backend/src/ERP.Domain/Modules/Configuration/Entities/SystemProviderSettings.cs.
 */
export interface SystemProviderSettings {
  ruc: string | null;
  legalName: string | null;
  ciiuCode: string | null;
  enabled: boolean;
  /** ISO date (YYYY-MM-DD), o null si no está fijada. */
  effectiveDate: string | null;
  isFullyConfigured: boolean;
  updatedAtUtc: string | null;
}

export interface UpdateSystemProviderSettingsPayload {
  ruc: string | null;
  legalName: string | null;
  ciiuCode: string | null;
  effectiveDate: string | null;
  enabled: boolean;
}
