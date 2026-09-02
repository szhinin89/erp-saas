import type { NavigateFunction } from "react-router-dom";
import { GLOBAL_TENANT_ID } from "../../access/permissionUi";
import type { AuthResponse } from "../../types/auth";

/**
 * Fase H — aplica una sesión ya autenticada (authStore.login) y redirige según el estado de la
 * respuesta, exactamente igual para LoginPage y CompletePasswordResetPage: nunca se reimplementa
 * esta rama en dos lugares. Requiere que `payload.requiresPasswordReset` ya sea false/undefined
 * (una sesión real) — el caller decide antes de invocar esto si corresponde en vez ir a
 * /complete-password-reset.
 */
export function completeLoginNavigation(
  payload: AuthResponse,
  login: (response: AuthResponse) => void,
  navigate: NavigateFunction,
) {
  login(payload);

  const isGlobalCoreSession =
    payload.tenantId === GLOBAL_TENANT_ID && !payload.companyId;

  if (isGlobalCoreSession) {
    navigate("/companies/new", { replace: true });
    return;
  }

  if (payload.requiresCompanySelection) {
    navigate("/select-company", { replace: true });
    return;
  }

  navigate("/dashboard", { replace: true });
}
