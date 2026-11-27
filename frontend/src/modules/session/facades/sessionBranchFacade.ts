/**
 * sessionBranchFacade — superficie pública read-only de las sucursales autorizadas del usuario
 * en la empresa activa (no exige permisos de configuración). Los módulos externos deben importar
 * desde aquí, nunca directamente de session/api/sessionService.
 */
import { sessionService } from "../api/sessionService";
import type { SessionBranchDto } from "../../../types/session";

export type { SessionBranchDto };

export const sessionBranchFacade = {
  listAvailable: () => sessionService.getAvailableBranches(),
};
