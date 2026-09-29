/**
 * bankLookupFacade — superficie pública read-only del catálogo de bancos para consumidores
 * externos (finance: cuentas bancarias de la empresa).
 *
 * Expone únicamente el listado; nunca las mutaciones de bankService (create/update/...).
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * settings/banks/api/bankService.
 */
import { bankService } from "../api/bankService";
import type { BankDto } from "../api/bankService";

export type { BankDto };

export const bankLookupFacade = {
  list: bankService.list,
};
