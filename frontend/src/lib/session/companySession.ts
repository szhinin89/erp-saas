import { useAuthStore } from "../../store/authStore";

/** Incrementa versión de sesión operativa; resetea UI sensible si es necesario. */
export function bumpCompanyOperationalSession(): void {
  useAuthStore.getState().incrementCompanySession();
}
