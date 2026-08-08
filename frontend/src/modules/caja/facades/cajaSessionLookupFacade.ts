/**
 * cajaSessionLookupFacade — superficie pública read-only de la sesión de
 * caja propia para consumidores externos (sales).
 *
 * Expone únicamente la consulta de la sesión propia; nunca las mutaciones
 * de cajaService (open/close/...). Los módulos externos deben importar
 * desde aquí, nunca directamente de caja/api/cajaService.
 */

import { cajaService } from "../api/cajaService";
import type { CashSessionDto } from "../api/cajaService";

export type { CashSessionDto };

export const cajaSessionLookupFacade = {
  getMy: cajaService.getMy,
};
