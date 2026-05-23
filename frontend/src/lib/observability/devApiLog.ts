import { useAuthStore } from '../../store/authStore';

export type ApiRequestMode = 'masterdata' | 'legacy';

export type DevApiRequestLog = {
  endpoint: string;
  mode: ApiRequestMode;
  method?: string;
};

export type DevMasterDataPickerLog = {
  kind: 'customer' | 'supplier';
  businessPartnerId: string;
  identificationNumber: string;
  legacyCustomerId?: string | null;
  legacySupplierId?: string | null;
  selectable: boolean;
};

function sessionSlice() {
  const { user, companySessionVersion } = useAuthStore.getState();
  return {
    subscriber_id: user?.subscriberId ?? null,
    company_id: user?.companyId ?? null,
    company_session_version: companySessionVersion,
  };
}

/** Logs estructurados DEV — sin JWT ni tokens. */
export function logDevApiRequest(payload: DevApiRequestLog): void {
  if (!import.meta.env.DEV) return;
  console.info('[erp.api]', {
    ...sessionSlice(),
    request_endpoint: payload.endpoint,
    request_mode: payload.mode,
    http_method: payload.method ?? 'GET',
  });
}

export function logDevMasterDataPicker(payload: DevMasterDataPickerLog): void {
  if (!import.meta.env.DEV) return;
  console.info('[erp.masterdata.picker]', {
    ...sessionSlice(),
    ...payload,
  });
}
