/**
 * operationalPreferencesLookupFacade — superficie pública read-only de preferencias
 * operacionales para consumidores externos (caja: movimientos manuales; sales: emisión).
 *
 * Expone únicamente la lectura; nunca updatePreferences. Los módulos externos deben importar
 * desde aquí, nunca directamente de configuracion/operaciones/api/operationalPreferencesService.
 */
import { operationalPreferencesService } from "../operaciones/api/operationalPreferencesService";
import type { OperationalPreferencesDto } from "../operaciones/api/operationalPreferencesService";

export type { OperationalPreferencesDto };

export const operationalPreferencesLookupFacade = {
  getPreferences: operationalPreferencesService.getPreferences,
};
