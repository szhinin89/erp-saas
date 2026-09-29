/**
 * priceListDefaultFacade — superficie pública para reasignar la lista de precios por defecto
 * desde Configuración → Ventas (configuracion/empresa).
 *
 * A diferencia de priceListLookupFacade, esta facade SÍ expone una mutación real
 * (setDefault, único punto de escritura del default) — nombrarla "lookup" escondería la
 * escritura. El listado sigue saliendo de priceListLookupFacade. Los módulos externos deben
 * importar desde aquí, nunca directamente de pricing/api/pricingService.
 */
import { priceListService } from "../api/pricingService";

export const priceListDefaultFacade = {
  setDefault: priceListService.setDefault,
};
