import { z } from "zod";

/**
 * COMPANY-PRECISION-POLICY-SSOT-01. Rangos EXACTOS del backend
 * (ERP.Domain.Configuration.Entities.CompanyPrecisionPolicy) — cualquier cambio aquí sin el
 * mismo cambio en backend queda atrapado por applyServerErrors en el submit, pero el objetivo es
 * que el usuario nunca llegue a ver ese error de servidor.
 */
export const precisionPolicySchema = z.object({
  profileType: z.enum(["StandardCommercial", "HighPrecision", "Custom"]),
  salesUnitPriceDecimals: z.coerce.number().int().min(2).max(8),
  purchaseUnitPriceDecimals: z.coerce.number().int().min(2).max(8),
  quantityDecimals: z.coerce.number().int().min(0).max(6),
  percentageDecimals: z.coerce.number().int().min(2).max(6),
  unitCostDecimals: z.coerce.number().int().min(2).max(8),
  averageCostDecimals: z.coerce.number().int().min(2).max(8),
  conversionFactorDecimals: z.coerce.number().int().min(2).max(8),
  settlementToleranceAmount: z.coerce.number().min(0).max(0.02),
});

export type PrecisionPolicyFormValues = z.infer<typeof precisionPolicySchema>;
