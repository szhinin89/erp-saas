/**
 * companyLookupFacade — superficie pública read-only de la empresa activa para consumidores
 * externos (auth: sincronización de empresa seleccionada).
 *
 * Expone únicamente la empresa actual; nunca las mutaciones de
 * companyManagementService (create). Los módulos externos deben importar
 * desde aquí, nunca directamente de company-management/api/companyManagementService.
 */
import { companyManagementService } from "../api/companyManagementService";

export const companyLookupFacade = {
  getCurrent: companyManagementService.getCurrent,
};
