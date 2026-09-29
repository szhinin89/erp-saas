/**
 * companyLookupFacade — superficie pública read-only de empresas para consumidores
 * externos (auth: sincronización de empresa seleccionada; admin: filtro de sesiones).
 *
 * Expone únicamente el listado y la empresa actual; nunca las mutaciones de
 * companyManagementService (create/update/...). Los módulos externos deben importar
 * desde aquí, nunca directamente de company-management/api/companyManagementService.
 */
import { companyManagementService } from "../api/companyManagementService";

export const companyLookupFacade = {
  list: companyManagementService.list,
  getCurrent: companyManagementService.getCurrent,
};
