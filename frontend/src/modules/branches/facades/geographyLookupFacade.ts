/**
 * geographyLookupFacade — superficie pública read-only del catálogo geográfico
 * (país → provincia → cantón → parroquia) para consumidores externos (settings: página
 * de consulta de geografía; masterData: direcciones de socios de negocio).
 *
 * Owner: branches — los casos de uso `GetGeo*` y `GeographyItemDto` viven en
 * `ERP.Application/Modules/Branches` (expuestos por `GeographyController`). El único
 * cliente HTTP de geografía del frontend es `branchService`; no crear otro.
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * branches/api/branchService.
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
