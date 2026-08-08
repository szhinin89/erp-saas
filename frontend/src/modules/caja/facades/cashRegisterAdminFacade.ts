/**
 * cashRegisterAdminFacade — superficie pública de administración de cajas
 * (CashRegister) para consumidores externos (cashRegisters).
 *
 * A diferencia de cajaSessionLookupFacade, esta facade SÍ expone mutaciones
 * reales (create/update/disable/enable) porque cashRegisters administra el
 * ciclo de vida completo de CashRegister, cuya entidad vive en el módulo
 * caja. No es una facade de solo lectura — nombrarla "lookup" escondería
 * la mutación real. Los módulos externos deben importar desde aquí, nunca
 * directamente de caja/api/cajaService.
 */

import { cajaService } from "../api/cajaService";
import type {
  CashRegisterDto,
  CashRegisterActiveStatus,
  EmissionPointLookupForBranchDto,
} from "../api/cajaService";

export type {
  CashRegisterDto,
  CashRegisterActiveStatus,
  EmissionPointLookupForBranchDto,
};

export const cashRegisterAdminFacade = {
  listAllCashRegisters: cajaService.listAllCashRegisters,
  emissionPointLookupsByBranch: cajaService.emissionPointLookupsByBranch,
  createCashRegister: cajaService.createCashRegister,
  updateCashRegister: cajaService.updateCashRegister,
  disableCashRegister: cajaService.disableCashRegister,
  enableCashRegister: cajaService.enableCashRegister,
};
