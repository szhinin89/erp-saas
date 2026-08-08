/**
 * rideGenerationFacade — superficie pública de generación/acceso al RIDE
 * para consumidores externos (sales; a futuro inventario, activos, POS —
 * ver comentario de diseño en rideService.ts).
 *
 * Expone las 3 operaciones del cliente Ride tal cual (getOrGenerate y
 * regenerate tienen efectos secundarios de generación de documento — no es
 * una facade de solo lectura). Los módulos externos deben importar desde
 * aquí, nunca directamente de ride/api/rideService.
 */

import { rideService } from "../api/rideService";
import type { RideGenerationResultDto, RideOutcome } from "../api/rideService";

export type { RideGenerationResultDto, RideOutcome };

export const rideGenerationFacade = {
  getOrGenerate: rideService.getOrGenerate,
  regenerate: rideService.regenerate,
  getContentBlob: rideService.getContentBlob,
};
