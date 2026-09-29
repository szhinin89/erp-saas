/**
 * cashRegisterLookupFacade — superficie pública read-only de cajas (CashRegister) para
 * consumidores externos (finance: cobros/reembolsos; supplier-payments: líneas de pago).
 *
 * Expone únicamente el listado; nunca las mutaciones de cajaService (para eso existe
 * cashRegisterAdminFacade). Los módulos externos deben importar desde aquí, nunca
 * directamente de caja/api/cajaService.
 */
import { cajaService } from "../api/cajaService";
import type { CashRegisterDto } from "../api/cajaService";

export type { CashRegisterDto };

export const cashRegisterLookupFacade = {
  getCashRegisters: cajaService.getCashRegisters,
};
