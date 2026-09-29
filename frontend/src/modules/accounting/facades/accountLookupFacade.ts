/**
 * accountLookupFacade — superficie pública read-only del plan de cuentas para consumidores
 * externos (expenses: selector de cuenta por categoría/línea; sales: cuenta de forma de pago).
 *
 * Expone únicamente el listado de cuentas; nunca asientos, periodos ni mutaciones de
 * accountingApi. Los módulos externos deben importar desde aquí, nunca directamente de
 * accounting/api/accountingApi.
 */
import { accountingApi } from "../api/accountingApi";
import type { AccountDto } from "../api/accountingApi";

export type { AccountDto };

export const accountLookupFacade = {
  listAccounts: accountingApi.listAccounts,
};
