import { apiGet, apiPost } from "../../lib/apiEnvelope";

/** Estado de inicialización del sistema (primer administrador). */
export interface SetupStatus {
  isInitialized: boolean;
  adminEmail?: string | null;
}

export interface CreateInitialAdminPayload {
  username: string;
  firstName: string;
  lastName: string;
  email: string | null;
  password: string;
  setupToken: string;
}

/** Cliente de la configuración inicial (`/api/v1/setup`): estado y alta del primer administrador. */
export const setupService = {
  getStatus: () => apiGet<SetupStatus>("/api/v1/setup/status"),
  createInitialAdmin: (payload: CreateInitialAdminPayload) =>
    apiPost<unknown>("/api/v1/setup/admin", payload),
};
