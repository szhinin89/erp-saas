import { companyManagementService } from "../company-management/api/companyManagementService";
import { clearPrecisionPolicy, loadPrecisionPolicy } from "../../lib/config/precisionPolicy.config";
import { bumpCompanyOperationalSession } from "../../lib/session/companySession";
import { logDevSessionContext } from "../../lib/session/devSessionLog";
import { useAuthStore } from "../../store/authStore";
import { useElectronicInvoicingStatusStore } from "../../store/electronicInvoicingStatusStore";
import { useSessionStore } from "../../store/sessionStore";
import type { AuthResponse } from "../../types/auth";
import { clearOperationalContext } from "./clearOperationalContext";

export async function syncCompanySelection(auth: AuthResponse): Promise<void> {
  useAuthStore.getState().login(auth);
  bumpCompanyOperationalSession();
  logDevSessionContext("switch-company");
  clearOperationalContext();
  // La política de la empresa anterior nunca debe sobrevivir al cambio de empresa.
  clearPrecisionPolicy();

  // Un fallo al cargar la política NO se traga: SessionBootstrap la vuelve a exigir para la empresa
  // activa y, si falla, bloquea la app con error + reintentar (sin valores por defecto).
  await Promise.allSettled([
    companyManagementService.getCurrent(),
    useSessionStore.getState().refresh(),
    loadPrecisionPolicy(),
    useElectronicInvoicingStatusStore.getState().refresh(),
  ]);
}
