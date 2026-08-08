/**
 * branchLookupFacade — superficie pública read-only de sucursales para
 * consumidores externos (access, cashRegisters, establishments, security).
 *
 * Expone únicamente el listado; nunca las mutaciones de branchService
 * (create/update/disable/enable) ni el detalle/geografía. Los módulos
 * externos deben importar desde aquí, nunca directamente de
 * branches/api/branchService.
 */

import { branchService } from "../api/branchService";
import type { BranchListItemDto, CatalogActiveStatus } from "../api/branchService";

export type { BranchListItemDto, CatalogActiveStatus };

export const branchLookupFacade = {
  list: branchService.list,
};
