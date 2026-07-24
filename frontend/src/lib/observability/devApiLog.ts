import { useAuthStore } from '../../store/authStore';

export type ApiRequestMode = 'masterdata' | 'legacy';

export type DevApiRequestLog = {
  endpoint: string;
  mode:     ApiRequestMode;
  method?:  string;
};

/** Dev picker log — V2: usa businessPartnerId directamente, sin legacyIds. */
export type DevMasterDataPickerLog = {
  kind:                 'customer' | 'supplier';
  businessPartnerId:    string;
  identificationNumber: string;
};

function sessionSlice() {
  const { user, companySessionVersion } = useAuthStore.getState();
  return {
    tenant_id:           user?.tenantId ?? null,
    company_id:              user?.companyId    ?? null,
    company_session_version: companySessionVersion,
  };
}

export function logDevApiRequest(payload: DevApiRequestLog): void {
  if (!import.meta.env.DEV) return;
  console.info('[erp.api]', {
    ...sessionSlice(),
    request_endpoint: payload.endpoint,
    request_mode:     payload.mode,
    http_method:      payload.method ?? 'GET',
  });
}

export function logDevMasterDataPicker(payload: DevMasterDataPickerLog): void {
  if (!import.meta.env.DEV) return;
  console.info('[erp.masterdata.picker]', { ...sessionSlice(), ...payload });
}
