/**
 * cashFundingRequestFacade — superficie pública de solicitudes de efectivo para consumidores
 * externos (supplier-payments): solo crear la solicitud y la ruta de su detalle. Listado, detalle y
 * resolución (entregar/rechazar/cancelar) viven en la pantalla de Caja; los módulos externos
 * importan desde aquí, nunca directamente de caja/api.
 */

import { cashFundingRequestService } from "../api/cashFundingRequestService";
import type {
  CashFundingRequestDto,
  CreateCashFundingRequestPayload,
} from "../api/cashFundingRequestService";

export type { CashFundingRequestDto, CreateCashFundingRequestPayload };

export const CASH_FUNDING_REQUESTS_ROUTE = "/treasury/cash/funding-requests";

export const cashFundingRequestRoute = (id: string) => `${CASH_FUNDING_REQUESTS_ROUTE}/${id}`;

export const cashFundingRequestFacade = {
  create: cashFundingRequestService.create,
};
