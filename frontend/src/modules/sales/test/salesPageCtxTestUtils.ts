import type { SalesPageContext } from "../hooks/useSalesPage";
import { computeSalesCollectionStatus, parseCashReceivedInput } from "../utils/salesCollectionStatus";
import { computeSalesConfigStatus } from "../utils/salesEmissionConfigStatus";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

type CtxLike = Partial<SalesPageContext> & Record<string, unknown>;

/**
 * Solo tests — completa un `SalesPageContext` simulado con los DERIVADOS que en producción calcula
 * `useSalesPage` (POS-COLLECTION-SSOT-01 / POS-CANEMIT-SSOT-01 / POS-EMISSION-TYPE-SNAPSHOT-01),
 * usando las MISMAS funciones puras del hook — así las suites de SalesPage no reimplementan las
 * reglas a mano. Cualquier campo presente en `ctx` (base u overrides del test) tiene prioridad.
 */
export function withPosDerivedCtx(rawCtx: object): SalesPageContext {
  const ctx = rawCtx as CtxLike;
  // Misma policy que el setup global de tests fija (setupPrecisionPolicy.ts).
  const policy = TEST_PRECISION_POLICY;
  const payments = (ctx.payments ?? []) as SalesPageContext["payments"];
  const paymentMethods = (ctx.paymentMethods ?? []) as SalesPageContext["paymentMethods"];
  const isCashMethodId =
    ctx.isCashMethodId ??
    ((id: string) =>
      paymentMethods.find((pm) => pm.id === id)?.affectsPhysicalCash === true);
  const editing = (ctx.editing ?? null) as SalesPageContext["editing"];
  const emissionType =
    ctx.emissionType !== undefined
      ? ctx.emissionType
      : editing
        ? editing.emissionType
        : (ctx.myCashSession?.emissionType ?? null);
  const cashReceivedInput = (ctx.cashReceivedInput ?? "") as string;
  const total = (ctx.summary as { total?: number } | undefined)?.total ?? 0;

  const collection =
    ctx.collection ??
    computeSalesCollectionStatus({
      total,
      payments,
      isCashMethod: isCashMethodId,
      cashReceived: parseCashReceivedInput(cashReceivedInput),
      tolerance: policy.settlementToleranceAmount,
      moneyDecimals: policy.moneyDecimals,
    });

  const configStatus =
    ctx.configStatus ??
    computeSalesConfigStatus({
      hasCashSession: (ctx.hasCashSession ?? null) as boolean | null,
      emissionType,
      docTypeCode: ctx.readOnly
        ? editing?.docTypeCode
        : (ctx.formWatch as { docTypeCode?: string } | undefined)?.docTypeCode,
      lines: (ctx.lines ?? []) as SalesPageContext["lines"],
      defaultSriPaymentCode: ctx.readOnly
        ? editing?.sriPaymentMethodCode
        : (ctx.formWatch as { sriPaymentMethodCode?: string } | undefined)
            ?.sriPaymentMethodCode,
      payments,
      paymentMethods,
    });

  return {
    ...ctx,
    emissionType,
    sessionEmissionType: ctx.sessionEmissionType ?? ctx.myCashSession?.emissionType ?? null,
    isElectronic: ctx.isElectronic ?? emissionType === "Electronic",
    isCashMethodId,
    paymentsForAdditionalMethod: ctx.paymentsForAdditionalMethod ?? (() => payments),
    cashReceivedInput,
    setCashReceivedInput: ctx.setCashReceivedInput ?? (() => {}),
    cashReceived: parseCashReceivedInput(cashReceivedInput),
    collection,
    configStatus,
    emitBlockers:
      ctx.emitBlockers ??
      (ctx.canEmit
        ? []
        : [{ source: "payment" as const, message: "Complete el cobro en Formas de Cobro." }]),
  } as unknown as SalesPageContext;
}
