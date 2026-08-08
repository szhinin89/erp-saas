/**
 * branchInvoiceDefaultsFacade — superficie pública de configuración de
 * defaults de factura por sucursal para consumidores externos (branches).
 *
 * Expone lectura y la mutación real de guardado (upsert) — no es una
 * facade de solo lectura, es una facade de configuración/acción, porque
 * `branches` necesita persistir la bodega por defecto de la sucursal, cuyo
 * endpoint vive en `configuracion`. Los módulos externos deben importar
 * desde aquí, nunca directamente de configuracion/empresa/api/orgConfigService.
 */

import { orgConfigService } from "../api/orgConfigService";
import type {
  BranchInvoiceOrgSettingsDto,
  UpsertBranchInvoiceOrgSettingsPayload,
} from "../api/orgConfigService";

export type { BranchInvoiceOrgSettingsDto, UpsertBranchInvoiceOrgSettingsPayload };

export const branchInvoiceDefaultsFacade = {
  getBranchInvoiceDefaults: orgConfigService.getBranchInvoiceDefaults,
  upsertBranchInvoiceDefaults: orgConfigService.upsertBranchInvoiceDefaults,
};
