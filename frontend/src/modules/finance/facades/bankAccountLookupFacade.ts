/**
 * bankAccountLookupFacade — superficie pública read-only de cuentas bancarias de la empresa
 * para consumidores externos (sales: pagos; supplier-payments: líneas de pago).
 *
 * Expone únicamente el listado; nunca las mutaciones de bankAccountService. Los módulos
 * externos deben importar desde aquí, nunca directamente de finance/api/bankAccountService.
 */
import { bankAccountService } from "../api/bankAccountService";
import type { CompanyBankAccountDto } from "../api/bankAccountService";

export type { CompanyBankAccountDto };

export const bankAccountLookupFacade = {
  list: bankAccountService.list,
};
