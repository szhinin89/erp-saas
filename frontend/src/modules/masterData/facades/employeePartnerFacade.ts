/**
 * employeePartnerFacade — superficie pública de masterData para el alta de empleados (EMP-E7):
 * buscar y leer socios de negocio, y las dos mutaciones que el wizard oficial necesita para el
 * rol Employee. Cada operación la autoriza el backend con los permisos de MasterData
 * (view / create / update); el módulo de empleados nunca importa masterData/api ni components.
 */
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { RoleTypeEnum } from "../types/businessPartner.types";
import type {
  BusinessPartnerSummaryDto,
  CreateBusinessPartnerBody,
} from "../types/businessPartner.types";

export { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
export type { BusinessPartnerSummaryDto, CreateBusinessPartnerBody };

const SEARCH_LIMIT = 20;

export const employeePartnerFacade = {
  /** Socios activos que ya tienen el rol Employee activo. */
  searchEligible: (
    q: string,
    signal?: AbortSignal,
  ): Promise<BusinessPartnerSummaryDto[]> =>
    businessPartnerFacade.searchBusinessPartners(
      { q, isActive: true, roles: [RoleTypeEnum.Employee], take: SEARCH_LIMIT },
      signal,
    ),
  getById: (id: string): Promise<BusinessPartnerSummaryDto> =>
    businessPartnerFacade.getBusinessPartner(id),
  create: (body: CreateBusinessPartnerBody): Promise<BusinessPartnerSummaryDto> =>
    businessPartnerFacade.createBusinessPartner(body),
  assignEmployeeRole: (id: string) =>
    businessPartnerFacade.assignRole(id, { roleType: RoleTypeEnum.Employee }),
};
