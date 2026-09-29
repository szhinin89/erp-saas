/**
 * geographyLookupFacade — superficie pública read-only del catálogo geográfico
 * (país → provincia → cantón → parroquia) para consumidores externos (settings: página
 * de consulta de geografía).
 *
 * Expone únicamente las lecturas encadenadas de branchService; los módulos externos deben
 * importar desde aquí, nunca directamente de branches/api/branchService.
 */

import { branchService } from "../api/branchService";
import type { GeographyItemDto } from "../api/branchService";

export type { GeographyItemDto };

export const geographyLookupFacade = {
  countries: branchService.countries,
  provinces: branchService.provinces,
  cantons: branchService.cantons,
  parishes: branchService.parishes,
};
