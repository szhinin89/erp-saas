/**
 * membershipLookupFacade — superficie pública read-only de las membresías de la empresa
 * operativa para consumidores externos (admin: sesiones).
 *
 * Expone únicamente el listado; nunca las mutaciones de membershipService
 * (upsert/revoke/lookup). Los módulos externos deben importar desde aquí, nunca directamente de
 * access/users/api/membershipService.
 */

import { membershipService } from "../users/api/membershipService";
import type { CompanyUserMembershipAdminDto } from "../users/api/membershipService";

export type { CompanyUserMembershipAdminDto };

export const membershipLookupFacade = {
  list: membershipService.list,
};
