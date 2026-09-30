/**
 * establishmentLookupFacade — superficie pública read-only de establecimientos para consumidores
 * externos (emissionPoints: selector de establecimiento del punto de emisión).
 *
 * Expone únicamente el lookup de establecimientos activos; nunca las mutaciones ni el listado
 * administrativo de establishmentService. Los módulos externos deben importar desde aquí, nunca
 * directamente de establishments/api/establishmentService.
 */
import { establishmentService } from "../api/establishmentService";
import type { EstablishmentLookupDto } from "../api/establishmentService";

export type { EstablishmentLookupDto };

export const establishmentLookupFacade = {
  lookups: establishmentService.lookups,
};
