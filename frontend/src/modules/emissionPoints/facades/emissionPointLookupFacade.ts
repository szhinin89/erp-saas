/** Public read-only emission-point lookup surface for other modules. */
import { emissionPointsService } from "../api/emissionPointsService";
import type { EmissionPointListItemDto } from "../api/emissionPointsService";

export type { EmissionPointListItemDto };

export const emissionPointLookupFacade = {
  list: emissionPointsService.list,
};
