import { companyManagementService } from "../company-management/api/companyManagementService";
import { loadDecimalConfig } from "../../lib/config/decimal.config";
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

  await Promise.allSettled([
    companyManagementService.getCurrent(),
    useSessionStore.getState().refresh(),
    loadDecimalConfig(),
    useElectronicInvoicingStatusStore.getState().refresh(),
  ]);
}
