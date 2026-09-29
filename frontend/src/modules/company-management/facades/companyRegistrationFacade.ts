/**
 * companyRegistrationFacade — superficie pública de alta de empresas para consumidores
 * externos (admin-core: creación de empresa desde la consola global).
 *
 * Expone únicamente la creación; nunca update/activación. Los módulos externos deben
 * importar desde aquí, nunca directamente de company-management/api/companyManagementService.
 */
import { companyManagementService } from "../api/companyManagementService";

export const companyRegistrationFacade = {
  create: companyManagementService.create,
};
